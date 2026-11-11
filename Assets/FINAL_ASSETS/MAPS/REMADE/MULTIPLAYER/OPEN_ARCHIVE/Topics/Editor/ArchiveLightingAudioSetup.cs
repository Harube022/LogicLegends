using System;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveLightingAudioSetup
    {
        public const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/LightingAudio";
        const string SourcePipeline = "Assets/Settings/UniversalRP.asset";
        [MenuItem("Tools/Logic Legends/Open Archive/Lighting/Install Lighting And Audio Polish")]
        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.isDirty || scene.path != "Assets/Scenes/MultiplayerGameplay.unity")
                throw new InvalidOperationException("Open the saved MultiplayerGameplay scene outside Play Mode.");
            var session = UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            if (session == null || session.worldProgression == null) throw new InvalidOperationException("Existing Open Archive session and world progression are required.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            ImportAudio();
            string pipelinePath = Folder + "/Archive_Mobile_HDR.asset";
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath) == null) AssetDatabase.CopyAsset(SourcePipeline, pipelinePath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            pipeline.supportsHDR = true;
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            pipelineSettings.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            pipelineSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            pipeline.mainLightShadowmapResolution = 2048; pipeline.shadowDistance = 55; pipeline.shadowCascadeCount = 2;
            pipeline.cascade2Split = .28f; pipeline.cascadeBorder = .18f; pipeline.shadowDepthBias = .65f; pipeline.shadowNormalBias = .35f;
            pipeline.maxAdditionalLightsCount = 6; pipeline.msaaSampleCount = 2;
            pipeline.supportsCameraDepthTexture = false; pipeline.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(pipeline); RegisterQuality(pipeline);

            var root = session.transform.Find("Archive_Lighting_And_Sound");
            if (root == null) { var go = new GameObject("Archive_Lighting_And_Sound"); go.transform.SetParent(session.transform, false); root = go.transform; }
            var lighting = GetOrAdd<ArchiveSceneLighting>(root.gameObject);
            lighting.session = session; lighting.pipeline = pipeline;
            var sounds = GetOrAdd<ArchiveAudioLandscape>(root.gameObject);
            sounds.session = session;
            sounds.dayBed = Clip("Archive_Day_Archive_Bed"); sounds.nightBed = Clip("Archive_Night_Suspense_Bed");
            ArchiveAdaptiveMusicSetup.AssignIfAvailable(sounds);
            sounds.proofDevourer = SoundSet("Proof"); sounds.primalAxiom = SoundSet("Primal");
            sounds.equip = Clip("Archive_Item_Equip"); sounds.flashlightOn = Clip("Archive_Item_Flashlight_On"); sounds.flashlightOff = Clip("Archive_Item_Flashlight_Off");
            sounds.battery = Clip("Archive_Item_Battery"); sounds.medkit = Clip("Archive_Item_Medkit"); sounds.revival = Clip("Archive_Item_Revival");
            sounds.handoff = Clip("Archive_Item_Handoff"); sounds.dartImpact = Clip("Archive_Item_Dart_Impact");
            sounds.dayVolume = .14f; sounds.nightVolume = .17f; sounds.itemVolume = .40f; sounds.monsterVolume = .32f;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder + "/Archive_Lighting_Profile.asset");
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, Folder + "/Archive_Lighting_Profile.asset"); }
            var bloom = Component<Bloom>(profile); bloom.intensity.Override(.20f); bloom.threshold.Override(1.05f); bloom.scatter.Override(.55f);
            bloom.clamp.Override(6); bloom.downscale.Override(BloomDownscaleMode.Quarter); bloom.maxIterations.Override(4); bloom.highQualityFiltering.Override(false);
            var grade = Component<ColorAdjustments>(profile); grade.contrast.Override(5); grade.saturation.Override(-3); grade.postExposure.Override(0);
            Component<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
            var vignette = Component<Vignette>(profile); vignette.intensity.Override(.10f); vignette.smoothness.Override(.70f);
            EditorUtility.SetDirty(profile);
            var volume = GetOrAdd<Volume>(root.gameObject);
            volume.isGlobal = true; volume.priority = 8; volume.weight = 1; volume.sharedProfile = profile; lighting.atmosphere = volume;

            var torches = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Torch_FireFX").ToArray();
            lighting.fireLights = new Light[torches.Length]; lighting.fireVoices = new AudioSource[torches.Length];
            for (int i = 0; i < torches.Length; i++)
            {
                var anchor = torches[i].Find("Warm_Fire_Illumination");
                if (anchor == null) { var go = new GameObject("Warm_Fire_Illumination"); go.transform.SetParent(torches[i], false); anchor = go.transform; }
                anchor.localPosition = Vector3.up * .25f;
                var light = GetOrAdd<Light>(anchor.gameObject);
                light.type = LightType.Point; light.color = new Color(1, .46f, .12f); light.range = 20; light.intensity = 10;
                light.shadows = LightShadows.None; light.renderMode = LightRenderMode.ForcePixel; light.bounceIntensity = 0;
                var voice = GetOrAdd<AudioSource>(anchor.gameObject);
                voice.clip = Clip("Archive_Fire_Crackle_Loop"); voice.loop = true; voice.playOnAwake = false; voice.volume = 0;
                voice.spatialBlend = 1; voice.rolloffMode = AudioRolloffMode.Linear; voice.minDistance = 2; voice.maxDistance = 22; voice.dopplerLevel = 0;
                AudioVolumeSettings.Route(voice, GameAudioChannel.SoundFX);
                lighting.fireLights[i] = light; lighting.fireVoices[i] = voice;
            }
            var flame = AssetDatabase.LoadAssetAtPath<Material>("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Art/Materials/Archive_FacetedFlameParticles.mat");
            if (flame != null) { flame.SetColor("_BaseColor", new Color(2.2f, 2.2f, 2.2f, 1)); EditorUtility.SetDirty(flame); }
            var world = session.worldProgression;
            foreach (var key in new[] { world.sun, world.moon }) if (key != null)
            { key.shadowBias = .05f; key.shadowNormalBias = .35f; key.shadowStrength = .8f; EditorUtility.SetDirty(key); }
            world.moonKeyIntensity = .20f;
            world.nightAmbientSky = new Color(.105f, .14f, .22f);
            world.nightAmbientEquator = new Color(.08f, .105f, .16f);
            world.nightAmbientGround = new Color(.06f, .075f, .11f);
            world.nightFogColor = new Color(.105f, .13f, .18f);
            foreach (var monster in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<KhezuHuntingController>(true)))
            { sounds.ApplyMonsterProfile(monster); EditorUtility.SetDirty(monster.GetComponent<KhezuCinematicJumpscareController>()); }
            // Main directional shadows follow the same low-poly silhouettes already
            // used by the trees, bridges, architecture and character rigs.
            int casters = 0;
            foreach (var renderer in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)))
            {
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer.sharedMaterial == null) continue;
                bool opaque = renderer.sharedMaterial.renderQueue < 2450;
                string name = renderer.name.ToLowerInvariant();
                bool solid = renderer is SkinnedMeshRenderer || name.Contains("tree") || name.Contains("bridge") || name.Contains("arch") || name.Contains("pillar") || name.Contains("cliff");
                if (!opaque || !solid) continue;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; EditorUtility.SetDirty(renderer); casters++;
            }
            EditorUtility.SetDirty(world); EditorUtility.SetDirty(lighting); EditorUtility.SetDirty(sounds); EditorUtility.SetDirty(volume);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Open Archive lighting/audio installed: " + torches.Length + " warm fire anchors, " + casters + " solid shadow casters; scene-scoped HDR/bloom and original sound beds.");
        }
        static T GetOrAdd<T>(GameObject go) where T : UnityEngine.Component
        { var component = go.GetComponent<T>(); return component != null ? component : go.AddComponent<T>(); }
        static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component)) return component;
            component = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(component, profile); return component;
        }
        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Audio/" + name + ".wav");
        static ArchiveMonsterSoundSet SoundSet(string name) => new ArchiveMonsterSoundSet {
            capture = Clip("Archive_" + name + "_Capture"), breathing = Clip("Archive_" + name + "_Breathing"), growl = Clip("Archive_" + name + "_Growl"),
            roar = Clip("Archive_" + name + "_Roar"), impact = Clip("Archive_" + name + "_Impact"), barrier = Clip("Archive_" + name + "_Barrier"),
            footstep = Clip("Archive_" + name + "_Footstep"), attack = Clip("Archive_" + name + "_Attack") };
        static void ImportAudio()
        {
            foreach (var path in Directory.GetFiles(Folder + "/Audio", "*.wav"))
            {
                var asset = path.Replace('\\', '/'); AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(asset); var settings = importer.defaultSampleSettings;
                bool music = asset.Contains("_Bed.wav");
                settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = music ? .65f : .78f;
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate; settings.sampleRateOverride = 24000;
                importer.defaultSampleSettings = settings; importer.forceToMono = !music; importer.loadInBackground = music;
                importer.SaveAndReimport();
            }
        }
        static void RegisterQuality(UniversalRenderPipelineAsset pipeline)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").First();
            var serialized = new SerializedObject(source); var array = serialized.FindProperty("m_QualitySettings");
            int index = -1; for (int i = 0; i < array.arraySize; i++) if (array.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == ArchiveSceneLighting.QualityProfile) index = i;
            if (index < 0)
            {
                index = array.arraySize; array.arraySize++;
                var from = array.GetArrayElementAtIndex(0); var to = array.GetArrayElementAtIndex(index);
                var field = from.Copy(); var end = from.GetEndProperty(); string prefix = from.propertyPath + ".";
                while (field.Next(true) && !SerializedProperty.EqualContents(field, end))
                {
                    var target = to.FindPropertyRelative(field.propertyPath.Substring(prefix.Length)); if (target == null) continue;
                    if (field.isArray && field.propertyType != SerializedPropertyType.String) { target.arraySize = field.arraySize; continue; }
                    switch (field.propertyType)
                    {
                        case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: case SerializedPropertyType.ArraySize: target.intValue = field.intValue; break;
                        case SerializedPropertyType.Boolean: target.boolValue = field.boolValue; break;
                        case SerializedPropertyType.Float: target.floatValue = field.floatValue; break;
                        case SerializedPropertyType.String: target.stringValue = field.stringValue; break;
                        case SerializedPropertyType.ObjectReference: target.objectReferenceValue = field.objectReferenceValue; break;
                        case SerializedPropertyType.Vector2: target.vector2Value = field.vector2Value; break;
                        case SerializedPropertyType.Vector3: target.vector3Value = field.vector3Value; break;
                        case SerializedPropertyType.Vector4: target.vector4Value = field.vector4Value; break;
                        case SerializedPropertyType.Color: target.colorValue = field.colorValue; break;
                    }
                }
            }
            var entry = array.GetArrayElementAtIndex(index); entry.FindPropertyRelative("name").stringValue = ArchiveSceneLighting.QualityProfile;
            entry.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
            // Register this scoped preset so the Android shader stripper keeps its
            // HDR/bloom and cascaded-shadow variants. Existing defaults are retained.
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(source);
        }
    }
}
