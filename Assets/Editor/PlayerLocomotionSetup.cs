#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Measures the actual rig's foot travel, rather than guessing a speed for every costume.</summary>
public static class PlayerLocomotionSetup
{
    public static readonly string[] Prefabs = {
        "Assets/Prefabs/PREFABS/CHARACTERS/Player.prefab",
        "Assets/Prefabs/PREFABS/CHARACTERS/Player_Female.prefab"
    };

    public static bool IsIdle(AnimatorState s) => s.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                s.name.IndexOf("idel", StringComparison.OrdinalIgnoreCase) >= 0;
    public static bool IsWalk(AnimatorState s) => s.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0;
    public static bool IsRun(AnimatorState s) => s.name.Equals("Run", StringComparison.OrdinalIgnoreCase);
    public static bool IsJump(AnimatorState s) => s.name.IndexOf("jump", StringComparison.OrdinalIgnoreCase) >= 0;

    [MenuItem("Logic Legends/Characters/Calibrate All Player Locomotion")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        // This old take contains 2.5 walk cycles. Its last half-cycle jumped back to
        // the opposite foot. Import exactly its first complete 12-frame cycle.
        var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/FINAL_ASSETS/CHARACTERS/EA.fbx");
        var imports = importer.clipAnimations;
        var walking = imports.First(c => c.name.Contains("Walking"));
        if (walking.lastFrame != 12 || !walking.loopTime)
        {
            walking.lastFrame = 12; walking.loopTime = true; walking.loopPose = true;
            importer.clipAnimations = imports; importer.SaveAndReimport();
        }
        foreach (string modelPath in new[] { "Assets/FINAL_ASSETS/CHARACTERS/SKINS/EKATUX.fbx",
                     "Assets/FINAL_ASSETS/CHARACTERS/SKINS/EATUX.fbx", "Assets/FINAL_ASSETS/CHARACTERS/FEMALE/WOMANMODEL1.fbx" })
        {
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            var clips = modelImporter.clipAnimations;
            if (clips.Length == 0) clips = modelImporter.defaultClipAnimations;
            bool changed = false;
            foreach (var clip in clips)
            {
                bool jump = clip.name.IndexOf("jump", StringComparison.OrdinalIgnoreCase) >= 0;
                if (clip.loopTime == !jump) continue;
                clip.loopTime = !jump; clip.loopPose = !jump; changed = true;
            }
            if (changed) { modelImporter.clipAnimations = clips; modelImporter.SaveAndReimport(); }
        }

        var femaleController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Animator/WOMANMODEL1.controller");
        var nativeFemale = AssetDatabase.LoadAllAssetsAtPath("Assets/FINAL_ASSETS/CHARACTERS/FEMALE/WOMANMODEL1.fbx")
            .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        foreach (var child in femaleController.layers[0].stateMachine.states)
        {
            var s = child.state;
            string suffix = IsWalk(s) ? "WALK" : IsJump(s) ? "JUMP" : IsIdle(s) ? "IDLE" : null;
            if (suffix != null) s.motion = nativeFemale.First(c => c.name.EndsWith("|" + suffix));
        }
        EditorUtility.SetDirty(femaleController);

        var configured = new HashSet<AnimatorController>();
        var report = new List<string>();
        foreach (string path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var driver in root.GetComponentsInChildren<PlayerAnimator>(true))
                {
                    var animator = driver.GetComponent<Animator>();
                    var controller = (AnimatorController)animator.runtimeAnimatorController;
                    var states = controller.layers[0].stateMachine.states.Select(c => c.state).ToArray();
                    var walk = states.First(IsWalk);
                    var run = states.FirstOrDefault(IsRun);
                    float walkSpeed = MeasureFootSpeed(driver.gameObject, (AnimationClip)walk.motion, (AnimationClip)states.First(IsIdle).motion);
                    float runSpeed = run == null ? walkSpeed : MeasureFootSpeed(driver.gameObject, (AnimationClip)run.motion, (AnimationClip)states.First(IsIdle).motion);
                    var so = new SerializedObject(driver);
                    so.FindProperty("sourceWalkSpeed").floatValue = walkSpeed;
                    so.FindProperty("sourceRunSpeed").floatValue = runSpeed;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    animator.applyRootMotion = false;
                    animator.updateMode = AnimatorUpdateMode.Normal;
                    if (configured.Add(controller)) ConfigureController(controller);
                    report.Add(driver.name + ": walk " + walkSpeed.ToString("F3") + ", run " + runSpeed.ToString("F3") + " units/s at playback 1; " + AssetDatabase.GetAssetPath(controller));
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        File.WriteAllLines("Library/LocomotionCalibration.txt", report);
        Debug.Log("Calibrated " + report.Count + " character variants across " + configured.Count + " controllers.");
    }

    static float MeasureFootSpeed(GameObject original, AnimationClip clip, AnimationClip idle)
    {
        var preview = EditorSceneManager.NewPreviewScene();
        var copy = UnityEngine.Object.Instantiate(original);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, preview);
        try
        {
            copy.SetActive(true); copy.GetComponent<Animator>().enabled = false;
            idle.SampleAnimation(copy, 0);
            var feet = FindFeet(copy);
            const int frames = 240;
            var positions = feet.Select(f => new Vector3[frames + 1]).ToArray();
            for (int i = 0; i <= frames; i++)
            {
                clip.SampleAnimation(copy, clip.length * i / frames);
                for (int f = 0; f < feet.Length; f++) positions[f][i] = copy.transform.InverseTransformPoint(feet[f].position);
            }
            float squaredVelocity = 0, backwardVelocity = 0;
            foreach (var track in positions)
            {
                float low = track.Min(v => v.y), high = track.Max(v => v.y);
                for (int i = 1; i <= frames; i++)
                {
                    float backwards = (track[i - 1].z - track[i].z) * frames / clip.length;
                    // Fit playback to the supporting foot's backward travel. Ignore
                    // the lifted foot returning forwards, and the turnaround poses.
                    if (backwards < .1f || (track[i].y + track[i - 1].y) * .5f > low + (high - low) * .35f) continue;
                    squaredVelocity += backwards * backwards; backwardVelocity += backwards;
                }
            }
            if (backwardVelocity <= 0) throw new InvalidOperationException("No supporting foot travel in " + original.name + " / " + clip.name);
            return squaredVelocity / backwardVelocity;
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); EditorSceneManager.ClosePreviewScene(preview); }
    }

    public static Transform[] FindFeet(GameObject model)
    {
        var bones = model.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r => r.bones).Where(b => b != null).Distinct().ToArray();
        float middle = bones.Average(b => model.transform.InverseTransformPoint(b.position).x);
        return new[] { bones.Where(b => model.transform.InverseTransformPoint(b.position).x < middle).OrderBy(b => model.transform.InverseTransformPoint(b.position).y).First(),
                       bones.Where(b => model.transform.InverseTransformPoint(b.position).x >= middle).OrderBy(b => model.transform.InverseTransformPoint(b.position).y).First() };
    }

    static void ConfigureController(AnimatorController controller)
    {
        foreach (string parameter in new[] { PlayerAnimator.WALK_PLAYBACK, PlayerAnimator.RUN_PLAYBACK })
            if (!controller.parameters.Any(p => p.name == parameter))
                controller.AddParameter(new AnimatorControllerParameter { name = parameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        var states = controller.layers[0].stateMachine.states.Select(c => c.state).ToArray();
        var idle = states.First(IsIdle); var walk = states.First(IsWalk); var jump = states.First(IsJump); var run = states.FirstOrDefault(IsRun);
        controller.layers[0].stateMachine.defaultState = idle;
        foreach (var s in new[] { idle, walk, jump, run }.Where(s => s != null))
        {
            foreach (var t in s.transitions.ToArray()) s.RemoveTransition(t);
            s.speed = 1;
            s.tag = s == jump ? "Jump" : s == idle ? "Idle" : "Locomotion";
            s.speedParameterActive = s == walk || s == run;
            s.speedParameter = s == walk ? PlayerAnimator.WALK_PLAYBACK : s == run ? PlayerAnimator.RUN_PLAYBACK : "";
        }
        foreach (var source in new[] { idle, walk, run }.Where(s => s != null))
            Connect(source, jump, .04f, new[] { "IsJumping" }, Array.Empty<string>());
        foreach (var source in new[] { idle, jump, walk, run }.Where(s => s != null))
        {
            if (source != idle) Connect(source, idle, .06f, Array.Empty<string>(), new[] { "IsWalking", "IsJumping" });
            if (source != walk) Connect(source, walk, .08f, new[] { "IsWalking" }, run == null ? new[] { "IsJumping" } : new[] { "IsJumping", "IsRunning" });
            if (run != null && source != run) Connect(source, run, .08f, new[] { "IsWalking", "IsRunning" }, new[] { "IsJumping" });
        }
        EditorUtility.SetDirty(controller);
    }

    static void Connect(AnimatorState from, AnimatorState to, float duration, string[] yes, string[] no)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false; t.hasFixedDuration = true; t.duration = duration;
        t.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
        // Only earlier, higher-priority transitions may interrupt the source.
        // Allowing this same transition to interrupt itself restarts its blend forever.
        t.orderedInterruption = true; t.canTransitionToSelf = false;
        foreach (string p in yes) t.AddCondition(AnimatorConditionMode.If, 0, p);
        foreach (string p in no) t.AddCondition(AnimatorConditionMode.IfNot, 0, p);
    }

    [MenuItem("Logic Legends/Characters/Audit Locomotion")]
    public static void Audit()
    {
        var rows = new List<object>();
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (string path in Prefabs)
            foreach (var driver in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<PlayerAnimator>(true))
            {
                var copy = UnityEngine.Object.Instantiate(driver.gameObject);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, preview);
                try
                {
                    copy.SetActive(true);
                    var animator = copy.GetComponent<Animator>();
                    animator.enabled = false;
                    var controller = (AnimatorController)animator.runtimeAnimatorController;
                    var states = controller.layers[0].stateMachine.states.Select(x => x.state).ToArray();
                    ((AnimationClip)states.First(IsIdle).motion).SampleAnimation(copy, 0);
                    var feet = FindFeet(copy);
                    foreach (var state in states.Where(s => s.motion is AnimationClip))
                    {
                        var clip = (AnimationClip)state.motion;
                        var samples = new List<object>();
                        var tracks = feet.Select(f => new List<Vector3>()).ToArray();
                        for (int f = 0; f <= 120; f++)
                        {
                            clip.SampleAnimation(copy, clip.length * f / 120);
                            var positions = feet.Select(b => copy.transform.InverseTransformPoint(b.position)).ToArray();
                            for (int b = 0; b < feet.Length; b++) tracks[b].Add(positions[b]);
                            if (f % 15 == 0) samples.Add(new { t = clip.length * f / 120, feet = positions.Select(v => v.ToString("F3")).ToArray() });
                        }
                        var settings = AnimationUtility.GetAnimationClipSettings(clip);
                        rows.Add(new { model = copy.name, controller = AssetDatabase.GetAssetPath(controller), state = state.name,
                            state.speed, clip = AssetDatabase.GetAssetPath(clip), clip.length, fps = clip.frameRate, loop = settings.loopTime,
                            feet = feet.Select(b => b.name).ToArray(),
                            travel = tracks.Select(t => new { sweep = t.Max(v => v.z) - t.Min(v => v.z),
                                seam = Vector3.Distance(t[0], t[t.Count - 1]),
                                backwards = Enumerable.Range(1, 120).Select(i => (t[i - 1].z - t[i].z) * 120 / clip.length).Where(v => v > .1f).OrderBy(v => v).ToArray() }).ToArray(),
                            missing = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform) && b.path != "" && copy.transform.Find(b.path) == null).Select(b => b.path).Distinct().ToArray(), samples });
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        File.WriteAllText("Library/LocomotionAudit.json", Newtonsoft.Json.JsonConvert.SerializeObject(rows, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("Locomotion audit saved to Library/LocomotionAudit.json");
    }
}
#endif
