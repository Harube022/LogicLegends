using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>One permission rule for legacy scene test buttons, separate from gameplay commands.</summary>
    public static class KhezuLegacyTestPermission
    {
        // Pure policy seam used by the Editor checks; the runtime property below
        // always supplies the actual compiled build flag and current session authority.
        public static bool Allows(bool developmentBuild, bool hasArchiveSession, bool archiveAdmin) =>
            developmentBuild && (!hasArchiveSession || archiveAdmin);
        public static bool CanUse
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!Application.isPlaying) return false;
                var session = LogicLegends.OpenArchive.Topics.ArchiveTopicSession.Current;
                return Allows(true, session != null, session != null && session.CanUseTopicAdmin);
#else
                return false;
#endif
            }
        }
        // UI permission is read every frame; exclusion belongs only to an
        // explicit test mutation, never to merely opening the development dock.
        public static void MarkActionUsed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var session = LogicLegends.OpenArchive.Topics.ArchiveTopicSession.Current;
            if (session != null && session.CanUseTopicAdmin) session.MarkRewardsAdminUsed();
#endif
        }
    }

    public sealed class KhezuHuntTestControls : MonoBehaviour
    {
        public KhezuHuntingController monster;
        public Text status;
        float nextUpdate;
        public void ForceFourMistakes() { RunTestCommand(KhezuCommand.ForceFourMistakes); }
        public void StartHunting() { RunTestCommand(KhezuCommand.StartHunting); }
        public void ResetCurrentHunt() { RunTestCommand(KhezuCommand.ResetHunt); }
        public void ResetFullRotation() { RunTestCommand(KhezuCommand.ResetRotation); }
        public void ReturnToSleep() { RunTestCommand(KhezuCommand.Sleep); }
        void RunTestCommand(KhezuCommand command)
        {
            if (!KhezuLegacyTestPermission.CanUse || monster == null) return;
            KhezuLegacyTestPermission.MarkActionUsed();
            monster.Request(command);
        }
        void Update()
        {
            if (Time.unscaledTime < nextUpdate || monster == null) return;
            nextUpdate = Time.unscaledTime + .15f;
            if (status != null) status.text = "HUNT: " + monster.State + "   |   Target: " + monster.TargetName + " [" + monster.TargetPlayerId + "]\n" +
                "Hunted IDs: " + string.Join(", ", monster.HuntedPlayerIds.OrderBy(id => id)) + "   |   " +
                (PhotonNetwork.InRoom ? monster.IsAuthority ? "Photon host" : "Photon client" : "Local test") +
                "   |   " + monster.Status;
        }
    }
}
