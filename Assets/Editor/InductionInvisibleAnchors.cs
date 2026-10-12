using System;
using System.Linq;
using LogicLegends.Induction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Invisible, data-keyed proof positions on the existing altar surfaces.</summary>
public static class InductionInvisibleAnchors
{
    [MenuItem("Tools/Logic Legends/Use Invisible Induction Placement Anchors")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before changing placement anchors.");
        var scene=SceneManager.GetActiveScene();
        if (scene.path!="Assets/Scenes/Induction.unity") throw new InvalidOperationException("Open Induction first.");
        var manager=Object.FindFirstObjectByType<InductionStageManager>();
        if(manager==null || manager.stations.Any(s=>s==null) || manager.slots.Length!=6 || manager.slots.Any(s=>s==null))
            throw new InvalidOperationException("Reconnect the existing three altars and six answer references first.");
        var renderers=manager.tabletPrefab.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.GetComponent<TMP_Text>()==null).ToArray();
        var scroll=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))scroll.Encapsulate(renderer.bounds);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Invisible Induction surface anchors");
        for(int index=0;index<manager.stations.Length;index++)
        {
            var station=manager.stations[index];var bounds=station.GetComponent<MeshRenderer>().bounds;
            float surface=SurfaceHeight(station);var collision=station.GetComponent<MeshCollider>();
            if(collision==null)throw new InvalidOperationException("The altar needs its existing surface collider.");
            var title=station.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="Station Label");
            if(title!=null)
            {
                // Keep the station title on the front plaque, below the proof labels.
                Undo.RecordObject(title.transform,"Separate station title from formula labels");
                title.transform.position=new Vector3(bounds.center.x,surface-.65f,bounds.min.z-.08f);
                EditorUtility.SetDirty(title.transform);
            }
            var prefix=new[]{"Base:","Hypothesis:","Step:"}[index];
            var targets=manager.slots.Where(s=>s.key.StartsWith(prefix)).OrderBy(s=>s.key).ToArray();
            for(int i=0;i<targets.Length;i++)
            {
                var target=targets[i];Undo.RecordObject(target,"Use an invisible proof anchor");
                // Destroy the old pad and empty-slot label, rather than hiding their renderers.
                foreach(var child in target.transform.Cast<Transform>().Where(t=>t!=target.tabletAnchor &&
                    (t.GetComponentInChildren<Renderer>(true)!=null || t.name=="Slot Rim" || t.name=="Slot Label")).ToArray())
                    Undo.DestroyObjectImmediate(child.gameObject);
                target.rim=null;target.label=null;
                target.name=index==0?"Base Placement Anchor":index==1?"Hypothesis Placement Anchor":"Step Placement Anchor "+(i+1);
                target.displayName=index==0?"Base Case altar":index==1?"Inductive Hypothesis altar":"Inductive Step line "+(i+1);
                float x=bounds.center.x+(targets.Length==1?0:i%2==0?-1.8f:1.8f);
                float z=bounds.center.z+(targets.Length==1?1f:i<2?1.1f:-1.1f);
                Vector3 pose=ClearPose(collision,bounds,surface,scroll,new Vector3(x,surface,z));
                Undo.RecordObject(target.transform,"Place scroll directly above the altar surface");
                target.transform.SetPositionAndRotation(pose,Quaternion.identity);
                Undo.RecordObject(target.tabletAnchor,"Use the surface pose for scrolls");
                target.tabletAnchor.name="Scroll Pose";target.tabletAnchor.localPosition=Vector3.zero;target.tabletAnchor.localRotation=Quaternion.identity;
                target.formulaLabelHeight=targets.Length==4?(i<2?1.8f:.65f):1.1f;
                var trigger=target.GetComponent<BoxCollider>();Undo.RecordObject(trigger,"Fit invisible placement detection");
                trigger.isTrigger=true;trigger.center=Vector3.zero;trigger.size=new Vector3(2f,1.4f,1f);
                target.MarkError(false);EditorUtility.SetDirty(target);
            }
        }
        // Remove any stale, separately named Base Case Slot left outside the manager's references.
        foreach(var old in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
            .Where(t=>t.name=="Base Case Slot" || t.name=="Base 1 Placement Slot").ToArray())Undo.DestroyObjectImmediate(old.gameObject);
        Undo.CollapseUndoOperations(group);Validate(manager);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Induction uses six invisible surface anchors; scrolls are graded only by submission at the book.");
    }
    public static float SurfaceHeight(Transform station)
    {
        // The largest upward-facing horizontal triangles describe the stone tabletop.
        // Crystal peaks in Renderer.bounds must not make all scrolls float high above it.
        var mesh=station.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
        float largest=0,height=0;
        for(int i=0;i<triangles.Length;i+=3)
        {
            var a=station.TransformPoint(vertices[triangles[i]]);var b=station.TransformPoint(vertices[triangles[i+1]]);var c=station.TransformPoint(vertices[triangles[i+2]]);
            var normal=Vector3.Cross(b-a,c-a);float area=normal.magnitude*.5f;
            if(normal.normalized.y>.98f && area>largest){largest=area;height=(a.y+b.y+c.y)/3;}
        }
        if(largest<1)throw new InvalidOperationException("Could not identify the altar's flat top surface.");
        return height;
    }
    static Vector3 ClearPose(MeshCollider altar,Bounds bounds,float surface,Bounds scroll,Vector3 desired)
    {
        Vector3 best=desired;float score=float.MaxValue;
        foreach(float dx in new[]{0f,-.2f,.2f,-.4f,.4f,-.6f,.6f})foreach(float dz in new[]{0f,-.2f,.2f,-.4f,.4f,-.6f,.6f})
        {
            var point=desired+new Vector3(dx,0,dz);float top=surface;
            for(int x=0;x<=6;x++)for(int z=0;z<=4;z++)
            {
                float X=point.x+Mathf.Lerp(scroll.min.x-.06f,scroll.max.x+.06f,x/6f);
                float Z=point.z+Mathf.Lerp(scroll.min.z-.06f,scroll.max.z+.06f,z/4f);
                RaycastHit hit;
                if(altar.Raycast(new Ray(new Vector3(X,bounds.max.y+3,Z),Vector3.down),out hit,bounds.size.y+6))top=Mathf.Max(top,hit.point.y);
            }
            float cost=(top-surface)*10+Mathf.Abs(dx)+Mathf.Abs(dz);
            if(cost>=score)continue;score=cost;best=new Vector3(point.x,top-scroll.min.y+.10f,point.z);
        }
        return best;
    }
    public static void Validate(InductionStageManager manager)
    {
        if(manager.slots.Select(s=>s.key).Distinct().Count()!=6)throw new InvalidOperationException("Proof answer keys must remain unique.");
        foreach(var slot in manager.slots)
        {
            if(slot.GetComponentsInChildren<Renderer>(true).Length!=0 || slot.rim!=null || slot.label!=null)
                throw new InvalidOperationException("A placement anchor still has a visible slot object.");
            if(slot.tabletAnchor.position.y<=SurfaceHeight(slot.transform.parent))throw new InvalidOperationException("A scroll pose is below its altar surface.");
        }
    }
}
