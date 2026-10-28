using TMPro;
using UnityEngine;

/// <summary>A visual arrow observes the actual pending input. It never accepts interaction.</summary>
[DefaultExecutionOrder(1500)]
public sealed class FunctionsRelationsGuidance : MonoBehaviour
{
    FunctionsRelationsStageManager stage;
    Transform arrow;
    Mesh mesh;
    Material yellow, shadow;
    public Transform Arrow => arrow;
    public Transform Target { get; private set; }
    public void Initialize(FunctionsRelationsStageManager owner)
    {
        stage = owner;
        mesh = new Mesh { name = "Mapping Down Arrow" };
        mesh.vertices = new[] {new Vector3(-.16f,.65f,0),new Vector3(.16f,.65f,0),new Vector3(.16f,0,0),
            new Vector3(.5f,0,0),new Vector3(0,-.6f,0),new Vector3(-.5f,0,0),new Vector3(-.16f,0,0)};
        mesh.triangles = new[]{0,1,2,0,2,6,5,3,4}; mesh.RecalculateBounds(); mesh.RecalculateNormals();
        yellow = new Material(Shader.Find("Universal Render Pipeline/Unlit")); yellow.SetColor("_BaseColor",new Color(1,.86f,.08f));
        shadow = new Material(yellow); shadow.SetColor("_BaseColor",new Color(.13f,.09f,.015f));
        arrow = new GameObject("Domain First Guidance Arrow").transform;
        Shape("Outline",shadow,1.16f,.025f); Shape("Yellow Down Arrow",yellow,1,0);
        arrow.gameObject.SetActive(false);
        // Old prefab captions can say Range / Codomain. Keep one accurate station label.
        foreach (var text in stage.RangeTarget.GetComponentsInChildren<TMP_Text>(true))
            if (text.name == "FR_PurposeLabel") text.text = "RANGE";
    }
    void Shape(string name,Material mat,float scale,float depth)
    {
        var shape = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); shape.transform.SetParent(arrow,false);
        shape.transform.localScale = Vector3.one*scale; shape.transform.localPosition = Vector3.forward*depth;
        shape.GetComponent<MeshFilter>().sharedMesh = mesh; shape.GetComponent<Renderer>().sharedMaterial = mat;
    }
    void LateUpdate()
    {
        if (arrow == null || stage == null || Camera.main == null) return;
        var practice = stage.Tutorial;
        bool practicing = practice != null && (practice.CurrentStep == FunctionsRelationsTutorialStep.Domain ||
            practice.CurrentStep == FunctionsRelationsTutorialStep.Input || practice.CurrentStep == FunctionsRelationsTutorialStep.Output ||
            practice.CurrentStep == FunctionsRelationsTutorialStep.OutputChoice || practice.CurrentStep == FunctionsRelationsTutorialStep.Connection);
        bool active = practicing || !stage.GameplayPaused && !stage.IsBoardOpen && stage.State == FunctionsRelationsState.ACTIVE &&
            stage.CurrentChallenge.questionKind == MappingQuestionKind.BuildExactRelation;
        arrow.gameObject.SetActive(active); if (!active) { Target = null; return; }
        bool output = practicing ? practice.CurrentStep == FunctionsRelationsTutorialStep.Output || practice.CurrentStep == FunctionsRelationsTutorialStep.OutputChoice : stage.SelectedDomain != null;
        Target = output ? stage.RangeTarget : stage.DomainTarget;
        Bounds b = new Bounds(Target.position,Vector3.zero); bool found=false;
        foreach(var r in Target.GetComponentsInChildren<Renderer>())
        {
            if(r.GetComponent<TMP_Text>()!=null||r.GetComponent<TMP_SubMesh>()!=null||r is LineRenderer)continue;
            if(!found){b=r.bounds;found=true;}else b.Encapsulate(r.bounds);
        }
        arrow.position = new Vector3(b.center.x,b.max.y+2f+Mathf.Sin(Time.unscaledTime*2.4f)*.2f,b.center.z);
        arrow.rotation = Camera.main.transform.rotation; arrow.localScale = Vector3.one*1.4f;
    }
    void OnDestroy()
    { if(arrow!=null)Destroy(arrow.gameObject);if(mesh!=null)Destroy(mesh);if(yellow!=null)Destroy(yellow);if(shadow!=null)Destroy(shadow); }
}
