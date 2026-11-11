using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    public enum ArchiveOnboardingStep { None, Book, Equip, Use, Share }

    /// <summary>Local first-use presentation. Reads accepted owner state; never sends a gameplay command or owns a timer.</summary>
    public sealed partial class ArchiveLearningUI
    {
        public const string OnboardingPreferenceKey="OpenArchive.Learning.v1.Controls.v1.Completed";
        RectTransform onboardingCard,onboardingOutline,onboardingBookMarker;
        TMP_Text onboardingHeading,onboardingBody;
        Button onboardingSkip,onboardingHide,onboardingReplay;
        RectTransform onboardingHand;
        Transform onboardingLegacyTools;
        ArchiveOnboardingStep onboardingStep;
        RectTransform onboardingTarget;
        Transform onboardingWorldTarget;
        string onboardingMatch="";
        bool onboardingDismissed,onboardingObserved;
        int onboardingMask,onboardingBookTopic=-1,onboardingPreviousSelected=-1,onboardingPreviousHearts,onboardingPreviousShot;
        int onboardingRecipient;
        int onboardingObservedRevision=-1;
        int[] onboardingPreviousItems=new int[6],onboardingRecipientItems=new int[6];
        bool onboardingPreviousLight;
        float onboardingPreviousBattery;
        double onboardingPreviousUse;
        float onboardingBindingsAt,onboardingDeferredUntil;
        readonly HashSet<string> onboardingSupport=new HashSet<string>();
        readonly HashSet<string> onboardingGifts=new HashSet<string>();
        readonly Vector3[] onboardingCorners=new Vector3[4];
        static readonly Color OnboardingGold=new Color(1,.79f,.22f,.95f);

        public bool OnboardingVisible=>onboardingCard!=null&&onboardingCard.gameObject.activeInHierarchy;
        public ArchiveOnboardingStep OnboardingStep=>onboardingStep;
        public string OnboardingText=>OnboardingVisible?onboardingBody.text:"";
        public RectTransform OnboardingCard=>onboardingCard;
        public RectTransform OnboardingHighlightTarget=>onboardingTarget;
        public Transform OnboardingWorldTarget=>onboardingWorldTarget;
        public bool OnboardingWorldMarkerVisible=>onboardingBookMarker!=null&&onboardingBookMarker.gameObject.activeSelf;
        public bool OnboardingSkippedThisMatch=>onboardingDismissed;
        public int OnboardingProgressMask=>onboardingMask;
        public Button OnboardingSkipButton=>onboardingSkip;
        public Button OnboardingHideButton=>onboardingHide;
        public Button OnboardingReplayButton=>onboardingReplay;

        public bool IsOnboardingComplete(ArchiveOnboardingStep step)=>step!=ArchiveOnboardingStep.None&&
            (PlayerPrefs.GetInt(OnboardingPreferenceKey,0)&(1<<((int)step-1)))!=0;

        void BuildOnboarding()
        {
            onboardingMask=PlayerPrefs.GetInt(OnboardingPreferenceKey,0)&15;
            onboardingCard=Rect("First_Use_Control_Tip",safe,Vector2.zero,Vector2.zero);
            onboardingCard.anchorMin=onboardingCard.anchorMax=new Vector2(0,.67f);onboardingCard.pivot=new Vector2(0,1);
            onboardingCard.anchoredPosition=new Vector2(24,0);onboardingCard.sizeDelta=new Vector2(420,240);
            var background=onboardingCard.gameObject.AddComponent<Image>();background.sprite=topicUI!=null&&topicUI.panelArt!=null?topicUI.panelArt:StageUITheme.Panel;
            background.type=Image.Type.Sliced;background.pixelsPerUnitMultiplier=3.5f;background.raycastTarget=false;
            onboardingHeading=Label("Control_Tip_Title",onboardingCard,"",25,true);
            Stretch(onboardingHeading.rectTransform,new Vector2(.055f,.78f),new Vector2(.95f,.90f));
            onboardingBody=Label("Control_Tip_Instructions",onboardingCard,"",21,false);
            Stretch(onboardingBody.rectTransform,new Vector2(.055f,.30f),new Vector2(.95f,.76f));onboardingBody.richText=false;
            onboardingBody.enableAutoSizing=true;onboardingBody.fontSizeMin=18;onboardingBody.fontSizeMax=21;
            onboardingSkip=Button("Skip_This_Control_Tip",onboardingCard,"Skip tip",SkipOnboardingStep);
            Stretch((RectTransform)onboardingSkip.transform,new Vector2(.055f,.055f),new Vector2(.41f,.27f));
            onboardingHide=Button("Hide_Control_Tips_For_Match",onboardingCard,"Hide for match",DismissOnboarding);
            Stretch((RectTransform)onboardingHide.transform,new Vector2(.44f,.055f),new Vector2(.95f,.27f));
            foreach(var button in new[]{onboardingSkip,onboardingHide})button.GetComponentInChildren<TMP_Text>().fontSize=20;
            onboardingOutline=Rect("Control_Tip_Target_Outline",safe,Vector2.one*.5f,Vector2.one*.5f);onboardingOutline.pivot=Vector2.one*.5f;
            var outline=onboardingOutline.gameObject.AddComponent<ArchiveHotbarSelectionBorder>();outline.color=OnboardingGold;outline.raycastTarget=false;
            onboardingBookMarker=Rect("Control_Tip_Book_Marker",safe,Vector2.one*.5f,Vector2.one*.5f);onboardingBookMarker.pivot=Vector2.one*.5f;onboardingBookMarker.sizeDelta=new Vector2(70,38);
            var marker=onboardingBookMarker.gameObject.AddComponent<Image>();marker.sprite=StageUITheme.ButtonArt;marker.type=Image.Type.Sliced;marker.color=new Color(.04f,.24f,.22f,.95f);marker.raycastTarget=false;
            var caption=Label("Marked_Book",onboardingBookMarker,"BOOK",20,true);caption.color=new Color(1,.90f,.58f);caption.alignment=TextAlignmentOptions.Center;
            // The replay action shares the guide's fixed footer without shrinking its reading area.
            onboardingReplay=Button("Replay_First_Use_Control_Tips",guide,"Replay control tips",ReplayOnboarding);
            Stretch((RectTransform)onboardingReplay.transform,new Vector2(.045f,.055f),new Vector2(.32f,.165f));
            StyleGuideButton(onboardingReplay,"Replay control tips",22);
            HideOnboarding();
        }

        public void SkipOnboardingStep()
        {if(onboardingStep!=ArchiveOnboardingStep.None)CompleteOnboarding(onboardingStep);HideOnboarding();}
        public void DismissOnboarding(){onboardingDismissed=true;HideOnboarding();}
        public void ReplayOnboarding()
        {
            ResetOnboardingForQA();PlayerPrefs.SetInt(PreferencePrefix+"AutoTips",1);PlayerPrefs.Save();UpdateAutoCaption();DismissGuide();
        }
        /// <summary>Resets local control-tip memory only, never academic work, inventory or shared state.</summary>
        public void ResetOnboardingForQA()
        {
            PlayerPrefs.DeleteKey(OnboardingPreferenceKey);PlayerPrefs.Save();onboardingMask=0;onboardingDismissed=false;
            onboardingBookTopic=-1;onboardingObserved=false;onboardingDeferredUntil=0;HideOnboarding();
        }
        void CompleteOnboarding(ArchiveOnboardingStep step)
        {
            if(step==ArchiveOnboardingStep.None)return;
            onboardingMask=(PlayerPrefs.GetInt(OnboardingPreferenceKey,0)&15)|(1<<((int)step-1));PlayerPrefs.SetInt(OnboardingPreferenceKey,onboardingMask);PlayerPrefs.Save();
            onboardingDeferredUntil=Time.unscaledTime+1.5f;
        }
        void HideOnboarding()
        {
            onboardingStep=ArchiveOnboardingStep.None;onboardingTarget=null;onboardingWorldTarget=null;
            if(onboardingCard!=null)onboardingCard.gameObject.SetActive(false);
            if(onboardingOutline!=null)onboardingOutline.gameObject.SetActive(false);
            if(onboardingBookMarker!=null)onboardingBookMarker.gameObject.SetActive(false);
        }

        void LateUpdate()=>RefreshOnboarding();
        public void RefreshOnboarding()
        {
            if(onboardingCard==null)return;
            if(session==null)session=ArchiveTopicSession.Current;if(topicUI==null)topicUI=ArchiveTopicUI.Current;
            bool ready=session!=null&&session.Ready&&session.gameObject.scene.path=="Assets/Scenes/MultiplayerGameplay.unity";
            if(!ready){onboardingObserved=false;HideOnboarding();return;}
            if(onboardingMatch!=session.State.matchId)
            {onboardingMatch=session.State.matchId;onboardingDismissed=false;onboardingObserved=false;onboardingBookTopic=-1;onboardingMask=PlayerPrefs.GetInt(OnboardingPreferenceKey,0)&15;}
            ObserveOnboarding();
            if(!AutoTips||onboardingDismissed||Time.unscaledTime<onboardingDeferredUntil||!OnboardingAllowed())
            {HideOnboarding();return;}
            if(Time.unscaledTime>=onboardingBindingsAt)
            {
                onboardingBindingsAt=Time.unscaledTime+.5f;
                if(onboardingHand==null)
                    foreach(var button in FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(button.name=="InteractButton"){onboardingHand=button.transform as RectTransform;break;}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(onboardingLegacyTools==null)onboardingLegacyTools=FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .FirstOrDefault(rect=>rect.name=="Test_Tools_Content");
#endif
            }
            if(onboardingLegacyTools!=null&&onboardingLegacyTools.gameObject.activeInHierarchy){HideOnboarding();return;}
            var view=ArchiveRewardView.Current;
            var point=topicUI!=null?topicUI.FocusedPoint:null;
            if(!IsOnboardingComplete(ArchiveOnboardingStep.Book)&&point!=null&&point.role==ArchivePointRole.Book&&
                point.station!=null&&session.State.stations[point.station.topic].phase!=2&&
                string.IsNullOrEmpty(session.TopicLockNotice(point.station.topic))&&onboardingHand!=null&&onboardingHand.gameObject.activeInHierarchy)
            {
                onboardingBookTopic=point.station.topic;
                ShowOnboarding(ArchiveOnboardingStep.Book,"READ THE BOOK","Face the marked Book. Tap the hand button to open its challenge. Book activation starts the topic clock.",onboardingHand,point.transform);return;
            }
            if(view==null){HideOnboarding();return;}
            int selected=session.EquippedItem(session.LocalActor);
            if(!IsOnboardingComplete(ArchiveOnboardingStep.Equip))
            {
                for(int slot=0;slot<6;slot++)
                {
                    var button=view.HotbarButton(slot);
                    if(session.CountItem(session.LocalActor,(ArchiveSurvivalItem)slot)<=0||button==null||!button.gameObject.activeInHierarchy||!button.interactable)continue;
                    string text=selected==slot?"This slot is already equipped. Tap it to put the item away, then tap again to equip it. The border follows your selection.":
                        "Tap the highlighted inventory slot. Its border shows the item you are holding. Selecting does not consume it.";
                    ShowOnboarding(ArchiveOnboardingStep.Equip,"EQUIP AN ITEM",text,(RectTransform)button.transform);return;
                }
            }
            if(selected>=0&&selected<6&&!IsOnboardingComplete(ArchiveOnboardingStep.Use)&&UsefulItemAction(view,selected,out string help))
            {ShowOnboarding(ArchiveOnboardingStep.Use,"USE YOUR ITEM",help,view.MobileItemActionRect);return;}
            if(selected>=0&&selected<6&&!IsOnboardingComplete(ArchiveOnboardingStep.Share)&&view.TeamItemPanel!=null&&view.TeamItemPanel.gameObject.activeInHierarchy&&view.GiveItemButton!=null&&view.GiveItemButton.IsInteractable())
            {
                string name=session.ActorName(view.TargetActor);if(name.Length>22)name=name.Substring(0,22);
                ShowOnboarding(ArchiveOnboardingStep.Share,"HELP A TEAMMATE","Check TO: "+name+" in the teammate panel. Tap GIVE 1 to transfer one held item. Sharing is optional.",(RectTransform)view.GiveItemButton.transform);return;
            }
            HideOnboarding();
        }
        bool OnboardingAllowed()
        {
            var player=Player.LocalInstance;
            if(!isActiveAndEnabled||player==null||!player.enabled||player.IsMovementLocked||Time.timeScale<=0||!session.MatchTimeAllowsGameplay||
                !session.CanAct(session.LocalActor)||session.TeamWon||session.TeamDefeated||session.PortalOpened||session.NightEntranceActive||MultiplayerLobbyService.IsGameplayRecovering||
                guideOpen||BarrierVisible||topicUI!=null&&topicUI.ReadingStation!=null||GameInput.Instance!=null&&GameInput.Instance.GameplayInputBlocked||
                TeleportManager.Instance!=null&&(TeleportManager.Instance.IsTeleporting||TeleportManager.Instance.FadeAlpha>.001f)||
                (session.GetComponent<ArchiveTopicTestControls>()?.AdminPanelOpen??false))return false;
            var team=ArchiveTeamCoordinationUI.Current;if(team!=null&&(team.PartyPanelOpen||team.PingMenuOpen))return false;
            foreach(var monster in KhezuHuntingController.ActiveMonsters)
            {
                if(monster==null)continue;
                if(monster.GetComponent<KhezuCinematicJumpscareController>()?.IsPlaying??false)return false;
                if(monster.IsTranquilized)continue;
                if(monster.State!=KhezuHuntState.Hunting&&monster.State!=KhezuHuntState.NavigationBlocked)continue;
                float distance=(monster.transform.position-player.transform.position).sqrMagnitude;
                if(distance<(monster.TargetPlayerId==session.LocalActor?80*80:28*28))return false;
            }
            return true;
        }
        bool UsefulItemAction(ArchiveRewardView view,int item,out string text)
        {
            text="";if(view.UseEquippedButton==null||!view.UseEquippedButton.gameObject.activeInHierarchy||!view.UseEquippedButton.IsInteractable())return false;
            switch((ArchiveSurvivalItem)item)
            {
                case ArchiveSurvivalItem.Flashlight:text="Tap LIGHT to switch your flashlight. Turn your character toward the area to light; switch it off when you do not need the beam.";return true;
                case ArchiveSurvivalItem.Battery:text="Your flashlight needs charge. Tap LOAD BATTERY to use one battery and refill it.";return true;
                case ArchiveSurvivalItem.Medkit:text="One of your hearts is missing. Tap HEAL to use a medkit and restore one heart.";return true;
                case ArchiveSurvivalItem.RevivalKit:text="You are beside a downed teammate. Tap REVIVE TEAMMATE to use your kit and bring them back.";return true;
                case ArchiveSurvivalItem.Tranquilizer:
                    var camera=Camera.main;if(camera==null)return false;
                    foreach(var monster in KhezuHuntingController.ActiveMonsters)
                    {
                        if(monster==null||monster.IsTranquilized||monster.State!=KhezuHuntState.Hunting&&monster.State!=KhezuHuntState.NavigationBlocked)continue;
                        var delta=monster.transform.position-camera.transform.position;
                        if((monster.transform.position-Player.LocalInstance.transform.position).sqrMagnitude<ArchiveTopicSession.TranquilizerRange*ArchiveTopicSession.TranquilizerRange&&delta.sqrMagnitude>28*28&&Vector3.Dot(camera.transform.forward,delta.normalized)>.85f)
                        {
                            var body=monster.GetComponent<Collider>();var destination=body!=null?body.bounds.center:monster.transform.position+Vector3.up*2;
                            if(Physics.Linecast(camera.transform.position,destination,out var obstruction,~(1<<7),QueryTriggerInteraction.Ignore)&&
                                obstruction.transform!=monster.transform&&!obstruction.transform.IsChildOf(monster.transform))continue;
                            text="An awake guardian is ahead. Aim at it, then tap FIRE. Each shot uses one dart.";return true;
                        }
                    }
                    return false;
                default:return false;
            }
        }

        void ObserveOnboarding()
        {
            int actor=session.LocalActor;var inventory=session.Inventory(actor);int selected=inventory?.equippedItem??-1;
            var view=ArchiveRewardView.Current;int target=view!=null?view.TargetActor:0;var recipient=target==0?null:session.Inventory(target);
            int hearts=session.hunting?.health!=null?session.hunting.health.ActorHearts(actor):0;
            // Reader visibility is local; it may change without a room revision.
            if(onboardingObserved&&AutoTips&&!onboardingDismissed&&!IsOnboardingComplete(ArchiveOnboardingStep.Book)&&
                onboardingBookTopic>=0&&topicUI?.ReadingStation?.topic==onboardingBookTopic)CompleteOnboarding(ArchiveOnboardingStep.Book);
            // All accepted inventory/health use transactions commit the existing
            // checkpoint. Avoid rebuilding receipt-key sets on unchanged frames.
            if(onboardingObserved&&onboardingObservedRevision==session.State.revision&&target==onboardingRecipient)return;
            if(onboardingObserved&&AutoTips&&!onboardingDismissed)
            {
                if(!IsOnboardingComplete(ArchiveOnboardingStep.Equip)&&selected>=0&&selected<6&&selected!=onboardingPreviousSelected&&session.CountItem(actor,(ArchiveSurvivalItem)selected)>0)
                    CompleteOnboarding(ArchiveOnboardingStep.Equip);
                bool consumed=false;if(inventory!=null)for(int i=0;i<6;i++)consumed|=inventory.items[i]<onboardingPreviousItems[i];
                bool supported=session.State.rewards?.support?.Any(s=>s.actor==actor&&!onboardingSupport.Contains(s.kind+":"+s.target))??false;
                bool used=inventory!=null&&inventory.nextUseAt>onboardingPreviousUse&&
                    (inventory.flashlightOn!=onboardingPreviousLight||consumed&&(hearts>onboardingPreviousHearts||inventory.batterySeconds>onboardingPreviousBattery+1||supported)||
                    session.State.survival.shotSerial>onboardingPreviousShot&&session.State.survival.shotActor==actor);
                if(used&&!IsOnboardingComplete(ArchiveOnboardingStep.Use))CompleteOnboarding(ArchiveOnboardingStep.Use);
                bool given=false;
                if(inventory!=null&&recipient!=null&&target==onboardingRecipient&&inventory.nextUseAt>onboardingPreviousUse)
                    for(int i=0;i<6;i++)given|=inventory.items[i]<onboardingPreviousItems[i]&&recipient.items[i]>onboardingRecipientItems[i];
                bool gift=inventory!=null&&inventory.nextUseAt>onboardingPreviousUse&&consumed&&
                    (session.State.rewards?.gifts?.Any(g=>g.donor==actor&&!onboardingGifts.Contains(g.recipient+":"+g.item))??false);
                if((given||gift)&&!IsOnboardingComplete(ArchiveOnboardingStep.Share))CompleteOnboarding(ArchiveOnboardingStep.Share);
            }
            onboardingObserved=true;onboardingObservedRevision=session.State.revision;onboardingPreviousSelected=selected;onboardingPreviousHearts=hearts;onboardingPreviousShot=session.State.survival.shotSerial;
            onboardingPreviousUse=inventory?.nextUseAt??0;onboardingPreviousLight=inventory?.flashlightOn??false;onboardingPreviousBattery=inventory?.batterySeconds??0;
            onboardingRecipient=target;
            for(int i=0;i<6;i++){onboardingPreviousItems[i]=inventory?.items[i]??0;onboardingRecipientItems[i]=recipient?.items[i]??0;}
            onboardingSupport.Clear();if(session.State.rewards?.support!=null)foreach(var credit in session.State.rewards.support.Where(s=>s.actor==actor))onboardingSupport.Add(credit.kind+":"+credit.target);
            onboardingGifts.Clear();if(session.State.rewards?.gifts!=null)foreach(var gift in session.State.rewards.gifts.Where(g=>g.donor==actor))onboardingGifts.Add(gift.recipient+":"+gift.item);
        }
        void ShowOnboarding(ArchiveOnboardingStep step,string heading,string body,RectTransform target,Transform world=null)
        {
            onboardingStep=step;onboardingTarget=target;onboardingWorldTarget=world;
            onboardingHeading.text=heading;onboardingBody.text=body;onboardingCard.gameObject.SetActive(true);
            // The card is deliberately outside the movement/jump controls and the
            // right-hand academic objective cards. Only its two actions raycast.
            float width=Mathf.Min(420,safe.rect.width*.36f);onboardingCard.sizeDelta=new Vector2(Mathf.Max(320,width),240);
            if(target!=null&&target.gameObject.activeInHierarchy)
            {
                target.GetWorldCorners(onboardingCorners);var canvas=target.GetComponentInParent<Canvas>();var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(safe,RectTransformUtility.WorldToScreenPoint(camera,onboardingCorners[0]),null,out var min);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(safe,RectTransformUtility.WorldToScreenPoint(camera,onboardingCorners[2]),null,out var max);
                onboardingOutline.anchoredPosition=(min+max)*.5f;onboardingOutline.sizeDelta=new Vector2(Mathf.Abs(max.x-min.x)+14,Mathf.Abs(max.y-min.y)+14);onboardingOutline.gameObject.SetActive(true);
            }
            else onboardingOutline.gameObject.SetActive(false);
            bool worldVisible=false;var view=Camera.main;
            if(world!=null&&view!=null)
            {
                var screen=view.WorldToScreenPoint(world.position+Vector3.up*1.5f);
                if(screen.z>0&&Screen.safeArea.Contains(new Vector2(screen.x,screen.y)))
                {RectTransformUtility.ScreenPointToLocalPointInRectangle(safe,screen,null,out var position);onboardingBookMarker.anchoredPosition=position;worldVisible=true;}
            }
            onboardingBookMarker.gameObject.SetActive(worldVisible);
        }
    }
}
