using UnityEngine;
using TMPro;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local label scale and distance/fog fade. Never disables challenge objects or the screen HUD.</summary>
    [DefaultExecutionOrder(12000)]
    public sealed class ArchiveWorldLabelLayout : MonoBehaviour
    {
        Canvas canvas;
        float nextDiscovery;
        void Awake(){canvas=GetComponent<Canvas>();}
        void LateUpdate()
        {
            var camera=Camera.main;if(camera==null)return;
            if(Time.unscaledTime>=nextDiscovery){DiscoverLabels();nextDiscovery=Time.unscaledTime+1;}
            PresentFor(camera,Time.unscaledDeltaTime);
        }
        void DiscoverLabels()
        {
            // Includes fixed notation and dynamic labels built by T1–T10, while
            // leaving screen-space HUD, reader panels and portal VFX symbols alone.
            foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if(text.GetComponentInParent<ArchiveWorldLabel>()!=null||text.GetComponentInParent<ArchiveVictoryPortal>()!=null)continue;
                var parentCanvas=text.GetComponentInParent<Canvas>();
                if(!(text is TextMeshPro)&&(parentCanvas==null||parentCanvas.renderMode!=RenderMode.WorldSpace))continue;
                var label=text.gameObject.AddComponent<ArchiveWorldLabel>();label.distanceOnly=true;label.floating=false;label.size=text.rectTransform.rect.size;
            }
        }
        public void PresentFor(Camera camera,float deltaTime)
        {
            if(camera==null)return;
            float uiScale=canvas!=null?canvas.scaleFactor:Screen.height/1080f;
            var viewport=new Rect(0,0,Screen.width,Screen.height);
            foreach(var label in ArchiveWorldLabel.All)
            {
                if(label==null||label.gameObject.scene!=gameObject.scene)continue;
                var t=label.transform;if(!label.distanceOnly)t.localPosition=label.home;
                // Signs stay attached to their backings. Camera frustum and depth testing handle clipping.
                if(label.floating&&!label.distanceOnly)
                {
                    float depth=Vector3.Dot(t.position-camera.transform.position,camera.transform.forward);
                    float width=label.size.x*Mathf.Abs(t.parent!=null?t.parent.lossyScale.x:1)*label.originalScale.x;
                    float pixels=width*Screen.height/(2*Mathf.Max(camera.nearClipPlane,depth)*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f));
                    float target=Mathf.Min(1,230*uiScale/Mathf.Max(1,pixels));
                    label.scale=Mathf.Lerp(label.scale,target,1-Mathf.Exp(-12*Time.unscaledDeltaTime));
                    t.localScale=label.originalScale*label.scale;
                }
                else if(!label.distanceOnly)t.localScale=label.originalScale;
                label.ScreenBounds=Bounds(camera,t,label.size);
                bool inView=camera.WorldToViewportPoint(t.position).z>0&&viewport.Overlaps(label.ScreenBounds);
                float distance=Vector3.Distance(camera.transform.position,t.position);
                float targetAlpha=inView?DistanceOpacity(distance,label.fadeStart,label.hideDistance):0;
                // The normal reading range stays legible even during night fog.
                // Mist only softens text in the distant fade band; it must not
                // turn a nearby challenge board into an unreadable blank sign.
                if(RenderSettings.fog)
                {
                    float farBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(label.fadeStart,Mathf.Max(label.fadeStart+.1f,label.hideDistance),distance));
                    targetAlpha*=Mathf.Lerp(1,.55f+.45f*FogOpacity(distance,RenderSettings.fogMode,RenderSettings.fogDensity,RenderSettings.fogStartDistance,RenderSettings.fogEndDistance),farBlend);
                }
                float opacity=Mathf.MoveTowards(label.VisibilityAlpha,targetAlpha,Mathf.Max(0,deltaTime)*6);
                label.SetVisibility(opacity);label.IsVisible=inView&&opacity>.005f;
            }
        }
        public static float DistanceOpacity(float distance,float start,float end)
        {float t=Mathf.InverseLerp(start,Mathf.Max(start+.1f,end),distance);return 1-t*t*(3-2*t);}
        public static float FogOpacity(float distance,FogMode mode,float density,float start,float end)
        {
            float transmission=mode==FogMode.ExponentialSquared?Mathf.Exp(-density*density*distance*distance):mode==FogMode.Exponential?Mathf.Exp(-density*distance):1-Mathf.InverseLerp(start,Mathf.Max(start+.1f,end),distance);
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.85f,transmission));
        }
        void OnDisable(){foreach(var label in ArchiveWorldLabel.All)if(label!=null&&label.gameObject.scene==gameObject.scene)label.SetVisibility(1);}
        static Rect Bounds(Camera camera,Transform t,Vector2 size)
        {
            Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
            {
                Vector3 p=camera.WorldToScreenPoint(t.TransformPoint(new Vector3(x*size.x*.5f,y*size.y*.5f,0)));
                min=Vector2.Min(min,p);max=Vector2.Max(max,p);
            }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
