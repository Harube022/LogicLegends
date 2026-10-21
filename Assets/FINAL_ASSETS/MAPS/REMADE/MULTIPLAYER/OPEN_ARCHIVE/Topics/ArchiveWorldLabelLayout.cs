using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Limits caption size without camera-dependent hiding or screen-space relocation.</summary>
    [DefaultExecutionOrder(12000)]
    public sealed class ArchiveWorldLabelLayout : MonoBehaviour
    {
        Canvas canvas;
        void Awake(){canvas=GetComponent<Canvas>();}
        void LateUpdate()
        {
            var camera=Camera.main;if(camera==null)return;
            float uiScale=canvas!=null?canvas.scaleFactor:Screen.height/1080f;
            var viewport=new Rect(0,0,Screen.width,Screen.height);
            foreach(var label in ArchiveWorldLabel.All)
            {
                if(label==null)continue;
                var t=label.transform;t.localPosition=label.home;
                // Signs stay attached to their backings. Camera frustum and depth testing handle clipping.
                if(label.floating)
                {
                    float depth=Vector3.Dot(t.position-camera.transform.position,camera.transform.forward);
                    float width=label.size.x*Mathf.Abs(t.parent!=null?t.parent.lossyScale.x:1)*label.originalScale.x;
                    float pixels=width*Screen.height/(2*Mathf.Max(camera.nearClipPlane,depth)*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f));
                    float target=Mathf.Min(1,230*uiScale/Mathf.Max(1,pixels));
                    label.scale=Mathf.Lerp(label.scale,target,1-Mathf.Exp(-12*Time.unscaledDeltaTime));
                    t.localScale=label.originalScale*label.scale;
                }
                else t.localScale=label.originalScale;
                label.ScreenBounds=Bounds(camera,t,label.size);
                label.IsVisible=camera.WorldToViewportPoint(t.position).z>0&&viewport.Overlaps(label.ScreenBounds);
            }
        }
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
