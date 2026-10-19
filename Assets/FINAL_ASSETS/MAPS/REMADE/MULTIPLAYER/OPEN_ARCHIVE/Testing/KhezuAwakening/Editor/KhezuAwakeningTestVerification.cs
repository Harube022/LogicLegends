using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogicLegends.Tests.Khezu.Editor
{
    public static class KhezuAwakeningTestVerification
    {
        [Serializable] public class CheckResult { public string name; public bool passed; public string detail; }
        [Serializable] public class ClipCheck
        {
            public string name;
            public float seconds, maximumBoneMotionMeters;
            public bool loop;
            public int bindings, unresolvedBindings;
        }
        [Serializable] public class Report
        {
            public string checkedUtc, unityVersion, scene;
            public bool passed;
            public List<CheckResult> checks = new List<CheckResult>();
            public List<ClipCheck> clips = new List<ClipCheck>();
            public string[] missingAnimations;
            public int observedFrames;
            public float maximumRootDisplacementMeters, maximumRootRotationDegrees;
            public float maximumRigRootDisplacementMeters, maximumRigRootRotationDegrees;
            public float maximumBoundaryLocalPositionErrorMeters, maximumBoundaryLocalRotationErrorDegrees;
            public float maximumBoneStepMetersAtFrameIntervalsUnder50ms;
            public Vector3 initialMonsterWorldScale;
            public string limits = "Unity Editor Play Mode only. No Android build/device, network session, navigation, capture/barrier/attack gameplay or player interaction tested. Pose continuity uses original clip boundaries, observed crossfades and frame samples; no replacement clips or rig edits.";
        }
        static Report report;
        static KhezuAwakeningTestController test;
        static Vector3 rootPosition, rigPosition;
        static Quaternion rootRotation, rigRotation;
        static Transform rigRoot;
        static Transform[] bones;
        static Vector3[] lastBones;
        static float lastSampleTime;
        static bool running;

        [MenuItem("Tools/Logic Legends/Khezu Phase 1/Verify In Play Mode")]
        public static void Run()
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().path != KhezuAwakeningTestSetup.ScenePath)
                throw new InvalidOperationException("Open the saved Khezu test scene and enter Play Mode first.");
            if (running) throw new InvalidOperationException("Verification is already running.");
            test = UnityEngine.Object.FindFirstObjectByType<KhezuAwakeningTestController>();
            if (test == null) throw new InvalidOperationException("No Khezu test controller.");
            report = new Report { checkedUtc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion,
                scene = SceneManager.GetActiveScene().path, initialMonsterWorldScale = test.transform.lossyScale };
            rigRoot = test.GetComponentsInChildren<Transform>().Single(t => t.name == "Root");
            bones = test.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            rootPosition = test.transform.position; rootRotation = test.transform.rotation;
            rigPosition = rigRoot.position; rigRotation = rigRoot.rotation;
            lastBones = bones.Select(b => b.position).ToArray();
            lastSampleTime = Time.time;
            running = true;
            test.StartCoroutine(RunChecks());
        }

        static void Check(string name, bool passed, string detail = "")
        {
            report.checks.Add(new CheckResult { name = name, passed = passed, detail = detail });
        }
        static IEnumerator WaitFor(Func<bool> condition, float timeout, string label)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end) { Sample(); yield return null; }
            Check(label, condition(), test.CurrentState + ", mistakes " + test.Mistakes + ", stage " + test.PlayingStage);
        }
        static IEnumerator Observe(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { Sample(); yield return null; }
        }
        static bool State(string name) { return test.CurrentState == name && !test.Animator.IsInTransition(0); }
        static void ClickUI(string name)
        {
            var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Check("On-screen button is reachable: " + name, hits.Count > 0 && hits[0].gameObject == button.gameObject);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static void Sample()
        {
            report.observedFrames++;
            report.maximumRootDisplacementMeters = Mathf.Max(report.maximumRootDisplacementMeters, Vector3.Distance(rootPosition, test.transform.position));
            report.maximumRootRotationDegrees = Mathf.Max(report.maximumRootRotationDegrees, Quaternion.Angle(rootRotation, test.transform.rotation));
            report.maximumRigRootDisplacementMeters = Mathf.Max(report.maximumRigRootDisplacementMeters, Vector3.Distance(rigPosition, rigRoot.position));
            report.maximumRigRootRotationDegrees = Mathf.Max(report.maximumRigRootRotationDegrees, Quaternion.Angle(rigRotation, rigRoot.rotation));
            float dt = Time.time - lastSampleTime;
            for (int i = 0; i < bones.Length; i++)
            {
                var position = bones[i].position;
                if (dt > 0 && dt <= .05f) report.maximumBoneStepMetersAtFrameIntervalsUnder50ms =
                    Mathf.Max(report.maximumBoneStepMetersAtFrameIntervalsUnder50ms, Vector3.Distance(position, lastBones[i]));
                lastBones[i] = position;
            }
            lastSampleTime = Time.time;
        }

        static IEnumerator RunChecks()
        {
            bool completed = false;
            try
            {
                Check("Sleep starts automatically without a test command", test.Mistakes == 0 && test.PlayingStage == 0 && State("Sleep") && !test.IsPreviewing);
                var scale = test.transform.lossyScale;
                Check("At original MonsterSpawnPoint, positive uniform scale", test.transform.parent.name == "MonsterSpawnPoint" && test.transform.localPosition == Vector3.zero && test.transform.localRotation == Quaternion.identity && scale.x > 0 && Mathf.Abs(scale.x - scale.y) < .0001f && Mathf.Abs(scale.x - scale.z) < .0001f);
                // Scene prefab correspondence is unavailable on runtime instances. Compare the
                // actual mesh, avatar and controller assets; verify the saved prefab link in Edit Mode.
                var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KhezuAwakeningTestSetup.PrefabPath);
                Check("Original mesh, rig and controller assets", test.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh == sourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh && test.Animator.avatar == sourcePrefab.GetComponent<Animator>().avatar && test.Animator.avatar.isValid && AssetDatabase.GetAssetPath(test.Animator.runtimeAnimatorController) == KhezuAwakeningTestSetup.ControllerPath);
                Check("Root motion disabled; no movement or gameplay components", !test.Animator.applyRootMotion && test.GetComponentsInChildren<MonoBehaviour>(true).Length == 1 && test.GetComponentsInChildren<Rigidbody>(true).Length == 0 && test.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true).Length == 0);
                ValidateOriginalClips();
                yield return Observe(9.1f);
                Check("Sleep loops for more than two cycles", State("Sleep") && test.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 2);
                KhezuAwakeningTestSetup.Capture("01_Sleep_PlayMode");
                for (int stage = 1; stage <= 4; stage++)
                {
                    string name = stage < 4 ? "Wake_0" + stage : "Full_Awaken";
                    ClickUI("Khezu_AddMistake");
                    Check("On-screen mistake button requests stage " + stage, test.Mistakes == stage);
                    yield return WaitFor(() => test.Animator.IsInTransition(0), 1, name + " starts with crossfade");
                    yield return WaitFor(() => State(name), 2, name + " becomes active");
                    yield return Observe(.3f);
                    KhezuAwakeningTestSetup.Capture("0" + (stage + 1) + "_" + name + "_PlayMode");
                    if (stage < 4)
                    {
                        yield return WaitFor(() => State(name) && test.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.1f, 5, name + " reaches and holds final pose");
                        yield return Observe(.25f);
                        Check(name + " holds until another mistake", State(name) && test.Mistakes == stage);
                    }
                }
                yield return WaitFor(() => State("Idle"), 6, "Full_Awaken automatically transitions to Idle");
                KhezuAwakeningTestSetup.Capture("06_Idle_PlayMode");
                yield return Observe(3.6f);
                Check("Idle loops at four mistakes", State("Idle") && test.Mistakes == 4 && test.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1);
                ClickUI("Khezu_ResetSleep");
                yield return WaitFor(() => State("Sleep"), 2, "Reset blends to Sleep");
                Check("Reset clears queued stages and preview", test.Mistakes == 0 && test.PlayingStage == 0 && !test.IsPreviewing);

                // Awaken All queues the four existing one-shots without skipping any stage.
                ClickUI("Khezu_AwakenAll");
                for (int i = 0; i < 2; i++) test.IncreaseMistakeCount();
                Check("Mistakes clamp to four", test.Mistakes == 4);
                for (int stage = 1; stage <= 4; stage++)
                {
                    string name = stage < 4 ? "Wake_0" + stage : "Full_Awaken";
                    yield return WaitFor(() => State(name), 6, "Queued sequence visits " + name);
                }
                yield return WaitFor(() => State("Idle"), 6, "Queued sequence ends in Idle");
                foreach (var name in test.AvailableAnimations)
                {
                    Check("Preview accepts " + name, test.PreviewAnimation(name));
                    yield return WaitFor(() => State(name), 2, "Preview plays " + name);
                    yield return Observe(.2f);
                }
                Check("Unknown animation is rejected", !test.PreviewAnimation("Missing_Clip"));
                // Reset during a preview and cancel an unfinished rapid-click queue.
                ClickUI("Khezu_ResetSleep");
                yield return WaitFor(() => State("Sleep"), 2, "Reset exits preview");
                ClickUI("Khezu_AwakenAll");
                yield return WaitFor(() => State("Wake_01"), 2, "Cancellation test enters Wake_01");
                ClickUI("Khezu_ResetSleep");
                yield return WaitFor(() => State("Sleep"), 2, "Reset interrupts active awakening smoothly");
                yield return Observe(3);
                Check("Cancelled queue does not restart", State("Sleep") && test.Mistakes == 0 && test.PlayingStage == 0);
                Check("Assembly never translates or turns", report.maximumRootDisplacementMeters < .0001f && report.maximumRootRotationDegrees < .01f);
                Check("Authored rig root never translates or turns", report.maximumRigRootDisplacementMeters < .0001f && report.maximumRigRootRotationDegrees < .01f);
                Check("Chosen monster size is preserved through all animations", Vector3.Distance(test.transform.lossyScale, report.initialMonsterWorldScale) < .0001f);
                completed = true;
            }
            finally
            {
                running = false;
                Check("Verification completes without interruption", completed);
                report.passed = report.checks.Count > 0 && report.checks.All(c => c.passed);
                File.WriteAllText(KhezuAwakeningTestSetup.Folder + "/Reports/PlayModeValidation.json", JsonUtility.ToJson(report, true));
                Debug.Log("Khezu Phase 1 checks finished: " + report.checks.Count(c => c.passed) + "/" + report.checks.Count + ". See PlayModeValidation.json.");
            }
        }

        static void ValidateOriginalClips()
        {
            var instance = PrefabUtility.LoadPrefabContents(KhezuAwakeningTestSetup.PrefabPath);
            try
            {
                var animator = instance.GetComponent<Animator>();
                var transforms = instance.GetComponentsInChildren<Transform>(true);
                var clips = animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
                var required = new[] { "Sleep", "Wake_01", "Wake_02", "Wake_03", "Full_Awaken", "Idle" };
                report.missingAnimations = required.Except(clips.Select(c => c.name)).ToArray();
                Check("All six required animations exist", report.missingAnimations.Length == 0);
                foreach (var clip in clips)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    var item = new ClipCheck { name = clip.name, seconds = clip.length, loop = clip.isLooping,
                        bindings = bindings.Length, unresolvedBindings = bindings.Count(b => b.path.Length > 0 && instance.transform.Find(b.path) == null) };
                    clip.SampleAnimation(instance, 0);
                    var start = transforms.Select(t => t.position).ToArray();
                    for (int sample = 1; sample <= Mathf.CeilToInt(clip.length * 30); sample++)
                    {
                        clip.SampleAnimation(instance, Mathf.Min(clip.length, sample / 30f));
                        for (int i = 0; i < transforms.Length; i++) item.maximumBoneMotionMeters = Mathf.Max(item.maximumBoneMotionMeters, Vector3.Distance(start[i], transforms[i].position));
                    }
                    report.clips.Add(item);
                    Check("Original clip bindings and motion: " + clip.name, item.bindings > 0 && item.unresolvedBindings == 0 && item.maximumBoneMotionMeters > .001f);
                }
                for (int pair = 0; pair < required.Length - 1; pair++)
                {
                    var previous = clips.Single(c => c.name == required[pair]);
                    var next = clips.Single(c => c.name == required[pair + 1]);
                    previous.SampleAnimation(instance, previous.length);
                    var p = transforms.Select(t => t.localPosition).ToArray();
                    var q = transforms.Select(t => t.localRotation).ToArray();
                    next.SampleAnimation(instance, 0);
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        report.maximumBoundaryLocalPositionErrorMeters = Mathf.Max(report.maximumBoundaryLocalPositionErrorMeters, Vector3.Distance(p[i], transforms[i].localPosition));
                        report.maximumBoundaryLocalRotationErrorDegrees = Mathf.Max(report.maximumBoundaryLocalRotationErrorDegrees, Quaternion.Angle(q[i], transforms[i].localRotation));
                    }
                }
                Check("Adjacent original awakening boundary poses match", report.maximumBoundaryLocalPositionErrorMeters < .001f && report.maximumBoundaryLocalRotationErrorDegrees < .1f);
                Check("Sleep and Idle loop; awakening clips do not", clips.Single(c => c.name == "Sleep").isLooping && clips.Single(c => c.name == "Idle").isLooping && required.Skip(1).Take(4).All(n => !clips.Single(c => c.name == n).isLooping));
            }
            finally { PrefabUtility.UnloadPrefabContents(instance); }
        }
    }
}
