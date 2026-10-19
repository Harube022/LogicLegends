using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchiveTesting;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LogicLegends.Tests.Khezu.Editor
{
    public static class KhezuHuntingVerification
    {
        [Serializable] public class Check { public string name, detail; public bool passed; }
        [Serializable] public class Report
        {
            public string checkedUtc;
            public bool passed;
            public List<Check> checks = new List<Check>();
            public string limits = "Editor single-process tests only. Rotation uses simulated 2/3/4-actor rosters; these are not live multiplayer sessions. Photon remote interpolation, late joins and Master handover require separate connected clients. Android device performance is not verified.";
        }
        static void Add(Report r, string name, bool pass, string detail = "") { r.checks.Add(new Check { name = name, passed = pass, detail = detail }); }
        static void Save(Report r, string file)
        {
            r.passed = r.checks.Count > 0 && r.checks.All(c => c.passed);
            Directory.CreateDirectory(KhezuHuntingSetup.Folder + "/Reports");
            File.WriteAllText(KhezuHuntingSetup.Folder + "/Reports/" + file, JsonUtility.ToJson(r, true));
        }
        [MenuItem("Tools/Logic Legends/Khezu Phase 2/Verify Navigation And Rotation")]
        public static void StaticChecks()
        {
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("o") };
            var random = new System.Random(74612);
            for (int count = 2; count <= 4; count++)
            {
                var rotation = new KhezuHuntRotation(); var ids = Enumerable.Range(1, count).ToArray(); bool unique = true;
                var orders = new HashSet<string>();
                for (int cycle = 0; cycle < 100; cycle++)
                {
                    var picks = new List<int>();
                    for (int i = 0; i < count; i++) picks.Add(rotation.Select(ids, n => random.Next(n)));
                    unique &= picks.Distinct().Count() == count; orders.Add(string.Join(",", picks));
                }
                Add(report, count + " players: no repeats within 100 full rotations", unique);
                Add(report, count + " players: randomized rotation order", orders.Count > 1, orders.Count + " unique orders");
            }
            var history = new KhezuHuntRotation(); history.Restore(new[] { 1, 2 });
            Add(report, "Temporary ineligibility preserves seen IDs", history.Select(new[] { 2, 3 }, n => 0) == 3 && history.HuntedPlayerIds.Contains(1));
            int before = history.HuntedPlayerIds.Count;
            Add(report, "No eligible players does not clear rotation", history.Select(new int[0], n => 0) == 0 && history.HuntedPlayerIds.Count == before);
            history.RemoveDisconnected(new[] { 2, 3 }); Add(report, "Disconnected IDs removed safely", !history.HuntedPlayerIds.Contains(1));
            var snapshot = new KhezuSnapshot { state = (int)KhezuHuntState.Hunting, mistakes = 4, stage = 4, target = 3,
                hunted = new[] { 2, 3 }, excluded = new[] { 1 }, position = new Vector3(4, .625f, 8), rotation = Quaternion.Euler(0, 160, 0),
                animation = "Chase", normalizedTime = .45f, animationSpeed = 1.1f, awake = true, huntSerial = 7, captureSerial = 2 };
            var restored = JsonUtility.FromJson<KhezuSnapshot>(JsonUtility.ToJson(snapshot));
            Add(report, "Network checkpoint serialization preserves state and rotation", restored.state == snapshot.state && restored.target == 3 && restored.hunted.SequenceEqual(snapshot.hunted) && restored.excluded.SequenceEqual(snapshot.excluded) && Vector3.Distance(restored.position, snapshot.position) < .0001f && Quaternion.Angle(restored.rotation, snapshot.rotation) < .01f && restored.animation == "Chase" && restored.stage == 4 && restored.captureSerial == 2);
            var loader = Object.FindFirstObjectByType<KhezuNavigationData>();
            var monster = Object.FindFirstObjectByType<KhezuHuntingController>();
            if (loader == null || monster == null) throw new InvalidOperationException("Open the Phase 2 scene.");
            var nav = NavMesh.AddNavMeshData(loader.data, new Vector3(20000, 0, 20000), Quaternion.identity);
            try
            {
                var filter = new NavMeshQueryFilter { agentTypeID = monster.GetComponent<NavMeshAgent>().agentTypeID, areaMask = NavMesh.AllAreas };
                var root = GameObject.Find("OpenArchive").transform;
                var offset = new Vector3(20000, 0, 20000);
                Func<Vector3, Vector3> world = p => root.TransformPoint(p) + offset;
                Action<string, Vector3, Vector3, bool> route = (name, from, to, straight) =>
                {
                    NavMeshHit a, b;
                    bool found = NavMesh.SamplePosition(world(from), out a, 3, filter) && NavMesh.SamplePosition(world(to), out b, 3, filter);
                    // Initialize both hits separately to keep diagnostics deterministic.
                    NavMesh.SamplePosition(world(from), out a, 3, filter); NavMesh.SamplePosition(world(to), out b, 3, filter);
                    var path = new NavMeshPath();
                    bool pass = found && NavMesh.CalculatePath(a.position, b.position, filter, path) && path.status == NavMeshPathStatus.PathComplete;
                    if (straight && pass)
                    {
                        var line = world(to) - world(from); line.y = 0;
                        foreach (var c in path.corners)
                        {
                            var relative = c - world(from); relative.y = 0;
                            float along = Vector3.Dot(relative, line) / line.sqrMagnitude;
                            if ((relative - line * along).magnitude > 2 || along < -.05f || along > 1.05f) pass = false;
                        }
                    }
                    Add(report, name, pass, "Corners: " + path.corners.Length);
                };
                var nest = new Vector3(0, .25f, 0);
                foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left }) route("Independent nest entrance " + d, nest, d * 25, true);
                route("Nest to player spawn", nest, new Vector3(0, .15f, -83), false);
                for (int i = 0; i < 4; i++)
                {
                    float angle = (45 + i * 90) * Mathf.Deg2Rad; var d = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    route("Bridge " + (i + 1) + " actual deck crossing", d * 22, d * 49.5f, true);
                    angle = (30 + i * 90) * Mathf.Deg2Rad;
                    NavMeshHit hit; bool water = NavMesh.SamplePosition(world(new Vector3(Mathf.Sin(angle) * 35, 0, Mathf.Cos(angle) * 35)), out hit, .8f, filter);
                    Add(report, "Deep water excluded " + i, !water);
                }
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI / 8, b = (i + 1) * Mathf.PI / 8;
                    route("Main ring segment " + i, new Vector3(Mathf.Sin(a) * 54, 0, Mathf.Cos(a) * 54), new Vector3(Mathf.Sin(b) * 54, 0, Mathf.Cos(b) * 54), false);
                }
                NavMeshHit outside;
                Add(report, "Outside island has no walkable navigation", !NavMesh.SamplePosition(world(new Vector3(109, 0, 0)), out outside, 1, filter));
            }
            finally { if (nav.valid) nav.Remove(); }
            Save(report, "NavigationAndRotation.json");
            Debug.Log("Khezu static checks: " + report.checks.Count(c => c.passed) + "/" + report.checks.Count);
        }
        [MenuItem("Tools/Logic Legends/Khezu Phase 2/Verify Hunting In Play Mode")]
        public static void PlayChecks()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var monster = Object.FindFirstObjectByType<KhezuHuntingController>();
            monster.StartCoroutine(VerifyPlay(monster));
        }
        static void Click(string name)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }
        static void VerifyButtonHit(Report report, string name)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Add(report, "On-screen control is reachable: " + name, hits.Count > 0 && hits[0].gameObject == button.gameObject);
        }
        static IEnumerator VerifyPlay(KhezuHuntingController monster)
        {
            var report = new Report { checkedUtc = DateTime.UtcNow.ToString("o") };
            var driver = monster.GetComponent<KhezuAwakeningTestController>();
            var session = Object.FindFirstObjectByType<OpenArchiveExplorationSession>(); session.ReturnToSpawn();
            bool complete = false; int catches = 0;
            Action<int> caught = id => catches++;
            monster.PlayerCaught += caught;
            try
            {
                var initial = monster.transform.position;
                yield return new WaitForSeconds(.3f);
                Add(report, "Automatic Sleep in nest", driver.CurrentState == "Sleep" && monster.State == KhezuHuntState.Sleeping && !monster.Agent.enabled);
                foreach (var name in new[] { "Hunt_ForceFour", "Hunt_Start", "Hunt_Reset", "Hunt_ClearRotation", "Hunt_Sleep" }) VerifyButtonHit(report, name);
                Click("Hunt_ForceFour");
                float end = Time.time + 16;
                bool stayedStill = true, sawFull = false, sawIdle = false, transitionSnapshotsMatch = true;
                int transitionSamples = 0;
                while (Time.time < end && monster.State != KhezuHuntState.Hunting && monster.State != KhezuHuntState.NavigationBlocked)
                {
                    stayedStill &= Vector3.Distance(initial, monster.transform.position) < .02f;
                    sawFull |= driver.CurrentState == "Full_Awaken"; sawIdle |= driver.CurrentState == "Idle";
                    if (driver.Animator.IsInTransition(0))
                    {
                        var next = driver.Animator.GetNextAnimatorStateInfo(0);
                        var checkpoint = monster.GetSnapshot();
                        transitionSnapshotsMatch &= next.IsName(checkpoint.animation) && Mathf.Abs(next.normalizedTime - checkpoint.normalizedTime) < .001f;
                        transitionSamples++;
                    }
                    yield return null;
                }
                Add(report, "Four shared mistakes recorded", driver.Mistakes == 4);
                Add(report, "No movement before Full_Awaken completes", stayedStill && sawFull && sawIdle);
                Add(report, "Crossfade checkpoints retain destination state for host recovery", transitionSamples > 0 && transitionSnapshotsMatch, transitionSamples + " samples");
                Add(report, "Eligible local target selected once", monster.TargetPlayerId == -1 && monster.HuntSerial == 1 && monster.HuntedPlayerIds.Contains(-1));
                Click("Hunt_Start"); Add(report, "Duplicate Start does not choose again", monster.HuntSerial == 1);
                var beforeMove = monster.transform.position; bool chase = false; float maxStep = 0;
                var previous = beforeMove; float previousTime = Time.time;
                bool relocatedPlayer = false, followedUpdatedDestination = false;
                end = Time.time + 35;
                while (Time.time < end && monster.State != KhezuHuntState.Caught)
                {
                    chase |= driver.CurrentState == "Chase";
                    if (!relocatedPlayer && Vector3.Distance(beforeMove, monster.transform.position) > 35)
                    {
                        // Relocate only the test target to another known dry main-ring point.
                        // The monster must update its route normally, without being moved by the test.
                        var position = GameObject.Find("OpenArchive").transform.TransformPoint(new Vector3(54, 0, 0));
                        var capsule = session.Explorer.GetComponent<CharacterController>();
                        RaycastHit hit;
                        if (Physics.Raycast(position + Vector3.up * 30, Vector3.down, out hit, 60, ~(1 << 7), QueryTriggerInteraction.Ignore))
                            position.y = hit.point.y - (capsule.center.y - capsule.height * .5f) * session.Explorer.transform.lossyScale.y + capsule.skinWidth;
                        capsule.enabled = false; session.Explorer.transform.position = position; capsule.enabled = true;
                        session.Explorer.ResetMotionAfterTeleport(); relocatedPlayer = true;
                    }
                    if (relocatedPlayer && monster.Agent.enabled && monster.Agent.isOnNavMesh && monster.Agent.hasPath)
                        followedUpdatedDestination |= Vector2.Distance(new Vector2(monster.Agent.destination.x, monster.Agent.destination.z), new Vector2(session.Explorer.transform.position.x, session.Explorer.transform.position.z)) < 4;
                    float dt = Time.time - previousTime;
                    if (dt > 0) maxStep = Mathf.Max(maxStep, Vector3.Distance(previous, monster.transform.position) / dt);
                    previous = monster.transform.position; previousTime = Time.time;
                    yield return null;
                }
                Add(report, "Actually leaves nest and follows player", Vector3.Distance(beforeMove, monster.transform.position) > 30);
                Add(report, "Same selected target is followed after changing location", relocatedPlayer && followedUpdatedDestination && monster.TargetPlayerId == -1 && monster.HuntSerial == 1);
                Add(report, "Existing Chase animation used", chase);
                Add(report, "Navigation advances without normal chase teleport", maxStep < monster.normalPlayerRunSpeed * monster.chaseSpeedMultiplier * 1.8f, "Maximum observed movement speed " + maxStep);
                Add(report, "Catch stops navigation and records target", monster.State == KhezuHuntState.Caught && monster.CaughtPlayerId == -1 && !monster.Agent.enabled && catches == 1);
                var stopped = monster.transform.position; yield return new WaitForSeconds(.6f);
                Add(report, "Catch notification fires only once", catches == 1 && Vector3.Distance(stopped, monster.transform.position) < .02f);
                var checkpointCaught = monster.GetSnapshot();
                monster.ApplySnapshot(checkpointCaught, true);
                Add(report, "Restoring a caught checkpoint does not replay capture", catches == 1);
                ScreenCapture.CaptureScreenshot(KhezuHuntingSetup.Folder + "/Reports/Caught_PlayMode.png");
                Click("Hunt_Sleep"); yield return new WaitForSeconds(.4f);
                Add(report, "Return to Sleep preserves rotation", monster.State == KhezuHuntState.Sleeping && driver.Mistakes == 0 && monster.HuntedPlayerIds.Contains(-1));
                Click("Hunt_ClearRotation"); Add(report, "Explicit full reset clears rotation", monster.HuntedPlayerIds.Count == 0);
                complete = true;
            }
            finally
            {
                monster.PlayerCaught -= caught;
                Add(report, "Verification completed", complete);
                Save(report, "HuntingPlayMode.json");
                Debug.Log("Khezu Play checks: " + report.checks.Count(c => c.passed) + "/" + report.checks.Count);
            }
        }
    }
}
