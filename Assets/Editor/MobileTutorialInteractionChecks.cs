using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in saved-scene checks through InputSystem touch events, not Button.onClick shortcuts.</summary>
public static class MobileTutorialInteractionChecks
{
    const string Flag="Mobile.TutorialQA", Report="Library/MobileTutorialInteractionChecks.txt";
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly string[] Scenes={"TruthTable","PRELIM","RulesOfInference","Formal vs. Informal Proofs","Direct&Indirect","Induction","Setssss","Functions and Relations","Graph","LogicGarden"};
    static readonly string[] History={"FunctionsRelations.LastVariant.","LogicLegends.Induction.LastVariant.","LogicLegends.ProofsLibrary.LastVariant.","LogicLegends.Graph.LastVariant.","LogicLegends.Trees.LastVariant."};
    static Touchscreen touchscreen;
    static int finger;
    [MenuItem("Logic Legends/Mobile/Check Quizzes and Tutorial Touch")]
    public static void Begin()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Exit Play Mode and save the scene first.");
        SessionState.SetString(Flag+".Scene",SceneManager.GetActiveScene().path);
        SessionState.SetInt(Flag+".Size",BookMapMobileChecks.GetSize());SessionState.SetBool(Flag,true);
        foreach(var prefix in History)for(int i=0;i<5;i++)
        {var key=prefix+i;SessionState.SetBool(Flag+key,PlayerPrefs.HasKey(key));SessionState.SetInt(Flag+key+".Value",PlayerPrefs.GetInt(key));}
        File.WriteAllText(Report,"Saved-scene mobile quiz and tutorial checks\n");
        EditorSceneManager.OpenScene("Assets/Scenes/TruthTable.unity");EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]
    static void Register(){EditorApplication.playModeStateChanged-=Restore;EditorApplication.playModeStateChanged+=Restore;}
    static void Restore(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Flag,false))return;
        SessionState.SetBool(Flag,false);SceneManager.sceneLoaded-=Loaded;
        var testFlags=BindingFlags.Static|BindingFlags.NonPublic;
        foreach(string field in new[]{"ValidationTransport","ValidationUid","ValidationEmail"})
            typeof(TrialBackendClient).GetField(field,testFlags).SetValue(null,null);
        BookMapMobileChecks.SetSize(SessionState.GetInt(Flag+".Size",6));Crystal.SafeArea.Sim=Crystal.SafeArea.SimDevice.None;
        foreach(var prefix in History)for(int i=0;i<5;i++)
        {var key=prefix+i;if(SessionState.GetBool(Flag+key,false))PlayerPrefs.SetInt(key,SessionState.GetInt(Flag+key+".Value",0));else PlayerPrefs.DeleteKey(key);}
        PlayerPrefs.Save();string scene=SessionState.GetString(Flag+".Scene","Assets/Scenes/TruthTable.unity");EditorApplication.delayCall+=()=>EditorSceneManager.OpenScene(scene);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if(!SessionState.GetBool(Flag,false))return;
        // Keep QA independent from real authentication, trial wallets and Admin permissions.
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        typeof(TrialBackendClient).GetField("ValidationTransport",flags).SetValue(null,(Func<string,string,System.Threading.Tasks.Task<string>>)((a,b)=>System.Threading.Tasks.Task.FromResult("{}")));
        typeof(TrialBackendClient).GetField("ValidationUid",flags).SetValue(null,null);
        StageSelectionState.SelectForEditorSpawn(2);SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;
    }
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(!SessionState.GetBool(Flag,false))return;
        foreach(var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if((behaviour.GetType().Namespace??"").StartsWith("Photon.Pun.Demo.Cockpit"))behaviour.enabled=false;
        if(Player.LocalInstance==null)
        {
            var avatar=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
            avatar.GetComponent<MultiplayerAvatarSync>().enabled=false;avatar.GetComponent<PlayerEquipmentLoader>().enabled=false;
            foreach(var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true))skin.gameObject.SetActive(skin.name=="UNIFORM_MALE");
            avatar.GetComponent<CharacterController>().enabled=true;
            var spawner=Find<LevelSpawner>();var spawn=spawner!=null?Field<Transform>(spawner,"spawnPoint"):null;
            if(spawn==null)spawn=GameObject.Find("Proofs Player Spawn")?.transform??GameObject.Find("Trial Player Spawn")?.transform;
            if(spawn!=null)avatar.transform.SetPositionAndRotation(spawn.position,spawn.rotation);
        }
        if(UnityEngine.Object.FindFirstObjectByType<MobileTutorialQaRunner>()==null)
        {var runner=new GameObject("Mobile Tutorial QA").AddComponent<MobileTutorialQaRunner>();UnityEngine.Object.DontDestroyOnLoad(runner);runner.StartCoroutine(Guard(Run()));}
    }
    static IEnumerator Guard(IEnumerator work)
    {
        var stack=new Stack<IEnumerator>();stack.Push(work);
        while(stack.Count>0)
        {
            object next;
            try{var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}next=current.Current;}
            catch(Exception error){File.AppendAllText(Report,"FAIL "+error+"\n");Debug.LogException(error);EditorApplication.isPlaying=false;yield break;}
            if(next is IEnumerator child)stack.Push(child);else yield return next;
        }
        Log("ALL MOBILE CHECKS PASSED");SceneManager.sceneLoaded-=Loaded;EditorApplication.isPlaying=false;
    }
    static IEnumerator Run()
    {
        Application.runInBackground=true;touchscreen=InputSystem.AddDevice<Touchscreen>();finger=10;
        foreach(string scene in Scenes)
        {
            Time.timeScale=1;GameInput.Instance?.SetGameplayInputBlocked(false);
            StageSelectionState.SelectForEditorSpawn(scene=="PRELIM"?1:scene=="TruthTable"?2:scene=="LogicGarden"?1:TrialCatalog.ForScene(scene).Number);
            if(SceneManager.GetActiveScene().name!=scene)yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/"+scene+".unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSecondsRealtime(2.5f);
            float until=Time.realtimeSinceStartup+30;
            while(Time.realtimeSinceStartup<until&&(Player.LocalInstance==null||Find<LogicLegends.Induction.InductionStageManager>()!=null&&!Find<LogicLegends.Induction.InductionStageManager>().Ready||Find<LogicLegends.Graph.GraphStageManager>()!=null&&!Find<LogicLegends.Graph.GraphStageManager>().Ready))yield return null;
            until=Time.realtimeSinceStartup+12;
            while(TeleportManager.Instance!=null&&TeleportManager.Instance.IsTransitioning&&Time.realtimeSinceStartup<until)yield return null;
            Check(TeleportManager.Instance==null||!TeleportManager.Instance.IsTransitioning,"spawn fade releases its input blocker "+scene);
            Check(Player.LocalInstance!=null&&GameInput.Instance!=null,"local player and controls "+scene);
            if(scene=="TruthTable"||scene=="PRELIM")
            {
                until=Time.realtimeSinceStartup+12;
                while(Time.realtimeSinceStartup<until&&!(scene=="TruthTable"?Find<TruthTableTutorial>().IsOpen:Find<PropositionalLogicTutorial>().IsOpen))yield return null;
            }
            MonoBehaviour coach=Prepare(scene);yield return null;yield return null;
            Check(coach!=null,"tutorial controller "+scene);
            CheckHandNotPremature(coach,scene);
            var hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.name=="InteractButton");
            BeginHand(coach,scene,(RectTransform)hand.transform);yield return null;yield return null;
            Check(Step(coach)=="Interaction","enter hand-only step "+scene);
            Check(!UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Any(t=>t.text.Contains("Interact with me")),"no obsolete practice label "+scene);
            var handPanel=Panel(coach,scene);
            TutorialSpotlightChecks.Verify(handPanel,(RectTransform)hand.transform);
            BookMapMobileChecks.SetSize(3);yield return new WaitForSecondsRealtime(.3f);
            TutorialSpotlightChecks.Verify(handPanel,(RectTransform)hand.transform);
            BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.2f);
            TutorialSpotlightChecks.Verify(handPanel,(RectTransform)hand.transform);
            int worldInteractions=0;EventHandler observer=(s,e)=>worldInteractions++;GameInput.Instance.OnInteractAction+=observer;
            yield return Tap(hand);GameInput.Instance.OnInteractAction-=observer;
            string expected=scene=="LogicGarden"?"MeetLefyahj":scene=="Formal vs. Informal Proofs"?"Jump":scene=="Direct&Indirect"?"Timer":"Book";
            Check(Step(coach)==expected&&worldInteractions==0,"one touch advances once and consumes gameplay interaction "+scene+" step="+Step(coach));
            var panel=Panel(coach,scene);yield return Stable(panel,scene);
            if(scene=="Formal vs. Informal Proofs")
            {
                // Continue the existing physical jump and explanations into the book-camera lesson.
                float groundedBy=Time.realtimeSinceStartup+4;
                while(!Player.LocalInstance.GetComponent<CharacterController>().isGrounded&&Time.realtimeSinceStartup<groundedBy)yield return null;
                yield return Tap(FindButton("JumpButton"));
                float jumpBy=Time.realtimeSinceStartup+4;
                while(Step(coach)=="Jump"&&Time.realtimeSinceStartup<jumpBy)yield return null;
                Check(Step(coach)=="Timer","successful mobile jump preserves the next tutorial step; at="+Player.LocalInstance.transform.position+" grounded="+Player.LocalInstance.GetComponent<CharacterController>().isGrounded+" state="+Step(coach));
            }
            if(scene=="Formal vs. Informal Proofs"||scene=="Direct&Indirect")
            {
                yield return new WaitForSecondsRealtime(1.05f);Call(coach,scene=="Direct&Indirect"?"Next":"NextStep");
                yield return new WaitForSecondsRealtime(1.05f);Call(coach,scene=="Direct&Indirect"?"Next":"NextStep");
                Check(Step(coach)=="Book","remaining timer/progress/book sequence preserved");yield return Stable(panel,scene+" book camera");
            }
            BookMapMobileChecks.SetSize(3);yield return new WaitForSecondsRealtime(.3f);yield return Stable(panel,scene+" portrait");
            BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.2f);
            
            yield return Cleanup(coach,scene);
            TutorialSpotlightChecks.VerifyCleanedUp();
            Check(!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled,"cleanup restores controls "+scene);
            Log(scene+": bright target/explanation/Skip in portrait and landscape; touch-safe spotlight; hand touch consumed; stable lesson; spotlight/camera/input cleanup");
            if(scene=="TruthTable")yield return Quizzes();
        }
        InputSystem.RemoveDevice(touchscreen);
    }
    static MonoBehaviour Prepare(string scene)
    {
        if(scene=="PRELIM"){var help=Find<PropositionalLogicTutorial>();help.CloseTutorial();var t=Find<PropositionalControlsOnboarding>();if(t==null)t=help.gameObject.AddComponent<PropositionalControlsOnboarding>();if(!t.IsActive)t.Begin(Field<Button>(help,"helpButton"));return t;}
        if(scene=="TruthTable"){var help=Find<TruthTableTutorial>();help.CloseTutorial();var t=Find<TruthTableControlsOnboarding>();if(t==null)t=help.gameObject.AddComponent<TruthTableControlsOnboarding>();if(!t.IsActive)t.Begin(Field<Button>(help,"helpButton"));return t;}
        if(scene=="RulesOfInference"){StageJourneyUI.Instance.CloseInferenceTutorial();var t=Find<InferenceControlsOnboarding>();if(!t.IsActive)t.Begin(Field<Button>(StageJourneyUI.Instance,"inferenceHelp"));return t;}
        if(scene=="Formal vs. Informal Proofs"){var s=Find<ProofsLibraryStageController>();s.Presentation.Help.CloseHelp();var t=Find<ProofsControlsOnboarding>();if(t==null)t=s.gameObject.AddComponent<ProofsControlsOnboarding>();t.Begin(s,s.Presentation.Help);return t;}
        if(scene=="Direct&Indirect"){var t=Find<DirectProofsGuide>();t.BeginFromHelp();return t;}
        if(scene=="Induction"){var s=Find<LogicLegends.Induction.InductionStageManager>();s.help.Close();s.onboarding.Begin(s);return s.onboarding;}
        if(scene=="Setssss"){var help=Find<SetssssHelpUI>();Call(help,"BeginFromHelp");var t=Find<SetssssControlsOnboarding>();if(t==null){t=help.gameObject.AddComponent<SetssssControlsOnboarding>();t.Begin(Field<Button>(help,"helpButton"));}return t;}
        if(scene=="Functions and Relations"){var s=Find<FunctionsRelationsStageManager>();s.Help.CloseHelp();var t=Find<FunctionsRelationsControlsOnboarding>();if(t==null)t=s.Help.gameObject.AddComponent<FunctionsRelationsControlsOnboarding>();t.Begin(s,s.Help);return t;}
        if(scene=="Graph"){var s=Find<LogicLegends.Graph.GraphStageManager>();s.SetLesson(false);var t=Find<LogicLegends.Graph.GraphControlsOnboarding>();if(t==null)t=s.gameObject.AddComponent<LogicLegends.Graph.GraphControlsOnboarding>();if(!t.IsActive)t.Begin(s);return t;}
        var garden=Find<LogicGardenIntroduction>();var tutorial=garden.Onboarding;if(!tutorial.IsActive)Call(tutorial,"Begin");return tutorial;
    }
    static void CheckHandNotPremature(MonoBehaviour coach,string scene)
    {string before=Step(coach);GameInput.Instance.MobileInteract();Check(Step(coach)==before,"early hand cannot skip movement "+scene);}
    static string Step(object coach)
    {var prop=coach.GetType().GetProperty("CurrentStep")??coach.GetType().GetProperty("Step");return prop!=null?prop.GetValue(coach).ToString():Field<object>(coach,"step").ToString();}
    static void BeginHand(object coach,string scene,RectTransform hand)
    {
        Time.timeScale=1;GameInput.Instance.SetGameplayInputBlocked(false);Player.LocalInstance.ToggleControl(true);
        if(new[]{"PRELIM","TruthTable","RulesOfInference","Setssss"}.Contains(scene)){Call(coach,"BeginInteraction");return;}
        var type=coach.GetType();var method=type.GetMethod(scene=="LogicGarden"?"Show":"ShowStep",Fields);var args=method.GetParameters();
        object step=Enum.Parse(args[0].ParameterType,"Interaction");var values=new object[args.Length];values[0]=step;values[1]="Tap the hand button to interact with nearby objects.";values[2]=hand;
        for(int i=3;i<values.Length;i++)values[i]=args[i].ParameterType==typeof(bool)?(object)false:null;
        method.Invoke(coach,values);
    }
    static RectTransform Panel(object coach,string scene)=>Field<RectTransform>(coach,scene=="Direct&Indirect"?"card":scene=="LogicGarden"?"panel":"instructions");
    static IEnumerator Stable(RectTransform panel,string scene)
    {
        yield return null;yield return null;Vector2 before=ScreenRect(panel).center;
        var bounds=ScreenRect(panel);
        Check(bounds.xMin>=-1&&bounds.xMax<=Screen.width+1&&bounds.yMin>=-1&&bounds.yMax<=Screen.height+1,"lesson fits screen "+scene+" bounds="+bounds+" screen="+Screen.width+"x"+Screen.height);
        foreach(string name in new[]{"InteractButton","JumpButton"})
        {
            var button=FindButton(name);
            if(button.gameObject.activeInHierarchy)Check(!bounds.Overlaps(ScreenRect((RectTransform)button.transform)),"lesson clears mobile control "+scene+" "+name);
        }
        if(scene=="TruthTable"||scene=="Formal vs. Informal Proofs book camera"||scene=="Direct&Indirect book camera")
            ScreenCapture.CaptureScreenshot("Library/MobileTutorial_"+scene.Replace(" ","_").Replace("&","And")+".png");
        for(int i=0;i<22;i++){yield return new WaitForSecondsRealtime(.04f);Check(Vector2.Distance(before,ScreenRect(panel).center)<1,"lesson stays fixed during camera transition "+scene);}
        Check(panel.GetComponentInParent<Canvas>().renderMode!=RenderMode.WorldSpace,"explanation is screen-space "+scene);
    }
    static IEnumerator Cleanup(MonoBehaviour coach,string scene)
    {
        if(scene=="LogicGarden"){Call(coach,"CancelReplay");yield return null;yield break;}
        string skip=scene=="Direct&Indirect"?"Skip":scene=="Graph"?"BeginConfirmation":"SkipTutorial";
        
        if(scene=="Induction")skip="ShowConfirmation";
        Call(coach,skip);yield return null;
        if(scene=="Direct&Indirect")Call(coach,"FinishPractice");else Call(coach,"ConfirmYes");
        yield return null;yield return null;
        var formal=Find<ProofsChallengeIntroductions>();if(formal!=null&&formal.IsOpen)formal.Skip();
        var sets=Find<SetssssChallengeIntroductions>();if(sets!=null&&sets.IsOpen)sets.Skip();
        var direct=Find<DirectProofsGuide>();if(direct!=null&&direct.IsBusy&&!direct.IsPractice)direct.Skip();
        yield return null;
    }
    static IEnumerator Quizzes()
    {
        var owner=Find<TruthTableDoorInteraction>();var clock=owner.GetComponent<TruthTableStageClock>();
        Check(clock.StartFromBook(),"real quiz test starts only through book");
        var choices=Field<Button[]>(owner,"answerButtons");var back=Field<Button>(owner,"closeButton");
        var assigned=Field<TruthTableDoorQuestionBank.Question[]>(owner,"assignedQuestions");
        foreach(int size in new[]{6,12,3})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.25f);
            for(int choice=0;choice<4;choice++)
            {
                owner.ResetAfterColumnEvaluation();yield return null;
                var door=owner.GetTutorialDoor(0);var at=door.ClosestPoint(door.bounds.center+Vector3.back*10)+Vector3.back*1.3f-Vector3.up*1.5f;
                PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,at,Quaternion.identity);
                yield return null;FieldSet(owner,"focusedDoor",door);Call(owner,"OpenFocusedDoor");yield return null;yield return null;
                Check(owner.IsQuizOpen&&clock.IsCountdownPaused,"quiz pauses countdown");
                if(choice==0)ScreenCapture.CaptureScreenshot("Library/TruthTableTouchQuiz_"+Screen.width+"x"+Screen.height+".png");float before=clock.RemainingSeconds;
                if(choice==0)
                {
                    var menu=Find<LevelMenu>();var pause=Field<GameObject>(menu,"pausePanel");
                    menu.OpenPause();yield return null;
                    var resume=pause.GetComponentsInChildren<Button>(true).First(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="ClosePause"));
                    yield return Tap(resume);
                    Check(owner.IsQuizOpen&&GameInput.Instance.GameplayInputBlocked&&!Player.LocalInstance.enabled&&clock.IsCountdownPaused,"nested pause returns to the still-paused quiz");
                }
                // Back works with no answer; reopen without recording anything.
                yield return Tap(back);Check(!owner.IsQuizOpen&&!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled,"Back touch escapes unanswered quiz");
                FieldSet(owner,"focusedDoor",door);Call(owner,"OpenFocusedDoor");yield return null;yield return null;
                before=clock.RemainingSeconds;int correct=assigned[0].correctChoice;
                yield return Tap(choices[choice]);
                Check(Field<bool>(owner,"answerSubmitted"),"answer choice receives touch "+choice);
                float expected=before-(choice==correct?0:10);Check(Mathf.Abs(clock.RemainingSeconds-expected)<.05f,"one penalty per answer");
                owner.SubmitAnswer(choice);Check(Mathf.Abs(clock.RemainingSeconds-expected)<.05f,"duplicate answer ignored");
                Check(back.interactable,"Back remains enabled during feedback");
                Time.timeScale=0;yield return Tap(back);Check(!owner.IsQuizOpen&&!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled,"Back touch works with paused game time");Time.timeScale=1;
            }
            Log("Quiz: four choices, unanswered Back, feedback Back, paused-time touch, exact penalty and input restoration at "+Screen.width+"x"+Screen.height);
        }
        clock.SetPlayerAtBoard(true);float paused=clock.RemainingSeconds;owner.ResetAfterColumnEvaluation();yield return null;
        var target=owner.GetTutorialDoor(0);PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,target.bounds.center-Vector3.up*1.5f,Quaternion.identity);
        yield return null;FieldSet(owner,"focusedDoor",target);Call(owner,"OpenFocusedDoor");yield return null;
        yield return Tap(back);Check(clock.IsPlayerAtBoard&&clock.IsCountdownPaused,"closing quiz preserves pre-existing board pause");
        yield return new WaitForSecondsRealtime(.2f);Check(Mathf.Abs(paused-clock.RemainingSeconds)<.05f,"pre-existing timer pause survives quiz close");clock.SetPlayerAtBoard(false);
        // A pre-existing non-running player/input state is restored, rather than forcibly enabled.
        owner.ResetAfterColumnEvaluation();yield return null;
        GameInput.Instance.SetGameplayInputBlocked(true);Player.LocalInstance.ToggleControl(false);
        FieldSet(owner,"focusedDoor",target);Call(owner,"OpenFocusedDoor");yield return null;
        yield return Tap(back);Check(GameInput.Instance.GameplayInputBlocked&&!Player.LocalInstance.enabled,"quiz restores prior blocked controls");
        GameInput.Instance.SetGameplayInputBlocked(false);Player.LocalInstance.ToggleControl(true);
        BookMapMobileChecks.SetSize(6);
    }
    static IEnumerator Tap(Button button)
    {
        Check(button!=null&&button.gameObject.activeInHierarchy&&button.IsInteractable(),"button available: "+button?.name);
        Canvas.ForceUpdateCanvases();var point=ScreenRect((RectTransform)button.transform).center;
        var pointer=new PointerEventData(EventSystem.current){position=point};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"top touch raycast targets "+button.name+"; got "+string.Join(",",hits.Take(4).Select(h=>h.gameObject.name+"/"+h.gameObject.GetComponentInParent<Canvas>().sortingOrder+" group="+string.Join(";",h.gameObject.GetComponentsInParent<CanvasGroup>().Select(g=>g.name+":"+g.alpha+":"+g.blocksRaycasts)))));
        int id=++finger;InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});
        yield return null;yield return null;
        InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
    static Rect ScreenRect(RectTransform rect){var c=new Vector3[4];rect.GetWorldCorners(c);var p=c.Select(v=>RectTransformUtility.WorldToScreenPoint(null,v)).ToArray();return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));}
    static Button FindButton(string name)=>UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.name==name);
    static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindFirstObjectByType<T>();
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Fields).GetValue(o);
    static void FieldSet(object o,string n,object v)=>o.GetType().GetField(n,Fields).SetValue(o,v);
    static void Call(object o,string n)
    {var method=o?.GetType().GetMethod(n,Fields);if(method==null)throw new InvalidOperationException("Missing QA method "+o?.GetType().Name+"."+n);method.Invoke(o,null);}
    static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
    static void Log(string text)=>File.AppendAllText(Report,"PASS "+text+"\n");
}
public sealed class MobileTutorialQaRunner:MonoBehaviour{}
