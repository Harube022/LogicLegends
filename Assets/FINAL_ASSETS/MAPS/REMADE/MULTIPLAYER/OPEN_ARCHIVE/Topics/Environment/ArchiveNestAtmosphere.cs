using LogicLegends.OpenArchive.Hunting;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local cosmetic nest warnings and waymarks. All encounter state belongs to the existing session.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveNestAtmosphere : MonoBehaviour
    {
        public ArchiveTopicSession session;
        public Transform nestAnchor;
        public Renderer[] warningRunes = System.Array.Empty<Renderer>();
        public Transform[] embers = System.Array.Empty<Transform>();
        public Renderer[] emberRenderers = System.Array.Empty<Renderer>();
        public Transform[] chains = System.Array.Empty<Transform>();
        public Renderer[] chainRenderers = System.Array.Empty<Renderer>();
        public Renderer[] waymarkRunes = System.Array.Empty<Renderer>();
        public Color[] waymarkColors = System.Array.Empty<Color>();
        [Header("Cosmetic warnings only")]
        public Color khezuColor = new Color(1, .055f, .12f);
        public Color rajangColor = new Color(1, .38f, .035f);
        [Range(0, 4)] public float glowIntensity = 1.3f;
        [Range(.1f, 3)] public float animationSpeed = 1;
        [Range(30, 180)] public float animationDistance = 100;
        [Range(0, .3f)] public float chainSwayDegrees = .24f;
        [Range(0, 1)] public float nightWaymarkGlow = .45f;
        public float WarningAmount { get; private set; }
        public Color WarningColor { get; private set; }
        public bool NearNest { get; private set; }
        public int ActiveNestRendererCount { get; private set; }
        Vector3[] emberRest;
        Quaternion[] chainRest;
        MaterialPropertyBlock block;
        float nextMarkerRead;
        static readonly int Base = Shader.PropertyToID("_BaseColor"), Emission = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            block = new MaterialPropertyBlock();
            emberRest = new Vector3[embers.Length];
            chainRest = new Quaternion[chains.Length];
            for (int i = 0; i < embers.Length; i++) if (embers[i] != null) emberRest[i] = embers[i].localPosition;
            for (int i = 0; i < chains.Length; i++) if (chains[i] != null) chainRest[i] = chains[i].localRotation;
        }
        public static float WarningFor(KhezuHuntState state, int mistakes, bool tranquilized)
        {
            if (tranquilized || state == KhezuHuntState.NestSleeping || state == KhezuHuntState.ReturningToNest) return 0;
            if (state == KhezuHuntState.Sleeping) return Mathf.Clamp01(mistakes / 4f) * .45f;
            if (state == KhezuHuntState.Waking) return Mathf.Lerp(.15f, 1, Mathf.Clamp01(mistakes / 4f));
            return 1;
        }
        float ReadMonster(KhezuHuntingController monster)
            => monster != null && monster.isActiveAndEnabled ? WarningFor(monster.State, monster.Mistakes, monster.IsTranquilized) : 0;
        void Paint(Renderer renderer, Color tint, float glow)
        {
            if (renderer == null) return;
            block.Clear(); block.SetColor(Base, tint); block.SetColor(Emission, tint * Mathf.Max(0, glow)); renderer.SetPropertyBlock(block);
        }
        void LateUpdate()
        {
            if (session == null || !session.Ready || nestAnchor == null) return;
            var observer = Player.LocalInstance != null ? Player.LocalInstance.transform : null;
            NearNest = session.NightEntranceActive || observer != null && (observer.position - nestAnchor.position).sqrMagnitude < animationDistance * animationDistance;
            var primary = session.hunting;
            var secondary = session.worldProgression != null ? session.worldProgression.secondary : null;
            float a = ReadMonster(primary), b = ReadMonster(secondary);
            var dominant = b > a ? secondary : primary;
            WarningAmount = Mathf.Max(a, b);
            WarningColor = dominant != null && dominant.rajangCounterProfile ? rajangColor : khezuColor;
            float clock = (float)ArchiveTopicSession.Clock * animationSpeed;
            float beat = .5f + .5f * Mathf.Sin(clock * Mathf.Lerp(1.1f, 4, WarningAmount));
            float strength = glowIntensity * (.035f + WarningAmount * (.35f + beat * .9f));
            ActiveNestRendererCount = 0;
            for (int i = 0; i < warningRunes.Length; i++)
            {
                var r = warningRunes[i]; if (r == null) continue;
                r.enabled = NearNest; if (NearNest) { Paint(r, WarningColor * (.25f + WarningAmount * .6f), strength); ActiveNestRendererCount++; }
            }
            for (int i = 0; i < emberRenderers.Length; i++)
            {
                var r = emberRenderers[i]; if (r == null) continue;
                bool show = NearNest && WarningAmount > .03f; r.enabled = show;
                if (!show) continue;
                Paint(r, WarningColor, strength * 1.2f); ActiveNestRendererCount++;
                if (i < embers.Length && embers[i] != null)
                    embers[i].localPosition = emberRest[i] + Vector3.up * (.14f + Mathf.Sin(clock * 1.2f + i) * .12f) * WarningAmount;
            }
            for (int i = 0; i < chainRenderers.Length; i++)
            {
                var r = chainRenderers[i]; if (r == null) continue;
                r.enabled = NearNest; if (NearNest) ActiveNestRendererCount++;
                if (i < chains.Length && chains[i] != null)
                    chains[i].localRotation = chainRest[i] * Quaternion.Euler(0, 0, NearNest ? Mathf.Sin(clock * 2 + i) * chainSwayDegrees * WarningAmount : 0);
            }
            if (Time.unscaledTime < nextMarkerRead) return;
            nextMarkerRead = Time.unscaledTime + .3f;
            bool night = session.State.worldTime != null && session.State.worldTime.phase >= 2;
            for (int i = 0; i < waymarkRunes.Length; i++)
            {
                var r = waymarkRunes[i]; if (r == null) continue;
                bool near = observer != null && (r.transform.position - observer.position).sqrMagnitude < 180 * 180;
                r.enabled = near;
                if (near) Paint(r, i < waymarkColors.Length ? waymarkColors[i] : Color.white, night ? nightWaymarkGlow : .10f);
            }
        }
        void OnDisable()
        {
            if (emberRest != null) for (int i = 0; i < embers.Length; i++) if (embers[i] != null) embers[i].localPosition = emberRest[i];
            if (chainRest != null) for (int i = 0; i < chains.Length; i++) if (chains[i] != null) chains[i].localRotation = chainRest[i];
            foreach (var r in warningRunes) if (r != null) { r.SetPropertyBlock(null); r.enabled = false; }
            foreach (var r in emberRenderers) if (r != null) { r.SetPropertyBlock(null); r.enabled = false; }
            foreach (var r in chainRenderers) if (r != null) r.enabled = true;
            foreach (var r in waymarkRunes) if (r != null) { r.SetPropertyBlock(null); r.enabled = true; }
        }
    }
}
