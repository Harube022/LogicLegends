#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Topics;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace LogicLegends.OpenArchive.Editor
{
    /// <summary>Native PhysX movement and isolated navigation checks against the saved scene.</summary>
    public static class OpenArchiveCollisionPlayChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope = "Offline Unity Play Mode. Native CharacterController probe copies the explorer's capsule and layer. Android and live-room play not tested.";
            public List<string> checks = new List<string>(), failures = new List<string>();
        }
        static void Check(Report report, bool passed, string detail) => (passed ? report.checks : report.failures).Add(detail);
        public static string Run(string output)
        {
            if (!Application.isPlaying || Photon.Pun.PhotonNetwork.InRoom) throw new InvalidOperationException("Use fresh offline MultiplayerGameplay Play Mode.");
            var report = new Report();
            var source = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).Single(c => c.name == "Archive_Explorer");
            Physics.SyncTransforms();
            var filters = OpenArchiveSolidCollisionRepair.SolidMeshes();
            foreach (var filter in filters)
            {
                var collider = filter.GetComponent<MeshCollider>();
                // Static batching replaces some renderer meshes in Play Mode; their
                // colliders correctly keep the original local-space collision mesh.
                bool meshMatches = collider != null && collider.sharedMesh != null && (collider.sharedMesh == filter.sharedMesh || filter.GetComponent<Renderer>().isPartOfStaticBatch);
                bool settings = meshMatches && collider.enabled && !collider.isTrigger && !collider.convex &&
                    !Physics.GetIgnoreLayerCollision(source.gameObject.layer, filter.gameObject.layer);
                Check(report, settings && SurfaceHit(filter, collider), "Solid mesh cooks, blocks player layer and raycasts: " + OpenArchiveSolidCollisionRepair.PathOf(filter.transform));
            }
            var go = new GameObject("Temporary_Archive_Collision_Probe");
            go.layer = source.gameObject.layer;
            go.transform.position = new Vector3(0, 2000, 0);
            var probe = go.AddComponent<CharacterController>();
            probe.radius = source.radius; probe.height = source.height; probe.center = source.center;
            probe.slopeLimit = source.slopeLimit; probe.stepOffset = source.stepOffset; probe.skinWidth = source.skinWidth; probe.minMoveDistance = source.minMoveDistance;
            try
            {
                foreach (var filter in filters.Where(f => f.name == "Question_Board_Frame" || f.name == "Stone_Post" || f.name == "Weathered_Stone_Pylon"))
                {
                    var center = filter.transform.TransformPoint(filter.sharedMesh.bounds.center);
                    var forward = filter.transform.forward; forward.y = 0; forward.Normalize();
                    foreach (int side in new[] { -1, 1 })
                    {
                        var start = center + forward * side * 5 - probe.center;
                        Place(probe, start);
                        Vector3 movement = -forward * side * 10;
                        var flags = probe.Move(movement);
                        float progress = Vector3.Dot(probe.transform.position - start, movement.normalized);
                        Check(report, (flags & CollisionFlags.Sides) != 0 && progress < 6,
                            "Capsule stops at " + filter.name + " from side " + side + " (travel " + progress.ToString("F2") + "/10m): " + filter.transform.parent.name);
                    }
                }
                var bridges = GameObject.Find("OpenArchive").transform.Find("Environment/Bridges");
                foreach (Transform bridge in bridges)
                {
                    if (bridge.GetComponents<BoxCollider>().Length != 3) continue;
                    var center = bridge.position;
                    RaycastHit bed;
                    bool foundBed = Physics.Raycast(center + Vector3.down, Vector3.down, out bed, 30, ~0, QueryTriggerInteraction.Ignore);
                    float rootY = foundBed ? bed.point.y + probe.height / 2 - probe.center.y + .12f : -100;
                    var start = bridge.TransformPoint(new Vector3(-3.7f, 0, 0)); start.y = rootY;
                    var finish = bridge.TransformPoint(new Vector3(3.7f, 0, 0)); finish.y = rootY;
                    Place(probe, start); Walk(probe, finish - start);
                    Check(report, foundBed && HorizontalDistance(probe.transform.position, finish) < 1,
                        "Bridge underpass stays open: " + center + " (remaining " + HorizontalDistance(probe.transform.position, finish).ToString("F2") + "m)");
                    float deckY = bridge.TransformPoint(new Vector3(0, .25f, 0)).y + probe.height / 2 - probe.center.y + .12f;
                    start = bridge.TransformPoint(new Vector3(0, 0, -6)); start.y = deckY;
                    finish = bridge.TransformPoint(new Vector3(0, 0, 6)); finish.y = deckY;
                    Place(probe, start); Walk(probe, finish - start);
                    Check(report, HorizontalDistance(probe.transform.position, finish) < 1,
                        "Bridge deck stays traversable: " + center + " (remaining " + HorizontalDistance(probe.transform.position, finish).ToString("F2") + "m)");
                }
                // Use an isolated NavMesh instance; never remove the game's loaded data.
                var offset = new Vector3(20000, 0, 20000);
                foreach (var nav in Object.FindObjectsByType<KhezuNavigationData>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(n => n.data).Where(d => d != null).Distinct())
                {
                    var instance = NavMesh.AddNavMeshData(nav, offset, Quaternion.identity);
                    try
                    {
                        var query = new NavMeshQueryFilter { agentTypeID = new SerializedObject(nav).FindProperty("m_AgentTypeID").intValue, areaMask = NavMesh.AllAreas & ~(1 << 1) };
                        NavMeshHit origin;
                        bool hasOrigin = NavMesh.SamplePosition(offset + Vector3.up * 2, out origin, 10, query);
                        foreach (var station in Object.FindObjectsByType<ArchiveTopicStation>(FindObjectsSortMode.None))
                        {
                            NavMeshHit destination; var path = new NavMeshPath();
                            bool sampled = NavMesh.SamplePosition(station.transform.TransformPoint(ArchiveTopicStation.BookPosition + Vector3.back * 2) + offset, out destination, 12, query);
                            bool reachable = hasOrigin && sampled && NavMesh.CalculatePath(origin.position, destination.position, query, path) && path.status == NavMeshPathStatus.PathComplete;
                            Check(report, reachable, nav.name + " has a complete nest-to-T" + (station.topic + 1) + " Book approach route");
                        }
                    }
                    finally { if (instance.valid) instance.Remove(); }
                }
            }
            finally { Object.DestroyImmediate(go); }
            report.passed = report.failures.Count == 0 && report.checks.Count > 0;
            string json = JsonUtility.ToJson(report, true);
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, json);
            return json;
        }
        static bool SurfaceHit(MeshFilter filter, MeshCollider collider)
        {
            if (collider == null) return false;
            var vertices = collider.sharedMesh.vertices; var triangles = collider.sharedMesh.triangles;
            // Sample actual faces, not the empty centre of a disconnected rock group.
            for (int i = 0; i + 2 < triangles.Length; i += Math.Max(3, triangles.Length / 90 / 3 * 3))
            {
                var a = filter.transform.TransformPoint(vertices[triangles[i]]);
                var b = filter.transform.TransformPoint(vertices[triangles[i + 1]]);
                var c = filter.transform.TransformPoint(vertices[triangles[i + 2]]);
                var normal = Vector3.Cross(b - a, c - a).normalized;
                if (normal.sqrMagnitude < .5f) continue;
                var center = (a + b + c) / 3;
                RaycastHit hit;
                if (collider.Raycast(new Ray(center + normal * .3f, -normal), out hit, .6f) && Vector3.Distance(hit.point, center) < .1f) return true;
            }
            return false;
        }
        static void Place(CharacterController probe, Vector3 position)
        {
            probe.enabled = false; probe.transform.position = position; probe.enabled = true; Physics.SyncTransforms();
        }
        static void Walk(CharacterController probe, Vector3 movement)
        {
            int steps = Mathf.CeilToInt(movement.magnitude / .2f);
            for (int i = 0; i < steps; i++) probe.Move(movement / steps);
        }
        static float HorizontalDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
    }
}
#endif
