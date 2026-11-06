#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Offline saved-scene visual checks. No rewards or account writes.</summary>
public static class InferenceSurfaceChecks
{
    const string Report = "Library/InferenceSurfaceChecks.txt";
    static void Check(bool pass, string message)
    { if (!pass) throw new InvalidOperationException(message); File.AppendAllText(Report, "PASS " + message + "\n"); }
    public static IEnumerator Run()
    {
        File.WriteAllText(Report, "RulesOfInference saved-scene surface verification. Offline account-free test session.\n");
        var root = GameObject.Find("Rules_Of_Inference"); var floor = root.transform.Find("FLOOR");
        var ground = floor.GetComponent<MeshRenderer>(); var material = ground.sharedMaterial;
        var report = JsonUtility.FromJson<InferenceSurfaceSetup.Report>(File.ReadAllText(InferenceSurfaceSetup.Folder + "/InferenceSurfaceRoutes.json"));
        Check(report.transformsCollidersAndGameplayPreserved, "authoring preserved transforms, colliders, meshes, water and gameplay components");
        Check(report.routes.Length == 10 && report.routes.Count(route => route.name.StartsWith("Crystal tower")) == 9, "ten routes connect the book, altar approach and all nine crystal towers");
        Check(report.stoneRenderers == 43 && report.changedSlots == 52, "stone and ground changes affect only the intended 52 material slots");
        Check(material.shader.name == "LogicLegends/Inference/StylizedMeadowAndPaths" && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader), "stylized grass shader compiles for the active mobile target");
        Check(AssetDatabase.GetAssetPath(material.GetTexture("_GrassMap")).Contains("MULTIPLAYER/OPEN_ARCHIVE"), "existing multiplayer painted grass texture is reused");
        Check(material.GetFloat("_TileMeters") == 4 && material.GetFloat("_DetailStrength") == .3f, "world-meter tiling and soft detail avoid stretched or photorealistic ground");
        var mask = (Texture2D)material.GetTexture("_PathMask");
        Check(mask.width == 1024 && mask.mipmapCount > 1 && mask.wrapMode == TextureWrapMode.Clamp, "baked path mask is 1K with mipmaps and clamped edges");
        Check(floor.GetComponent<MeshCollider>().enabled && floor.GetComponent<MeshCollider>().sharedMesh == floor.GetComponent<MeshFilter>().sharedMesh, "original floor collision and mesh remain active");
        var stones = floor.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.sharedMaterials.Any(m => m != null && AssetDatabase.GetAssetPath(m).StartsWith(InferenceSurfaceSetup.Folder + "/Stone_"))).ToArray();
        Check(stones.Length == 43 && stones.All(r => r.sharedMaterials.Where(m => m != null && m.name.StartsWith("Stone_")).All(m => m.shader.isSupported && !ShaderUtil.ShaderHasError(m.shader) && m.GetFloat("_StoneTextureStrength") == 0)), "43 stone renderers, including perimeter rocks, use supported soft painted surfaces without photographic rock detail");
        var altar = floor.Find("ALTAR/Cylinder.077").GetComponent<MeshRenderer>();
        Check(altar.sharedMaterials[0] != altar.sharedMaterials[1] && altar.sharedMaterials[1] == altar.sharedMaterials[4], "altar's dark/light rune contrast remains distinct");
        Check(altar.sharedMaterials[5].name.Contains("GOLD") && altar.sharedMaterials[9].name.Contains("BLUE"), "gold and blue altar inlays keep their original materials");
        var ocean = floor.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Ocean50x50W750H750");
        Check(!ocean.GetComponent<MeshRenderer>().sharedMaterials.Any(m => m != null && AssetDatabase.GetAssetPath(m).StartsWith(InferenceSurfaceSetup.Folder)), "water retains its original materials");
        Check(!root.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Dirt Decal") || t.name.Contains("Path Overlay")), "no coplanar dirt meshes or decals can introduce z-fighting");
        var inference = UnityEngine.Object.FindFirstObjectByType<LogicLegends.Inference.InferenceChallenge>();
        Check(!inference.HasStarted && inference.crystalSpawns.Length == 9, "visual checks begin with the real challenge waiting at the book and nine tower references");
        float before = StageJourneyUI.Instance.InferenceRemainingSeconds;
        var camera = Camera.main; var brain = camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        var position = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
        bool brainBefore = brain != null && brain.enabled;
        if (brain != null) brain.enabled = false;
        try
        {
            camera.transform.SetPositionAndRotation(new Vector3(116, -126, 820), Quaternion.LookRotation(new Vector3(103, -132, 799) - new Vector3(116, -126, 820)));
            camera.fieldOfView = 65;
            foreach (int size in new[] { 6, 12, 3 })
            {
                BookMapMobileChecks.SetSize(size); yield return new WaitForSecondsRealtime(.35f); yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Library/InferenceSurfaces_Play_" + Screen.width + "x" + Screen.height + ".png"); yield return null;
                Check(camera.targetTexture == null, "normal direct gameplay and UI rendering at " + Screen.width + "x" + Screen.height);
            }
            var a = Capture("Library/InferenceSurfaces_StableA.png"); var b = Capture("Library/InferenceSurfaces_StableB.png");
            var pa = a.GetPixels32(); var pb = b.GetPixels32(); double difference = 0;
            for (int i = 0; i < pa.Length; i++) difference += Math.Abs(pa[i].r - pb[i].r) + Math.Abs(pa[i].g - pb[i].g) + Math.Abs(pa[i].b - pb[i].b);
            difference /= pa.Length * 3 * 255.0; UnityEngine.Object.Destroy(a); UnityEngine.Object.Destroy(b);
            Check(difference < .001, "fixed-view surface render has no flicker; repeated-frame difference=" + difference);
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov; if (brain != null) brain.enabled = brainBefore; BookMapMobileChecks.SetSize(6); }
        Check(Mathf.Abs(before - StageJourneyUI.Instance.InferenceRemainingSeconds) < .01f && !inference.HasStarted, "visual checks do not start a challenge or consume its timer");
        Check(inference.StartFromBook(), "existing Book Statue activation still starts the challenge");
        inference.OpenBoard(Player.LocalInstance); yield return new WaitForSecondsRealtime(.2f);
        Check(inference.IsBoardOpen && inference.Stage == LogicLegends.Inference.InferencePuzzleStage.CompletingPremises, "the original board still opens with its premises and gameplay references");
        inference.CloseBoard(); yield return null;
        Check(!inference.IsBoardOpen && Player.LocalInstance.enabled, "Back restores gameplay controls");
        File.AppendAllText(Report, "ALL SURFACE CHECKS PASSED. Physical-device GPU profiling not performed.\n");
    }
    static Texture2D Capture(string path)
    {
        var go = new GameObject("Temporary Surface Stability Camera"); var camera = go.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(114, -126, 819), Quaternion.LookRotation(new Vector3(103, -132, 797) - new Vector3(114, -126, 819)));
        camera.fieldOfView = 65; camera.nearClipPlane = .1f; camera.farClipPlane = 700;
        var rt = new RenderTexture(1280, 720, 24); camera.targetTexture = rt; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
        RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(rt); return texture;
    }
}
#endif
