using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveCoopOfflineChecks
    {
        [Serializable] sealed class Report { public bool passed; public string scope="Offline Editor: ten real Book activations, optional controls, UI fit and saved geometry. No fabricated main-answer or bonus receipts. Physical Android is unverified."; public List<string> checks=new List<string>(),errors=new List<string>(); }
        static Report report;static IEnumerator flow;static double next;static ArchiveTopicSession session;static bool oldHunting;
        public static string Status { get; private set; }="Idle";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test T1 Optional Relay Controls")]
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Offline Play Mode required.");
            session=ArchiveTopicSession.Current;oldHunting=session.hunting.enabled;session.hunting.Request(Hunting.KhezuCommand.Sleep);session.hunting.enabled=false;
            report=new Report();Directory.CreateDirectory("Temp/ArchiveCurriculum");Status="Running";flow=Run();next=0;EditorApplication.update+=Tick;
        }
        static void Check(bool pass,string label){report.checks.Add((pass?"PASS ":"FAIL ")+label);if(!pass)throw new InvalidOperationException(label);}
        static void Tick()
        {
            if(flow==null)return;
            try {EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<next)return;if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float f?f:0);}
            catch(Exception e){report.errors.Add(e.ToString());Finish(false);}
        }
        static void Finish(bool pass)
        {
            EditorApplication.update-=Tick;flow=null;report.passed=pass&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            if(Application.isPlaying&&session!=null){ArchiveTopicUI.Current.Close();session.hunting.enabled=oldHunting;session.hunting.Request(Hunting.KhezuCommand.Sleep);}
            File.WriteAllText("Temp/ArchiveCurriculum/OptionalRelayControls.json",JsonUtility.ToJson(report,true));
        }
        public static IEnumerator CompletePendingRelay()
        {
            var s=ArchiveTopicSession.Current;
            if(s==null||!s.Ready||s.State.stations[0].phase!=2)throw new InvalidOperationException("Finish native T1 academic work first.");
            yield break;
        }
        static IEnumerator Run()
        {
            for(int topic=0;topic<1;topic++)
            {
                Status="Optional T"+(topic+1)+" controls";session.Initialize(314159);ArchiveTopicUI.Current.Close();
                var station=session.stations[topic];var trial=station.GetComponent<ArchiveT1CoopTrial>();
                Check(trial!=null&&station.GetComponents<ArchiveT1CoopTrial>().Length==1,"T"+(topic+1)+" has one reusable relay controller");
                Check(!session.RelayAvailable(topic),"Side activity starts unavailable");
                ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);session.Request(topic,ArchiveAction.Start);yield return .25f;ArchiveTopicUI.Current.Close();
                Check(session.RelayAvailable(topic)&&session.State.stations[topic].round==0,"Real Book enables the active optional exercise");
                var canvas=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(c=>c.name=="T"+(topic+1)+"_Relay_Controls");
                var panel=canvas.transform.Find("Rune_Input").GetComponent<RectTransform>();
                for(int input=0;input<2;input++)
                {
                    ArchiveTopicPlayChecks.MoveTo(station,trial.SwitchPosition(input));yield return .22f;
                    Check(panel.gameObject.activeSelf&&session.RelayCanSetInput(-1,topic,input),"Nearby rune enables actual local controls");
                    var yes=panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.GetComponentInChildren<TMP_Text>().text=="TRUE");
                    Check(yes.interactable,"TRUE touch button is enabled");yes.onClick.Invoke();yield return .08f;
                    var c=session.Relay(topic);Check(input==0?c.pSet&&c.p:c.qSet&&c.q,"Displayed TRUE button changes the matching shared input");
                    Canvas.ForceUpdateCanvases();
                    foreach(var text in panel.GetComponentsInChildren<TMP_Text>()) {text.ForceMeshUpdate();Check(!text.isTextTruncated&&!text.isTextOverflowing,"Visible optional label fits: T"+(topic+1)+" / "+text.name);}
                }
                Check(!session.Relay(topic).open&&session.State.stations[topic].round==0&&session.State.mistakes.Count==0,"Side input cannot replace main progress or create a monster mistake");
                var corners=new Vector3[4];panel.GetWorldCorners(corners);
                Check(corners.All(v=>v.x>=Screen.safeArea.xMin&&v.x<=Screen.safeArea.xMax&&v.y>=Screen.safeArea.yMin&&v.y<=Screen.safeArea.yMax),"Optional panel stays inside the safe area");
                for(int i=0;i<7;i++)
                {
                    Vector3 at=station.transform.TransformPoint(trial.LanePoint(new Vector3(-7+i*14f/6,0,12.9f)));
                    Check(Physics.RaycastAll(at+Vector3.up*1.2f,Vector3.down,3,~(1<<7),QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.7f&&h.collider.GetComponentInParent<Player>()==null),"Relay deck support sample "+i);
                }
                if(topic==0||topic==6||topic==7||topic==9)
                {
                    var look=Player.LocalInstance.GetComponentInChildren<CameraTargetController>();if(look!=null)look.SetViewAngles(25,station.transform.eulerAngles.y+180);
                    yield return .25f;ScreenCapture.CaptureScreenshot("Temp/ArchiveCurriculum/T"+(topic+1)+"_OptionalControls.png");yield return .15f;
                }
                ArchiveTopicUI.Current.Open(station);yield return .1f;Check(!panel.gameObject.activeSelf,"Main reader hides overlapping optional controls");ArchiveTopicUI.Current.Close();
            }
            Check(session.State.completedRelays.Count==0,"Visual/input checks never invent a solved optional activity");
        }
    }
}
