using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LogicLegends.OpenArchive.Hunting;
namespace LogicLegends.OpenArchive.Topics.Editor
{
    // Exercises real UI commands in a fresh offline scene. No room, account or device writes.
    public static class ArchiveAdminUsabilityChecks
    {
        [Serializable] sealed class Report{public bool passed;public string scope="Native offline Unity Play Mode. Admin navigation, stop/resume, lighting, existing timer, inventory, teammate rescue, topic lock, capture release and reset. No physical-device or multi-client test.";public List<string> checks=new List<string>(),errors=new List<string>();}
        static Report report;static IEnumerator flow;static double next;static ArchiveTopicSession session;static ArchiveTopicTestControls ui;
        public static string Status{get;private set;}="Idle";
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use fresh offline MultiplayerGameplay Play Mode.");
            session=ArchiveTopicSession.Current;ui=session.GetComponent<ArchiveTopicTestControls>();report=new Report();flow=Flow();next=0;Status="Starting";Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
        }
        static void Log(string message,string trace,LogType type){if(report.errors.Count<8&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(message+"\n"+trace);}
        static void Tick(){try{if(EditorApplication.timeSinceStartup<next)return;EditorApplication.QueuePlayerLoopUpdate();if(!Application.isPlaying)throw new Exception("Play stopped");if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float?(float)flow.Current:0);}catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}}
        static void Finish(bool ok){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;report.passed=ok&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Save();}
        static void Save(){Directory.CreateDirectory("output/AdminLabUsability");File.WriteAllText("output/AdminLabUsability/playmode-checks.json",JsonUtility.ToJson(report,true));}
        static void Check(bool pass,string text){report.checks.Add((pass?"PASS ":"FAIL ")+text);Save();if(!pass)throw new Exception(text);}
        static Button Button(string name){return ui.GetComponentsInChildren<Button>(true).First(b=>b.name==name&&b.gameObject.activeInHierarchy);}
        static void Click(string name){var b=Button(name);Check(b.interactable,"Enabled UI action: "+name);b.onClick.Invoke();}
        static void Open(int tab=0){if(!ui.AdminPanelOpen)ui.ToggleTopicAdmin();ui.ChooseAdminTab(tab);Canvas.ForceUpdateCanvases();}
        static void Capture(string name){ScreenCapture.CaptureScreenshot("output/AdminLabUsability/"+name+".png");}
        static IEnumerator Flow()
        {
            Status="Navigation and mobile layout";Open();yield return .3f;
            Check(ui.AdminPanelOpen,"One Admin launcher opens the themed lab");
            Check(ui.GetComponentsInChildren<Button>(true).Count(b=>b.name.StartsWith("Admin_Tab_"))==6,"Six named sections replace the mixed control list");
            Check(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name=="Test_Tools_Content"||t.name=="Return_To_Spawn").All(t=>!t.gameObject.activeInHierarchy),"Legacy tools and floating keyboard respawn prompt stay hidden");
            var scroll=ui.GetComponentsInChildren<ScrollRect>(true).First(s=>s.name=="Admin_Scroll");
            Check(scroll.verticalScrollbar!=null&&scroll.viewport.GetComponent<RectMask2D>()!=null,"Each section clips correctly and has a visible drag scrollbar");
            var panel=ui.GetComponentsInChildren<RectTransform>(true).First(r=>r.name=="Admin_Lab_Panel");var corners=new Vector3[4];panel.GetWorldCorners(corners);
            Check(corners.All(p=>p.x>=Screen.safeArea.xMin&&p.x<=Screen.safeArea.xMax&&p.y>=Screen.safeArea.yMin&&p.y<=Screen.safeArea.yMax),"Admin panel stays within the landscape safe area");Capture("Quick-Test");yield return .2f;
            Status="Persistent stop and time previews";Click("Admin_Stop_Monsters");yield return .3f;
            Check(session.AdminMonstersStopped&&session.hunting.State==KhezuHuntState.Sleeping,"Stop Monsters places the active guardian asleep and sets the existing shared test checkpoint");
            var anchor=session.hunting.transform.position;session.hunting.Request(KhezuCommand.StartHunting);session.hunting.AwakenForNight();yield return .7f;
            Check(session.hunting.State==KhezuHuntState.Sleeping&&Vector3.Distance(anchor,session.hunting.transform.position)<.03f,"Stop persists against manual hunting and automatic night awakening");
            var checkpoint=JsonUtility.FromJson<ArchiveWorldTimeState>(JsonUtility.ToJson(session.State.worldTime));Check(checkpoint.adminMonstersStopped,"Monster stop survives the existing world-state checkpoint serialization");
            Open(2);int completed=session.State.stations.Count(s=>s.phase==2);int mistakes=session.State.mistakes.Count;
            foreach(var pair in new[]{new{button="Admin_Time_Day",phase=0},new{button="Admin_Time_Afternoon",phase=1},new{button="Admin_Time_Evening",phase=2},new{button="Admin_Time_Night",phase=3}})
            {Click(pair.button);yield return .15f;Check(session.State.worldTime.phase==pair.phase&&session.State.worldTime.adminPhaseOverride==pair.phase,"Lighting preview selects phase "+pair.phase);}
            Check(!session.NightEntranceActive&&session.State.worldTime.arrivalStartedAt<0&&session.State.mistakes.Count==mistakes&&session.State.stations.Count(s=>s.phase==2)==completed,"Changing lighting adds no mistakes, topic progress or surprise night entrance");Capture("Time-And-World");yield return .15f;
            double deadline=session.State.matchTime.deadline;Click("Admin_Time_Add");Check(Math.Abs(session.State.matchTime.deadline-deadline-300)<.001,"Add 5 minutes changes the original match deadline exactly once");Click("Admin_Time_Subtract");Check(Math.Abs(session.State.matchTime.deadline-deadline)<.001,"Remove 5 minutes restores the same original deadline");
            Click("Admin_Time_Auto");Check(session.State.worldTime.adminPhaseOverride==-1&&session.State.worldTime.phase==ArchiveTopicSession.WorldPhaseForTopics(completed),"Automatic time returns to topic-driven progression");
            Status="Items and selected practice player";Open(5);Click("Admin_Give_All_Items");yield return .2f;
            Check(Enum.GetValues(typeof(ArchiveSurvivalItem)).Cast<ArchiveSurvivalItem>().All(item=>session.CountItem(session.LocalActor,item)>0),"Give All uses the selected player's actual six-item inventory");Capture("Items-And-Chests");yield return .15f;
            Open(4);Click("Admin_Damage_Heart");Check(session.hunting.health.ActorHearts(session.LocalActor)==2,"Damage button deducts exactly one heart");
            Check(session.RequestUseItem(ArchiveSurvivalItem.Medkit)&&session.hunting.health.ActorHearts(session.LocalActor)==3,"Granted medkit works through the real use command");
            Click("Admin_Spawn_Teammate");yield return .25f;Check(ui.TestActor==-2&&Player.LocalInstance!=session.ActorPlayer(-2),"Spawn selects a distinct practice teammate without stealing local player ownership");Capture("Players");yield return .15f;
            Click("Admin_Down_At_Nest");yield return .4f;Check(session.Downed(-2)?.phase==2&&session.ActorPlayer(-2).IsMovementLocked&&!Player.LocalInstance.IsMovementLocked,"Down At Nest affects only the selected teammate; the tester remains free");
            Open(4);Click("Admin_Revive_Player");yield return .25f;Check(session.hunting.health.ActorHearts(-2)==1&&!session.ActorPlayer(-2).IsMovementLocked,"Admin rescue releases the selected downed body and restores one heart");Click("Admin_Remove_Teammate");yield return .2f;Check(ui.TestActor==session.LocalActor,"Removing the selected practice actor safely falls back to yourself");
            Status="Topic selection and active-topic lock";Open(3);Click("Admin_Select_T8");Click("Admin_Start_Topic");yield return .3f;Check(session.ActiveTopic==7,"Go & Start Topic uses the selected topic's real Book flow");
            Open(3);Click("Admin_Select_T2");Check(!Button("Admin_Start_Topic").interactable,"Another topic's start button is disabled while T8 is unfinished");
            Click("Admin_Complete_Active_Topic");yield return .2f;Check(session.State.stations[7].phase==2&&session.State.mistakes.Count==mistakes,"Complete Active Topic clears the actual running topic without mistakes");
            Click("Admin_Select_T8");int revision=session.State.stations[7].revision;Click("Admin_Reset_Selected");Check(session.State.stations[7].phase==2,"First reset tap requests confirmation without deleting progress");Click("Admin_Reset_Selected");Check(session.State.stations[7].phase==0&&session.State.stations[7].revision>revision,"Second reset tap clears the chosen topic and invalidates stale work");Capture("Topics");yield return .15f;
            Status="Two guardians and stopping during night";Open(2);Click("Admin_Night_Entrance");double timeout=EditorApplication.timeSinceStartup+35;
            while(!session.State.worldTime.secondaryReleased||session.NightEntranceActive){if(EditorApplication.timeSinceStartup>timeout)throw new Exception("Night entrance timed out");yield return .2f;}yield return .5f;
            Open(1);Click("Admin_Stop_Monsters");yield return .4f;
            var monsters=KhezuHuntingController.ActiveMonsters;Check(monsters.Length==2&&monsters.All(m=>m.State==KhezuHuntState.Sleeping),"Stop Monsters handles both primary and secondary guardians at night");
            var positions=monsters.Select(m=>m.transform.position).ToArray();yield return 1.5f;Check(monsters.Select((m,i)=>Vector3.Distance(m.transform.position,positions[i])<.05f&&m.State==KhezuHuntState.Sleeping).All(v=>v),"Night progression does not restart or move stopped monsters");
            Click("Admin_Resume_Monsters");yield return .2f;Check(!session.AdminMonstersStopped&&monsters.All(m=>m.State!=KhezuHuntState.Sleeping),"Resume wakes both active guardians using their existing encounter controls");Click("Admin_Stop_Monsters");Capture("Monsters");yield return .2f;
            Status="Capture release and navigation";Click("Admin_Preview_Barrier");yield return .5f;Check(session.hunting.State==KhezuHuntState.Caught&&Player.LocalInstance.IsMovementLocked,"Enter Barrier uses the actual captured-player challenge and movement lock");
            Open();Click("Admin_Stop_Monsters");yield return .3f;Check(!Player.LocalInstance.IsMovementLocked&&session.hunting.State==KhezuHuntState.Sleeping,"Stop safely releases an active capture without leaving a movement lock");Click("Admin_Return_To_Spawn");yield return .3f;
            var exploration=UnityEngine.Object.FindFirstObjectByType<LogicLegends.OpenArchiveTesting.OpenArchiveExplorationSession>();Check(Vector2.Distance(new Vector2(Player.LocalInstance.transform.position.x,Player.LocalInstance.transform.position.z),new Vector2(exploration.SpawnPoint.position.x,exploration.SpawnPoint.position.z))<1,"Return To Spawn uses the existing player spawn and camera reset");
            Status="Portal and confirmed full reset";Open(3);Click("Admin_Open_Portal");yield return .3f;Check(session.State.stations.All(t=>t.phase==2),"Open Portal completes all ten topics for the existing escape flow");
            Open(3);Click("Admin_Reset_Match");Check(session.State.stations.All(t=>t.phase==2),"Full match reset also requires a second deliberate tap");Click("Admin_Reset_Match");yield return .4f;Check(session.State.stations.All(t=>t.phase==0)&&!session.AdminMonstersStopped&&session.hunting.health.ActorHearts(session.LocalActor)==3,"Full reset clears fixture progress, monster stop override and damage");
            if(ui.AdminPanelOpen)ui.ToggleTopicAdmin();Check(Time.timeScale==1,"Admin never pauses global game time");
        }
    }
}
