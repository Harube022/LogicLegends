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
        public float score;
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
    SoloSettingsData settings;
    float nextScan, nextRank;
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
                fixtures.Add(new Fixture { light = light, enabled = light.enabled, lastApplied = light.enabled, shadows = light.shadows });
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
            if (fixture.light == null || fixture.light.enabled == fixture.lastApplied) continue;
            // A scene controller can activate a chamber/fixture after discovery.
            // Do not permanently latch its initial disabled state.
            fixture.enabled = fixture.light.enabled;
            nextRank = 0;
        }
        var camera = Camera.main;
        if (camera == null || camera.gameObject.scene != gameObject.scene) return;
        if (now >= nextRank)
        {
            nextRank = now + .25f;
            RankLights(camera.transform.position);
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
        // Runs after scene-specific lamp/flicker scripts, without fighting their intensity animations.
        ActiveLocalLights = 0;
        foreach (var fixture in fixtures)
        {
            var light = fixture.light; if (light == null) continue;
            bool active = fixture.enabled && fixture.selected;
            if (light.enabled != active) light.enabled = active;
            fixture.lastApplied = light.enabled;
            if (light.shadows != LightShadows.None) light.shadows = LightShadows.None;
            if (light.isActiveAndEnabled) ActiveLocalLights++;
        }
    }
    void RankLights(Vector3 position)
    {
        ranked.Clear();
        foreach (var fixture in fixtures)
        {
            var light = fixture.light;
            if (light == null || !fixture.enabled || !light.gameObject.activeInHierarchy) { fixture.selected = false; continue; }
            float distance = Vector3.Distance(position, light.transform.position);
            // Hysteresis avoids switching equally distant fixtures back and forth while walking.
            fixture.score = Mathf.Max(0, distance - light.range) + distance * .1f - (fixture.selected ? 1.5f : 0);
            fixture.selected = false;
            if (distance < light.range + SoloGraphicsBudget.FoliageDistance(settings)) ranked.Add(fixture);
        }
        ranked.Sort((a, b) => a.score.CompareTo(b.score));
        int count = Mathf.Min(ranked.Count, SoloGraphicsBudget.LightLimit(settings));
        for (int i = 0; i < count; i++) ranked[i].selected = true;
    }
    void OnDestroy()
    {
        foreach (var fixture in fixtures) if (fixture.light != null) { fixture.light.enabled = fixture.enabled; fixture.light.shadows = fixture.shadows; }
        foreach (var plant in grass) if (plant.renderer != null) { plant.renderer.forceRenderingOff = plant.hidden; plant.renderer.shadowCastingMode = plant.shadows; }
    }
}
