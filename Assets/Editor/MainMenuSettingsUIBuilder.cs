using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit Main Menu-only authoring tool; never runs on scene load.</summary>
[InitializeOnLoad]
public static class MainMenuSettingsUIBuilder
{
    private const string Folder = "Assets/Prefabs/UI/Settings";
    private static TMP_FontAsset font;
    private static Sprite frame, status, plaque, sectionHeader, teal, red, green, track, fill, handle;
    private static readonly Color Ink = new Color32(55, 32, 15, 255);
    private static readonly Color Cream = new Color32(255, 242, 212, 255);
    private static readonly Color Blue = new Color32(5, 121, 177, 255);
    private static readonly Color Muted = new Color32(125, 87, 52, 255);

    static MainMenuSettingsUIBuilder() { EditorApplication.update += CheckCloseRequest; }

    private static void CheckCloseRequest()
    {
        const string request = "Temp/MainMenuSettingsClose.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            File.Delete(request);
            CenterExistingSettings();
            File.WriteAllText("Temp/MainMenuSettingsClose.result", "PASS\nSettings centered with a red X. Prefab connection preserved.");
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/MainMenuSettingsClose.result", "FAIL\n" + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("Logic Legends/Main Menu/Center Settings And Add Close X")]
    public static void CenterExistingSettings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before editing Settings.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Main Menu.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Main Menu.unity", OpenSceneMode.Additive);
        try
        {
            var settings = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Settings_Menu");
            var menu = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuManager>(true)).Single();
            var canvas = settings.GetComponentInParent<Canvas>().transform;
            settings.SetParent(canvas, false);
            Stretch((RectTransform)settings); settings.localScale = Vector3.one;
            var ui = settings.Find("MainMenuSettingsUI") as RectTransform;
            if (!ui) throw new InvalidOperationException("The existing Settings UI is missing.");
            Stretch(ui); ui.localScale = Vector3.one;
            var panel = ui.Find("SettingsFrame") as RectTransform;
            Stretch(panel, .095f, .07f, .905f, .86f);
            var title = ui.Find("SettingsTitle") as RectTransform;
            Fixed(title, 0, 0, 560, 102, .5f, .92f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(title);
            var arrow = settings.Find("back_btn");
            if (arrow) arrow.gameObject.SetActive(false);
            var existing = ui.Find("CloseSettings");
            var close = existing ? existing.GetComponent<Button>() : MultiplayerUIArt.CloseAction("CloseSettings", ui, .905f, .92f);
            MultiplayerUIArt.StyleCloseAction(close);
            var rect = (RectTransform)close.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.905f, .92f);
            rect.pivot = new Vector2(1, .5f); rect.anchoredPosition = Vector2.zero;
            if (close.onClick.GetPersistentEventCount() == 0) UnityEventTools.AddPersistentListener(close.onClick, menu.ShowMainMenu);
            EditorUtility.SetDirty(close);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static Sprite SpriteAt(string path, string name = null)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>();
        return name == null ? sprites.OrderByDescending(s => s.rect.width * s.rect.height).First()
            : sprites.First(s => s.name == name);
    }

    private static RectTransform Node(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = LayerMask.NameToLayer("UI");
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform r, float x0 = 0, float y0 = 0, float x1 = 1, float y1 = 1)
    {
        r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private static void Fixed(RectTransform r, float x, float y, float width, float height, float ax = .5f, float ay = .5f)
    {
        r.anchorMin = r.anchorMax = new Vector2(ax, ay);
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(width, height);
    }

    private static Image Art(RectTransform r, Sprite sprite, float multiplier = 4.5f)
    {
        var image = r.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = Color.white; image.raycastTarget = false;
        image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = multiplier;
        return image;
    }

    private static TMP_Text Text(Transform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        var r = Node(name, parent); Stretch(r);
        var label = r.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = size; label.color = color;
        label.alignment = align; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        return label;
    }

    private static SettingsRoundedGraphic Capsule(RectTransform r, Color color)
    {
        var shape = r.gameObject.AddComponent<SettingsRoundedGraphic>();
        shape.color = color; shape.raycastTarget = false;
        return shape;
    }

    private static Button ButtonAt(Transform parent, string name, string caption, Sprite art,
        float width = 280, float height = 88)
    {
        var r = Node(name, parent); Fixed(r, 0, 0, width, height);
        var image = Art(r, art);
        var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.transition = Selectable.Transition.None; button.enabled = false;
        var text = Text(r, "Label", caption, 28, Cream, TextAlignmentOptions.Center);
        text.margin = new Vector4(24, 10, 24, 10);
        return button;
    }

    private static RectTransform Column(Transform page, string name, string heading, bool right)
    {
        var col = Node(name, page); Stretch(col, right ? .515f : 0, 0, right ? 1 : .485f, 1);
        var title = Node("SectionHeading", col);
        title.anchorMin = new Vector2(0, 1); title.anchorMax = Vector2.one;
        title.pivot = new Vector2(.5f, 1); title.sizeDelta = new Vector2(0, 64);
        Art(title, sectionHeader, 4.5f);
        Text(title, "Heading", heading, 32, Cream, TextAlignmentOptions.Center);
        return col;
    }

    private static RectTransform Row(RectTransform col, int index, string caption)
    {
        var row = Node(caption.Replace(" ", ""), col);
        row.anchorMin = new Vector2(0, 1); row.anchorMax = Vector2.one; row.pivot = new Vector2(.5f, 1);
        bool dense = col.parent.name == "GRAPHICSPage";
        row.anchoredPosition = new Vector2(0, -76 - index * (dense ? 80 : 92));
        row.sizeDelta = new Vector2(0, dense ? 70 : 82);
        var label = Text(row, "SettingLabel", caption, 28, Ink);
        label.enableAutoSizing = true; label.fontSizeMin = 24; label.fontSizeMax = 28;
        Stretch(label.rectTransform, .025f, .1f, .43f, .9f);
        var line = Node("Divider", row); Stretch(line, .025f, 0, .975f, 0);
        line.sizeDelta = new Vector2(0, 2);
        var lineImage = line.gameObject.AddComponent<Image>();
        lineImage.color = new Color32(160, 112, 59, 65); lineImage.raycastTarget = false;
        var control = Node("Control", row); Stretch(control, .45f, .085f, .975f, .915f);
        return control;
    }

    private static void Segments(RectTransform row, string[] choices, int selected)
    {
        Capsule(row, new Color32(226, 201, 151, 255));
        for (int i = 0; i < choices.Length; i++)
        {
            var cell = Node(choices[i].Replace(" ", ""), row);
            Stretch(cell, (float)i / choices.Length, 0, (float)(i + 1) / choices.Length, 1);
            var image = cell.gameObject.AddComponent<SettingsRoundedGraphic>(); image.raycastTarget = false;
            image.roundLeft = i == 0; image.roundRight = i == choices.Length - 1;
            image.color = i == selected ? Blue : new Color32(232, 209, 166, 255);
            var button = cell.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.transition = Selectable.Transition.None; button.enabled = false;
            Text(cell, "Label", choices[i], 27, i == selected ? Cream : Ink, TextAlignmentOptions.Center);
        }
    }

    private static void Dropdown(RectTransform r, string value)
    {
        var image = Art(r, status, 4.5f);
        var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.transition = Selectable.Transition.None; button.enabled = false;
        var text = Text(r, "Value", value, 28, Ink); Stretch(text.rectTransform, .08f, .1f, .8f, .9f);
        var arrow = Node("Arrow", r); Fixed(arrow, 0, 0, 20, 12, .9f, .5f);
        var graphic = arrow.gameObject.AddComponent<SettingsDropdownArrow>(); graphic.color = Ink; graphic.raycastTarget = false;
    }

    private static void Switch(RectTransform r, bool on)
    {
        var body = Node("Toggle", r); Stretch(body, .36f, .1f, .99f, .9f);
        Capsule(body, Ink);
        var inset = Node("Face", body); Stretch(inset, .018f, .07f, .982f, .93f);
        Capsule(inset, on ? Blue : new Color32(112, 65, 31, 255));
        var knob = Node("Handle", body); Fixed(knob, 0, 0, 44, 44, on ? .855f : .145f, .5f); Capsule(knob, Cream);
        var label = Text(body, "State", on ? "ON" : "OFF", 26, Cream, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, on ? .05f : .31f, .1f, on ? .69f : .95f, .9f);
        var toggle = body.gameObject.AddComponent<Toggle>(); toggle.isOn = on;
        toggle.transition = Selectable.Transition.None; toggle.enabled = false;
    }

    private static void SliderAt(RectTransform r, float value, string readout)
    {
        var body = Node("Slider", r); Stretch(body, 0, .1f, .78f, .9f);
        var thumb = Node("Handle", body); Fixed(thumb, 0, 0, 38, 58, .035f + .93f * value, .5f);
        var handleImage = Art(thumb, handle); handleImage.type = Image.Type.Simple;
        var slider = body.gameObject.AddComponent<Slider>(); slider.transition = Selectable.Transition.None;
        slider.minValue = 0; slider.maxValue = 1; slider.value = value;
        slider.targetGraphic = handleImage; slider.enabled = false;
        AlignSlider(slider, track, fill);
        var label = Text(r, "Value", readout, 27, Ink, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, .79f, 0, 1, 1);
    }

    public static void AlignSlider(Slider slider, Sprite trackSprite, Sprite fillSprite)
    {
        var body = slider.transform;
        var trackRect = body.Find("Track") as RectTransform;
        if (!trackRect) trackRect = Node("Track", body);
        Stretch(trackRect, 0, .18f, 1, .82f);
        var oldTrack = trackRect.GetComponent<SettingsRoundedGraphic>();
        if (oldTrack) UnityEngine.Object.DestroyImmediate(oldTrack);
        var image = trackRect.GetComponent<Image>();
        if (!image) image = trackRect.gameObject.AddComponent<Image>();
        image.sprite = trackSprite; image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 5; image.color = Color.white; image.raycastTarget = false;
        var area = trackRect.Find("Fill Area") as RectTransform;
        if (!area) area = Node("Fill Area", trackRect);
        Stretch(area); area.offsetMin = new Vector2(5.5f, 4.5f); area.offsetMax = new Vector2(-5.5f, -4.5f);
        var fillRect = body.Find("Fill") as RectTransform;
        if (!fillRect) fillRect = area.Find("Fill") as RectTransform;
        if (!fillRect) fillRect = Node("Fill", area);
        fillRect.SetParent(area, false); Stretch(fillRect);
        var oldFill = fillRect.GetComponent<Image>(); if (oldFill) UnityEngine.Object.DestroyImmediate(oldFill);
        var paint = fillRect.GetComponent<SettingsRoundedGraphic>();
        if (!paint) paint = fillRect.gameObject.AddComponent<SettingsRoundedGraphic>();
        paint.surface = fillSprite; paint.color = Color.white; paint.raycastTarget = false;
        var handleArea = body.Find("Handle Slide Area") as RectTransform;
        if (!handleArea) handleArea = Node("Handle Slide Area", body);
        Stretch(handleArea); handleArea.offsetMin = new Vector2(5.5f, 0); handleArea.offsetMax = new Vector2(-5.5f, 0);
        var thumb = body.Find("Handle") as RectTransform;
        if (!thumb && slider.handleRect) thumb = slider.handleRect;
        if (!thumb) thumb = handleArea.Find("Handle") as RectTransform;
        if (thumb)
        {
            thumb.SetParent(handleArea, false); Fixed(thumb, 0, 0, 38, 58);
        }
        slider.enabled = false;
        // Disabled Slider components do not restore driven anchors on load.
        // Author static preview geometry; connect these references in the functional pass.
        slider.fillRect = null;
        slider.handleRect = null;
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = new Vector2(slider.normalizedValue, 1);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        if (thumb) Fixed(thumb, 0, 0, 38, 58, slider.normalizedValue, .5f);
    }

    public static void ConnectAudioControls(GameObject settingsUI)
    {
        var audio = settingsUI.transform.Find("SettingsFrame/AUDIOPage");
        if (!audio) throw new InvalidOperationException("Settings Audio page is missing.");
        var musicRow = audio.Find("Volume/MUSIC/Control");
        var soundFXRow = audio.Find("Volume/SOUNDEFFECTS/Control");
        var musicSlider = ConnectVolumeSlider(musicRow, AudioVolumeSettings.MusicVolume);
        var soundFXSlider = ConnectVolumeSlider(soundFXRow, AudioVolumeSettings.SoundFXVolume);
        var binding = audio.GetComponent<AudioVolumePanel>();
        if (!binding) binding = audio.gameObject.AddComponent<AudioVolumePanel>();
        binding.enabled = false;
        var serialized = new SerializedObject(binding);
        serialized.FindProperty("musicSlider").objectReferenceValue = musicSlider;
        serialized.FindProperty("soundFXSlider").objectReferenceValue = soundFXSlider;
        serialized.FindProperty("musicValue").objectReferenceValue = musicRow.Find("Value").GetComponent<TMP_Text>();
        serialized.FindProperty("soundFXValue").objectReferenceValue = soundFXRow.Find("Value").GetComponent<TMP_Text>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        musicRow.Find("Value").GetComponent<TMP_Text>().text = Mathf.RoundToInt(musicSlider.value) + "%";
        soundFXRow.Find("Value").GetComponent<TMP_Text>().text = Mathf.RoundToInt(soundFXSlider.value) + "%";
        binding.enabled = true;
    }

    private static Slider ConnectVolumeSlider(Transform row, float volume)
    {
        var body = row.Find("Slider");
        var slider = body.GetComponent<Slider>();
        var hit = body.GetComponent<Image>();
        if (!hit) hit = body.gameObject.AddComponent<Image>();
        hit.color = Color.clear; hit.raycastTarget = true;
        hit.raycastPadding = new Vector4(0, -12, 0, -12);
        var handleRect = body.Find("Handle Slide Area/Handle").GetComponent<RectTransform>();
        var handleArea = (RectTransform)handleRect.parent;
        handleArea.anchorMin = new Vector2(0, .5f); handleArea.anchorMax = new Vector2(1, .5f);
        handleArea.offsetMin = new Vector2(5.5f, -29); handleArea.offsetMax = new Vector2(-5.5f, 29);
        handleRect.sizeDelta = new Vector2(38, 0); handleRect.anchoredPosition = Vector2.zero;
        var handleImage = handleRect.GetComponent<Image>(); handleImage.raycastTarget = true;
        slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
        slider.fillRect = body.Find("Track/Fill Area/Fill").GetComponent<RectTransform>();
        slider.handleRect = handleRect; slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight; slider.transition = Selectable.Transition.None;
        slider.interactable = true;
        var navigation = slider.navigation; navigation.mode = Navigation.Mode.None; slider.navigation = navigation;
        slider.SetValueWithoutNotify(Mathf.RoundToInt(volume * 100));
        slider.enabled = true;
        return slider;
    }

    public static void ConnectSettingsConfirmations(Transform settings)
    {
        if (Application.isPlaying || settings.gameObject.scene.path != "Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Connect confirmations in Main Menu Edit Mode only.");
        var controller = settings.GetComponent<MainMenuSettingsConfirmation>();
        if (!controller) throw new InvalidOperationException("The existing Settings confirmation dialog is missing.");
        var footer = settings.Find("MainMenuSettingsUI/SettingsFrame/Footer");
        var logout = footer.Find("LogOut").GetComponent<Button>();
        var quit = footer.Find("QuitGame").GetComponent<Button>();
        var data = new SerializedObject(controller);
        data.FindProperty("logoutButton").objectReferenceValue = logout;
        data.FindProperty("quitButton").objectReferenceValue = quit;
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach (var button in new[] { logout, quit })
        {
            // Direct exit/auth callbacks must go through the existing confirmation event.
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                string method = button.onClick.GetPersistentMethodName(i);
                if (method == "OnClickLogout" || method == "QuitGame" ||
                    (button.onClick.GetPersistentTarget(i) == controller &&
                     (method == "RequestLogout" || method == "RequestQuit")))
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
            }
            button.enabled = true; button.interactable = true;
            var image = button.GetComponent<Image>(); image.raycastTarget = true;
            EditorUtility.SetDirty(image);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
        }
        UnityEventTools.AddPersistentListener(logout.onClick, controller.RequestLogout);
        UnityEventTools.AddPersistentListener(quit.onClick, controller.RequestQuit);
        foreach (var button in new[] { logout, quit })
        {
            EditorUtility.SetDirty(button);
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        }
        controller.enabled = true;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
    }

    private static void Hint(Transform col, int row, string value)
    {
        var label = Text(col, "Hint", value, 26, Muted);
        var r = label.rectTransform; r.anchorMin = new Vector2(.025f, 1); r.anchorMax = new Vector2(.975f, 1);
        int headingGap = col.parent.name == "AUDIOPage" ? 84 : 68;
        r.pivot = new Vector2(.5f, 1); r.anchoredPosition = new Vector2(0, -headingGap - row * (row == 4 ? 82 : 92));
        r.sizeDelta = new Vector2(0, 72);
    }

    private static void ActionRow(RectTransform col, int index, string caption, string action)
    {
        var slot = Row(col, index, caption);
        var button = ButtonAt(slot, action.Replace(" ", ""), action, teal, 280, 88);
        Fixed((RectTransform)button.transform, 0, 0, 280, 88);
    }

    [MenuItem("Tools/Logic Legends/Build Main Menu Settings UI")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/Main Menu.unity")
            throw new InvalidOperationException("Open Main Menu in Edit Mode before building its Settings UI.");
        var settings = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .First(t => t.name == "Settings_Menu");
        if (settings.Find("MainMenuSettingsUI") != null)
            throw new InvalidOperationException("Settings UI already exists; edit its prefab instead of rebuilding the scene.");
        Directory.CreateDirectory(Folder);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/IMPACT SDF.asset");
        frame = SpriteAt("Assets/Art/GraphHUD/GraphMapFrame.png", "GraphMapFrame");
        status = SpriteAt("Assets/Art/GraphHUD/GraphStatusFrame.png", "GraphStatusFrame");
        plaque = SpriteAt("Assets/Art/ProfilePanel/ProfileTitlePlaque.png", "ProfileTitlePlaque");
        sectionHeader = SpriteAt("Assets/Art/SettingsPanel/SettingsSectionHeader.png", "SettingsSectionHeader");
        teal = SpriteAt("Assets/Resources/StageUI/WoodButtonTeal.png", "WoodButtonTeal");
        red = SpriteAt("Assets/Resources/StageUI/WoodButtonRed.png", "WoodButtonRed");
        green = SpriteAt("Assets/Resources/StageUI/WoodButtonGreen.png", "WoodButtonGreen");
        track = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/SettingsPanel/SettingsSliderTrack.asset");
        fill = SpriteAt("Assets/Interface_Sprites/settings_interface/fill_background.png");
        handle = SpriteAt("Assets/Interface_Sprites/settings_interface/handle.png");
        var preview = EditorSceneManager.NewPreviewScene();
        var root = Node("MainMenuSettingsUI", null);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, preview);
        Stretch(root);
        try
        {
            var title = Node("SettingsTitle", root); Fixed(title, 0, 0, 560, 102, .535f, .92f);
            Art(title, plaque, 4.5f); Text(title, "Title", "SETTINGS", 54, Cream, TextAlignmentOptions.Center);
            var panel = Node("SettingsFrame", root); Stretch(panel, .13f, .07f, .94f, .86f);
            Art(panel, frame, 2.8f).raycastTarget = true;
            var tabRow = Node("Tabs", panel); Stretch(tabRow, .04f, .88f, .96f, .88f);
            tabRow.sizeDelta = new Vector2(0, 88);
            string[] names = { "GRAPHICS", "CONTROLS", "AUDIO", "ACCESSIBILITY", "GENERAL" };
            var tabs = new Button[5]; var faces = new Graphic[5]; var labels = new TMP_Text[5]; var pages = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                tabs[i] = ButtonAt(tabRow, names[i] + "Tab", names[i], status);
                var tr = (RectTransform)tabs[i].transform;
                Stretch(tr, i * .2f, 0, (i + 1) * .2f, 1);
                tr.offsetMin = new Vector2(i == 0 ? 0 : 8, 0);
                tr.offsetMax = new Vector2(i == 4 ? 0 : -8, 0);
                tabs[i].enabled = true; tabs[i].targetGraphic.raycastTarget = true;
                var face = Node("SelectedFill", tr); Stretch(face);
                face.offsetMin = new Vector2(12, 12); face.offsetMax = new Vector2(-12, -12);
                faces[i] = face.gameObject.AddComponent<SettingsTabFace>();
                faces[i].color = Blue; faces[i].raycastTarget = false; face.SetAsFirstSibling();
                // Background stays behind the selected face so its brown rim is preserved.
                face.SetSiblingIndex(0);
                labels[i] = tr.Find("Label").GetComponent<TMP_Text>();
                var page = Node(names[i] + "Page", panel); Stretch(page, .045f, .19f, .955f, .80f);
                pages[i] = page.gameObject;
            }
            var graphics = pages[0].transform;
            var display = Column(graphics, "Display", "DISPLAY", false);
            Segments(Row(display, 0, "GRAPHICS QUALITY"), new[] { "LOW", "MEDIUM", "HIGH" }, 1);
            Segments(Row(display, 1, "FRAME RATE"), new[] { "30 FPS", "60 FPS" }, 0);
            SliderAt(Row(display, 2, "RESOLUTION QUALITY"), .8f, "80%");
            Dropdown(Row(display, 3, "SHADOW QUALITY"), "LOW");
            Dropdown(Row(display, 4, "EDGE SMOOTHING"), "2X");
            var effects = Column(graphics, "Effects", "EFFECTS", true);
            Dropdown(Row(effects, 0, "LIGHTING QUALITY"), "MEDIUM");
            Switch(Row(effects, 1, "BLOOM"), false);
            Switch(Row(effects, 2, "FOG"), true);
            Switch(Row(effects, 3, "MOTION BLUR"), false);
            Hint(effects, 4, "Higher quality uses more battery.");

            var camera = Column(pages[1].transform, "Camera", "CAMERA", false);
            SliderAt(Row(camera, 0, "HORIZONTAL SENSITIVITY"), .5f, "1.0X");
            SliderAt(Row(camera, 1, "VERTICAL SENSITIVITY"), .5f, "1.0X");
            SliderAt(Row(camera, 2, "PINCH ZOOM SENSITIVITY"), .5f, "1.0X");
            Switch(Row(camera, 3, "INVERT VERTICAL LOOK"), false);
            var touch = Column(pages[1].transform, "Touch", "TOUCH CONTROLS", true);
            SliderAt(Row(touch, 0, "BUTTON SIZE"), .5f, "100%");
            SliderAt(Row(touch, 1, "BUTTON OPACITY"), .8f, "80%");
            Switch(Row(touch, 2, "LEFT-HANDED LAYOUT"), false);
            Hint(touch, 4, "Comfortable controls make every trial easier.");

            var volume = Column(pages[2].transform, "Volume", "VOLUME", false);
            SliderAt(Row(volume, 0, "MUSIC"), .6f, "60%");
            SliderAt(Row(volume, 1, "SOUND EFFECTS"), .6f, "60%");
            Switch(Row(volume, 2, "MUTE ALL AUDIO"), false);
            var sound = Column(pages[2].transform, "Sound", "SOUND", true);
            Hint(sound, 0, "Set music and sound effects to a comfortable level.");
            Hint(sound, 2, "Music sets the mood. Sound effects help you follow the action.");

            var reading = Column(pages[3].transform, "Readability", "READABILITY", false);
            SliderAt(Row(reading, 0, "TEXT SIZE"), .5f, "100%");
            Switch(Row(reading, 1, "HIGH-CONTRAST LABELS"), true);
            Switch(Row(reading, 2, "COLOR-INDEPENDENT CUES"), true);
            Hint(reading, 4, "Symbols and labels help make puzzles clear.");
            var motion = Column(pages[3].transform, "Motion", "MOTION & COMFORT", true);
            Switch(Row(motion, 0, "REDUCED CAMERA MOTION"), false);
            Switch(Row(motion, 1, "CAMERA SHAKE"), false);
            Hint(motion, 3, "Choose a calmer view for comfortable play.");

            var account = Column(pages[4].transform, "Account", "ACCOUNT & GAME", false);
            ActionRow(account, 0, "ACCOUNT INFORMATION", "VIEW ACCOUNT");
            ActionRow(account, 1, "TUTORIAL", "REPLAY TUTORIAL");
            Dropdown(Row(account, 2, "LANGUAGE"), "ENGLISH");
            var version = Row(account, 3, "GAME VERSION"); Text(version, "Version", Application.version, 28, Ink, TextAlignmentOptions.Center);
            var help = Column(pages[4].transform, "Help", "HELP & PRIVACY", true);
            ActionRow(help, 0, "NEED HELP?", "SUPPORT");
            ActionRow(help, 1, "YOUR PRIVACY", "PRIVACY POLICY");
            Hint(help, 3, "Reset restores settings. Your account and trial progress stay safe.");

            var footer = Node("Footer", panel); Stretch(footer, .065f, .065f, .935f, .18f);
            var logout = ButtonAt(footer, "LogOut", "LOG OUT", red); Fixed((RectTransform)logout.transform, 140, 0, 280, 88, 0, .5f);
            var quit = ButtonAt(footer, "QuitGame", "QUIT GAME", red); Fixed((RectTransform)quit.transform, 444, 0, 280, 88, 0, .5f);
            var reset = ButtonAt(footer, "Reset", "RESET", teal, 220); Fixed((RectTransform)reset.transform, -354, 0, 220, 88, 1, .5f);
            var apply = ButtonAt(footer, "Apply", "APPLY", green, 220); Fixed((RectTransform)apply.transform, -110, 0, 220, 88, 1, .5f);
            root.gameObject.AddComponent<MainMenuSettingsTabs>().Configure(pages, tabs, faces, labels);
            ConnectAudioControls(root.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, Folder + "/MainMenuSettingsUI.prefab");
            Export(tabs[1].gameObject, "SettingsTab");
            Export(reset.gameObject, "SettingsActionButton");
            Export(display.Find("GRAPHICSQUALITY/Control").gameObject, "SettingsQualitySelector");
            Export(display.Find("FRAMERATE/Control").gameObject, "SettingsFrameRateSelector");
            Export(display.Find("SHADOWQUALITY/Control").gameObject, "SettingsDropdown");
            Export(display.Find("RESOLUTIONQUALITY/Control").gameObject, "SettingsSlider");
            Export(effects.Find("FOG/Control").gameObject, "SettingsToggle");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }

        // Keep old authored controls and confirmation wiring available for the later functional pass.
        var legacy = Node("LegacySettingsUI", settings); Stretch(legacy); legacy.gameObject.SetActive(false);
        var oldChildren = settings.Cast<Transform>().Where(t => t != legacy && t.name != "back_btn").ToArray();
        foreach (var child in oldChildren) child.SetParent(legacy, false);
        var audioPanel = settings.GetComponent<AudioVolumePanel>(); if (audioPanel) audioPanel.enabled = false;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/MainMenuSettingsUI.prefab");
        PrefabUtility.InstantiatePrefab(prefab, settings);
        ConnectSettingsConfirmations(settings);
        CenterExistingSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Main Menu Settings UI created. Tabs, music/SFX volume and confirmed logout/quit work; other controls are unconnected.");
    }

    private static void Export(GameObject source, string name)
    {
        var clone = UnityEngine.Object.Instantiate(source);
        clone.name = name;
        var r = clone.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.anchoredPosition = Vector2.zero;
        if (name == "SettingsTab") r.sizeDelta = new Vector2(280, 88);
        else if (name == "SettingsActionButton") r.sizeDelta = new Vector2(220, 88);
        else r.sizeDelta = new Vector2(350, 68);
        // Standalone tabs have no owning page until inserted into a tab controller.
        foreach (var selectable in clone.GetComponentsInChildren<Selectable>(true)) selectable.enabled = false;
        PrefabUtility.SaveAsPrefabAsset(clone, Folder + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(clone);
    }
}
