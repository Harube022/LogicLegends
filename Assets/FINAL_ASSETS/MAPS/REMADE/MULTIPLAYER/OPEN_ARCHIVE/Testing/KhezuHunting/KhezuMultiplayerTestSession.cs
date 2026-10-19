using System.Collections;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Uses the existing PlayerSpawner and exploration UI. Never spawns a monster over the network.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class KhezuMultiplayerTestSession : MonoBehaviour
    {
        public OpenArchiveExplorationSession exploration;
        public GameObject offlineExplorer;
        public PlayerSpawner playerSpawner;
        public Transform spawn;
        public CinemachineCamera followCamera;
        void Awake()
        {
            bool online = PhotonNetwork.InRoom;
            playerSpawner.enabled = online;
            if (online) { exploration.enabled = false; offlineExplorer.SetActive(false); }
        }
        IEnumerator Start()
        {
            if (!PhotonNetwork.InRoom) yield break;
            while (PhotonNetwork.InRoom && global::Player.LocalInstance == null) yield return null;
            if (global::Player.LocalInstance == null) yield break;
            exploration.Configure(global::Player.LocalInstance, spawn, followCamera);
            exploration.enabled = true;
        }
    }
}
