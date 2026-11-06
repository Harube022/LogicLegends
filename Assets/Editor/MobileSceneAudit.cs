#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Read-only inventory of every game scene, including inactive prototypes.</summary>
public static class MobileSceneAudit
{
    [Serializable] public sealed class Entry
    {
        public string scene; public bool inBuild; public int objects, missingScripts, colliders;
        public string[] cameras, inputSystems, cameraTriggers, missingMeshes, broadConvexMeshes, transparentRaycasts;
    }
    [Serializable] public sealed class Report { public List<Entry> scenes = new List<Entry>(); }
    static string[] paths; static int index; static Report report;
    public const string Output = "Library/AllScenesMobileInventory.json";
    [MenuItem("Logic Legends/Mobile/Audit Saved Scene Components")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        report = new Report(); index = 0;
        paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    static void Tick()
    {
        if (EditorApplication.isCompiling) return;
        if (index >= paths.Length)
        { EditorApplication.update -= Tick; File.WriteAllText(Output, JsonUtility.ToJson(report, true)); return; }
        string path = paths[index++];var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var objects=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
            var colliders=objects.SelectMany(o=>o.GetComponents<Collider>()).ToArray();
            var cameras=objects.SelectMany(o=>o.GetComponents<Camera>()).ToArray();
            var entry=new Entry { scene=path,inBuild=EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==path),objects=objects.Length,
                missingScripts=objects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount),colliders=colliders.Length };
            entry.cameras=cameras.Select(c=>Name(c.transform)+" active="+c.gameObject.activeInHierarchy+" enabled="+c.enabled+" tag="+c.tag+
                " clip="+c.nearClipPlane+".."+c.farClipPlane+" texture="+(c.targetTexture!=null)+" writers="+
                string.Join(",",c.GetComponents<Behaviour>().Where(b=>b!=null&&b.enabled).Select(b=>b.GetType().Name))).ToArray();
            entry.inputSystems=objects.SelectMany(o=>o.GetComponents<EventSystem>()).Select(e=>Name(e.transform)+" active="+e.gameObject.activeInHierarchy+" modules="+
                string.Join(",",e.GetComponents<BaseInputModule>().Select(m=>m.GetType().Name+":"+m.enabled))).ToArray();
            entry.cameraTriggers=objects.SelectMany(o=>o.GetComponents<CameraZoomTrigger>()).Select(c=>Name(c.transform)+" collider="+c.GetComponent<Collider>()?.GetType().Name).ToArray();
            entry.missingMeshes=colliders.OfType<MeshCollider>().Where(c=>c.enabled&&c.sharedMesh==null).Select(c=>Name(c.transform)).ToArray();
            entry.broadConvexMeshes=colliders.OfType<MeshCollider>().Where(c=>c.enabled&&c.convex&&c.attachedRigidbody==null&&c.sharedMesh!=null&&
                c.GetComponent<MeshFilter>()?.sharedMesh==c.sharedMesh&&c.bounds.size.magnitude>12).Select(c=>Name(c.transform)+" bounds="+c.bounds).ToArray();
            entry.transparentRaycasts=objects.SelectMany(o=>o.GetComponents<Image>()).Where(i=>i.gameObject.activeInHierarchy&&i.raycastTarget&&i.color.a<.01f&&
                i.GetComponentInParent<MobileLookInput>()==null&&i.GetComponentInParent<MobileInputUI>()==null&&i.GetComponentInParent<Selectable>()==null&&
                i.GetComponentInParent<ScrollRect>()==null).Select(i=>Name(i.transform)).Take(20).ToArray();
            report.scenes.Add(entry);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static string Name(Transform t) => t.parent==null?t.name:Name(t.parent)+"/"+t.name;
}
#endif
