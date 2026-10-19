#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Photon.Realtime;
using Unity.Cinemachine;
using UnityEngine;

namespace LogicLegends.Tests.Khezu
{
    public sealed class KhezuCinematicLifecycleChecks : MonoBehaviour
    {
        const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuCinematic/Reports";
        [Serializable] sealed class Report { public List<string> passed = new List<string>(), failed = new List<string>(); public string scope = "Offline snapshot fixtures and disconnect callback; not a live multiplayer test"; }
        readonly Report report = new Report();
        KhezuCinematicJumpscareController c; KhezuHuntingController h; KhezuCaptureEncounterController e;
        int barriers, challenges;
        void Check(bool ok, string name) { (ok ? report.passed : report.failed).Add(name); }
        public static void Begin()
        {
            if (PhotonNetwork.InRoom) throw new InvalidOperationException("Lifecycle fixtures run offline only.");
            new GameObject("Khezu_Cinematic_Lifecycle_Checks").AddComponent<KhezuCinematicLifecycleChecks>();
        }
        void Capture(int actor, float age, int phase = 0)
        {
            var s = h.GetSnapshot(); s.state = (int)KhezuHuntState.Caught; s.caught = s.target = actor;
            s.captureSerial++; s.captureStartedAt = KhezuHuntingController.EncounterClock - age; s.encounterPhase = phase;
            s.animation = "Idle"; s.animationSpeed = 1; s.pending = false; s.awake = true;
            h.ApplySnapshot(s, true);
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(.5f);
            c = FindFirstObjectByType<KhezuCinematicJumpscareController>(); h = c.GetComponent<KhezuHuntingController>(); e = c.GetComponent<KhezuCaptureEncounterController>();
            var ui = FindFirstObjectByType<KhezuCinematicTestControls>(); var p = global::Player.LocalInstance;
            var brain = Camera.main.GetComponent<CinemachineBrain>(); var channels = brain.ChannelMask;
            e.hostBarrierActivated.AddListener((a, s) => barriers++); e.localChallengeRequested.AddListener((a, s) => challenges++);
            h.Request(KhezuCommand.ResetHunt);
            int starts = c.Starts;
            Capture(999, 0); yield return new WaitForSecondsRealtime(.3f);
            Check(c.Starts == starts && !c.IsPlaying && p.enabled && !GameInput.Instance.GameplayInputBlocked && brain.ChannelMask == channels, "Uncaught actor fixture leaves local camera and controls unchanged");
            Check(h.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Jumpscare"), "Observer fixture receives the quick world Jumpscare by 0.3 seconds");
            h.Request(KhezuCommand.ResetHunt); yield return null;

            int roars = c.RoarMarkers;
            float lateAge = KhezuCinematicJumpscareController.RealTime(2.5f);
            Capture(-1, lateAge, 2); yield return new WaitForSecondsRealtime(.12f);
            Check(c.IsPlaying && c.Elapsed >= lateAge && c.Elapsed < KhezuCinematicJumpscareController.Duration, "Late capture checkpoint resumes elapsed time");
            Check(c.RoarMarkers == roars, "Late checkpoint does not stack an expired roar cue");
            while (c.IsPlaying) yield return null;
            yield return new WaitForSecondsRealtime(.15f);
            Check(barriers == 1 && challenges == 1 && ui.handoffPanel.activeSelf, "Resumed capture completes one host barrier and local handoff");
            ScreenCapture.CaptureScreenshot(Folder + "/Phase3_Handoff.png"); yield return new WaitForEndOfFrame(); yield return null;
            h.Request(KhezuCommand.ResetHunt); yield return null;

            int b = barriers, q = challenges;
            Capture(-1, 0); yield return new WaitForSecondsRealtime(.45f);
            Check(c.IsPlaying && !p.enabled, "New capture starts a fresh local sequence");
            h.Request(KhezuCommand.Sleep); yield return null; yield return null;
            Check(!c.IsPlaying && p.enabled && !GameInput.Instance.GameplayInputBlocked && brain.ChannelMask == channels, "Host sleep/reset during cinematic restores original camera and controls");
            yield return new WaitForSecondsRealtime(.2f);
            Check(barriers == b && challenges == q && !ui.handoffPanel.activeSelf, "Interrupted encounter emits no barrier or challenge");

            Capture(-1, 0); yield return new WaitForSecondsRealtime(.3f);
            e.OnDisconnected(DisconnectCause.ClientTimeout); yield return null;
            Check(!c.IsPlaying && p.enabled && brain.ChannelMask == channels, "Disconnect callback cleans up local presentation");
            var expired = h.GetSnapshot(); expired.captureStartedAt -= 4; h.ApplySnapshot(expired, true);
            yield return new WaitForSecondsRealtime(.2f);
            Check(barriers == b && challenges == q, "Disconnected encounter cannot activate a later barrier");
            h.Request(KhezuCommand.ResetHunt); e.OnJoinedRoom(); yield return null;

            // Existing Phase 3 checkpoint must not re-emit an authoritative barrier creation.
            Capture(-1, 5, 4); yield return new WaitForSecondsRealtime(.15f);
            Check(!c.IsPlaying && barriers == b && challenges == q + 1, "Completed checkpoint opens local handoff without replay or duplicate host barrier");
            Check(!e.ReleaseAfterHostChallenge(999, h.CaptureSerial) && !e.ReleaseAfterHostChallenge(-1, h.CaptureSerial - 1), "Host release rejects wrong actor and stale capture serial");
            Check(e.ReleaseAfterHostChallenge(-1, h.CaptureSerial), "Matching host-owned challenge handoff can release encounter");
            yield return null;

            c.reducedMotion = true; c.PreviewLocal();
            c.EvaluatePresentation(KhezuCinematicJumpscareController.RealTime(1.38f)); var face = (c.MouthUpper.position + c.MouthLower.position) * .5f;
            var offset = c.PresentationCamera.transform.position - face;
            c.EvaluatePresentation(KhezuCinematicJumpscareController.RealTime(1.42f)); var face2 = (c.MouthUpper.position + c.MouthLower.position) * .5f;
            var offset2 = c.PresentationCamera.transform.position - face2;
            Check((offset - offset2).sqrMagnitude < .00001f && Mathf.Abs(offset.x) < .0001f, "Reduced motion removes lunge translation and camera shake");
            e.enabled = false; yield return null;
            Check(!c.IsPlaying && p.enabled && !GameInput.Instance.GameplayInputBlocked && brain.ChannelMask == channels, "Encounter component disable also cleans up explicit preview");
            e.enabled = true; c.reducedMotion = false;
            File.WriteAllText(Folder + "/CinematicLifecycle.json", JsonUtility.ToJson(report, true));
            Debug.Log("Khezu lifecycle checks: " + report.passed.Count + " passed, " + report.failed.Count + " failed.");
            Destroy(gameObject);
        }
    }
}
#endif
