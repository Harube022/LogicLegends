#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Upgrades the shared authored settings once; gameplay scenes keep their own Settings button wiring.</summary>
public static class SoloSettingsAuthoring
{
    const string UI = "Assets/Prefabs/UI/Settings/MainMenuSettingsUI.prefab";
    const string Folder = "Assets/Resources/Settings";
    [MenuItem("Logic Legends/Settings/Connect Main Menu And Solo Trials")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Main Menu.unity" || scene.isDirty) throw new InvalidOperationException("Open the saved Main Menu first.");
        Directory.CreateDirectory("Temp/SoloSettings/Before");
        if (!File.Exists("Temp/SoloSettings/Before/MainMenuSettingsUI.prefab")) File.Copy(UI, "Temp/SoloSettings/Before/MainMenuSettingsUI.prefab");
        if (!File.Exists("Temp/SoloSettings/Before/Main Menu.unity")) File.Copy(scene.path, "Temp/SoloSettings/Before/Main Menu.unity");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var root = PrefabUtility.LoadPrefabContents(UI);
        try
        {
            var frame = root.transform.Find("SettingsFrame");
            foreach (string path in new[] {
                "GRAPHICSPage/Display/RESOLUTIONQUALITY", "GRAPHICSPage/Display/EDGESMOOTHING",
                "CONTROLSPage/Camera/INVERTVERTICALLOOK", "ACCESSIBILITYPage/Motion/REDUCEDCAMERAMOTION", "ACCESSIBILITYPage/Motion/CAMERASHAKE" })
            { var child = frame.Find(path); if (child != null) Object.DestroyImmediate(child.gameObject); }
            UpdateGraphicsRows(frame);
            foreach (var binding in root.GetComponentsInChildren<AudioVolumePanel>(true)) Object.DestroyImmediate(binding);
            var motion = frame.Find("ACCESSIBILITYPage/Motion");
            motion.Find("SectionHeading/Heading").GetComponent<TMP_Text>().text = "READING & PUZZLE CUES";
            var hint = motion.Find("Hint").GetComponent<TMP_Text>();
            hint.text = "Adjust reading size and contrast.\n\nText cues help explain correct answers and mistakes without relying on color alone.";
            var hintRect = hint.rectTransform; hintRect.anchorMin = new Vector2(.025f, .18f); hintRect.anchorMax = new Vector2(.975f, .78f); hintRect.offsetMin = hintRect.offsetMax = Vector2.zero;
            SetCaption(frame, "GENERALPage/Account/TUTORIAL", "TRIAL GUIDES");
            frame.Find("GENERALPage/Account/TUTORIAL/Control/REPLAYTUTORIAL/Label").GetComponent<TMP_Text>().text = "VIEW GUIDES";
            frame.Find("GENERALPage/Help/YOURPRIVACY/Control/PRIVACYPOLICY/Label").GetComponent<TMP_Text>().text = "DATA & PRIVACY";
            frame.Find("GENERALPage/Help/Hint").GetComponent<TMP_Text>().text = "Reset selects defaults. Apply saves settings. Your account and trial progress stay safe.";
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                selectable.enabled = true; selectable.interactable = true;
                selectable.navigation = new Navigation { mode = Navigation.Mode.None };
                if (selectable.targetGraphic != null) selectable.targetGraphic.raycastTarget = true;
            }
            GameSettingsPanel.DisableUnavailableActions(frame);
            foreach (var slider in root.GetComponentsInChildren<Slider>(true))
            {
                slider.fillRect = slider.transform.Find("Track/Fill Area/Fill") as RectTransform;
                slider.handleRect = slider.transform.Find("Handle Slide Area/Handle") as RectTransform;
                if (slider.handleRect != null)
                {
                    var area = (RectTransform)slider.handleRect.parent;
                    area.anchorMin = new Vector2(0, .5f); area.anchorMax = new Vector2(1, .5f);
                    area.offsetMin = new Vector2(5.5f, -29); area.offsetMax = new Vector2(-5.5f, 29);
                    slider.handleRect.anchorMin = new Vector2(.5f, 0); slider.handleRect.anchorMax = new Vector2(.5f, 1);
                    slider.handleRect.sizeDelta = new Vector2(38, 0); slider.handleRect.anchoredPosition = Vector2.zero;
                    slider.targetGraphic = slider.handleRect.GetComponent<Image>();
                }
            }
            if (root.GetComponent<GameSettingsPanel>() == null) root.AddComponent<GameSettingsPanel>();
            var panelSettings = new SerializedObject(root.GetComponent<GameSettingsPanel>());
            panelSettings.FindProperty("accountInformationPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ProfileAccountInformationUI>("Assets/Prefabs/UI/ProfileAccountInformation.prefab");
            panelSettings.ApplyModifiedPropertiesWithoutUndo();
            if (root.GetComponent<SoloSettingsLayout>() == null) root.AddComponent<SoloSettingsLayout>();
            StageUITheme.Stretch((RectTransform)frame, new Vector2(.095f, .07f), new Vector2(.905f, .86f));
            var title = (RectTransform)root.transform.Find("SettingsTitle"); title.anchorMin = title.anchorMax = new Vector2(.5f, .92f);
            if (root.transform.Find("CloseSettingsAction") == null) MultiplayerUIArt.CloseAction("CloseSettingsAction", root.transform, .095f, .86f);
            PrefabUtility.SaveAsPrefabAsset(root, UI);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(UI);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var variant = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            variant.name = "TrialSettingsUI";
            variant.transform.Find("SettingsFrame/Footer/LogOut/Label").GetComponent<TMP_Text>().text = "MAIN MENU";
            PrefabUtility.SaveAsPrefabAsset(variant, Folder + "/TrialSettingsUI.prefab");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        BuildLightingAssets();
        var settings = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Settings_Menu");
        var oldClose = settings.Find("MainMenuSettingsUI/CloseSettings"); if (oldClose != null) oldClose.gameObject.SetActive(false);
        MainMenuSettingsUIBuilder.ConnectSettingsConfirmations(settings);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Shared settings connected for Main Menu and Trials 1–10. Removed six controls, kept existing pause/navigation owners, and created mobile-budget solo lighting presets.");
    }
    [MenuItem("Logic Legends/Settings/Update Solo Graphics Controls")]
    public static void UpgradeGraphics()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(UI);
        try
        {
            UpdateGraphicsRows(root.transform.Find("SettingsFrame"));
            PrefabUtility.SaveAsPrefabAsset(root, UI);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        const string path = Folder + "/SoloGraphicsProfile.asset";
        const string archive = LogicLegends.OpenArchive.Topics.Editor.ArchiveLightingAudioSetup.Folder;
        var profile = AssetDatabase.LoadAssetAtPath<SoloGraphicsProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<SoloGraphicsProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        profile.pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(archive + "/Archive_Mobile_HDR.asset");
        profile.atmosphere = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(archive + "/Archive_Lighting_Profile.asset");
        if (profile.pipeline == null || profile.atmosphere == null) throw new InvalidOperationException("Missing the Open Archive graphics assets.");
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }

    static void UpdateGraphicsRows(Transform frame)
    {
        var display = frame.Find("GRAPHICSPage/Display");
        var effects = frame.Find("GRAPHICSPage/Effects");
        var shadow = display.Find("SHADOWQUALITY");
        var resolution = display.Find("RESOLUTION");
        if (resolution == null) resolution = Object.Instantiate(shadow.gameObject, display, false).transform;
        resolution.name = "RESOLUTION";
        resolution.Find("SettingLabel").GetComponent<TMP_Text>().text = "RESOLUTION";

        var texture = display.Find("TEXTUREQUALITY");
        if (texture == null) texture = Object.Instantiate(shadow.gameObject, display, false).transform;
        texture.name = "TEXTUREQUALITY";
        texture.Find("SettingLabel").GetComponent<TMP_Text>().text = "TEXTURE QUALITY";

        var special = effects.Find("SPECIALEFFECTS") ?? effects.Find("LIGHTINGQUALITY");
        special.name = "SPECIALEFFECTS";
        special.Find("SettingLabel").GetComponent<TMP_Text>().text = "SPECIAL EFFECTS";
        var bloom = effects.Find("BLOOM") ?? effects.Find("MOTIONBLUR");
        bloom.name = "BLOOM";
        bloom.Find("SettingLabel").GetComponent<TMP_Text>().text = "BLOOM";

        Reflow(display, "GRAPHICSQUALITY", "RESOLUTION", "TEXTUREQUALITY", "FRAMERATE", "SHADOWQUALITY");
        Reflow(effects, "SPECIALEFFECTS", "BLOOM", "FOG");
        GameSettingsPanel.AuthorDropdown(shadow.Find("Control"), new[] { "OFF", "LOW", "MEDIUM", "HIGH" });
        GameSettingsPanel.AuthorDropdown(resolution.Find("Control"), new[] { "720p", "1080p" });
        GameSettingsPanel.AuthorDropdown(texture.Find("Control"), new[] { "LOW", "MEDIUM", "HIGH" });
        GameSettingsPanel.AuthorDropdown(special.Find("Control"), new[] { "LOW", "MEDIUM", "HIGH" });
    }

    static void Reflow(Transform column, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            var row = (RectTransform)column.Find(names[i]);
            row.anchoredPosition = new Vector2(0, -76 - i * 72);
            row.SetSiblingIndex(i + 1);
        }
    }

    static void SetCaption(Transform frame, string path, string caption) => frame.Find(path + "/SettingLabel").GetComponent<TMP_Text>().text = caption;
    static void BuildLightingAssets()
    {
        var original = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
        var rendererSource = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/ForwardRenderer.asset");
        for (int tier = 0; tier < 3; tier++)
        {
            string rendererPath = Folder + "/SoloRenderer" + tier + ".asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = Object.Instantiate(rendererSource); renderer.name = "SoloRenderer" + tier;
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            // Mobile Forward avoids clustered-light and full-screen AO passes.
            // Renderer data belongs to these solo presets, not the global pipeline.
            foreach (var feature in renderer.rendererFeatures) if (feature != null) feature.SetActive(false);
            renderer.rendererFeatures.Clear();
            var rendererData = new SerializedObject(renderer);
            rendererData.FindProperty("m_RendererFeatureMap").ClearArray();
            rendererData.FindProperty("m_RenderingMode").intValue = 0;
            rendererData.FindProperty("m_IntermediateTextureMode").intValue = 0;
            rendererData.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(renderer);
            string path = Folder + "/SoloLighting" + tier + ".asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipeline == null) { pipeline = Object.Instantiate(original); pipeline.name = "SoloLighting" + tier; AssetDatabase.CreateAsset(pipeline, path); }
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            // Vertex-only lighting misses small light pools on low-poly walls.
            // Keep bounded per-pixel lights; no additional-light shadow passes.
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = 1;
            serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            serialized.FindProperty("m_AnyShadowsSupported").boolValue = true;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.FindProperty("m_RequireDepthTexture").boolValue = false;
            serialized.FindProperty("m_RequireOpaqueTexture").boolValue = false;
            serialized.FindProperty("m_SupportsHDR").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.shadowDepthBias = .7f; pipeline.shadowNormalBias = .5f;
            pipeline.maxAdditionalLightsCount = new[] { 4, 6, 8 }[tier];
            pipeline.renderScale = tier == 0 ? .8f : tier == 1 ? .9f : 1f;
            pipeline.msaaSampleCount = tier == 2 ? 2 : 1;
            EditorUtility.SetDirty(pipeline);
        }
        string materialPath = Folder + "/SoloOpaqueLit.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")); material.name = "SoloOpaqueLit";
            material.SetFloat("_Smoothness", .15f); material.SetColor("_SpecColor", new Color(.12f, .12f, .12f, 1));
            AssetDatabase.CreateAsset(material, materialPath);
        }
    }
}
#endif
