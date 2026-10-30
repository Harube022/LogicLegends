using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in checks of the actual local presentation and existing shared entrance clock.</summary>
    public static class ArchiveGuardianEntranceChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope = "Fresh offline Editor Play. Exercises both original rigs through existing Admin replay/daylight commands, shared entrance/warning timestamps, camera/VFX reuse and scoped player locks. Does not verify remote Photon or Android rendering.";
            public List<string> checks = new List<string>(), errors = new List<string>(), observations = new List<string>();
        }
        static Report report;
        static IEnumerator flow;
        static double next;
        static ArchiveTopicSession session;
        static ArchiveWorldProgression world;
        static KhezuCinematicJumpscareController cinematic;
        static DevelopmentMonster originalSelection;
        static bool originalReduced, originalSuppress, createdTeammate;
        static Camera gameplayCamera;
        static bool gameplayCameraEnabled;
        static int gameplayCameraMask;
        static CameraClearFlags gameplayCameraClear;
        static UnityEngine.Object testLock;
        static readonly Dictionary<KhezuHuntingController, int[]> excluded = new Dictionary<KhezuHuntingController, int[]>();
        public static string Status { get; private set; } = "Idle";

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Guardian Entrance (Fresh Offline Play)")]
        public static void Begin()
        {
            if (!Application.isPlaying || PhotonNetwork.InRoom || flow != null)
                throw new InvalidOperationException("Use fresh offline MultiplayerGameplay Play Mode.");
            session = ArchiveTopicSession.Current;
            if (session == null || !session.Ready || session.worldProgression == null || Player.LocalInstance == null ||
                session.State.stations.Any(s => s.phase != 0) || Player.LocalInstance.IsMovementLocked)
                throw new InvalidOperationException("Start a fresh offline MultiplayerGameplay scene with an unlocked player.");
            world = session.worldProgression; cinematic = session.hunting.GetComponent<KhezuCinematicJumpscareController>();
            originalSelection = world.selector.Selected; originalReduced = cinematic.reducedMotion; originalSuppress = world.suppressThreatsForVerification;
            gameplayCamera = Camera.main; gameplayCameraEnabled = gameplayCamera != null && gameplayCamera.enabled;
            gameplayCameraMask = gameplayCamera != null ? gameplayCamera.cullingMask : 0;
            gameplayCameraClear = gameplayCamera != null ? gameplayCamera.clearFlags : CameraClearFlags.Nothing;
            createdTeammate = session.ActorPlayer(-2) == null; excluded.Clear();
            report = new Report(); flow = Flow(); next = 0; Status = "Starting";
            EditorApplication.update += Tick; EditorApplication.playModeStateChanged += PlayState; Application.logMessageReceived += Log;
            Save();
        }
        static void PlayState(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode && flow != null) Finish(false); }
        static void Log(string text, string stack, LogType type)
        { if (report != null && report.errors.Count < 12 && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) report.errors.Add(text + "\n" + stack); }
        static void Tick()
        {
            if (flow == null) return;
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup < next) return;
                if (!Application.isPlaying) throw new InvalidOperationException("Play Mode ended.");
                if (!flow.MoveNext()) { Finish(true); return; }
                next = EditorApplication.timeSinceStartup + (flow.Current is float seconds ? seconds : 0);
            }
            catch (Exception error) { report.errors.Add(Status + ": " + error); Finish(false); }
        }
        static void Check(bool passed, string label)
        { report.checks.Add((passed ? "PASS " : "FAIL ") + label); Save(); if (!passed) throw new InvalidOperationException(label); }
        static void Save()
        { Directory.CreateDirectory("Temp/ArchiveGuardianEntrance"); File.WriteAllText("Temp/ArchiveGuardianEntrance/Play.json", JsonUtility.ToJson(report, true)); }
        static Camera FilmCamera => (Camera)(typeof(ArchiveWorldProgression).GetProperty("FilmCamera")?.GetValue(world) ??
            typeof(ArchiveWorldProgression).GetField("filmCamera", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(world));
        static ArchiveGuardianEntranceVfx Effects => (ArchiveGuardianEntranceVfx)(typeof(ArchiveWorldProgression).GetProperty("EntranceEffects")?.GetValue(world) ??
            world.GetComponent<ArchiveGuardianEntranceVfx>());
        static bool GameplayRenderingRestored => gameplayCamera == null || gameplayCamera.enabled == gameplayCameraEnabled &&
            gameplayCamera.cullingMask == gameplayCameraMask && gameplayCamera.clearFlags == gameplayCameraClear;
        static Light[] RevealLights => world.GetComponentsInChildren<Light>(true).Where(l =>
            l.name == "Guardian_Reveal_Key" || l.name == "Guardian_Reveal_Rim").ToArray();
        static string GuardianState(double arrival)
        {
            var secondary = world.secondary;
            return "activeSelf=" + secondary.gameObject.activeSelf + ", activeHierarchy=" + secondary.gameObject.activeInHierarchy +
                ", enabled=" + secondary.enabled + ", initialized=" + secondary.Initialized + ", held=" + secondary.EntranceHeld +
                ", target=" + secondary.TargetPlayerId + ", state=" + secondary.State + ", released=" + session.State.worldTime.secondaryReleased +
                ", elapsed=" + (ArchiveTopicSession.Clock - arrival).ToString("F3") + ", film=" + world.FullscreenVisible +
                ", position=" + secondary.transform.position + ", status=" + secondary.Status;
        }
        static void ExcludeHunters()
        {
            foreach (var hunt in KhezuHuntingController.ActiveMonsters)
            {
                if (!excluded.ContainsKey(hunt)) excluded.Add(hunt, hunt.GetSnapshot().excluded ?? Array.Empty<int>());
                foreach (int actor in session.AdminTestActors) hunt.SetPlayerEligibility(actor, false);
            }
        }
        static Bounds VisualBounds()
        {
            var profile = world.selector.Selected == DevelopmentMonster.Rajang ? world.selector.KhezuEncounterProfile : world.selector.RajangEncounterProfile;
            var original = world.selector.ActiveTest.transform; var renderers = new List<Renderer>();
            foreach (var source in profile.visualRoots)
            {
                string path = AnimationUtility.CalculateTransformPath(source.transform, original);
                var copy = world.secondary.transform.Find(path);
                if (copy != null) renderers.AddRange(copy.GetComponentsInChildren<Renderer>().Where(r => r.enabled &&
                    (r is MeshRenderer || r is SkinnedMeshRenderer)));
            }
            if (renderers.Count == 0) throw new InvalidOperationException("No visible renderer bounds for the original secondary rig.");
            Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        static void CheckFraming(string label)
        {
            var camera = FilmCamera; var bounds = VisualBounds(); bool fits = true;
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var point = camera.WorldToViewportPoint(corner);
                fits &= point.z > camera.nearClipPlane && point.x >= -.035f && point.x <= 1.035f && point.y >= -.035f && point.y <= 1.035f;
            }
            Check(fits, label + ": complete visible original rig stays inside the camera frustum");
        }
        static IEnumerator Flow()
        {
            Check(Mathf.Approximately(world.entranceSeconds, 6) && Mathf.Approximately(world.warningSeconds, 5),
                "Existing six-second entrance and five-second warning remain unchanged");
            if (createdTeammate) Check(session.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate), "Existing Admin practice teammate supplies the second independent lock target");
            yield return .25f;
            world.suppressThreatsForVerification = false;
            Camera reusedCamera = null; RenderTexture reusedTexture = null; ArchiveGuardianEntranceVfx reusedEffects = null;
            foreach (var primary in new[] { DevelopmentMonster.Rajang, DevelopmentMonster.Khezu })
            {
                Status = primary + " initial / opposite guardian reveal";
                Check(session.RequestAdminTest(ArchiveAdminTestAction.ReturnDaylight), "Daylight command prepares " + primary + " profile test");
                yield return .25f; world.selector.Select(primary); ExcludeHunters();
                cinematic.reducedMotion = primary == DevelopmentMonster.Khezu;
                int mistakes = session.State.mistakes.Count;
                Check(session.RequestAdminTest(ArchiveAdminTestAction.ReplayEntrance), "Existing Admin command starts the shared entrance for " + primary);
                double arrival = session.State.worldTime.arrivalStartedAt;
                yield return .45f; ExcludeHunters();
                report.observations.Add(primary + " initial: " + GuardianState(arrival)); Save();
                // SetActive occurs in LateUpdate; the newly activated hunting driver
                // initializes in Start on its first subsequent player-loop frame.
                // Wait only for that lifecycle boundary, never through the six-second
                // reveal or into the warning/release interval.
                double initializedUntil = EditorApplication.timeSinceStartup + 2;
                while ((!world.secondary.Initialized || !world.secondary.EntranceHeld) &&
                    EditorApplication.timeSinceStartup < initializedUntil && ArchiveTopicSession.Clock - arrival < world.entranceSeconds - .2f)
                    yield return .05f;
                report.observations.Add(primary + " ready: " + GuardianState(arrival)); Save();
                Check(world.FullscreenVisible && session.NightEntranceActive && world.IntroLocksHeld == session.AdminTestActors.Length,
                    "Fullscreen film owns each eligible actor lock during " + primary + " match");
                Check(session.AdminTestActors.All(a => session.ActorPlayer(a).IsMovementLocked), "Both practice players remain movement-locked through the film");
                Check(world.secondary.Initialized && world.secondary.EntranceHeld && world.secondary.TargetPlayerId == 0 &&
                    !session.State.worldTime.secondaryReleased && ArchiveTopicSession.Clock - arrival < world.entranceSeconds,
                    "Secondary guardian is stationary and cannot target before the authoritative release: " + GuardianState(arrival));
                Check(world.SecondaryName == (primary == DevelopmentMonster.Rajang ? "Khezu" : "Rajang"), "Night profile is the opposite original monster");
                var camera = FilmCamera; var fx = Effects;
                Check(camera != null && camera.enabled && camera.targetTexture != null && camera.targetTexture.IsCreated(), "Entrance camera uses a valid reusable target");
                Check(camera.targetTexture.width <= 1280 && camera.targetTexture.width <= Mathf.Max(320, Screen.width), "Film target respects the native mobile-resolution budget");
                float nativeAspect = Mathf.Clamp((float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height), .6f, 2.5f);
                Check(Mathf.Abs((float)camera.targetTexture.width / camera.targetTexture.height - nativeAspect) < .01f &&
                    Mathf.Abs(camera.aspect - nativeAspect) < .01f, "Film render target and camera preserve the native screen aspect");
                Check(gameplayCamera == null || gameplayCamera.enabled == gameplayCameraEnabled &&
                    gameplayCamera.cullingMask == 0 && gameplayCamera.clearFlags == CameraClearFlags.Nothing,
                    "Fullscreen film suppresses the covered world draw while preserving enabled MainCamera consumers");
                var revealLights = RevealLights;
                Check(revealLights.Length == 2 && revealLights.All(l => l.enabled && l.type == LightType.Spot && l.shadows == LightShadows.None),
                    "Exactly two local shadowless spotlights illuminate the fullscreen guardian reveal");
                Check(fx != null && fx.Visible && fx.ActiveRendererCount <= 3 && fx.CosmeticElementCount <= ArchiveGuardianEntranceVfx.MaximumCosmeticElements,
                    "Formation uses the bounded reusable three-renderer VFX pool");
                var pool = fx.transform.Find("Guardian_Entrance_Cosmetic_Pool");
                Check(pool != null && pool.GetComponentsInChildren<Renderer>(true).Length == 3,
                    "The formation pool contains exactly three actual renderers");
                Check(fx.GroundSampleCount == 81 && fx.GroundSurfaceCount > 0, "Formation ground conformity uses the bounded 81-sample cached surface grid");
                if (reusedCamera == null) { reusedCamera = camera; reusedTexture = camera.targetTexture; reusedEffects = fx; }
                else Check(camera == reusedCamera && camera.targetTexture == reusedTexture && fx == reusedEffects, "The second profile reuses camera, render target and VFX owner");
                Check(world.GetComponentsInChildren<Camera>(true).Count(c => c.targetTexture != null) == 1 && world.GetComponents<ArchiveGuardianEntranceVfx>().Length == 1,
                    "Replays do not duplicate cinematic cameras or VFX components");
                CheckFraming(primary + " wide reveal");
                if (cinematic.reducedMotion)
                {
                    var update = typeof(ArchiveWorldProgression).GetMethod("UpdateFilmCamera", BindingFlags.Instance | BindingFlags.NonPublic);
                    update.Invoke(world, new object[] { .1f }); var position = camera.transform.position; var rotation = camera.transform.rotation;
                    update.Invoke(world, new object[] { .8f });
                    Check(Vector3.Distance(position, camera.transform.position) < .001f && Quaternion.Angle(rotation, camera.transform.rotation) < .01f,
                        "Reduced-motion reveal keeps a steady camera across the full entrance");
                }
                yield return 3.4f;
                var revealFade=world.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(i=>i.name=="Reveal_Fade");
                Check(revealFade.color.a<.01f,"Opening fade is fully transparent throughout the actual monster reveal");
                CheckFraming(primary + " close reveal");
                ScreenCapture.CaptureScreenshot("Temp/ArchiveGuardianEntrance/" + world.SecondaryName + "_Reveal.png");
                float remaining = (float)(arrival + world.entranceSeconds - ArchiveTopicSession.Clock);
                if (remaining > 0) yield return remaining + .25f;
                Check(!world.FullscreenVisible && world.WarningVisible && world.IntroLocksHeld == 0 &&
                    session.AdminTestActors.All(a => !session.ActorPlayer(a).IsMovementLocked), "Film ends and releases its scoped locks before the warning");
                Check(GameplayRenderingRestored, "Gameplay camera enabled state, culling mask and clear flags return to their exact originals");
                Check(!fx.Visible && !camera.enabled && !session.State.worldTime.secondaryReleased && world.secondary.EntranceHeld,
                    "Formation VFX clears while the existing five-second warning still holds the guardian");
                Check(RevealLights.Length == 2 && RevealLights.All(l => !l.enabled), "Both reveal-only spotlights turn off during the warning");
                Check(session.State.mistakes.Count == mistakes, "Presentation creates no academic mistakes");
                remaining = (float)(arrival + world.entranceSeconds + world.warningSeconds - ArchiveTopicSession.Clock);
                if (remaining > 0) yield return remaining + .3f;
                Check(session.State.worldTime.secondaryReleased && !world.secondary.EntranceHeld && !world.FullscreenVisible,
                    "Only the existing world-time owner releases the guardian after the complete warning");
                double oldArrival = session.State.worldTime.arrivalStartedAt; yield return .3f;
                Check(session.State.worldTime.arrivalStartedAt == oldArrival && !world.FullscreenVisible && world.IntroLocksHeld == 0,
                    "An already elapsed shared entrance does not replay on subsequent presentation updates");
            }
            Status = "Scoped lock and disable cleanup";
            session.RequestAdminTest(ArchiveAdminTestAction.ReturnDaylight); yield return .25f;
            testLock = new GameObject("Guardian_Entrance_External_Lock_Fixture");
            var local = Player.LocalInstance; local.AcquireMovementLock(testLock, "Guardian test: another encounter owns this player", local.transform.position, 100);
            Check(session.RequestAdminTest(ArchiveAdminTestAction.ReplayEntrance), "Admin replay can coexist with a separately owned movement lock"); yield return .3f;
            Check(!world.FullscreenVisible && local.IsMovementLocked && world.IntroLocksHeld == 0, "Another encounter's lock suppresses the local film and stays owned by that encounter");
            Check(GameplayRenderingRestored, "A separately locked player's skipped film leaves original camera rendering intact");
            Check(RevealLights.All(l => !l.enabled), "A skipped film leaves its temporary reveal lighting off");
            session.RequestAdminTest(ArchiveAdminTestAction.ReturnDaylight); yield return .2f;
            Check(local.IsMovementLocked, "Daylight cleanup cannot release another system's lock");
            local.ReleaseMovementLock(testLock); UnityEngine.Object.DestroyImmediate(testLock); testLock = null;
            ExcludeHunters(); session.RequestAdminTest(ArchiveAdminTestAction.ReplayEntrance); yield return .4f;
            Check(world.FullscreenVisible, "Fresh Admin replay restarts the same presentation resources");
            world.enabled = false; yield return .15f;
            Check(!world.FullscreenVisible && !world.WarningVisible && world.IntroLocksHeld == 0 && !local.IsMovementLocked &&
                !FilmCamera.enabled && !Effects.Visible, "Disabling the presenter clears every overlay, effect, camera and owned movement lock");
            Check(GameplayRenderingRestored, "Disable cleanup restores the original gameplay camera state, mask and clear flags");
            Check(RevealLights.Length == 2 && RevealLights.All(l => !l.enabled), "Disable cleanup turns off both temporary reveal spotlights");
            session.RequestAdminTest(ArchiveAdminTestAction.ReturnDaylight); world.enabled = true; yield return .2f;
        }
        static void Finish(bool success)
        {
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayState; Application.logMessageReceived -= Log; flow = null;
            try
            {
                if (Player.LocalInstance != null && testLock != null) Player.LocalInstance.ReleaseMovementLock(testLock);
                if (testLock != null) UnityEngine.Object.DestroyImmediate(testLock); testLock = null;
                if (session != null && Application.isPlaying)
                {
                    session.RequestAdminTest(ArchiveAdminTestAction.ReturnDaylight);
                    if (createdTeammate) session.RemoveAdminTeammate();
                }
                if (world != null)
                {
                    world.enabled = true; world.suppressThreatsForVerification = originalSuppress;
                    world.selector.Select(originalSelection);
                }
                if (cinematic != null) cinematic.reducedMotion = originalReduced;
                foreach (var entry in excluded) if (entry.Key != null)
                {
                    foreach (int actor in new[] { -1, -2 }) entry.Key.SetPlayerEligibility(actor, !entry.Value.Contains(actor));
                    if(entry.Key.gameObject.activeInHierarchy)entry.Key.Request(KhezuCommand.Sleep);
                }
                if (gameplayCamera != null)
                { gameplayCamera.enabled = gameplayCameraEnabled; gameplayCamera.cullingMask = gameplayCameraMask; gameplayCamera.clearFlags = gameplayCameraClear; }
            }
            catch (Exception error) { report.errors.Add("Cleanup: " + error); }
            report.passed = success && report.errors.Count == 0; Status = report.passed ? "Passed" : "Failed"; Save();
        }
    }
}
