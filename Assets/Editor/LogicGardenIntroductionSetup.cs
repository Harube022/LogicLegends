#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>One-time scene wiring; saved objects/material remaps survive model reimport.</summary>
public static class LogicGardenIntroductionSetup
{
    private const string BorderPath = "Assets/FINAL_ASSETS/MAPS/REMADE/REMAKE TUTS/GARDEN/GARDENBORDER.fbx";
    private const string MaterialFolder = "Assets/Materials/LogicGardenBorder";

    [MenuItem("Logic Legends/Setup LogicGarden Introduction Guidance")]
    public static void SetupGuidance()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "LogicGarden") { Debug.LogError("Open LogicGarden first."); return; }
        Transform[] all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        LogicGardenIntroduction intro = all.Select(t => t.GetComponent<LogicGardenIntroduction>()).First(c => c != null);
        Transform wizard = all.First(t => t.name == "WIZARD" && t.parent != null && t.parent.name == "Logic Garden");
        InteractPortal door = all.First(t => t.name == "Door" && t.parent == null).GetComponent<InteractPortal>();
        InteractPortal sidePortal = all.First(t => t.name == "PORTAL (1)").GetComponent<InteractPortal>();
        DialogueManager dialogue = all.Select(t => t.GetComponent<DialogueManager>()).First(d => d != null);
        Transform safeArea = all.First(t => t.name == "Canvas 1").Find("SafeArea");

        TextMeshProUGUI notice = MakeText("Lefyahj Introduction Notice", safeArea, dialogue.dialogueText, 38,
            new Vector2(.5f, 1f), new Vector2(0, -150), new Vector2(1050, 72));
        notice.text = "Lefyahj the wizard wants to talk to you!";
        notice.color = new Color(1f, .9f, .53f);
        notice.outlineWidth = .25f;
        notice.outlineColor = new Color32(16, 28, 32, 255);

        Transform marker = wizard.Find("Introduction Attention Marker");
        if (marker == null)
        {
            marker = new GameObject("Introduction Attention Marker").transform;
            marker.SetParent(wizard, false);
        }
        marker.position = wizard.position + Vector3.up * 7.2f;
        TextMeshPro markerText = marker.GetComponent<TextMeshPro>();
        if (markerText == null) markerText = marker.gameObject.AddComponent<TextMeshPro>();
        marker = markerText.transform;
        markerText.font = dialogue.dialogueText.font;
        markerText.text = "!";
        markerText.fontSize = 7f;
        markerText.alignment = TextAlignmentOptions.Center;
        markerText.color = new Color(1f, .83f, .24f);
        markerText.outlineWidth = .25f;
        markerText.outlineColor = new Color32(18, 25, 26, 255);
        markerText.rectTransform.sizeDelta = new Vector2(3f, 4f);
        if (marker.GetComponent<FaceCamera>() == null) marker.gameObject.AddComponent<FaceCamera>();

        Transform cameraTransform = intro.transform.Find("Library Dialogue Camera");
        if (cameraTransform == null)
        {
            cameraTransform = new GameObject("Library Dialogue Camera").transform;
            cameraTransform.SetParent(intro.transform, false);
        }
        CinemachineCamera shot = cameraTransform.GetComponent<CinemachineCamera>();
        if (shot == null) shot = cameraTransform.gameObject.AddComponent<CinemachineCamera>();
        CinemachineCamera normal = all.First(t => t.name == "CM_ThirdPersonCam").GetComponent<CinemachineCamera>();
        if (normal != null) shot.Lens = normal.Lens;
        shot.Priority = new PrioritySettings { Enabled = true, Value = 150 };
        Vector3 focus = door.transform.position + Vector3.up * 4.7f;
        cameraTransform.position = door.transform.position + new Vector3(0f, 6.5f, 22f);
        cameraTransform.rotation = Quaternion.LookRotation(focus - cameraTransform.position, Vector3.up);
        shot.gameObject.SetActive(false);

        SerializedObject introObject = new SerializedObject(intro);
        Set(introObject, "wizardNotification", notice);
        Set(introObject, "wizardAttentionMarker", marker.gameObject);
        Set(introObject, "libraryDialogueCamera", shot);
        SerializedProperty entrances = introObject.FindProperty("otherLibraryEntrances");
        entrances.arraySize = 1;
        entrances.GetArrayElementAtIndex(0).objectReferenceValue = sidePortal;
        introObject.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject sideObject = new SerializedObject(sidePortal);
        sideObject.FindProperty("interactionUnlocked").boolValue = false;
        sideObject.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("LogicGarden library entrances, wizard notice, and guided dialogue camera are wired.");
    }

    [MenuItem("Logic Legends/Setup LogicGarden Introduction")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "LogicGarden") { Debug.LogError("Open LogicGarden first."); return; }
        Transform[] all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform wizard = all.First(t => t.name == "WIZARD" && t.parent != null && t.parent.name == "Logic Garden");
        InteractPortal door = all.First(t => t.name == "Door" && t.parent == null).GetComponent<InteractPortal>();
        Transform canvas = all.First(t => t.name == "Canvas 1");
        Transform safeArea = canvas.Find("SafeArea");
        DialogueManager dialogue = all.Select(t => t.GetComponent<DialogueManager>()).First(d => d != null);
        TextMeshProUGUI objective = all.First(t => t.name == "Ch1_TalkToWizard_Task" && t.IsChildOf(canvas)).GetComponent<TextMeshProUGUI>();
        RectTransform objectiveRect = objective.rectTransform;
        objectiveRect.sizeDelta = new Vector2(320, 106);
        objectiveRect.anchoredPosition = new Vector2(0, -72);
        objective.fontSize = 28;
        objective.enableWordWrapping = true;

        GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "LogicGarden Introduction");
        if (root == null) root = new GameObject("LogicGarden Introduction");
        LogicGardenIntroduction intro = root.GetComponent<LogicGardenIntroduction>();
        if (intro == null) intro = root.AddComponent<LogicGardenIntroduction>();

        SerializedObject portalObject = new SerializedObject(door);
        portalObject.FindProperty("requireInteraction").boolValue = true;
        portalObject.FindProperty("interactionUnlocked").boolValue = false;
        portalObject.FindProperty("interactionRange").floatValue = 4f;
        portalObject.ApplyModifiedPropertiesWithoutUndo();

        TextMeshProUGUI title = MakeText("LogicGarden Spawn Title", safeArea, dialogue.dialogueText, 58,
            new Vector2(.5f, 1f), new Vector2(0, -60), new Vector2(900, 90));
        title.text = "LogicGarden";
        title.color = new Color(1f, .94f, .72f);
        title.outlineWidth = .22f;
        title.outlineColor = new Color32(19, 28, 30, 255);

        Transform entranceLabel = door.transform.Find("Logic Library Entrance Label");
        if (entranceLabel == null)
        {
            GameObject labelObject = new GameObject("Logic Library Entrance Label");
            entranceLabel = labelObject.transform;
            entranceLabel.SetParent(door.transform, false);
        }
        // Face the Garden approach (+Z) and stay below the doorway lintel.
        entranceLabel.position = door.transform.position + new Vector3(0f, 2.2f, 1.4f);
        TextMeshPro worldText = entranceLabel.GetComponent<TextMeshPro>();
        if (worldText == null) worldText = entranceLabel.gameObject.AddComponent<TextMeshPro>();
        entranceLabel = worldText.transform; // Adding TMP replaces Transform with RectTransform.
        worldText.text = "To Logic Library";
        worldText.font = dialogue.dialogueText.font;
        worldText.fontSize = 4.3f;
        worldText.alignment = TextAlignmentOptions.Center;
        worldText.color = new Color(1f, .95f, .75f);
        worldText.outlineWidth = .22f;
        worldText.outlineColor = new Color32(18, 27, 31, 255);
        worldText.rectTransform.sizeDelta = new Vector2(10f, 1.5f);
        if (entranceLabel.GetComponent<FaceCamera>() == null) entranceLabel.gameObject.AddComponent<FaceCamera>();

        Button context = MakeButton("LogicGarden Context Interaction", safeArea, dialogue.dialogueText,
            new Vector2(.5f, .11f), new Vector2(0, 0), new Vector2(490, 94));
        TextMeshProUGUI contextText = context.GetComponentInChildren<TextMeshProUGUI>();
        contextText.text = "Talk with Lefyahj";
        Button returnButton = MakeButton("Return to LogicGarden", safeArea, dialogue.dialogueText,
            new Vector2(1f, 1f), new Vector2(-260, -68), new Vector2(420, 82));
        returnButton.GetComponentInChildren<TextMeshProUGUI>().text = "Return to LogicGarden";
        returnButton.gameObject.SetActive(false);

        dialogue.dialoguePanel.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 1080);
        RectTransform background = dialogue.dialoguePanel.transform.Find("DialogueBackground") as RectTransform;
        background.sizeDelta = new Vector2(0, 380);
        RectTransform dialogueRect = dialogue.dialogueText.rectTransform;
        dialogueRect.anchorMin = new Vector2(.04f, .16f);
        dialogueRect.anchorMax = new Vector2(.68f, .76f);
        dialogueRect.offsetMin = dialogueRect.offsetMax = Vector2.zero;
        dialogue.dialogueText.fontSize = 40;
        dialogue.dialogueText.enableAutoSizing = true;
        dialogue.dialogueText.fontSizeMin = 27;
        dialogue.dialogueText.fontSizeMax = 40;
        dialogue.dialogueText.enableWordWrapping = true;
        dialogue.dialogueText.overflowMode = TextOverflowModes.Truncate;
        dialogue.dialogueText.margin = Vector4.zero;
        TextMeshProUGUI speaker = MakeText("LogicGarden Speaker", background, dialogue.dialogueText, 36,
            new Vector2(.27f, .88f), new Vector2(0, 0), new Vector2(520, 54));
        speaker.alignment = TextAlignmentOptions.Left;
        speaker.text = "Lefyahj";
        speaker.color = new Color(1f, .84f, .43f);
        background.GetComponent<Image>().color = new Color(.055f, .105f, .13f, .94f);
        dialogue.dialogueText.color = Color.white;
        Button next = dialogue.dialoguePanel.GetComponentsInChildren<Button>(true).First(b => b.name == "NextDialogue_Button");
        next.gameObject.SetActive(true);
        RectTransform nextRect = next.GetComponent<RectTransform>();
        nextRect.SetParent(background, false);
        nextRect.localScale = Vector3.one;
        nextRect.pivot = new Vector2(.5f, .5f);
        nextRect.anchorMin = nextRect.anchorMax = new Vector2(.84f, .29f);
        nextRect.anchoredPosition = Vector2.zero;
        nextRect.sizeDelta = new Vector2(300, 110);
        next.GetComponent<Image>().color = new Color(.09f, .37f, .41f, 1f);
        UnityEngine.UI.Text nextLabel = next.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (nextLabel != null) { nextLabel.text = "Next"; nextLabel.fontSize = 32; nextLabel.color = Color.white; }

        SerializedObject introObject = new SerializedObject(intro);
        Set(introObject, "lefyahj", wizard);
        Set(introObject, "libraryDoor", door);
        Set(introObject, "objectivesText", objective);
        Set(introObject, "dialogue", dialogue);
        Set(introObject, "speakerText", speaker);
        Set(introObject, "nextButton", next);
        Set(introObject, "contextualButton", context);
        Set(introObject, "contextualButtonText", contextText);
        Set(introObject, "locationTitle", title);
        Set(introObject, "returnToGardenButton", returnButton);
        Set(introObject, "gardenEntry", all.First(t => t.name == "Spawn" && t.parent == null));
        Set(introObject, "studyLibrary", all.First(t => t.name == "LogicGarden Study Library").GetComponent<StudyLibraryController>());
        introObject.ApplyModifiedPropertiesWithoutUndo();

        FixBorder(all.First(t => t.name == "GARDENBORDER").GetComponent<MeshRenderer>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("LogicGarden introduction, locked library portal, and border materials are ready.");
    }

    private static void Set(SerializedObject obj, string name, Object value) => obj.FindProperty(name).objectReferenceValue = value;

    private static TextMeshProUGUI MakeText(string name, Transform parent, TMP_Text style, float size,
        Vector2 anchor, Vector2 position, Vector2 dimensions)
    {
        Transform child = parent.Find(name);
        if (child == null) { child = new GameObject(name, typeof(RectTransform)).transform; child.SetParent(parent, false); }
        RectTransform rect = (RectTransform)child;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        if (text == null) text = child.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = style.font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private static Button MakeButton(string name, Transform parent, TMP_Text style,
        Vector2 anchor, Vector2 position, Vector2 dimensions)
    {
        Transform child = parent.Find(name);
        if (child == null) { child = new GameObject(name, typeof(RectTransform)).transform; child.SetParent(parent, false); }
        RectTransform rect = (RectTransform)child;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Image image = child.GetComponent<Image>();
        if (image == null) image = child.gameObject.AddComponent<Image>();
        image.color = new Color(.08f, .22f, .25f, .96f);
        Button button = child.GetComponent<Button>();
        if (button == null) button = child.gameObject.AddComponent<Button>();
        TextMeshProUGUI label = MakeText("Label", child, style, 34,
            new Vector2(.5f, .5f), Vector2.zero, dimensions - new Vector2(18, 12));
        label.color = Color.white;
        return button;
    }

    private static void FixBorder(MeshRenderer renderer)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "LogicGardenBorder");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var replacements = new[]
        {
            MakeMaterial("GardenPalette", new Color(.40f, .57f, .30f), lit),
            MakeMaterial("CrystalPalette", new Color(.29f, .73f, .75f), lit),
            MakeMaterial("WaterPalette", new Color(.19f, .49f, .69f), lit)
        };
        var importer = (ModelImporter)AssetImporter.GetAtPath(BorderPath);
        string[] sourceNames = { "GardenPalette", "CrystalPalette", "WaterPalette",
            "GardenPalette.001", "CrystalPalette.001", "GardenPalette.002", "CrystalPalette.002" };
        for (int i = 0; i < sourceNames.Length; i++)
        {
            int replacementIndex = sourceNames[i].StartsWith("Garden") ? 0 :
                sourceNames[i].StartsWith("Crystal") ? 1 : 2;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceNames[i]), replacements[replacementIndex]);
        }
        importer.SaveAndReimport();
        Material[] slots = renderer.sharedMaterials;
        for (int i = 0; i < slots.Length; i++)
        {
            string name = slots[i] != null ? slots[i].name : "";
            if (name.StartsWith("GardenPalette")) slots[i] = replacements[0];
            else if (name.StartsWith("CrystalPalette")) slots[i] = replacements[1];
            else if (name.StartsWith("WaterPalette")) slots[i] = replacements[2];
        }
        renderer.sharedMaterials = slots;
    }

    private static Material MakeMaterial(string name, Color color, Shader shader)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", name == "WaterPalette" ? .54f : .16f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
#endif
