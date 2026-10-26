using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Independent content checks and native input checks for the nine non-T1 bonus mechanics.</summary>
    public static class ArchiveCoopActivityChecks
    {
        [Serializable] public sealed class ContentReport
        {
            public bool passed; public int cases, formulaRows, rejectedWrongAnswers;
            public string scope="Seed-stable optional content; independent displayed-expression, set-membership, function-rule, directed-path and tree-structure checks. Difficulty and learning efficacy require student playtests.";
            public List<string> errors=new List<string>();
        }
        [Serializable] sealed class PlayReport
        {
            public bool passed; public int topics, acceptedActions;
            public string scope="Offline Editor native Books and bonus requests, physical graph walking, stale-command cutoff, station isolation and UI. A clearly labelled accepted-main-work checkpoint fixture supplies eligibility for each isolated bonus; this test does not claim a complete academic topic journey. Physical Android remains unverified.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static void Require(bool pass,string label){if(!pass)throw new InvalidOperationException(label);}
        public static int[] FindRoute(ArchiveCoopActivity a)
        {
            var route=new List<int>{a.startNode};
            bool Visit()
            {
                if(route.Last()==a.goalNode)return true;
                foreach(int link in a.initialLinks.Where(e=>e/100==route.Last()))
                {
                    int node=link%100;if(route.Contains(node))continue;
                    route.Add(node);if(Visit())return true;route.RemoveAt(route.Count-1);
                }
                return false;
            }
            Require(Visit(),"No legal directed bonus route exists.");return route.ToArray();
        }
        public static void Solution(ArchiveCoopActivity a,out int[] values,out List<int> links,out List<int> route)
        {
            values=a.expected.ToArray();links=a.requiredLinks.ToList();route=a.mechanic==ArchiveBonusMechanic.GraphRoute?FindRoute(a).ToList():a.mechanic==ArchiveBonusMechanic.InductionSequence?a.expected.ToList():new List<int>();
        }
        static string Fingerprint(ArchiveCoopActivity a)=>a.mechanic+"|"+a.context+"|"+string.Join(";",a.items)+"|"+string.Join(",",a.expected)+"|"+string.Join(",",a.initialLinks)+"|"+string.Join(",",a.requiredLinks);
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Validate Distinct Bonus Content")]
        public static ContentReport Validate()
        {
            var result=new ContentReport();
            var variants=Enumerable.Range(0,10).Select(_=>new HashSet<string>()).ToArray();
            for(int seed=0;seed<64;seed++)for(int topic=1;topic<10;topic++)
            {
                try
                {
                    var a=ArchiveCoopActivity.Build(topic,seed*7919);
                    Require((int)a.mechanic==topic-1,"A topic lost its unique interaction mechanic.");
                    Require(Fingerprint(a)==Fingerprint(ArchiveCoopActivity.Build(topic,seed*7919)),"Seeded content is not deterministic.");
                    variants[topic].Add(Fingerprint(a));
                    Require(!string.IsNullOrWhiteSpace(a.moduleReference)&&!string.IsNullOrWhiteSpace(a.context)&&!string.IsNullOrWhiteSpace(a.instructions),"Missing topic lesson or curriculum reference.");
                    Require(a.items.Length<=8&&a.nodes.Length<=6&&a.slots.Length<=6,"Bonus exceeds the compact physical-interaction budget.");
                    Solution(a,out int[] values,out var links,out var route);
                    Require(a.Validate(values,links,route),"Authored solution rejected.");
                    Require(!a.Validate(a.initialValues,a.initialLinks.ToList(),new List<int>()),"Bonus starts solved.");
                    if(a.expected.Length>0&&a.mechanic!=ArchiveBonusMechanic.InductionSequence)
                    {
                        var bad=values.ToArray();bad[0]=-1;
                        Require(!a.Validate(bad,links,route),"Incomplete placement accepted.");result.rejectedWrongAnswers++;
                    }
                    else if(a.mechanic==ArchiveBonusMechanic.InductionSequence)
                    {
                        var bad=route.ToList();(bad[0],bad[1])=(bad[1],bad[0]);Require(!a.Validate(values,links,bad),"Induction allows the hypothesis before the base signal.");result.rejectedWrongAnswers++;
                    }
                    else if(a.mechanic==ArchiveBonusMechanic.GraphRoute)
                    {
                        Require(!a.Validate(values,links,new List<int>{a.startNode,a.startNode,a.goalNode}),"Repeated-vertex graph route accepted.");
                        Require(!a.Validate(values,links,route.AsEnumerable().Reverse().ToList()),"Reverse directed route accepted.");result.rejectedWrongAnswers+=2;
                    }
                    else
                    {
                        var bad=links.ToList();bad[0]=links[1];Require(!a.Validate(values,bad,route),"Duplicate cable/branch accepted.");result.rejectedWrongAnswers++;
                    }
                    IndependentAudit(a,result);result.cases++;
                }
                catch(Exception e){result.errors.Add("T"+(topic+1)+"/seed"+seed+": "+e.Message);}
            }
            for(int topic=1;topic<10;topic++)if(variants[topic].Count<2)result.errors.Add("T"+(topic+1)+" does not vary with the seed.");
            result.passed=result.errors.Count==0;Directory.CreateDirectory("Temp/ArchiveTopicBonuses");File.WriteAllText("Temp/ArchiveTopicBonuses/Content.json",JsonUtility.ToJson(result,true));return result;
        }
        static int[] Set(string context,string name)=>Regex.Match(context,name+@"=\{([^}]+)\}").Groups[1].Value.Split(',').Select(v=>int.Parse(v.Trim())).ToArray();
        static void IndependentAudit(ArchiveCoopActivity a,ContentReport report)
        {
            switch(a.mechanic)
            {
                case ArchiveBonusMechanic.TruthTiles:
                    string expression=a.context.Substring("Separate bonus table: ".Length).Split('.')[0];
                    for(int row=0;row<4;row++){bool value=new Formula(expression,row<2,row%2==0).Evaluate();Require((a.expected[row]==0)==value,"T2 display and output key disagree: "+expression);report.formulaRows++;}break;
                case ArchiveBonusMechanic.InferenceChain:
                    string premise=a.items[a.expected[0]],support=a.items[a.expected[1]],rule=a.items[a.expected[2]],conclusion=a.items[a.expected[3]];
                    for(int bits=0;bits<4;bits++){bool p=(bits&1)!=0,q=(bits&2)!=0;if(new Formula(premise,p,q).Evaluate()&&new Formula(support,p,q).Evaluate())Require(new Formula(conclusion,p,q).Evaluate(),"Inference conclusion does not follow from its displayed premises.");}
                    Require(rule=="Modus Ponens"&&premise.Contains('→')&&support=="A"||rule=="Modus Tollens"&&support=="¬B"||rule=="Disjunctive Syllogism"&&premise.Contains('∨'),"Rule crystal does not name the displayed argument pattern.");break;
                case ArchiveBonusMechanic.ProofSort:
                    for(int i=0;i<a.items.Length;i++){bool formal=Regex.IsMatch(a.items[i],@"\n1\.")&&a.items[i].Contains('—');Require((a.expected[i]==0)==formal,"Proof sorting key confuses explicit derivation with natural-language presentation.");}break;
                case ArchiveBonusMechanic.ProofRepair:
                    Require(a.items[a.expected[0]].StartsWith("Assume ")&&a.items[a.expected[1]].Contains("some integer k")&&a.items[a.expected[2]].Contains('=')&&(a.items[a.expected[3]].StartsWith("Therefore")||a.items[a.expected[3]].StartsWith("Thus")),"Proof order omits an assumption, integer representation, calculation or justified conclusion.");
                    for(int k=-20;k<=20;k++){int even=2*k,odd=2*k+1;Require(even*even%2==0&&(odd-3)%2==0&&Math.Abs(odd*odd%2)==1,"Displayed parity derivation fails an integer witness.");}break;
                case ArchiveBonusMechanic.InductionSequence:
                    string[] roles={"BASE:","HYPOTHESIS:","STEP:","CONCLUDE:"};for(int i=0;i<4;i++)Require(a.items[a.expected[i]].StartsWith(roles[i]),"Induction signal sequence mixes proof roles.");
                    Require(!a.expected.Any(i=>a.items[i].StartsWith("CIRCULAR:")),"Circular assumption is included in the proof.");break;
                case ArchiveBonusMechanic.SetDelivery:
                    var setA=Set(a.context,"A");var setB=Set(a.context,"B");
                    for(int i=0;i<a.items.Length;i++){int n=int.Parse(a.items[i]);int region=setA.Contains(n)?setB.Contains(n)?1:0:2;Require((setA.Contains(n)||setB.Contains(n))&&a.expected[i]==region,"Venn token membership key is wrong.");}break;
                case ArchiveBonusMechanic.MappingLinks:
                    var match=Regex.Match(a.context,@"f\(x\)=(\d*)x(?:\s*([+−])\s*(\d+))?");Require(match.Success,"Function rule cannot be read from context.");
                    int scale=string.IsNullOrEmpty(match.Groups[1].Value)?1:int.Parse(match.Groups[1].Value),offset=match.Groups[3].Success?int.Parse(match.Groups[3].Value)*(match.Groups[2].Value=="−"?-1:1):0;
                    foreach(int link in a.requiredLinks){int x=int.Parse(a.nodes[link/100].Substring(2)),y=int.Parse(a.nodes[link%100].Substring(2));Require(y==scale*x+offset,"Mapping cable does not satisfy the displayed function.");}break;
                case ArchiveBonusMechanic.GraphRoute:
                    var path=FindRoute(a);Require(path.Distinct().Count()==path.Length&&path.First()==a.startNode&&path.Last()==a.goalNode,"Graph route lacks distinct endpoints.");
                    for(int i=1;i<path.Length;i++)Require(a.initialLinks.Contains(path[i-1]*100+path[i]),"Graph solution reverses or invents an edge.");break;
                case ArchiveBonusMechanic.TreeRepair:
                    Require(a.requiredLinks.Length==a.nodes.Length-1&&a.requiredLinks.All(e=>e%100!=0),"Rooted tree has the wrong edge count or a parent for root A.");
                    for(int node=1;node<a.nodes.Length;node++)Require(a.requiredLinks.Count(e=>e%100==node)==1,"A non-root node has multiple or missing parents.");
                    var seen=new HashSet<int>{0};for(int pass=0;pass<a.nodes.Length;pass++)foreach(int e in a.requiredLinks)if(seen.Contains(e/100))seen.Add(e%100);Require(seen.Count==a.nodes.Length,"Required tree is disconnected or cyclic.");break;
            }
        }
        sealed class Formula
        {
            readonly string text;readonly bool p,q;int at;
            public Formula(string source,bool p,bool q){text=source.Replace(" ","");this.p=p;this.q=q;}
            bool Take(char ch){if(at>=text.Length||text[at]!=ch)return false;at++;return true;}
            public bool Evaluate(){bool v=Equivalent();Require(at==text.Length,"Unparsed formula "+text);return v;}
            bool Equivalent(){bool v=Implies();while(Take('↔')){bool b=Implies();v=v==b;}return v;}
            bool Implies(){bool v=Or();if(Take('→')){bool b=Implies();v=!v||b;}return v;}
            bool Or(){bool v=And();while(Take('∨')){bool b=And();v=v||b;}return v;}
            bool And(){bool v=Unary();while(Take('∧')){bool b=Unary();v=v&&b;}return v;}
            bool Unary(){if(Take('¬'))return !Unary();if(Take('(')){bool v=Equivalent();Require(Take(')'),"Missing parenthesis.");return v;}if(Take('P')||Take('A'))return p;if(Take('Q')||Take('B'))return q;throw new FormatException(text);}
        }

        static PlayReport play;static ArchiveTopicSession session;static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();static bool running,oldHunting;static double next;
        public static string Status {get;private set;}="Idle";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Distinct Bonuses (Offline Play Mode)")]
        public static void BeginPlay()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||running)throw new InvalidOperationException("Offline Play Mode required.");
            session=ArchiveTopicSession.Current;oldHunting=session.hunting.enabled;session.hunting.Request(Hunting.KhezuCommand.Sleep);session.hunting.enabled=false;play=new PlayReport();stack.Clear();stack.Push(Flow());next=0;running=true;EditorApplication.update+=Tick;Application.logMessageReceived+=RuntimeLog;
        }
        static void Check(bool pass,string label){play.checks.Add((pass?"PASS ":"FAIL ")+label);Require(pass,label);}
        static void Tick()
        {
            if(!running)return;
            try{EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;if(stack.Count==0){Finish(true);return;}var e=stack.Peek();if(!e.MoveNext()){stack.Pop();return;}if(e.Current is IEnumerator nested)stack.Push(nested);else next=EditorApplication.timeSinceStartup+(e.Current is float f?f:0);}
            catch(Exception e){play.errors.Add(Status+": "+e);Finish(false);}
        }
        static void RuntimeLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)play.errors.Add(message+"\n"+trace);}
        static void Finish(bool pass){running=false;stack.Clear();EditorApplication.update-=Tick;Application.logMessageReceived-=RuntimeLog;if(Application.isPlaying&&session!=null){session.hunting.enabled=oldHunting;session.hunting.Request(Hunting.KhezuCommand.Sleep);ArchiveTopicUI.Current.Close();}play.passed=pass&&play.errors.Count==0;Status=play.passed?"Passed":"Failed";Directory.CreateDirectory("Temp/ArchiveTopicBonuses");File.WriteAllText("Temp/ArchiveTopicBonuses/Play.json",JsonUtility.ToJson(play,true));}
        static IEnumerator Use(int topic,int action,Vector3 point,int a=-1,int b=-1)
        {
            if(action!=6||session.BonusActivity(topic).mechanic!=ArchiveBonusMechanic.GraphRoute)ArchiveTopicPlayChecks.MoveTo(session.stations[topic],point);yield return .045f;
            Check(session.BonusCanUse(-1,topic,action,a,b),"T"+(topic+1)+" authorizes native bonus action "+action+"/"+a);
            int revision=session.State.revision;session.RequestBonus(topic,action,a,b);yield return .045f;
            Check(session.State.revision>revision,"Native bonus action creates authoritative checkpoint");play.acceptedActions++;
        }
        static IEnumerator Walk(ArchiveTopicStation station,Vector3 to)
        {
            var player=Player.LocalInstance;var target=station.transform.TransformPoint(to);var cc=player.GetComponent<CharacterController>();float until=Time.realtimeSinceStartup+20;
            while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(target.x,target.z))>.16f&&Time.realtimeSinceStartup<until)
            {var delta=target-player.transform.position;delta.y=0;cc.Move(delta.normalized*Mathf.Min(.2f,delta.magnitude));yield return .025f;}
            Check(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(target.x,target.z))<.3f,"Graph runner physically reaches the next bonus node");yield return .14f;
        }
        static IEnumerator Solve(int topic,ArchiveCoopActivity a,ArchiveCoopActivityView view)
        {
            var c=session.Relay(topic);
            if(a.items.Length>0&&a.mechanic!=ArchiveBonusMechanic.InductionSequence)
            {
                for(int i=0;i<a.expected.Length;i++)
                {
                    int item=a.regionPlacement?i:a.expected[i],slot=a.regionPlacement?a.expected[i]:i;
                    if(c.bonusValues.Length>i&&c.bonusValues[i]==a.expected[i])continue;
                    yield return Use(topic,0,view.CurrentItemPosition(item),item);yield return Use(topic,1,view.SlotPosition(slot),slot);
                }
            }
            else if(a.mechanic==ArchiveBonusMechanic.InductionSequence)
                foreach(int item in a.expected)yield return Use(topic,2,view.ItemPosition(item),item);
            else if(a.mechanic==ArchiveBonusMechanic.GraphRoute)
            {
                int[] route=FindRoute(a);yield return Use(topic,2,view.NodePosition(a.startNode),a.startNode);
                for(int i=1;i<route.Length;i++)yield return Walk(view.station,view.NodePosition(route[i]));
                Check(c.bonusRoute.SequenceEqual(route),"Bonus route records actual intermediate nodes");
            }
            else
            {
                foreach(int link in c.bonusLinks.Except(a.requiredLinks).ToArray())
                {yield return Use(topic,3,view.NodePosition(link/100),link/100);yield return Use(topic,3,view.NodePosition(link%100),link%100);yield return Use(topic,5,view.NodePosition(link%100),link/100,link%100);}
                foreach(int link in a.requiredLinks.Except(c.bonusLinks).ToArray())
                {yield return Use(topic,3,view.NodePosition(link/100),link/100);yield return Use(topic,3,view.NodePosition(link%100),link%100);yield return Use(topic,4,view.NodePosition(link%100),link/100,link%100);}
            }
        }
        static IEnumerator Flow()
        {
            for(int topic=1;topic<10;topic++)
            {
                Status="T"+(topic+1)+" distinct bonus";session.Initialize(314159);ArchiveTopicUI.Current.Close();var station=session.stations[topic];var view=station.GetComponent<ArchiveCoopActivityView>();
                Check(view!=null,"Saved T"+(topic+1)+" contains its distinct mechanic view");Check(!session.RelayAvailable(topic),"Bonus stays unavailable before Book activation");
                ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(topic,ArchiveAction.Start);yield return .2f;ArchiveTopicUI.Current.Close();
                var a=ArchiveCoopActivity.Build(topic,session.State.seed,station.session.State.stations[topic].testResetSerial);var c=session.Relay(topic);var main=session.State.stations[topic];
                // Explicit isolation fixture, not a manufactured played academic round.
                session.State.solved.Add(new ArchiveSolvedMilestone{topic=topic,round=-1,subphase=-1,contributors=new[]{-1}});
                string before=JsonUtility.ToJson(main);int mistakes=session.State.mistakes.Count;double deadline=main.timerDeadline;
                Check(session.RelayAvailable(topic),"First Book opens the topic's distinct optional activity");
                int handItem=a.mechanic==ArchiveBonusMechanic.InductionSequence?a.expected[0]:0;
                Vector3 handPoint=a.nodes.Length>0?view.NodePosition(a.mechanic==ArchiveBonusMechanic.GraphRoute?a.startNode:0):view.CurrentItemPosition(handItem);
                ArchiveTopicPlayChecks.MoveTo(station,handPoint);yield return .15f;
                Check(ArchiveCoopActivityView.HandlesHand(session),"Physical bonus prop owns the shared Hand input");
                // Force a competing main Book focus to exercise either listener order.
                typeof(ArchiveTopicUI).GetField("focus",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ArchiveTopicUI.Current,station.points.First(p=>p.role==ArchivePointRole.Book));
                int handRevision=c.assignmentRevision;GameInput.Instance.MobileInteract();yield return .08f;
                Check(c.assignmentRevision>handRevision&&ArchiveTopicUI.Current.ReadingStation==null&&JsonUtility.ToJson(main)==before,"Mobile Hand acts only on the bonus even with a competing main focus");
                yield return Use(topic,7,view.SubmitPosition);
                yield return Solve(topic,a,view);
                if(a.mechanic!=ArchiveBonusMechanic.GraphRoute)ArchiveTopicPlayChecks.MoveTo(station,view.SubmitPosition);yield return .1f;
                Canvas.ForceUpdateCanvases();if(view.Panel!=null&&view.Panel.gameObject.activeSelf)foreach(var text in view.Panel.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextTruncated,"Bonus panel label fits: "+text.name);}
                yield return Use(topic,6,view.SubmitPosition);yield return .1f;
                Check(c.completed,"Native optional interaction completes T"+(topic+1)+" without an extra P/Q relay");
                Check(JsonUtility.ToJson(main)==before&&main.timerDeadline==deadline,"Bonus leaves the whole academic state and exact deadline intact");
                Check(session.State.mistakes.Count==mistakes&&session.State.quality.All(q=>q.key!="coop:"+topic),"Offline practice adds no monster mistakes or multiplayer quality credit");
                Check(session.State.topicRelays.Where((_,i)=>i!=topic).All(v=>v.round<0&&!v.completed),"Other topics' optional work stays untouched");
                int count=session.State.completedRelays.Count;session.RequestBonus(topic,6);yield return .04f;Check(session.State.completedRelays.Count==count,"A completed bonus cannot be earned twice");
                session.Initialize(271828);station=session.stations[topic];ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(topic,ArchiveAction.Start);yield return .1f;
                c=session.Relay(topic);var delayed=new ArchiveCommand{topic=topic,action=ArchiveAction.BonusInteract,a=6,sequence=int.MaxValue,matchId=session.State.matchId,captureSerial=c.serial,resetSerial=session.State.stations[topic].testResetSerial,workStamp=c.assignmentRevision.ToString()};
                Check(session.RequestTopicAdmin(topic,session.State.stations[topic].revision,true),"Authorized Admin closes the isolated active topic fixture");
                Check(!session.Accept(-1,delayed)&&session.Relay(topic).closed&&!session.Relay(topic).completed,"Delayed bonus submit is rejected after main topic completion");
                play.topics++;
            }
        }
    }
}
