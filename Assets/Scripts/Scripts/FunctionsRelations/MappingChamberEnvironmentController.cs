using UnityEngine;

public enum MappingChamberQuality { Auto, Low, Medium, High }

/// <summary>
/// Visual-only chamber animation. It observes the stage's public state without changing
/// interaction, timer, answers, lasers, or player movement.
/// </summary>
[DisallowMultipleComponent]
public sealed class MappingChamberEnvironmentController : MonoBehaviour
{
    [SerializeField] private FunctionsRelationsStageManager stage;
    [SerializeField] private Transform book;
    [SerializeField] private Renderer bookRune;
    [SerializeField] private Renderer[] progressionRunes = new Renderer[0];
    [SerializeField] private Renderer[] crystals = new Renderer[0];
    [SerializeField] private Renderer[] torchFlames = new Renderer[0];
    [SerializeField] private ParticleSystem bookMotes;
    [SerializeField] private GameObject distantDetails;
    [SerializeField] private GameObject distantTrees;
    [SerializeField] private MappingChamberQuality quality = MappingChamberQuality.Auto;

    private MaterialPropertyBlock block;
    private Player player;
    private FunctionsRelationsState previousState;
    private float nextVisualUpdate;
    private float activationUntil;
    private int lastProgress = -1;

    public MappingChamberQuality Quality => quality;

    public void Configure(FunctionsRelationsStageManager manager, Transform bookTransform,
        Renderer centralRune, Renderer[] stageRunes, Renderer[] crystalRenderers,
        Renderer[] flames, ParticleSystem motes, GameObject backgroundDetails,
        GameObject farTreeGroup)
    {
        stage = manager;
        book = bookTransform;
        bookRune = centralRune;
        progressionRunes = stageRunes;
        crystals = crystalRenderers;
        torchFlames = flames;
        bookMotes = motes;
        distantDetails = backgroundDetails;
        distantTrees = farTreeGroup;
    }

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (stage == null) stage = FindFirstObjectByType<FunctionsRelationsStageManager>();
        previousState = stage != null ? stage.State : FunctionsRelationsState.WAITING_FOR_ACTIVATION;
        ApplyQuality();
        RefreshProgress();
    }

    public void SetQuality(MappingChamberQuality value)
    {
        quality = value;
        ApplyQuality();
    }

    private MappingChamberQuality EffectiveQuality()
    {
        if (quality != MappingChamberQuality.Auto) return quality;
        if (SystemInfo.deviceType != DeviceType.Handheld) return MappingChamberQuality.High;
        return SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 2400 ?
            MappingChamberQuality.Low : MappingChamberQuality.Medium;
    }

    private void ApplyQuality()
    {
        MappingChamberQuality active = EffectiveQuality();
        if (distantDetails != null) distantDetails.SetActive(active == MappingChamberQuality.High);
        if (distantTrees != null) distantTrees.SetActive(active != MappingChamberQuality.Low);
        if (bookMotes != null)
        {
            ParticleSystem.MainModule main = bookMotes.main;
            main.maxParticles = active == MappingChamberQuality.Low ? 0 :
                active == MappingChamberQuality.Medium ? 6 : 12;
            ParticleSystem.EmissionModule emission = bookMotes.emission;
            emission.rateOverTime = active == MappingChamberQuality.Low ? 0f :
                active == MappingChamberQuality.Medium ? 1.5f : 3f;
            if (active == MappingChamberQuality.Low) bookMotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            else if (!bookMotes.isPlaying) bookMotes.Play();
        }
    }

    private void Update()
    {
        if (Time.time < nextVisualUpdate) return;
        nextVisualUpdate = Time.time + (EffectiveQuality() == MappingChamberQuality.Low ? .18f : .09f);
        if (stage == null) return;

        FunctionsRelationsState state = stage.State;
        if (previousState == FunctionsRelationsState.WAITING_FOR_ACTIVATION &&
            state == FunctionsRelationsState.ACTIVE)
            activationUntil = Time.time + 1.2f;
        previousState = state;

        if (lastProgress != stage.CompletedChallengeCount) RefreshProgress();
        if (player == null) player = Player.LocalInstance;
        float near = 0f;
        if (player != null && book != null)
        {
            float distanceSq = (player.transform.position - book.position).sqrMagnitude;
            near = 1f - Mathf.Clamp01((distanceSq - 9f) / 40f);
        }
        float pulse = .5f + .5f * Mathf.Sin(Time.time * 2.0f);
        float activation = Mathf.Clamp01((activationUntil - Time.time) / 1.2f);
        Color runeColor = Color.Lerp(new Color(.10f, .36f, .44f), new Color(.25f, 1.12f, 1.24f),
            .22f + near * .35f + activation * .45f + pulse * .12f);
        SetColor(bookRune, runeColor);

        Color crystalColor = Color.Lerp(new Color(.07f, .40f, .62f), new Color(.25f, .83f, 1.05f),
            .17f + pulse * .20f + stage.CompletedChallengeCount * .09f);
        for (int i = 0; i < crystals.Length; i++) SetColor(crystals[i], crystalColor);
        Color flameColor = Color.Lerp(new Color(.94f, .33f, .07f),
            new Color(1.24f, .64f, .15f), .35f + .25f * Mathf.Sin(Time.time * 7.1f));
        for (int i = 0; i < torchFlames.Length; i++) SetColor(torchFlames[i], flameColor);
    }

    private void RefreshProgress()
    {
        if (stage == null) return;
        lastProgress = stage.CompletedChallengeCount;
        for (int i = 0; i < progressionRunes.Length; i++)
            SetColor(progressionRunes[i], i <= lastProgress ?
                new Color(.17f, .72f, .84f) : new Color(.08f, .16f, .20f));
    }

    private void SetColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
    }
}
