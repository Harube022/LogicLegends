using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.Tests.Khezu;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.Tests.Monsters.Editor
{
    /// <summary>Real Play Mode checks against the existing Animator, without advancing its clock manually.</summary>
    public static class RajangR1Verification
    {
        [Serializable] public sealed class Report
        {
            public string checkedUtc, scene;
            public bool passed, automaticSleep, sleepLoops, oneActiveMonster = true;
            public bool originalAnimator, rootMotionDisabled, queuedSequence, selectionSwitch;
            public bool smoothStageBlends, materialsSupported, finalSleep;
            public float rootPositionDrift, rootRotationDrift, rootScaleDrift;
            public int frames;
            public List<string> stages = new List<string>();
            public List<string> previews = new List<string>();
            public List<string> queuedStages = new List<string>();
            public List<string> errors = new List<string>();
            public string unverified = "Android hardware performance, multiplayer sessions and all Phase R2 gameplay are outside R1.";
        }
        static DevelopmentMonsterSelection settings;
        static KhezuAwakeningTestController driver;
        static Animator animator;
        static Report report;
        static Vector3 position, scale;
        static Quaternion rotation;
        static float started, phaseStarted;
        static int phase, stage, previewIndex, lastFrame;
        static string[] previews;
        static bool captured, sawBlend;
        static readonly string[] Stages = { "Sleep", "Wake_01", "Wake_02", "Wake_03", "Full_Awaken", "Idle" };
        static readonly List<string> queued = new List<string>();
        public static bool Running => report != null;

        [MenuItem("Tools/Logic Legends/Rajang R1/Run Play Mode Verification")]
        public static void Begin()
        {
            if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != RajangR1Setup.ScenePath)
                throw new InvalidOperationException("Enter Play Mode in OpenArchive_RajangR1_Test first.");
            if (Running) throw new InvalidOperationException("The R1 verification is already running.");
            settings = UnityEngine.Object.FindFirstObjectByType<DevelopmentMonsterSelection>();
            driver = settings.Rajang; animator = driver.Animator;
            report = new Report { checkedUtc = DateTime.UtcNow.ToString("o"), scene = RajangR1Setup.ScenePath,
                automaticSleep = settings.Selected == DevelopmentMonster.Rajang && driver.CurrentState == "Sleep",
                originalAnimator = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) == RajangR1Setup.AssetFolder + "/Animations/PrimalAxiom.controller",
                rootMotionDisabled = !animator.applyRootMotion,
                materialsSupported = driver.GetComponentsInChildren<SkinnedMeshRenderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported && m.shader.name == "Universal Render Pipeline/Lit")) };
            position = driver.transform.position; rotation = driver.transform.rotation; scale = driver.transform.lossyScale;
            settings.ResetRajang();
            started = phaseStarted = Time.unscaledTime;
            phase = stage = previewIndex = 0; lastFrame = -1; captured = sawBlend = false;
            previews = driver.AvailableAnimations.OrderBy(n => n).ToArray();
            queued.Clear();
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            try
            {
                if (!Application.isPlaying) { Fail("Play Mode ended before the verification finished."); return; }
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                report.frames++;
                report.oneActiveMonster &= settings.Khezu.gameObject.activeInHierarchy != settings.Rajang.gameObject.activeInHierarchy;
                report.rootPositionDrift = Mathf.Max(report.rootPositionDrift, Vector3.Distance(position, driver.transform.position));
                report.rootRotationDrift = Mathf.Max(report.rootRotationDrift, Quaternion.Angle(rotation, driver.transform.rotation));
                report.rootScaleDrift = Mathf.Max(report.rootScaleDrift, Vector3.Distance(scale, driver.transform.lossyScale));
                if (Time.unscaledTime - started > 75) { Fail("R1 playback verification timed out at phase " + phase + ", state " + driver.CurrentState); return; }
                var state = animator.GetCurrentAnimatorStateInfo(0);
                bool blending = animator.IsInTransition(0);
                float elapsed = Time.unscaledTime - phaseStarted;
                if (phase == 0)
                {
                    if (state.IsName("Sleep") && state.normalizedTime >= 1.05f && !blending)
                    {
                        report.sleepLoops = true; Capture("Sleep"); report.stages.Add("Sleep");
                        stage = 1; settings.Mistake1(); phase = 1; phaseStarted = Time.unscaledTime;
                    }
                }
                else if (phase == 1)
                {
                    sawBlend |= blending;
                    if (!captured && state.IsName(Stages[stage]) && state.normalizedTime > .6f && !blending)
                    {
                        Capture(Stages[stage]); report.stages.Add(Stages[stage]); captured = true;
                    }
                    if (stage < 4 && state.IsName(Stages[stage]) && state.normalizedTime >= 1.02f && !blending)
                    {
                        report.smoothStageBlends = stage == 1 ? sawBlend : report.smoothStageBlends && sawBlend;
                        stage++; captured = sawBlend = false; settings.TriggerMistake(stage);
                    }
                    else if (stage == 4 && state.IsName("Idle") && !blending)
                    {
                        report.smoothStageBlends &= sawBlend;
                        Capture("Idle"); report.stages.Add("Idle");
                        phase = 2; phaseStarted = Time.unscaledTime; settings.Preview(previews[0]);
                    }
                }
                else if (phase == 2 && elapsed > driver.BlendSeconds + .4f && !blending)
                {
                    string name = previews[previewIndex];
                    if (!state.IsName(name)) { Fail("Preview did not reach " + name + ": " + driver.CurrentState); return; }
                    report.previews.Add(name);
                    if (++previewIndex < previews.Length) settings.Preview(previews[previewIndex]);
                    else
                    {
                        settings.ResetRajang(); phase = 5;
                    }
                    phaseStarted = Time.unscaledTime;
                }
                else if (phase == 3)
                {
                    var current = driver.CurrentState;
                    if (Stages.Contains(current) && (queued.Count == 0 || queued[queued.Count - 1] != current)) queued.Add(current);
                    if (state.IsName("Idle") && !blending && driver.PlayingStage == 4)
                    {
                        report.queuedSequence = Stages.Skip(1).SequenceEqual(queued.Where(n => n != "Sleep"));
                        settings.SelectKhezu();
                        bool khezuOnly = settings.Khezu.gameObject.activeInHierarchy && !settings.Rajang.gameObject.activeInHierarchy;
                        settings.SelectRajang();
                        report.selectionSwitch = khezuOnly && !settings.Khezu.gameObject.activeInHierarchy && settings.Rajang.gameObject.activeInHierarchy;
                        phase = 4; phaseStarted = Time.unscaledTime;
                    }
                }
                else if (phase == 5 && elapsed > .4f && state.IsName("Sleep") && !blending)
                {
                    // Let the final preview blend completely back to Sleep before testing rapid inputs.
                    queued.Clear(); queued.Add("Sleep"); settings.Mistake4();
                    phase = 3; phaseStarted = Time.unscaledTime;
                }
                else if (phase == 4 && elapsed > .5f && state.IsName("Sleep") && !blending)
                {
                    report.finalSleep = true; Finish();
                }
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        static void Fail(string message) { if (report != null) { report.errors.Add(message); Finish(); } }
        static void Finish()
        {
            EditorApplication.update -= Tick;
            report.queuedStages = queued.ToList();
            report.passed = report.automaticSleep && report.sleepLoops && report.oneActiveMonster && report.originalAnimator &&
                report.rootMotionDisabled && report.queuedSequence && report.selectionSwitch && report.smoothStageBlends &&
                report.materialsSupported && report.finalSleep && report.rootPositionDrift < .0001f && report.rootRotationDrift < .001f &&
                report.rootScaleDrift < .0001f && report.stages.SequenceEqual(Stages) && report.previews.Count == 13 && report.errors.Count == 0;
            Directory.CreateDirectory(RajangR1Setup.Folder + "/Reports");
            File.WriteAllText(RajangR1Setup.Folder + "/Reports/PlayModeVerification.json", JsonUtility.ToJson(report, true));
            Debug.Log("Rajang R1 playback verification " + (report.passed ? "PASSED" : "FAILED") + "; " + report.frames + " frames, " + report.previews.Count + " animation previews.");
            report = null;
        }

        static void Capture(string name)
        {
            var camera = GameObject.Find("Rajang_Test_Review_Camera").GetComponent<Camera>();
            var target = RenderTexture.GetTemporary(1280, 900, 24);
            var previous = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(1280, 900, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0); pixels.Apply();
                Directory.CreateDirectory(RajangR1Setup.Folder + "/Previews");
                File.WriteAllBytes(RajangR1Setup.Folder + "/Previews/R1_" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
