using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Visuals follow the existing station phase; no interaction or progress lives here.</summary>
    public sealed class ArchiveAnimatedBook : MonoBehaviour
    {
        public Transform bookRig,coverHinge;
        public MeshFilter turningPage;
        [Min(.1f)] public float openingSeconds=.65f,pageSeconds=2.4f;
        public float hoverHeight=.38f;
        public float OpenAmount {get;private set;}
        ArchiveTopicStation station;
        Vector3 rest;
        Mesh page;
        Vector3[] vertices;
        const int Segments=12;
        void Awake()
        {
            station=GetComponentInParent<ArchiveTopicStation>();rest=bookRig.localPosition;
            page=new Mesh{name="Archive turning paper (local visual)"};page.MarkDynamic();
            vertices=new Vector3[(Segments+1)*4];var uv=new Vector2[vertices.Length];var triangles=new int[Segments*12];
            for(int side=0;side<2;side++)for(int i=0;i<=Segments;i++)for(int edge=0;edge<2;edge++)
                uv[side*(Segments+1)*2+i*2+edge]=new Vector2(i/(float)Segments,edge);
            int index=0;
            for(int side=0;side<2;side++)for(int i=0;i<Segments;i++)
            {
                int a=side*(Segments+1)*2+i*2,b=a+1,c=a+2,d=a+3;
                if(side==0){triangles[index++]=a;triangles[index++]=b;triangles[index++]=c;triangles[index++]=b;triangles[index++]=d;triangles[index++]=c;}
                else{triangles[index++]=a;triangles[index++]=c;triangles[index++]=b;triangles[index++]=b;triangles[index++]=c;triangles[index++]=d;}
            }
            page.vertices=vertices;page.uv=uv;page.triangles=triangles;turningPage.sharedMesh=page;turningPage.gameObject.SetActive(false);
        }
        void Update()
        {
            bool active=station!=null&&station.session!=null&&station.session.Ready&&station.session.State.stations[station.topic].phase==1;
            OpenAmount=Mathf.MoveTowards(OpenAmount,active?1:0,Time.deltaTime/Mathf.Max(.1f,openingSeconds));
            float open=Mathf.SmoothStep(0,1,OpenAmount),time=(float)(ArchiveTopicSession.Clock%3600);
            bookRig.localPosition=rest+new Vector3(.55f*open,(hoverHeight+Mathf.Sin(time*1.7f)*.035f)*open,0);
            bookRig.localRotation=Quaternion.Euler(Mathf.Sin(time*.7f)*2*open,Mathf.Sin(time*.45f)*4*open,Mathf.Sin(time*.9f)*1.5f*open);
            coverHinge.localRotation=Quaternion.Euler(0,0,155*open);
            float cycle=Mathf.Repeat(time/Mathf.Max(.8f,pageSeconds),1);
            bool show=OpenAmount>.98f&&cycle>.08f&&cycle<.90f;
            turningPage.gameObject.SetActive(show);if(!show)return;
            float angle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.90f,cycle))*155*Mathf.Deg2Rad;
            for(int side=0;side<2;side++)for(int i=0;i<=Segments;i++)for(int edge=0;edge<2;edge++)
            {
                float t=i/(float)Segments,curl=Mathf.Sin(t*Mathf.PI)*Mathf.Sin(angle)*.12f;
                vertices[side*(Segments+1)*2+i*2+edge]=new Vector3(-.58f+Mathf.Cos(angle)*t*1.10f,.255f+Mathf.Sin(angle)*t*1.10f+curl+(side==0?.002f:-.002f),(edge==0?-.38f:.38f)+Mathf.Sin(t*Mathf.PI)*Mathf.Sin(angle)*.025f);
            }
            page.vertices=vertices;page.RecalculateNormals();page.RecalculateBounds();
        }
        void OnDestroy(){if(page!=null)Destroy(page);}
    }
}
