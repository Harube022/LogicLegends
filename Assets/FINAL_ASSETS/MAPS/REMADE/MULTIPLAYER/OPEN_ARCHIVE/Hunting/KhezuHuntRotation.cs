using System;
using System.Collections.Generic;
using System.Linq;

namespace LogicLegends.OpenArchive.Hunting
{
    /// <summary>Actor IDs survive sleep; temporary ineligibility does not erase history.</summary>
    public sealed class KhezuHuntRotation
    {
        public readonly HashSet<int> HuntedPlayerIds = new HashSet<int>();
        public int Select(IEnumerable<int> eligible, Func<int, int> randomIndex, int avoidImmediate=0)
        {
            var ids = eligible.Distinct().OrderBy(id => id).ToArray();
            if (ids.Length == 0) return 0;
            var candidates = ids.Where(id => !HuntedPlayerIds.Contains(id)).ToArray();
            if (candidates.Length == 0) { HuntedPlayerIds.Clear(); candidates = ids; }
            if(ids.Length>1 && candidates.Contains(avoidImmediate))candidates=candidates.Where(id=>id!=avoidImmediate).ToArray();
            if(candidates.Length==0)candidates=ids.Where(id=>id!=avoidImmediate).ToArray();
            int selected = candidates[randomIndex(candidates.Length)];
            HuntedPlayerIds.Add(selected);
            return selected;
        }
        public void RemoveDisconnected(IEnumerable<int> connected)
        {
            var valid = new HashSet<int>(connected);
            HuntedPlayerIds.RemoveWhere(id => !valid.Contains(id));
        }
        public void Restore(IEnumerable<int> ids) { HuntedPlayerIds.Clear(); foreach (int id in ids) HuntedPlayerIds.Add(id); }
    }
}
