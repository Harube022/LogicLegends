#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Graph;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Topics;
using LogicLegends.OpenArchive.Topics.Editor;
using Photon.Pun;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ArchiveShowcaseCapture : MonoBehaviour
{
    const string Output = "Library/ArchiveShowcaseCapture";
    static ArchiveShowcaseCapture current;
    static ArchiveTopicSession session;
    static Camera gameplayCamera;
    static Behaviour brain;
    static float began;
    static string chapter;
    RecorderController recorder;

    public static string Status { get; private set; }
    public static void Begin()
    {
        session = ArchiveTopicSession.Current;
        if (!Application.isPlaying || PhotonNetwork.InRoom || session == null || !session.Ready || current != null)
            throw new InvalidOperationException("Use the saved MultiplayerGameplay scene in offline Play Mode.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/cues.tsv", "");
        current = new GameObject("Archive Showcase Recording Only").AddComponent<ArchiveShowcaseCapture>();
        current.StartCoroutine(current.Guard());
    }
    IEnumerator Guard()
    {
        var stack = new System.Collections.Generic.Stack<IEnumerator>();
        stack.Push(Record());
        while (stack.Count != 0)
        {
            object wait = null;
            try
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator child) { stack.Push(child); continue; }
                wait = top.Current;
            }
            catch (Exception exception)
            {
                Status = "Failed " + chapter + ": " + exception;
                File.WriteAllText(Output + "/status.txt", Status);
                Debug.LogError(Status);
                Stop();
                yield break;
            }
            yield return wait;
        }
        Stop();
        Status = "Complete | " + (Time.unscaledTime - began).ToString("F2");
        File.WriteAllText(Output + "/status.txt", Status);
    }
    void Stop()
    {
        if (recorder != null) { recorder.StopRecording(); recorder = null; }
        if (brain != null) brain.enabled = true;
    }
    void OnDestroy() => Stop();

    static void Cue(string title, string detail)
    {
        chapter = title;
        Status = title + " | " + (Time.unscaledTime - began).ToString("F2");
        File.AppendAllText(Output + "/cues.tsv", FormattableString.Invariant($"{Time.unscaledTime-began:F3}\t{title}\t{detail}\n"));
        File.WriteAllText(Output + "/status.txt", Status);
    }
    static WaitForSeconds Hold(float seconds) => new WaitForSeconds(seconds);
    static IEnumerator WaitFor(Func<bool> ready, float seconds, string label)
    {
        float until = Time.unscaledTime + seconds;
        while (!ready() && Time.unscaledTime < until) yield return null;
        if (!ready()) throw new InvalidOperationException(label);
    }
    static void Place(Player player, Vector3 position, Quaternion rotation)
    {
        if (player == null) return;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(position, rotation);
        if (cc != null) cc.enabled = true;
        player.ResetMotionAfterTeleport();
        Physics.SyncTransforms();
    }
    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0;
        return Vector3.Distance(a,b);
    }
    static void Frame(Vector3 at, Vector3 back, float distance=9, float height=4)
    {
        gameplayCamera = Camera.main ?? gameplayCamera;
        if (gameplayCamera == null) return;
        brain = gameplayCamera.GetComponents<Behaviour>().FirstOrDefault(b=>b.GetType().Name=="CinemachineBrain");
        if (brain != null) brain.enabled = false;
        var eye = at + back.normalized * distance + Vector3.up * height;
        gameplayCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(at+Vector3.up*1.15f-eye));
    }
    static void MoveTo(ArchiveTopicStation station, Vector3 local)
    {
        ArchiveTopicPlayChecks.MoveTo(station, local);
    }
    static void Send(int topic,ArchiveAction action,int a=-1,int b=-1)
    {
        var actor = session.State.actors.FirstOrDefault(p=>p.actor==session.LocalActor);
        var command = new ArchiveCommand {topic=topic,revision=session.State.stations[topic].revision,sequence=(actor?.lastSequence??0)+1,action=action,a=a,b=b};
        if (!session.Accept(session.LocalActor, command)) throw new InvalidOperationException("Rejected " + action + " (" + a + "," + b + "): " + session.State.stations[topic].feedback);
    }
    static Button ButtonByName(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(b=>b.name==name);
    static void UseLatestStudent(Player player,bool female)
    {
        if (player == null) return;
        var basePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(female?"Assets/Prefabs/PREFABS/CHARACTERS/Player_Female.prefab":"Assets/Prefabs/PREFABS/CHARACTERS/Player.prefab");
        var profile=basePrefab.GetComponentsInChildren<IntegratedSkinProfile>(true).FirstOrDefault(p=>string.Equals(p.itemID,female?"female_default":"male_default",StringComparison.OrdinalIgnoreCase));
        if (profile == null) throw new InvalidOperationException("Updated default student is missing from the integrated Player prefab.");
        Transform visual=profile.transform;
        while(visual.parent!=null&&visual.name!="Latest Skin Visual") visual=visual.parent;
        if(visual==basePrefab.transform) throw new InvalidOperationException("Updated student visual root is missing.");
        foreach(var old in player.GetComponentsInChildren<Animator>(true))
            if(old.transform!=player.transform) old.gameObject.SetActive(false);
        if(player.GetComponent<PlayerCarryRig>()==null&&player.GetHoldPoint()!=null)player.gameObject.AddComponent<PlayerCarryRig>();
        var copy=Object.Instantiate(visual.gameObject,player.transform,false);
        copy.SetActive(true);
        var live=copy.GetComponentInChildren<IntegratedSkinProfile>(true);
        for(var target=live.transform;target!=copy.transform;target=target.parent)target.gameObject.SetActive(true);
        var capsule=player.GetComponent<CharacterController>();
        if(IntegratedSkinProfile.TryVisualBounds(copy.transform,out var bounds)&&bounds.size.y>.05f)
            copy.transform.position+=Vector3.up*(LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(capsule).y-bounds.min.y+.035f);
        File.AppendAllText(Output+"/characters.txt",player.name+" uses "+live.itemID+" ("+live.name+")\n");
    }
    void StartCapture()
    {
        var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        settings.SetRecordModeToManual();
        settings.FrameRate = 24;
        settings.CapFrameRate = true;
        settings.ExitPlayMode = false;
        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "Open Archive Gameplay";
        movie.Enabled = true;
        movie.CaptureAudio = true;
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1280, OutputHeight = 720 };
        movie.OutputFile = Path.GetFullPath(Output + "/OpenArchive_Raw");
        settings.AddRecorderSettings(movie);
        recorder = new RecorderController(settings);
        recorder.PrepareRecording();
        if (!recorder.StartRecording()) throw new InvalidOperationException("Unity Recorder failed to start.");
        began = Time.unscaledTime;
    }
    IEnumerator Record()
    {
        session.Initialize(314159);
        UseLatestStudent(Player.LocalInstance, false);
        foreach (var script in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (script.GetType().Name.EndsWith("TestControls") || script.GetType().Name.EndsWith("DevelopmentUI")) script.enabled = false;
        foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            if (rect.name == "Show_Hide_Test_Tools" || rect.name == "Test_Tools_Content") rect.parent.gameObject.SetActive(false);
        var learning = ArchiveLearningUI.Current;
        if (learning != null)
            typeof(ArchiveLearningUI).GetField("onboardingDismissed",BindingFlags.NonPublic|BindingFlags.Instance)?.SetValue(learning,true);
        var view = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (view != null) EditorWindow.GetWindow(view).Focus();
        StartCapture();
        Cue("THE OPEN ARCHIVE","Logic Legends multiplayer · offline showcase");
        var film = session.GetComponent<ArchiveMatchCinematic>() ?? Object.FindFirstObjectByType<ArchiveMatchCinematic>();
        if (film != null && !film.IsPlaying) film.PreviewSummoning();
        yield return Hold(2);
        if (film != null) yield return WaitFor(()=>!film.IsPlaying,35,"The summoning cinematic did not finish.");
        session.RequestAdminTest(ArchiveAdminTestAction.StartMatchClock);
        session.hunting.Request(KhezuCommand.Sleep);
        session.hunting.enabled = false;
        session.worldProgression.suppressThreatsForVerification = true;
        session.suppressDefeatForVerification = true;
        var player = Player.LocalInstance;
        Cue("A SHARED MATCH","Ten topics. One clock. Follow the markers to the Books.");
        Frame(player.transform.position, -player.transform.forward, 12, 7);
        yield return Hold(5);
        string[] titles = {"Propositional Logic","Truth Tables","Rules of Inference","Formal and Informal Proofs","Methods of Proof","Mathematical Induction","Sets","Functions and Relations","Graphs","Trees"};
        string[] summaries = {"Match the statement to its logical connective.","Place truth values in the correct table cells.","Complete the premises and choose the inference rule.","Classify proof statements and arrange the reasoning.","Choose the proof method and assemble its steps.","Establish the base case, hypothesis and induction step.","Place elements into the correct regions.","Build mappings and classify their properties.","Follow graph routes and answer graph questions.","Connect nodes and identify properties of trees."};
        for (int topic=0;topic<10;topic++)
        {
            var station=session.stations[topic];
            var state=session.State.stations[topic];
            Cue("T"+(topic+1)+" · "+titles[topic],summaries[topic]);
            MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            Frame(station.transform.TransformPoint(ArchiveTopicStation.BookPosition),-station.transform.forward,8,4.5f);
            yield return Hold(3);
            for (int round=0;round<ArchiveTopicCatalog.RoundCount(topic);round++)
            {
                MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
                station.points.First(p=>p.role==ArchivePointRole.Book).Interact();
                yield return Hold(round==0 ? 5f : 1.7f);
                if (state.phase!=1) throw new InvalidOperationException("Book did not activate round "+(round+1)+". "+state.feedback);
                while (state.round==round)
                {
                    var q=session.Question(topic);
                    var solution=ArchiveTopicChecks.Solution(q);
                    if (q.activity==ArchiveActivity.Choice)
                    {
                        ArchiveTopicUI.Current.Close();
                        var answerPosition=station.ItemPosition(q.correctChoice,q);
                        MoveTo(station,answerPosition);
                        Frame(station.transform.TransformPoint(answerPosition),-station.transform.forward,7,3.5f);
                        yield return Hold(.65f);
                        Send(topic,ArchiveAction.BeginChoice,q.correctChoice);
                        yield return Hold(session.answerChannelSeconds+.55f);
                    }
                    else
                    {
                            if(q.activity==ArchiveActivity.Method&&ArchiveMethodPresentation.NeedsMethod(q)){MoveTo(station,ArchiveMethodPresentation.MethodPosition(solution.selection));Send(topic,ArchiveAction.Select,solution.selection);}
                            if(q.regionPlacement)
                            {
                                for(int i=0;i<solution.placements.Length;i++)if(solution.placements[i]>=0)
                                {
                                    MoveTo(station,station.CurrentItemPosition(i,q,state));Send(topic,ArchiveAction.Pick,i);yield return Hold(.27f);
                                    MoveTo(station,station.SlotPosition(solution.placements[i],q));Send(topic,ArchiveAction.Place,solution.placements[i]);yield return Hold(.27f);
                                }
                            }
                            else for(int slot=0;slot<solution.placements.Length;slot++)
                            {
                                int item=solution.placements[slot];MoveTo(station,station.CurrentItemPosition(item,q,state));Send(topic,ArchiveAction.Pick,item);yield return Hold(.27f);
                                MoveTo(station,station.SlotPosition(slot,q));Send(topic,ArchiveAction.Place,slot);yield return Hold(.27f);
                            }
                            if(q.mapping!=null)
                            {
                                if(q.mapping.questionKind==MappingQuestionKind.ClassifyFunction){MoveTo(station,ArchiveMappingPresentation.AnswerPosition(solution.selection));Send(topic,ArchiveAction.Select,solution.selection);}
                                else if(q.mapping.questionKind==MappingQuestionKind.SelectProperties)
                                {if(solution.flags==0){MoveTo(station,ArchiveMappingPresentation.AnswerPosition(3));Send(topic,ArchiveAction.Select,0);}else foreach(int flag in new[]{2,4,8})if((solution.flags&flag)!=0){MoveTo(station,ArchiveMappingPresentation.AnswerPosition(Array.IndexOf(new[]{2,4,8},flag)));Send(topic,ArchiveAction.Select,flag);}}
                                else foreach(int link in solution.links.Except(state.links).ToArray())
                                {
                                    MoveTo(station,station.InputPosition(link/100));Send(topic,ArchiveAction.Select,link/100);yield return Hold(.27f);
                                    MoveTo(station,station.OutputPosition(link%100));Send(topic,ArchiveAction.Connect,link/100,link%100);yield return Hold(.27f);
                                }
                            }
                            if(q.tree!=null)
                            {
                                MoveTo(station,Vector3.zero);
                                if(q.tree.Editable)
                                {
                                    foreach(int link in state.links.Except(solution.links).ToArray()){Send(topic,ArchiveAction.Select,link/100);Send(topic,ArchiveAction.Select,link%100);Send(topic,ArchiveAction.Remove,link/100,link%100);}
                                    foreach(int link in solution.links.Except(state.links).ToArray()){Send(topic,ArchiveAction.Select,link/100);Send(topic,ArchiveAction.Select,link%100);Send(topic,ArchiveAction.Connect,link/100,link%100);}
                                }
                                if(q.tree.type==LogicLegends.Trees.TreeChallengeType.Identify)Send(topic,ArchiveAction.Select,solution.selection);
                                if(solution.flags>=0)Send(topic,ArchiveAction.Select,100+solution.flags);
                            }
                            if(q.graph!=null)
                            {
                                if(q.graph.type==GraphChallengeType.Degree){MoveTo(station,Vector3.zero);Send(topic,ArchiveAction.Select,solution.selection);}
                                else
                                {
                                    var start=q.nodes[solution.route[0]]+ArchiveTopicStation.GraphOffset;MoveTo(station,new Vector3(start.x,0,start.y));yield return Hold(.2f);Send(topic,ArchiveAction.RetryRoute);
                                    for(int i=1;i<solution.route.Count;i++)
                                    {
                                        var end=q.nodes[solution.route[i]]+ArchiveTopicStation.GraphOffset;var target=station.transform.TransformPoint(new Vector3(end.x,0,end.y));
                                        var cc=Player.LocalInstance.GetComponent<CharacterController>();float until=Time.realtimeSinceStartup+35;
                                        while(HorizontalDistance(Player.LocalInstance.transform.position,target)>.18f&&Time.realtimeSinceStartup<until)
                                        {
                                            var delta=target-Player.LocalInstance.transform.position;delta.y=0;cc.Move(delta.normalized*Mathf.Min(.35f,delta.magnitude));yield return null;
                                        }
                                        yield return Hold(.15f);
                                    }
                                    if (!state.continuous || !state.route.SequenceEqual(solution.route)) throw new InvalidOperationException("Graph route did not record.");
                                }
                            }
                            yield return Hold(round==0 ? 2.5f : 1.6f); Send(topic,ArchiveAction.Submit); yield return Hold(round==0 ? 2.3f : .85f);

                    }
                    if(state.round==round&&!(topic==2&&state.subphase==1))throw new InvalidOperationException("Round did not advance. "+state.feedback);
                }
            }
            ArchiveTopicUI.Current.Close();
            if (state.phase!=2) throw new InvalidOperationException("Topic did not complete.");
            Frame(station.transform.position,-station.transform.forward,14,8);
            Cue("T"+(topic+1)+" COMPLETE",titles[topic]+" · Shared progress "+(topic+1)+"/10");
            yield return Hold(3.3f);
            if(topic==0)
            {
                MoveTo(station,ArchiveRewardView.ChestLocalPosition);
                Frame(station.transform.TransformPoint(ArchiveRewardView.ChestLocalPosition),-station.transform.forward,6,3.5f);
                Cue("REWARD CHEST","The final solver can claim the supply earned by completing the topic.");
                yield return Hold(3);
                if (session.CanClaimChest(session.LocalActor,topic)) session.RequestChest(topic);
                yield return Hold(3);
            }
            if (topic==6) yield return Night();
        }
        ArchiveTopicUI.Current.Close();
        yield return FinishMatch();
    }
    IEnumerator Night()
    {
        Cue("NIGHTFALL","Seven completed topics bring out the second guardian.");
        session.worldProgression.suppressThreatsForVerification = false;
        session.RequestAdminTest(ArchiveAdminTestAction.TeleportToNest);
        Frame(session.hunting.transform.position,-session.hunting.transform.forward,19,8);
        session.RequestAdminTest(ArchiveAdminTestAction.ReplayEntrance);
        yield return Hold(3);
        float until=Time.unscaledTime+22;
        while(session.NightEntranceActive&&Time.unscaledTime<until) yield return null;
        session.RequestAdminTest(ArchiveAdminTestAction.SleepMonster);
        session.RequestAdminTest(ArchiveAdminTestAction.SleepMonster,1);
        session.worldProgression.suppressThreatsForVerification = true;
        Cue("TEAM SUPPLIES","Offline practice teammate · team signals and flashlight demonstration");
        session.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,session.LocalActor);
        session.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate);
        var local=Player.LocalInstance;var partner=session.ActorPlayer(-2);
        UseLatestStudent(partner, true);
        Place(partner,local.transform.position+local.transform.right*2.5f,local.transform.rotation);
        Frame(local.transform.position,-local.transform.forward,8,3);
        var team=ArchiveTeamCoordinationUI.Current;
        if(team!=null)
        {
            team.SetPartyPanelOpen(true); yield return Hold(3);
            team.SetPartyPanelOpen(false);
            team.TogglePingMenu(); yield return Hold(2);
            team.SendPing(ArchiveTeamPingKind.HelpHere); yield return Hold(3);
        }
        session.RequestEquipItem((int)ArchiveSurvivalItem.Flashlight);
        yield return Hold(1);
        session.RequestUseEquippedItem();
        yield return Hold(4);
        session.RequestUseEquippedItem();
        Cue("GUARDIAN CAPTURE","Offline encounter preview · solve the DM1 escape challenge.");
        session.RequestAdminTest(ArchiveAdminTestAction.PreviewBarrier);
        until=Time.unscaledTime+12;
        while(ArchiveLearningUI.Current!=null&&!ArchiveLearningUI.Current.BarrierVisible&&Time.unscaledTime<until)yield return null;
        if(ArchiveLearningUI.Current!=null&&ArchiveLearningUI.Current.BarrierVisible)
        {
            yield return Hold(5);
            ButtonByName("Barrier_Next_Hint")?.onClick.Invoke(); yield return Hold(3);
            ButtonByName("Return_To_Question")?.onClick.Invoke(); yield return Hold(1);
            var problem=session.BarrierProblem();
            if (problem!=null)
            {
                int correct=Enumerable.Range(0,problem.options.Length).First(i=>problem.Validate(i,out _));
                ButtonByName("Barrier_Choice_"+correct)?.onClick.Invoke(); yield return Hold(2);
                ButtonByName("Confirm_Barrier_Answer")?.onClick.Invoke(); yield return Hold(3);
            }
        }
        session.RequestAdminTest(ArchiveAdminTestAction.ReleaseCapture);
        Cue("RESCUE AT THE NEST","A teammate can use a revival kit to bring a downed player back.");
        session.RequestAdminTest(ArchiveAdminTestAction.DownAtNest,-2);
        yield return Hold(2);
        partner=session.ActorPlayer(-2);
        if(partner!=null)
        {
            Place(local,partner.transform.position+Vector3.right*2,local.transform.rotation);
            Frame(partner.transform.position,Vector3.back,7,3);
            session.RequestEquipItem((int)ArchiveSurvivalItem.RevivalKit);
            yield return Hold(3);
            session.RequestUseEquippedItem(-2);
            yield return Hold(4);
        }
        session.RequestAdminTest(ArchiveAdminTestAction.SleepMonster);
        session.RequestAdminTest(ArchiveAdminTestAction.SleepMonster,1);
    }
    IEnumerator FinishMatch()
    {
        var portal=session.exitPortal;
        Cue("THE ESCAPE PORTAL","All ten topics are complete. Every surviving teammate must enter.");
        yield return WaitFor(()=>portal!=null&&portal.Formed,20,"The portal did not form after ten completed topics.");
        var local=Player.LocalInstance;
        var partner=session.ActorPlayer(-2);
        var ground=portal.transform.position;
        var forward=portal.transform.forward;
        var facing=Quaternion.LookRotation(forward);
        Place(local,ground-forward*11-portal.transform.right*2,facing);
        Place(partner,ground-forward*8+portal.transform.right*2,facing);
        Frame(ground+Vector3.up*4,-forward+portal.transform.right*.32f,30,12);
        yield return Hold(5);
        if(partner!=null&&!session.HasEscapedActor(-2))
        {
            yield return WalkInto(partner,ground+portal.transform.right*2,forward);
            yield return Hold(1.8f);
        }
        yield return WalkInto(local,ground-portal.transform.right*2,forward);
        yield return WaitFor(()=>session.TeamWon,12,"The team did not receive a victory receipt.");
        Cue("VICTORY","Every surviving teammate escaped. Offline demonstration.");
        yield return Hold(15);
    }
    static IEnumerator WalkInto(Player player,Vector3 target,Vector3 forward)
    {
        var cc=player.GetComponent<CharacterController>();
        for (int i=0;i<90 && cc!=null && cc.enabled && HorizontalDistance(player.transform.position,target)>1;i++)
        {
            cc.Move(forward*.20f);
            yield return null;
        }
        if(cc!=null&&cc.enabled&&!session.exitPortal.ContainsPlayer(player))
            Place(player,target+Vector3.up*.45f,Quaternion.LookRotation(forward));
    }
}
#endif

