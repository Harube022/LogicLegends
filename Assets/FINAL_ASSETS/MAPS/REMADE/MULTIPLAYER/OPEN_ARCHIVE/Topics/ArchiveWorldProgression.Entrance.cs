using System.Collections.Generic;
using LogicLegends.OpenArchive.Cinematics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveWorldProgression
    {
        readonly HashSet<Player> introPlayers=new HashSet<Player>();
        Image[] warningEdges;
        RectTransform warningRoot;
        TMP_Text filmTitle,filmSubtitle;
        UnityEngine.UI.Image filmFade,filmAccent;
        double entranceToken=-2;
        public bool FullscreenVisible=>frame!=null&&frame.gameObject.activeInHierarchy;
        public bool WarningVisible=>warningRoot!=null&&warningRoot.gameObject.activeInHierarchy;
        public int IntroLocksHeld=>introPlayers.Count;
        bool ReducedEntranceMotion=>session?.hunting?.GetComponent<KhezuCinematicJumpscareController>()?.reducedMotion??true;
        static RectTransform EntranceRect(string name,Transform parent,Vector2 min,Vector2 max)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        static TMP_Text EntranceLabel(string name,Transform parent,Vector2 min,Vector2 max,int size)
        {var rect=EntranceRect(name,parent,min,max);var t=rect.gameObject.AddComponent<TextMeshProUGUI>();t.font=StageUITheme.ReadingFont;t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.color=new Color(1,.85f,.65f);t.raycastTarget=false;return t;}
        void BuildUI()
        {
            var go=new GameObject("World_Time_Cinematic",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10000;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            frame=EntranceRect("Guardian_Entrance_Fullscreen",go.transform,Vector2.zero,Vector2.one);var black=frame.gameObject.AddComponent<Image>();black.color=Color.black;
            var screen=EntranceRect("Entrance_Film",frame,Vector2.zero,Vector2.one);film=screen.gameObject.AddComponent<RawImage>();film.raycastTarget=false;
            // The render texture follows the actual device aspect. Fill this existing
            // full-screen frame instead of nesting a 16:9 window inside a wider phone.
            filmFade=EntranceRect("Reveal_Fade",frame,Vector2.zero,Vector2.one).gameObject.AddComponent<UnityEngine.UI.Image>();filmFade.color=Color.black;filmFade.raycastTarget=false;
            foreach(var range in new[]{new Vector4(0,0,1,.12f),new Vector4(0,.88f,1,1)})
            {var bar=EntranceRect("Cinematic_Letterbox",frame,new Vector2(range.x,range.y),new Vector2(range.z,range.w));bar.gameObject.AddComponent<Image>().color=new Color(.008f,.008f,.018f,.97f);}
            filmTitle=EntranceLabel("Guardian_Reveal_Title",frame,new Vector2(.08f,.90f),new Vector2(.92f,.975f),40);
            filmSubtitle=EntranceLabel("Guardian_Reveal_Subtitle",frame,new Vector2(.08f,.025f),new Vector2(.92f,.095f),25);
            filmSubtitle.text="NIGHTFALL  ·  THE SECOND GUARDIAN HAS AWAKENED";
            filmAccent=EntranceRect("Guardian_Energy_Accent",frame,new Vector2(.18f,.879f),new Vector2(.82f,.882f)).gameObject.AddComponent<UnityEngine.UI.Image>();filmAccent.raycastTarget=false;
            var status=EntranceRect("Phase_And_Warning",go.transform,new Vector2(.26f,.79f),new Vector2(.74f,.89f));
            var shade=status.gameObject.AddComponent<Image>();shade.color=new Color(.08f,.01f,.025f,.94f);shade.raycastTarget=false;
            banner=EntranceLabel("Warning_Text",status,new Vector2(.02f,.08f),new Vector2(.98f,.92f),27);
            warningRoot=EntranceRect("Night_Warning_Energy",go.transform,Vector2.zero,Vector2.one);warningRoot.SetAsFirstSibling();
            warningEdges=new Image[4];var ranges=new[]{new Vector4(0,0,.02f,1),new Vector4(.98f,0,1,1),new Vector4(0,0,1,.025f),new Vector4(0,.975f,1,1)};
            for(int i=0;i<4;i++){var r=ranges[i];warningEdges[i]=EntranceRect("Warning_Edge_"+i,warningRoot,new Vector2(r.x,r.y),new Vector2(r.z,r.w)).gameObject.AddComponent<Image>();warningEdges[i].raycastTarget=false;}
            frame.gameObject.SetActive(false);status.gameObject.SetActive(false);warningRoot.gameObject.SetActive(false);
        }
        void RefreshEntranceToken(double token)
        {
            if(entranceToken==token)return;entranceToken=token;releasedOnce=false;heardEntrance=false;entranceImpactPlayed=false;closedReader=false;primaryAwakenRequested=false;ReleaseIntroLocks();ClearEntrancePresentation();ResetFilmSubject();
        }
        void PresentIntroLocks(bool visible)
        {
            if(!visible){ReleaseIntroLocks();return;}
            foreach(int actor in session.AdminTestActors)
            {
                var player=session.ActorPlayer(actor);if(player==null||introPlayers.Contains(player)||player.IsMovementLocked)continue;
                introPlayers.Add(player);player.AcquireMovementLock(this,"Nightfall cinematic",player.transform.position,70);
            }
            filmTitle.text=SecondaryName=="Rajang"?"RAJANG  ·  THE PRIMAL AXIOM":"KHEZU  ·  THE PROOF DEVOURER";
            var elapsed=(float)(ArchiveTopicSession.Clock-entranceToken);
            var tint=EntranceTint;
            filmTitle.color=Color.Lerp(tint,Color.white,.40f);filmTitle.alpha=Mathf.Clamp01((elapsed-.25f)*1.2f);
            filmSubtitle.text=elapsed<entranceSeconds*.65f?"NIGHTFALL  ·  A SECOND GUARDIAN AWAKENS":"STAY TOGETHER  ·  PREPARE FOR THE HUNT";
            filmSubtitle.color=new Color(1,.9f,.8f,Mathf.Clamp01((elapsed-.6f)*1.5f));
            float t=Mathf.Clamp01(elapsed/Mathf.Max(.1f,entranceSeconds));
            filmFade.color=new Color(0,0,0,Mathf.Max(1-Mathf.SmoothStep(0,1,elapsed/.40f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.94f,1,t))));
            filmAccent.color=new Color(tint.r,tint.g,tint.b,Mathf.Clamp01(elapsed*2)*.8f);
            // The normal phase panel belongs after the film, never over the monster reveal.
            banner.transform.parent.gameObject.SetActive(false);
        }
        void OnDisable(){RestoreNightAtmosphere();ReleaseIntroLocks();ClearEntrancePresentation();if(frame!=null)frame.gameObject.SetActive(false);if(warningRoot!=null)warningRoot.gameObject.SetActive(false);if(filmCamera!=null)filmCamera.enabled=false;}
        void ReleaseIntroLocks(){foreach(var p in introPlayers)if(p!=null)p.ReleaseMovementLock(this);introPlayers.Clear();}
        void UpdateWarningFx(bool active,float elapsed)
        {
            if(FullscreenVisible)banner.transform.parent.gameObject.SetActive(false);
            warningRoot.gameObject.SetActive(active);if(!active)return;
            float pulse=ReducedEntranceMotion?.3f:.25f+.20f*(.5f+.5f*Mathf.Sin(elapsed*6));
            var color=SecondaryName=="Rajang"?new Color(1,.35f,.04f,pulse):new Color(.95f,.04f,.12f,pulse);
            foreach(var edge in warningEdges)edge.color=color;
        }
    }
}
