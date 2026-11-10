using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LogicLegends.Graph
{
    /// <summary>Graph-only fixture animation. Navigation, cameras and puzzle state are untouched.</summary>
    [DisallowMultipleComponent]
    public sealed class GraphChamberAtmosphere : MonoBehaviour
    {
        [Serializable]
        public sealed class CrystalSurface
        {
            public Renderer renderer;
            public int materialIndex;
            [ColorUsage(false,true)] public Color emission=new Color(.025f,.55f,1.1f);
            [NonSerialized] public MaterialPropertyBlock properties;
        }

        public Light[] torchLights=Array.Empty<Light>();
        public Light[] crystalLights=Array.Empty<Light>();
        public CrystalSurface[] crystalSurfaces=Array.Empty<CrystalSurface>();
        public Volume bloomVolume;
        static GraphChamberAtmosphere active;
        public static bool WantsBloom(Scene scene)=>active!=null&&active.isActiveAndEnabled&&
            active.gameObject.scene==scene&&active.bloomVolume!=null&&SoloGameSettings.Current.lightingQuality>0;
        [Min(0)] public float torchIntensity=150;
        [Min(1)] public float torchRange=24;
        [Range(0,.15f)] public float torchFlicker=.06f;
        [Min(0)] public float crystalIntensity=100;
        [Min(1)] public float crystalRange=18;
        [Range(0,.2f)] public float crystalPulse=.12f;
        [Min(3)] public float pulseSeconds=5.5f;
        float nextUpdate;
        static readonly int Emission=Shader.PropertyToID("_EmissionColor");

        void OnEnable()
        {
            if(gameObject.scene.name=="Graph")active=this;
            if(bloomVolume!=null)bloomVolume.enabled=WantsBloom(gameObject.scene);
            foreach(var surface in crystalSurfaces)
                if(surface.renderer!=null)
                {
                    surface.properties=new MaterialPropertyBlock();
                    surface.renderer.GetPropertyBlock(surface.properties,surface.materialIndex);
                }
            UpdateFixtures(0);
        }
        void Update()
        {
            if(bloomVolume!=null)bloomVolume.enabled=WantsBloom(gameObject.scene);
            // Twenty small updates per second are sufficient for slow, smooth ambience.
            // Paused reading/tutorial interfaces keep their lighting steady.
            if(Time.timeScale<=0||Time.unscaledTime<nextUpdate)return;
            nextUpdate=Time.unscaledTime+.05f;
            UpdateFixtures(Time.time);
        }
        void UpdateFixtures(float time)
        {
            for(int i=0;i<torchLights.Length;i++)
            {
                var light=torchLights[i];if(light==null)continue;
                float flicker=(Mathf.PerlinNoise(i*3.17f+11,time*.85f)-.5f)*2;
                light.intensity=torchIntensity*(1+torchFlicker*flicker);
                light.range=torchRange;
            }
            for(int i=0;i<crystalLights.Length;i++)
            {
                var light=crystalLights[i];if(light==null)continue;
                light.intensity=crystalIntensity*Pulse(time,0);
                light.range=crystalRange;
            }
            foreach(var surface in crystalSurfaces)
                if(surface.renderer!=null&&surface.properties!=null)
                {
                    surface.properties.SetColor(Emission,surface.emission*Pulse(time,0));
                    surface.renderer.SetPropertyBlock(surface.properties,surface.materialIndex);
                }
        }
        float Pulse(float time,float phase)=>1+crystalPulse*Mathf.Sin(time*Mathf.PI*2/Mathf.Max(3,pulseSeconds)+phase);
        void OnDisable()
        {
            if(active==this)active=null;
            if(bloomVolume!=null)bloomVolume.enabled=false;
            // Authored steady values are retained if the effect is disabled.
            foreach(var light in torchLights)if(light!=null)light.intensity=torchIntensity;
            foreach(var light in crystalLights)if(light!=null)light.intensity=crystalIntensity;
            foreach(var surface in crystalSurfaces)
                if(surface.renderer!=null&&surface.properties!=null)
                {
                    surface.properties.SetColor(Emission,surface.emission);
                    surface.renderer.SetPropertyBlock(surface.properties,surface.materialIndex);
                }
        }
    }
}
