using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicTestControls
    {
        RectTransform adminPanel,adminContent;
        TMP_Text adminMessage,adminInfo;
        Button adminToggle;
        ScrollRect adminScroll;
        Canvas adminCanvas;
        int tab,actorIndex,chosenItem,monsterIndex;
        string confirm="";
        public bool AdminPanelOpen=>adminPanel!=null&&adminPanel.gameObject.activeSelf;
        static readonly Color AdminInk=new Color(.025f,.075f,.10f,.985f),AdminCream=new Color(1,.94f,.8f);
        public int TestActor { get {var ids=session.AdminTestActors;return ids.Length>0?ids[Mathf.Clamp(actorIndex,0,ids.Length-1)]:session.LocalActor;} }
        void BuildTopicAdmin(RectTransform dock,Transform body)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var header=dock.Find("Show_Hide_Test_Tools") as RectTransform;if(header==null)return;
            var title=header.Find("Title");if(title!=null){var text=title.GetComponent<TMP_Text>();if(text!=null)text.text="TEST TOOLS";var tr=title.GetComponent<RectTransform>();tr.anchorMax=new Vector2(.37f,1);tr.offsetMax=Vector2.zero;}
            adminToggle=AdminButton("Topic_Admin_Toggle",header,"ADMIN LAB",new Vector2(.40f,.06f),new Vector2(.72f,.94f),ToggleTopicAdmin);
            var go=new GameObject("Archive_Admin_Laboratory",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            adminCanvas=go.GetComponent<Canvas>();adminCanvas.renderMode=RenderMode.ScreenSpaceOverlay;adminCanvas.sortingOrder=400;
            var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
            adminPanel=AdminRect("Admin_Lab_Panel",go.transform,new Vector2(.12f,.13f),new Vector2(.88f,.89f));adminPanel.gameObject.AddComponent<Image>().color=AdminInk;
            AdminLabel("Title",adminPanel,"OPEN ARCHIVE / TEST LAB",30,new Vector2(.025f,.90f),new Vector2(.77f,.985f));
            AdminButton("Close_Lab",adminPanel,"CLOSE",new Vector2(.82f,.91f),new Vector2(.98f,.98f),ToggleTopicAdmin);
            adminInfo=AdminLabel("Target_Status",adminPanel,"",21,new Vector2(.025f,.79f),new Vector2(.97f,.90f));
            string[] tabs={"TOPICS","SUPPLIES","PLAYERS","MONSTERS / SKY"};
            for(int i=0;i<4;i++){int t=i;AdminButton("Tab_"+i,adminPanel,tabs[i],new Vector2(.02f+i*.245f,.70f),new Vector2(.255f+i*.245f,.78f),()=>{tab=t;confirm="";PopulateAdmin();});}
            var scroll=AdminRect("Admin_Scroll",adminPanel,new Vector2(.025f,.14f),new Vector2(.975f,.685f));adminScroll=scroll.gameObject.AddComponent<ScrollRect>();adminScroll.horizontal=false;adminScroll.movementType=ScrollRect.MovementType.Clamped;adminScroll.scrollSensitivity=45;
            var viewport=AdminRect("Viewport",scroll,Vector2.zero,new Vector2(.97f,1));viewport.gameObject.AddComponent<Image>().color=new Color(.035f,.12f,.14f);viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            adminContent=AdminRect("Content",viewport,new Vector2(0,1),Vector2.one);adminContent.pivot=new Vector2(.5f,1);
            var layout=adminContent.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=10;layout.padding=new RectOffset(12,12,10,10);layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var fit=adminContent.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;adminScroll.viewport=viewport;adminScroll.content=adminContent;
            var rail=AdminRect("Visible_Scrollbar",scroll,new Vector2(.977f,0),Vector2.one);rail.gameObject.AddComponent<Image>().color=new Color(.15f,.22f,.24f);
            var slide=AdminRect("Sliding_Area",rail,Vector2.zero,Vector2.one);var handle=AdminRect("Handle",slide,Vector2.zero,Vector2.one);handle.gameObject.AddComponent<Image>().color=new Color(.88f,.65f,.28f);
            var bar=rail.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();adminScroll.verticalScrollbar=bar;adminScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
            adminMessage=AdminLabel("Action_Feedback",adminPanel,"",20,new Vector2(.025f,.025f),new Vector2(.975f,.125f));adminMessage.alignment=TextAlignmentOptions.TopLeft;
            adminPanel.gameObject.SetActive(false);PopulateAdmin();
#endif
        }
        public void ToggleTopicAdmin(){if(adminPanel==null||!session.CanUseTopicAdmin)return;bool show=!AdminPanelOpen;adminPanel.gameObject.SetActive(show);if(show){ArchiveTopicUI.Current?.Close();PopulateAdmin();}}
        void RefreshTopicAdmin()
        {
            if(adminToggle==null)return;bool allowed=session!=null&&session.CanUseTopicAdmin;adminToggle.gameObject.SetActive(allowed);if(!allowed){adminPanel.gameObject.SetActive(false);return;}
            if(AdminPanelOpen){adminInfo.text="T"+(selectedTopic+1)+" · "+ArchiveTopicCatalog.Names[selectedTopic]+"    |    Target: "+session.ActorName(TestActor)+"    |    Hearts "+session.hunting.health.ActorHearts(TestActor)+"/"+session.hunting.health.MaximumHealth;
                adminMessage.text=string.IsNullOrEmpty(confirm)?session.AdminTestNotice:confirm;}
        }
        void PopulateAdmin()
        {
            if(adminContent==null)return;foreach(Transform child in adminContent){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            void Row(string a,Action first,string b,Action second){var row=AdminRect("Control_Row",adminContent,Vector2.zero,Vector2.one);row.gameObject.AddComponent<LayoutElement>().preferredHeight=74;AdminButton(a,row,a,new Vector2(0,0),new Vector2(.49f,1),()=>first());AdminButton(b,row,b,new Vector2(.51f,0),Vector2.one,()=>second());}
            void Run(ArchiveAdminTestAction action,int target=0,int value=0,bool close=false){confirm="";session.RequestAdminTest(action,target,value);if(close&&AdminPanelOpen)ToggleTopicAdmin();}
            Row("Previous topic",()=>{selectedTopic=(selectedTopic+9)%10;PopulateAdmin();},"Next topic",()=>{selectedTopic=(selectedTopic+1)%10;PopulateAdmin();});
            if(tab==0)
            {
                Row("Visit selected",()=>Run(ArchiveAdminTestAction.VisitTopic,selectedTopic,0,true),"Visit + start Book",()=>Run(ArchiveAdminTestAction.StartTopic,selectedTopic,0,true));
                Row("Complete selected topic",()=>Run(ArchiveAdminTestAction.CompleteTopic,selectedTopic),"Reset selected topic",()=>Run(ArchiveAdminTestAction.ResetTopic,selectedTopic));
                Row("Complete ACTIVE topic",()=>Run(ArchiveAdminTestAction.CompleteTopic,session.ActiveTopic),"Reset ACTIVE topic",()=>Run(ArchiveAdminTestAction.ResetTopic,session.ActiveTopic));
                Row("Complete all T1-T10 / portal",()=>Run(ArchiveAdminTestAction.CompleteAllTopics),"RESET MATCH (tap twice)",()=>{if(confirm=="Tap RESET MATCH again to restart all practice progress.")Run(ArchiveAdminTestAction.ResetMatch);else confirm="Tap RESET MATCH again to restart all practice progress.";});
            }
            else if(tab==1)
            {
                Row("Previous item",()=>{chosenItem=(chosenItem+5)%6;PopulateAdmin();},"Next item",()=>{chosenItem=(chosenItem+1)%6;PopulateAdmin();});
                var name=AdminLabel("Item_Selection",adminContent,"SELECTED DROP: "+ArchiveRewardView.ItemName((ArchiveSurvivalItem)chosenItem),25,Vector2.zero,Vector2.one);name.gameObject.AddComponent<LayoutElement>().preferredHeight=50;
                Row("Give selected item",()=>Run(ArchiveAdminTestAction.GrantItem,TestActor,chosenItem),"Give ALL supplies",()=>Run(ArchiveAdminTestAction.GrantAllItems,TestActor));
                Row("Spawn selected chest",()=>Run(ArchiveAdminTestAction.SpawnChest,selectedTopic,chosenItem,true),"Spawn random chest",()=>Run(ArchiveAdminTestAction.SpawnChest,selectedTopic,-1,true));
                Row("Clear target inventory",()=>Run(ArchiveAdminTestAction.ClearInventory,TestActor),"Change player target",()=>{actorIndex=(actorIndex+1)%session.AdminTestActors.Length;PopulateAdmin();});
            }
            else if(tab==2)
            {
                Row("Change player target",()=>{actorIndex=(actorIndex+1)%session.AdminTestActors.Length;PopulateAdmin();},"Spawn TEST TEAMMATE",()=>{Run(ArchiveAdminTestAction.SpawnTeammate);actorIndex=session.AdminTestActors.Length-1;});
                Row("Remove test teammate",()=>Run(ArchiveAdminTestAction.RemoveTeammate),"Recall teammate here",()=>Run(ArchiveAdminTestAction.RecallTeammate));
                Row("Lose 1 heart",()=>Run(ArchiveAdminTestAction.DamageHeart,TestActor),"Restore full hearts",()=>Run(ArchiveAdminTestAction.SetHearts,TestActor,session.hunting.health.MaximumHealth));
                Row("ZERO hearts / carry test",()=>Run(ArchiveAdminTestAction.SetHearts,TestActor,0,true),"Downed at nest (skip carry)",()=>Run(ArchiveAdminTestAction.DownAtNest,TestActor,0,true));
                Row("Admin rescue target",()=>Run(ArchiveAdminTestAction.ReviveAtNest,TestActor),"Go to nest + give revive kit",()=>{Run(ArchiveAdminTestAction.GrantItem,session.LocalActor,(int)ArchiveSurvivalItem.RevivalKit);Run(ArchiveAdminTestAction.TeleportToNest,0,0,true);});
                var help=AdminLabel("Practice_Instructions",adminContent,"RESCUE TEST: Spawn teammate → ZERO hearts → watch the carry → take Revival Kit to nest → use it in SUPPLIES. Test teammate is offline only; actual human multiplayer remains unchanged.",20,Vector2.zero,Vector2.one);help.gameObject.AddComponent<LayoutElement>().preferredHeight=110;
            }
            else
            {
                Row("Start shared clock now",()=>Run(ArchiveAdminTestAction.StartMatchClock),"Clock: 1 minute left",()=>Run(ArchiveAdminTestAction.MatchClockOneMinute,0,0,true));
                Row("Force MATCH TIMEOUT",()=>Run(ArchiveAdminTestAction.ExpireMatchClock,0,0,true),"Open portal / test 2m escape",()=>Run(ArchiveAdminTestAction.OpenPortal,0,0,true));
                Row("Monster: "+(monsterIndex==0?"PRIMARY":"NIGHT GUARDIAN"),()=>{monsterIndex=1-monsterIndex;PopulateAdmin();},"Go to nest",()=>Run(ArchiveAdminTestAction.TeleportToNest,0,0,true));
                Row("Awaken / begin hunt",()=>Run(ArchiveAdminTestAction.StartHunt,monsterIndex,0,true),"Sleep selected monster",()=>Run(ArchiveAdminTestAction.SleepMonster,monsterIndex));
                Row("Full-screen NIGHT entrance",()=>Run(ArchiveAdminTestAction.PreviewNight,0,0,true),"Return DAYLIGHT",()=>Run(ArchiveAdminTestAction.ReturnDaylight));
                Row("Replay second entrance",()=>Run(ArchiveAdminTestAction.ReplayEntrance,0,0,true),"POV jumpscare (offline)",()=>Run(ArchiveAdminTestAction.PreviewJumpscare,monsterIndex,0,true));
                Row("Preview real barrier",()=>Run(ArchiveAdminTestAction.PreviewBarrier,monsterIndex,0,true),"Release / reset capture",()=>Run(ArchiveAdminTestAction.ReleaseCapture,monsterIndex));
                Row("Barrier SUCCESS",()=>Run(ArchiveAdminTestAction.BarrierSuccess,monsterIndex),"Barrier FAILED / attack",()=>Run(ArchiveAdminTestAction.BarrierFail,monsterIndex,0,true));
            }
            Canvas.ForceUpdateCanvases();adminScroll.verticalNormalizedPosition=1;
        }
        static RectTransform AdminRect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        Button AdminButton(string name,Transform parent,string caption,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {if(caption=="Complete ACTIVE topic")name="Admin_Complete_Active_Topic";if(caption=="Reset ACTIVE topic")name="Admin_Reset_Active_Topic";var rect=AdminRect(name,parent,min,max);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.12f,.32f,.34f);var button=rect.gameObject.AddComponent<Button>();button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(action);AdminLabel("Label",rect,caption,22,new Vector2(.03f,.05f),new Vector2(.97f,.95f));return button;}
        TMP_Text AdminLabel(string name,Transform parent,string text,int size,Vector2 min,Vector2 max)
        {var r=AdminRect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=textFont!=null?textFont:StageUITheme.ReadingFont;t.fontSize=size;t.color=AdminCream;t.text=text;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;}
    }
}
