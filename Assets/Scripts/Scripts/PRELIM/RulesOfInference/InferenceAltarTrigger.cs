using UnityEngine;
using Photon.Pun;

namespace LogicLegends.Inference
{
    [RequireComponent(typeof(BoxCollider))]
    public class InferenceAltarTrigger : MonoBehaviour
    {
        public InferenceChallenge challenge;
        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<Player>();
            if (player == null) return;
            var view = player.GetComponent<PhotonView>();
            if (PhotonNetwork.InRoom && view != null && !view.IsMine) return;
            challenge.OpenBoard(player);
        }
    }
}
