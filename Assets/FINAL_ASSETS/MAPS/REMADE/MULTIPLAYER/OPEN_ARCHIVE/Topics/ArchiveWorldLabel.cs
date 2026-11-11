using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local presentation bounds, independent of topic and interaction state.</summary>
    public sealed class ArchiveWorldLabel : MonoBehaviour
    {
        internal static readonly List<ArchiveWorldLabel> All=new List<ArchiveWorldLabel>();
        public Vector2 size;
        public bool floating=true;
        [Tooltip("Visibility only; preserve the owner's animated transform.")]
        public bool distanceOnly;
        [Min(1)] public float fadeStart=55,hideDistance=85;
        public ArchiveTopicPoint point;
        internal Vector3 home,originalScale;
        internal float scale=1;
        public bool IsVisible {get;internal set;}
        public Rect ScreenBounds {get;internal set;}
        public float VisibilityAlpha {get;private set;}=1;
        readonly Dictionary<TMP_Text,float> originalAlpha=new Dictionary<TMP_Text,float>();
        readonly Dictionary<Renderer,bool> originalForced=new Dictionary<Renderer,bool>();
        bool refreshVisuals=true;
        void Awake(){home=transform.localPosition;originalScale=transform.localScale;}
        void OnEnable(){if(!All.Contains(this))All.Add(this);}
        void OnDisable(){All.Remove(this);SetVisibility(1);}
        void OnTransformChildrenChanged(){refreshVisuals=true;}
        internal void SetVisibility(float alpha)
        {
            if(refreshVisuals)
            {
                foreach(var text in GetComponentsInChildren<TMP_Text>(true))if(!originalAlpha.ContainsKey(text))originalAlpha.Add(text,text.alpha);
                foreach(var renderer in GetComponentsInChildren<Renderer>(true))if(!originalForced.ContainsKey(renderer))originalForced.Add(renderer,renderer.forceRenderingOff);
                refreshVisuals=false;
            }
            VisibilityAlpha=Mathf.Clamp01(alpha);
            foreach(var pair in originalAlpha)if(pair.Key!=null&&Mathf.Abs(pair.Key.alpha-pair.Value*VisibilityAlpha)>.001f)pair.Key.alpha=pair.Value*VisibilityAlpha;
            foreach(var pair in originalForced)if(pair.Key!=null)pair.Key.forceRenderingOff=pair.Value||VisibilityAlpha<=.005f;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetRegistry(){All.Clear();}
    }
}
