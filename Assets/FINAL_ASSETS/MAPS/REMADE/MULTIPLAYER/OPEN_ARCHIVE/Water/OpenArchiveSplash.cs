using UnityEngine;

namespace LogicLegends.OpenArchive.Water
{
    /// <summary>Pooled low-poly droplets, a fading surface ring, and existing water audio.</summary>
    public sealed class OpenArchiveSplash : MonoBehaviour
    {
        public ParticleSystem droplets;
        public LineRenderer ripple;
        public AudioSource sound;
        readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[48];
        float age, strength;
        bool active;
        public bool IsActive => active;
        public int DropletCount => droplets != null ? droplets.particleCount : 0;
        public void Show(Vector3 surface, float impact, AudioClip clip, float volume, int seed)
        {
            transform.position = surface + Vector3.up * .04f;
            age = 0; strength = Mathf.Clamp(impact, .25f, 1.5f); active = true;
            var random = new System.Random(seed);
            int count = Mathf.Clamp(Mathf.RoundToInt(28 * strength), 8, particles.Length);
            droplets.Clear(); droplets.Play();
            for (int i = 0; i < count; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                Vector3 outward = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                float lifetime = .45f + (float)random.NextDouble() * .35f;
                particles[i] = new ParticleSystem.Particle {
                    position = transform.position + outward * (.2f + (float)random.NextDouble() * .35f),
                    velocity = outward * (2 + (float)random.NextDouble() * 3) * strength + Vector3.up * (3 + (float)random.NextDouble() * 5) * Mathf.Sqrt(strength),
                    startLifetime = lifetime, remainingLifetime = lifetime,
                    startSize = (.13f + (float)random.NextDouble() * .21f) * Mathf.Sqrt(strength),
                    startColor = Color.Lerp(new Color(.32f, .88f, .94f), Color.white, (float)random.NextDouble() * .8f)
                };
            }
            droplets.SetParticles(particles, count);
            ripple.enabled = true; UpdateRing();
            sound.Stop(); sound.pitch = .94f + (float)random.NextDouble() * .12f;
            AudioVolumeSettings.Route(sound, GameAudioChannel.SoundFX);
            if (clip != null) sound.PlayOneShot(clip, volume);
        }
        void Update()
        {
            if (!active) return;
            age += Time.deltaTime; UpdateRing();
            if (age >= 2.25f) { active = false; ripple.enabled = false; }
        }
        void UpdateRing()
        {
            float progress = Mathf.Clamp01(age / .9f);
            float radius = Mathf.Lerp(.35f, 2.3f, progress) * Mathf.Sqrt(strength);
            for (int i = 0; i < ripple.positionCount; i++) {
                float angle = i * Mathf.PI * 2 / ripple.positionCount;
                ripple.SetPosition(i, new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius));
            }
            var color = new Color(.64f, .96f, 1, (1 - progress) * .85f);
            ripple.startColor = ripple.endColor = color;
            ripple.widthMultiplier = Mathf.Lerp(.14f, .025f, progress);
            ripple.enabled = progress < 1;
        }
        public void Stop()
        {
            active = false; droplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ripple.enabled = false; sound.Stop();
        }
    }
}
