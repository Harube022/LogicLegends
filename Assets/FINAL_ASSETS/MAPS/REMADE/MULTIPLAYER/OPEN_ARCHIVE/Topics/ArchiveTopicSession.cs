using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>One shared team state owner. Solo state, clocks, rewards and encounter outcomes remain separate.</summary>
    [DefaultExecutionOrder(160), DisallowMultipleComponent]
    public sealed partial class ArchiveTopicSession : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public const byte CommandEvent = 174, SnapshotEvent = 175;
        public const string Channel = "OpenArchive.Topics.v1", CheckpointKey = "oa.topics.v1";
        public const float ChoiceUseRadius = 1.15f, BookUseRadius = 2.5f;
        public ArchiveTopicCatalog catalog;
        public ArchiveTopicStation[] stations;
        public KhezuHuntingController hunting;
        [Min(.5f)] public float answerChannelSeconds = 1.35f;
        [Min(1)] public float propositionTimeLimitSeconds = 360;
        [Min(1)] public float truthTimeLimitSeconds = 360;
        [Min(1)] public float inferenceTimeLimitSeconds = 360;
        [Min(1)] public float proofsTimeLimitSeconds = 360;
        [Min(1)] public float proofMethodsTimeLimitSeconds = 360;
        [Min(1)] public float inductionTimeLimitSeconds = 360;
        [Min(1)] public float setsChallengeTimeLimitSeconds = 1080;
        [Min(0)] public float setsIncorrectPenaltySeconds = 10;
        [Min(1)] public float mappingTimeLimitSeconds = 600;
        [Min(0)] public float mappingIncorrectPenaltySeconds = 10;
        [Min(1)] public float graphTimeLimitSeconds = 600;
        [Min(0)] public float graphIncorrectPenaltySeconds = 10;
        [Min(1)] public float treeTimeLimitSeconds = 600;
        [Min(0)] public float treeIncorrectPenaltySeconds = 10;
        public ArchiveTeamState State { get; private set; }
        public static ArchiveTopicSession Current { get; private set; }
        public bool Ready => State != null;
        public bool IsAuthority => !MultiplayerLobbyService.IsGameplayRecovering && (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient);
        public int LocalActor => PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        // Derive the team reservation from checkpointed progress, including Book gates and timeouts.
        // No separate client lock or second topic-progress tracker is needed.
        public int ActiveTopic => !Ready ? -1 : State.stations.FirstOrDefault(s=>s.phase!=2&&
            (s.phase==1||s.round>0||s.subphase>0||s.timerRunning||s.timerExpired||s.timerAttempt>0))?.topic??-1;
        public string TopicLockNotice(int topic)
        {
            int active=ActiveTopic;
            return active>=0&&active!=topic&&topic>=0&&topic<10&&State.stations[topic].phase!=2
                ?"Finish T"+(active+1)+" · "+ArchiveTopicCatalog.Names[active]+" before starting another topic.":"";
        }
        public bool CanStartTopic(int topic)=>Ready&&topic>=0&&topic<10&&State.stations[topic].phase==0&&string.IsNullOrEmpty(TopicLockNotice(topic));
        public event Action Changed;
        public string LocalNotice { get; private set; } = "";
        readonly Dictionary<string, ArchiveQuestion> questions = new Dictionary<string, ArchiveQuestion>();
        readonly Dictionary<string, double> lastSubmit = new Dictionary<string, double>();
        int localSequence;
        float nextTick;
        string trustedSnapshot;
        bool roomMode;
        double nextGraphCheckpoint;
        public static double Clock => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.unscaledTimeAsDouble;
        public static double TimerClock => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;
        public static bool HasTopicTimer(int topic)=>topic>=0&&topic<=9;
        float TopicTimeLimit(int topic)=>Mathf.Max(1,topic==9?treeTimeLimitSeconds:topic==8?graphTimeLimitSeconds:topic==7?mappingTimeLimitSeconds:topic==6?setsChallengeTimeLimitSeconds:topic==5?inductionTimeLimitSeconds:topic==4?proofMethodsTimeLimitSeconds:topic==3?proofsTimeLimitSeconds:topic==2?inferenceTimeLimitSeconds:topic==1?truthTimeLimitSeconds:propositionTimeLimitSeconds);
        public float TimerRemaining(ArchiveStationState state)
        {
            if(state.timerRunning)return Mathf.Clamp((float)(state.timerDeadline-TimerClock),0,state.timerLimit);
            if(state.timerExpired)return 0;
            if(state.timerAttempt>0)return state.timerSavedRemaining;
            return TopicTimeLimit(state.topic);
        }
        void StartTopicTimer(ArchiveStationState state,int actor)
        {
            state.timerActor=actor;
            if(state.timerRunning)return;
            state.timerLimit=TopicTimeLimit(state.topic);
            float remaining=(state.topic==7||state.topic==8||state.topic==9)&&state.timerAttempt>0&&!state.timerExpired?Mathf.Clamp(state.timerSavedRemaining,0,state.timerLimit):state.timerLimit;
            state.timerSavedRemaining=remaining;state.timerDeadline=TimerClock+remaining;
            state.timerRunning=true;state.timerExpired=false;state.timerAttempt++;
        }
        bool ExpireTopicTimer(ArchiveStationState state,bool chargeMistake=true)
        {
            if(!HasTopicTimer(state.topic)||!state.timerRunning||state.phase==2||TimerClock<state.timerDeadline)return false;
            if(state.topic==6)foreach(int actor in state.carries.Select(c=>c.actor).Distinct().ToArray())ReleaseCarried(actor);
            state.timerRunning=false;state.timerExpired=true;state.timerSavedRemaining=0;state.timerTimeouts++;
            state.phase=0;if(state.topic!=4&&state.topic!=7&&state.topic!=8&&state.topic!=9)state.selection=-1;state.channelActor=0;state.channelChoice=-1;state.carries.Clear();
            if(state.topic==7)ClearMappingInputs();
            if(state.topic==9)ClearTreeInputs();
            if(state.topic==8){state.routeActor=0;state.routeSampleValid=false;if(Question(8).graph.type!=LogicLegends.Graph.GraphChallengeType.Degree)ArchiveGraphTraversal.Invalidate(state,"Time up. Your route is kept for review; retry from the start after Book activation.");}
            if(chargeMistake)RecordMistake(state.timerActor,0,state,"timeout");
            state.feedback=chargeMistake?"Time up. One timeout mistake recorded for "+ActorName(state.timerActor)+". Solved challenges kept. Return to the Book Statue to retry this challenge.":"Time up after the incorrect submission. One mistake recorded; your work is kept. Return to the Book to retry.";
            Touch(state);return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic() { Current = null; }
        void Awake()
        {
            if (Current != null && Current != this) { enabled = false; Debug.LogError("Open Archive needs one topic session.", this); return; }
            Current = this;
        }
        void Start()
        {
            roomMode = PhotonNetwork.InRoom;
            BindLearningEncounter();
            foreach (var station in stations) station.session = this;
            if (!PhotonNetwork.InRoom) Initialize(Guid.NewGuid().GetHashCode());
            else if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CheckpointKey, out var value) && value is string json) Receive(json, true);
            else if (IsAuthority) Initialize(Guid.NewGuid().GetHashCode());
        }
        public void Initialize(int seed)
        {
            if (!IsAuthority || catalog == null) return;
            ReleaseEscapedPlayers();
            State = new ArchiveTeamState { seed = seed, stations = Enumerable.Range(0, 10).Select(i => new ArchiveStationState { topic = i }).ToArray() };
            questions.Clear();lastSubmit.Clear();localSequence=0;LocalNotice=""; Commit();
        }
        public ArchiveQuestion Question(int topic)
        {
            var s = State.stations[topic]; int round = Math.Min(s.round, ArchiveTopicCatalog.RoundCount(topic) - 1);
            string key = topic + ":" + round + ":" + s.subphase;
            if (!questions.TryGetValue(key, out var q)) questions[key] = q = ArchiveTopicQuestions.Build(catalog, topic, round, State.seed, s.subphase);
            return q;
        }
        public void Request(int topic, ArchiveAction action, int a = -1, int b = -1)
        {
            if (!Ready || topic < 0 || topic >= 10 || !CanAct(LocalActor)) return;
            LocalNotice=TopicLockNotice(topic);
            if(!string.IsNullOrEmpty(LocalNotice))return;
            var record = State.actors.FirstOrDefault(p => p.actor == LocalActor);
            if (record != null) localSequence = Math.Max(localSequence, record.lastSequence);
            var cmd = new ArchiveCommand { topic = topic, revision = State.stations[topic].revision, sequence = ++localSequence, action = action, a = a, b = b };
            if (IsAuthority) Accept(LocalActor, cmd);
            else PhotonNetwork.RaiseEvent(CommandEvent, new object[] { Channel, JsonUtility.ToJson(cmd) }, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }
        public Player ActorPlayer(int actor)
        {
            if (!PhotonNetwork.InRoom) return actor == -1 ? Player.LocalInstance : null;
            foreach (var player in FindObjectsByType<Player>(FindObjectsSortMode.None))
            {
                var view = player.GetComponent<PhotonView>();
                if (view != null && view.ViewID != 0 && view.OwnerActorNr == actor) return player;
            }
            return null;
        }
        public bool CanAct(int actor)
        {
            if (MultiplayerLobbyService.IsGameplayRecovering) return false;
            if (PhotonNetwork.InRoom && (PhotonNetwork.CurrentRoom.GetPlayer(actor) == null || PhotonNetwork.CurrentRoom.GetPlayer(actor).IsInactive)) return false;
            if (HasEscapedActor(actor) || TeamWon) return false;
            var player = ActorPlayer(actor);
            if (player == null || !player.gameObject.activeInHierarchy || player.IsMovementLocked) return false;
            if (hunting != null && hunting.State == KhezuHuntState.Caught && hunting.CaughtPlayerId == actor) return false;
            return hunting == null || hunting.health == null || !hunting.health.HasAuthoritativeActorHealth || hunting.health.IsActorAlive(actor);
        }
        public bool Near(int actor, int topic, Vector3 localPoint, float distance)
        {
            var p = ActorPlayer(actor); if (p == null) return false;
            var local = stations[topic].transform.InverseTransformPoint(p.transform.position);
            return Mathf.Abs(local.y) < 5 && Vector2.Distance(new Vector2(local.x, local.z), new Vector2(localPoint.x, localPoint.z)) <= distance;
        }
        public bool Accept(int actor, ArchiveCommand cmd)
        {
            if (!IsAuthority || !Ready || cmd == null || cmd.topic < 0 || cmd.topic >= 10 || !Enum.IsDefined(typeof(ArchiveAction), cmd.action)) return false;
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.GetPlayer(actor) == null) return false;
            if (cmd.action == ArchiveAction.AdminCompleteTopic || cmd.action == ArchiveAction.AdminResetTopic)
                return AcceptTopicAdmin(actor, cmd);
            if (cmd.action == ArchiveAction.BarrierHint || cmd.action == ArchiveAction.BarrierAnswer)
                return AcceptBarrierLearning(actor, cmd);
            if (cmd.action == ArchiveAction.RequestHint) return AcceptLearningHint(actor, cmd);
            if(ExpireTopicTimer(State.stations[cmd.topic]))return false;
            if (!CanAct(actor) || !Near(actor, cmd.topic, Vector3.zero, 14)) return false;
            var record = State.actors.FirstOrDefault(p => p.actor == actor);
            if (record == null) { record = new ArchiveActor { actor = actor, displayName = actor==-1?"Local explorer":PhotonNetwork.CurrentRoom.GetPlayer(actor).NickName }; State.actors.Add(record); }
            if (cmd.sequence <= record.lastSequence) return false;
            record.lastSequence = cmd.sequence;
            var s = State.stations[cmd.topic]; var station = stations[cmd.topic];
            if (cmd.revision != s.revision) return false;
            int activeTopic=ActiveTopic;
            if(activeTopic>=0&&activeTopic!=cmd.topic)
            {
                if(actor==LocalActor)LocalNotice=TopicLockNotice(cmd.topic);
                return false;
            }
            if (cmd.action == ArchiveAction.Start)
            {
                if (s.phase != 0 || !Near(actor, cmd.topic, ArchiveTopicStation.BookPosition, BookUseRadius)) return false;
                // Retry the current physical puzzle without discarding its arrangement or inference stage.
                bool resumeWork=s.topic>=1&&s.topic<=9&&s.timerExpired&&s.placements.Length==Question(cmd.topic).PlacementCount;
                s.phase = 1;if(!resumeWork)ArchiveTopicQuestions.Prepare(Question(cmd.topic), s);s.routeActor = actor;
                if(s.topic==7)ClearMappingInputs();
                if(s.topic==8)s.routeActor=0;
                if(s.topic==9)ClearTreeInputs();
                if(HasTopicTimer(s.topic))StartTopicTimer(s,actor);
                s.feedback = "Challenge active. Your team shares this work."; Touch(s); return true;
            }
            if (s.phase != 1) return false;
            var q = Question(cmd.topic);
            switch (cmd.action)
            {
                case ArchiveAction.BeginChoice:
                    if (q.activity != ArchiveActivity.Choice || cmd.a < 0 || cmd.a >= q.items.Length || s.channelActor != 0 || !Near(actor, cmd.topic, station.ItemPosition(cmd.a, q), ChoiceUseRadius)) return false;
                    s.channelActor = actor; s.channelChoice = cmd.a; s.channelStarted = Clock; s.channelSequence = cmd.sequence; break;
                case ArchiveAction.Pick:
                    if (q.activity == ArchiveActivity.Choice) return false;
                    if(s.topic==6&&!ArchiveSetsPresentation.CanPick(cmd.a,q,s))return false;
                    if (cmd.a < 0 || cmd.a >= q.items.Length || !Near(actor, cmd.topic, station.CurrentItemPosition(cmd.a, q, s), 2.2f) || s.carries.Any(c => c.item == cmd.a && c.actor != actor && !q.reusableItems)) return false;
                    ReleaseCarried(actor);
                    if(s.topic==6&&cmd.a<s.setGrades.Length)s.setGrades[cmd.a]=0;
                    if (!q.reusableItems)
                    {
                        if (q.regionPlacement) s.placements[cmd.a] = -1;
                        else for (int i = 0; i < s.placements.Length; i++) if (s.placements[i] == cmd.a) s.placements[i] = -1;
                    }
                    s.carries.Add(new ArchiveCarry { actor = actor, item = cmd.a }); break;
                case ArchiveAction.ReturnItem:
                    if (!s.carries.Any(c => c.actor == actor)) return false;
                    ReleaseCarried(actor); break;
                case ArchiveAction.Place:
                    if (cmd.a < 0 || cmd.a >= q.slots.Length || !Near(actor, cmd.topic, station.SlotPosition(cmd.a, q), 2.2f)) return false;
                    var held = s.carries.FirstOrDefault(c => c.actor == actor); if (held == null) return false;
                    if (q.regionPlacement) s.placements[held.item] = cmd.a;
                    else { if (cmd.a >= s.placements.Length) return false; s.placements[cmd.a] = held.item; }
                    s.carries.Remove(held); break;
                case ArchiveAction.Select:
                    if(q.tree!=null){if(!SelectTree(q,s,record,cmd.a))return false;break;}
                    if(q.trial!=null&&(q.trial.kind==TrialProofKind.ArrangeSteps||cmd.a<0||cmd.a>2||!Near(actor,cmd.topic,ArchiveMethodPresentation.MethodPosition(cmd.a),2.4f)))return false;
                    if (q.mapping != null && q.mapping.questionKind == MappingQuestionKind.BuildExactRelation)
                    {
                        if (cmd.a < 0 || cmd.a >= q.inputLabels.Length || !Near(actor, cmd.topic, station.InputPosition(cmd.a), 2.4f)) return false;
                        record.mappingInput = cmd.a; record.mappingRound = s.round; break;
                    }
                    if(q.mapping!=null&&!Near(actor,cmd.topic,ArchiveMappingPresentation.AnswerPosition(q.mapping.questionKind==MappingQuestionKind.SelectProperties?Array.IndexOf(new[]{2,4,8},cmd.a):cmd.a),2.4f))return false;
                    if (!Select(q,s,cmd.a)) return false; break;
                case ArchiveAction.Connect: case ArchiveAction.Remove:
                    if (q.mapping != null)
                    {
                        if (cmd.action != ArchiveAction.Connect || q.mapping.questionKind != MappingQuestionKind.BuildExactRelation ||
                            cmd.a < 0 || cmd.a >= q.inputLabels.Length || cmd.b < 0 || cmd.b >= q.outputLabels.Length ||
                            record.mappingInput != cmd.a || record.mappingRound != s.round || !Near(actor,cmd.topic,station.OutputPosition(cmd.b),2.4f) || s.links.Contains(cmd.a*100+cmd.b)) return false;
                        s.links.Add(cmd.a*100+cmd.b); break;
                    }
                    if(q.tree==null||record.treeRound!=s.round||record.treeFirst!=cmd.a||record.treeSecond!=cmd.b||!ArchiveTreeRules.Edit(q,s,cmd.a,cmd.b,cmd.action==ArchiveAction.Remove,out string treeReason))return false;
                    record.treeFirst=record.treeSecond=-1;s.feedback=treeReason;break;
                case ArchiveAction.ClearSelection:
                    if(q.tree==null)return false;
                    record.treeFirst=record.treeSecond=-1;record.treeRound=s.round;
                    if(!q.tree.Editable){s.selection=-1;s.flags=-1;}s.feedback="Pick cleared. The team's branches are kept.";break;
                case ArchiveAction.Undo:
                    if (q.mapping == null || q.mapping.questionKind != MappingQuestionKind.BuildExactRelation || s.links.Count <= (q.mapping.initialPairsLocked ? q.mapping.initialPairs.Count : 0) || !Enumerable.Range(0,q.outputLabels.Length).Any(i=>Near(actor,cmd.topic,station.OutputPosition(i),2.4f))) return false;
                    s.links.RemoveAt(s.links.Count - 1); break;
                case ArchiveAction.RetryRoute:
                    if (q.graph == null) return false;
                    if(q.graph.type==LogicLegends.Graph.GraphChallengeType.Degree){s.selection=-1;break;}
                    if(!CanUseGraphStart(actor))return false;
                    ArchiveGraphTraversal.Begin(s,q,actor,GraphFeet(actor),Clock);s.feedback="Route runner: "+ActorName(actor)+". Follow the stone paths and confirm on the goal.";break;
                case ArchiveAction.Submit:
                    if(s.topic==7&&!ArchiveMappingPresentation.ReadyToCheck(q,s))return false;
                    if (q.activity == ArchiveActivity.Choice || s.carries.Count > 0 || (q.graph != null && q.graph.type != LogicLegends.Graph.GraphChallengeType.Degree && s.routeActor != actor)) return false;
                    string attemptKey=actor+":"+s.topic+":"+s.round+":"+s.subphase;
                    if (lastSubmit.TryGetValue(attemptKey,out double previous) && Clock - previous < 1.5) return false;
                    lastSubmit[attemptKey] = Clock;if(q.graph!=null&&q.graph.type!=LogicLegends.Graph.GraphChallengeType.Degree)SampleRoute(s,q);Submit(actor,cmd.sequence,s,q); return true;
                default:return false;
            }
            Touch(s); return true;
        }
        bool Select(ArchiveQuestion q,ArchiveStationState s,int value)
        {
            if (q.mapping != null)
            {
                if (q.mapping.questionKind == MappingQuestionKind.ClassifyFunction) { if(value<0||value>2)return false;s.selection=value;return true; }
                if (q.mapping.questionKind == MappingQuestionKind.SelectProperties) { if(value!=2&&value!=4&&value!=8)return false;s.flags^=value;return true; }
                return false;
            }
            if(q.activity==ArchiveActivity.Method){if(value<0||value>2)return false;s.selection=value;return true;}
            if(q.graph!=null){if(q.graph.type!=LogicLegends.Graph.GraphChallengeType.Degree||value<0||value>6)return false;s.selection=value;return true;}
            return false;
        }
        bool SelectTree(ArchiveQuestion q,ArchiveStationState s,ArchiveActor actor,int value){
            if(ArchiveTreeRules.NumberQuestion(q)){if(value<100||value>105)return false;s.flags=value-100;s.feedback="Selected "+s.flags+". Confirm the team answer.";return true;}
            if(value<0||value>=q.nodeIds.Length)return false;
            if(!q.tree.Editable){s.selection=value;s.feedback=ActorName(actor.actor)+" selected node "+q.nodeIds[value]+". Confirm the team answer.";return true;}
            if(actor.treeRound!=s.round){actor.treeRound=s.round;actor.treeFirst=actor.treeSecond=-1;}
            if(actor.treeFirst<0||actor.treeSecond>=0){actor.treeFirst=value;actor.treeSecond=-1;}else if(actor.treeFirst!=value)actor.treeSecond=value;
            s.feedback=ActorName(actor.actor)+" selected "+q.nodeIds[actor.treeFirst]+(actor.treeSecond<0?". Select the second endpoint.":" → "+q.nodeIds[actor.treeSecond]+". Connect or Remove.");return true;
        }
        public ArchiveActor TreeSelection(int actor)=>Ready?State.actors.FirstOrDefault(a=>a.actor==actor&&a.treeRound==State.stations[9].round):null;
        void ClearTreeInputs(){foreach(var actor in State.actors){actor.treeFirst=actor.treeSecond=actor.treeRound=-1;}}
        void Submit(int actor,int sequence,ArchiveStationState s,ArchiveQuestion q)
        {
            bool correct=ArchiveTopicQuestions.Validate(q,s,out string feedback);
            if(s.topic>=0&&s.topic<=9)
            {
                s.answerSerial++;s.lastAnswerActor=actor;s.lastAnswerChoice=s.selection;s.lastAnswerRound=s.round;
                s.lastAnswerResult=correct?1:-1;s.lastAnswerAt=Clock;
            }
            s.channelActor=0;s.channelChoice=-1;
            if(s.topic==6)s.setGrades=ArchiveTopicQuestions.SetPlacementGrades(q,s);
            if(correct)
            {
                if(s.topic==2&&s.subphase==0)
                {
                    s.subphase=1;ArchiveTopicQuestions.Prepare(Question(s.topic),s);s.feedback="Premises correct. Find the matching Rule Crystal.";
                }
                else if(s.topic==6)
                {
                    string accepted=ArchiveTopicQuestions.SetsReveal(q);s.round++;s.carries.Clear();s.subphase=0;
                    s.phase=s.round>=ArchiveTopicCatalog.RoundCount(6)?2:s.round%ArchiveTopicCatalog.SetProblemsPerChallenge==0?0:1;
                    if(s.phase==1){ArchiveTopicQuestions.Prepare(Question(6),s);s.feedback="Correct! Problem 2 / 2 is active; the same challenge clock continues.";}
                    else if(s.phase==0){s.placements=Array.Empty<int>();s.setGrades=Array.Empty<int>();s.timerRunning=false;s.timerSavedRemaining=TopicTimeLimit(6);s.feedback="Two problems accepted. Read the Book to start the next Sets challenge.";}
                    else{s.timerSavedRemaining=TimerRemaining(s);s.timerRunning=false;s.feedback="All five Sets challenges complete for the team!";}
                    s.reveal=accepted;
                }
                else
                {
                    s.reveal=s.topic==1?q.prompt+"\n"+string.Join("  ·  ",q.slots.Select((row,i)=>row.Replace("P=","").Replace("Q=","").Replace(", ","")+" = "+(s.placements[i]==0?"T":"F"))):q.successText??"";
                    if(s.topic==3)s.reveal=ArchiveTopicQuestions.ProofReveal(q,s);
                    if(s.topic==4)s.reveal=ArchiveTopicQuestions.MethodReveal(q,s);
                    if(s.topic==5)s.reveal=ArchiveTopicQuestions.InductionReveal(q,s);
                    if(s.topic==7){s.reveal=ArchiveTopicQuestions.MappingReveal(q,s);s.timerSavedRemaining=TimerRemaining(s);s.timerRunning=false;ClearMappingInputs();}
                    if(s.topic==8){s.reveal=ArchiveTopicQuestions.GraphReveal(q,s);s.timerSavedRemaining=TimerRemaining(s);s.timerRunning=false;s.routeActor=0;s.routeSampleValid=false;}
                    if(s.topic==9){s.reveal=ArchiveTreeRules.Reveal(q,s);s.timerSavedRemaining=TimerRemaining(s);s.timerRunning=false;ClearTreeInputs();}
                    if(s.topic==1)s.placements=Enumerable.Repeat(-1,4).ToArray();
                    s.round++;s.subphase=0;s.phase=s.round>=ArchiveTopicCatalog.RoundCount(s.topic)?2:0;
                    // A timeout at a Book gate must not carry the previous solved tablet IDs into a new proof.
                    // Keep the published reveal, but distinguish a not-yet-started proof from active retry work.
                    if((s.topic==4||s.topic==5)&&s.phase==0){s.placements=Array.Empty<int>();s.selection=-1;}
                    if(s.topic==7&&s.phase==0){s.links.Clear();s.selection=-1;s.flags=0;}
                    if(s.topic==8&&s.phase==0){s.route.Clear();s.routeEdge=-1;s.routeCrossedMiddle=false;s.routeAtVertex=false;s.routeReason="";s.selection=-1;}
                    if(s.topic==9&&s.phase==0){s.links.Clear();s.selection=-1;s.flags=-1;}
                    s.feedback=s.phase==2?"Topic complete for the team!":"Correct! Use the book to start the next challenge.";
                    s.carries.Clear();
                    if(HasTopicTimer(s.topic)&&s.phase==2){s.timerSavedRemaining=TimerRemaining(s);s.timerRunning=false;}
                }
            }
            else
            {
                feedback=ArchiveLearningFeedback.Diagnose(q,s,feedback);
                RecordMistake(actor,sequence,s,"answer");
                s.feedback="Incorrect — "+ActorName(actor)+" recorded one mistake. "+feedback;
                if(s.topic==6||s.topic==7||s.topic==8||s.topic==9)
                {
                    double now=TimerClock,remaining=Math.Max(0,s.timerDeadline-now),penalty=Math.Min(Math.Max(0,s.topic==9?treeIncorrectPenaltySeconds:s.topic==8?graphIncorrectPenaltySeconds:s.topic==7?mappingIncorrectPenaltySeconds:setsIncorrectPenaltySeconds),remaining);
                    s.feedback+=(s.topic==9?" Your branches and answer are kept. −":s.topic==8?" Your route or degree choice is kept. −":s.topic==7?" Your connections and choices are kept. −":" Red blocks need correction; green blocks can stay. −")+penalty.ToString("0.#")+"s.";
                    // Compare in double precision before subtracting: a float-rounded remainder can
                    // leave a tiny positive deadline and charge a second mistake on the next tick.
                    if(penalty>=remaining){s.timerDeadline=now;ExpireTopicTimer(s,false);return;}
                    s.timerDeadline-=penalty;
                }
                if(s.topic==0)s.feedback="Rune "+(char)('A'+s.selection)+" does not match. One mistake recorded for "+ActorName(actor)+". "+feedback;
            }
            Touch(s);
        }
        void ClearMappingInputs(){foreach(var actor in State.actors){actor.mappingInput=-1;actor.mappingRound=-1;}}
        void RecordMistake(int actor,int sequence,ArchiveStationState state,string reason)
        {
            var record=State.actors.FirstOrDefault(a=>a.actor==actor);
            if(record==null){record=new ArchiveActor{actor=actor,displayName=ActorName(actor)};State.actors.Add(record);}
            record.mistakes++;
            State.mistakes.Add(new ArchiveMistake{actor=actor,topic=state.topic,round=state.round,revision=state.revision,sequence=sequence,time=Clock,reason=reason});
            if(hunting!=null)hunting.Request(KhezuCommand.AddMistake);
        }
        public string ActorName(int actor)
        {
            if(actor==-1)return "Local explorer";
            string current=PhotonNetwork.InRoom?PhotonNetwork.CurrentRoom.GetPlayer(actor)?.NickName:null;
            if(!string.IsNullOrWhiteSpace(current))return current;
            string saved=State?.actors.FirstOrDefault(a=>a.actor==actor)?.displayName;
            return string.IsNullOrWhiteSpace(saved)?"Player "+actor:saved;
        }
        void ReleaseCarried(int actor)
        {
            foreach(var s in State.stations){if(s.topic==6&&s.carries.Any(c=>c.actor==actor)){var q=Question(6);if(q.setGroup==4)foreach(var held in s.carries.Where(c=>c.actor==actor))s.placements[held.item]=q.setInitialPlacements[held.item];}if(s.carries.RemoveAll(c=>c.actor==actor)>0)s.revision++;}
        }
        void Touch(ArchiveStationState s){s.revision++;Commit();}
        void Commit()
        {
            AdvanceExitProgress();
            SyncEscapedPlayers();
            State.revision++;Changed?.Invoke();
            if(PhotonNetwork.InRoom&&IsAuthority)
            {
                string json=JsonUtility.ToJson(State);trustedSnapshot=json;
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable{{CheckpointKey,json}});
                PhotonNetwork.RaiseEvent(SnapshotEvent,new object[]{Channel,json},new RaiseEventOptions{Receivers=ReceiverGroup.Others},SendOptions.SendReliable);
            }
        }
        void Update()
        {
            if (Time.unscaledTime >= nextExitSync) { nextExitSync=Time.unscaledTime+.1f; SyncEscapedPlayers(); }
            if(!Ready||!IsAuthority||Time.unscaledTime<nextTick||Time.timeScale<=0)return;
            if(roomMode!=PhotonNetwork.InRoom){enabled=false;return;}
            nextTick=Time.unscaledTime+.1f;
            TickLearningEncounter();
            if (AdvanceExitProgress()) Commit();
            if (TeamWon) return;
            int activeTopic=ActiveTopic;
            foreach(var s in State.stations)
            {
                if(activeTopic>=0&&s.topic!=activeTopic)continue;
                // Migrate an already active legacy checkpoint once, without restarting saved deadlines.
                if(HasTopicTimer(s.topic)&&s.phase==1&&s.timerAttempt==0&&!s.timerRunning&&!s.timerExpired){StartTopicTimer(s,s.routeActor);Touch(s);}
                if(ExpireTopicTimer(s))continue;
                if(s.phase!=1)continue;
                var q=Question(s.topic);bool dirty=false;
                if(s.topic==7)foreach(var actor in State.actors.Where(a=>a.mappingInput>=0&&!CanAct(a.actor))){actor.mappingInput=-1;actor.mappingRound=-1;dirty=true;}
                if(s.topic==9)foreach(var actor in State.actors.Where(a=>a.treeFirst>=0&&!CanAct(a.actor))){actor.treeFirst=actor.treeSecond=actor.treeRound=-1;dirty=true;}
                foreach(var held in s.carries.ToArray())
                    if(!CanAct(held.actor)||!Near(held.actor,s.topic,Vector3.zero,18)){ReleaseCarried(held.actor);dirty=true;}
                if(s.channelActor!=0)
                {
                    int actor=s.channelActor;
                    if(!CanAct(actor)||!Near(actor,s.topic,stations[s.topic].ItemPosition(s.channelChoice,q),ChoiceUseRadius))
                    {
                        s.channelActor=0;s.channelChoice=-1;dirty=true;
                        if(s.topic==0)s.feedback="Channel cancelled. No mistake recorded. Stay inside a rune's ring until it fills.";
                    }
                    else if(Clock-s.channelStarted>=answerChannelSeconds){s.selection=s.channelChoice;Submit(actor,s.channelSequence,s,q);continue;}
                }
                if(q.graph!=null&&q.graph.type!=LogicLegends.Graph.GraphChallengeType.Degree)dirty|=SampleRoute(s,q);
                if(dirty)Touch(s);
            }
        }
        public Vector3 GraphFeet(int actor){var player=ActorPlayer(actor);if(player==null)return Vector3.one*1000;var cc=player.GetComponent<CharacterController>();var position=cc!=null?LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(cc):player.transform.position;return stations[8].transform.InverseTransformPoint(position);}
        public bool CanUseGraphStart(int actor){if(!Ready||State.stations[8].phase!=1||!CanAct(actor)||Question(8).graph.type==LogicLegends.Graph.GraphChallengeType.Degree)return false;var s=State.stations[8];var q=Question(8);return(s.routeActor==0||s.routeActor==actor||!s.continuous||!CanAct(s.routeActor))&&ArchiveGraphTraversal.AtVertex(GraphFeet(actor),ArchiveGraphTraversal.Vertex(q,ArchiveGraphTraversal.StartIndex(q)));}
        bool SampleRoute(ArchiveStationState s,ArchiveQuestion q){
            if(s.routeActor==0)return false;
            if(!CanAct(s.routeActor)){s.routeActor=0;s.routeSampleValid=false;ArchiveGraphTraversal.Invalidate(s,"Runner unavailable. Another teammate can restart at the green start rune.");s.feedback=s.routeReason;return true;}
            if(!s.continuous)return false;var player=ActorPlayer(s.routeActor);var cc=player.GetComponent<CharacterController>();float scale=Mathf.Max(.01f,Mathf.Abs(stations[8].transform.lossyScale.x)),footprint=cc==null?.18f:(cc.radius+cc.skinWidth)*Mathf.Abs(player.transform.lossyScale.x)/scale;int count=s.route.Count,edge=s.routeEdge;bool at=s.routeAtVertex,middle=s.routeCrossedMiddle;var previous=s.lastRoutePosition;
            ArchiveGraphTraversal.Sample(s,q,GraphFeet(s.routeActor),Clock,footprint,22/scale,s.routeActor==LocalActor&&TeleportManager.Instance!=null&&TeleportManager.Instance.IsTeleporting);
            if(!s.continuous)s.feedback=s.routeReason;
            bool changed=count!=s.route.Count||edge!=s.routeEdge||at!=s.routeAtVertex||middle!=s.routeCrossedMiddle||!s.continuous;
            if(changed||(s.lastRoutePosition-previous).sqrMagnitude>.0001f&&Clock>=nextGraphCheckpoint){nextGraphCheckpoint=Clock+.2;return true;}return false;
        }
        public void OnEvent(EventData data)
        {
            if(!PhotonNetwork.InRoom||!(data.CustomData is object[] p)||p.Length!=2||!(p[0] is string channel)||channel!=Channel||!(p[1] is string json))return;
            if(data.Code==SnapshotEvent&&data.Sender==PhotonNetwork.MasterClient.ActorNumber&&!IsAuthority)Receive(json,false);
            else if(data.Code==CommandEvent&&IsAuthority&&json.Length<2048)
            {
                try{Accept(data.Sender,JsonUtility.FromJson<ArchiveCommand>(json));}catch(Exception e){Debug.LogWarning("Rejected malformed topic command: "+e.GetType().Name);}
            }
        }
        void Receive(string json,bool initial)
        {
            try
            {
                var state=JsonUtility.FromJson<ArchiveTeamState>(json);
                if(state==null||state.version!=1||state.stations==null||state.stations.Length!=10||(!initial&&State!=null&&state.revision<=State.revision))return;
                State=state;NormalizeExit();NormalizeLearning();questions.Clear();trustedSnapshot=json;SyncEscapedPlayers();Changed?.Invoke();
            }
            catch(Exception e){Debug.LogWarning("Ignored malformed topic snapshot: "+e.GetType().Name);}
        }
        public override void OnPlayerLeftRoom(Photon.Realtime.Player other){if(IsAuthority&&Ready){ReleaseCarried(other.ActorNumber);var departed=State.actors.FirstOrDefault(a=>a.actor==other.ActorNumber);if(departed!=null)departed.treeFirst=departed.treeSecond=departed.treeRound=-1;foreach(var s in State.stations)if(s.routeActor==other.ActorNumber){s.routeActor=0;s.routeSampleValid=false;if(s.topic==8)ArchiveGraphTraversal.Invalidate(s,"Runner left. A teammate can restart at the green start rune.");}Commit();}}
        public override void OnPlayerEnteredRoom(Photon.Realtime.Player other){if(IsAuthority&&Ready)Commit();}
        public override void OnMasterClientSwitched(Photon.Realtime.Player master)
        {
            if(!IsAuthority)return;
            if(!string.IsNullOrEmpty(trustedSnapshot))Receive(trustedSnapshot,true);
            if(!Ready&&PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CheckpointKey,out var value)&&value is string json)Receive(json,true);
            if(Ready){foreach(var s in State.stations){if(s.topic!=8)s.routeSampleValid=false;s.channelActor=0;}Commit();}
        }
        public bool RestoreRecoveryCheckpoint()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CheckpointKey, out var value) || !(value is string json)) return false;
            Receive(json, true); roomMode = true;
            return Ready;
        }
        public override void OnLeftRoom(){if(MultiplayerLobbyService.IsGameplayRecovering)return;ReleaseEscapedPlayers();State=null;Changed?.Invoke();}
        public override void OnDisconnected(DisconnectCause cause){if(MultiplayerLobbyService.IsGameplayRecovering)return;ReleaseEscapedPlayers();State=null;Changed?.Invoke();}
        void OnDestroy(){UnbindLearningEncounter();ReleaseEscapedPlayers();if(Current==this)Current=null;}
    }
}
