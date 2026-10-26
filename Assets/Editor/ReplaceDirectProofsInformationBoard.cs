#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Visual-only replacement. Existing dynamic TMP components keep their file IDs and references.</summary>
public static class ReplaceDirectProofsInformationBoard
{
    public const string Model="Assets/FINAL_ASSETS/MAPS/REMADE/Induction/InductionBoards/Prefabs/InformationBoard.prefab";
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Use a saved scene in Edit Mode.");
        if(SceneManager.GetActiveScene().path!=ShareProofChamberDecorations.DirectScene)
            EditorSceneManager.OpenScene(ShareProofChamberDecorations.DirectScene);
        var stage=UnityEngine.Object.FindFirstObjectByType<DirectIndirectProofStageController>();
        string original=EditorJsonUtility.ToJson(stage);
        var text=stage.InformationBoard.GetComponent<TMP_Text>();var parent=text.transform.parent;
        var header=parent.Find("Trial Board Header").GetComponent<TMP_Text>();
        var old=GameObject.Find("Board");
        if(old==null||parent.Find("InformationBoard Visuals")!=null)throw new InvalidOperationException("Inspect the board before replacing it again.");
        if(old.GetComponentsInChildren<MonoBehaviour>(true).Length!=0||text.transform.IsChildOf(old.transform)||header.transform.IsChildOf(old.transform))
            throw new InvalidOperationException("The old art contains a functional component; preserve it before removal.");
        // Retain the previous framing transform, replacing only its primitive geometry.
        var visuals=parent.Find("Board Wall Backer");
        foreach(var component in visuals.GetComponents<Component>())if(!(component is Transform))UnityEngine.Object.DestroyImmediate(component);
        visuals.name="InformationBoard Visuals";visuals.position=Vector3.zero;visuals.rotation=Quaternion.identity;visuals.localScale=Vector3.one;
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model),visuals);
        model.name="Information Board Model";model.transform.SetPositionAndRotation(new Vector3(56.7f,1.42f,33.1f),Quaternion.identity);
        model.transform.localScale=Vector3.one*3.8f;
        // These are sample labels from the art pack. The stage's existing labels remain authoritative.
        foreach(var sample in model.GetComponentsInChildren<TMP_Text>(true))sample.gameObject.SetActive(false);
        foreach(var c in model.GetComponentsInChildren<Collider>(true))c.enabled=false;
        var art=model.GetComponentsInChildren<Renderer>().Where(r=>r.GetComponent<TMP_Text>()==null&&r.GetComponent<TMP_SubMesh>()==null).ToArray();
        var bounds=art[0].bounds;foreach(var renderer in art.Skip(1))bounds.Encapsulate(renderer.bounds);
        var box=visuals.gameObject.AddComponent<BoxCollider>();box.center=visuals.InverseTransformPoint(bounds.center);box.size=bounds.size;
        var parchment=model.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Info_ParchmentSurface").bounds;
        float front=parchment.max.z+.045f;
        SetText(header,new Vector3(parchment.center.x,parchment.max.y-.65f,front),new Vector2(5.7f,.85f),3.8f,2.8f,TextAlignmentOptions.Center);
        SetText(text,new Vector3(parchment.center.x,parchment.center.y-.45f,front),new Vector2(5.7f,5.55f),3.4f,1.95f,TextAlignmentOptions.TopLeft);
        // Only the art hierarchy is removed; all stage/controller/TMP references stay in place.
        UnityEngine.Object.DestroyImmediate(old);
        foreach(string name in new[]{"Board Left Support","Board Right Support"})
        {
            var support=parent.Find(name);
            if(support.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)throw new InvalidOperationException("Support has gameplay behaviour.");
            UnityEngine.Object.DestroyImmediate(support.gameObject);
        }
        if(EditorJsonUtility.ToJson(stage)!=original)throw new InvalidOperationException("A stage reference changed during the visual replacement.");
        Physics.SyncTransforms();
        ValidateBank(stage,text,parchment);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.AppendAllText("Library/DirectInformationBoardChecks.txt","PASS old model/supports removed; original stage serialization and dynamic text references preserved; upright parchment faces gameplay area (+Z).\n");
    }
    static void SetText(TMP_Text text,Vector3 position,Vector2 size,float max,float min,TextAlignmentOptions alignment)
    {
        text.transform.SetPositionAndRotation(position,Quaternion.Euler(0,180,0));text.transform.localScale=Vector3.one;
        text.rectTransform.sizeDelta=size;text.rectTransform.pivot=new Vector2(.5f,.5f);
        text.alignment=alignment;text.enableAutoSizing=true;text.fontSize=text.fontSizeMax=max;text.fontSizeMin=min;
        text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;
        text.margin=new Vector4(.12f,.08f,.12f,.08f);text.raycastTarget=false;text.ForceMeshUpdate();
    }
    public static void ValidateBank(DirectIndirectProofStageController stage,TMP_Text text,Bounds parchment)
    {
        string original=text.text;var catalog=(DirectIndirectProofCatalog)new SerializedObject(stage).FindProperty("catalog").objectReferenceValue;
        int count=0;float smallest=100;
        foreach(var set in catalog.variantSets.Select((bank,index)=>new{bank,index}))foreach(var q in set.bank.variants)
        {
            foreach(string copy in new[]{"CHALLENGE "+(set.index+1)+" · QUESTION 2 OF 2\n"+q.title+"\n\n"+q.problem,
                "CHALLENGE "+(set.index+1)+" · QUESTION 2 COMPLETE\n"+q.title+"\n\n"+q.explanation+"\n\nThe timer is paused."})
            {
                text.text=copy;text.ForceMeshUpdate();
                if(text.isTextOverflowing)throw new InvalidOperationException("Board copy clips: "+q.title);
                smallest=Mathf.Min(smallest,text.fontSize);
            }
            count++;
        }
        text.text=original;text.ForceMeshUpdate();
        var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
        if(corners.Any(p=>p.x<parchment.min.x||p.x>parchment.max.x||p.y<parchment.min.y||p.y>parchment.max.y))throw new InvalidOperationException("Dynamic text leaves the parchment face.");
        File.WriteAllText("Library/DirectInformationBoardChecks.txt","PASS all "+count+" bank questions and completion explanations wrap without clipping; minimum fitted font="+smallest+".\n");
    }
}
#endif
