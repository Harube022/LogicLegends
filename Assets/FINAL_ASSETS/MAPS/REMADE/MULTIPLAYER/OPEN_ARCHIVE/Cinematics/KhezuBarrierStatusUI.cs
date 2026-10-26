using LogicLegends.OpenArchive.Hunting;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Cinematics
{
    /// <summary>Owner-only presentation of the existing encounter clock. Retained in release builds.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(140)]
    public sealed class KhezuBarrierStatusUI : MonoBehaviour
    {
        KhezuCaptureEncounterController encounter;
        Canvas canvas;
        RectTransform safe;
        Text caption;
        Image fill;
        public bool Visible=>canvas!=null && canvas.gameObject.activeSelf;
        public string Message=>caption==null?"":caption.text;
        // Presentation only: the detailed math card reads the same encounter clock.
        [System.NonSerialized] public bool detailedChallengeVisible;
        void Awake(){
            encounter=GetComponent<KhezuCaptureEncounterController>();
            var root=new GameObject("Barrier_Challenge_Timer",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,gameObject.scene);
            canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=85;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            safe=Rect("SafeArea",root.transform);safe.offsetMin=safe.offsetMax=Vector2.zero;
            var panel=Rect("Countdown",safe);panel.anchorMin=panel.anchorMax=new Vector2(.5f,1);
            panel.pivot=new Vector2(.5f,1);panel.anchoredPosition=new Vector2(0,-104);panel.sizeDelta=new Vector2(350,76);
            var background=panel.gameObject.AddComponent<Image>();background.color=new Color(.045f,.075f,.095f,.92f);background.raycastTarget=false;
            var label=Rect("Time",panel);label.offsetMin=new Vector2(12,17);label.offsetMax=new Vector2(-12,-6);
            caption=label.gameObject.AddComponent<Text>();caption.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize=25;caption.fontStyle=FontStyle.Bold;caption.alignment=TextAnchor.MiddleCenter;caption.raycastTarget=false;
            var track=Rect("Remaining",panel);track.anchorMin=new Vector2(0,0);track.anchorMax=new Vector2(1,0);
            track.offsetMin=new Vector2(12,9);track.offsetMax=new Vector2(-12,13);
            var bar=track.gameObject.AddComponent<Image>();bar.color=new Color(.2f,.25f,.28f);bar.raycastTarget=false;
            var foreground=Rect("Fill",track);foreground.offsetMin=foreground.offsetMax=Vector2.zero;
            fill=foreground.gameObject.AddComponent<Image>();fill.raycastTarget=false;
            root.SetActive(false);
        }
        static RectTransform Rect(string name,Transform parent){
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        void Update(){
            var hunt=encounter==null?null:encounter.Hunting;
            bool captive=encounter!=null && encounter.isActiveAndEnabled && hunt!=null &&
                hunt.State==KhezuHuntState.Caught && hunt.EncounterPhase==4 && KhezuCaptureEncounterController.IsCaughtLocalPlayer(hunt.CaughtPlayerId) &&
                (encounter.cinematic==null || !encounter.cinematic.IsPlaying);
            bool stunned=encounter!=null && encounter.LocalFailureStun;
            bool show=(captive && !detailedChallengeVisible) || stunned;
            if(canvas==null)return;
            if(canvas.gameObject.activeSelf!=show)canvas.gameObject.SetActive(show);
            if(!show)return;
            var area=Screen.safeArea;safe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);
            safe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
            float fraction;
            if(stunned){caption.text="STUNNED  "+Mathf.CeilToInt(hunt.StunSecondsRemaining)+"s";fraction=hunt.StunSecondsRemaining/(float)KhezuHuntingController.FailureStunSeconds;}
            else if(hunt.BarrierResolving){caption.text=hunt.ResolvedOutcome==1?"CHALLENGE COMPLETE":"INCOMING ATTACK";fraction=0;}
            else{
                int seconds=Mathf.CeilToInt(hunt.BarrierSecondsRemaining);
                caption.text="BARRIER  "+(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
                fraction=hunt.BarrierSecondsRemaining/Mathf.Max(1,(float)(hunt.BarrierDeadline-hunt.BarrierStartedAt));
            }
            caption.color=stunned?new Color(1,.78f,.35f):hunt.BarrierResolving || hunt.BarrierSecondsRemaining<=30?new Color(1,.35f,.3f):new Color(1,.93f,.72f);
            fill.color=caption.color;fill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(fraction),1);
        }
        void OnDisable(){if(canvas!=null)canvas.gameObject.SetActive(false);}
        void OnDestroy(){if(canvas!=null)Destroy(canvas.gameObject);}
    }
}
