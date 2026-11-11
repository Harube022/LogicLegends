using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Monsters;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicTestControls
    {
        RectTransform adminPanel,adminContent,adminSafe;
        TMP_Text adminMessage,adminInfo;
        Button adminToggle;
        ScrollRect adminScroll;
        Canvas adminCanvas;
        Transform oldTools,oldSpawnButton;
        readonly List<Button> adminTabs=new List<Button>();
        readonly List<Availability> availability=new List<Availability>();
        readonly List<Graphic> adminFixedLabels=new List<Graphic>();
        readonly List<TMP_Text> sectionLabels=new List<TMP_Text>();
        int tab,selectedActor,chosenItem,monsterIndex,beaconIndex;
        string confirm="";
        Vector2 adminScreen;Rect adminScreenSafe;
        static readonly Color AdminInk=new Color(.025f,.075f,.09f,1),AdminCream=new Color(1,.95f,.82f),AdminGold=new Color(.88f,.69f,.35f),AdminMuted=new Color(.65f,.81f,.80f);
        public bool AdminPanelOpen=>adminPanel!=null&&adminPanel.gameObject.activeInHierarchy;
        public int TestActor=>session.AdminTestActors.Contains(selectedActor)?selectedActor:session.LocalActor;
        sealed class Availability{public Button button;public Func<bool> can;}
        sealed class LabAction{public string id,title,help;public Action run;public Func<bool> can;}
        KhezuHuntingController SelectedMonster=>monsterIndex==0?session.hunting:session.worldProgression?.secondary;
        bool MonsterReady=>SelectedMonster!=null&&SelectedMonster.Initialized&&SelectedMonster.gameObject.activeInHierarchy;
        bool CanMoveTester=>Player.LocalInstance!=null&&!Player.LocalInstance.IsMovementLocked&&!session.NightEntranceActive;
        bool HasActiveTopic=>session.ActiveTopic>=0&&session.ActiveTopic<10;
        DevelopmentMonsterSelection Selection=>session.worldProgression!=null?session.worldProgression.selector:FindFirstObjectByType<DevelopmentMonsterSelection>();

        void BuildTopicAdmin(RectTransform dock,Transform body)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var header=dock.Find("Show_Hide_Test_Tools") as RectTransform;if(header==null)return;
            oldTools=body;oldTools.gameObject.SetActive(false);
            foreach(Transform child in header)child.gameObject.SetActive(false);
            adminToggle=AdminButton("Topic_Admin_Toggle",header,"ADMIN TOOLS",new Vector2(.015f,.03f),new Vector2(.985f,.97f),ToggleTopicAdmin);
            var oldGraphic=header.GetComponent<Graphic>();if(oldGraphic!=null)oldGraphic.enabled=false;
            var oldButton=header.GetComponent<Button>();if(oldButton!=null)oldButton.enabled=false;
            var launcher=adminToggle.GetComponent<RectTransform>();launcher.anchorMin=launcher.anchorMax=new Vector2(0,.5f);launcher.pivot=new Vector2(0,.5f);launcher.sizeDelta=new Vector2(220,56);launcher.anchoredPosition=Vector2.zero;
            var old=FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(r=>r.name=="Return_To_Spawn");
            if(old!=null){oldSpawnButton=old;old.gameObject.SetActive(false);}
            var go=new GameObject("Archive_Admin_Laboratory",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            adminCanvas=go.GetComponent<Canvas>();adminCanvas.renderMode=RenderMode.ScreenSpaceOverlay;adminCanvas.sortingOrder=20000;
            var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
            var scrim=AdminRect("Admin_Backdrop",go.transform,Vector2.zero,Vector2.one);scrim.gameObject.AddComponent<Image>().color=new Color(0,.025f,.035f,.78f);
            adminSafe=AdminRect("Admin_Safe_Area",scrim,Vector2.zero,Vector2.one);
            adminPanel=AdminRect("Admin_Lab_Panel",adminSafe,new Vector2(.04f,.055f),new Vector2(.96f,.95f));
            var frame=adminPanel.gameObject.AddComponent<Image>();frame.sprite=StageUITheme.Panel;frame.type=Image.Type.Sliced;frame.color=Color.white;
            var inner=AdminRect("Dark_Interior",adminPanel,new Vector2(.018f,.025f),new Vector2(.982f,.975f));inner.gameObject.AddComponent<Image>().color=AdminInk;
            var title=AdminChromeLabel("Title",adminPanel,"ADMIN LAB",35,new Vector2(.04f,.91f),new Vector2(.32f,.97f));title.fontStyle=FontStyle.Bold;title.alignment=TextAnchor.MiddleLeft;
            var sub=AdminChromeLabel("Subtitle",adminPanel,"Choose a section, select a target, then test.",21,new Vector2(.32f,.91f),new Vector2(.83f,.965f));sub.color=AdminMuted;
            AdminButton("Close_Lab",adminPanel,"CLOSE",new Vector2(.85f,.91f),new Vector2(.965f,.965f),ToggleTopicAdmin);
            var rail=AdminRect("Navigation_Background",adminPanel,new Vector2(.03f,.14f),new Vector2(.19f,.89f));rail.gameObject.AddComponent<Image>().color=new Color(.045f,.12f,.14f);
            string[] names={"QUICK TEST","MONSTERS","TIME & WORLD","TOPICS","PLAYERS","ITEMS & CHESTS"};
            for(int i=0;i<names.Length;i++){int index=i;var button=AdminButton("Admin_Tab_"+i,rail,names[i],new Vector2(.025f,.87f-i*.132f),new Vector2(.975f,.985f-i*.132f),()=>ChooseAdminTab(index));adminTabs.Add(button);}
            var badge=AdminChromeLabel("Test_Only",adminPanel,"ADMIN TESTING\nActions disable rewards",18,new Vector2(.04f,.035f),new Vector2(.185f,.125f));badge.color=AdminGold;
            adminInfo=AdminLabel("Target_Status",adminPanel,"",24,new Vector2(.22f,.82f),new Vector2(.96f,.90f));adminInfo.alignment=TextAlignmentOptions.Left;
            var scroll=AdminRect("Admin_Scroll",adminPanel,new Vector2(.215f,.155f),new Vector2(.965f,.815f));adminScroll=scroll.gameObject.AddComponent<ScrollRect>();adminScroll.horizontal=false;adminScroll.movementType=ScrollRect.MovementType.Clamped;adminScroll.scrollSensitivity=45;
            var viewport=AdminRect("Viewport",scroll,Vector2.zero,new Vector2(.975f,1));viewport.gameObject.AddComponent<Image>().color=AdminInk;viewport.gameObject.AddComponent<RectMask2D>();
            adminContent=AdminRect("Content",viewport,new Vector2(0,1),Vector2.one);adminContent.pivot=new Vector2(.5f,1);
            var layout=adminContent.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=10;layout.padding=new RectOffset(2,12,2,12);layout.childControlWidth=layout.childControlHeight=layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            adminContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;adminScroll.viewport=viewport;adminScroll.content=adminContent;
            var track=AdminRect("Visible_Scrollbar",scroll,new Vector2(.985f,0),Vector2.one);track.gameObject.AddComponent<Image>().color=new Color(.12f,.22f,.23f);
            var slide=AdminRect("Sliding_Area",track,Vector2.zero,Vector2.one);var handle=AdminRect("Handle",slide,Vector2.zero,Vector2.one);handle.gameObject.AddComponent<Image>().color=AdminGold;
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();adminScroll.verticalScrollbar=bar;adminScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
            adminMessage=AdminLabel("Action_Feedback",adminPanel,"",21,new Vector2(.22f,.035f),new Vector2(.96f,.135f));adminMessage.alignment=TextAlignmentOptions.Left;
            PositionAdmin();scrim.gameObject.SetActive(false);PopulateAdmin();
#endif
        }
        public void ToggleTopicAdmin()
        {
            if(adminPanel==null||session==null||!session.CanUseTopicAdmin)return;
            bool show=!AdminPanelOpen;adminPanel.parent.parent.gameObject.SetActive(show);
            if(show){ArchiveTopicUI.Current?.Close();if(GameInput.Instance!=null)GameInput.Instance.SetMobileMovement(Vector2.zero);MobileInputUI.FindJoystick()?.ResetJoystick();MobileLookInput.ResetDelta();confirm="";PopulateAdmin();}
        }
        public void ChooseAdminTab(int index){if(index<0||index>5)return;tab=index;confirm="";PopulateAdmin();}
        void PositionAdmin()
        {
            adminScreen=new Vector2(Screen.width,Screen.height);adminScreenSafe=Screen.safeArea;
            adminSafe.anchorMin=new Vector2(adminScreenSafe.xMin/Mathf.Max(1,Screen.width),adminScreenSafe.yMin/Mathf.Max(1,Screen.height));
            adminSafe.anchorMax=new Vector2(adminScreenSafe.xMax/Mathf.Max(1,Screen.width),adminScreenSafe.yMax/Mathf.Max(1,Screen.height));
        }
        void RefreshTopicAdmin()
        {
            if(adminToggle==null)return;
            if(oldTools!=null)oldTools.gameObject.SetActive(false);if(oldSpawnButton!=null)oldSpawnButton.gameObject.SetActive(false);
            bool allowed=session!=null&&session.CanUseTopicAdmin;adminToggle.gameObject.SetActive(allowed);
            if(!allowed){adminPanel.parent.parent.gameObject.SetActive(false);return;}
            if(!AdminPanelOpen)return;
            if(adminScreen!=new Vector2(Screen.width,Screen.height)||adminScreenSafe!=Screen.safeArea)PositionAdmin();
            selectedTopic=Mathf.Clamp(selectedTopic,0,9);
            string monster=SelectedMonster==null?"Not available":LogicLegends.OpenArchive.ArchiveMonsterNames.ForPrimalAxiom(SelectedMonster.rajangCounterProfile);
            if(monsterIndex==1&&Selection!=null)monster=LogicLegends.OpenArchive.ArchiveMonsterNames.ForPrimalAxiom(Selection.Selected!=DevelopmentMonster.Rajang);
            string sky=session.State.worldTime==null?"Daylight":new[]{"Daylight","Afternoon","Evening","Night"}[Mathf.Clamp(session.State.worldTime.phase,0,3)];
            string info=tab==0?(session.AdminMonstersStopped?"MONSTERS STOPPED":"MONSTERS: NORMAL")+"   |   "+sky+"   |   Active topic: "+(HasActiveTopic?"T"+(session.ActiveTopic+1):"None"):
                tab==1?monster+"  ·  "+(!MonsterReady?"Not spawned yet":SelectedMonster.State.ToString())+(session.AdminMonstersStopped?"  ·  STOPPED":""):
                tab==2?"Current time: "+sky+"  ·  "+(session.State.worldTime.adminPhaseOverride<0?"Automatic progression":"Manual lighting preview"):
                tab==3?"T"+(selectedTopic+1)+"  ·  "+ArchiveTopicCatalog.Names[selectedTopic]+"  |  "+(session.State.stations[selectedTopic].phase==2?"Complete":session.State.stations[selectedTopic].round+" / "+ArchiveTopicCatalog.RoundCount(selectedTopic)+" challenges solved"):
                "Selected player: "+session.ActorName(TestActor)+"  ·  "+session.hunting.health.ActorHearts(TestActor)+" / "+session.hunting.health.MaximumHealth+" hearts";
            if(adminInfo.text!=info)adminInfo.text=info;
            string message=string.IsNullOrEmpty(confirm)?session.AdminTestNotice:confirm;if(adminMessage.text!=message)adminMessage.text=message;
            adminMessage.color=string.IsNullOrEmpty(confirm)?AdminMuted:AdminGold;
            foreach(var a in availability)if(a.button!=null)a.button.interactable=a.can==null||a.can();
            // Keep the small amount of fixed chrome fresh while the test panel is
            // visible; section rebuilds and clipping must not leave stale UI meshes.
            foreach(var text in adminFixedLabels)if(text!=null)text.SetVerticesDirty();
            foreach(var text in sectionLabels)if(text!=null)text.SetVerticesDirty();
        }
        void PopulateAdmin()
        {
            if(adminContent==null)return;confirm="";availability.Clear();sectionLabels.Clear();foreach(Transform child in adminContent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            for(int i=0;i<adminTabs.Count;i++)adminTabs[i].GetComponent<Image>().color=i==tab?new Color(1,.85f,.5f):new Color(.70f,.85f,.85f);
            switch(tab){case 0:QuickPage();break;case 1:MonsterPage();break;case 2:WorldPage();break;case 3:TopicPage();break;case 4:PlayerPage();break;case 5:ItemsPage();break;}
            Canvas.ForceUpdateCanvases();adminScroll.verticalNormalizedPosition=1;RefreshTopicAdmin();
        }
        LabAction A(string id,string title,string help,Action run,Func<bool> can=null)=>new LabAction{id=id,title=title,help=help,run=run,can=can};
        LabAction Cmd(string id,string title,string help,ArchiveAdminTestAction action,int target=0,int value=0,bool close=false,Func<bool> can=null)=>A(id,title,help,()=>RunAdmin(action,target,value,close),can);
        void RunAdmin(ArchiveAdminTestAction action,int target=0,int value=0,bool close=false)
        {
            confirm="";bool ok=session.RequestAdminTest(action,target,value);
            if(ok&&close&&AdminPanelOpen)ToggleTopicAdmin();
        }
        LabAction Stop()=>Cmd("Admin_Stop_Monsters","STOP MONSTERS","Stops both guardians until you resume them.",ArchiveAdminTestAction.StopMonsters,can:()=>!session.AdminMonstersStopped);
        LabAction Resume()=>Cmd("Admin_Resume_Monsters","RESUME MONSTERS","Wake active guardians and start hunting.",ArchiveAdminTestAction.ResumeMonsters,can:()=>session.AdminMonstersStopped);
        void QuickPage()
        {
            Section("START HERE","Frequently used controls. The world and match clock keep running.");
            Row(Stop(),Resume());
            Row(Cmd("Admin_Return_To_Spawn","RETURN TO SPAWN","Move yourself to the starting plaza.",ArchiveAdminTestAction.ReturnToSpawn,close:true,can:()=>CanMoveTester),
                A("Admin_Quick_Supplies","GIVE ME ALL ITEMS","Flashlight, battery, medkit, revive kit, gun and darts.",()=>RunAdmin(ArchiveAdminTestAction.GrantAllItems,session.LocalActor)));
            Row(A("Admin_Complete_Active_Topic","COMPLETE ACTIVE TOPIC","Clear the running topic without adding mistakes.",()=>RunAdmin(ArchiveAdminTestAction.CompleteTopic,session.ActiveTopic),()=>HasActiveTopic),
                A("Admin_Reset_Active_Topic","RESET ACTIVE TOPIC","Clear its progress and release the topic lock.",()=>ConfirmAdmin("Reset the active topic? Tap again to confirm.",()=>RunAdmin(ArchiveAdminTestAction.ResetTopic,session.ActiveTopic)),()=>HasActiveTopic));
            Row(A("Admin_Quick_Time","CHANGE TIME","Daylight, afternoon, evening, night or automatic.",()=>ChooseAdminTab(2)),
                A("Admin_Quick_Teammate","TEST A TEAMMATE","Spawn a practice player; test downing and revival.",()=>ChooseAdminTab(4)));
        }
        void MonsterPage()
        {
            Section("CHOOSE A MONSTER","Primary is the match's starting guardian. The second guardian appears at night.");
            Row(64,A("Admin_Select_Primary","PRIMARY GUARDIAN","",()=>{monsterIndex=0;PopulateAdmin();}),A("Admin_Select_Secondary","NIGHT GUARDIAN","",()=>{monsterIndex=1;PopulateAdmin();}));
            Row(Stop(),Resume());
            Row(A("Admin_Wake_One","AWAKEN ONE STAGE","Preview the next wake animation and alert icon.",()=>RunAdmin(ArchiveAdminTestAction.AddMonsterMistake,monsterIndex),()=>MonsterReady&&!session.AdminMonstersStopped),
                A("Admin_Start_Hunt","START HUNTING","Fully awaken the selected monster, then chase.",()=>RunAdmin(ArchiveAdminTestAction.StartHunt,monsterIndex,0,true),()=>MonsterReady));
            Row(A("Admin_Sleep_One","PUT SELECTED TO SLEEP","Sleep once. Use Stop Monsters to keep both stopped.",()=>RunAdmin(ArchiveAdminTestAction.SleepMonster,monsterIndex),()=>MonsterReady),
                A("Admin_Swap_Monster","SWAP STARTING MONSTER","Offline only. Stop the encounter before swapping.",()=>RunAdmin(ArchiveAdminTestAction.SwapMonster),()=>Selection!=null&&Selection.CanSwap));
            Row(A("Admin_Reset_Hunt","RESET SELECTED HUNT","Stop its current pursuit and release its capture.",()=>RunAdmin(ArchiveAdminTestAction.ReleaseCapture,monsterIndex),()=>MonsterReady),
                A("Admin_Reset_Rotation","RESET TARGET ROTATION","Clear the selected monster's no-repeat player list.",()=>RunAdmin(ArchiveAdminTestAction.ResetMonsterRotation,monsterIndex),()=>MonsterReady));
            Section("CAPTURE & ESCAPE","Uses the actual POV, barrier questions, heart penalty and recovery.");
            Row(A("Admin_Preview_POV","PREVIEW JUMPSCARE","Offline preview of the selected guardian's POV.",()=>RunAdmin(ArchiveAdminTestAction.PreviewJumpscare,monsterIndex,0,true),()=>MonsterReady&&!Photon.Pun.PhotonNetwork.InRoom),
                A("Admin_Preview_Barrier","ENTER BARRIER","Capture yourself without waiting for a chase.",()=>RunAdmin(ArchiveAdminTestAction.PreviewBarrier,monsterIndex,0,true),()=>MonsterReady&&!session.NightEntranceActive&&session.CanAct(session.LocalActor)));
            Row(A("Admin_Barrier_Success","PASS BARRIER","Test success, release, return to nest and sleep.",()=>RunAdmin(ArchiveAdminTestAction.BarrierSuccess,monsterIndex),()=>MonsterReady&&SelectedMonster.State==KhezuHuntState.Caught),
                A("Admin_Barrier_Fail","FAIL BARRIER","Test attack, one lost heart, knockback and stun.",()=>RunAdmin(ArchiveAdminTestAction.BarrierFail,monsterIndex,0,true),()=>MonsterReady&&SelectedMonster.State==KhezuHuntState.Caught));
            Row(A("Admin_Release_Capture","RELEASE CAPTURE","Cancel the selected encounter without a penalty.",()=>RunAdmin(ArchiveAdminTestAction.ReleaseCapture,monsterIndex),()=>MonsterReady),
                Cmd("Admin_Visit_Nest","GO TO MONSTER NEST","Move yourself beside the central nest.",ArchiveAdminTestAction.TeleportToNest,close:true,can:()=>CanMoveTester));
        }
        void WorldPage()
        {
            Section("CHANGE TIME OF DAY","Lighting previews only. They do not play the second-monster entrance.");
            Row(Cmd("Admin_Time_Day","DAYLIGHT","Bright exploration lighting.",ArchiveAdminTestAction.SetWorldPhase,0),Cmd("Admin_Time_Afternoon","AFTERNOON","Warm afternoon lighting.",ArchiveAdminTestAction.SetWorldPhase,1));
            Row(Cmd("Admin_Time_Evening","EVENING","Dim evening lighting.",ArchiveAdminTestAction.SetWorldPhase,2),Cmd("Admin_Time_Night","NIGHT","Darkness, fog, firelight and night music.",ArchiveAdminTestAction.SetWorldPhase,3));
            Row(Cmd("Admin_Time_Auto","AUTOMATIC TIME","Follow completed topics: 3 afternoon, 5 evening, 7 night.",ArchiveAdminTestAction.SetWorldPhase,-1),Cmd("Admin_Night_Entrance","PLAY NIGHT ENTRANCE","Re-enable guardians; test reveal, warning and hunt.",ArchiveAdminTestAction.PreviewNight,close:true));
            Section("SHARED MATCH CLOCK","Changes the existing match timer; topic and barrier clocks stay separate.");
            Row(Cmd("Admin_Start_Clock","START MATCH CLOCK","Skip the waiting/countdown stage for this test.",ArchiveAdminTestAction.StartMatchClock,can:()=>!session.MatchTimeAllowsGameplay&&!session.TeamWon&&!session.TeamDefeated));
            Row(Cmd("Admin_Time_Add","ADD 5 MINUTES","Extend the current match or escape deadline.",ArchiveAdminTestAction.AddMatchMinutes,value:5,can:()=>session.MatchTimeAllowsGameplay),Cmd("Admin_Time_Subtract","REMOVE 5 MINUTES","Shorten the current match or escape deadline.",ArchiveAdminTestAction.AddMatchMinutes,value:-5,can:()=>session.MatchTimeAllowsGameplay));
            Row(Cmd("Admin_Time_One_Minute","SET TO 1 MINUTE","Test the final-minute warning.",ArchiveAdminTestAction.MatchClockOneMinute,can:()=>session.MatchTimeAllowsGameplay),A("Admin_Match_Timeout","TRIGGER TIMEOUT","Ends the test match immediately. Tap twice.",()=>ConfirmAdmin("End this test match now? Tap again to confirm.",()=>RunAdmin(ArchiveAdminTestAction.ExpireMatchClock,0,0,true)),()=>session.MatchTimeAllowsGameplay));
            Section("CINEMATICS & WORLD EFFECTS","Preview without changing topic answers.");
            Row(A("Admin_Intro_Film","REPLAY MATCH INTRO","Players first, then the active monster. Offline only.",()=>PreviewMatchFilm(false),()=>!Photon.Pun.PhotonNetwork.InRoom),A("Admin_Victory_Film","PREVIEW VICTORY FILM","Plays the ending cinematic. Offline only.",()=>PreviewMatchFilm(true),()=>!Photon.Pun.PhotonNetwork.InRoom));
            Row(Cmd("Admin_Field_Mist","LUMINOUS MIST","Preview the nearest field disturbance.",ArchiveAdminTestAction.PreviewFieldEvent,0),Cmd("Admin_Field_Shards","FLOATING SHARDS","Preview the shard warning and effect.",ArchiveAdminTestAction.PreviewFieldEvent,1));
            Row(Cmd("Admin_Field_Runes","RUNE SURGE","Preview a rune-field disturbance.",ArchiveAdminTestAction.PreviewFieldEvent,2),Cmd("Admin_Clear_Field","CLEAR FIELD EFFECTS","Remove the current disturbance and beacon diversion.",ArchiveAdminTestAction.ClearFieldEffects));
            Row(A("Admin_Next_Beacon","SELECT BEACON "+(beaconIndex+1)+" / 4","Tap to choose the next beacon.",()=>{beaconIndex=(beaconIndex+1)%4;PopulateAdmin();}),A("Admin_Visit_Beacon","GO TO BEACON","Move beside the selected beacon.",()=>RunAdmin(ArchiveAdminTestAction.VisitBeacon,beaconIndex,0,true),()=>CanMoveTester));
            Row(Cmd("Admin_Recharge_Beacons","RECHARGE BEACONS","Clear beacon cooldowns for another test.",ArchiveAdminTestAction.RechargeBeacons),Cmd("Admin_Visit_Field","GO TO RUNE FIELD","Move beside the nearest disturbance site.",ArchiveAdminTestAction.VisitFieldSite,close:true,can:()=>CanMoveTester));
        }
        void TopicPage()
        {
            Section("CHOOSE A TOPIC","Select T1–T10 directly. The full topic name appears above.");
            for(int row=0;row<2;row++){var choices=new List<LabAction>();for(int i=row*5;i<row*5+5;i++){int topic=i;choices.Add(A("Admin_Select_T"+(i+1),"T"+(i+1)+(selectedTopic==i?"  SELECTED":""),"",()=>{selectedTopic=topic;PopulateAdmin();}));}Row(58,choices.ToArray());}
            Row(A("Admin_Visit_Topic","GO TO SELECTED TOPIC","Move beside its Book without starting it.",()=>RunAdmin(ArchiveAdminTestAction.VisitTopic,selectedTopic,0,true),()=>CanMoveTester),
                A("Admin_Start_Topic","GO & START TOPIC","Uses its actual Book flow. Finish or reset the active topic first.",()=>RunAdmin(ArchiveAdminTestAction.StartTopic,selectedTopic,0,true),()=>CanMoveTester&&session.CanStartTopic(selectedTopic)));
            Row(A("Admin_Complete_Selected","COMPLETE SELECTED TOPIC","Clear every challenge in this topic for testing.",()=>RunAdmin(ArchiveAdminTestAction.CompleteTopic,selectedTopic)),A("Admin_Reset_Selected","RESET SELECTED TOPIC","Clear its work, chest and solved challenges. Tap twice.",()=>ConfirmAdmin("Reset T"+(selectedTopic+1)+"? Tap again to confirm.",()=>RunAdmin(ArchiveAdminTestAction.ResetTopic,selectedTopic))));
            Row(A("Admin_Complete_Active_Topic","COMPLETE ACTIVE TOPIC","Clear the topic that is currently running.",()=>RunAdmin(ArchiveAdminTestAction.CompleteTopic,session.ActiveTopic),()=>HasActiveTopic),A("Admin_Reset_Active_Topic","RESET ACTIVE TOPIC","Release the current topic lock for another test.",()=>ConfirmAdmin("Reset the active topic? Tap again to confirm.",()=>RunAdmin(ArchiveAdminTestAction.ResetTopic,session.ActiveTopic)),()=>HasActiveTopic));
            Section("FINISH OR RESTART A TEST","Admin skips do not award earned academic credit or permanent currency.");
            Row(Cmd("Admin_Open_Portal","COMPLETE ALL & OPEN PORTAL","Clear T1–T10, then test the real escape and victory flow.",ArchiveAdminTestAction.OpenPortal,close:true),A("Admin_Reset_Match","RESET ENTIRE MATCH","Restart all practice progress and health. Tap twice.",()=>ConfirmAdmin("Reset the entire match? Tap again to confirm.",()=>RunAdmin(ArchiveAdminTestAction.ResetMatch,0,0,true))));
        }
        void PlayerPicker()
        {
            var choices=session.AdminTestActors.Select(actor=>A("Admin_Select_Player_"+actor,(actor==session.LocalActor?"YOU":session.ActorName(actor))+(actor==TestActor?"  SELECTED":""),"",()=>{selectedActor=actor;PopulateAdmin();})).ToArray();Row(64,choices);
        }
        void PlayerPage()
        {
            Section("CHOOSE A PLAYER","Health and revival controls affect only this selected player.");PlayerPicker();
            Row(A("Admin_Spawn_Teammate","SPAWN PRACTICE TEAMMATE","Offline body for downed, carry, revive and sharing tests.",()=>{if(session.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate)){selectedActor=-2;PopulateAdmin();}},()=>!Photon.Pun.PhotonNetwork.InRoom&&session.ActorPlayer(-2)==null),
                Cmd("Admin_Recall_Teammate","BRING TEAMMATE HERE","Move the living practice teammate beside you.",ArchiveAdminTestAction.RecallTeammate,can:()=>session.ActorPlayer(-2)!=null&&!session.ActorPlayer(-2).IsMovementLocked));
            Row(A("Admin_Damage_Heart","LOSE 1 HEART","Test the selected player's damage and heart display.",()=>RunAdmin(ArchiveAdminTestAction.DamageHeart,TestActor),()=>session.hunting.health.IsActorAlive(TestActor)),
                A("Admin_Restore_Hearts","RESTORE FULL HEARTS","Restore the selected player and release downed state.",()=>RunAdmin(ArchiveAdminTestAction.SetHearts,TestActor,session.hunting.health.MaximumHealth)));
            Row(A("Admin_Zero_Hearts","DOWN PLAYER & TEST CARRY","Lose all hearts; watch the monster carry and spectator view.",()=>RunAdmin(ArchiveAdminTestAction.SetHearts,TestActor,0,true)),
                A("Admin_Down_At_Nest","DOWN PLAYER AT NEST","Skip travel; test the lying body and revive interaction.",()=>RunAdmin(ArchiveAdminTestAction.DownAtNest,TestActor,0,true)));
            Row(A("Admin_Revive_Player","REVIVE SELECTED PLAYER","Admin rescue: restore one heart and release the body.",()=>RunAdmin(ArchiveAdminTestAction.ReviveAtNest,TestActor),()=>session.Downed(TestActor)!=null),
                A("Admin_Rescue_Test","GO TO NEST WITH REVIVE KIT","Give yourself a kit, then test its normal Use button.",()=>{RunAdmin(ArchiveAdminTestAction.GrantItem,session.LocalActor,(int)ArchiveSurvivalItem.RevivalKit);RunAdmin(ArchiveAdminTestAction.TeleportToNest,0,0,true);},()=>CanMoveTester));
            Row(Cmd("Admin_Remove_Teammate","REMOVE PRACTICE TEAMMATE","Remove only the offline test body.",ArchiveAdminTestAction.RemoveTeammate,can:()=>session.ActorPlayer(-2)!=null),Cmd("Admin_Player_Spawn","RETURN YOURSELF TO SPAWN","Move to the starting plaza without resetting topics.",ArchiveAdminTestAction.ReturnToSpawn,close:true,can:()=>CanMoveTester));
            Note("RESCUE TEST: Spawn a teammate → Down player & test carry → Go to nest with revive kit → use the real item button. If monsters are stopped, Resume Monsters first to test carrying. A practice body does not replace a second network client.");
        }
        void ItemsPage()
        {
            Section("CHOOSE WHO RECEIVES THE ITEM","These controls use the real inventory. Close Admin to equip, use or share items.");PlayerPicker();
            for(int row=0;row<2;row++){var choices=new List<LabAction>();for(int i=row*3;i<row*3+3;i++){int item=i;choices.Add(A("Admin_Select_Item_"+i,ArchiveRewardView.ItemName((ArchiveSurvivalItem)i)+(chosenItem==i?"  SELECTED":""),"",()=>{chosenItem=item;PopulateAdmin();}));}Row(64,choices.ToArray());}
            Row(A("Admin_Give_Item","GIVE SELECTED ITEM","Add "+ArchiveRewardView.ItemName((ArchiveSurvivalItem)chosenItem)+" to the selected player.",()=>RunAdmin(ArchiveAdminTestAction.GrantItem,TestActor,chosenItem)),
                A("Admin_Give_All_Items","GIVE ALL SIX ITEMS","Add the complete test supply kit.",()=>RunAdmin(ArchiveAdminTestAction.GrantAllItems,TestActor)));
            Row(A("Admin_Spawn_Chest","SPAWN CHEST WITH THIS ITEM","Complete T"+(selectedTopic+1)+" and move to its selected-drop chest.",()=>RunAdmin(ArchiveAdminTestAction.SpawnChest,selectedTopic,chosenItem,true),()=>CanMoveTester),
                A("Admin_Random_Chest","SPAWN RANDOM CHEST","Complete T"+(selectedTopic+1)+" and test a random drop.",()=>RunAdmin(ArchiveAdminTestAction.SpawnChest,selectedTopic,-1,true),()=>CanMoveTester));
            Row(A("Admin_Choose_Chest_Topic","CHOOSE CHEST LOCATION","Currently T"+(selectedTopic+1)+". Select another topic in Topics.",()=>ChooseAdminTab(3)),
                A("Admin_Clear_Items","CLEAR SELECTED INVENTORY","Remove all items from the selected player. Tap twice.",()=>ConfirmAdmin("Clear "+session.ActorName(TestActor)+"'s inventory? Tap again to confirm.",()=>RunAdmin(ArchiveAdminTestAction.ClearInventory,TestActor))));
            Note("ITEM TEST: Give items → Close Admin → tap an inventory slot → tap its Use button. For medkit, lose one heart first. For sharing or revival, spawn the practice teammate in Players.");
        }
        void PreviewMatchFilm(bool victory)
        {
            if(Photon.Pun.PhotonNetwork.InRoom)return;var cinematic=session.GetComponent<ArchiveMatchCinematic>();if(cinematic==null)return;
            if(AdminPanelOpen)ToggleTopicAdmin();if(victory)cinematic.PreviewVictory();else cinematic.PreviewSummoning();
        }
        void ConfirmAdmin(string message,Action apply){if(confirm==message){confirm="";apply();}else confirm=message;}
        void Section(string title,string hint)
        {
            var row=AdminRect("Section_"+title,adminContent,Vector2.zero,Vector2.one);row.gameObject.AddComponent<LayoutElement>().preferredHeight=64;
            var heading=AdminLabel("Heading",row,title,23,new Vector2(.01f,.48f),new Vector2(.99f,1));heading.fontStyle=FontStyles.Bold;heading.color=AdminGold;heading.alignment=TextAlignmentOptions.Left;
            var help=AdminLabel("Description",row,hint,19,new Vector2(.01f,0),new Vector2(.99f,.5f));help.color=AdminMuted;help.alignment=TextAlignmentOptions.Left;
            sectionLabels.Add(heading);sectionLabels.Add(help);
        }
        void Note(string text){var t=AdminLabel("Test_Guide",adminContent,text,20,Vector2.zero,Vector2.one);t.alignment=TextAlignmentOptions.TopLeft;t.color=AdminMuted;t.gameObject.AddComponent<LayoutElement>().preferredHeight=100;}
        void Row(params LabAction[] actions){Row(102,actions);}
        void Row(float height,params LabAction[] actions)
        {
            if(actions.Length==0)return;var row=AdminRect("Control_Row",adminContent,Vector2.zero,Vector2.one);row.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
            float cell=1f/actions.Length;
            for(int i=0;i<actions.Length;i++)
            {
                var a=actions[i];var rect=AdminRect(a.id,row,new Vector2(i*cell+.004f,.015f),new Vector2((i+1)*cell-.004f,.985f));
                var image=rect.gameObject.AddComponent<Image>();image.sprite=buttonArt!=null?buttonArt:StageUITheme.ButtonArt;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4;image.color=Color.white;
                var button=rect.gameObject.AddComponent<Button>();button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(()=>a.run());ButtonColors(button);
                availability.Add(new Availability{button=button,can=a.can});
                var title=AdminLabel("Label",rect,a.title,22,new Vector2(.055f,string.IsNullOrEmpty(a.help)?.12f:.47f),new Vector2(.945f,.89f));title.fontStyle=FontStyles.Bold;
                if(!string.IsNullOrEmpty(a.help)){var help=AdminLabel("Description",rect,a.help,18,new Vector2(.055f,.12f),new Vector2(.945f,.49f));help.color=new Color(.80f,.90f,.87f);}
            }
        }
        static void ButtonColors(Button b){var colors=b.colors;colors.highlightedColor=new Color(1,.94f,.75f);colors.pressedColor=new Color(.65f,.78f,.73f);colors.disabledColor=new Color(.42f,.48f,.47f);b.colors=colors;}
        static RectTransform AdminRect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        Button AdminButton(string name,Transform parent,string caption,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var rect=AdminRect(name,parent,min,max);var image=rect.gameObject.AddComponent<Image>();image.sprite=buttonArt!=null?buttonArt:StageUITheme.ButtonArt;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4;image.color=Color.white;
            var button=rect.gameObject.AddComponent<Button>();button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(action);ButtonColors(button);
            var text=AdminLabel("Label",rect,caption,22,new Vector2(.05f,.10f),new Vector2(.95f,.90f));text.fontStyle=FontStyles.Bold;return button;
        }
        TMP_Text AdminLabel(string name,Transform parent,string text,int size,Vector2 min,Vector2 max)
        {
            var r=AdminRect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=textFont!=null?textFont:StageUITheme.ReadingFont;t.fontSize=size;t.color=AdminCream;t.text=text;
            t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.enableAutoSizing=true;t.fontSizeMin=size-3;t.fontSizeMax=size;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        Text AdminChromeLabel(string name,Transform parent,string text,int size,Vector2 min,Vector2 max)
        {
            // Static chrome is independent of the frequently rebuilt masked TMP lists.
            var label=AdminRect(name,parent,min,max).gameObject.AddComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text=text;label.fontSize=size;label.color=AdminCream;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;label.maskable=false;adminFixedLabels.Add(label);return label;
        }
    }
}
