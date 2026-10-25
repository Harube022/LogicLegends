using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.Induction
{
    [DefaultExecutionOrder(1700), DisallowMultipleComponent]
    public sealed class InductionAltarIndicators : MonoBehaviour
    {
        [Min(.5f)] public float clearance = 2;
        [Min(.3f)] public float arrowScale = 1.1f;
        [Range(0,.5f)] public float bobDistance = .2f;
        [Min(.1f)] public float bobSeconds = 2.4f;
        public Shader arrowShader;
        public Transform[] Arrows { get; private set; }
        InductionStageManager stage;
        Mesh mesh;
        Material gold, outline;
        static readonly string[] Stations = { "Base", "Hypothesis", "Step" };
        public void Initialize(InductionStageManager owner)
        {
            stage = owner;
            mesh = new Mesh { name = "Downward Altar Arrow" };
            mesh.vertices = new[] { new Vector3(-.18f,.8f,0),new Vector3(.18f,.8f,0),new Vector3(.18f,0,0),new Vector3(.6f,0,0),new Vector3(0,-.7f,0),new Vector3(-.6f,0,0),new Vector3(-.18f,0,0) };
            mesh.triangles = new[] { 0,1,2,0,2,6,5,3,4 };mesh.RecalculateNormals();mesh.RecalculateBounds();
            var shader = arrowShader != null ? arrowShader : Shader.Find("Universal Render Pipeline/Unlit");
            gold = new Material(shader) { name = "Altar Indicator Gold" };gold.SetColor("_BaseColor",new Color(1,.8f,.2f));
            outline = new Material(shader) { name = "Altar Indicator Outline" };outline.SetColor("_BaseColor",new Color(.11f,.07f,.025f));
            Arrows = new Transform[3];
            for(int i=0;i<Arrows.Length;i++)
            {
                var arrow=new GameObject(Stations[i]+" Active Altar Arrow").transform;arrow.SetParent(transform,false);Arrows[i]=arrow;
                MakeMesh(arrow,"Outline",outline,1.15f,.025f);MakeMesh(arrow,"Arrow",gold,1,0);
                arrow.gameObject.SetActive(false);
            }
        }
        void MakeMesh(Transform parent,string name,Material material,float scale,float depth)
        {
            var shape=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));shape.transform.SetParent(parent,false);
            shape.transform.localScale=Vector3.one*scale;shape.transform.localPosition=Vector3.forward*depth;
            shape.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=shape.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if(stage==null||Arrows==null)return;
            for(int i=0;i<Arrows.Length;i++)
            {
                bool active=stage.State==InductionState.Active&&!stage.Reading&&stage.Challenge.Uses(Stations[i]);
                var arrow=Arrows[i];arrow.gameObject.SetActive(active);if(!active)continue;
                var renderer=stage.stations[i].GetComponent<Renderer>();var bounds=renderer.bounds;
                float labelTop=stage.slots.Where(s=>s.key.StartsWith(Stations[i]+":")).Max(s=>s.transform.position.y+s.formulaLabelHeight+.55f);
                foreach(var label in stage.stations[i].GetComponentsInChildren<InductionWorldLabel>())
                    labelTop=Mathf.Max(labelTop,label.transform.position.y+label.GetComponent<TMPro.TMP_Text>().rectTransform.rect.height*.5f);
                foreach(var scroll in stage.Tablets.Where(t=>t.Slot!=null&&t.Slot.key.StartsWith(Stations[i]+":")))
                    labelTop=Mathf.Max(labelTop,scroll.label.bounds.max.y);
                float height=Mathf.Max(bounds.max.y,labelTop)+clearance+Mathf.Sin(Time.time*Mathf.PI*2/bobSeconds)*bobDistance;
                arrow.position=new Vector3(bounds.center.x,height,bounds.center.z);arrow.localScale=Vector3.one*arrowScale;
                if(Camera.main!=null)arrow.rotation=Quaternion.LookRotation(Camera.main.transform.forward,Camera.main.transform.up);
            }
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(gold!=null)Destroy(gold);if(outline!=null)Destroy(outline);}
    }
}
