using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Tests.Khezu
{
    public sealed class KhezuCinematicTestControls : MonoBehaviour
    {
        public KhezuCinematicJumpscareController cinematic;
        public KhezuCaptureEncounterController encounter;
        public KhezuHuntingController hunting;
        public GameObject handoffPanel;
        public Text status;
        public Button previewButton;
        public Toggle reducedMotion;
        public void Preview() { cinematic.PreviewLocal(); }
        public void SetReducedMotion(bool value) { cinematic.reducedMotion = value; }
        public void ShowHandoff(int actor, int serial) { handoffPanel.SetActive(true); }
        public void HideHandoff() { handoffPanel.SetActive(false); }
        public void ResetEncounter()
        {
            // Test control only. This is not a challenge success/failure or damage decision.
            hunting.Request(KhezuCommand.ResetHunt); HideHandoff();
        }
        void Update()
        {
            if (status != null) status.text = cinematic.IsPlaying ? "POV  " + cinematic.Elapsed.ToString("0.0") + " / " + KhezuCinematicJumpscareController.Duration.ToString("0.0") + " s" :
                "CINEMATIC TEST  |  Offline preview / capture POV";
            if (previewButton != null) previewButton.interactable = !PhotonNetwork.InRoom && !cinematic.IsPlaying;
        }
    }
}
