#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Bakes neutral card portraits on disposable clones; source rigs and animations are never edited.</summary>
public static class LatestSkinPortraitSetup
{
    public const string PortraitFolder = "Assets/Art/ShopPortraits";

    public static string UpdateAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var result = JsonUtility.FromJson<LatestSkinPreparation.Result>(File.ReadAllText(LatestSkinPreparation.Root + "/Preparation/PreparedSkins.json"));
        foreach (var skin in result.skins)
        {
            string id = skin.category == "Uniform" ? skin.gender.ToLowerInvariant() + "_default" : skin.category + (skin.gender == "Female" ? "_Costume" : "_Skin");
            BakePortrait(AssetDatabase.LoadAssetAtPath<GameObject>(skin.prefab), PortraitFolder + "/" + id + ".png");
        }
        return "Updated " + result.skins.Count + " transparent neutral portraits; existing sprite GUIDs retained.";
    }

    public static void BakePortrait(GameObject source, string path)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var preview = new PreviewRenderUtility();
        Texture2D pixels = null, image = null;
        try
        {
            var clone = Object.Instantiate(source);
            clone.SetActive(false);
            clone.transform.SetParent(null, false);
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var behaviour in clone.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
            foreach (var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            preview.AddSingleGO(clone);
            clone.SetActive(true);
            PoseNeutral(clone);
            var bounds = LatestSkinPreparation.BakedBounds(clone);
            float bottom = bounds.min.y + bounds.size.y * .48f, top = bounds.max.y + bounds.size.y * .04f;
            var center = new Vector3(bounds.center.x, (bottom + top) * .5f, bounds.center.z);
            var camera = preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max((top - bottom) * .5f, bounds.extents.x / (425f / 320) * 1.04f);
            camera.transform.position = center + Vector3.forward * Mathf.Max(15, bounds.size.y * 3);
            camera.transform.LookAt(center);
            camera.nearClipPlane = .01f;
            camera.farClipPlane = Mathf.Max(100, bounds.size.y * 6);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            preview.ambientColor = new Color(.65f, .65f, .65f);
            preview.lights[0].intensity = 1.15f;
            preview.lights[0].transform.rotation = Quaternion.Euler(25, 195, 0);
            preview.lights[1].intensity = .75f;
            preview.lights[1].transform.rotation = Quaternion.Euler(15, 145, 0);
            preview.BeginStaticPreview(new Rect(0, 0, 425, 320));
            preview.Render(true, true);
            var target = camera.targetTexture;
            bool encodeSrgb = QualitySettings.activeColorSpace == ColorSpace.Linear && !target.sRGB;
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                pixels = new Texture2D(425, 320, TextureFormat.RGBAFloat, false, true);
                pixels.ReadPixels(new Rect(0, 0, 425, 320), 0, 0);
                pixels.Apply();
            }
            finally { RenderTexture.active = previous; }
            Object.DestroyImmediate(preview.EndStaticPreview());
            var colors = pixels.GetPixels();
            // The preview's linear HDR pixels need display encoding before PNG export.
            // Color.gamma changes RGB while preserving the transparent alpha channel.
            if (encodeSrgb)
                for (int i = 0; i < colors.Length; i++) colors[i] = colors[i].gamma;
            image = new Texture2D(425, 320, TextureFormat.RGBA32, false);
            image.SetPixels(colors);
            image.Apply();
            if (image.GetPixel(0, 0).a > .01f) throw new InvalidOperationException("Portrait must have a transparent background: " + path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            if (pixels != null) Object.DestroyImmediate(pixels);
            if (image != null) Object.DestroyImmediate(image);
            preview.Cleanup();
        }
        if (!path.StartsWith("Assets/", StringComparison.Ordinal)) return;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    public static void PoseNeutral(GameObject clone)
    {
        // Use each rig's mesh bind matrices; Idle poses and donor rig bases differ.
        var bindWorld = new Dictionary<Transform, Matrix4x4>();
        foreach (var renderer in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var bones = renderer.bones;
            var poses = renderer.sharedMesh.bindposes;
            for (int i = 0; i < Mathf.Min(bones.Length, poses.Length); i++)
                if (bones[i] != null && !bindWorld.ContainsKey(bones[i]))
                    bindWorld.Add(bones[i], renderer.transform.localToWorldMatrix * poses[i].inverse);
        }
        foreach (var entry in bindWorld.OrderBy(p => Depth(p.Key)))
        {
            var bone = entry.Key;
            var local = bone.parent.worldToLocalMatrix * entry.Value;
            bone.localPosition = local.GetColumn(3);
            bone.localRotation = local.rotation;
            bone.localScale = local.lossyScale;
        }
        foreach (string side in new[] { "Left", "Right" })
        {
            var arm = Bone(clone, side + "Arm");
            var forearm = Bone(clone, side + "ForeArm");
            var hand = Bone(clone, side + "Hand");
            if (arm == null || forearm == null || hand == null) continue;
            float sign = Mathf.Sign(arm.position.x - clone.transform.position.x);
            arm.rotation = Quaternion.FromToRotation(forearm.position - arm.position, new Vector3(sign * .16f, -1, 0).normalized) * arm.rotation;
            forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, new Vector3(sign * .12f, -1, .06f).normalized) * forearm.rotation;
        }
    }

    static Transform Bone(GameObject g, string suffix) => g.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == suffix || t.name.EndsWith(":" + suffix));
    static int Depth(Transform t) { int depth = 0; for (; t.parent != null; t = t.parent) depth++; return depth; }
}
#endif
