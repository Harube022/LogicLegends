using System;
using UnityEngine;

/// <summary>Legacy fixture ambience outside solo trials. Solo lamps stay steady.</summary>
[DisallowMultipleComponent]
public sealed class ChamberLightFlicker : MonoBehaviour
{
    [SerializeField] private Light[] flickeringLights = Array.Empty<Light>();
    [SerializeField, Min(2f)] private float minimumInterval = 6f;
    [SerializeField, Min(2f)] private float maximumInterval = 14f;
    [SerializeField, Range(0f, 1f)] private float outageChance = .25f;
    [SerializeField] private Vector2 dipBrightness = new Vector2(.2f, .55f);
    [SerializeField] private Vector2 holdSeconds = new Vector2(.18f, .45f);
    [SerializeField, Min(.1f)] private float fadeDownSeconds = .2f;
    [SerializeField, Min(.1f)] private float recoverySeconds = .45f;
    [SerializeField] private int seed = 51027;

    private System.Random random;
    private float[] baseline;
    private float remaining, elapsed, hold, low;
    private int active = -1, previous = -1;
    public Light[] AffectedLights => flickeringLights;
    public Light ActiveLight => active >= 0 ? flickeringLights[active] : null;
    public int CompletedEvents { get; private set; }

    private float Between(float a, float b) => a + (b - a) * (float)random.NextDouble();
    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        // Keep solo fixtures steady. Their brightness must not dip to zero
        // independently of the player's actions or the settings menu.
        if (SoloGameSettings.IsTrial(gameObject.scene.name)) return;
        random = new System.Random(seed);
        baseline = new float[flickeringLights.Length];
        for (int i = 0; i < baseline.Length; i++)
            baseline[i] = flickeringLights[i] != null ? flickeringLights[i].intensity : 0;
        active = previous = -1; CompletedEvents = 0;
        remaining = Between(Mathf.Max(2, minimumInterval), Mathf.Max(minimumInterval, maximumInterval));
    }
    private void Update()
    {
        // Independent of gameplay pauses; at most one small group of fixtures is being evaluated.
        Advance(Mathf.Min(Time.unscaledDeltaTime, .1f));
    }
    private void Advance(float delta)
    {
        if (baseline == null || baseline.Length == 0) return;
        if (active < 0)
        {
            remaining -= delta;
            if (remaining > 0) return;
            active = random.Next(flickeringLights.Length);
            if (flickeringLights.Length > 1 && active == previous) active = (active + 1) % flickeringLights.Length;
            previous = active; elapsed = 0;
            low = random.NextDouble() < outageChance ? 0 : Between(dipBrightness.x, dipBrightness.y);
            hold = Between(holdSeconds.x, holdSeconds.y);
        }
        elapsed += delta;
        float down = Mathf.Max(.1f, fadeDownSeconds), up = Mathf.Max(.1f, recoverySeconds);
        float brightness = elapsed < down ? Mathf.Lerp(1, low, Mathf.SmoothStep(0, 1, elapsed / down)) :
            elapsed < down + hold ? low : Mathf.Lerp(low, 1, Mathf.SmoothStep(0, 1, (elapsed - down - hold) / up));
        if (flickeringLights[active] != null) flickeringLights[active].intensity = baseline[active] * brightness;
        if (elapsed < down + hold + up) return;
        if (flickeringLights[active] != null) flickeringLights[active].intensity = baseline[active];
        active = -1; CompletedEvents++;
        remaining = Between(Mathf.Max(2, minimumInterval), Mathf.Max(minimumInterval, maximumInterval));
    }
    private void OnDisable()
    {
        if (baseline != null)
            for (int i = 0; i < baseline.Length; i++)
                if (flickeringLights[i] != null) flickeringLights[i].intensity = baseline[i];
        baseline = null; active = -1;
    }
}
