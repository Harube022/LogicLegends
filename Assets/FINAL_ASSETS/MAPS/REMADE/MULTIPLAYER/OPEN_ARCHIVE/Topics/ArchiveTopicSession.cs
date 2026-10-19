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
    public sealed class ArchiveTopicSession : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public const byte CommandEvent = 174, SnapshotEvent = 175;
        public const string Channel = "OpenArchive.Topics.v1", CheckpointKey = "oa.topics.v1";
        public ArchiveTopicCatalog catalog;
        public ArchiveTopicStation[] stations;
        public KhezuHuntingController hunting;
        [Min(.5f)] public float answerChannelSeconds = 1.35f;
        public ArchiveTeamState State { get; private set; }
        public static ArchiveTopicSession Current { get; private set; }
        public bool Ready => State != null;
        public bool IsAuthority => !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;
        public int LocalActor => PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        public event Action Changed;
        public string LocalNotice { get; private set; } = "";
        readonly Dictionary<string, ArchiveQuestion> questions = new Dictionary<string, ArchiveQuestion>();
        readonly Dictionary<string, double> lastSubmit = new Dictionary<string, double>();
        int localSequence;
        float nextTick;
        string trustedSnapshot;
        bool roomMode;
        public static double Clock => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.unscaledTimeAsDouble;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic() { Current = null; }
        void Awake()
        {
            if (Current != null && Current != this) { enabled = false; Debug.LogError("Open Archive needs one topic session.", this); return; }
            Current = this;
        }
        void Start()
        {
            roomMode = PhotonNetwork.InRoom;
            foreach (var station in stations) station.session = this;
            if (!PhotonNetwork.InRoom) Initialize(Guid.NewGuid().GetHashCode());
            else if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CheckpointKey, out var value) && value is string json) Receive(json, true);
            else if (IsAuthority) Initialize(Guid.NewGuid().GetHashCode());
        }
        public void Initialize(int seed)
        {
            if (!IsAuthority || catalog == null) return;
            State = new ArchiveTeamState { seed = seed, stations = Enumerable.Range(0, 10).Select(i => new ArchiveStationState { topic = i }).ToArray() };
            questions.Clear();lastSubmit.Clear();localSequence=0; Commit();
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
            var player = ActorPlayer(actor);
            if (player == null || !player.gameObject.activeInHierarchy || player.IsMovementLocked) return false;
            if (hunting != null && hunting.State == KhezuHuntState.Caught && hunting.CaughtPlayerId == actor) return false;
            return hunting == null || hunting.health == null || !hunting.health.HasAuthoritativeActorHealth || hunting.health.IsActorAlive(actor);
        }
        bool Near(int actor, int topic, Vector3 localPoint, float distance)
        {
            var p = ActorPlayer(actor); if (p == null) return false;
            var local = stations[topic].transform.InverseTransformPoint(p.transform.position);
            return Mathf.Abs(local.y) < 5 && Vector2.Distance(new Vector2(local.x, local.z), new Vector2(localPoint.x, localPoint.z)) <= distance;
        }
        public bool Accept(int actor, ArchiveCommand cmd)
        {
            if (!IsAuthority || !Ready || cmd == null || cmd.topic < 0 || cmd.topic >= 10 || !Enum.IsDefined(typeof(ArchiveAction), cmd.action)) return false;
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.GetPlayer(actor) == null) return false;
            if (!CanAct(actor) || !Near(actor, cmd.topic, Vector3.zero, 14)) return false;
            var record = State.actors.FirstOrDefault(p => p.actor == actor);
            if (record == null) { record = new ArchiveActor { actor = actor, displayName = actor==-1?"Local explorer":PhotonNetwork.CurrentRoom.GetPlayer(actor).NickName }; State.actors.Add(record); }
            if (cmd.sequence <= record.lastSequence) return false;
            record.lastSequence = cmd.sequence;
            var s = State.stations[cmd.topic]; var station = stations[cmd.topic];
            if (cmd.revision != s.revision) return false;
            if (cmd.action == ArchiveAction.Start)
            {
                if (s.phase != 0 || !Near(actor, cmd.topic, ArchiveTopicStation.BookPosition, 2.5f)) return false;
                s.phase = 1; ArchiveTopicQuestions.Prepare(Question(cmd.topic), s); s.routeActor = actor;
                s.feedback = "Challenge active. Your team shares this work."; Touch(s); return true;
            }
            if (s.phase != 1) return false;
            var q = Question(cmd.topic);
            switch (cmd.action)
            {
                case ArchiveAction.BeginChoice:
                    if (q.activity != ArchiveActivity.Choice || cmd.a < 0 || cmd.a >= q.items.Length || s.channelActor != 0 || !Near(actor, cmd.topic, station.ItemPosition(cmd.a, q), 1.15f)) return false;
                    s.channelActor = actor; s.channelChoice = cmd.a; s.channelStarted = Clock; s.channelSequence = cmd.sequence; break;
                case ArchiveAction.Pick:
                    if (q.activity == ArchiveActivity.Choice) return false;
                    if (cmd.a < 0 || cmd.a >= q.items.Length || !Near(actor, cmd.topic, station.CurrentItemPosition(cmd.a, q, s), 2.2f) || s.carries.Any(c => c.item == cmd.a && c.actor != actor && !q.reusableItems)) return false;
                    ReleaseCarried(actor);
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
                    if (q.mapping != null && q.mapping.questionKind == MappingQuestionKind.BuildExactRelation)
                    {
                        if (cmd.a < 0 || cmd.a >= q.inputLabels.Length || !Near(actor, cmd.topic, station.InputPosition(cmd.a), 2.4f)) return false;
                        record.mappingInput = cmd.a; record.mappingRound = s.round; break;
                    }
                    if (!Select(q,s,cmd.a)) return false; break;
                case ArchiveAction.Connect: case ArchiveAction.Remove:
                    if (q.mapping != null)
                    {
                        if (cmd.action != ArchiveAction.Connect || q.mapping.questionKind != MappingQuestionKind.BuildExactRelation ||
                            cmd.a < 0 || cmd.a >= q.inputLabels.Length || cmd.b < 0 || cmd.b >= q.outputLabels.Length ||
                            record.mappingInput != cmd.a || record.mappingRound != s.round || !Near(actor,cmd.topic,station.OutputPosition(cmd.b),2.4f) || s.links.Contains(cmd.a*100+cmd.b)) return false;
                        s.links.Add(cmd.a*100+cmd.b); break;
                    }
                    if (q.tree == null || !q.tree.Editable || cmd.a < 0 || cmd.b < 0 || cmd.a >= q.nodes.Length || cmd.b >= q.nodes.Length || cmd.a == cmd.b) return false;
                    int edge = cmd.a * 100 + cmd.b;
                    int reverse = cmd.b * 100 + cmd.a;
                    if (cmd.action == ArchiveAction.Remove)
                    {
                        if (!s.links.Remove(edge) && !s.links.Remove(reverse)) return false;
                    }
                    else
                    {
                        if (s.links.Contains(edge) || (!q.tree.Directed && s.links.Contains(reverse))) return false;
                        if (q.tree.type == LogicLegends.Trees.TreeChallengeType.Spanning && !q.tree.initial.Any(e => (e.source == q.nodeIds[cmd.a] && e.destination == q.nodeIds[cmd.b]) || (e.source == q.nodeIds[cmd.b] && e.destination == q.nodeIds[cmd.a]))) return false;
                        s.links.Add(edge);
                    } break;
                case ArchiveAction.Undo:
                    if (q.mapping == null || q.mapping.questionKind != MappingQuestionKind.BuildExactRelation || s.links.Count <= (q.mapping.initialPairsLocked ? q.mapping.initialPairs.Count : 0)) return false;
                    s.links.RemoveAt(s.links.Count - 1); break;
                case ArchiveAction.RetryRoute:
                    if (q.graph == null) return false;
                    s.route.Clear(); s.continuous = true; s.routeActor = actor; s.routeSampleValid = false; break;
                case ArchiveAction.Submit:
                    if (q.activity == ArchiveActivity.Choice || s.carries.Count > 0 || (q.graph != null && q.graph.type != LogicLegends.Graph.GraphChallengeType.Degree && s.routeActor != actor)) return false;
                    string attemptKey=actor+":"+s.topic+":"+s.round+":"+s.subphase;
                    if (lastSubmit.TryGetValue(attemptKey,out double previous) && Clock - previous < 1.5) return false;
                    lastSubmit[attemptKey] = Clock; Submit(actor,cmd.sequence,s,q); return true;
                default:return false;
            }
            Touch(s); return true;
        }
        bool Select(ArchiveQuestion q,ArchiveStationState s,int value)
        {
            if (q.mapping != null)
            {
                if (q.mapping.questionKind == MappingQuestionKind.ClassifyFunction) { if(value<0||value>1)return false;s.selection=value;return true; }
                if (q.mapping.questionKind == MappingQuestionKind.SelectProperties) { if(value!=2&&value!=4&&value!=8)return false;s.flags^=value;return true; }
                return false;
            }
            if(q.activity==ArchiveActivity.Method){if(value<0||value>2)return false;s.selection=value;return true;}
            if(q.graph!=null){if(q.graph.type!=LogicLegends.Graph.GraphChallengeType.Degree||value<0||value>20)return false;s.selection=value;return true;}
            if(q.tree!=null)
            {
                if(value>=100){if(value>120)return false;s.flags=value-100;}
                else{if(value<0||value>=q.nodeIds.Length)return false;s.selection=value;}
                return true;
            }
            return false;
        }
        void Submit(int actor,int sequence,ArchiveStationState s,ArchiveQuestion q)
        {
            bool correct=ArchiveTopicQuestions.Validate(q,s,out string feedback);
            s.channelActor=0;s.channelChoice=-1;
            if(correct)
            {
                if(s.topic==2&&s.subphase==0)
                {
                    s.subphase=1;ArchiveTopicQuestions.Prepare(Question(s.topic),s);s.feedback="Premises correct. Find the matching Rule Crystal.";
                }
                else
                {
                    s.reveal=q.successText??"";
                    s.round++;s.subphase=0;s.phase=s.round>=ArchiveTopicCatalog.RoundCount(s.topic)?2:0;
                    s.feedback=s.phase==2?"Topic complete for the team!":"Correct! Use the book to start the next challenge.";
                    s.carries.Clear();
                }
            }
            else
            {
                var record=State.actors.First(a=>a.actor==actor);record.mistakes++;
                State.mistakes.Add(new ArchiveMistake{actor=actor,topic=s.topic,round=s.round,revision=s.revision,sequence=sequence,time=Clock});
                s.feedback="Incorrect — "+ActorName(actor)+" recorded one mistake. "+feedback;
                if(hunting!=null)hunting.Request(KhezuCommand.AddMistake);
            }
            Touch(s);
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
            foreach(var s in State.stations) if(s.carries.RemoveAll(c=>c.actor==actor)>0)s.revision++;
        }
        void Touch(ArchiveStationState s){s.revision++;Commit();}
        void Commit()
        {
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
            if(!Ready||!IsAuthority||Time.unscaledTime<nextTick||Time.timeScale<=0)return;
            if(roomMode!=PhotonNetwork.InRoom){enabled=false;return;}
            nextTick=Time.unscaledTime+.1f;
            foreach(var s in State.stations)
            {
                if(s.phase!=1)continue;
                var q=Question(s.topic);bool dirty=false;
                foreach(var held in s.carries.ToArray())
                    if(!CanAct(held.actor)||!Near(held.actor,s.topic,Vector3.zero,18)){s.carries.Remove(held);dirty=true;}
                if(s.channelActor!=0)
                {
                    int actor=s.channelActor;
                    if(!CanAct(actor)||!Near(actor,s.topic,stations[s.topic].ItemPosition(s.channelChoice,q),1.15f)){s.channelActor=0;s.channelChoice=-1;dirty=true;}
                    else if(Clock-s.channelStarted>=answerChannelSeconds){s.selection=s.channelChoice;Submit(actor,s.channelSequence,s,q);continue;}
                }
                if(q.graph!=null&&q.graph.type!=LogicLegends.Graph.GraphChallengeType.Degree)dirty|=SampleRoute(s,q);
                if(dirty)Touch(s);
            }
        }
        bool SampleRoute(ArchiveStationState s,ArchiveQuestion q)
        {
            if(s.routeActor==0||!CanAct(s.routeActor))return false;
            var player=ActorPlayer(s.routeActor);if(player==null)return false;
            Vector3 current=stations[s.topic].transform.InverseTransformPoint(player.transform.position);
            var point=new Vector2(current.x,current.z);
            bool changed=false;
            if(s.route.Count>0&&s.continuous&&s.routeSampleValid)
            {
                float distance=Vector3.Distance(current,s.lastRoutePosition);
                if(distance>5)s.continuous=false;
                else for(float t=0;t<=1.001f;t+=1f/Mathf.Max(1,Mathf.CeilToInt(distance/.25f)))
                    if(!stations[s.topic].OnGraphCorridor(Vector2.Lerp(new Vector2(s.lastRoutePosition.x,s.lastRoutePosition.z),point,t),q)){s.continuous=false;break;}
                changed=!s.continuous;
            }
            s.lastRoutePosition=current;s.routeSampleValid=true;
            int node=Array.FindIndex(q.nodes,p=>Vector2.Distance(point,p+ArchiveTopicStation.GraphOffset)<.9f);
            if(node>=0)
            {
                if(s.route.Count==0){if(q.nodeIds[node]!=q.graph.start)return changed;s.route.Add(node);changed=true;}
                else if(s.route[s.route.Count-1]!=node){s.route.Add(node);changed=true;}
            }
            return changed;
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
                State=state;questions.Clear();trustedSnapshot=json;Changed?.Invoke();
            }
            catch(Exception e){Debug.LogWarning("Ignored malformed topic snapshot: "+e.GetType().Name);}
        }
        public override void OnPlayerLeftRoom(Photon.Realtime.Player other){if(IsAuthority&&Ready){ReleaseCarried(other.ActorNumber);foreach(var s in State.stations)if(s.routeActor==other.ActorNumber){s.routeActor=0;s.routeSampleValid=false;}Commit();}}
        public override void OnPlayerEnteredRoom(Photon.Realtime.Player other){if(IsAuthority&&Ready)Commit();}
        public override void OnMasterClientSwitched(Photon.Realtime.Player master)
        {
            if(!IsAuthority)return;
            if(!string.IsNullOrEmpty(trustedSnapshot))Receive(trustedSnapshot,true);
            if(!Ready&&PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CheckpointKey,out var value)&&value is string json)Receive(json,true);
            if(Ready){foreach(var s in State.stations){s.routeSampleValid=false;s.channelActor=0;}Commit();}
        }
        public override void OnLeftRoom(){State=null;Changed?.Invoke();}
        public override void OnDisconnected(DisconnectCause cause){State=null;Changed?.Invoke();}
        void OnDestroy(){if(Current==this)Current=null;}
    }
}
