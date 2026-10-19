#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Explicit Editor-only moving-target test on the original north route. Never saved in the scene.</summary>
    public sealed class KhezuPursuitPlayChecks : MonoBehaviour
    {
        const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuHunting/Reports";
        [Serializable] sealed class Sample
        {
            public float seconds, separation, monsterSpeed, targetSpeed, remainingDistance;
            public string state;
        }
        [Serializable] sealed class Report
        {
            public string scope = "Offline real NavMesh pursuit on the north route; test target uses its actual CharacterController at the existing 19.5 m/s run speed. Only the test player is repositioned. No monster teleport or player system edit.";
            public bool baseline, autoBraking, caughtWhileRunning, caughtAfterStopping;
            public float startSeparation, endSeparation, minimumRunChaseSpeed = 999, meanRunChaseSpeed, targetDistance, runSeconds;
            public float longestSlowdown;
            public List<string> passed = new List<string>(), failed = new List<string>();
            public List<Sample> samples = new List<Sample>();
        }
        readonly Report report = new Report();
        bool baseline;
        public static void Begin(bool baseline = false)
        {
            if (!Application.isPlaying || PhotonNetwork.InRoom) throw new InvalidOperationException("Run only in offline Play Mode.");
            var test = new GameObject("Khezu_Editor_Pursuit_Checks").AddComponent<KhezuPursuitPlayChecks>(); test.baseline = baseline;
        }
        void Check(bool ok, string name) { (ok ? report.passed : report.failed).Add(name); }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(.5f);
            report.baseline = baseline;
            var h = FindFirstObjectByType<KhezuHuntingController>();
            var player = global::Player.LocalInstance; var capsule = player.GetComponent<CharacterController>();
            var session = FindFirstObjectByType<OpenArchiveExplorationSession>();
            var cinema = h.GetComponent<KhezuCinematicJumpscareController>();
            var encounter = h.GetComponent<KhezuCaptureEncounterController>();
            bool wasPlayer = player.enabled, wasSession = session.enabled, wasCinema = cinema != null && cinema.enabled, wasEncounter = encounter != null && encounter.enabled;
            Vector3 originalPlayer = player.transform.position; Quaternion originalRotation = player.transform.rotation;
            bool inputBlocked = GameInput.Instance.GameplayInputBlocked;
            int captureBefore = h.CaptureSerial;
            try
            {
                session.enabled = false; player.enabled = false; GameInput.Instance.SetGameplayInputBlocked(true);
                if (encounter != null) encounter.enabled = false; if (cinema != null) cinema.enabled = false;
                h.Request(KhezuCommand.Sleep);
                var start = new Vector3(0, 0, 90);
                RaycastHit floor;
                if (!Physics.Raycast(start + Vector3.up * 30, Vector3.down, out floor, 60, ~(1 << 7), QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("North test route floor missing.");
                start.y = floor.point.y - (capsule.center.y - capsule.height * .5f) * player.transform.lossyScale.y + capsule.skinWidth;
                capsule.enabled = false; player.transform.SetPositionAndRotation(start, Quaternion.identity); capsule.enabled = true;
                player.ResetMotionAfterTeleport();
                h.Request(KhezuCommand.ForceFourMistakes);
                float deadline = Time.time + 25;
                while (h.State != KhezuHuntState.Hunting && h.State != KhezuHuntState.Caught && Time.time < deadline) yield return null;
                Check(h.State == KhezuHuntState.Hunting, "Awakens and begins the real north-route hunt");
                deadline = Time.time + 12;
                while (h.State != KhezuHuntState.Caught && Vector3.Distance(h.transform.position, player.transform.position) > 16 && Time.time < deadline) yield return null;
                Check(h.State != KhezuHuntState.Caught, "Runner can begin moving before capture range");
                report.startSeparation = FlatDistance(h.transform.position, player.transform.position);
                report.autoBraking = h.Agent.autoBraking;
                Vector3 runStart = player.transform.position;
                float started = Time.time, next = 0, sum = 0, slowing = 0; int count = 0;
                while (Time.time - started < 6 && h.State != KhezuHuntState.Caught)
                {
                    capsule.Move((Vector3.forward * h.normalPlayerRunSpeed + Vector3.down * 5) * Time.deltaTime);
                    float elapsed = Time.time - started;
                    float currentSpeed = h.Agent.enabled && h.Agent.isOnNavMesh ? h.Agent.velocity.magnitude : 0;
                    if (elapsed >= .8f)
                    {
                        slowing = currentSpeed < h.normalPlayerRunSpeed * .95f ? slowing + Time.deltaTime : 0;
                        report.longestSlowdown = Mathf.Max(report.longestSlowdown, slowing);
                    }
                    if (elapsed >= next)
                    {
                        next = elapsed + .1f;
                        float speed = currentSpeed;
                        report.samples.Add(new Sample { seconds = elapsed, separation = FlatDistance(h.transform.position, player.transform.position),
                            monsterSpeed = speed, targetSpeed = new Vector2(capsule.velocity.x, capsule.velocity.z).magnitude,
                            remainingDistance = h.Agent.enabled && h.Agent.isOnNavMesh && h.Agent.hasPath ? h.Agent.remainingDistance : 0, state = h.State.ToString() });
                        if (elapsed >= .8f) { report.minimumRunChaseSpeed = Mathf.Min(report.minimumRunChaseSpeed, speed); sum += speed; count++; }
                    }
                    yield return null;
                }
                report.runSeconds = Time.time - started;
                report.targetDistance = FlatDistance(runStart, player.transform.position);
                report.endSeparation = FlatDistance(h.transform.position, player.transform.position);
                report.meanRunChaseSpeed = count == 0 ? 0 : sum / count;
                report.caughtWhileRunning = h.State == KhezuHuntState.Caught;
                if (!baseline)
                {
                    Check(!report.autoBraking, "Moving-target pursuit does not use arrival auto-braking");
                    Check(!report.caughtWhileRunning && report.runSeconds >= 6, "Close runner gets at least six seconds of fair escape movement");
                    Check(report.targetDistance > h.normalPlayerRunSpeed * 6 * .9f, "Real CharacterController completes the run along the existing environment");
                    // Real route transitions can briefly reduce speed. Reject sustained stalls,
                    // while the separation and average-speed checks verify continuing pressure.
                    Check(report.longestSlowdown < .35f && report.minimumRunChaseSpeed > h.normalPlayerRunSpeed * .8f,
                        "Close pursuit recovers from brief route transitions without sustained slowing");
                    Check(report.endSeparation <= report.startSeparation + .75f && report.startSeparation - report.endSeparation < 7,
                        "Running neither opens a free gap nor causes an immediate capture");
                    Check(report.meanRunChaseSpeed > h.normalPlayerRunSpeed && report.meanRunChaseSpeed < h.normalPlayerRunSpeed * 1.12f,
                        "Close pursuit averages only slightly above the player's run speed");
                }
                deadline = Time.time + 4;
                while (h.State != KhezuHuntState.Caught && Time.time < deadline)
                { capsule.Move(Vector3.down * 5 * Time.deltaTime); yield return null; }
                report.caughtAfterStopping = h.State == KhezuHuntState.Caught;
                if (!baseline)
                {
                    Check(report.caughtAfterStopping && h.CaptureSerial == captureBefore + 1, "Stopping still allows one normal close-range capture");
                    Check(h.HuntSerial == 1 && h.TargetPlayerId == -1, "Moving pursuit keeps the selected target and hunting rotation");
                }
            }
            finally
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Folder + (baseline ? "/PursuitBaseline.json" : "/PursuitMovingTarget.json"), JsonUtility.ToJson(report, true));
                h.Request(KhezuCommand.Sleep);
                capsule.enabled = false; player.transform.SetPositionAndRotation(originalPlayer, originalRotation); capsule.enabled = true;
                player.ResetMotionAfterTeleport(); player.enabled = wasPlayer; session.enabled = wasSession;
                GameInput.Instance.SetGameplayInputBlocked(inputBlocked);
                if (cinema != null) cinema.enabled = wasCinema; if (encounter != null) encounter.enabled = wasEncounter;
                Debug.Log("Khezu pursuit check: " + report.passed.Count + " passed / " + report.failed.Count + " failed. Minimum running chase speed=" + report.minimumRunChaseSpeed);
                Destroy(gameObject);
            }
        }
        static float FlatDistance(Vector3 a, Vector3 b) { return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)); }
    }
}
#endif
