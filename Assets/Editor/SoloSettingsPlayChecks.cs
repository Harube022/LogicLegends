#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Offline settings integration checks. No Firebase initialization, sign-out, real navigation or wallet writes.</summary>
public static class SoloSettingsPlayChecks
{
    const string Flag = "SoloSettings.Checks", Fixture = "Assets/Editor/SoloSettingsFixture.unity", Report = "Library/SoloSettingsChecks.txt";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    static string uid;
    [MenuItem("Logic Legends/Settings/Check Shared Solo Settings In Play Mode")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the scene and exit Play Mode first.");
        string originalScene = SceneManager.GetActiveScene().path;
        SessionState.SetString(Flag + ".Scene", originalScene == Fixture ? "Assets/Scenes/Main Menu.unity" : originalScene);
        SessionState.SetString(Flag + ".Data", JsonUtility.ToJson(SoloGameSettings.Current));
        foreach (string key in new[] { SoloGameSettings.PreferenceKey, AudioVolumeSettings.MusicPreference, AudioVolumeSettings.SoundFXPreference })
        { SessionState.SetBool(Flag + key, PlayerPrefs.HasKey(key)); SessionState.SetString(Flag + key + ".Value", key == SoloGameSettings.PreferenceKey ? PlayerPrefs.GetString(key, "") : PlayerPrefs.GetFloat(key, 1).ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        SessionState.SetFloat(Flag + ".Listener", AudioListener.volume);
        SessionState.SetBool(Flag + ".RunInBackground", Application.runInBackground);
        SessionState.SetInt(Flag + ".FPS", Application.targetFrameRate); SessionState.SetInt(Flag + ".Vsync", QualitySettings.vSyncCount);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Lighting Test Floor"; floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(20, 1, 20);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.55f, .5f, .4f) };
        // A saved test material is unnecessary; use the project's existing opaque lit material.
        UnityEngine.Object.DestroyImmediate(material);
        floor.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("Settings/SoloOpaqueLit");
        for (int i = 0; i < 3; i++) { var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "Contact Shading Block"; cube.transform.position = new Vector3(i * 2 - 2, 1, 0); cube.transform.localScale = new Vector3(1.5f, 2, 1.5f); cube.GetComponent<Renderer>().sharedMaterial = floor.GetComponent<Renderer>().sharedMaterial; }
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(CameraTargetController)).GetComponent<Camera>(); camera.tag = "MainCamera"; camera.transform.position = new Vector3(10, 8, -12); camera.transform.LookAt(new Vector3(0, 1, 0));
        var sun = new GameObject("Key light", typeof(Light)).GetComponent<Light>(); sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(45, -25, 0); sun.intensity = 1.1f; sun.shadows = LightShadows.Soft; RenderSettings.sun = sun;
        BuildAuthoredControls(scene);
        EditorSceneManager.SaveScene(scene, Fixture);
        uid = "settings-qa-" + Guid.NewGuid().ToString("N"); SessionState.SetString(Flag + ".Uid", uid); SessionState.SetBool(Flag, true);
        File.WriteAllText(Report, "Offline shared settings fixture; test transport, no real account operations.\n");
        EditorApplication.isPlaying = true;
    }
    static void BuildAuthoredControls(Scene fixture)
    {
        var canvas = new GameObject("Settings Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var source = EditorSceneManager.OpenScene("Assets/Scenes/PRELIM.unity", OpenSceneMode.Additive);
        try
        {
            var mobile = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MobileInputUI>(true)).First(m => m.HasJoystick);
            Transform joystick = mobile.transform;
            while (joystick.name != "Joystick") joystick = joystick.parent;
            var host = joystick.parent;
            foreach (var control in new[] { joystick, host.Find("JumpButton"), host.Find("InteractButton") })
            {
                var copy = UnityEngine.Object.Instantiate(control.gameObject, canvas.transform, false); copy.name = control.name; copy.SetActive(true);
                foreach (var button in copy.GetComponentsInChildren<Button>(true)) button.onClick = new Button.ButtonClickedEvent();
            }
        }
        finally { SceneManager.SetActiveScene(fixture); EditorSceneManager.CloseScene(source, true); }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        uid = SessionState.GetString(Flag + ".Uid", "");
        typeof(TrialBackendClient).GetField("ValidationUid", Static).SetValue(null, uid);
        typeof(TrialBackendClient).GetField("ValidationTransport", Static).SetValue(null, (Func<string, string, Task<string>>)((path, json) => Task.FromResult("{\"admin\":false}")));
        typeof(SoloEconomyConnection).GetField("ValidationEnabled", Static).SetValue(null, (bool?)false);
        TeleportManager.ValidationNavigation = destination => { };
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Loaded()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        new GameObject("Settings QA Runner").AddComponent<TrialAccountQaRunner>().StartCoroutine(Guard(Run()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        var stack = new System.Collections.Generic.Stack<IEnumerator>(); stack.Push(routine);
        while (stack.Count > 0)
        {
            object value;
            try { var next = stack.Peek(); if (!next.MoveNext()) { stack.Pop(); continue; } value = next.Current; }
            catch (Exception e) { File.AppendAllText(Report, "FAIL " + e + "\n"); Debug.LogException(e); EditorApplication.isPlaying = false; yield break; }
            if (value is IEnumerator child) stack.Push(child); else yield return value;
        }
        File.AppendAllText(Report, "ALL SHARED SETTINGS CHECKS PASSED\n"); EditorApplication.isPlaying = false;
    }
    static IEnumerator Run()
    {
        Application.runInBackground = true; yield return null; yield return null;
        var legacy = new SoloSettingsData { graphicsQuality = 2, lightingQuality = 2, shadowQuality = 3, frameRate = 60, motionBlur = true, horizontalSensitivity = 1.35f, music = .45f };
        PlayerPrefs.SetString(SoloGameSettings.PreferenceKey, JsonUtility.ToJson(legacy).Replace("\"mobilePerformanceVersion\":1,", ""));
        typeof(SoloGameSettings).GetField("current", Static).SetValue(null, null);
        var migrated = SoloGameSettings.Current;
        Check(migrated.graphicsQuality == 0 && migrated.lightingQuality == 0 && migrated.shadowQuality == 0 && migrated.frameRate == 30 && !migrated.motionBlur, "older graphics preferences migrate to the Android baseline");
        Check(Mathf.Approximately(migrated.horizontalSensitivity, 1.35f) && Mathf.Approximately(migrated.music, .45f), "graphics migration preserves control and audio preferences");
        SoloGameSettings.Apply(new SoloSettingsData());
        var canvas = GameObject.Find("Settings Test Canvas");
        var runtime = UnityEngine.Object.FindFirstObjectByType<SoloSettingsRuntime>();
        var touchButton = canvas.transform.Find("JumpButton").GetComponent<Button>();
        var touchRect = (RectTransform)touchButton.transform;
        var touchAnchor = touchRect.anchorMin; var touchScale = touchRect.localScale;
        var touchGroup = touchRect.GetComponent<CanvasGroup>(); float touchAlpha = touchGroup != null ? touchGroup.alpha : 1;
        var joystick = canvas.transform.Find("Joystick") as RectTransform;
        var joystickPosition = joystick.anchoredPosition;
        var background = joystick.GetComponentInChildren<MobileInputUI>().transform as RectTransform;
        var backgroundAnchor = background.anchorMin; var backgroundScale = background.localScale;
        var lookArea = StageUITheme.Rect("LookArea", canvas.transform); StageUITheme.Stretch(lookArea, new Vector2(.5f, 0), Vector2.one);
        lookArea.gameObject.AddComponent<MobileLookInput>();
        for (int trial = 1; trial <= 10; trial++)
        {
            foreach (var old in UnityEngine.Object.FindObjectsByType<SoloTrialVisuals>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            var previousScene = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene(TrialCatalog.Get(trial).Scene);
            foreach (var root in previousScene.GetRootGameObjects()) SceneManager.MoveGameObjectToScene(root, scene);
            SceneManager.SetActiveScene(scene); yield return SceneManager.UnloadSceneAsync(previousScene);
            typeof(SoloSettingsRuntime).GetMethod("SceneLoaded", Hidden).Invoke(runtime, new object[] { scene, LoadSceneMode.Single });
            yield return null; yield return null; yield return new WaitForSecondsRealtime(.1f);
            var pause = StageUITheme.Rect("PausePanel", canvas.transform); StageUITheme.Stretch(pause, Vector2.zero, Vector2.one); pause.gameObject.SetActive(false);
            var oldContent = StageUITheme.Rect("Old pause contents", pause);
            var menu = new GameObject("Trial Level Menu").AddComponent<LevelMenu>(); typeof(LevelMenu).GetField("pausePanel", Hidden).SetValue(menu, pause.gameObject);
            menu.PrepareSoloSettings();
            var panel = pause.GetComponentInChildren<GameSettingsPanel>(true);
            Check(panel != null && !oldContent.gameObject.activeSelf, trial + ": shared menu replaces legacy contents");
            var look = Camera.main.GetComponent<CameraTargetController>(); look.enabled = true; Time.timeScale = 1;
            menu.OpenPause(); yield return null;
            Check(Time.timeScale == 0 && !look.enabled && pause.gameObject.activeInHierarchy, trial + ": Settings pauses time and camera");
            Check(panel.transform.Find("SettingsFrame/Footer/LogOut/Label").GetComponent<TMP_Text>().text == "MAIN MENU", trial + ": Main Menu replaces Log Out");
            Check(!panel.GetComponentsInChildren<TMP_Text>(true).Any(t => new[] { "RESOLUTION QUALITY", "EDGE SMOOTHING", "BLOOM", "INVERT VERTICAL LOOK", "CAMERA SHAKE", "REDUCED CAMERA MOTION" }.Contains(t.text)), trial + ": removed controls absent");
            var frame = panel.transform.Find("SettingsFrame");
            CheckSteadyFixture(trial);
            if (trial == 1)
            {
                var lowPipeline = QualitySettings.renderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                Check(lowPipeline != null && Mathf.Approximately(lowPipeline.renderScale, .8f) && lowPipeline.shadowDistance == 0 && lowPipeline.msaaSampleCount == 1, "Android baseline uses 80 percent world resolution and no real-time shadow range");
                Check(!lowPipeline.supportsCameraDepthTexture && !lowPipeline.supportsCameraOpaqueTexture && !lowPipeline.supportsHDR && !lowPipeline.supportsAdditionalLightShadows, "Android baseline avoids depth, opaque, HDR and additional shadow passes");
                var renderer = Resources.Load<UnityEngine.Rendering.Universal.UniversalRendererData>("Settings/SoloRenderer0");
                Check(renderer.rendererFeatures.Count == 0, "Android baseline has no screen-space ambient occlusion feature");
                Check(lowPipeline.additionalLightsRenderingMode == UnityEngine.Rendering.Universal.LightRenderingMode.PerPixel && lowPipeline.maxAdditionalLightsCount == 4, "Low lighting renders local lamp pools per pixel with a four-light budget");
                CheckUnlitFixture();
                var audioOnly = SoloGameSettings.Current.Copy(); audioOnly.music = .55f; SoloGameSettings.Apply(audioOnly);
                Check(QualitySettings.renderPipeline == lowPipeline, "audio changes reuse the current render pipeline");
                foreach (var choice in new[] { "LOW", "MEDIUM", "HIGH" })
                {
                    int shadow = panel.Draft.shadowQuality, lighting = panel.Draft.lightingQuality;
                    frame.Find("GRAPHICSPage/Display/GRAPHICSQUALITY/Control/" + choice).GetComponent<Button>().onClick.Invoke();
                    Check(panel.Draft.shadowQuality == shadow && panel.Draft.lightingQuality == lighting, choice + " graphics selection leaves shadow and lighting quality unchanged");
                }
                panel.ResetDraft();
                frame.Find("CONTROLSPage/Camera/HORIZONTALSENSITIVITY/Control").GetComponentInChildren<Slider>(true).value = 1.7f;
                frame.Find("CONTROLSPage/Camera/VERTICALSENSITIVITY/Control").GetComponentInChildren<Slider>(true).value = .8f;
                frame.Find("CONTROLSPage/Camera/PINCHZOOMSENSITIVITY/Control").GetComponentInChildren<Slider>(true).value = 1.6f;
                frame.Find("CONTROLSPage/Touch/BUTTONOPACITY/Control").GetComponentInChildren<Slider>(true).value = .6f;
                frame.Find("CONTROLSPage/Touch/BUTTONSIZE/Control").GetComponentInChildren<Slider>(true).value = 1.15f;
                frame.Find("CONTROLSPage/Touch/LEFT-HANDEDLAYOUT/Control").GetComponentInChildren<Toggle>(true).isOn = true;
                frame.Find("GRAPHICSPage/Display/FRAMERATE/Control/60FPS").GetComponent<Button>().onClick.Invoke();
                frame.Find("AUDIOPage/Volume/MUTEALLAUDIO/Control").GetComponentInChildren<Toggle>(true).isOn = true;
                frame.Find("Footer/Apply").GetComponent<Button>().onClick.Invoke(); yield return null;
                Check(Mathf.Approximately(SoloGameSettings.LookMultiplier.x, 1.7f) && Mathf.Approximately(SoloGameSettings.ZoomMultiplier, 1.6f), "camera preferences reach gameplay inputs");
                Check(SoloGameSettings.LeftHanded && Mathf.Approximately(AudioListener.volume, 0) && Application.targetFrameRate == 60, "handedness, mute and FPS apply");
                yield return new WaitForSecondsRealtime(.6f);
                Check(Mathf.Abs(touchRect.anchorMin.x - (1 - touchAnchor.x)) < .001f && Mathf.Abs(touchRect.localScale.x - touchScale.x * 1.15f) < .001f && Mathf.Abs(touchRect.GetComponent<CanvasGroup>().alpha - touchAlpha * .6f) < .001f, "authored touch button mirrors, resizes and changes opacity");
                var joystickBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, background);
                var jumpBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, touchRect);
                var handBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas.transform, canvas.transform.Find("InteractButton"));
                Check(joystickBounds.center.x > 0 && jumpBounds.center.x < 0 && handBounds.center.x < 0 && !joystickBounds.Intersects(jumpBounds) && !joystickBounds.Intersects(handBounds), "left-handed authored joystick is on the right, clear of both action buttons");
                Check(background.anchorMin == backgroundAnchor && background.localScale == backgroundScale, "joystick background is not mirrored or scaled twice");
                var touchSettings = UnityEngine.Object.FindFirstObjectByType<SoloTouchSettings>();
                for (int update = 0; update < 3; update++)
                {
                    joystick.anchoredPosition = joystickPosition;
                    typeof(SoloTouchSettings).GetMethod("LateUpdate", Hidden).Invoke(touchSettings, null);
                    Check(joystick.anchorMin.x == 1 && Mathf.Abs(joystick.anchoredPosition.x + joystickPosition.x) < .001f, "partial stage layout updates preserve the mirrored joystick " + update);
                }
                Check(lookArea.anchorMin.x == 0 && lookArea.anchorMax.x == .5f && lookArea.localScale == Vector3.one, "left-handed layout moves the look surface without resizing it");
                var status = frame.Find("SettingsFeedback").GetComponent<TMP_Text>();
                Check(status.text == "Changes Saved" && status.color.g > status.color.r * 2 && status.color.g > status.color.b * 2, "save feedback remains exactly Changes Saved in green");
                Capture(canvas, "ChangesSaved");
                menu.ClosePause(); yield return null; Capture(canvas, "LeftHandedControls");
                var left = SoloGameSettings.Current.Copy(); var right = left.Copy(); right.leftHanded = false; SoloGameSettings.Apply(right); yield return null;
                Check(joystick.anchorMin.x == 0 && Mathf.Abs(joystick.anchoredPosition.x - joystickPosition.x) < .001f, "right-handed layout restores the whole joystick");
                Capture(canvas, "RightHandedControls"); SoloGameSettings.Apply(left); yield return null;
                menu.ClosePause(); look.SetViewAngles(0, 0);
                typeof(MobileLookInput).GetField("<LookDelta>k__BackingField", Static).SetValue(null, new Vector2(10, 10));
                typeof(CameraTargetController).GetMethod("LateUpdate", Hidden).Invoke(look, null);
                Check(Mathf.Abs(Mathf.DeltaAngle(0, look.transform.eulerAngles.y) - 2.55f) < .01f && Mathf.Abs(Mathf.DeltaAngle(0, look.transform.eulerAngles.x) + 1.2f) < .01f, "actual camera yaw and pitch use independent sensitivities");
                menu.OpenPause(); yield return null;
                typeof(SoloGameSettings).GetField("current", Static).SetValue(null, null);
                Check(Mathf.Approximately(SoloGameSettings.Current.horizontalSensitivity, 1.7f), "saved preferences survive cache reload");
                yield return CheckSettingsDialogs(menu, panel, canvas);
                // Native options stay anchored to the painted dropdown control.
                var dropdown = frame.Find("GRAPHICSPage/Effects/LIGHTINGQUALITY/Control").GetComponent<TMP_Dropdown>();
                dropdown.Show(); yield return new WaitForSecondsRealtime(.12f);
                Check(dropdown.IsExpanded && !panel.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Settings Dialog"), "lighting options open as an inline dropdown");
                Capture(canvas, "InlineDropdown");
                var option = dropdown.GetComponentsInChildren<Toggle>().Last(); option.isOn = true;
                Check(panel.Draft.lightingQuality == 2, "lighting dropdown selection works"); panel.Apply(); yield return null;
                Check(QualitySettings.renderPipeline != null && QualitySettings.renderPipeline.name.Contains("Runtime"), "solo pipeline is a runtime copy");
                // Return is confirmed and goes through navigation without signing out.
                string destination = null; TeleportManager.ValidationNavigation = value => destination = value;
                frame.Find("Footer/LogOut").GetComponent<Button>().onClick.Invoke(); yield return null;
                Check(destination == null && Time.timeScale == 0, "Main Menu request waits for confirmation");
                var card = Dialog(panel).transform.Find("ConfirmationCard") as RectTransform;
                Check(card != null && card.sizeDelta == new Vector2(960, 520), "Main Menu confirmation uses the compact painted card");
                CheckFullScreenDialog(panel, canvas, "Main Menu");
                Capture(canvas, "MainMenuConfirmation");
                DialogButton(panel, "MAIN MENU").onClick.Invoke(); yield return null;
                Check(destination == "Main Menu" && Time.timeScale == 1, "confirmed Main Menu navigation restores time");
                menu.ClosePause();
            }
            else
            {
                bool originalFog = SoloGameSettings.Current.fog;
                frame.Find("GRAPHICSPage/Effects/FOG/Control").GetComponentInChildren<Toggle>(true).isOn = !originalFog;
                panel.Close(); yield return null;
                Check(Dialog(panel) != null && Time.timeScale == 0, trial + ": unsaved close waits for confirmation");
                CheckFullScreenDialog(panel, canvas, "Trial " + trial);
                DialogButton(panel, "CANCEL").onClick.Invoke(); yield return null;
                Check(SoloGameSettings.Current.fog == originalFog, trial + ": Cancel discards unsaved changes");
            }
            Check(Time.timeScale == 1 && look.enabled, trial + ": closing Settings restores gameplay");
            Time.timeScale = 0; look.enabled = false; menu.OpenPause(); yield return null; menu.ClosePause();
            Check(Time.timeScale == 0 && !look.enabled, trial + ": nested pause preserves earlier modal state"); Time.timeScale = 1; look.enabled = true;
            UnityEngine.Object.Destroy(menu.gameObject); UnityEngine.Object.Destroy(pause.gameObject); yield return null;
            if (trial == 2) { CheckTruthTableFixtures(); yield return null; }
        }
        var mainRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Settings/MainMenuSettingsUI.prefab"), canvas.transform, false);
        var mainPanel = mainRoot.GetComponent<GameSettingsPanel>(); yield return null;
        mainRoot.transform.Find("SettingsFrame/GRAPHICSPage/Effects/FOG/Control").GetComponentInChildren<Toggle>(true).isOn = !SoloGameSettings.Current.fog;
        mainPanel.Close(); Check(Dialog(mainPanel) != null && mainRoot.activeSelf, "Main Menu settings also protect unsaved edits");
        CheckFullScreenDialog(mainPanel, canvas, "Main Menu settings");
        DialogButton(mainPanel, "CloseConfirmation").onClick.Invoke(); Check(mainRoot.activeSelf && mainPanel.HasPendingChanges, "Main Menu confirmation X keeps the settings draft");
        mainPanel.Close(); DialogButton(mainPanel, "CANCEL").onClick.Invoke(); Check(!mainRoot.activeSelf, "Main Menu unsaved Cancel closes Settings");
        UnityEngine.Object.Destroy(mainRoot); yield return null;
        var previous = SceneManager.GetActiveScene(); var active = SceneManager.CreateScene("MultiplayerGameplay");
        foreach (var root in previous.GetRootGameObjects()) SceneManager.MoveGameObjectToScene(root, active);
        SceneManager.SetActiveScene(active); yield return SceneManager.UnloadSceneAsync(previous);
        typeof(SoloSettingsRuntime).GetMethod("SceneLoaded", Hidden).Invoke(runtime, new object[] { active, LoadSceneMode.Single });
        yield return null; yield return null; yield return new WaitForSecondsRealtime(.1f);
        Check(QualitySettings.renderPipeline == null || !QualitySettings.renderPipeline.name.Contains("Solo Pipeline (Runtime)"), "leaving solo restores the previous pipeline");
        Check(SoloGameSettings.LookMultiplier == Vector2.one && SoloGameSettings.ZoomMultiplier == 1 && !SoloGameSettings.LeftHanded, "other modes retain their camera controls");
    }
    static GameObject Dialog(GameSettingsPanel panel) => (GameObject)typeof(GameSettingsPanel).GetField("popup", Hidden).GetValue(panel);
    static Button DialogButton(GameSettingsPanel panel, string name) => Dialog(panel).GetComponentsInChildren<Button>().Single(b => b.name == name);
    static void CheckFullScreenDialog(GameSettingsPanel panel, GameObject canvas, string context)
    {
        Canvas.ForceUpdateCanvases();
        var rect = Dialog(panel).transform as RectTransform; var full = canvas.transform as RectTransform;
        Check(rect.parent == full && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero && Vector2.Distance(rect.rect.size, full.rect.size) < .1f,
            context + ": confirmation background covers the full canvas outside the fitted settings panel");
    }
    static IEnumerator CheckSettingsDialogs(LevelMenu menu, GameSettingsPanel panel, GameObject canvas)
    {
        var frame = panel.transform.Find("SettingsFrame");
        string original = JsonUtility.ToJson(panel.Draft);
        frame.Find("Footer/Reset").GetComponent<Button>().onClick.Invoke();
        Check(Dialog(panel) != null && JsonUtility.ToJson(panel.Draft) == original, "Reset waits for Yes before changing the draft");
        CheckFullScreenDialog(panel, canvas, "Reset"); Capture(canvas, "ResetConfirmation");
        DialogButton(panel, "CloseConfirmation").onClick.Invoke();
        Check(Dialog(panel) == null && panel.gameObject.activeInHierarchy && JsonUtility.ToJson(panel.Draft) == original, "reset confirmation X preserves settings and edits");
        frame.Find("Footer/Reset").GetComponent<Button>().onClick.Invoke(); DialogButton(panel, "CANCEL").onClick.Invoke();
        Check(JsonUtility.ToJson(panel.Draft) == original && panel.gameObject.activeInHierarchy, "reset Cancel preserves the draft and keeps Settings open");
        frame.Find("Footer/Reset").GetComponent<Button>().onClick.Invoke(); DialogButton(panel, "YES").onClick.Invoke();
        Check(panel.Draft.horizontalSensitivity == 1 && Mathf.Approximately(SoloGameSettings.Current.horizontalSensitivity, 1.7f), "confirmed Reset selects defaults without saving before Apply");
        panel.Apply(); yield return null;
        var fog = frame.Find("GRAPHICSPage/Effects/FOG/Control").GetComponentInChildren<Toggle>(true);
        bool savedFog = SoloGameSettings.Current.fog;
        string saved = PlayerPrefs.GetString(SoloGameSettings.PreferenceKey);
        fog.isOn = !savedFog; panel.Close();
        Check(Dialog(panel) != null && Time.timeScale == 0 && panel.HasPendingChanges, "closing unsaved settings pauses for Apply or Cancel");
        CheckFullScreenDialog(panel, canvas, "Unsaved changes"); Capture(canvas, "UnsavedChanges");
        DialogButton(panel, "CloseConfirmation").onClick.Invoke();
        Check(Dialog(panel) == null && panel.HasPendingChanges && Time.timeScale == 0, "unsaved confirmation X keeps edits and the pause menu open");
        panel.Close(); DialogButton(panel, "CANCEL").onClick.Invoke(); yield return null;
        Check(!panel.gameObject.activeInHierarchy && Time.timeScale == 1 && PlayerPrefs.GetString(SoloGameSettings.PreferenceKey) == saved, "unsaved Cancel closes Settings without saving");
        menu.OpenPause(); yield return null;
        Check(panel.Draft.fog == savedFog && !panel.HasPendingChanges, "reopening Settings reloads the saved values after Cancel");
        fog.isOn = !savedFog; panel.Close(); DialogButton(panel, "APPLY").onClick.Invoke(); yield return null;
        Check(!panel.gameObject.activeInHierarchy && Time.timeScale == 1 && SoloGameSettings.Current.fog != savedFog, "unsaved Apply saves changes and closes Settings");
        menu.OpenPause(); yield return null;
        panel.ResetDraft(); panel.Apply(); panel.Close(); yield return null;
        Check(!panel.gameObject.activeInHierarchy && Dialog(panel) == null, "closing without pending changes needs no confirmation");
        menu.OpenPause(); yield return null;
    }
    static void CheckSteadyFixture(int trial)
    {
        var root = new GameObject("Steady fixture sample"); root.SetActive(false);
        var light = root.AddComponent<Light>(); light.type = LightType.Point; light.intensity = 8;
        var effect = root.AddComponent<ChamberLightFlicker>(); typeof(ChamberLightFlicker).GetField("flickeringLights", Hidden).SetValue(effect, new[] { light }); root.SetActive(true);
        var advance = typeof(ChamberLightFlicker).GetMethod("Advance", Hidden);
        for (int i = 0; i < 3600; i++) advance.Invoke(effect, new object[] { .05f });
        Check(light.intensity == 8 && effect.CompletedEvents == 0, trial + ": fixture intensity stays steady through three simulated minutes");
        UnityEngine.Object.Destroy(root);
    }
    static void CheckUnlitFixture()
    {
        var fixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var renderer = fixture.GetComponent<Renderer>();
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Torch Flame Sample" };
        renderer.sharedMaterial = material;
        var visuals = UnityEngine.Object.FindFirstObjectByType<SoloTrialVisuals>();
        typeof(SoloTrialVisuals).GetMethod("LightOpaqueSurface", Hidden).Invoke(visuals, new object[] { renderer });
        Check(renderer.sharedMaterial == material, "unlit torch flames keep their authored self-lit appearance");
        UnityEngine.Object.Destroy(fixture); UnityEngine.Object.Destroy(material);
    }
    static void CheckTruthTableFixtures()
    {
        var root = new GameObject("Steady streetlamps sample"); root.SetActive(false);
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Celestial Gold Sample" };
        for (int i = 0; i < 2; i++)
        {
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube); lamp.name = i == 0 ? "STREETLAMP2" : "STREETLAMP2 (1)";
            lamp.transform.SetParent(root.transform, false); lamp.GetComponent<Renderer>().sharedMaterial = material;
        }
        var effect = root.AddComponent<TruthTableDuskLighting>(); root.SetActive(true);
        Check(effect.FixtureCount == 2 && effect.FlickeringCount == 0 && effect.LampLights.All(l => l.intensity > 0 && l.shadows == LightShadows.None), "Truth Table lamps stay lit without programmed dips or extra shadow maps");
        Check(root.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial.IsKeywordEnabled("_EMISSION")), "Truth Table lantern surfaces retain their own full glow");
        UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(material);
    }
    static void Capture(GameObject root, string name)
    {
        var canvas = root.GetComponent<Canvas>(); var camera = Camera.main;
        var previousMode = canvas.renderMode; var previousCamera = canvas.worldCamera; var previousDistance = canvas.planeDistance;
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Texture2D image = null;
        try
        {
            camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .01f;
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(true);
            camera.Render(); RenderTexture.active = target;
            image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            Directory.CreateDirectory("output/settings-followup"); File.WriteAllBytes("output/settings-followup/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousDistance;
            UnityEngine.Object.Destroy(target); if (image != null) UnityEngine.Object.Destroy(image);
        }
    }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); File.AppendAllText(Report, "PASS " + message + "\n"); }
    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.playModeStateChanged -= Restore; EditorApplication.playModeStateChanged += Restore;
        if (SessionState.GetBool(Flag + ".RestorePending", false)) EditorApplication.delayCall += RestorePending;
    }
    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false); TeleportManager.ValidationNavigation = null;
        var data = JsonUtility.FromJson<SoloSettingsData>(SessionState.GetString(Flag + ".Data", "{}"));
        AudioVolumeSettings.SetMusicVolume(data.music); AudioVolumeSettings.SetSoundFXVolume(data.soundFX); AudioVolumeSettings.Save();
        foreach (string key in new[] { SoloGameSettings.PreferenceKey, AudioVolumeSettings.MusicPreference, AudioVolumeSettings.SoundFXPreference })
        {
            if (!SessionState.GetBool(Flag + key, false)) PlayerPrefs.DeleteKey(key);
            else if (key == SoloGameSettings.PreferenceKey) PlayerPrefs.SetString(key, SessionState.GetString(Flag + key + ".Value", ""));
            else PlayerPrefs.SetFloat(key, float.Parse(SessionState.GetString(Flag + key + ".Value", "1"), System.Globalization.CultureInfo.InvariantCulture));
        }
        for (int trial = 1; trial <= 10; trial++) PlayerPrefs.DeleteKey("TrialRewardOutbox.v1." + SessionState.GetString(Flag + ".Uid", "") + "." + trial);
        PlayerPrefs.Save(); typeof(SoloGameSettings).GetField("current", Static).SetValue(null, null);
        AudioListener.volume = SessionState.GetFloat(Flag + ".Listener", 1); Application.targetFrameRate = SessionState.GetInt(Flag + ".FPS", -1); QualitySettings.vSyncCount = SessionState.GetInt(Flag + ".Vsync", 0);
        Application.runInBackground = SessionState.GetBool(Flag + ".RunInBackground", false);
        typeof(TrialBackendClient).GetField("ValidationTransport", Static).SetValue(null, null); typeof(TrialBackendClient).GetField("ValidationUid", Static).SetValue(null, null);
        typeof(SoloEconomyConnection).GetField("ValidationEnabled", Static).SetValue(null, null);
        SessionState.SetBool(Flag + ".RestorePending", true);
        EditorApplication.delayCall += RestorePending;
    }
    public static void RestorePending()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += RestorePending; return; }
        string path = SessionState.GetString(Flag + ".Scene", "Assets/Scenes/Main Menu.unity");
        if (string.IsNullOrEmpty(path) || path == Fixture) path = "Assets/Scenes/Main Menu.unity";
        EditorSceneManager.OpenScene(path); AssetDatabase.DeleteAsset(Fixture);
        SessionState.SetBool(Flag + ".RestorePending", false);
    }
}
#endif
