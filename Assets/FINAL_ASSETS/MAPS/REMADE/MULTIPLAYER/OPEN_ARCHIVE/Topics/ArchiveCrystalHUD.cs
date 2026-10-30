using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>One small non-interactive HUD for the existing session's crystal read model.</summary>
    public sealed class ArchiveCrystalHUD : MonoBehaviour
    {
        public ArchiveTopicSession session;
        Canvas canvas;RectTransform root,panel;TMP_Text title,percent,contributors,notice;Image fill;
        float next;Vector2 screen;Rect safe;
        RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();t.SetParent(parent,false);t.anchorMin=min;t.anchorMax=max;t.offsetMin=t.offsetMax=Vector2.zero;return t;}
        TMP_Text Text(string name,Vector2 min,Vector2 max,float size)
        {var r=Rect(name,panel,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=StageUITheme.ReadingFont;t.fontSize=size;t.color=new Color(1,.96f,.84f);t.raycastTarget=false;t.richText=false;return t;}
        void Start()
        {
            if(session==null)session=GetComponent<ArchiveTopicSession>();
            var go=new GameObject("Topic_Crystal_HUD",typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=105;root=go.GetComponent<RectTransform>();
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            panel=Rect("Crystal_Progress",root,new Vector2(.5f,.5f),new Vector2(.5f,.5f));panel.sizeDelta=new Vector2(370,240);
            var bg=panel.gameObject.AddComponent<Image>();bg.color=new Color(.025f,.075f,.10f,.92f);bg.raycastTarget=false;
            title=Text("Crystal_Title",new Vector2(.05f,.82f),new Vector2(.95f,.97f),22);
            percent=Text("Crystal_Percent",new Vector2(.05f,.68f),new Vector2(.95f,.83f),18);
            var bar=Rect("Progress_Track",panel,new Vector2(.05f,.61f),new Vector2(.95f,.65f));var track=bar.gameObject.AddComponent<Image>();track.color=new Color(.13f,.20f,.23f);track.raycastTarget=false;
            var f=Rect("Progress_Fill",bar,Vector2.zero,Vector2.one);fill=f.gameObject.AddComponent<Image>();fill.raycastTarget=false;
            contributors=Text("Player_Runes",new Vector2(.05f,.22f),new Vector2(.95f,.59f),17);
            notice=Text("Crystal_Status",new Vector2(.05f,.02f),new Vector2(.95f,.21f),16);
            panel.gameObject.SetActive(false);Place();
        }
        void Place()
        {
            Canvas.ForceUpdateCanvases();screen=new Vector2(Screen.width,Screen.height);safe=Screen.safeArea;float scale=Mathf.Max(.01f,canvas.scaleFactor);
            panel.sizeDelta=new Vector2(Mathf.Min(370,safe.width/scale-24),Mathf.Min(240,safe.height/scale*.4f));
            var position=new Vector2(safe.xMin+panel.rect.width*scale*.5f+20,safe.yMin+safe.height*.65f);
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(root,position,null,out var p))panel.anchoredPosition=p;
        }
        void Update()
        {
            if(panel==null||Time.unscaledTime<next)return;next=Time.unscaledTime+.2f;
            if(screen!=new Vector2(Screen.width,Screen.height)||safe!=Screen.safeArea)Place();
            int topic=session!=null&&session.Ready?session.ActiveTopic:-1;
            if(topic<0&&session!=null&&session.Ready&&Player.LocalInstance!=null)
            {
                float distance=float.MaxValue;
                foreach(var station in session.stations)
                {var pillar=station.GetComponentInChildren<ArchiveCrystalPillar>();if(pillar==null)continue;float d=Vector3.Distance(Player.LocalInstance.transform.position,pillar.transform.position);if(d<18&&d<distance){distance=d;topic=station.topic;}}
            }
            bool show=topic>=0&&session.CrystalsEnabled&&Time.timeScale>0&&Player.LocalInstance!=null&&!Player.LocalInstance.IsMovementLocked&&(ArchiveTopicUI.Current==null||ArchiveTopicUI.Current.ReadingStation==null);
            panel.gameObject.SetActive(show);if(!show)return;
            var c=session.Crystal(topic);if(c==null){panel.gameObject.SetActive(false);return;}
            var color=ArchiveCrystalPillar.Palette[topic];int value=ArchiveCrystalProgress.Percent(session.State,c);
            title.text="T"+(topic+1)+" - "+ArchiveCrystalPillar.ColorNames[topic]+" Crystal";title.color=color;
            percent.text=value+"% restored - "+(c.online?"Contributors "+ArchiveCrystalProgress.ContributorCount(session.State,c)+"/"+Mathf.Max(2,ArchiveCrystalProgress.Required(session.State,c).Length):"Solved "+ArchiveCrystalProgress.Solved(session.State,topic)+"/"+ArchiveCrystalProgress.Total(topic));
            fill.rectTransform.anchorMax=new Vector2(value/100f,1);fill.color=color;
            contributors.text=!c.started?"Team runes appear on Book activation":string.Join("\n",c.members.Select((m,i)=>
                (i+1)+". "+(m.name!=null&&m.name.Length>17?m.name.Substring(0,16)+"...":m.name)+" - "+(ArchiveCrystalProgress.Contributed(session.State,topic,m.actor)?"LIT":m.excused?"EXCUSED":m.awaitingGrace?"RECONNECTING":"NEEDS WORK")));
            notice.text=session.CrystalStatus(topic);
        }
    }
}
