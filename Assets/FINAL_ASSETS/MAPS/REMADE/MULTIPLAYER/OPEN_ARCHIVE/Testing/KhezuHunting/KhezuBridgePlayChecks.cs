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
    /// <summary>Explicit offline traversal checks. Never attached to a saved scene or included in a build.</summary>
    public sealed class KhezuBridgePlayChecks : MonoBehaviour
    {
        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Report {
            public string checkedUtc, scope = "Offline Editor Play Mode: original CharacterController and original host hunting controller. Only the test player is repositioned; Khezu travels normally from the nest. All four bridges tested.";
            public List<Check> checks = new List<Check>();
        }
        readonly Report report = new Report();
        global::Player player;
        CharacterController capsule;
        KhezuHuntingController hunting;
        Transform root;
        public static void Begin() {
            if (!Application.isPlaying || PhotonNetwork.InRoom) throw new InvalidOperationException("Use offline Play Mode.");
            new GameObject("Khezu_Editor_Bridge_Checks").AddComponent<KhezuBridgePlayChecks>();
        }
        void Record(string name, bool pass, string detail = "") { report.checks.Add(new Check { name = name, passed = pass, detail = detail }); }
        static Vector3 Polar(float angle, float radius) { angle *= Mathf.Deg2Rad; return new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius); }
        void PlacePlayer(Vector3 local) {
            var point = root.TransformPoint(local); RaycastHit floor;
            if (!Physics.Raycast(point + Vector3.up * 30, Vector3.down, out floor, 60, ~(1 << 7), QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Missing bridge test floor.");
            point.y = floor.point.y - (capsule.center.y - capsule.height * .5f) * player.transform.lossyScale.y + capsule.skinWidth;
            capsule.enabled = false; player.transform.position = point; capsule.enabled = true; player.ResetMotionAfterTeleport();
        }
        IEnumerator Start() {
            yield return new WaitForSecondsRealtime(.5f);
            report.checkedUtc = DateTime.UtcNow.ToString("o");
            root = GameObject.Find("OpenArchive").transform;
            hunting = FindFirstObjectByType<KhezuHuntingController>();
            player = global::Player.LocalInstance; capsule = player.GetComponent<CharacterController>();
            var session = FindFirstObjectByType<OpenArchiveExplorationSession>();
            var cinema = hunting.GetComponent<KhezuCinematicJumpscareController>();
            var encounter = hunting.GetComponent<KhezuCaptureEncounterController>();
            bool originalPlayerEnabled = player.enabled, originalSession = session.enabled;
            bool originalCinema = cinema.enabled, originalEncounter = encounter.enabled;
            bool blocked = GameInput.Instance.GameplayInputBlocked;
            var position = player.transform.position; var rotation = player.transform.rotation;
            var monsterScale = hunting.transform.lossyScale;
            try {
                player.enabled = false; session.enabled = false; cinema.enabled = false; encounter.enabled = false;
                GameInput.Instance.SetGameplayInputBlocked(true);
                hunting.Request(KhezuCommand.Sleep);
                for (int i = 0; i < 4; i++) {
                    float a = 45 + i * 90;
                    for (int direction = -1; direction <= 1; direction += 2) {
                        var loop = new List<Vector3>();
                        for (int s = 0; s <= 20; s++) loop.Add(Polar(a + direction * (-20 + s * 2), 24.4f));
                        yield return Walk("Player bridge " + i + " inner loop " + direction, loop);
                        yield return Walk("Player bridge " + i + " deck " + direction, new List<Vector3> {
                            Polar(a, direction == 1 ? 24.4f : 54), Polar(a, direction == 1 ? 54 : 24.4f) });
                    }
                }
                PlacePlayer(Polar(25, 24.4f));
                hunting.Request(KhezuCommand.ForceFourMistakes);
                float deadline = Time.time + 25;
                while (hunting.State != KhezuHuntState.Hunting && Time.time < deadline) yield return null;
                Record("Awakens normally before traversal", hunting.State == KhezuHuntState.Hunting);
                for (int i = 0; i < 4; i++) {
                    float a = 45 + i * 90;
                    yield return Hunt("Khezu bridge " + i + " approach", Polar(a - 20, 24.4f), i, 0);
                    yield return Hunt("Khezu bridge " + i + " inner loop", Polar(a + 20, 24.4f), i, 1);
                    yield return Hunt("Khezu bridge " + i + " outward crossing", Polar(a, 54), i, 2);
                    yield return Hunt("Khezu bridge " + i + " inward crossing and turn", Polar(a - 20, 24.4f), i, 2);
                }
                Record("Existing Khezu size and navigation radius retained", hunting.transform.lossyScale == monsterScale && Mathf.Approximately(hunting.Agent.radius, 9.7f));
            }
            finally {
                hunting.Request(KhezuCommand.Sleep);
                capsule.enabled = false; player.transform.SetPositionAndRotation(position, rotation); capsule.enabled = true;
                player.ResetMotionAfterTeleport(); player.enabled = originalPlayerEnabled; session.enabled = originalSession;
                GameInput.Instance.SetGameplayInputBlocked(blocked); cinema.enabled = originalCinema; encounter.enabled = originalEncounter;
                const string folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuHunting/Reports";
                Directory.CreateDirectory(folder); File.WriteAllText(folder + "/BridgePlayMode.json", JsonUtility.ToJson(report, true));
                int passed = report.checks.FindAll(c => c.passed).Count;
                Debug.Log("Khezu bridge Play Mode checks: " + passed + "/" + report.checks.Count);
                Destroy(gameObject);
            }
        }
        IEnumerator Walk(string name, List<Vector3> points) {
            PlacePlayer(points[0]); float deadline = Time.time + 10;
            bool complete = true;
            for (int i = 1; i < points.Count; i++) {
                var target = root.TransformPoint(points[i]);
                while (true) {
                    var delta = target - player.transform.position; delta.y = 0;
                    if (delta.magnitude < .2f) break;
                    if (Time.time >= deadline) { complete = false; break; }
                    capsule.Move(Vector3.ClampMagnitude(delta, hunting.normalPlayerRunSpeed * Time.deltaTime) + Vector3.down * 5 * Time.deltaTime);
                    yield return null;
                }
                if (!complete) break;
            }
            Record(name, complete, "Actual CharacterController, " + hunting.normalPlayerRunSpeed + "m/s; final position " + root.InverseTransformPoint(player.transform.position));
        }
        IEnumerator Hunt(string name, Vector3 target, int bridgeIndex, int checkKind) {
            hunting.Request(KhezuCommand.ResetHunt); PlacePlayer(target);
            int serial = hunting.CaptureSerial;
            hunting.Request(KhezuCommand.StartHunting);
            float deadline = Time.time + 22, minRadius = float.MaxValue, maxSpeed = 0;
            bool blocked = false, crossedDeck = false, railCollision = false;
            var bridge = root.Find("Environment/Bridges").GetChild(bridgeIndex);
            var rails = bridge.GetComponents<BoxCollider>(); var body = hunting.GetComponent<BoxCollider>();
            var previous = hunting.transform.position;
            while (hunting.State != KhezuHuntState.Caught && Time.time < deadline) {
                var p = root.InverseTransformPoint(hunting.transform.position);
                minRadius = Mathf.Min(minRadius, new Vector2(p.x, p.z).magnitude);
                var local = bridge.InverseTransformPoint(hunting.transform.position);
                crossedDeck |= Mathf.Abs(local.x) < 2.5f && Mathf.Abs(local.z) < 1;
                blocked |= hunting.State == KhezuHuntState.NavigationBlocked;
                if (Time.deltaTime > 0) maxSpeed = Mathf.Max(maxSpeed, Vector3.Distance(previous, hunting.transform.position) / Time.deltaTime);
                previous = hunting.transform.position;
                for (int i = 1; i < rails.Length; i++) {
                    Vector3 direction; float depth;
                    if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                        rails[i], rails[i].transform.position, rails[i].transform.rotation, out direction, out depth) && depth > .05f) railCollision = true;
                }
                yield return null;
            }
            bool pass = hunting.State == KhezuHuntState.Caught && hunting.CaptureSerial == serial + 1 && !blocked && !railCollision && maxSpeed < 55;
            if (checkKind == 1) pass &= minRadius >= 20.5f;
            if (checkKind == 2) pass &= crossedDeck;
            Record(name, pass, "caught=" + (hunting.CaptureSerial == serial + 1) + ", blocked=" + blocked + ", rail penetration=" + railCollision +
                ", crossed deck=" + crossedDeck + ", min source radius=" + minRadius.ToString("F2") + ", max observed speed=" + maxSpeed.ToString("F2"));
        }
    }
}
#endif
