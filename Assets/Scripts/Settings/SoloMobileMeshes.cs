using System;
using UnityEngine;

// Prepared from the source model's own LODs; no runtime mesh simplification.
public sealed class SoloMobileMeshes : ScriptableObject
{
    [Serializable] public sealed class Entry
    {
        public Mesh source;
        public Mesh low, medium, high;
        public Mesh ForTier(int tier) => tier == 0 ? low : tier == 1 ? medium : high;
    }
    public Entry[] entries;
    public Mesh Resolve(Mesh source, int tier)
    {
        foreach (var entry in entries)
            if (entry.source == source || entry.low == source || entry.medium == source || entry.high == source) return entry.ForTier(tier);
        return source;
    }
}
