#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Explicitly invoked Editor smoke test. Never attached to a saved scene or a player build.</summary>
    public sealed class KhezuPhotonOfflineChecks : MonoBehaviour
    {
        [Serializable] class Check { public string name, detail; public bool passed; }
        [Serializable] class Report
        {
            public bool passed;
            public string checkedUtc;
            public List<Check> checks = new List<Check>();
            public string limits = "One Photon OfflineMode room and one actor. This verifies PUN spawning, actor targeting and room checkpoint storage locally. It does not test live network transport, remote interpolation, real multi-client targets, late joining or Master migration.";
        }
        Report report = new Report { checkedUtc = DateTime.UtcNow.ToString("o") };
        void Record(string name, bool passed, string detail = "") { report.checks.Add(new Check { name = name, passed = passed, detail = detail }); }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            if (PhotonNetwork.InRoom || PhotonNetwork.IsConnected) throw new InvalidOperationException("Run only from a disconnected local test session.");
            string scene = SceneManager.GetActiveScene().name;
            bool complete = false;
            try
            {
                PhotonNetwork.OfflineMode = true;
                bool created = PhotonNetwork.CreateRoom("Khezu_Phase2_LocalSmoke");
                float end = Time.realtimeSinceStartup + 5;
                while (!PhotonNetwork.InRoom && Time.realtimeSinceStartup < end) yield return null;
                Record("Photon offline room created without external connection", created && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient);
                yield return SceneManager.LoadSceneAsync(scene);
                end = Time.realtimeSinceStartup + 12;
                while ((global::Player.LocalInstance == null || !FindFirstObjectByType<OpenArchiveExplorationSession>().enabled) && Time.realtimeSinceStartup < end) yield return null;
                yield return new WaitForSeconds(.5f);
                var monster = FindFirstObjectByType<KhezuHuntingController>();
                var session = FindFirstObjectByType<OpenArchiveExplorationSession>();
                var player = global::Player.LocalInstance;
                var view = player == null ? null : player.GetComponent<PhotonView>();
                int id = PhotonNetwork.LocalPlayer.ActorNumber;
                Record("Existing PlayerSpawner creates one owned network avatar", player != null && view != null && view.ViewID != 0 && view.OwnerActorNr == id && FindObjectsByType<global::Player>(FindObjectsSortMode.None).Length == 1);
                Record("Offline character disabled, existing controls rebound", session.enabled && session.Explorer == player && GameInput.Instance != null && Camera.main != null);
                Record("Only one scene Khezu", FindObjectsByType<KhezuHuntingController>(FindObjectsSortMode.None).Length == 1);
                monster.Request(KhezuCommand.ForceFourMistakes);
                end = Time.time + 18;
                while (monster.HuntSerial == 0 && Time.time < end) yield return null;
                Record("Host selects Photon ActorNumber after awakening", monster.IsAuthority && monster.TargetPlayerId == id && monster.HuntSerial == 1 && monster.HuntedPlayerIds.Contains(id), "Actor " + id);
                end = Time.time + 30;
                while (monster.State != KhezuHuntState.Caught && Time.time < end) yield return null;
                Record("Host navigation and catch use network avatar", monster.State == KhezuHuntState.Caught && monster.CaughtPlayerId == id && monster.CaptureSerial == 1);
                yield return new WaitForSecondsRealtime(2.2f);
                object value;
                var packet = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KhezuPhotonSync.SnapshotKey, out value) ? value as object[] : null;
                KhezuSnapshot saved = packet != null && packet.Length == 4 ? JsonUtility.FromJson<KhezuSnapshot>((string)packet[3]) : null;
                Record("PUN room checkpoint stores state, actor and rotation", saved != null && saved.state == (int)KhezuHuntState.Caught && saved.target == id && saved.hunted.Contains(id) && saved.captureSerial == 1);

                monster.Request(KhezuCommand.ResetHunt);
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["ll.eliminated"] = true });
                yield return new WaitForSeconds(.6f);
                monster.Request(KhezuCommand.StartHunting);
                yield return new WaitForSeconds(.2f);
                Record("Eliminated Photon actor excluded", monster.State == KhezuHuntState.WaitingForPlayers && monster.TargetPlayerId == 0);
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["ll.eliminated"] = false, ["ll.khezu.available"] = false });
                yield return new WaitForSeconds(.7f);
                Record("Unavailable Photon actor excluded without clearing history", monster.State == KhezuHuntState.WaitingForPlayers && monster.HuntedPlayerIds.Contains(id));
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["ll.khezu.available"] = true });
                yield return new WaitForSeconds(.8f);
                Record("Returning eligible actor resumes a waiting hunt", monster.TargetPlayerId == id && monster.HuntSerial >= 2);
                monster.Request(KhezuCommand.Sleep);
                yield return new WaitForSeconds(.4f);
                Record("Sleep retains Photon actor rotation", monster.State == KhezuHuntState.Sleeping && monster.HuntedPlayerIds.Contains(id));
                complete = true;
            }
            finally
            {
                Record("Offline smoke test completed", complete);
                report.passed = report.checks.All(c => c.passed);
                var folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuHunting/Reports";
                Directory.CreateDirectory(folder);
                File.WriteAllText(folder + "/PhotonOfflineSmoke.json", JsonUtility.ToJson(report, true));
                Debug.Log("Khezu Photon offline checks: " + report.checks.Count(c => c.passed) + "/" + report.checks.Count);
                StartCoroutine(Cleanup(scene));
            }
        }
        IEnumerator Cleanup(string scene)
        {
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
            float end = Time.realtimeSinceStartup + 5;
            while (PhotonNetwork.InRoom && Time.realtimeSinceStartup < end) yield return null;
            if (!PhotonNetwork.InRoom) PhotonNetwork.OfflineMode = false;
            yield return SceneManager.LoadSceneAsync(scene);
            Destroy(gameObject);
        }
    }
}
#endif
