using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local presentation bounds, independent of topic and interaction state.</summary>
    public sealed class ArchiveWorldLabel : MonoBehaviour
    {
        internal static readonly List<ArchiveWorldLabel> All=new List<ArchiveWorldLabel>();
        public Vector2 size;
        public bool floating=true;
        public ArchiveTopicPoint point;
        internal Vector3 home,originalScale;
        internal float scale=1;
        public bool IsVisible {get;internal set;}
        public Rect ScreenBounds {get;internal set;}
        void Awake(){home=transform.localPosition;originalScale=transform.localScale;}
        void OnEnable(){if(!All.Contains(this))All.Add(this);}
        void OnDisable(){All.Remove(this);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetRegistry(){All.Clear();}
    }
}
