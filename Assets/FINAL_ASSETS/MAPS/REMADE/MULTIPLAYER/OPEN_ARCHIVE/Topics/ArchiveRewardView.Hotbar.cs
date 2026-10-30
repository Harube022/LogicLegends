using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveRewardView
    {
        readonly Image[] slotFaces=new Image[6],slotIcons=new Image[6];
        readonly RectTransform[] slotSelections=new RectTransform[6];
        readonly TMP_Text[] slotNumbers=new TMP_Text[6],slotCaptions=new TMP_Text[6];
        static readonly string[] ShortItemNames={"FLASHLIGHT","BATTERY","MEDKIT","REVIVE","TRANQUILIZER\nGUN","AMMO"};
        const float HotbarHeight=216;
        Button useEquipped;
        TMP_Text itemHelp;
        RectTransform mobileAction,handControl,jumpControl;
        readonly RectTransform[] actionGlyphs=new RectTransform[6];
        readonly Vector3[] controlCorners=new Vector3[4];
        RectTransform flashlightRays;
        UnityEngine.UI.Image[] glyphImages;
        public RectTransform MobileItemActionRect=>mobileAction;
        public bool HasCircularItemAction=>useEquipped!=null&&useEquipped.image!=null&&
            useEquipped.image.sprite!=null&&useEquipped.image.sprite.name=="joystick_circle_outer_0";
        public int SelectedHotbarSlot=>session!=null?session.EquippedItem(session.LocalActor):-1;
        public Button HotbarButton(int slot)=>slot>=0&&slot<6?itemButtons[slot]:null;
        public RectTransform HotbarSelectionBorder(int slot)=>slot>=0&&slot<6?slotSelections[slot]:null;
        public Button UseEquippedButton=>useEquipped;
        public string SelectedActionLabel=>info!=null?info.text.Replace("\n"," "):"";
        public string SelectedActionHint=>itemHelp!=null?itemHelp.text:"";
        public bool RequestVisibleForQA()=>pack!=null&&pack.gameObject.activeInHierarchy;
        void BuildHotbar()
        {
            pack=Rect("Survival_Hotbar",root,Vector2.one*.5f,Vector2.one*.5f);pack.sizeDelta=new Vector2(600,HotbarHeight);
            contents=Rect("Collected_Item_Slots",pack,Vector2.zero,new Vector2(1,108/HotbarHeight));
            for(int i=0;i<6;i++)
            {
                int slot=i;var r=Rect("Hotbar_Slot_"+(i+1),contents,new Vector2(i/6f,0),new Vector2((i+1)/6f,1));r.offsetMin=new Vector2(4,0);r.offsetMax=new Vector2(-4,0);
                slotFaces[i]=r.gameObject.AddComponent<Image>();var b=r.gameObject.AddComponent<Button>();b.targetGraphic=slotFaces[i];itemButtons[i]=b;
                b.navigation=new Navigation{mode=Navigation.Mode.None};b.onClick.AddListener(()=>SelectHotbarSlot(slot));
                var icon=Rect("Collected_Item_Icon",r,new Vector2(.19f,.47f),new Vector2(.81f,.91f));slotIcons[i]=icon.gameObject.AddComponent<Image>();slotIcons[i].preserveAspect=true;slotIcons[i].raycastTarget=false;if(itemIcons!=null&&i<itemIcons.Length)slotIcons[i].sprite=itemIcons[i];
                slotNumbers[i]=Text("Slot_Number",r,16);slotNumbers[i].text=(i+1).ToString();slotNumbers[i].alignment=TextAlignmentOptions.TopLeft;slotNumbers[i].margin=new Vector4(10,6,0,0);
                slotCaptions[i]=Text("Item_Button_Name",r,15);slotCaptions[i].text=ShortItemNames[i];slotCaptions[i].rectTransform.anchorMin=new Vector2(.08f,.28f);slotCaptions[i].rectTransform.anchorMax=new Vector2(.92f,.50f);slotCaptions[i].alignment=TextAlignmentOptions.Center;
                itemTexts[i]=Text("Stack_Count",r,18);itemTexts[i].rectTransform.anchorMin=new Vector2(.08f,.08f);itemTexts[i].rectTransform.anchorMax=new Vector2(.92f,.32f);itemTexts[i].alignment=TextAlignmentOptions.Center;
                StageUITheme.SkinButton(b);
                // Refresh TMP's cached material instance before setting outlines after a font change.
                slotNumbers[i].font=StageUITheme.ReadingFont;slotNumbers[i].fontSharedMaterial=StageUITheme.ReadingFont.material;_=slotNumbers[i].fontMaterial;slotNumbers[i].enableAutoSizing=false;slotNumbers[i].fontSize=16;
                slotCaptions[i].enableAutoSizing=true;slotCaptions[i].fontSizeMin=10;slotCaptions[i].fontSizeMax=15;
                if(i==4){slotCaptions[i].rectTransform.anchorMin=new Vector2(.06f,.26f);slotCaptions[i].rectTransform.anchorMax=new Vector2(.94f,.53f);}
                itemTexts[i].font=StageUITheme.ReadingFont;itemTexts[i].fontSharedMaterial=StageUITheme.ReadingFont.material;_=itemTexts[i].fontMaterial;itemTexts[i].enableAutoSizing=false;itemTexts[i].fontSize=17;itemTexts[i].outlineWidth=.08f;
                var marker=Rect("Equipped_Item_Border",r,Vector2.zero,Vector2.one);marker.offsetMin=Vector2.one*-1;marker.offsetMax=Vector2.one;
                var border=marker.gameObject.AddComponent<ArchiveHotbarSelectionBorder>();border.raycastTarget=false;
                slotSelections[i]=marker;marker.gameObject.SetActive(false);
            }
            var selectedShade=Rect("Selected_Item_Backplate",pack,new Vector2(0,.76f),Vector2.one);StageUITheme.CompactSurface(selectedShade.gameObject.AddComponent<UnityEngine.UI.Image>());selectedShade.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            packTitle=Text("Selected_Item_Name",pack,22);packTitle.font=StageUITheme.HeadingFont;packTitle.rectTransform.anchorMin=new Vector2(.035f,.78f);packTitle.rectTransform.anchorMax=new Vector2(.965f,.97f);packTitle.alignment=TextAlignmentOptions.Left;
            var helpShade=Rect("Item_Instructions_Backplate",pack,new Vector2(0,.51f),new Vector2(1,.73f));helpShade.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.03f,.10f,.12f,.96f);helpShade.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            itemHelp=Text("Selected_Item_Instructions",pack,18);itemHelp.rectTransform.anchorMin=new Vector2(.02f,.515f);itemHelp.rectTransform.anchorMax=new Vector2(.98f,.725f);itemHelp.alignment=TextAlignmentOptions.Left;itemHelp.margin=new Vector4(4,0,4,0);itemHelp.enableAutoSizing=true;itemHelp.fontSizeMin=14;itemHelp.fontSizeMax=18;
            BuildMobileItemAction();
            BuildTeamItemActions();
        }
        void BuildMobileItemAction()
        {
            UnityEngine.UI.Button actualHand=null;
            foreach(var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(b.name=="InteractButton"){actualHand=b;handControl=b.transform as RectTransform;}
                if(b.name=="JumpButton")jumpControl=b.transform as RectTransform;
            }
            Sprite circle=null;
            foreach(var image in FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(image.sprite!=null&&image.sprite.name=="joystick_circle_outer_0"){circle=image.sprite;break;}
            // Reuse the existing mobile control ring. This is deliberately a separate
            // gameplay action like Hand/Jump, not another framed hotbar button.
            mobileAction=Rect("Use_Equipped_Item",root,Vector2.one*.5f,Vector2.one*.5f);mobileAction.sizeDelta=Vector2.one*220;
            var face=mobileAction.gameObject.AddComponent<UnityEngine.UI.Image>();face.sprite=circle;face.preserveAspect=true;face.color=Color.white;
            useEquipped=mobileAction.gameObject.AddComponent<UnityEngine.UI.Button>();useEquipped.targetGraphic=face;
            useEquipped.transition=UnityEngine.UI.Selectable.Transition.ColorTint;
            if(actualHand!=null)useEquipped.colors=actualHand.colors;
            useEquipped.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};useEquipped.onClick.AddListener(()=>UseSelectedHotbarItem());
            for(int i=0;i<6;i++)
            {
                var g=Rect("Item_Action_"+((ArchiveSurvivalItem)i),mobileAction,Vector2.one*.5f,Vector2.one*.5f);g.sizeDelta=Vector2.one*120;actionGlyphs[i]=g;
            }
            var torch=actionGlyphs[0];
            GlyphBox(torch,"Flashlight_Handle",new Vector2(-10,-14),new Vector2(27,60),-35);
            GlyphBox(torch,"Flashlight_Head",new Vector2(15,20),new Vector2(49,22),-35);
            flashlightRays=Rect("Light_Rays",torch,Vector2.one*.5f,Vector2.one*.5f);flashlightRays.sizeDelta=Vector2.one*120;
            GlyphLine(flashlightRays,"Ray_1",new Vector2(35,32),new Vector2(50,39),4);
            GlyphLine(flashlightRays,"Ray_2",new Vector2(22,42),new Vector2(28,56),4);
            GlyphLine(flashlightRays,"Ray_3",new Vector2(42,17),new Vector2(56,17),4);
            GlyphBox(actionGlyphs[1],"Battery",Vector2.zero,new Vector2(47,77));
            GlyphSolid(actionGlyphs[1],"Terminal",new Vector2(0,44),new Vector2(24,9));
            GlyphLine(actionGlyphs[1],"Plus_H",new Vector2(-12,13),new Vector2(12,13),5);
            GlyphLine(actionGlyphs[1],"Plus_V",new Vector2(0,1),new Vector2(0,25),5);
            GlyphLine(actionGlyphs[1],"Minus",new Vector2(-10,-21),new Vector2(10,-21),5);
            for(int i=2;i<=3;i++)
            {
                GlyphBox(actionGlyphs[i],"Kit",new Vector2(0,-7),new Vector2(77,58));
                GlyphBox(actionGlyphs[i],"Handle",new Vector2(0,29),new Vector2(30,16));
                GlyphSolid(actionGlyphs[i],"Aid_H",new Vector2(0,-7),new Vector2(37,11));
                GlyphSolid(actionGlyphs[i],"Aid_V",new Vector2(0,-7),new Vector2(11,37));
            }
            GlyphLine(actionGlyphs[3],"Revive_Arrow_Stem",new Vector2(48,-21),new Vector2(48,30),5);
            GlyphLine(actionGlyphs[3],"Revive_Arrow_L",new Vector2(48,30),new Vector2(37,17),5);
            GlyphLine(actionGlyphs[3],"Revive_Arrow_R",new Vector2(48,30),new Vector2(59,17),5);
            GlyphSolid(actionGlyphs[4],"Dart_Gun_Barrel",new Vector2(7,16),new Vector2(88,17));
            GlyphSolid(actionGlyphs[4],"Dart_Gun_Receiver",new Vector2(-15,5),new Vector2(55,27));
            GlyphSolid(actionGlyphs[4],"Dart_Gun_Grip",new Vector2(-26,-25),new Vector2(17,45),-13);
            GlyphLine(actionGlyphs[4],"Trigger",new Vector2(-8,-7),new Vector2(-8,-22),4);
            GlyphLine(actionGlyphs[4],"Trigger_Guard",new Vector2(-8,-22),new Vector2(-28,-22),4);
            for(int n=0;n<3;n++)
            {
                float x=(n-1)*25;
                GlyphLine(actionGlyphs[5],"Dart_"+n,new Vector2(x,-37),new Vector2(x,32),5);
                GlyphLine(actionGlyphs[5],"Tip_L_"+n,new Vector2(x,43),new Vector2(x-7,28),4);
                GlyphLine(actionGlyphs[5],"Tip_R_"+n,new Vector2(x,43),new Vector2(x+7,28),4);
                GlyphLine(actionGlyphs[5],"Fins_"+n,new Vector2(x-8,-31),new Vector2(x+8,-31),5);
            }
            glyphImages=mobileAction.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            info=Text("Use_Action",mobileAction,23);info.rectTransform.anchorMin=new Vector2(-.08f,-.22f);info.rectTransform.anchorMax=new Vector2(1.08f,-.025f);
            info.alignment=TextAlignmentOptions.Center;info.font=StageUITheme.ReadingFont;info.fontStyle=FontStyles.Bold;info.color=Color.white;
            info.fontSize=21;info.enableAutoSizing=true;info.fontSizeMin=18;info.fontSizeMax=22;
            mobileAction.gameObject.SetActive(false);
        }
        static void GlyphSolid(Transform parent,string name,Vector2 position,Vector2 size,float angle=0)
        {
            var r=Rect(name,parent,Vector2.one*.5f,Vector2.one*.5f);r.anchoredPosition=position;r.sizeDelta=size;r.localEulerAngles=new Vector3(0,0,angle);
            var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=Color.white;image.raycastTarget=false;
        }
        static void GlyphLine(Transform parent,string name,Vector2 start,Vector2 end,float thickness)
        {var d=end-start;GlyphSolid(parent,name,(start+end)*.5f,new Vector2(d.magnitude,thickness),Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);}
        static void GlyphBox(Transform parent,string name,Vector2 position,Vector2 size,float angle=0)
        {
            var g=Rect(name,parent,Vector2.one*.5f,Vector2.one*.5f);g.anchoredPosition=position;g.sizeDelta=size;g.localEulerAngles=new Vector3(0,0,angle);
            var h=size*.5f;
            GlyphLine(g,"Top",new Vector2(-h.x,h.y),h,5);GlyphLine(g,"Bottom",new Vector2(-h.x,-h.y),new Vector2(h.x,-h.y),5);
            GlyphLine(g,"Left",new Vector2(-h.x,-h.y),new Vector2(-h.x,h.y),5);GlyphLine(g,"Right",new Vector2(h.x,-h.y),h,5);
        }
        static string ChargeClock(float value)
        {int seconds=Mathf.CeilToInt(Mathf.Max(0,value));return (seconds/60).ToString("00")+":"+(seconds%60).ToString("00");}
        public void SelectHotbarSlot(int slot)
        {
            if(slot<0||slot>=6||!HotbarControlsAllowed()||session.CountItem(session.LocalActor,(ArchiveSurvivalItem)slot)<=0)return;
            session.RequestEquipItem(SelectedHotbarSlot==slot?-1:slot);
        }
        bool HotbarControlsAllowed()
        {
            if(session==null||!session.Ready||Time.timeScale<=0||!session.CanAct(session.LocalActor))return false;
            // Chat owns the shared input block while typing. Keyboard polling and stale
            // pointer callbacks must honor it just like the existing movement/Hand input.
            if(GameInput.Instance!=null&&GameInput.Instance.GameplayInputBlocked)return false;
            if(ArchiveTopicUI.Current!=null&&ArchiveTopicUI.Current.ReadingStation!=null)return false;
            return !(session.GetComponent<ArchiveTopicTestControls>()?.AdminPanelOpen??false);
        }
        public bool UseSelectedHotbarItem()
        {
            if(!HotbarControlsAllowed()||!SelectedActionReady(out _,out _))return false;
            if(SelectedHotbarSlot==(int)ArchiveSurvivalItem.Tranquilizer)
                session.GetComponent<ArchiveEquippedItems>()?.FaceLocalShot(ArchiveTopicSession.LocalTranquilizerAim(Player.LocalInstance,Camera.main));
            // Darts already belong to the gun's authoritative stack. This shortcut equips
            // the existing gun without consuming ammo or introducing a reload system.
            return SelectedHotbarSlot==(int)ArchiveSurvivalItem.TranquilizerAmmo?
                session.RequestEquipItem((int)ArchiveSurvivalItem.Tranquilizer):session.RequestUseEquippedItem();
        }
        bool CarriesTopicProp()=>session.Ready&&Array.Exists(session.State.stations,s=>s.carries.Exists(c=>c.actor==session.LocalActor));
        bool CanAimEquippedGun()=>Player.LocalInstance!=null&&(Camera.main==null||Vector3.Dot(Camera.main.transform.forward,Player.LocalInstance.transform.forward)>.15f);
        bool SelectedActionReady(out string caption,out string help)
        {
            int actor=session.LocalActor,selected=SelectedHotbarSlot;
            caption="USE ITEM";help="Tap an item to equip it; tap it again to put it away.";
            if(selected<0||selected>=6)return false;
            var item=(ArchiveSurvivalItem)selected;var inventory=session.Inventory(actor);
            if(inventory==null||session.CountItem(actor,item)<=0)return false;
            bool ready=true;
            switch(item)
            {
                case ArchiveSurvivalItem.Flashlight:
                    bool lit=inventory.flashlightOn;float remaining=session.FlashlightRemaining(actor);
                    caption=lit?"TURN OFF\nFLASHLIGHT":"TURN ON\nFLASHLIGHT";
                    help="Turn your character toward the area to light. Charge: "+ChargeClock(remaining)+" / 04:00. One battery lasts four minutes.";
                    if(!lit&&remaining<=0){ready=false;help="Battery empty. Equip a battery, then tap LOAD BATTERY.";}
                    break;
                case ArchiveSurvivalItem.Battery:
                    caption="LOAD\nBATTERY";help="Loads up to four minutes of flashlight charge. Uses one battery.";
                    if(session.CountItem(actor,ArchiveSurvivalItem.Flashlight)<=0){ready=false;help="Find a flashlight first. Keep this battery for later.";}
                    else if(session.FlashlightRemaining(actor)>=ArchiveTopicSession.FlashlightCapacity-1){ready=false;help="Flashlight charge is full. This battery will not be wasted.";}
                    break;
                case ArchiveSurvivalItem.Medkit:
                    caption="HEAL\n+1 HEART";
                    int hearts=session.hunting!=null&&session.hunting.health!=null?session.hunting.health.ActorHearts(actor):0;
                    int maximum=session.hunting!=null&&session.hunting.health!=null?session.hunting.health.MaximumHealth:0;
                    help="Restore one of your missing hearts. Hearts: "+hearts+" / "+maximum+".";
                    if(hearts<=0||hearts>=maximum){ready=false;help=hearts<=0?"A teammate must use a Revival Kit to rescue you.":"Your hearts are full. Keep this medkit for later.";}
                    break;
                case ArchiveSurvivalItem.RevivalKit:
                    caption="REVIVE\nTEAMMATE";help="Bring this kit beside a downed teammate at the Monster Nest.";
                    ArchiveDownedActor nearest=null;float nearestDistance=float.MaxValue;
                    var player=Player.LocalInstance;
                    if(player!=null)foreach(var fallen in session.State.survival.downed)
                    {
                        if(fallen==null||fallen.actor==actor||!session.IsRevivalTargetAvailable(fallen.actor))continue;
                        float distance=Vector3.Distance(player.transform.position,fallen.anchor);
                        if(distance<nearestDistance){nearest=fallen;nearestDistance=distance;}
                    }
                    if(nearest==null){ready=false;help="No teammate is ready for rescue at the nest. Keep this kit.";}
                    else if(nearestDistance>ArchiveTopicSession.RescueUseDistance){ready=false;help="Move beside "+session.ActorName(nearest.actor)+" at the Monster Nest to revive them.";}
                    else help="Revive "+session.ActorName(nearest.actor)+" with one heart. Uses one Revival Kit.";
                    break;
                case ArchiveSurvivalItem.Tranquilizer:
                    caption="FIRE\nTRANQUILIZER";help="Aim at a monster, then tap FIRE. Each shot uses one dart.";
                    if(session.CountItem(actor,ArchiveSurvivalItem.TranquilizerAmmo)<=0){ready=false;help="No darts left. Find a tranquilizer ammo drop.";}
                    else if(!CanAimEquippedGun()){ready=false;caption="TURN TO\nAIM";help="Drag the camera behind your character to aim, then tap FIRE.";}
                    break;
                case ArchiveSurvivalItem.TranquilizerAmmo:
                    caption="EQUIP\nTRANQUILIZER";help="Ammo loads automatically. Equip the tranquilizer gun, then tap FIRE to use one dart.";
                    if(session.CountItem(actor,ArchiveSurvivalItem.Tranquilizer)<=0){ready=false;caption="NEED\nTRANQUILIZER";help="This ammo needs a tranquilizer gun. Keep it until you find one.";}
                    break;
            }
            if(CarriesTopicProp()){ready=false;help="Put down the topic puzzle item before using survival supplies.";}
            if(ready&&item!=ArchiveSurvivalItem.TranquilizerAmmo&&ArchiveTopicSession.Clock<inventory.nextUseAt)
            {ready=false;help="Ready in a moment. Your last item action is still finishing.";}
            return ready;
        }
        void RefreshHotbar(bool canUse,bool reading)
        {
            canUse=canUse&&HotbarControlsAllowed();
            int selected=SelectedHotbarSlot,total=0,visibleSlots=0;
            for(int i=0;i<6;i++)if(session.CountItem(session.LocalActor,(ArchiveSurvivalItem)i)>0)visibleSlots++;
            int visibleIndex=0;
            for(int i=0;i<6;i++)
            {
                int count=session.CountItem(session.LocalActor,(ArchiveSurvivalItem)i);total+=count;bool owned=count>0;
                itemButtons[i].gameObject.SetActive(owned);
                if(owned)
                {
                    // Center the remaining items without stretching them or changing their
                    // item identities. The displayed number still matches its keyboard key.
                    float column=(6-visibleSlots)*.5f+visibleIndex++;
                    var rect=(RectTransform)itemButtons[i].transform;
                    rect.anchorMin=new Vector2(column/6f,0);rect.anchorMax=new Vector2((column+1)/6f,1);
                    rect.offsetMin=new Vector2(4,0);rect.offsetMax=new Vector2(-4,0);
                }
                slotFaces[i].color=Color.white;slotSelections[i].gameObject.SetActive(owned&&selected==i);
                itemButtons[i].interactable=canUse&&owned;slotIcons[i].gameObject.SetActive(owned&&slotIcons[i].sprite!=null);
                itemTexts[i].text=owned?(i==0||i==4?"":("x"+count)):"";
                if(owned&&i==0)itemTexts[i].text=ChargeClock(session.FlashlightRemaining(session.LocalActor));
                if(owned&&i==4)itemTexts[i].text=session.CountItem(session.LocalActor,ArchiveSurvivalItem.TranquilizerAmmo)+" darts";
            }
            pack.gameObject.SetActive(canUse&&!reading&&total>0);
            packTitle.text=selected<0?"SURVIVAL ITEMS":ItemName((ArchiveSurvivalItem)selected)+(selected==0?" · "+(session.Inventory(session.LocalActor).flashlightOn?"ON":"OFF"):"");
            bool ready=SelectedActionReady(out var caption,out var help);info.text=caption;itemHelp.text=help;
            mobileAction.gameObject.SetActive(canUse&&!reading&&selected>=0);useEquipped.interactable=canUse&&ready;
            for(int i=0;i<actionGlyphs.Length;i++)actionGlyphs[i].gameObject.SetActive(i==selected);
            if(flashlightRays!=null)flashlightRays.gameObject.SetActive(selected==0&&session.Inventory(session.LocalActor).flashlightOn);
            var glyphColor=ready?Color.white:new Color(.72f,.77f,.78f,.60f);
            if(glyphImages!=null)foreach(var image in glyphImages)if(image!=useEquipped.image)image.color=glyphColor;
            info.color=ready?Color.white:new Color(.72f,.77f,.78f,.8f);
            crosshair.gameObject.SetActive(canUse&&!reading&&selected==(int)ArchiveSurvivalItem.Tranquilizer&&CanAimEquippedGun()&&!CarriesTopicProp());
            RefreshTeamItemActions(canUse&&!reading);
        }
        void TickHotbarInput()
        {
            if(!HotbarControlsAllowed())return;
            var keyboard=Keyboard.current;if(keyboard==null)return;
            var keys=new[]{keyboard.digit1Key,keyboard.digit2Key,keyboard.digit3Key,keyboard.digit4Key,keyboard.digit5Key,keyboard.digit6Key};
            for(int i=0;i<keys.Length;i++)if(keys[i].wasPressedThisFrame)SelectHotbarSlot(i);
            if(keyboard.fKey.wasPressedThisFrame)UseSelectedHotbarItem();
        }
        void PlaceHotbar()
        {
            float scale=Mathf.Max(.01f,canvas.scaleFactor);pack.sizeDelta=new Vector2(Mathf.Min(600,safe.width/scale*.34f),HotbarHeight);
            // Keep the six slots at their previous bottom edge; the instructions grow upward.
            var point=new Vector2(safe.xMin+safe.width*.48f,safe.yMin+safe.height*.175f+(HotbarHeight-174)*.5f*scale);
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(root,point,null,out var position))pack.anchoredPosition=position;
            PlaceMobileItemAction(scale);
            PlaceTeamItemActions(scale);
        }
        UnityEngine.Rect MobileControlBounds(RectTransform control)
        {
            control.GetWorldCorners(controlCorners);
            var canvas=control.GetComponentInParent<Canvas>();var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var a=RectTransformUtility.WorldToScreenPoint(camera,controlCorners[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,controlCorners[2]);
            return UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
        }
        void PlaceMobileItemAction(float scale)
        {
            if(mobileAction==null)return;
            float diameter=Mathf.Min(220*scale,safe.height*.22f);
            Vector2 center=new Vector2(safe.xMin+safe.width*.88f,safe.yMin+safe.height*.15f);
            if(handControl!=null)
            {
                var hand=MobileControlBounds(handControl);
                diameter=Mathf.Min(hand.height*.95f,safe.height*.22f);
                center=new Vector2(hand.xMax+diameter*.5f+24*scale,hand.center.y);
            }
            diameter=Mathf.Max(120*scale,diameter);
            center.x=Mathf.Clamp(center.x,safe.xMin+diameter*.5f+16*scale,safe.xMax-diameter*.5f-16*scale);
            center.y=Mathf.Max(center.y,safe.yMin+diameter*.5f+48*scale);
            if(jumpControl!=null)
            {
                var jump=MobileControlBounds(jumpControl);
                var proposed=new UnityEngine.Rect(center-Vector2.one*diameter*.5f,Vector2.one*diameter);
                if(proposed.Overlaps(jump))center.y=jump.yMin-diameter*.5f-20*scale;
            }
            mobileAction.sizeDelta=Vector2.one*(diameter/scale);
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(root,center,null,out var position))mobileAction.anchoredPosition=position;
        }
    }

    // A hollow, rounded stroke preserves the painted slot and never covers its contents.
    [RequireComponent(typeof(CanvasRenderer))]
    sealed class ArchiveHotbarSelectionBorder : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();
            Ring(mesh,r,22,3.5f,new Color(1,.84f,.35f,1));
            r.xMin+=3.5f;r.xMax-=3.5f;r.yMin+=3.5f;r.yMax-=3.5f;
            Ring(mesh,r,18.5f,1.25f,new Color(.13f,.71f,.65f,1));
        }
        static void Ring(UnityEngine.UI.VertexHelper mesh,UnityEngine.Rect rect,float radius,float width,Color tint)
        {
            if(rect.width<=width*2||rect.height<=width*2)return;
            radius=Mathf.Min(radius,Mathf.Min(rect.width,rect.height)*.5f);
            int start=mesh.currentVertCount;const int arcSteps=6,points=4*(arcSteps+1);
            for(int corner=0;corner<4;corner++)for(int step=0;step<=arcSteps;step++)
            {
                var center=new Vector2(corner==0||corner==3?rect.xMax-radius:rect.xMin+radius,
                    corner<2?rect.yMax-radius:rect.yMin+radius);
                float angle=(corner*90f+step*90f/arcSteps)*Mathf.Deg2Rad;
                var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                mesh.AddVert(center+direction*radius,tint,Vector2.zero);
                mesh.AddVert(center+direction*Mathf.Max(0,radius-width),tint,Vector2.zero);
            }
            for(int i=0;i<points;i++)
            {
                int a=start+i*2,b=start+((i+1)%points)*2;
                mesh.AddTriangle(a,b,a+1);mesh.AddTriangle(a+1,b,b+1);
            }
        }
    }
}
