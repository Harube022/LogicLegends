using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTeamCoordinationUI
    {
        TMP_Text signalSource;
        ArchiveSignalIconGraphic toastIcon;
        RectTransform toastLife;
        CanvasGroup toastFade;
        float toastStarted,toastDuration;
        bool toastIsNotice;

        void BuildSignalToast()
        {
            toastCard=Fixed("Incoming_Team_Signal",objectiveCard,new Vector2(.5f,0),new Vector2(.5f,1),new Vector2(344,66));
            toastCard.anchoredPosition=new Vector2(0,-7);
            var face=toastCard.gameObject.AddComponent<Image>();face.sprite=StageUITheme.ButtonArt;face.type=Image.Type.Sliced;face.pixelsPerUnitMultiplier=5;face.raycastTarget=false;
            toastFade=toastCard.gameObject.AddComponent<CanvasGroup>();toastFade.interactable=false;toastFade.blocksRaycasts=false;
            var badge=Fixed("Signal_Emblem",toastCard,new Vector2(.115f,.54f),new Vector2(.5f,.5f),new Vector2(44,44));
            var rim=badge.gameObject.AddComponent<ArchiveSignalSectorGraphic>();rim.raycastTarget=false;
            toastIcon=Icon("Signal_Icon",badge,ArchiveSignalIcon.Help,Vector2.zero,new Vector2(27,27),Cream);
            toast=Text("Signal_Message",toastCard,22,true,Cream);
            signalSource=Text("Signal_Sender",toastCard,16,false,new Color(.77f,.86f,.79f));
            var track=Rect("Signal_Lifetime",toastCard,new Vector2(.235f,.115f),new Vector2(.93f,.14f));
            var trackFace=track.gameObject.AddComponent<Image>();trackFace.color=new Color(1,.91f,.64f,.12f);trackFace.raycastTarget=false;
            toastLife=Rect("Remaining",track,Vector2.zero,Vector2.one);
            var life=toastLife.gameObject.AddComponent<Image>();life.raycastTarget=false;
        }

        // Only formats the existing accepted receipt. No new ping state or network command.
        void ShowTeamSignal(ArchiveTeamPing ping)
        {
            string title,detail=session.ActorName(ping.actor);ArchiveSignalIcon icon;Color accent;
            switch(ping.kind)
            {
                case ArchiveTeamPingKind.NeedMedkit:
                    title="NEED MEDKIT";icon=ArchiveSignalIcon.Medkit;accent=new Color(.50f,.94f,.72f);break;
                case ArchiveTeamPingKind.ReviveTeammate:
                    title="REVIVE AT NEST";detail=session.ActorName(ping.targetActor);icon=ArchiveSignalIcon.Revive;accent=new Color(1,.61f,.59f);break;
                case ArchiveTeamPingKind.GoToPortal:
                    title="TO THE PORTAL";icon=ArchiveSignalIcon.Portal;accent=new Color(.49f,.86f,1);break;
                default:
                    title="HELP HERE";if(ping.topic>=0)detail+=" · T"+(ping.topic+1);icon=ArchiveSignalIcon.Help;accent=new Color(1,.81f,.40f);break;
            }
            float remaining=Mathf.Min(5,Mathf.Max(0,(float)(ping.createdAt+ArchiveTopicSession.TeamPingLifetimeSeconds-ArchiveTopicSession.Clock)));
            BeginSignalToast(title,detail,icon,accent,session.TeamPingText(ping),remaining,false);
        }

        void ShowSignalNotice(string title,string detail,float seconds)
        {BeginSignalToast(title,detail,ArchiveSignalIcon.Signal,Cream,detail,seconds,true);}

        void BeginSignalToast(string title,string detail,ArchiveSignalIcon icon,Color accent,string message,float seconds,bool notice)
        {
            toastValue=message;toastStarted=Time.unscaledTime;toastDuration=seconds;toastUntil=toastStarted+seconds;toastIsNotice=notice;
            toast.text=title;signalSource.text=detail;toastIcon.icon=icon;toastIcon.color=accent;toastIcon.SetVerticesDirty();toast.color=accent;
            toastLife.GetComponent<Image>().color=accent;PlaceSignalToast();
        }

        void PlaceSignalToast()
        {
            if(toastCard==null)return;
            toastCard.sizeDelta=new Vector2(Mathf.Min(344,Mathf.Max(0,safe.width/Mathf.Max(.01f,scale)-28)),toastIsNotice?104:66);
            Stretch(toast.rectTransform,new Vector2(.235f,toastIsNotice?.67f:.49f),new Vector2(.94f,.91f));
            Stretch(signalSource.rectTransform,new Vector2(.235f,.19f),new Vector2(.94f,toastIsNotice?.69f:.53f));
            signalSource.textWrappingMode=toastIsNotice?TextWrappingModes.Normal:TextWrappingModes.NoWrap;
            signalSource.fontSize=signalSource.fontSizeMax=toastIsNotice?15:16;signalSource.fontSizeMin=toastIsNotice?13:14;
        }

        void RefreshSignalToast()
        {
            float remaining=toastUntil-Time.unscaledTime;
            bool visible=remaining>0&&!string.IsNullOrEmpty(toastValue);toastCard.gameObject.SetActive(visible);if(!visible)return;
            float age=Time.unscaledTime-toastStarted,enter=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.16f));
            toastFade.alpha=enter*Mathf.Clamp01(remaining/.24f);
            toastCard.anchoredPosition=new Vector2(0,-7-6*(1-enter));
            toastIcon.rectTransform.localScale=Vector3.one*(1+.12f*Mathf.Sin(Mathf.PI*Mathf.Clamp01(age/.5f)));
            toastLife.anchorMax=new Vector2(Mathf.Clamp01(remaining/Mathf.Max(.01f,toastDuration)),1);
        }
    }
}
