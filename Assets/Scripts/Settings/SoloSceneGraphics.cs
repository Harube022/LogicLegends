using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Scene-local cost gate for the existing settings runtime. Never changes colliders or gameplay objects.
[DefaultExecutionOrder(31950)]
public sealed class SoloSceneGraphics : MonoBehaviour
{
    sealed class Fixture
    {
        public Light light;
        public bool enabled, selected, lastApplied;
        public LightShadows shadows;
        public float score, intensity, lastIntensity, weight;
    }
    sealed class Grass
    {
        public Renderer renderer;
        public bool hidden;
        public ShadowCastingMode shadows;
        public int index;
    }
    readonly List<Fixture> fixtures = new List<Fixture>();
    readonly HashSet<Light> known = new HashSet<Light>();
    readonly List<Grass> grass = new List<Grass>();
    readonly List<Light> scan = new List<Light>();
    readonly List<Fixture> ranked = new List<Fixture>();
    readonly Plane[] lightViewPlanes = new Plane[6];
    SoloSettingsData settings;
    float nextScan, nextRank;
    bool lightsInitialized;
    const float LightFadeSeconds = .18f;
    public int ActiveLocalLights { get; private set; }
    public int ManagedGrass => grass.Count;
    public int VisibleGrass { get; private set; }

    public void Apply(SoloSettingsData value)
    {
        settings = value; nextRank = 0; nextScan = 0;
    }
    public void AddFoliage(Renderer renderer)
    {
        if (renderer == null) return;
        var lod = renderer.GetComponentInParent<LODGroup>();
        Vector3 p = lod != null ? lod.transform.position : renderer.transform.position;
        // Stable across sessions; all LODs of one plant have the same density decision.
        int hash = unchecked(Mathf.RoundToInt(p.x * 13) * 73856093 ^ Mathf.RoundToInt(p.z * 13) * 19349663);
        grass.Add(new Grass { renderer = renderer, hidden = renderer.forceRenderingOff, shadows = renderer.shadowCastingMode, index = hash });
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }
    void DiscoverLights()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            scan.Clear(); root.GetComponentsInChildren(true, scan);
            foreach (var light in scan)
            {
                if (light == null || light.type != LightType.Point && light.type != LightType.Spot || !known.Add(light)) continue;
                // Baked lights cost no real-time light pass and must not compete
                // with the fixtures lighting the moving player.
                var baking = light.bakingOutput;
                if (baking.isBaked && baking.lightmapBakeType == LightmapBakeType.Baked) continue;
                fixtures.Add(new Fixture { light = light, enabled = light.enabled, lastApplied = light.enabled,
                    shadows = light.shadows, intensity = light.intensity, lastIntensity = light.intensity });
            }
        }
    }
    void LateUpdate()
    {
        if (settings == null) return;
        float now = Time.unscaledTime;
        if (now >= nextScan) { nextScan = now + 2; DiscoverLights(); }
        foreach (var fixture in fixtures)
        {
            var light = fixture.light; if (light == null) continue;
            // A scene controller can activate a chamber/fixture after discovery.
            // Do not permanently latch its initial disabled state.
            if (light.enabled != fixture.lastApplied)
            {
                if (fixture.enabled != light.enabled) nextRank = 0;
                fixture.enabled = light.enabled;
            }
            if (!Mathf.Approximately(light.intensity, fixture.lastIntensity))
            {
                fixture.intensity = light.intensity;
                nextRank = 0;
            }
        }
        var camera = Camera.main;
        if (camera == null || camera.gameObject.scene != gameObject.scene) return;
        if (now >= nextRank)
        {
            nextRank = now + .25f;
            var player = Player.LocalInstance;
            Vector3 focus = player != null && player.gameObject.scene == gameObject.scene
                ? player.transform.position + Vector3.up : camera.transform.position;
            RankLights(camera, focus);
            float distance = SoloGraphicsBudget.FoliageDistance(settings);
            VisibleGrass = 0;
            foreach (var plant in grass)
            {
                var renderer = plant.renderer; if (renderer == null) continue;
                bool visible = !plant.hidden && SoloGraphicsBudget.KeepFoliage(settings, plant.index) &&
                    renderer.bounds.SqrDistance(camera.transform.position) < distance * distance;
                if (renderer.forceRenderingOff == visible) renderer.forceRenderingOff = !visible;
                if (visible && renderer.gameObject.activeInHierarchy) VisibleGrass++;
            }
        }
        ApplyLights(Time.unscaledDeltaTime);
    }
    void ApplyLights(float delta)
    {
        int occupied = 0, limit = SoloGraphicsBudget.LightLimit(settings);
        float step = Mathf.Max(0, delta) / LightFadeSeconds;
        foreach (var fixture in fixtures)
        {
            if (fixture.light == null || !fixture.enabled || !fixture.light.gameObject.activeInHierarchy)
                fixture.weight = 0;
            else if (!lightsInitialized) fixture.weight = fixture.selected ? 1 : 0;
            else if (fixture.weight > 0) fixture.weight = Mathf.MoveTowards(fixture.weight, fixture.selected ? 1 : 0, step);
            if (fixture.weight > 0) occupied++;
        }
        // A deliberate quality reduction must respect the new cap immediately.
        foreach (var fixture in fixtures)
            if (occupied > limit && !fixture.selected && fixture.weight > 0) { fixture.weight = 0; occupied--; }
        // Fade an outgoing light fully before handing its slot to the next one.
        // Crossfading two sets at once would exceed Forward's per-object limit.
        foreach (var fixture in ranked)
            if (fixture.selected && fixture.weight == 0 && fixture.enabled && occupied < limit)
            { fixture.weight = Mathf.Min(1, step); if (fixture.weight > 0) occupied++; }
        lightsInitialized = true;
        ActiveLocalLights = 0;
        // Disable outgoing lights before enabling replacements, preserving the cap during swaps.
        foreach (var fixture in fixtures)
            if (fixture.light != null && fixture.weight <= 0) fixture.light.enabled = false;
        foreach (var fixture in fixtures)
        {
            var light = fixture.light; if (light == null) continue;
            bool active = fixture.enabled && fixture.weight > 0 && light.gameObject.activeInHierarchy;
            if (light.enabled != active) light.enabled = active;
            fixture.lastApplied = light.enabled;
            float intensity = active ? fixture.intensity * fixture.weight : fixture.intensity;
            if (!Mathf.Approximately(light.intensity, intensity)) light.intensity = intensity;
            fixture.lastIntensity = intensity;
            if (light.shadows != LightShadows.None) light.shadows = LightShadows.None;
            if (light.isActiveAndEnabled) ActiveLocalLights++;
        }
    }
    void RankLights(Camera camera, Vector3 focus)
    {
        ranked.Clear();
        GeometryUtility.CalculateFrustumPlanes(camera, lightViewPlanes);
        foreach (var fixture in fixtures)
        {
            var light = fixture.light;
            if (light == null || !fixture.enabled || !light.gameObject.activeInHierarchy ||
                fixture.intensity <= 0 || (light.cullingMask & camera.cullingMask) == 0)
            { fixture.selected = false; continue; }
            float distance = Vector3.Distance(focus, light.transform.position);
            bool reachesPlayer = distance < light.range;
            // Test the light's volume, not its bulb: a bulb outside the picture
            // can still illuminate a visible wall or the player.
            bool visible = GeometryUtility.TestPlanesAABB(lightViewPlanes,
                new Bounds(light.transform.position, Vector3.one * (light.range * 2)));
            // Hysteresis avoids switching equally distant fixtures back and forth while walking.
            fixture.score = Mathf.Max(0, distance - light.range) + distance / Mathf.Max(1f, light.range) * 10f
                - (fixture.selected ? 1f : 0);
            if (!reachesPlayer) fixture.score += 10f;
            fixture.selected = false;
            if ((reachesPlayer || visible) && distance < light.range + SoloGraphicsBudget.LightDistance(settings)) ranked.Add(fixture);
        }
        ranked.Sort(CompareLights);
        int count = Mathf.Min(ranked.Count, SoloGraphicsBudget.LightLimit(settings));
        for (int i = 0; i < count; i++) ranked[i].selected = true;
    }
    static int CompareLights(Fixture a, Fixture b)
    {
        int score = a.score.CompareTo(b.score);
        return score != 0 ? score : a.light.GetInstanceID().CompareTo(b.light.GetInstanceID());
    }
    void OnDestroy()
    {
        foreach (var fixture in fixtures) if (fixture.light != null)
        { fixture.light.enabled = fixture.enabled; fixture.light.shadows = fixture.shadows; fixture.light.intensity = fixture.intensity; }
        foreach (var plant in grass) if (plant.renderer != null) { plant.renderer.forceRenderingOff = plant.hidden; plant.renderer.shadowCastingMode = plant.shadows; }
    }
}
