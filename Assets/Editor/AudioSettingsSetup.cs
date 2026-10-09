using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors settings in the owning scenes, so controls are visible and editable outside Play Mode.</summary>
public static class AudioSettingsSetup
{
    private const string Art = "Assets/Interface_Sprites/settings_interface/";
    private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/IMPACT SDF.asset");
    private static Sprite Sprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();

    public static void Configure(Scene scene)
    {
        if (scene.name == "Main Menu") ConfigureMainMenu(scene);
        // Gameplay pause controls are authored in the shared Canvas 1 prefab.
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Resources/Audio/LogicLegendsAudio.mixer");
        var sfx = mixer.FindMatchingGroups("SFX").First(group => group.name == "SFX");
        foreach (var root in scene.GetRootGameObjects())
            foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
                if (source.outputAudioMixerGroup == null) { source.outputAudioMixerGroup = sfx; EditorUtility.SetDirty(source); }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static Transform Find(Scene scene, string name) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).First(transform => transform.name == name);

    private static void ConfigureMainMenu(Scene scene)
    {
        var panel = Find(scene, "Settings_Menu");
        var music = panel.GetComponentsInChildren<Slider>(true).First(slider => slider.name == "Music Slider");
        var sfx = panel.GetComponentsInChildren<Slider>(true).First(slider => slider.name.StartsWith("SFX"));
        ConfigureSlider(music, new Vector2(-50, 135));
        ConfigureSlider(sfx, new Vector2(-50, -52));
        var musicValue = Text(panel, "MusicVolumeValue", "100%", new Vector2(335, 135), new Vector2(125, 72), 40);
        var sfxValue = Text(panel, "SoundFXVolumeValue", "100%", new Vector2(335, -52), new Vector2(125, 72), 40);
        Bind(panel.gameObject, music, sfx, musicValue, sfxValue);
        if (!scene.GetRootGameObjects().Any(root => root.name == "MainMenuMusic"))
        {
            var go = new GameObject("MainMenuMusic", typeof(AudioSource));
            SceneManager.MoveGameObjectToScene(go, scene);
            var source = go.GetComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/WORLDBGM/LOFI BG.mp3");
            source.loop = true; source.playOnAwake = true; source.spatialBlend = 0; source.volume = 0.35f;
            source.outputAudioMixerGroup = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Resources/Audio/LogicLegendsAudio.mixer")
                .FindMatchingGroups("Music").First(group => group.name == "Music");
        }
    }

    public static void ConfigurePausePrefab(GameObject prefabRoot)
    {
        var pause = prefabRoot.GetComponentsInChildren<Transform>(true).First(transform => transform.name == "PausePanel");
        var card = pause.Find("MenuContainer");
        var modalCanvas = pause.GetComponent<Canvas>();
        if (modalCanvas == null) modalCanvas = pause.gameObject.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true; modalCanvas.sortingOrder = 30000;
        var modalData = new SerializedObject(modalCanvas);
        modalData.FindProperty("m_OverrideSorting").boolValue = true;
        modalData.FindProperty("m_SortingOrder").intValue = 30000;
        modalData.ApplyModifiedPropertiesWithoutUndo();
        if (pause.GetComponent<GraphicRaycaster>() == null) pause.gameObject.AddComponent<GraphicRaycaster>();
        foreach (var layout in card.GetComponents<LayoutGroup>()) Object.DestroyImmediate(layout);
        var fitter = card.GetComponent<ContentSizeFitter>();
        if (fitter != null) Object.DestroyImmediate(fitter);
        var unusedSafeArea = pause.Find("PauseSafeArea");
        if (unusedSafeArea != null && unusedSafeArea.childCount == 0) Object.DestroyImmediate(unusedSafeArea.gameObject);
        if (card.GetComponent<PauseSettingsLayout>() == null) card.gameObject.AddComponent<PauseSettingsLayout>();
        Place((RectTransform)card, Vector2.zero, new Vector2(1000, 900));
        var backdrop = pause.GetComponent<Image>();
        if (backdrop != null) { backdrop.sprite = null; backdrop.color = new Color(0.015f, 0.025f, 0.035f, 0.82f); backdrop.raycastTarget = true; }
        var frame = card.GetComponent<Image>();
        frame.sprite = Sprite(Art + "settings_background.png"); frame.type = UnityEngine.UI.Image.Type.Simple; frame.color = Color.white;
        frame.raycastTarget = false;
        var banner = Image(card, "SettingsBanner", Sprite(Art + "settings_banner.png"));
        Place(banner.rectTransform, new Vector2(0, 380), new Vector2(500, 123));
        Text(card, "MusicLabel", "MUSIC", new Vector2(-50, 215), new Vector2(580, 62), 40);
        Text(card, "SoundFXLabel", "SOUND FX", new Vector2(-50, 15), new Vector2(580, 62), 40);
        var music = Child(card, "MusicSlider").GetComponent<Slider>();
        if (music == null) music = card.Find("MusicSlider").gameObject.AddComponent<Slider>();
        var sfx = Child(card, "SoundFXSlider").GetComponent<Slider>();
        if (sfx == null) sfx = card.Find("SoundFXSlider").gameObject.AddComponent<Slider>();
        ConfigureSlider(music, new Vector2(-50, 135));
        ConfigureSlider(sfx, new Vector2(-50, -65));
        var musicValue = Text(card, "MusicVolumeValue", "100%", new Vector2(335, 135), new Vector2(125, 72), 40);
        var sfxValue = Text(card, "SoundFXVolumeValue", "100%", new Vector2(335, -65), new Vector2(125, 72), 40);
        var hint = Text(card, "VolumeSaveHint", "Volume changes are saved automatically.", new Vector2(0, -150), new Vector2(780, 55), 28);
        hint.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        Place((RectTransform)card.Find("ResumeButton"), new Vector2(-365, 310), new Vector2(112, 112));
        Place((RectTransform)card.Find("ReturnButton"), new Vector2(0, -240), new Vector2(590, 108));
        Place((RectTransform)card.Find("QuitButton"), new Vector2(0, -365), new Vector2(440, 108));
        StylePauseAction(card.Find("ReturnButton").GetComponent<Button>(), "RETURN TO MAIN MENU");
        StylePauseAction(card.Find("QuitButton").GetComponent<Button>(), "QUIT GAME");
        Bind(pause.gameObject, music, sfx, musicValue, sfxValue);
    }

    private static void StylePauseAction(Button button, string title)
    {
        foreach (var oldLabel in button.GetComponentsInChildren<UnityEngine.UI.Text>(true)) Object.DestroyImmediate(oldLabel.gameObject);
        var label = Text(button.transform, "Label", title, Vector2.zero, Vector2.zero, 38);
        Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(28, 12); label.rectTransform.offsetMax = new Vector2(-28, -12);
        StageUITheme.SkinButton(button);
        button.GetComponent<Image>().sprite = Resources.LoadAll<Sprite>("StageUI/WoodButtonRed").First();
        label.fontSharedMaterial = Font.material;
        label.fontSizeMin = 26; label.fontSizeMax = 38;
    }

    private static void Bind(GameObject panel, Slider music, Slider soundFX, TMP_Text musicValue, TMP_Text soundFXValue)
    {
        var binding = panel.GetComponent<AudioVolumePanel>();
        if (binding == null) binding = panel.AddComponent<AudioVolumePanel>();
        var serialized = new SerializedObject(binding);
        serialized.FindProperty("musicSlider").objectReferenceValue = music;
        serialized.FindProperty("soundFXSlider").objectReferenceValue = soundFX;
        serialized.FindProperty("musicValue").objectReferenceValue = musicValue;
        serialized.FindProperty("soundFXValue").objectReferenceValue = soundFXValue;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureSlider(Slider slider, Vector2 position)
    {
        Place((RectTransform)slider.transform, position, new Vector2(590, 100));
        var hit = slider.GetComponent<Image>();
        if (hit == null) hit = slider.gameObject.AddComponent<Image>();
        hit.color = Color.clear; hit.raycastTarget = true;
        var background = Image(slider.transform, "Background", Sprite(Art + "music_slider_background.png"));
        Place(background.rectTransform, Vector2.zero, new Vector2(590, 65));
        var fillArea = Child(slider.transform, "Fill Area"); Stretch(fillArea);
        fillArea.offsetMin = new Vector2(17, 28); fillArea.offsetMax = new Vector2(-17, -28);
        var originalFill = fillArea.Find("Fill");
        var clip = Child(fillArea, "RoundedFill"); Stretch(clip);
        var shape = clip.GetComponent<RoundedSliderMask>();
        if (shape == null) shape = clip.gameObject.AddComponent<RoundedSliderMask>();
        shape.color = Color.white; shape.raycastTarget = false;
        var mask = clip.GetComponent<Mask>();
        if (mask == null) mask = clip.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        if (originalFill != null) originalFill.SetParent(clip, false);
        var fill = Image(clip, "Fill", Sprite(Art + "fill_background.png"));
        Stretch(fill.rectTransform); fill.type = UnityEngine.UI.Image.Type.Simple;
        var handleArea = Child(slider.transform, "Handle Slide Area"); Stretch(handleArea);
        handleArea.offsetMin = new Vector2(17, 4); handleArea.offsetMax = new Vector2(-17, -4);
        var handle = Image(handleArea, "Handle", Sprite(Art + "handle.png"));
        handle.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        handle.rectTransform.anchoredPosition = Vector2.zero; handle.rectTransform.sizeDelta = new Vector2(72, 0);
        handle.raycastTarget = true;
        slider.fillRect = clip; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true; slider.SetValueWithoutNotify(100);
        var colors = slider.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1, 0.94f, 0.8f);
        colors.pressedColor = new Color(0.85f, 0.75f, 0.6f); colors.disabledColor = new Color(0.6f, 0.6f, 0.6f);
        slider.colors = colors; slider.transition = Selectable.Transition.ColorTint;
        EditorUtility.SetDirty(slider);
    }

    private static RectTransform Child(Transform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null) return existing;
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    private static Image Image(Transform parent, string name, Sprite sprite)
    {
        var rect = Child(parent, name);
        var image = rect.GetComponent<Image>();
        if (image == null) image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = Color.white; image.raycastTarget = false;
        return image;
    }
    private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var rect = Child(parent, name);
        var text = rect.GetComponent<TextMeshProUGUI>();
        if (text == null) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        Place(rect, position, size);
        text.text = value; text.font = Font; text.fontSharedMaterial = Font.material; text.fontSize = fontSize;
        text.color = new Color(1f, 0.92f, 0.75f); text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f); rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}