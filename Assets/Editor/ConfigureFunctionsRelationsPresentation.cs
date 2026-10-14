using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Updates existing objects and prefab overrides without rebuilding the user's environment.</summary>
public static class ConfigureFunctionsRelationsPresentation
{
    [MenuItem("Logic Legends/Update Functions and Relations Presentation")]
    public static void Configure()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "Functions and Relations") throw new InvalidOperationException("Open Functions and Relations first.");
        var stage = UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>();
        var so = new SerializedObject(stage);
        var canvas = GameObject.Find("FR_GameplayCanvas");
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;
        var safe = EnsureRect("SafeArea", canvas.transform);
        StageUITheme.Stretch(safe, Vector2.zero, Vector2.one);
        if (safe.GetComponent<Crystal.SafeArea>() == null) safe.gameObject.AddComponent<Crystal.SafeArea>();
        var root = canvas.transform.Find("FR_HUD") as RectTransform ?? safe.Find("FR_HUD") as RectTransform;
        root.SetParent(safe, false); StageUITheme.Stretch(root, Vector2.zero, Vector2.one);
        var ui = canvas.GetComponent<FunctionsRelationsPresentation>();
        if (ui == null) ui = canvas.AddComponent<FunctionsRelationsPresentation>();
        ui.stage = stage; ui.safeArea = safe;
        var hud = EnsureRect("CurrentStageHud", root); ui.hud = hud;
        StageUITheme.CompactSurface(Image(hud));
        var heading = Label("CurrentStageLabel", hud, "STAGE 5 / FUNCTIONS & RELATIONS", 30f, true);
        Stretch(heading.rectTransform, .06f, .62f, .94f, .86f);
        var clock = so.FindProperty("hudTimerText").objectReferenceValue as TMP_Text;
        clock.name = "StageCountdown"; clock.transform.SetParent(hud, false);
        Style(clock, 48f, true); Stretch(clock.rectTransform, .06f, .18f, .31f, .57f);
        var progress = Label("StageProgress", hud, "CHALLENGE 1 OF 5", 28f, false);
        progress.fontSizeMin = 23f; Stretch(progress.rectTransform, .32f, .08f, .94f, .60f); ui.progress = progress;
        ui.status = (so.FindProperty("statusText").objectReferenceValue as TMP_Text).rectTransform;
        Style(ui.status.GetComponent<TMP_Text>(), 28f, false);
        ui.status.GetComponent<TMP_Text>().outlineColor = new Color(.035f, .06f, .055f, .9f);
        ui.status.GetComponent<TMP_Text>().outlineWidth = .1f;
        ui.status.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.Normal;
        ui.viewBoard = (so.FindProperty("openButton").objectReferenceValue as Button).transform as RectTransform;
        StageUITheme.SkinButton(ui.viewBoard.GetComponent<Button>());
        ui.viewBoard.GetComponentInChildren<TMP_Text>().text = "View Board";
        var submit = so.FindProperty("submitButton").objectReferenceValue as Button;
        submit.transform.SetParent(root, false);
        submit.name = "Submit Answer"; StageUITheme.SkinButton(submit);
        submit.GetComponentInChildren<TMP_Text>().text = "Submit";
        ui.submit = submit.transform as RectTransform; submit.gameObject.SetActive(false);
        var back = root.Find("Board Back") != null ? root.Find("Board Back").GetComponent<Button>() :
            StageUITheme.Button("Board Back", root, "Back", null);
        ui.boardBack = back.transform as RectTransform; back.gameObject.SetActive(false);
        ui.interaction = (so.FindProperty("interactionPromptText").objectReferenceValue as TMP_Text).transform.parent as RectTransform;
        var interact = ui.interaction.GetComponent<Button>();
        if (interact == null) interact = ui.interaction.gameObject.AddComponent<Button>();
        StageUITheme.SkinButton(interact); interact.image.raycastTarget = true;
        var interactionLabel = interact.GetComponentInChildren<TMP_Text>();
        interactionLabel.fontSizeMin = 25f; interactionLabel.fontSizeMax = 32f;
        interactionLabel.textWrappingMode = TextWrappingModes.Normal;
        Set(so, "nearbyInteractionButton", interact);
        ui.domainPanel = so.FindProperty("domainChoicesPanel").objectReferenceValue as RectTransform;
        ui.rangePanel = so.FindProperty("rangeChoicesPanel").objectReferenceValue as RectTransform;
        ui.domainHint = Choices(ui.domainPanel, "DOMAIN • INPUTS", so.FindProperty("domainChoiceButtons"), stage, false);
        ui.rangeHint = Choices(ui.rangePanel, "RANGE • OUTPUTS", so.FindProperty("rangeChoiceButtons"), stage, true);
        var undo = so.FindProperty("undoConnectionButton").objectReferenceValue as Button;
        StageUITheme.SkinButton(undo); Stretch((RectTransform)undo.transform, .06f, .08f, .65f, .29f);
        ui.mobileControls = GameObject.Find("Canvas 1").GetComponentsInChildren<RectTransform>(true)
            .Where(t => t.name == "Joystick" || t.name == "JumpButton" || t.name == "InteractButton" || t.name == "SettingsButton" || t.name == "Minimap").ToArray();
        Set(so, "domainInteractionTarget", GameObject.Find("Domain").transform);
        Set(so, "rangeInteractionTarget", GameObject.Find("Range").transform);

        // Keep the supplied InformationBoard prefab connected and update its existing text anchors.
        var info = GameObject.Find("InformationBoard");
        var labels = info.GetComponentsInChildren<TMP_Text>(true);
        var title = labels.Single(t => t.name == "ChallengeTitleLabel");
        var sets = labels.Single(t => t.name == "ChallengeFormulaLabel");
        var instructions = labels.Single(t => t.name == "ChallengeInstructionsLabel");
        var cameraView = stage.GetComponent<FunctionsRelationsBoardView>();
        if (cameraView == null) cameraView = stage.gameObject.AddComponent<FunctionsRelationsBoardView>();
        cameraView.informationBoard = info.transform; cameraView.boardInstructions = instructions; cameraView.backButton = back;
        cameraView.viewSafeArea = safe;
        var mapping = so.FindProperty("mapping").objectReferenceValue as LaserMappingController;
        cameraView.connectionBeams = new SerializedObject(mapping).FindProperty("beamRoot").objectReferenceValue as Transform;
        Set(so, "informationBoardView", cameraView); EditorUtility.SetDirty(cameraView);
        title.text = "CHALLENGE 1 OF 5";
        sets.text = "FUNCTIONS & RELATIONS";
        instructions.text = "Interact with the Book Statue to reveal the challenge.\nThe shared timer is paused.";
        title.font = StageUITheme.HeadingFont; title.fontSizeMin = 1.7f; title.fontSizeMax = 2.2f;
        sets.font = instructions.font = StageUITheme.ReadingFont;
        sets.fontSizeMin = 1.25f; sets.fontSizeMax = 1.8f;
        instructions.fontSizeMin = 1.05f; instructions.fontSizeMax = 1.55f;
        instructions.rectTransform.sizeDelta = new Vector2(1.43f, .90f);
        instructions.rectTransform.anchoredPosition = new Vector2(0f, -.11f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instructions.rectTransform);
        foreach (var label in labels)
        {
            label.enableAutoSizing = true; label.textWrappingMode = TextWrappingModes.Normal;
            label.color = StageUITheme.ReadingInk; label.alignment = TextAlignmentOptions.Center;
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        }
        Set(so, "worldProblemText", instructions);
        Set(so, "worldChallengeTitleText", title);
        Set(so, "worldChallengeSetsText", sets);
        // Retire the old board and its stale instructions as one object; retain it disabled for recovery.
        var oldBoard = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Board");
        if (oldBoard != null) oldBoard.SetActive(false);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stage); EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Functions and Relations PRELIM HUD, choice panels, interaction targets, and InformationBoard connected.");
    }

    private static TMP_Text Choices(RectTransform panel, string title, SerializedProperty buttons,
        FunctionsRelationsStageManager stage, bool range)
    {
        StageUITheme.CompactSurface(Image(panel));
        var heading = panel.Find("Heading").GetComponent<TMP_Text>();
        Style(heading, 36f, true); heading.text = title; Stretch(heading.rectTransform, .06f, .78f, .94f, .94f);
        var hint = Label("Selection Hint", panel, "Choose a value.", 28f, false);
        Stretch(hint.rectTransform, .06f, .62f, .94f, .77f);
        for (int i = 0; i < buttons.arraySize; i++)
        {
            var button = buttons.GetArrayElementAtIndex(i).objectReferenceValue as Button;
            StageUITheme.SkinButton(button);
            Stretch((RectTransform)button.transform, .06f + i * .225f, .33f, .265f + i * .225f, .59f);
            var text = button.GetComponentInChildren<TMP_Text>();
            text.fontSizeMin = 36f; text.fontSizeMax = 44f;
        }
        var close = panel.Find("Close Choices") != null ? panel.Find("Close Choices").GetComponent<Button>() :
            StageUITheme.Button("Close Choices", panel, "Close", null);
        close.onClick.RemoveAllListeners();
        // A persistent callback survives scene reloads; the selection listeners remain owned by the stage.
        while (close.onClick.GetPersistentEventCount() > 0)
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(close.onClick, 0);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick, stage.CloseDiamondChoices);
        Stretch((RectTransform)close.transform, range ? .68f : .35f, .08f, range ? .94f : .65f, .29f);
        panel.gameObject.SetActive(false);
        return hint;
    }

    private static RectTransform EnsureRect(string name, Transform parent)
    { return parent.Find(name) as RectTransform ?? StageUITheme.Rect(name, parent); }
    private static Image Image(RectTransform rect)
    {
        var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false; return image;
    }
    private static TMP_Text Label(string name, Transform parent, string value, float size, bool heading)
    {
        var label = parent.Find(name) != null ? parent.Find(name).GetComponent<TMP_Text>() : StageUITheme.Text(name, parent, value, size, !heading);
        label.text = value; Style(label, size, heading); return label;
    }
    private static void Style(TMP_Text text, float size, bool heading)
    {
        text.font = heading ? StageUITheme.HeadingFont : StageUITheme.ReadingFont;
        text.fontSharedMaterial = text.font.material;
        text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMin = size * .8f; text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.color = heading ? StageUITheme.Gold : StageUITheme.Ink;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
    }
    private static void Set(SerializedObject so, string name, UnityEngine.Object value)
    { so.FindProperty(name).objectReferenceValue = value; }
    private static void Stretch(RectTransform rect, float x0, float y0, float x1, float y1)
    { StageUITheme.Stretch(rect, new Vector2(x0, y0), new Vector2(x1, y1)); }
}
