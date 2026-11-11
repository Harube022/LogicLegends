using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using LogicLegends.Graph;

namespace LogicLegends.Students
{
    // Scene-local population; nothing is written to equipment, rewards or account saves.
    public sealed class StudentNpcDirector : MonoBehaviour
    {
        public static StudentNpcDirector Active { get; private set; }
        public StudentNpcCatalog Config { get; private set; }
        public bool CatalogReady { get; private set; }
        public bool Paused { get; private set; }=true;
        public bool AllowsRunning=>state!=null&&(state.Trial==2||state.Trial==3);
        public bool HideModelsForView=>Time.timeScale<=0||GameInput.Instance!=null&&GameInput.Instance.GameplayInputBlocked;
        readonly List<StudentNpcAgent> students=new List<StudentNpcAgent>();
        readonly StudentHintLedger ledger=new StudentHintLedger();
        readonly Dictionary<string,bool> hintEligibility=new Dictionary<string,bool>();
        readonly Dictionary<string,int> lastDialogueLine=new Dictionary<string,int>();
        readonly List<Vector3> keepOut=new List<Vector3>();
        readonly List<Bounds> hazards=new List<Bounds>();
        readonly System.Random random=new System.Random();
        MonoBehaviour sourceComponent;
        IStudentTrialSource source;
        StudentTrialState state;
        StudentNpcDialogue dialogue;
        Coroutine building;
        string populationKey,equipped;
        float refresh;
        float nextSnapshot;
        bool reported;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Active=null;SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneLoaded+=SceneLoaded;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FirstScene()=>Install(SceneManager.GetActiveScene());
        static void SceneLoaded(Scene scene,LoadSceneMode mode)=>Install(scene);
        static void Install(Scene scene)
        {
            if(!Application.isPlaying||!scene.IsValid())return;
            int trial;
            try{trial=TrialCatalog.ForScene(scene.name).Number;}catch(ArgumentException){return;}
            if(trial==1&&StageSelectionState.SelectedStage==2)return;
            foreach(var root in scene.GetRootGameObjects())if(root.GetComponent<StudentNpcDirector>()!=null)return;
            var go=new GameObject("RoamingStudents");SceneManager.MoveGameObjectToScene(go,scene);
            var director=go.AddComponent<StudentNpcDirector>();director.StartCoroutine(director.LoadCatalog());
        }
        IEnumerator LoadCatalog()
        {
            // Import/integrate skin dependencies after the outgoing scene unloads,
            // under the shared loading artwork rather than blocking sceneLoaded.
            var request=Resources.LoadAsync<StudentNpcCatalog>("Students/StudentNpcCatalog");
            yield return request;Config=request.asset as StudentNpcCatalog;CatalogReady=true;
            if(Config==null)Debug.LogWarning("Student NPC catalog missing. Use Logic Legends/Students/Prepare and validate catalog.");
        }
        void Awake(){Active=this;dialogue=gameObject.AddComponent<StudentNpcDialogue>();}
        void Update()
        {
            if(Config==null)return;
            if(sourceComponent==null)
            {
                if(Time.unscaledTime<refresh)return;refresh=Time.unscaledTime+1;
                source=null;Paused=true;
                foreach(var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if(component.gameObject.scene==gameObject.scene&&component is IStudentTrialSource candidate)
                    {
                        if(!TrySnapshot(candidate,out var snapshot)||!snapshot.Ready)continue;
                        sourceComponent=component;source=candidate;break;
                    }
                if(source==null)return;
            }
            // Stage snapshots are read-only. Some controllers initialise their bank in Start.
            // Snapshot providers build strings/arrays; roaming does not need a new
            // allocation-heavy puzzle snapshot on every rendered frame.
            if (Time.unscaledTime < nextSnapshot)
            {
                if (Time.timeScale <= 0 || GameInput.Instance == null || GameInput.Instance.GameplayActionsBlocked ||
                    Player.LocalInstance == null || Player.LocalInstance.IsMovementLocked) Paused = true;
                return;
            }
            nextSnapshot = Time.unscaledTime + .1f;
            StudentTrialState next;
            if(!TrySnapshot(source,out next)){Paused=true;sourceComponent=null;source=null;return;}
            if(!next.Ready){Paused=true;sourceComponent=null;source=null;refresh=Time.unscaledTime+.5f;return;}
            if(dialogue.IsOpen && (next.Challenge!=state.Challenge||next.QuestionKey!=state.QuestionKey||next.Finished||dialogue.IsHint&&next.Action.Key!=state.Action.Key))dialogue.CloseImmediate();
            state=next;
            Paused=state.Finished||state.Action.Paused||Time.timeScale<=0||GameInput.Instance==null||GameInput.Instance.GameplayActionsBlocked||
                Player.LocalInstance==null||Player.LocalInstance.IsMovementLocked||TrialContextHints.Instance!=null&&TrialContextHints.Instance.Suspended;
            if(state.Finished)
            {
                if(building!=null){StopCoroutine(building);building=null;}
                ClearPopulation();populationKey=null;return;
            }
            string current=StudentNpcPolicy.PopulationKey(state.Trial,state.Challenge,state.QuestionKey);
            // Graph question geometry is regenerated; never retain actors on old platforms.
            if(state.Trial==9&&(!state.GraphVisible||state.Challenge==1))
            {
                if(building!=null){StopCoroutine(building);building=null;}
                ClearPopulation();populationKey=null;return;
            }
            var profile=Player.LocalInstance!=null?Player.LocalInstance.GetComponentInChildren<IntegratedSkinProfile>():null;
            string selected=profile!=null?profile.gender+"_"+profile.category:null;
            if(selected!=equipped)
            {
                equipped=selected;
                // Admin character switching never permits an identical student skin.
                foreach(var student in students.ToArray())if(SameSkin(student.Identity,selected))
                {
                    students.Remove(student);student.gameObject.SetActive(false);
                    var replacement=Config.skins.Where(s=>s.prefab!=null&&!SameSkin(s.id,selected)&&students.All(n=>n.Identity!=s.id)).OrderBy(_=>random.Next()).FirstOrDefault();
                    if(replacement!=null)
                    {
                        var nextStudent=CreateStudent(replacement,student.Map,student.transform.position,student.transform.rotation);
                        if(ledger.Used(student.Identity,state.Challenge))ledger.Consume(nextStudent.Identity,state.Challenge);
                    }
                    Destroy(student.gameObject);
                }
            }
            if(current!=populationKey)
            {
                if(building!=null)StopCoroutine(building);
                ClearPopulation();populationKey=current;
                building=StartCoroutine(BuildPopulation(state));
            }
        }
        static bool SameSkin(string a,string b)=>!string.IsNullOrEmpty(b)&&string.Equals(a?.Replace("Elte","Elite"),b.Replace("Elte","Elite"),StringComparison.OrdinalIgnoreCase);
        static bool TrySnapshot(IStudentTrialSource candidate,out StudentTrialState snapshot)
        {
            snapshot=null;
            try{snapshot=candidate.StudentState();return snapshot!=null;}
            catch(NullReferenceException){return false;}
            catch(IndexOutOfRangeException){return false;}
            catch(ArgumentOutOfRangeException){return false;}
        }
        IEnumerator BuildPopulation(StudentTrialState initial)
        {
            yield return null; // Also gives zero-population challenges a clean coroutine lifetime.
            while(Paused){yield return null;if(state==null||state.Finished)yield break;}
            CollectSafety(gameObject.scene,state.Areas,keepOut,hazards);
            var available=Config.skins.Where(s=>s!=null&&s.prefab!=null&&!SameSkin(s.id,equipped)).OrderBy(_=>random.Next()).ToList();
            var seeds=initial.Trial==1?initial.Areas.Select(t=>t.position).ToArray():new[]{Player.LocalInstance.transform.position};
            if(seeds.Length==0)seeds=new[]{Player.LocalInstance.transform.position};
            foreach(var seed in seeds)
            {
                int count=StudentNpcPolicy.Population(initial.Trial,initial.Challenge,random);
                if(count==0)continue;
                var platforms=initial.Trial==9?FindObjectsByType<GraphVertex>(FindObjectsSortMode.None):null;
                // Flood outward from actual playable ground. The old extra house
                // seeds landed on isolated raised porches and supplied all early
                // spawn candidates before the field had finished scanning.
                var map=CreateWalkMap(initial.Trial,Config,keepOut,hazards,platforms,gameObject.scene);
                var builder=map.Build(seed);bool done=false;
                while(!(done=!builder.MoveNext()))
                {
                    // Begin with a verified local area while the wider-map scan
                    // continues incrementally. Players need not wait for the whole island.
                    if(initial.Trial!=9&&map.Nodes.Count>=(initial.Trial==2||initial.Trial==3?512:256))break;
                    yield return null;
                }
                if(state==null||state.Finished)yield break;
                var candidates=map.Nodes.Select((node,index)=>index).OrderBy(_=>random.Next()).ToList();
                // Distribute persistent-map students into different parts of the reachable map.
                for(int n=0;n<count&&available.Count>0;n++)
                {
                    available.RemoveAll(s=>SameSkin(s.id,equipped)||students.Any(a=>a.Identity==s.id));
                    if(available.Count==0)break;
                    int chosen=-1;float best=-1;
                    foreach(int i in candidates)
                    {
                        var p=map.Nodes[i].Point;
                        if(map.Nodes[i].Links.Count<2||!map.Clear(p)||!CharacterSpaceFree(null,p,Config.radius*4)||StudentWalkMap.PlanarSquare(p,Player.LocalInstance.transform.position)<64)continue;
                        float distance=students.Count==0?random.Next(100):students.Min(s=>StudentWalkMap.PlanarSquare(s.transform.position,p));
                        if(distance>best){chosen=i;best=distance;}
                    }
                    if(chosen<0)break;
                    var skin=available[0];available.RemoveAt(0);
                    CreateStudent(skin,map,map.Nodes[chosen].Point,Quaternion.Euler(0,random.Next(360),0));
                    yield return null;
                }
                if(map.Nodes.Count==0&&!reported){reported=true;Debug.LogWarning("Students: no safe walkable nodes in an area. No unsafe fallback spawn was used.",this);}
                if(!done)while(builder.MoveNext())yield return null;
            }
            building=null;
        }
        StudentNpcAgent CreateStudent(StudentNpcCatalog.Skin skin,StudentWalkMap map,Vector3 position,Quaternion rotation)
        {
            var actor=new GameObject("Student "+skin.displayName);actor.transform.SetParent(transform,false);actor.transform.SetPositionAndRotation(position,rotation);
            var agent=actor.AddComponent<StudentNpcAgent>();agent.Configure(this,skin,Array.IndexOf(Config.skins,skin),map);students.Add(agent);return agent;
        }
        public static StudentWalkMap CreateWalkMap(int trial,StudentNpcCatalog settings,List<Vector3> interactions,List<Bounds> water,GraphVertex[] platforms,Scene scene)
        {
            // The field has narrow walkable gaps between its vegetation and
            // paths. A finer grid finds them without shrinking NPC collision.
            return new StudentWalkMap(settings,interactions,water,platforms,scene,null,trial==2?1.25f:0,trial==2?30000:0);
        }
        public static void CollectSafety(Scene scene,IEnumerable<Transform> areas,List<Vector3> keepOut,List<Bounds> hazards)
        {
            keepOut.Clear();hazards.Clear();
            foreach(var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if(component.gameObject.scene!=scene)continue;
                string name=component.GetType().Name.ToLowerInvariant();
                if(name.Contains("bookstatue")||name.Contains("housedoor")||name=="truthtabledoorinteraction"||name=="setplacementzone"||name.Contains("inferencecrystal")||name.Contains("scroll")||name.Contains("station")||name.Contains("altar"))
                    keepOut.Add(component.transform.position);
                if(component is TruthTableDoorInteraction houses)for(int i=0;i<6;i++)
                {var door=houses.GetTutorialDoor(i);if(door!=null)keepOut.Add(door.bounds.center);}
            }
            foreach(var collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if(collider.gameObject.scene!=scene)continue;
                string name=collider.name.ToLowerInvariant();
                // Stairs are transit surfaces, not interaction points. Excluding
                // their centres split the main field from the raised walkways.
                if(name.Contains("door")||name.Contains("book_statue")||name.Contains("informationboard"))keepOut.Add(collider.bounds.center);
            }
            foreach(var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if(renderer.gameObject.scene!=scene)continue;
                string name=renderer.name.ToLowerInvariant();
                if(name.Contains("water")||name.Contains("ocean")||name.Contains("river"))hazards.Add(renderer.bounds);
            }
            if(areas!=null)foreach(var area in areas)if(area!=null)keepOut.Add(area.position);
        }
        public static List<Vector3> HouseEntranceSeeds(Scene scene,Vector3 center)
        {
            var result=new List<Vector3>();
            foreach(var houses in FindObjectsByType<TruthTableDoorInteraction>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(houses.gameObject.scene!=scene)continue;
                var central=houses.TutorialBook!=null?houses.TutorialBook.position:center;
                for(int i=0;i<6;i++)
                {
                    var door=houses.GetTutorialDoor(i);if(door==null)continue;
                    var foot=door.bounds.center;foot.y=door.bounds.min.y;
                    var outward=central-foot;outward.y=0;
                    if(outward.sqrMagnitude<.01f)outward=door.transform.forward;
                    result.Add(foot+outward.normalized*7);
                }
            }
            return result;
        }
        public bool CharacterSpaceFree(StudentNpcAgent self,Vector3 position,float separation=0)
        {
            if(separation<=0)separation=Config.radius*2.5f;
            var player=Player.LocalInstance;
            if(player!=null&&StudentWalkMap.PlanarSquare(position,player.transform.position)<separation*separation)return false;
            foreach(var other in students)if(other!=null&&other!=self&&StudentWalkMap.PlanarSquare(position,other.transform.position)<separation*separation)return false;
            return true;
        }
        public bool AreaActive(Vector3 point)=>state==null||state.Trial!=1||Player.LocalInstance==null||StudentWalkMap.PlanarSquare(point,Player.LocalInstance.transform.position)<16900;
        string EligibilityKey(StudentNpcAgent student)=>state.Challenge+":"+student.Identity;
        public string NextCasual(string identity)
        {
            var lines=StudentDialogueBank.Lines(identity);
            int previous=lastDialogueLine.TryGetValue(identity,out int value)?value:-1;
            int next=StudentDialogueBank.NextIndex(lines.Length,previous,random);
            if(next<0)return null;
            lastDialogueLine[identity]=next;return lines[next];
        }
        public bool HintAvailable(StudentNpcAgent student)
        {
            if(state==null||state.Finished)return false;
            string key=EligibilityKey(student);
            if(!hintEligibility.TryGetValue(key,out bool allowed))hintEligibility[key]=allowed=random.NextDouble()<Config.hintChance;
            return allowed&&!ledger.Used(student.Identity,state.Challenge);
        }
        public static bool TryTalk()=>Active!=null&&Active.OpenNearestConversation();
        bool OpenNearestConversation()
        {
            if(Paused||state==null||!state.Action.Available)return false;
            if(dialogue.IsVisible&&!dialogue.IsOpen)return true;
            var player=Player.LocalInstance;if(player==null)return false;
            StudentNpcAgent nearest=null;float distance=Config.talkRange*Config.talkRange;
            foreach(var student in students)
            {
                float d=StudentWalkMap.PlanarSquare(player.transform.position,student.transform.position);
                var direction=student.transform.position-player.transform.position;direction.y=0;
                if(d>=distance||Mathf.Abs(student.transform.position.y-player.transform.position.y)>Config.height||Vector3.Dot(player.transform.forward,direction.normalized)<.65f)continue;
                var from=player.transform.position+Vector3.up*Config.height*.6f;var to=student.transform.position+Vector3.up*Config.height*.6f;
                if(Physics.Linecast(from,to,out var hit,~0,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<Player>()==null)continue;
                nearest=student;distance=d;
            }
            if(nearest==null)return false;
            if(dialogue.IsOpen&&dialogue.Speaker==nearest)return true;
            bool hint=HintAvailable(nearest)&&ledger.Consume(nearest.Identity,state.Challenge);
            string line=hint?StudentConversation.Hint(nearest.Style,state):nearest.NextChat();
            dialogue.Show(nearest,line,hint);
            return true;
        }
        void ClearPopulation(){if(dialogue!=null)dialogue.CloseImmediate();foreach(var student in students)if(student!=null){student.gameObject.SetActive(false);Destroy(student.gameObject);}students.Clear();}
        void OnDisable(){if(dialogue!=null)dialogue.CloseImmediate();if(Active==this)Active=null;}
    }
}
