using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.Graph
{
    /// <summary>Hide only roof renderers for elevated cameras; leave all physical geometry intact.</summary>
    public static class GraphOverheadVisibility
    {
        public static Dictionary<Renderer,bool> HideCeiling()
        {
            var saved=new Dictionary<Renderer,bool>();
            foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                for(var t=r.transform;t!=null;t=t.parent)
                    if(t.name.IndexOf("Ceiling",System.StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("Roof",System.StringComparison.OrdinalIgnoreCase)>=0)
                    {saved[r]=r.enabled;r.enabled=false;break;}
            return saved;
        }
        public static void Restore(Dictionary<Renderer,bool> saved)
        {if(saved==null)return;foreach(var item in saved)if(item.Key!=null)item.Key.enabled=item.Value;saved.Clear();}
    }
}
