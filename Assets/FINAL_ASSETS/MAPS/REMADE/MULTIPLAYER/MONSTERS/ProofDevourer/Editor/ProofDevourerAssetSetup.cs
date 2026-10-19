// Editor-only asset authoring and verification. No component is added to the
// monster and this code is excluded from Android/player builds.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ProofDevourerAssetSetup
{
    public const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/MONSTERS/ProofDevourer";
    public const string ModelPath = Folder + "/Models/ProofDevourer.fbx";
    public const string PrefabPath = Folder + "/Prefabs/ProofDevourer.prefab";
    public const string ControllerPath = Folder + "/Animations/ProofDevourer.controller";
    public static readonly string[] ClipNames = {
        "Sleep", "Wake_01", "Wake_02", "Wake_03", "Full_Awaken", "Idle", "Chase",
        "Capture", "Jumpscare", "Barrier_Cast", "Attack", "Return_To_Sleep", "Stunned"
    };
    static bool Loops(string name) { return name == "Sleep" || name == "Idle" || name == "Chase"; }
    static string ClipPath(string name) { return Folder + "/Animations/" + name + ".anim"; }

    // CPU BakeMesh reads the active project weight limit in Unity 6000.3.
    // Preserve both the value and the dirty state; previewing must not save
    // an unrelated QualitySettings asset or override prior user edits.
    sealed class BakeWeightsScope : IDisposable
    {
        readonly SkinWeights previous;
        readonly QualitySettings settings;
        readonly bool wasDirty;
        public BakeWeightsScope()
        {
            previous = QualitySettings.skinWeights;
            settings = Resources.FindObjectsOfTypeAll<QualitySettings>().FirstOrDefault();
            wasDirty = settings != null && EditorUtility.IsDirty(settings);
            QualitySettings.skinWeights = SkinWeights.FourBones;
        }
        public void Dispose()
        {
            QualitySettings.skinWeights = previous;
            if (settings != null && !wasDirty) EditorUtility.ClearDirty(settings);
        }
    }

    [MenuItem("Tools/Proof Devourer/Build asset package")]
    public static void Build()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP/Lit shader is missing.");
        ConfigureTexture(Folder + "/Textures/ProofDevourer_BaseColor.png", 1024);
        ConfigureTexture(Folder + "/Textures/ProofDevourer_Emission.png", 512);
        var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/ProofDevourer_BaseColor.png");
        var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/ProofDevourer_Emission.png");
        var stone = CreateMaterial("Devourer_PaleSkin", shader, baseMap, emission, false);
        var glow = CreateMaterial("Devourer_RedRunes", shader, baseMap, emission, true);

        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new FileNotFoundException("Build the Blender source first.", ModelPath);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = false;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.optimizeBones = false;
        importer.optimizeGameObjects = false;
        importer.skinWeights = ModelImporterSkinWeights.Standard;
        importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
        importer.animationRotationError = .1f;
        importer.animationPositionError = .01f;
        importer.animationScaleError = .01f;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Devourer_PaleSkin"), stone);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Devourer_RedRunes"), glow);
        var takes = importer.defaultClipAnimations;
        if (takes.Length != ClipNames.Length) throw new InvalidOperationException("Expected thirteen FBX takes, found " + takes.Length);
        foreach (var take in takes)
        {
            take.name = take.takeName.Split('|').Last();
            if (!ClipNames.Contains(take.name)) throw new InvalidOperationException("Unexpected take: " + take.takeName);
            take.loopTime = Loops(take.name);
            take.loopPose = false; // The authored endpoints already match exactly.
            take.loop = Loops(take.name);
            take.wrapMode = Loops(take.name) ? WrapMode.Loop : WrapMode.ClampForever;
            take.keepOriginalOrientation = true;
            take.keepOriginalPositionY = true;
            take.keepOriginalPositionXZ = true;
            take.lockRootRotation = true;
            take.lockRootHeightY = true;
            take.lockRootPositionXZ = true;
            take.heightFromFeet = false;
        }
        importer.clipAnimations = takes;
        importer.SaveAndReimport();

        var embedded = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
        foreach (var name in ClipNames)
        {
            if (!embedded.ContainsKey(name)) throw new InvalidOperationException("No imported animation " + name);
            var target = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(name));
            if (target == null)
            {
                target = UnityEngine.Object.Instantiate(embedded[name]);
                AssetDatabase.CreateAsset(target, ClipPath(name));
            }
            else EditorUtility.CopySerialized(embedded[name], target);
            target.name = name;
            target.legacy = false;
            target.wrapMode = Loops(name) ? WrapMode.Loop : WrapMode.ClampForever;
            var settings = AnimationUtility.GetAnimationClipSettings(target);
            settings.loopTime = Loops(name);
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(target, settings);
            EditorUtility.SetDirty(target);
        }
        ProofDevourerSlashAuthoring.ApplyIfAvailable();
        var controller = MakeController();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            instance.name = "ProofDevourer";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().Single();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.sharedMaterials = new[] { stone, glow };
                renderer.quality = SkinQuality.Bone4;
                renderer.updateWhenOffscreen = false;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
                renderer.skinnedMotionVectors = false;
                // Cover every authored pose, including the raised hands and sleep.
                var bounds = SampleLocalBounds(instance, renderer);
                bounds.Expand(.12f);
                renderer.localBounds = bounds;
            }
            // Store the initial sleeping pose, so the prefab is also asleep in
            // the scene view before its Animator receives its first update.
            AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath("Sleep")).SampleAnimation(instance, 0);
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        AssetDatabase.SaveAssets();
        var report = Verify();
        if (!report.passed) throw new InvalidOperationException("Asset verification failed. See Previews/UnityValidation.json");
        Debug.Log("Proof Devourer: " + report.triangles + " triangles, " + report.bones + " bones, " + report.materials + " materials, " + report.clips.Count + " verified clips.");
    }

    static void ConfigureTexture(string path, int size)
    {
        var t = (TextureImporter)AssetImporter.GetAtPath(path);
        t.textureType = TextureImporterType.Default;
        t.sRGBTexture = true;
        t.alphaSource = TextureImporterAlphaSource.None;
        t.mipmapEnabled = true;
        t.isReadable = false;
        t.maxTextureSize = size;
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.anisoLevel = 1;
        t.textureCompression = TextureImporterCompression.CompressedHQ;
        t.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
            name = "Android", overridden = true, maxTextureSize = size,
            format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50
        });
        t.SaveAndReimport();
    }

    static Material CreateMaterial(string name, Shader shader, Texture2D baseMap, Texture2D emission, bool glow)
    {
        var path = Folder + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        mat.SetTexture("_BaseMap", baseMap);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 0);
        mat.SetFloat("_AlphaClip", 0);
        mat.SetFloat("_Cull", 2);
        mat.SetFloat("_Metallic", 0);
        mat.SetFloat("_Smoothness", glow ? .2f : .12f);
        if (glow)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", emission);
            mat.SetColor("_EmissionColor", new Color(1.8f, 1.8f, 1.8f, 1));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static AnimatorController MakeController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = controller.layers[0].stateMachine;
        foreach (var transition in sm.anyStateTransitions) sm.RemoveAnyStateTransition(transition);
        foreach (var state in sm.states) sm.RemoveState(state.state);
        controller.parameters = new AnimatorControllerParameter[0];
        var states = new Dictionary<string, AnimatorState>();
        for (int i = 0; i < ClipNames.Length; i++)
        {
            var name = ClipNames[i];
            var state = sm.AddState(name, new Vector3(340 + i % 4 * 240, i / 4 * 140));
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(name));
            state.writeDefaultValues = false;
            states.Add(name, state);
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            var transition = sm.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = .14f;
            transition.canTransitionToSelf = false;
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            transition.AddCondition(AnimatorConditionMode.If, 0, name);
        }
        sm.defaultState = states["Sleep"];
        foreach (var name in new[] { "Full_Awaken", "Capture", "Jumpscare", "Barrier_Cast", "Attack", "Stunned", "Return_To_Sleep" })
        {
            var transition = states[name].AddTransition(states[name == "Return_To_Sleep" ? "Sleep" : "Idle"]);
            transition.hasExitTime = true;
            transition.exitTime = 1;
            transition.hasFixedDuration = true;
            transition.duration = .14f;
        }
        // Wake_01/02/03 retain their authored end poses until another trigger.
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static Bounds SampleLocalBounds(GameObject instance, SkinnedMeshRenderer renderer)
    {
        // BakeMesh uses the global CPU weight limit in this Unity version,
        // even when the renderer explicitly requests Bone4. Match the asset's
        // GPU quality for the bake, and restore the project's active preset.
        var bakeQuality = new BakeWeightsScope();
        var mesh = new Mesh();
        var result = new Bounds();
        bool first = true;
        try
        {
            foreach (var name in ClipNames)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(name));
                for (int sample = 0; sample <= 16; sample++)
                {
                    clip.SampleAnimation(instance, clip.length * sample / 16f);
                    renderer.BakeMesh(mesh);
                    if (first) { result = mesh.bounds; first = false; }
                    else result.Encapsulate(mesh.bounds);
                }
            }
        }
        finally { bakeQuality.Dispose(); UnityEngine.Object.DestroyImmediate(mesh); }
        return result;
    }

    [Serializable] public class ClipReport
    {
        public string name;
        public float seconds;
        public bool loop;
        public int bindings;
        public int unresolvedBindings;
        public float maximumMotionMeters;
        public float loopEndpointErrorMeters;
        public float minimumHeightMeters;
        public float rootDisplacementMeters;
        public float mouthApertureChangeMeters;
    }
    [Serializable] public class ValidationReport
    {
        public string unityVersion;
        public string checkedUtc;
        public bool passed;
        public string prefab;
        public int triangles, vertices, bones, materials, renderers;
        public bool genericAvatarValid, rootMotionDisabled;
        public int missingReferences;
        public string activeProjectSkinWeights;
        public string rendererSkinQuality;
        public string bakeSkinWeights = "FourBones; restored after verification";
        public float returnSleepEndpointErrorMeters;
        public float maximumAwakeningBoundaryErrorMeters;
        public List<ClipReport> clips = new List<ClipReport>();
        public List<string> errors = new List<string>();
        public string limits = "Verified in the Unity Editor. Android device performance and a player build have not been tested.";
    }

    [MenuItem("Tools/Proof Devourer/Verify imported prefab and clips")]
    public static void VerifyMenu() { var r = Verify(); Debug.Log(JsonUtility.ToJson(r, true)); }

    public static ValidationReport Verify(bool matchAuthoredWeights = true)
    {
        var report = new ValidationReport { unityVersion = Application.unityVersion,
            checkedUtc = DateTime.UtcNow.ToString("o"), prefab = PrefabPath };
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { report.errors.Add("Prefab does not load."); return report; }
        // A prefab isolation scene keeps verification out of the user's scene.
        var instance = PrefabUtility.LoadPrefabContents(PrefabPath);
        report.activeProjectSkinWeights = QualitySettings.skinWeights.ToString();
        report.bakeSkinWeights = matchAuthoredWeights ? "FourBones; restored after verification" : QualitySettings.skinWeights.ToString();
        var bakeQuality = matchAuthoredWeights ? new BakeWeightsScope() : null;
        try
        {
            var animator = instance.GetComponent<Animator>();
            report.genericAvatarValid = animator != null && animator.avatar != null && animator.avatar.isValid && !animator.avatar.isHuman;
            report.rootMotionDisabled = animator != null && !animator.applyRootMotion;
            if (!report.genericAvatarValid) report.errors.Add("Generic avatar is invalid.");
            if (!report.rootMotionDisabled) report.errors.Add("Root motion is enabled.");
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            report.renderers = renderers.Length;
            report.materials = renderers.SelectMany(r => r.sharedMaterials).Distinct().Count();
            report.bones = renderers.SelectMany(r => r.bones).Distinct().Count();
            foreach (var renderer in renderers)
            {
                report.vertices += renderer.sharedMesh.vertexCount;
                for (int i = 0; i < renderer.sharedMesh.subMeshCount; i++) report.triangles += (int)renderer.sharedMesh.GetIndexCount(i) / 3;
                if (renderer.bones.Any(b => b == null)) report.errors.Add("A skin bone is missing.");
                if (renderer.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name != "Universal Render Pipeline/Lit")) report.errors.Add("A URP material is missing.");
            }
            if (report.triangles < 5000 || report.triangles > 8000) report.errors.Add("Triangle budget failed.");
            if (report.bones < 25 || report.bones > 35) report.errors.Add("Bone budget failed.");
            if (report.materials > 2) report.errors.Add("Material budget failed.");
            foreach (var component in instance.GetComponentsInChildren<Component>(true))
            {
                if (component == null) { report.missingReferences++; continue; }
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        report.missingReferences++;
            }
            var root = instance.transform.Find("DevourerRig/Root");
            var mouthUpper = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Mouth_Upper");
            var mouthLower = instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Mouth_Lower");
            var skin = renderers.Single();
            report.rendererSkinQuality = skin.quality.ToString();
            var baked = new Mesh();
            Vector3[] sleepStart = null;
            Vector3[] returnEnd = null;
            try
            {
                foreach (var name in ClipNames)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(name));
                    if (clip == null) { report.errors.Add("Missing clip " + name); continue; }
                    var info = new ClipReport { name = name, seconds = clip.length,
                        loop = AnimationUtility.GetAnimationClipSettings(clip).loopTime,
                        minimumHeightMeters = float.PositiveInfinity };
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    info.bindings = bindings.Length;
                    info.unresolvedBindings = bindings.Count(b => !string.IsNullOrEmpty(b.path) && instance.transform.Find(b.path) == null);
                    if (info.unresolvedBindings > 0) report.errors.Add(name + " has unresolved skeleton bindings.");
                    if (info.loop != Loops(name)) report.errors.Add(name + " has an incorrect loop setting.");
                    Vector3[] first = null, last = null;
                    Vector3 rootStart = Vector3.zero;
                    float apertureStart = 0;
                    int frames = Mathf.RoundToInt(clip.length * 30);
                    for (int sample = 0; sample <= frames; sample++)
                    {
                        clip.SampleAnimation(instance, clip.length * sample / frames);
                        skin.BakeMesh(baked);
                        var points = baked.vertices;
                        // BakeMesh uses renderer-local coordinates. Convert to
                        // prefab meters for scale, bounds and motion checks.
                        var toRoot = instance.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                        for (int i = 0; i < points.Length; i++)
                        {
                            points[i] = toRoot.MultiplyPoint3x4(points[i]);
                            if (float.IsNaN(points[i].x) || float.IsInfinity(points[i].x)) report.errors.Add(name + " contains a nonfinite vertex.");
                            info.minimumHeightMeters = Mathf.Min(info.minimumHeightMeters, points[i].y);
                        }
                        if (sample == 0) { first = points; rootStart = root.position; apertureStart = Vector3.Distance(mouthUpper.position, mouthLower.position); }
                        else
                        {
                            for (int i = 0; i < points.Length; i++) info.maximumMotionMeters = Mathf.Max(info.maximumMotionMeters, Vector3.Distance(first[i], points[i]));
                            info.rootDisplacementMeters = Mathf.Max(info.rootDisplacementMeters, Vector3.Distance(rootStart, root.position));
                            info.mouthApertureChangeMeters = Mathf.Max(info.mouthApertureChangeMeters, Mathf.Abs(Vector3.Distance(mouthUpper.position, mouthLower.position) - apertureStart));
                        }
                        last = points;
                    }
                    if (info.maximumMotionMeters < .005f) report.errors.Add(name + " does not move the mesh.");
                    if (info.minimumHeightMeters < -.010f) report.errors.Add(name + " penetrates the ground.");
                    if (info.rootDisplacementMeters > .005f) report.errors.Add(name + " moves the root.");
                    if (info.loop)
                    {
                        for (int i = 0; i < first.Length; i++) info.loopEndpointErrorMeters = Mathf.Max(info.loopEndpointErrorMeters, Vector3.Distance(first[i], last[i]));
                        if (info.loopEndpointErrorMeters > .012f) report.errors.Add(name + " loop endpoint does not match.");
                    }
                    if (name == "Sleep") sleepStart = first;
                    if (name == "Return_To_Sleep") returnEnd = last;
                    report.clips.Add(info);
                }
                if (sleepStart != null && returnEnd != null)
                    for (int i = 0; i < sleepStart.Length; i++) report.returnSleepEndpointErrorMeters = Mathf.Max(report.returnSleepEndpointErrorMeters, Vector3.Distance(sleepStart[i], returnEnd[i]));
                if (report.returnSleepEndpointErrorMeters > .012f) report.errors.Add("Return_To_Sleep does not end in the sleeping pose.");
                var pairs = new[] {
                    new[] { "Sleep", "Wake_01" }, new[] { "Wake_01", "Wake_02" },
                    new[] { "Wake_02", "Wake_03" }, new[] { "Wake_03", "Full_Awaken" },
                    new[] { "Full_Awaken", "Idle" }
                };
                foreach (var pair in pairs)
                {
                    Vector3[] previous = null;
                    for (int endpoint = 0; endpoint < 2; endpoint++)
                    {
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(pair[endpoint]));
                        clip.SampleAnimation(instance, endpoint == 0 ? clip.length : 0);
                        skin.BakeMesh(baked);
                        var points = baked.vertices;
                        var toRoot = instance.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                        for (int i = 0; i < points.Length; i++) points[i] = toRoot.MultiplyPoint3x4(points[i]);
                        if (endpoint == 0) previous = points;
                        else for (int i = 0; i < points.Length; i++)
                            report.maximumAwakeningBoundaryErrorMeters = Mathf.Max(report.maximumAwakeningBoundaryErrorMeters, Vector3.Distance(previous[i], points[i]));
                    }
                }
                if (report.maximumAwakeningBoundaryErrorMeters > .012f) report.errors.Add("Awakening stage boundary poses do not match.");
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null || controller.layers[0].stateMachine.states.Length != 13) report.errors.Add("Animator states are incomplete.");
            if (controller != null && controller.layers[0].stateMachine.defaultState.name != "Sleep") report.errors.Add("Default state is not Sleep.");
            if (report.missingReferences > 0) report.errors.Add("Missing asset references.");
        }
        finally { if (bakeQuality != null) bakeQuality.Dispose(); PrefabUtility.UnloadPrefabContents(instance); }
        report.passed = report.errors.Count == 0 && report.clips.Count == 13;
        var output = Folder + "/Previews/" + (matchAuthoredWeights ? "UnityValidation.json" : "UnityValidation_ActiveQuality.json");
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        AssetDatabase.ImportAsset(output);
        return report;
    }

    // Render the actual URP prefab in an isolated preview scene. No gameplay
    // scene is opened, saved or modified by these review images.
    [MenuItem("Tools/Proof Devourer/Render Unity review")]
    public static void RenderReview()
    {
        RenderPoseReview(false);
    }

    [MenuItem("Tools/Proof Devourer/Render animation review frames")]
    public static void RenderMotionFrames()
    {
        RenderPoseReview(true);
    }

    [MenuItem("Tools/Proof Devourer/Render live skin check at project quality")]
    public static void RenderLiveSkinReview()
    {
        RenderPoseReview(false, true);
    }

    static void RenderPoseReview(bool motion, bool liveSkin = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var scene = EditorSceneManager.NewPreviewScene();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var bakedPose = new Mesh();
        var bakeQuality = liveSkin ? null : new BakeWeightsScope();
        try
        {
            instance.GetComponent<Animator>().enabled = false;
            var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            var frozen = new GameObject("Baked review pose");
            frozen.transform.SetParent(skin.transform, false);
            frozen.AddComponent<MeshFilter>().sharedMesh = bakedPose;
            var frozenRenderer = frozen.AddComponent<MeshRenderer>();
            frozenRenderer.sharedMaterials = skin.sharedMaterials;
            frozenRenderer.enabled = !liveSkin;
            skin.enabled = liveSkin;
            skin.updateWhenOffscreen = true;
            skin.forceMatrixRecalculationPerRender = true;
            var cameraGO = new GameObject("Review camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGO, scene);
            var camera = cameraGO.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .072f, .085f);
            camera.orthographic = true; camera.orthographicSize = 2.7f;
            camera.nearClipPlane = .05f; camera.farClipPlane = 50;
            camera.transform.position = new Vector3(6.2f, 4.6f, 10f);
            camera.transform.LookAt(new Vector3(0, 1.45f, .25f));
            for (int i = 0; i < 3; i++)
            {
                var lightGO = new GameObject("Review light " + i);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGO, scene);
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = i == 0 ? 1.6f : .8f;
                light.color = i == 1 ? new Color(.68f,.82f,1) : new Color(1,.84f,.65f);
                light.transform.rotation = Quaternion.Euler(i == 0 ? 40 : 25, i == 0 ? -145 : i == 1 ? -40 : 70, 0);
            }
            foreach (var name in ClipNames)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(name));
                if (motion)
                {
                    var output = "Temp/ProofDevourerReviewFrames/" + name;
                    Directory.CreateDirectory(output);
                    int frames = Mathf.RoundToInt(clip.length * 12);
                    for (int frame = 0; frame < frames; frame++)
                    {
                        clip.SampleAnimation(instance, clip.length * frame / frames);
                        skin.BakeMesh(bakedPose);
                        RenderCamera(camera, output + "/" + frame.ToString("D3") + ".png", 420, 420);
                    }
                }
                else
                {
                    clip.SampleAnimation(instance, name == "Sleep" || name == "Idle" ? 0 : clip.length * .48f);
                    if (!liveSkin) skin.BakeMesh(bakedPose);
                    RenderCamera(camera, Folder + "/Previews/" + (liveSkin ? "UnityLive_" : "Unity_") + name + ".png", 700, 700);
                }
            }
            if (!motion && !liveSkin)
            {
                AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath("Idle")).SampleAnimation(instance, 0);
                skin.BakeMesh(bakedPose);
                var bounds = frozenRenderer.bounds;
                var center = bounds.center;
                camera.aspect = 1;
                camera.orthographicSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) * .62f;
                var viewNames = new[] { "Front", "Side", "Back", "Top" };
                var positions = new[] { new Vector3(0,1,10), new Vector3(10,1,0), new Vector3(0,1,-10), new Vector3(0,10,0) };
                for (int i = 0; i < positions.Length; i++)
                {
                    camera.transform.position = center + positions[i];
                    if (i == 3) camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    else camera.transform.LookAt(center);
                    RenderCamera(camera, Folder + "/Previews/UnityModel_" + viewNames[i] + ".png", 700, 700);
                }
            }
        }
        finally { if (bakeQuality != null) bakeQuality.Dispose(); EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(bakedPose); }
        AssetDatabase.Refresh();
    }

    static void RenderCamera(Camera camera, string path, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var oldTarget = camera.targetTexture;
        Texture2D image = null;
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
