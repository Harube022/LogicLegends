#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Water;
using LogicLegends.OpenArchiveTesting;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Explicit offline regression suite. Never attached to a saved scene or production prefab.</summary>
    public sealed class MonsterCloseCapturePlayChecks : MonoBehaviour
    {
        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Report
        {
            public string checkedUtc;
            public string scope = "Unity Editor offline: both existing rigs, real baked navigation, supported/airborne/occluded reach, native capture and barrier handoff. Remote clients and Android device unverified.";
            public bool passed;
            public List<Check> checks = new List<Check>();
        }
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string ReportPath = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuHunting/Reports/MonsterCloseCapturePlayMode.json";
        readonly Report report = new Report();
        KhezuHuntingController hunt;
        KhezuCaptureEncounterController encounter;
        Player player;
        CharacterController capsule;

        public static void Begin()
        {
            if (!Application.isPlaying || PhotonNetwork.InRoom) throw new InvalidOperationException("Use offline Play Mode.");
            if (FindFirstObjectByType<MonsterCloseCapturePlayChecks>() != null) throw new InvalidOperationException("Checks already running.");
            new GameObject("Monster_Close_Capture_Checks").AddComponent<MonsterCloseCapturePlayChecks>();
        }
        void Record(string name, bool passed, string detail = "")
        {
            report.checks.Add(new Check { name = name, passed = passed, detail = detail });
        }
        object Target(Transform transform, CharacterController controller)
        {
            var type = typeof(KhezuHuntingController).GetNestedType("Target", BindingFlags.NonPublic);
            var target = Activator.CreateInstance(type);
            type.GetField("transform").SetValue(target, transform);
            type.GetField("capsule").SetValue(target, controller);
            return target;
        }
        object Call(string method, Transform transform, CharacterController controller)
        {
            Physics.SyncTransforms();
            return typeof(KhezuHuntingController).GetMethod(method, Private).Invoke(hunt, new[] { Target(transform, controller) });
        }
        void ResetHunt()
        {
            hunt.Request(KhezuCommand.ResetHunt);
            encounter.SynchronizeBarrierMovement();
        }
        Vector3 Floor(Vector3 point)
        {
            RaycastHit hit;
            if (!Physics.Raycast(point + Vector3.up * 15, Vector3.down, out hit, 35, hunt.catchObstructionMask, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Missing fixture ground at " + point);
            return hit.point;
        }
        void Place(Vector3 feet)
        {
            float offset = (capsule.center.y - capsule.height * .5f) * player.transform.lossyScale.y;
            capsule.enabled = false;
            player.transform.position = feet + Vector3.up * (-offset + capsule.skinWidth * Mathf.Abs(player.transform.lossyScale.y));
            capsule.enabled = true;
            player.ResetMotionAfterTeleport();
            Physics.SyncTransforms();
        }
        void AtNest()
        {
            var query = new NavMeshQueryFilter { agentTypeID = hunt.Agent.agentTypeID, areaMask = hunt.Agent.areaMask };
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(Vector3.zero, out hit, 10, query)) throw new InvalidOperationException("Missing nest navigation.");
            hunt.Agent.enabled = true;
            if (!hunt.Agent.Warp(hit.position)) throw new InvalidOperationException("Could not place fixture on existing navigation.");
            hunt.Agent.isStopped = true;
            hunt.transform.rotation = Quaternion.identity;
        }
        List<Vector3> CapturePoints()
        {
            var points = new List<Vector3> { new Vector3(25, 0, 30), new Vector3(30, 0, 25), new Vector3(55, 0, 95), new Vector3(60, 0, 40), new Vector3(13.875f, 4.7f, -33), new Vector3(0, 0, -70),
                // Previously missed beside actual pine trunks, including a dense pair.
                new Vector3(94.19f, 0, 57.41f), new Vector3(90.58f, 0, 59.85f), new Vector3(97.85f, 0, -36.33f), new Vector3(-44.57f, 0, 104.38f) };
            BoxCollider nearestPine = null;
            foreach (var box in GameObject.Find("OpenArchive").transform.Find("Environment/Trees").GetComponentsInChildren<BoxCollider>())
                if (box.name.StartsWith("Tree_02") && (nearestPine == null || box.transform.position.sqrMagnitude < nearestPine.transform.position.sqrMagnitude)) nearestPine = box;
            if (nearestPine == null) throw new InvalidOperationException("Missing existing pine fixture.");
            foreach (var direction in new[] { Vector3.left, Vector3.right })
            {
                var probe = nearestPine.bounds.center + direction * 50; probe.y = 2;
                Vector3 point = nearestPine.ClosestPoint(probe) + direction * (capsule.radius + .16f); point.y = 0;
                points.Add(point);
            }
            return points;
        }
        void Fixtures(string monster)
        {
            ResetHunt(); AtNest();
            var animator = hunt.GetComponent<Animator>();
            animator.Play("Base Layer.Idle", 0, 0); animator.Update(0);
            hunt.RefreshAnimationRig();
            var dummy = new GameObject("Temporary_Close_Capture_Capsule"); dummy.layer = 7;
            var cc = dummy.AddComponent<CharacterController>(); cc.height = capsule.height; cc.center = capsule.center; cc.radius = capsule.radius;
            var support = GameObject.CreatePrimitive(PrimitiveType.Cube); support.name = "Temporary_Capture_Support";
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Temporary_Capture_Wall"; wall.SetActive(false);
            try
            {
                var body = hunt.GetComponent<BoxCollider>();
                Vector3 half = Vector3.Scale(body.size, hunt.transform.lossyScale) * .5f;
                Vector3 center = Vector3.Scale(body.center, hunt.transform.lossyScale);
                float x = Mathf.Abs(center.x) + half.x, z = Mathf.Abs(center.z) + half.z;
                float sweep = Mathf.Min(hunt.Agent.radius, Mathf.Sqrt(x * x + z * z));
                float offset = cc.center.y - cc.height * .5f;
                Vector3 feet = hunt.transform.position + Vector3.forward * (sweep + cc.radius + hunt.catchSurfaceDistance * .5f) + Vector3.up * .05f;
                dummy.transform.position = feet - Vector3.up * offset;
                support.transform.position = feet - Vector3.up * .2f; support.transform.localScale = new Vector3(2.5f, .4f, 2.5f);
                Record(monster + ": supported close runner outside rectangular body can be caught",
                    (float)Call("TargetSurfaceGap", dummy.transform, cc) > hunt.catchSurfaceDistance && (bool)Call("CanCatch", dummy.transform, cc));
                dummy.transform.position += Vector3.forward * 1.4f; support.transform.position += Vector3.forward * 1.4f;
                Record(monster + ": open navigation does not gain the scenery-only arm extension", !(bool)Call("CanCatch", dummy.transform, cc));
                dummy.transform.position -= Vector3.forward * 1.4f; support.transform.position -= Vector3.forward * 1.4f;
                float closeDistance = (body.center.z + body.size.z * .5f) * hunt.transform.lossyScale.z + cc.radius + hunt.catchSurfaceDistance * .5f;
                Vector3 closeFeet = hunt.transform.position + Vector3.forward * closeDistance + Vector3.up * .05f;
                dummy.transform.position = closeFeet - Vector3.up * offset;
                support.transform.position = closeFeet - Vector3.up * .2f;
                wall.transform.position = hunt.transform.position + Vector3.forward * (closeDistance * .5f) + Vector3.up * 1.2f;
                wall.transform.localScale = new Vector3(50, 2.4f, .6f); wall.SetActive(true);
                Vector3 lowFrom = hunt.transform.position, lowTo = dummy.transform.TransformPoint(cc.center);
                lowFrom.y = lowTo.y = closeFeet.y + 1.5f; RaycastHit obstruction;
                Physics.SyncTransforms();
                Record(monster + ": low rock blocks the low ray but permits visible upper-body capture",
                    Physics.Linecast(lowFrom, lowTo, out obstruction, hunt.catchObstructionMask, QueryTriggerInteraction.Ignore) && obstruction.collider.gameObject == wall &&
                    (float)Call("TargetSurfaceGap", dummy.transform, cc) <= hunt.catchSurfaceDistance && (bool)Call("CanCatch", dummy.transform, cc));
                wall.transform.position += Vector3.up * 9; wall.transform.localScale = new Vector3(50, 35, .6f);
                Record(monster + ": solid wall still blocks a target inside normal body tolerance", !(bool)Call("CanCatch", dummy.transform, cc));
                wall.SetActive(false);
                dummy.transform.position = feet - Vector3.up * offset; support.transform.position = feet - Vector3.up * .2f;
                dummy.transform.position += Vector3.forward * 4; support.transform.position += Vector3.forward * 4;
                Record(monster + ": distant supported runner stays outside capture reach", !(bool)Call("CanCatch", dummy.transform, cc));
                dummy.transform.position -= Vector3.forward * 4; support.transform.position -= Vector3.forward * 4;
                hunt.transform.rotation = Quaternion.Euler(0, 180, 0);
                Record(monster + ": extended reach requires turning toward the runner", !(bool)Call("CanCatch", dummy.transform, cc));
                hunt.transform.rotation = Quaternion.identity;
                support.SetActive(false);
                bool foundAirborneGap = false, airborneRejected = false;
                // Each rig has a different box aspect ratio. Choose a point beyond
                // its existing ledge allowance, so this checks only the new fallback.
                foreach (float angle in new[] { 0f, 60f, 70f, 75f })
                {
                    dummy.transform.position = hunt.transform.position + Quaternion.Euler(0, angle, 0) * Vector3.forward * (sweep + cc.radius + hunt.catchSurfaceDistance * .5f);
                    dummy.transform.position += Vector3.up * (.05f - offset + 1.2f);
                    float originalLimit = hunt.catchSurfaceDistance + hunt.elevatedReachDistance * Mathf.Abs(hunt.transform.lossyScale.x);
                    if ((float)Call("TargetSurfaceGap", dummy.transform, cc) <= originalLimit + .05f) continue;
                    foundAirborneGap = true; airborneRejected = !(bool)Call("CanCatch", dummy.transform, cc); break;
                }
                Record(monster + ": unsupported jump does not gain the supported turning footprint", foundAirborneGap && airborneRejected);
                dummy.transform.position = feet - Vector3.up * offset; support.SetActive(true);
                wall.transform.position = hunt.transform.position + Vector3.forward * (sweep * .55f) + Vector3.up * 9;
                wall.transform.localScale = new Vector3(50, 35, .6f); wall.SetActive(true);
                Record(monster + ": solid wall blocks the supported capture fallback", !(bool)Call("CanCatch", dummy.transform, cc));
                wall.SetActive(false);
                Record(monster + ": removing the wall restores close capture", (bool)Call("CanCatch", dummy.transform, cc));
                float oldY = dummy.transform.position.y;
                dummy.transform.position += Vector3.up * 4; support.transform.position += Vector3.up * 4;
                Record(monster + ": supported low ledge remains reachable", (bool)Call("CanCatch", dummy.transform, cc));
                float highFeet = body.bounds.max.y + 2;
                dummy.transform.position += Vector3.up * (highFeet - (dummy.transform.position.y + offset));
                Record(monster + ": above-body platform remains outside reach", !(bool)Call("CanCatch", dummy.transform, cc));
                dummy.transform.position = new Vector3(dummy.transform.position.x, oldY, dummy.transform.position.z);
                // The slab separates every upper-body origin from an otherwise reachable elevated capsule.
                float platformFeet = body.bounds.max.y - .1f;
                dummy.transform.position += Vector3.up * (platformFeet - (dummy.transform.position.y + offset));
                wall.transform.position = new Vector3(feet.x, platformFeet - .3f, feet.z);
                wall.transform.localScale = new Vector3(60, .5f, 60); wall.SetActive(true);
                Record(monster + ": solid overhead slab blocks reach from below", !(bool)Call("CanCatch", dummy.transform, cc));
            }
            finally
            {
                support.SetActive(false); wall.SetActive(false); dummy.SetActive(false);
                Destroy(support); Destroy(wall); Destroy(dummy);
                hunt.Agent.enabled = false;
            }
        }
        IEnumerator Start()
        {
            yield return null;
            hunt = FindFirstObjectByType<KhezuHuntingController>();
            encounter = hunt.GetComponent<KhezuCaptureEncounterController>();
            player = Player.LocalInstance; capsule = player.GetComponent<CharacterController>();
            var selection = FindFirstObjectByType<DevelopmentMonsterSelection>();
            var session = FindFirstObjectByType<OpenArchiveExplorationSession>();
            var cinematic = hunt.GetComponent<KhezuCinematicJumpscareController>();
            var sinking = FindFirstObjectByType<OpenArchiveWaterSinking>();
            bool oldSession = session.enabled, oldEncounter = encounter.enabled, oldCinema = cinematic.enabled, oldSinking = sinking.enabled;
            bool oldInput = GameInput.Instance.GameplayInputBlocked;
            Vector3 oldPosition = player.transform.position; Quaternion oldRotation = player.transform.rotation;
            var oldMonster = selection.Selected; int hearts = hunt.health.CurrentHealth; bool completed = false;
            report.checkedUtc = DateTime.UtcNow.ToString("o");
            try
            {
                session.enabled = cinematic.enabled = encounter.enabled = sinking.enabled = false;
                GameInput.Instance.SetGameplayInputBlocked(false); GameInput.Instance.SetMobileMovement(Vector2.zero);
                foreach (var monster in new[] { DevelopmentMonster.Rajang, DevelopmentMonster.Khezu })
                {
                    hunt.Request(KhezuCommand.Sleep); encounter.SynchronizeBarrierMovement();
                    selection.Select(monster); yield return null;
                    Fixtures(monster.ToString()); yield return null;
                    Place(Floor(new Vector3(0, 0, -140))); AtNest(); hunt.Agent.enabled = false;
                    hunt.Request(KhezuCommand.StartHunting);
                    float deadline = Time.time + 30;
                    while (hunt.State != KhezuHuntState.Hunting && Time.time < deadline) yield return null;
                    Record(monster + ": original awakening starts native hunt", hunt.State == KhezuHuntState.Hunting);
                    ResetHunt(); Place(Floor(new Vector3(25, 0, 30)));
                    var query = new NavMeshQueryFilter { agentTypeID = hunt.Agent.agentTypeID, areaMask = hunt.Agent.areaMask };
                    NavMeshHit stoppedPose;
                    if (!NavMesh.SamplePosition(player.transform.position, out stoppedPose, hunt.Agent.radius + 2, query)) throw new InvalidOperationException("Missing stopped reach fixture.");
                    hunt.Request(KhezuCommand.StartHunting); hunt.Agent.enabled = true; hunt.Agent.Warp(stoppedPose.position); hunt.Agent.isStopped = true; hunt.Agent.ResetPath();
                    Vector3 away = stoppedPose.position - player.transform.position; away.y = 0;
                    hunt.transform.rotation = Quaternion.LookRotation(away); Quaternion initialTurn = hunt.transform.rotation;
                    typeof(KhezuHuntingController).GetField("nextDestination", Private).SetValue(hunt, Time.time + 5);
                    int stoppedSerial = hunt.CaptureSerial; deadline = Time.time + 3;
                    while (hunt.State != KhezuHuntState.Caught && Time.time < deadline) yield return null;
                    Record(monster + ": stopped agent turns and captures without teleporting",
                        hunt.State == KhezuHuntState.Caught && hunt.CaptureSerial == stoppedSerial + 1 &&
                        Quaternion.Angle(initialTurn, hunt.transform.rotation) > 1 && Vector3.Distance(stoppedPose.position, hunt.transform.position) < .15f);
                    ResetHunt();
                    foreach (var point in CapturePoints())
                    {
                        ResetHunt(); AtNest(); hunt.Agent.enabled = false;
                        Place(Floor(point)); yield return new WaitForSeconds(.2f);
                        Vector3 playerStart = player.transform.position, previous = hunt.transform.position;
                        int serial = hunt.CaptureSerial; bool continuous = true;
                        hunt.Request(KhezuCommand.StartHunting); deadline = Time.time + 14;
                        while (hunt.State != KhezuHuntState.Caught && Time.time < deadline)
                        {
                            continuous &= Vector3.Distance(previous, hunt.transform.position) <= hunt.normalPlayerRunSpeed * hunt.chaseSpeedMultiplier * Mathf.Max(.001f, Time.deltaTime) + 2;
                            previous = hunt.transform.position; yield return null;
                        }
                        string detail = "target=" + playerStart + ", monster=" + hunt.transform.position + ", state=" + hunt.State + ", bodyGap=" + Call("TargetSurfaceGap", player.transform, capsule);
                        Record(monster + ": native capture at " + point, hunt.State == KhezuHuntState.Caught && hunt.CaptureSerial == serial + 1 && hunt.CaughtPlayerId == -1 && Vector3.Distance(playerStart, player.transform.position) < .4f, detail);
                        Record(monster + ": continuous pursuit and movement lock at " + point, continuous && hunt.State == KhezuHuntState.Caught && player.IsMovementLocked, detail);
                        ResetHunt();
                    }
                }
                hunt.Request(KhezuCommand.Sleep); encounter.SynchronizeBarrierMovement(); selection.SelectRajang(); yield return null;
                Place(Floor(new Vector3(25, 0, 30))); AtNest(); hunt.Agent.enabled = false;
                encounter.enabled = true; cinematic.enabled = true;
                hunt.Request(KhezuCommand.StartHunting);
                float handoffDeadline = Time.time + 35;
                while ((encounter.Phase != KhezuEncounterPhase.BarrierChallenge || cinematic.IsPlaying) && Time.time < handoffDeadline) yield return null;
                Record("Native close capture still completes cinematic and barrier handoff", encounter.Phase == KhezuEncounterPhase.BarrierChallenge && !cinematic.IsPlaying && player.IsMovementLocked);
                ResetHunt(); yield return null;
                Record("Capture checks do not deduct hearts or disable player controls", hunt.health.CurrentHealth == hearts && player.enabled && capsule.enabled && !player.IsMovementLocked,
                    "hearts=" + hunt.health.CurrentHealth + ", playerEnabled=" + player.enabled + ", capsuleEnabled=" + capsule.enabled + ", movementLocked=" + player.IsMovementLocked);
                Record("Pursuit speeds and existing surface distance are unchanged", Mathf.Approximately(hunt.catchSurfaceDistance, .65f) && Mathf.Approximately(hunt.chaseSpeedMultiplier, 1.3685f) && Mathf.Approximately(hunt.closeChaseSpeedMultiplier, 1.04f));
                completed = true;
            }
            finally
            {
                hunt.Request(KhezuCommand.Sleep); encounter.SynchronizeBarrierMovement(); cinematic.Cancel();
                selection.Select(oldMonster);
                capsule.enabled = false; player.transform.SetPositionAndRotation(oldPosition, oldRotation); capsule.enabled = true; player.ResetMotionAfterTeleport();
                session.enabled = oldSession; encounter.enabled = oldEncounter; cinematic.enabled = oldCinema; sinking.enabled = oldSinking;
                GameInput.Instance.SetGameplayInputBlocked(oldInput);
                report.passed = completed && report.checks.Count > 0 && report.checks.TrueForAll(c => c.passed);
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                Debug.Log("Monster close capture: " + report.checks.FindAll(c => c.passed).Count + "/" + report.checks.Count);
                Destroy(gameObject);
            }
        }
    }
}
#endif
