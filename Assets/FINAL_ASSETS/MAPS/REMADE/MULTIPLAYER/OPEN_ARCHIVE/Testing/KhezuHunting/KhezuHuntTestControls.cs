using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Tests.Khezu
{
    public sealed class KhezuHuntTestControls : MonoBehaviour
    {
        public KhezuHuntingController monster;
        public Text status;
        float nextUpdate;
        public void ForceFourMistakes() { monster.Request(KhezuCommand.ForceFourMistakes); }
        public void StartHunting() { monster.Request(KhezuCommand.StartHunting); }
        public void ResetCurrentHunt() { monster.Request(KhezuCommand.ResetHunt); }
        public void ResetFullRotation() { monster.Request(KhezuCommand.ResetRotation); }
        public void ReturnToSleep() { monster.Request(KhezuCommand.Sleep); }
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
