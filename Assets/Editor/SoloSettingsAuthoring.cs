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
                "GRAPHICSPage/Display/RESOLUTIONQUALITY", "GRAPHICSPage/Display/EDGESMOOTHING", "GRAPHICSPage/Effects/BLOOM",
                "CONTROLSPage/Camera/INVERTVERTICALLOOK", "ACCESSIBILITYPage/Motion/REDUCEDCAMERAMOTION", "ACCESSIBILITYPage/Motion/CAMERASHAKE" })
            { var child = frame.Find(path); if (child != null) Object.DestroyImmediate(child.gameObject); }
            Reflow(frame.Find("GRAPHICSPage/Display"), "GRAPHICSQUALITY", "FRAMERATE", "SHADOWQUALITY");
            Reflow(frame.Find("GRAPHICSPage/Effects"), "LIGHTINGQUALITY", "FOG", "MOTIONBLUR");
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
            GameSettingsPanel.AuthorDropdown(frame.Find("GRAPHICSPage/Display/SHADOWQUALITY/Control"), new[] { "OFF", "LOW", "MEDIUM", "HIGH" });
            GameSettingsPanel.AuthorDropdown(frame.Find("GRAPHICSPage/Effects/LIGHTINGQUALITY/Control"), new[] { "LOW", "MEDIUM", "HIGH" });
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
    static void Reflow(Transform column, params string[] names)
    {
        for (int i = 0; i < names.Length; i++) ((RectTransform)column.Find(names[i])).anchoredPosition = new Vector2(0, -76 - i * 80);
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
