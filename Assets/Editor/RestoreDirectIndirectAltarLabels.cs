using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates readable, lightweight labels on the front of the two proof altars.</summary>
public static class RestoreDirectIndirectAltarLabels
{
    private const string MaterialFolder = "Assets/Materials/DirectIndirectProofs/";

    [MenuItem("Logic Legends/Restore Direct and Indirect Altar Labels")]
    public static void RestoreAndSave()
    {
        EnsureSceneObjects();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    public static void EnsureSceneObjects()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Direct&Indirect")
            throw new InvalidOperationException("Open Direct&Indirect in Edit mode to restore its altar labels.");

        Transform room = RequiredRoot(scene, "TrialOfProofs").transform;
        Transform direct = RequiredRoot(scene, "DirectAltar").transform;
        Transform indirect = RequiredRoot(scene, "IndirectAltar").transform;
        DirectIndirectProofStageController stage = RequiredRoot(scene, "DirectIndirectProofStageManager")
            .GetComponent<DirectIndirectProofStageController>();
        if (stage == null) throw new InvalidOperationException("The proof stage controller is missing.");

        Material dark = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "ChamberSign_Dark.mat");
        Material directBlue = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "DirectSeal_Blue.mat");
        Material indirectRed = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "ContradictionSeal_Red.mat");
        if (dark == null || directBlue == null || indirectRed == null)
            throw new InvalidOperationException("The existing proof altar colors or sign material are missing.");

        Transform directPlate = EnsureNameplate(room, direct, "Direct Altar Nameplate", "DIRECT PROOF",
            new Color(.79f, .93f, 1f), dark, directBlue);
        Transform indirectPlate = EnsureNameplate(room, indirect, "Indirect Altar Nameplate", "INDIRECT PROOF",
            new Color(1f, .87f, .73f), dark, indirectRed);
        SerializedObject serialized = new SerializedObject(stage);
        serialized.FindProperty("directNameplate").objectReferenceValue = directPlate;
        serialized.FindProperty("indirectNameplate").objectReferenceValue = indirectPlate;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static Transform EnsureNameplate(Transform room, Transform altar, string name, string text,
        Color textColor, Material dark, Material borderMaterial)
    {
        Transform existing = room.Find(name);
        Transform plate = existing != null ? existing : new GameObject(name).transform;
        if (existing == null) plate.SetParent(room, false);
        PositionAtAltarFront(plate, altar);

        Transform oldBorder = plate.Find("Bronze Border");
        if (oldBorder != null) oldBorder.name = "Altar Color Border";
        EnsurePanel(plate, "Altar Color Border", Vector3.zero,
            new Vector3(5.40f, 1.05f, .12f), borderMaterial);
        EnsurePanel(plate, "Dark Inset", new Vector3(0f, 0f, .075f),
            new Vector3(5.12f, .79f, .08f), dark);
        Transform label = plate.Find("Proof Name");
        if (label == null)
        {
            label = new GameObject("Proof Name", typeof(TextMeshPro)).transform;
            label.SetParent(plate, false);
        }
        label.localPosition = new Vector3(0f, 0f, .16f);
        // TextMeshPro's front face points toward local -Z; the player approaches from +Z.
        label.localRotation = Quaternion.Euler(0f, 180f, 0f);
        label.localScale = Vector3.one;
        TextMeshPro tmp = label.GetComponent<TextMeshPro>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 3.2f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1.8f;
        tmp.fontSizeMax = 3.2f;
        tmp.rectTransform.sizeDelta = new Vector2(4.92f, .70f);
        tmp.color = textColor;
        plate.gameObject.SetActive(altar.gameObject.activeSelf);
        return plate;
    }

    private static void PositionAtAltarFront(Transform plate, Transform altar)
    {
        Renderer[] renderers = altar.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException(altar.name + " has no visible mesh.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        plate.SetPositionAndRotation(
            new Vector3(bounds.center.x, bounds.max.y + .55f, bounds.center.z), Quaternion.identity);
        plate.localScale = Vector3.one;
    }

    private static void EnsurePanel(Transform parent, string name, Vector3 position,
        Vector3 scale, Material material)
    {
        Transform existing = parent.Find(name);
        GameObject panel = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (existing == null) { panel.name = name; panel.transform.SetParent(parent, false); }
        panel.transform.localPosition = position;
        panel.transform.localRotation = Quaternion.identity;
        panel.transform.localScale = scale;
        panel.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = panel.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
    }

    private static GameObject RequiredRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
        throw new InvalidOperationException("Missing scene object: " + name);
    }
}
