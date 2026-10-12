using System;
using UnityEngine;

namespace LogicLegends.Graph
{
    /// <summary>Graph-owned countdown. Penalties never increase the active-play stopwatch.</summary>
    public sealed class GraphStageClock : MonoBehaviour
    {
        public float Remaining { get; private set; }
        public float ActiveSeconds { get; private set; }
        public bool Running { get; private set; }
        private Action timeout;
        public void Initialize(float seconds, Action onTimeout)
        { Remaining = Mathf.Max(0, seconds); ActiveSeconds = 0; Running = false; timeout = onTimeout; }
        public void Resume() { if (Remaining > 0) Running = true; }
        public void Pause() => Running = false;
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            if (!Running || seconds <= 0) return;
            float elapsed = Mathf.Min(seconds, Remaining);
            ActiveSeconds += elapsed;
            Remaining = Mathf.Max(0, Remaining - elapsed);
            ExpireIfNeeded();
        }
        public void Penalize(float seconds)
        {
            if (!Running || seconds <= 0) return;
            Remaining = Mathf.Max(0, Remaining - seconds);
            ExpireIfNeeded();
        }
        private void ExpireIfNeeded()
        {
            if (Remaining > 0 || !Running) return;
            Running = false; timeout?.Invoke();
        }
    }
}
