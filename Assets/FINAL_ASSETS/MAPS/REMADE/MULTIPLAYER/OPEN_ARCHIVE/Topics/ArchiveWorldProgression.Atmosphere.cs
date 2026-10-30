using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveWorldProgression
    {
        [Header("Night distance fog - lightweight URP scene fog")]
        public Color nightFogColor=new Color(.10f,.13f,.18f);
        [Range(.004f,.04f)] public float nightFogDensity=.016f;
        [Header("Night ambience - uses the existing SFX mixer")]
        [Range(0,.5f)] public float nightWindVolume=.22f;
        [Range(0,.5f)] public float nightDetailVolume=.24f;
        [Range(0,1)] public float warningAmbienceMultiplier=.18f;
        FogMode originalFogMode;
        float originalFogStart,originalFogEnd,nightAudioDuck=1;
        public float NightFogBlend {get;private set;}
        public bool NightFogActive => NightFogBlend>0&&RenderSettings.fog&&RenderSettings.fogMode==FogMode.ExponentialSquared;
        public float NightAmbienceDuck => nightAudioDuck;
        public int NightAmbienceVoiceCount => (wind!=null&&wind.isPlaying?1:0)+(cracks!=null&&cracks.isPlaying?1:0);

        void CaptureNightAtmosphere()
        {
            originalFogMode=RenderSettings.fogMode;
            originalFogStart=RenderSettings.fogStartDistance;originalFogEnd=RenderSettings.fogEndDistance;
        }
        float OriginalExp2Density()
        {
            if(!originalFogEnabled)return 0;
            if(originalFogMode==FogMode.ExponentialSquared)return originalFogDensity;
            // If an author switches the daylight mode, match its transmission at
            // 64 m before blending. The normal saved Exp2 profile stays exact.
            const float reference=64;
            float transmission=originalFogMode==FogMode.Exponential?Mathf.Exp(-originalFogDensity*reference):
                Mathf.Clamp01((originalFogEnd-reference)/Mathf.Max(.001f,originalFogEnd-originalFogStart));
            return Mathf.Sqrt(-Mathf.Log(Mathf.Clamp(transmission,.0001f,1)))/reference;
        }
        void ApplyNightFog(float blend)
        {
            NightFogBlend=Mathf.Clamp01(blend);
            if(NightFogBlend<=.00001f){RestoreDayFog();return;}
            // URP's existing per-vertex/fragment fog is shared by the map's
            // custom shaders. No fullscreen pass, particles or depth texture.
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=Color.Lerp(originalFog,nightFogColor,NightFogBlend);
            RenderSettings.fogDensity=Mathf.Lerp(OriginalExp2Density(),nightFogDensity,NightFogBlend);
            RenderSettings.fogStartDistance=originalFogStart;RenderSettings.fogEndDistance=originalFogEnd;
        }
        void RestoreDayFog()
        {
            RenderSettings.fog=originalFogEnabled;RenderSettings.fogMode=originalFogMode;
            RenderSettings.fogColor=originalFog;RenderSettings.fogDensity=originalFogDensity;
            RenderSettings.fogStartDistance=originalFogStart;RenderSettings.fogEndDistance=originalFogEnd;
            NightFogBlend=0;
        }
        void UpdateNightAmbience(float night,bool warningOrCaptured)
        {
            nightAudioDuck=Mathf.MoveTowards(nightAudioDuck,warningOrCaptured?warningAmbienceMultiplier:1,Time.unscaledDeltaTime*2);
            SetNightLoop(wind,night*nightWindVolume*Mathf.Lerp(.48f,1,nightAudioDuck));
            SetNightLoop(cracks,night*nightDetailVolume*nightAudioDuck);
            for(int i=0;dayAmbience!=null&&dayVolumes!=null&&i<dayAmbience.Length&&i<dayVolumes.Length;i++)
                if(dayAmbience[i]!=null)dayAmbience[i].volume=dayVolumes[i]*(1-night*.95f);
        }
        static void SetNightLoop(AudioSource voice,float volume)
        {
            if(voice==null)return;
            voice.volume=volume;
            if(volume<=.00001f){if(voice.isPlaying)voice.Stop();return;}
            if(voice.clip!=null&&!voice.isPlaying)voice.Play();
        }
        void RestoreNightAtmosphere()
        {
            if(!initialized)return;
            RestoreDayFog();nightAudioDuck=1;
            if(wind!=null){wind.Stop();wind.volume=0;}if(cracks!=null){cracks.Stop();cracks.volume=0;}
            for(int i=0;dayAmbience!=null&&dayVolumes!=null&&i<dayAmbience.Length&&i<dayVolumes.Length;i++)
                if(dayAmbience[i]!=null)dayAmbience[i].volume=dayVolumes[i];
        }
    }
}
