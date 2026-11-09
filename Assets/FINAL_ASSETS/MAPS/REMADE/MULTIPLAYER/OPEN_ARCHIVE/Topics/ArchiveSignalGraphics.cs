using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Small code-drawn fantasy chrome; no generated textures or extra materials.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArchiveSignalSectorGraphic : MaskableGraphic
    {
        public float startAngle,arc=360,innerRadius;
        bool selected,available=true;
        float progress;
        public void State(bool highlight,bool enabled,float hold=0)
        {if(selected==highlight&&available==enabled&&Mathf.Approximately(progress,hold))return;selected=highlight;available=enabled;progress=hold;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();float radius=Mathf.Min(r.width,r.height)*.5f;
            var wood=new Color(.17f,.10f,.055f);var gold=selected?new Color(1,.79f,.36f):new Color(.69f,.49f,.24f);
            var fill=!available?new Color(.10f,.14f,.15f):selected?new Color(.08f,.40f,.39f):new Color(.035f,.17f,.19f);
            bool circle=arc>350;
            Ring(vh,r.center,radius*innerRadius,radius,startAngle,arc,wood);
            Ring(vh,r.center,radius*(innerRadius>0?innerRadius+.008f:0),radius*.977f,startAngle+(circle?0:1),Mathf.Max(0,arc-(circle?0:2)),gold);
            Ring(vh,r.center,radius*(innerRadius>0?innerRadius+.027f:0),radius*.95f,startAngle+(circle?0:1.8f),Mathf.Max(0,arc-(circle?0:3.6f)),fill);
            if(arc>350)
            {
                Ring(vh,r.center,radius*.82f,radius*.835f,0,360,new Color(.27f,.46f,.43f));
                if(progress>0)Ring(vh,r.center,radius*.92f,radius*.973f,90,progress*360,new Color(1,.84f,.4f));
            }
        }
        void Ring(VertexHelper vh,Vector2 center,float inside,float outside,float angle,float degrees,Color tint)
        {
            int count=Mathf.Max(2,Mathf.CeilToInt(degrees/5));
            for(int i=0;i<count;i++)
            {
                float a=(angle+degrees*i/count)*Mathf.Deg2Rad,b=(angle+degrees*(i+1)/count)*Mathf.Deg2Rad;
                var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var q=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                int n=vh.currentVertCount;var c=tint*color;
                vh.AddVert(center+p*inside,c,Vector2.zero);vh.AddVert(center+p*outside,c,Vector2.zero);
                vh.AddVert(center+q*outside,c,Vector2.zero);vh.AddVert(center+q*inside,c,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
        public override bool Raycast(Vector2 screenPoint,Camera eventCamera)
        {
            if(!base.Raycast(screenPoint,eventCamera)||!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screenPoint,eventCamera,out var p))return false;
            var r=rectTransform.rect;p-=r.center;float radius=Mathf.Min(r.width,r.height)*.5f;
            float length=p.magnitude/Mathf.Max(1,radius);if(length<innerRadius||length>1)return false;
            if(arc>350)return true;
            float angle=Mathf.Repeat(Mathf.Atan2(p.y,p.x)*Mathf.Rad2Deg-startAngle,360);return angle<=arc;
        }
    }

    public enum ArchiveSignalIcon { Team,Signal,Help,Medkit,Revive,Portal,Chevron,Bars }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArchiveSignalIconGraphic : MaskableGraphic
    {
        public ArchiveSignalIcon icon;
        public int bars=3;
        VertexHelper mesh;Rect bounds;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();mesh=vh;bounds=GetPixelAdjustedRect();
            switch(icon)
            {
                case ArchiveSignalIcon.Team:
                    Disc(.35f,.70f,.14f);Disc(.72f,.65f,.115f);Line(.11f,.20f,.13f,.38f,.095f);Line(.13f,.38f,.35f,.48f,.095f);Line(.35f,.48f,.57f,.38f,.095f);Line(.57f,.38f,.59f,.20f,.095f);Line(.60f,.43f,.73f,.46f,.085f);Line(.73f,.46f,.91f,.35f,.085f);Line(.91f,.35f,.92f,.20f,.085f);break;
                case ArchiveSignalIcon.Signal:
                    Arc(.50f,.61f,.26f,.055f,0,360);Triangle(.31f,.48f,.69f,.48f,.50f,.07f);Disc(.50f,.61f,.075f);break;
                case ArchiveSignalIcon.Help:
                    Line(.5f,.79f,.5f,.37f,.12f);Disc(.5f,.19f,.066f);Line(.17f,.5f,.28f,.5f,.045f);Line(.72f,.5f,.83f,.5f,.045f);break;
                case ArchiveSignalIcon.Medkit:
                    Box(.12f,.15f,.88f,.75f,.075f);Box(.34f,.75f,.66f,.88f,.055f);Line(.5f,.31f,.5f,.60f,.095f);Line(.35f,.455f,.65f,.455f,.095f);break;
                case ArchiveSignalIcon.Revive:
                    Disc(.33f,.70f,.12f);Line(.33f,.54f,.33f,.25f,.10f);Line(.12f,.39f,.50f,.39f,.09f);Line(.33f,.25f,.16f,.10f,.085f);Line(.33f,.25f,.48f,.10f,.085f);Line(.74f,.20f,.74f,.77f,.09f);Line(.74f,.77f,.57f,.60f,.09f);Line(.74f,.77f,.91f,.60f,.09f);break;
                case ArchiveSignalIcon.Portal:
                    Arc(.45f,.49f,.33f,.065f,0,360);Line(.36f,.49f,.90f,.49f,.08f);Line(.90f,.49f,.74f,.66f,.08f);Line(.90f,.49f,.74f,.32f,.08f);break;
                case ArchiveSignalIcon.Chevron:Line(.15f,.65f,.5f,.30f,.12f);Line(.5f,.30f,.85f,.65f,.12f);break;
                default:
                    for(int i=0;i<3;i++){float x=.16f+i*.28f,y=.28f+i*.23f;Quad(x,.12f,x+.16f,y,i<bars?color:new Color(.26f,.31f,.30f));}break;
            }
        }
        Vector2 P(float x,float y)=>new Vector2(bounds.xMin+x*bounds.width,bounds.yMin+y*bounds.height);
        void Quad(float x,float y,float x2,float y2,Color tint)
        {int n=mesh.currentVertCount;mesh.AddVert(P(x,y),tint,Vector2.zero);mesh.AddVert(P(x2,y),tint,Vector2.zero);mesh.AddVert(P(x2,y2),tint,Vector2.zero);mesh.AddVert(P(x,y2),tint,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);}
        void Triangle(float x,float y,float x2,float y2,float x3,float y3)
        {int n=mesh.currentVertCount;mesh.AddVert(P(x,y),color,Vector2.zero);mesh.AddVert(P(x2,y2),color,Vector2.zero);mesh.AddVert(P(x3,y3),color,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);}
        void Line(float x,float y,float x2,float y2,float width)
        {
            var a=P(x,y);var b=P(x2,y2);var delta=b-a;var side=new Vector2(-delta.y,delta.x).normalized*Mathf.Min(bounds.width,bounds.height)*width*.5f;
            int n=mesh.currentVertCount;foreach(var p in new[]{a-side,b-side,b+side,a+side})mesh.AddVert(p,color,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
        }
        void Disc(float x,float y,float radius)=>Arc(x,y,radius,radius,0,360);
        void Box(float x,float y,float x2,float y2,float width){Line(x,y,x2,y,width);Line(x2,y,x2,y2,width);Line(x2,y2,x,y2,width);Line(x,y2,x,y,width);}
        void Arc(float x,float y,float radius,float width,float start,float span)
        {
            int count=Mathf.Max(6,Mathf.CeilToInt(span/12));var center=P(x,y);float r=Mathf.Min(bounds.width,bounds.height)*radius,inner=Mathf.Max(0,r-width*Mathf.Min(bounds.width,bounds.height));
            for(int i=0;i<count;i++){float a=(start+span*i/count)*Mathf.Deg2Rad,b=(start+span*(i+1)/count)*Mathf.Deg2Rad;var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var q=new Vector2(Mathf.Cos(b),Mathf.Sin(b));int n=mesh.currentVertCount;foreach(var v in new[]{center+p*inner,center+p*r,center+q*r,center+q*inner})mesh.AddVert(v,color,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);}
        }
    }
}
