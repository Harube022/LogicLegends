using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.OpenArchive.Water
{
    [DefaultExecutionOrder(-100)]
    public sealed class OpenArchiveWater : MonoBehaviour
    {
        public MeshFilter[] surfaces;
        public OpenArchiveSplash splashPrefab;
        public AudioClip landingSound, wadingSound;
        [Range(.5f, 1)] public float movementMultiplier = .85f;
        [Range(0, 1)] public float landingVolume = .65f;
        public int LandingSplashCount { get; private set; }
        public int WadingSplashCount { get; private set; }
        public OpenArchiveSplash LastSplash { get; private set; }
        sealed class Surface {
            public MeshFilter filter; public Vector3[] vertices; public int[] triangles; public Bounds bounds;
        }
        sealed class Walker {
            public Player player; public CharacterController capsule;
            public Vector3 previousFoot; public bool initialized, wet;
            public float distance, lastSplash = -10;
        }
        readonly List<Surface> areas = new List<Surface>();
        readonly List<Walker> walkers = new List<Walker>();
        readonly List<OpenArchiveSplash> pool = new List<OpenArchiveSplash>();
        GameObject effects;
        float nextScan;
        int sequence;
        void Awake()
        {
            foreach (var filter in surfaces) if (filter != null && filter.sharedMesh != null)
                areas.Add(new Surface {filter = filter, vertices = filter.sharedMesh.vertices, triangles = filter.sharedMesh.triangles, bounds = filter.sharedMesh.bounds});
        }
        void Update()
        {
            if (Time.time >= nextScan) { ScanPlayers(); nextScan = Time.time + .5f; }
            foreach (var walker in walkers)
            {
                if (walker.player == null) continue;
                bool wet = IsInWater(walker.capsule);
                if (walker.player == Player.LocalInstance && walker.player.enabled && walker.capsule.enabled && wet)
                    walker.player.SetMovementSpeedModifier(this, movementMultiplier);
                else walker.player.RemoveMovementSpeedModifier(this);
            }
        }
        void ScanPlayers()
        {
            walkers.RemoveAll(w => w.player == null);
            foreach (var player in FindObjectsByType<Player>(FindObjectsSortMode.None))
            {
                if (player.gameObject.scene != gameObject.scene || walkers.Exists(w => w.player == player)) continue;
                var capsule = player.GetComponent<CharacterController>();
                if (capsule != null) walkers.Add(new Walker {player = player, capsule = capsule});
            }
        }
        public static Vector3 Feet(CharacterController capsule) => capsule.transform.TransformPoint(capsule.center) - Vector3.up * capsule.height * Mathf.Abs(capsule.transform.lossyScale.y) * .5f;
        public bool IsInWater(CharacterController capsule)
        {
            if (capsule == null || !capsule.enabled || !capsule.gameObject.activeInHierarchy) return false;
            Vector3 feet = Feet(capsule); float height;
            return SampleSurface(feet, out height) && feet.y <= height + .12f;
        }
        public bool SampleSurface(Vector3 world, out float height)
        {
            foreach (var area in areas)
            {
                Vector3 p = area.filter.transform.InverseTransformPoint(world);
                if (p.x < area.bounds.min.x || p.x > area.bounds.max.x || p.z < area.bounds.min.z || p.z > area.bounds.max.z) continue;
                for (int i = 0; i < area.triangles.Length; i += 3)
                {
                    Vector3 a = area.vertices[area.triangles[i]], b = area.vertices[area.triangles[i + 1]], c = area.vertices[area.triangles[i + 2]];
                    if (Mathf.Abs(Cross(b - a, c - a)) < .000001f) continue;
                    float ab = Cross(b - a, p - a), bc = Cross(c - b, p - b), ca = Cross(a - c, p - c);
                    if ((ab >= -.00001f && bc >= -.00001f && ca >= -.00001f) || (ab <= .00001f && bc <= .00001f && ca <= .00001f))
                    { height = area.filter.transform.TransformPoint(new Vector3(p.x, a.y, p.z)).y; return true; }
                }
            }
            height = 0; return false;
        }
        static float Cross(Vector3 a, Vector3 b) => a.x * b.z - a.z * b.x;
        void LateUpdate()
        {
            foreach (var walker in walkers)
            {
                if (walker.player == null || walker.capsule == null) continue;
                Vector3 feet = Feet(walker.capsule); float height;
                bool wet = SampleSurface(feet, out height) && feet.y <= height + .12f;
                Vector3 delta = feet - walker.previousFoot;
                bool usable = walker.initialized && walker.player.enabled && walker.capsule.enabled && Time.timeScale > 0 &&
                    delta.magnitude < Mathf.Max(6, walker.player.moveSpeed * Time.deltaTime * 4);
                if (usable && wet && !walker.wet && walker.previousFoot.y > height + .12f && delta.y < 0 && Time.time - walker.lastSplash > .35f)
                {
                    float speed = -delta.y / Mathf.Max(.001f, Time.deltaTime);
                    Emit(new Vector3(feet.x, height, feet.z), Mathf.Lerp(.65f, 1.3f, Mathf.InverseLerp(4, 25, speed)), landingSound, landingVolume);
                    LandingSplashCount++; walker.lastSplash = Time.time; walker.distance = 0;
                }
                if (usable && wet && walker.capsule.isGrounded)
                {
                    delta.y = 0; walker.distance += delta.magnitude;
                    if (walker.distance >= 3 && Time.time - walker.lastSplash > .4f)
                    {
                        Emit(new Vector3(feet.x, height, feet.z), .28f, wadingSound, .16f);
                        WadingSplashCount++; walker.distance = 0; walker.lastSplash = Time.time;
                    }
                }
                else walker.distance = 0;
                walker.previousFoot = feet; walker.wet = wet; walker.initialized = true;
            }
        }
        void Emit(Vector3 point, float strength, AudioClip clip, float volume)
        {
            if (splashPrefab == null) return;
            if (effects == null) {
                effects = new GameObject("OpenArchive_Water_Effects");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effects, gameObject.scene);
            }
            var effect = pool.Find(s => !s.IsActive);
            if (effect == null && pool.Count < 8) { effect = Instantiate(splashPrefab, effects.transform); effect.transform.localScale = Vector3.one; pool.Add(effect); }
            if (effect == null) { effect = pool[sequence % pool.Count]; effect.Stop(); }
            effect.Show(point, strength, clip, volume, ++sequence * 7369); LastSplash = effect;
        }
        void OnDisable()
        {
            foreach (var walker in walkers) if (walker.player != null) walker.player.RemoveMovementSpeedModifier(this);
            foreach (var effect in pool) if (effect != null) effect.Stop();
            if (effects != null) Destroy(effects); pool.Clear(); effects = null;
        }
    }
}
