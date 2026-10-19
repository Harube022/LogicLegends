#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Editor-only checks; never attached to a saved scene or built into Android.</summary>
    public sealed class KhezuCinematicPlayChecks : MonoBehaviour
    {
        const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuCinematic/Reports";
        [Serializable] sealed class Report
        {
            public List<string> passed = new List<string>(), failed = new List<string>();
            public string missingAudio;
            public float maximumMarkerError, measuredDuration, maximumChaseSpeed, impactTranslation, impactRotation, maximumVoiceGain;
        }
        readonly Report report = new Report();
        KhezuCinematicJumpscareController c;
        KhezuHuntingController h;
        KhezuCaptureEncounterController e;
        KhezuCinematicTestControls ui;
        global::Player p;
        int barriers, challenges;
        void Check(bool value, string name) { (value ? report.passed : report.failed).Add(name); }
        public static void Begin() { new GameObject("Khezu_Cinematic_Editor_Checks").AddComponent<KhezuCinematicPlayChecks>(); }
        IEnumerator Start()
        {
            Directory.CreateDirectory(Folder);
            yield return new WaitForSecondsRealtime(1);
            c = FindFirstObjectByType<KhezuCinematicJumpscareController>(); h = c.GetComponent<KhezuHuntingController>();
            e = c.GetComponent<KhezuCaptureEncounterController>(); ui = FindFirstObjectByType<KhezuCinematicTestControls>(); p = global::Player.LocalInstance;
            e.hostBarrierActivated.AddListener((actor, serial) => barriers++);
            e.localChallengeRequested.AddListener((actor, serial) => challenges++);
            Check(p != null && p.enabled, "Existing explorer controls enabled before cinematic");
            Check(c.capture != null && c.jumpscare != null && c.barrierCast != null, "All three original animation clips assigned");
            Check(h.State == KhezuHuntState.Sleeping, "Sleep starts automatically");
            Check(Mathf.Abs(h.normalPlayerRunSpeed * h.chaseSpeedMultiplier - 23.205f * 1.15f) < .01f, "Chase tuning is fifteen percent faster than the previous 23.205 m/s");
            Check(string.IsNullOrEmpty(c.MissingAudio()), "All six designed jumpscare audio cues are assigned");
            Check(!c.BeginConfirmedCapture(999, 1, KhezuHuntingController.EncounterClock), "Foreign actor cannot start local POV");
            var camera = Camera.main; var brain = camera.GetComponent<CinemachineBrain>(); var mask = brain.ChannelMask;
            var position = p.transform.position; var monsterPosition = h.transform.position; var monsterRotation = h.transform.rotation;
            int starts = c.Starts;
            ui.previewButton.onClick.Invoke();
            Check(c.IsPlaying && c.Starts == starts + 1, "On-screen preview button starts local presentation");
            Check(c.PresentationInstance.transform.parent.GetComponents<AudioSource>().Any(x => x.clip == c.captureImpact && x.isPlaying), "Capture impact begins immediately with the quick POV");
            Check(p.enabled && p.IsMovementLocked && GameInput.Instance.GameplayInputBlocked, "Cinematic token restricts movement while retaining the player component");
            Check(camera.enabled && camera.GetComponent<AudioListener>().enabled, "Gameplay camera and original single listener retained");
            Check(c.PresentationInstance.GetComponentsInChildren<Photon.Pun.PhotonView>(true).Length == 0, "Presentation contains no network object");
            Check(c.PresentationInstance.GetComponentsInChildren<Collider>(true).All(x => !x.enabled), "Presentation has no active physics collider");
            Check((brain.ChannelMask & OutputChannels.Channel15) == 0, "Gameplay brain cannot select presentation Cinemachine camera");
            Check(!c.PresentationCamera.GetComponent<AudioListener>(), "Presentation adds no duplicate listener");
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.enabled) == 1, "Exactly one active audio listener");
            c.onRoarMarker.AddListener(() => {
                report.maximumMarkerError = Mathf.Abs(c.Elapsed - KhezuCinematicJumpscareController.LungeMarker);
                var roar = c.PresentationInstance.transform.parent.GetComponents<AudioSource>().FirstOrDefault(x => x.clip == c.monsterRoar);
                Check(roar != null && roar.isPlaying, "Designed maw roar starts at the synchronized lunge marker");
                Check(roar != null && roar.outputAudioMixerGroup != null && roar.outputAudioMixerGroup.name == "SFX", "Jumpscare audio respects the existing SFX volume and mute control");
            });
            yield return At(.75f); SaveFrame("Pinned_16x9.png");
            CheckFace("Pinned");
            CheckQuickMotion();
            CheckShake();
            yield return At(1.45f); SaveFrame("Maw_Lunge_16x9.png");
            CheckFace("Maw lunge");
            Check(Vector3.Distance(c.MouthUpper.position, c.MouthLower.position) > 1.4f, "Existing Jumpscare clip opens the circular mouth");
            Check(c.RoarMarkers == 1 && report.maximumMarkerError < .12f, "Roar marker fires once within one practical frame of lunge");
            yield return At(2.2f); SaveFrame("Impact_16x9.png");
            CheckFace("Impact");
            yield return At(3.05f); SaveFrame("Barrier_Transition_16x9.png");
            yield return new WaitUntil(() => !c.IsPlaying);
            report.measuredDuration = c.Elapsed;
            Check(report.measuredDuration >= 2 && report.measuredDuration < 2.15f, "Faster cinematic completes in approximately two seconds");
            Check(report.maximumVoiceGain > .2f && report.maximumVoiceGain <= c.masterVolume + .005f, "Combined active audio gain remains within the configured master budget");
            yield return null;
            Check(p.enabled && !p.IsMovementLocked && !GameInput.Instance.GameplayInputBlocked, "Preview releases its movement token and input at completion");
            Check(brain.ChannelMask == mask, "Original Cinemachine channel mask restored");
            Check(Vector3.Distance(p.transform.position, position) < .03f, "Preview did not teleport player");
            Check(Vector3.Distance(h.transform.position, monsterPosition) < .001f && Quaternion.Angle(h.transform.rotation, monsterRotation) < .01f, "Preview did not move or rotate world monster");
            Check(barriers == 0 && challenges == 0, "Preview does not activate a barrier or challenge");
            Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).All(x => x.name != "Khezu_Local_POV_Output"), "Presentation camera destroyed after completion");

            // Preserve an existing modal input lock, pre-disabled player, and reduced-motion option.
            GameInput.Instance.SetGameplayInputBlocked(true); p.enabled = false; c.reducedMotion = true;
            c.PreviewLocal(); yield return new WaitForSecondsRealtime(.6f);
            c.Cancel(); yield return null;
            Check(!p.enabled && GameInput.Instance.GameplayInputBlocked, "Interruption preserves pre-existing disabled player and blocked input");
            Check(brain.ChannelMask == mask && !c.IsPlaying, "Interruption restores camera channels and tears down rig");
            p.enabled = true; GameInput.Instance.SetGameplayInputBlocked(false); c.reducedMotion = false;

            // Actual host hunting/catch path, using existing player spawn and NavMesh (no teleport).
            h.Request(KhezuCommand.ForceFourMistakes);
            float deadline = Time.unscaledTime + 55;
            while (h.State != KhezuHuntState.Caught && Time.unscaledTime < deadline)
            {
                if (h.Agent.enabled && h.Agent.isOnNavMesh) report.maximumChaseSpeed = Mathf.Max(report.maximumChaseSpeed, h.Agent.velocity.magnitude);
                yield return null;
            }
            Check(report.maximumChaseSpeed > 26.2f && report.maximumChaseSpeed < 27.3f, "Real NavMesh chase reaches the increased speed without overspeed");
            Check(h.State == KhezuHuntState.Caught, "Real awakening, hunting and host capture reaches cinematic adapter");
            if (h.State == KhezuHuntState.Caught)
            {
                yield return null;
                position = p.transform.position; monsterPosition = h.transform.position; monsterRotation = h.transform.rotation;
                starts = c.Starts;
                Check(c.IsPlaying && p.enabled && p.IsMovementLocked, "Host-confirmed capture starts caught player's POV with movement protection");
                Check(!c.BeginConfirmedCapture(h.CaughtPlayerId, h.CaptureSerial, h.CaptureStartedAt), "Duplicate capture cannot overlap or replay POV");
                h.Request(KhezuCommand.StartHunting);
                Check(h.State == KhezuHuntState.Caught, "Start hunting cannot bypass active captured encounter");
                Check(!ui.handoffPanel.activeSelf, "Challenge handoff remains hidden during cinematic");
                yield return At(1.5f);
                Check(h.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Jumpscare"), "World monster performs Jumpscare for observers");
                yield return At(3.05f);
                Check(h.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Barrier_Cast"), "World monster performs Barrier_Cast");
                yield return new WaitUntil(() => !c.IsPlaying);
                yield return new WaitForSecondsRealtime(.2f);
                var outcomeUi=FindFirstObjectByType<KhezuBarrierDevelopmentUI>();
                Check(barriers == 1 && challenges == 1 && outcomeUi!=null && outcomeUi.Visible, "One host barrier notification and one connected development outcome panel after cinematic");
                Check(e.Phase == KhezuEncounterPhase.BarrierChallenge && p.enabled && p.IsMovementLocked, "Gameplay camera returns while the barrier movement token remains active");
                Check(Vector3.Distance(p.transform.position, position) < .03f && Vector3.Distance(h.transform.position, monsterPosition) < .03f && Quaternion.Angle(h.transform.rotation, monsterRotation) < .01f, "Cinematic leaves authoritative transforms stationary");
                var checkpoint = JsonUtility.FromJson<KhezuSnapshot>(JsonUtility.ToJson(h.GetSnapshot()));
                h.ApplySnapshot(checkpoint, true); yield return new WaitForSecondsRealtime(.3f);
                Check(c.Starts == starts && barriers == 1 && challenges == 1, "Checkpoint replay cannot duplicate cinema or barrier");
                ScreenCapture.CaptureScreenshot(Folder + "/Phase3_Handoff.png");
                yield return new WaitForEndOfFrame();
                ui.ResetEncounter(); yield return null;
                Check(!outcomeUi.Visible && !p.IsMovementLocked && p.enabled && !GameInput.Instance.GameplayInputBlocked, "Host reset hides the outcome panel and releases its movement token");
            }
            report.missingAudio = c.MissingAudio();
            File.WriteAllText(Folder + "/CinematicPlayMode.json", JsonUtility.ToJson(report, true));
            Debug.Log("Khezu cinematic checks: " + report.passed.Count + " passed, " + report.failed.Count + " failed.");
            Destroy(gameObject);
        }
        IEnumerator At(float timelineTime)
        {
            float time = KhezuCinematicJumpscareController.RealTime(timelineTime);
            while (c.IsPlaying && c.Elapsed < time)
            {
                float gain = 0;
                foreach (var voice in c.PresentationInstance.transform.parent.GetComponents<AudioSource>()) if (voice.isPlaying) gain += voice.volume;
                report.maximumVoiceGain = Mathf.Max(report.maximumVoiceGain, gain);
                yield return null;
            }
        }
        void CheckShake()
        {
            float intensity = c.shakeIntensity;
            float sample = KhezuCinematicJumpscareController.LungeMarker + .04f;
            c.shakeIntensity = 0; c.EvaluatePresentation(sample);
            var position = c.PresentationCamera.transform.position; var rotation = c.PresentationCamera.transform.rotation;
            c.shakeIntensity = intensity; c.EvaluatePresentation(sample);
            report.impactTranslation = Vector3.Distance(position, c.PresentationCamera.transform.position);
            report.impactRotation = Quaternion.Angle(rotation, c.PresentationCamera.transform.rotation);
            Check(report.impactTranslation > .01f && report.impactTranslation <= c.shakeTranslation * intensity + .002f, "Impact shake adds bounded visible camera translation");
            Check(report.impactRotation > .2f && report.impactRotation < 5, "Impact recoil adds visible pitch/yaw/roll while keeping the face framed");
            CheckFace("Shaken lunge");
            c.EvaluatePresentation(KhezuCinematicJumpscareController.RealTime(2.7f));
            position = c.PresentationCamera.transform.position; rotation = c.PresentationCamera.transform.rotation;
            c.shakeIntensity = 0; c.EvaluatePresentation(KhezuCinematicJumpscareController.RealTime(2.7f));
            Check(Vector3.Distance(position, c.PresentationCamera.transform.position) < .001f && Quaternion.Angle(rotation, c.PresentationCamera.transform.rotation) < .02f, "Shake settles completely before the barrier transition");
            c.shakeIntensity = intensity; c.EvaluatePresentation(c.Elapsed);
        }
        void CheckQuickMotion()
        {
            float intensity = c.shakeIntensity; c.shakeIntensity = 0;
            c.EvaluatePresentation(KhezuCinematicJumpscareController.CaptureEnd);
            var before = (c.MouthUpper.position + c.MouthLower.position) * .5f;
            float beforeCamera = c.PresentationCamera.transform.position.z;
            float beforeModel = c.PresentationInstance.transform.localPosition.z;
            SaveFrame("Fast_Windup_End.png");
            c.EvaluatePresentation(KhezuCinematicJumpscareController.LungeMarker);
            var after = (c.MouthUpper.position + c.MouthLower.position) * .5f;
            Check(after.z - before.z > .6f && KhezuCinematicJumpscareController.LungeMarker - KhezuCinematicJumpscareController.CaptureEnd <= .11f,
                "Monster face thrusts forward over 0.6m within a tenth of a second");
            Check(c.PresentationInstance.transform.localPosition.z - beforeModel > .4f,
                "Visual monster itself lunges forward rather than relying on camera zoom");
            Check(Mathf.Abs(c.PresentationCamera.transform.position.z - beforeCamera) < .005f,
                "POV camera does not track forward and cancel the monster's lunge");
            Check(Vector3.Distance(c.MouthUpper.position, c.MouthLower.position) > 1.4f && KhezuCinematicJumpscareController.LungeMarker <= .18f,
                "Wide-mouth attack lands within the first 0.18 seconds");
            CheckFace("Quick attack peak"); SaveFrame("Fast_Lunge_Peak.png");
            c.shakeIntensity = intensity; c.EvaluatePresentation(c.Elapsed);
        }
        void CheckFace(string name)
        {
            var cam = c.PresentationCamera;
            var top = cam.WorldToViewportPoint(c.MouthUpper.position); var bottom = cam.WorldToViewportPoint(c.MouthLower.position);
            Check(top.z > .1f && bottom.z > .1f && top.y < .96f && bottom.y > .04f && Mathf.Abs(top.x - .5f) < .25f, name + " mouth fits frame and stays in front of near plane");
        }
        void SaveFrame(string name)
        {
            var cam = c.PresentationCamera; cam.Render(); var previous = RenderTexture.active;
            RenderTexture.active = cam.targetTexture;
            var image = new Texture2D(cam.targetTexture.width, cam.targetTexture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply();
            File.WriteAllBytes(Folder + "/" + name, image.EncodeToPNG());
            RenderTexture.active = previous; Destroy(image);
        }
    }
}
#endif
