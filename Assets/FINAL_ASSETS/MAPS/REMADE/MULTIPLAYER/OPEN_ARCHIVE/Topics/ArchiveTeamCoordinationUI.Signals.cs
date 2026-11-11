using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTeamCoordinationUI
    {
        TMP_Text teamCountCaption,wheelCenter;
        ArchiveSignalIconGraphic teamChevron;
        ArchiveSignalIconGraphic pingDockIcon;
        ArchiveSignalSectorGraphic pingMedallion;
        ArchiveHoldSignalInput signalHold;
        readonly ArchiveSignalSectorGraphic[] sectors=new ArchiveSignalSectorGraphic[4];
        readonly ArchiveSignalIconGraphic[] signalIcons=new ArchiveSignalIconGraphic[4];
        CanvasGroup wheelFade;
        Vector2 wheelOrigin;
        float wheelScale=1,wheelOpenedAt,holdProgress;
        int selectedSignal=-1;
        public bool CanOpenSignalWheel=>isActiveAndEnabled&&Visible&&!SuppressHud();
        public bool SignalHoldActive=>signalHold!=null&&signalHold.Holding;
        public int SelectedSignal=>selectedSignal;
        public RectTransform SignalWheel=>pingMenu;

        void StyleTeamDock()
        {
            teamCaption.gameObject.SetActive(false);teamToggle.GetComponent<Image>().enabled=false;
            var badge=Fixed("Party_Emblem",teamToggle.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(80,80));
            var face=badge.gameObject.AddComponent<ArchiveSignalSectorGraphic>();teamToggle.targetGraphic=face;
            Icon("Party_Icon",badge,ArchiveSignalIcon.Team,new Vector2(-3,10),new Vector2(35,35),Cream);
            teamCountCaption=Text("Party_Count",badge,19,false,Cream);teamCountCaption.fontStyle=FontStyles.Bold;
            Stretch(teamCountCaption.rectTransform,new Vector2(.14f,.14f),new Vector2(.86f,.40f));teamCountCaption.alignment=TextAlignmentOptions.Center;
            teamChevron=Icon("Roster_Arrow",badge,ArchiveSignalIcon.Chevron,new Vector2(.78f,.64f),new Vector2(10,10),Cream,true);

            pingToggle.GetComponent<Image>().enabled=false;
            var ring=Fixed("Signal_Medallion",pingToggle.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(80,80));
            pingMedallion=ring.gameObject.AddComponent<ArchiveSignalSectorGraphic>();pingToggle.targetGraphic=pingMedallion;
            pingDockIcon=Icon("Signal_Icon",ring,ArchiveSignalIcon.Signal,Vector2.zero,new Vector2(37,37),Cream);
            pingCaption.transform.SetAsLastSibling();Stretch(pingCaption.rectTransform,new Vector2(.18f,.14f),new Vector2(.82f,.43f));
            pingCaption.rectTransform.offsetMin=pingCaption.rectTransform.offsetMax=Vector2.zero;
            pingCaption.fontSize=pingCaption.fontSizeMax=17;pingCaption.fontSizeMin=15;
            signalHold=pingToggle.gameObject.AddComponent<ArchiveHoldSignalInput>();signalHold.owner=this;
        }

        ArchiveSignalIconGraphic Icon(string name,Transform parent,ArchiveSignalIcon kind,Vector2 position,Vector2 size,Color tint,bool normalized=false)
        {
            var rect=Fixed(name,parent,normalized?position:new Vector2(.5f,.5f),new Vector2(.5f,.5f),size);
            if(!normalized)rect.anchoredPosition=position;
            var graphic=rect.gameObject.AddComponent<ArchiveSignalIconGraphic>();graphic.icon=kind;graphic.color=tint;graphic.raycastTarget=false;return graphic;
        }

        void BuildSignalWheel()
        {
            pingMenu=Fixed("Quick_Signal_Menu",root,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.one*400);
            wheelFade=pingMenu.gameObject.AddComponent<CanvasGroup>();
            string[] captions={"HELP HERE","NEED MEDKIT","REVIVE TEAMMATE","GO TO PORTAL"};
            for(int i=0;i<4;i++)
            {
                int choice=i;float angle=90-i*90;var direction=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
                var r=Rect("Ping_"+((ArchiveTeamPingKind)i),pingMenu,Vector2.zero,Vector2.one);
                var graphic=r.gameObject.AddComponent<ArchiveSignalSectorGraphic>();graphic.startAngle=angle-42;graphic.arc=84;graphic.innerRadius=.35f;sectors[i]=graphic;
                var b=r.gameObject.AddComponent<Button>();b.targetGraphic=graphic;b.transition=Selectable.Transition.None;b.navigation=new Navigation{mode=Navigation.Mode.None};
                b.onClick.AddListener(()=>SendPing((ArchiveTeamPingKind)choice));pingButtons[i]=b;
                signalIcons[i]=Icon("Signal_Icon",r,(ArchiveSignalIcon)((int)ArchiveSignalIcon.Help+i),direction*132+Vector2.up*17,new Vector2(34,34),Cream);
                var label=Text("Signal_Name",r,20,true,Cream);pingButtonCaptions[i]=label;
                var rt=label.rectTransform;rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.pivot=new Vector2(.5f,.5f);
                rt.sizeDelta=new Vector2(i%2==0?148:100,43);rt.anchoredPosition=direction*132-Vector2.up*21;
                label.text=captions[i];label.textWrappingMode=TextWrappingModes.Normal;label.alignment=TextAlignmentOptions.Center;label.fontSizeMin=17;
            }
            var center=Fixed("Cancel_Signal",pingMenu,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.one*128);
            var centerFace=center.gameObject.AddComponent<ArchiveSignalSectorGraphic>();
            var cancel=center.gameObject.AddComponent<Button>();cancel.targetGraphic=centerFace;cancel.navigation=new Navigation{mode=Navigation.Mode.None};cancel.onClick.AddListener(CancelSignalInteraction);
            wheelCenter=Text("Selection_Or_Cancel",center,18,true,Cream);Stretch(wheelCenter.rectTransform,new Vector2(.11f,.16f),new Vector2(.89f,.84f));
            wheelCenter.alignment=TextAlignmentOptions.Center;wheelCenter.textWrappingMode=TextWrappingModes.Normal;
            var footer=Fixed("Signal_Instructions",pingMenu,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(400,72));footer.anchoredPosition=new Vector2(0,-242);
            var face=footer.gameObject.AddComponent<Image>();face.sprite=StageUITheme.ButtonArt;face.type=Image.Type.Sliced;face.pixelsPerUnitMultiplier=5;face.raycastTarget=false;
            pingHint=Text("Signal_Cooldown_And_Feedback",footer,17,false,Cream);Stretch(pingHint.rectTransform,new Vector2(.07f,.14f),new Vector2(.93f,.86f));
            pingHint.textWrappingMode=TextWrappingModes.Normal;pingHint.alignment=TextAlignmentOptions.Center;pingHint.fontSizeMin=14;
        }

        public bool OpenSignalWheel(Vector2 screenPoint)
        {
            if(!CanOpenSignalWheel)return false;
            wheelOrigin=screenPoint;selectedSignal=-1;partyOpen=false;pingOpen=true;wheelOpenedAt=Time.unscaledTime;
            RefreshPanels();pingMenu.SetAsLastSibling();PlaceSignalWheel();return true;
        }
        void PlaceSignalWheel()
        {
            if(pingMenu==null||!pingOpen)return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root,new Vector2(safe.xMin,safe.yMin),null,out var low);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root,new Vector2(safe.xMax,safe.yMax),null,out var high);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root,wheelOrigin,null,out var point);
            wheelScale=Mathf.Min(1,(high.x-low.x-24)/404,(high.y-low.y-24)/484);
            point.x=Mathf.Clamp(point.x,low.x+202*wheelScale,high.x-202*wheelScale);
            point.y=Mathf.Clamp(point.y,low.y+282*wheelScale,high.y-202*wheelScale);
            pingMenu.position=root.TransformPoint(point);pingMenu.localScale=Vector3.one*wheelScale;
        }
        public static int PickSignal(Vector2 delta)
        {
            if(delta.magnitude<70||delta.magnitude>270)return -1;
            float angle=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
            return (Mathf.RoundToInt((90-angle)/90)%4+4)%4;
        }
        public void PointSignal(Vector2 screenPoint)
        {
            if(!pingOpen)return;
            var delta=(screenPoint-wheelOrigin)/Mathf.Max(.01f,canvas.scaleFactor*wheelScale);
            selectedSignal=PickSignal(delta);
        }
        public void ReleaseSignal(Vector2 screenPoint)
        {
            if(!pingOpen)return;
            PointSignal(screenPoint);int selected=selectedSignal;CloseSignalWheel();
            if(selected>=0)SendPing((ArchiveTeamPingKind)selected);
        }
        public void CloseSignalWheel(){pingOpen=false;selectedSignal=-1;RefreshPanels();}
        public void CancelSignalInteraction(){if(signalHold!=null)signalHold.Abort();else CloseSignalWheel();}
        public void SignalHoldProgress(float amount){holdProgress=amount;}
        public void HoldSignalHint(){ShowSignalNotice("HOLD TO PING","Drag to a signal, then release.",2);}

        void UpdateSignalWheelVisuals()
        {
            if(pingMedallion==null)return;
            pingMedallion.State(SignalHoldActive,session.Ready&&session.TeamPingCooldownRemaining(session.LocalActor)<=0,holdProgress);
            teamChevron.rectTransform.localRotation=Quaternion.Euler(0,0,partyOpen?180:0);
            if(!pingOpen)return;
            float appear=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-wheelOpenedAt)/.13f));
            wheelFade.alpha=appear;pingMenu.localScale=Vector3.one*(wheelScale*Mathf.Lerp(.92f,1,appear));
            for(int i=0;i<4;i++)
            {
                bool available=pingButtons[i].interactable;
                sectors[i].State(selectedSignal==i,available);
                var tint=available?(selectedSignal==i?new Color(1,.83f,.42f):Cream):new Color(.40f,.49f,.48f);
                pingButtonCaptions[i].color=tint;signalIcons[i].color=tint;
            }
            if(selectedSignal<0)wheelCenter.text="DRAG TO\nSELECT\n\nCANCEL";
            else
            {
                bool allowed=session.CanTeamPing((ArchiveTeamPingKind)selectedSignal,out var reason);
                wheelCenter.text=allowed?"RELEASE\nTO SEND":"NOT READY";
                if(!allowed)pingHint.text=reason;
            }
        }
    }
}
