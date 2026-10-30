using System;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Cinematics.VFX;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Khezu;
using LogicLegends.Tests.Monsters;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Cosmetic world transitions and the existing night encounter. Shared timing lives in the topic session.</summary>
    public sealed partial class ArchiveWorldProgression : MonoBehaviour
    {
        public ArchiveTopicSession session;
        public DevelopmentMonsterSelection selector;
        public KhezuHuntingController secondary;
        public Transform secondarySpawn;
        public Light sun,moon;
        public AudioClip nightWind,nightCracks,entranceRoar;
        [Header("Second guardian cinematic accents")]
        public AudioClip entranceRise,entranceImpact;
        public AudioSource[] dayAmbience;
        [Min(1)] public float lightingTransitionSeconds=8;
        [Min(2)] public float entranceSeconds=6;
        [Min(3)] public float warningSeconds=5;
        [NonSerialized] public bool suppressThreatsForVerification;
        public string SecondaryName {get;private set;}="Second guardian";
        static readonly Color[] SkyColors={Color.white,new Color(1,.74f,.49f),new Color(.45f,.29f,.51f),new Color(.10f,.17f,.33f)};
        static readonly Color[] SunColors={Color.white,new Color(1,.69f,.40f),new Color(1,.43f,.30f),new Color(.48f,.62f,1)};
        static readonly float[] SunStrength={1, .84f,.34f,.10f},SkyExposure={1,.8f,.45f,.19f};
        Material originalSky,runtimeSky;Color originalFog,originalSkyAmbient,originalEquator,originalGround,originalSun,originalSkyTint;
        Quaternion originalSunRotation;float originalSunIntensity,originalFogDensity,originalExposure;bool originalFogEnabled;
        bool initialized,ambientDiscovered;AmbientMode originalAmbientMode;
        public bool NightAmbiencePlaying => wind!=null&&cracks!=null&&wind.isPlaying&&cracks.isPlaying&&wind.volume>0&&cracks.volume>0;
        float[] dayVolumes;AudioSource wind,cracks,roar;
        Canvas canvas;TMP_Text banner;RawImage film;RectTransform frame;
        Camera filmCamera;RenderTexture filmTexture;
        string match="";bool releasedOnce,profileReady,heardEntrance,closedReader,primaryAwakenRequested;
        void Start()
        {
            if(session==null)session=GetComponent<ArchiveTopicSession>();
            initialized=true;originalAmbientMode=RenderSettings.ambientMode;RenderSettings.ambientMode=AmbientMode.Trilight;
            if(dayAmbience==null)dayAmbience=Array.Empty<AudioSource>();
            originalSky=RenderSettings.skybox;originalFog=RenderSettings.fogColor;originalFogDensity=RenderSettings.fogDensity;originalFogEnabled=RenderSettings.fog;
            CaptureNightAtmosphere();
            originalSkyAmbient=RenderSettings.ambientSkyColor;originalEquator=RenderSettings.ambientEquatorColor;originalGround=RenderSettings.ambientGroundColor;
            if(originalSky!=null){runtimeSky=new Material(originalSky);originalExposure=runtimeSky.HasProperty("_Exposure")?runtimeSky.GetFloat("_Exposure"):1;originalSkyTint=runtimeSky.HasProperty("_Tint")?runtimeSky.GetColor("_Tint"):Color.white;RenderSettings.skybox=runtimeSky;}
            if(sun!=null){originalSunIntensity=sun.intensity;originalSun=sun.color;originalSunRotation=sun.transform.rotation;}
            dayVolumes=dayAmbience.Select(a=>a==null?0:a.volume).ToArray();
            wind=Voice("Night_Wind",nightWind,true,.0f);cracks=Voice("Night_Cracks",nightCracks,true,0);roar=Voice("Second_Guardian_Entrance",entranceRoar,false,.55f);
            wind.priority=220;cracks.priority=230;roar.priority=85;
            BuildEntrancePresentation();
            BuildUI();
        }
        AudioSource Voice(string name,AudioClip clip,bool loop,float volume)
        {var go=new GameObject(name);go.transform.SetParent(transform,false);var a=go.AddComponent<AudioSource>();a.clip=clip;a.loop=loop;a.playOnAwake=false;a.spatialBlend=0;a.volume=volume;AudioVolumeSettings.Route(a,GameAudioChannel.SoundFX);if(loop&&clip!=null&&volume>0)a.Play();return a;}
        void ApplyProfile()
        {
            if(selector==null||secondary==null||secondarySpawn==null)return;
            var kind=selector.Selected==DevelopmentMonster.Rajang?DevelopmentMonster.Khezu:DevelopmentMonster.Rajang;
            SecondaryName=kind==DevelopmentMonster.Rajang?"Rajang":"Khezu";
            var profile=kind==DevelopmentMonster.Rajang?selector.RajangEncounterProfile:selector.KhezuEncounterProfile;
            var original=selector.ActiveTest.transform;
            string Relative(Transform root){string path=root.name;while(root.parent!=null&&root.parent!=original){root=root.parent;path=root.name+"/"+path;}return path;}
            foreach(var group in new[]{selector.KhezuEncounterProfile,selector.RajangEncounterProfile})foreach(var root in group.visualRoots)
            {var copy=secondary.transform.Find(Relative(root.transform));if(copy!=null)copy.gameObject.SetActive(false);}
            foreach(var root in profile.visualRoots){var copy=secondary.transform.Find(Relative(root.transform));if(copy!=null)copy.gameObject.SetActive(true);}
            secondary.transform.localScale=profile.localScale;secondary.transform.SetPositionAndRotation(secondarySpawn.position,secondarySpawn.rotation*Quaternion.Euler(profile.nestEuler));
            var agent=secondary.GetComponent<NavMeshAgent>();agent.enabled=false;agent.agentTypeID=profile.agentTypeId;agent.radius=profile.agentRadius;agent.height=profile.agentHeight;
            var filter=new NavMeshQueryFilter{agentTypeID=profile.agentTypeId,areaMask=NavMesh.AllAreas};
            if(NavMesh.SamplePosition(secondarySpawn.position,out var hit,20,filter))secondary.transform.position=hit.position;
            var animator=secondary.GetComponent<Animator>();animator.avatar=profile.avatar;animator.runtimeAnimatorController=profile.controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var body=secondary.GetComponent<BoxCollider>();body.center=profile.bodyCenter;body.size=profile.bodySize;
            var cinematic=secondary.GetComponent<KhezuCinematicJumpscareController>();cinematic.presentationPrefab=profile.presentationPrefab;cinematic.capture=profile.capture;cinematic.jumpscare=profile.jumpscare;cinematic.barrierCast=profile.barrierCast;
            cinematic.mouthUpperBone=profile.mouthUpper;cinematic.mouthLowerBone=profile.mouthLower;cinematic.faceTargetOffset=profile.faceOffset;cinematic.framingDistance=profile.framingDistance;
            secondary.GetComponent<KhezuBarrierVfxPresentation>().styleOverride=profile.barrierStyle;
            var encounter=secondary.GetComponent<KhezuCaptureEncounterController>();encounter.inferenceBarrier=kind==DevelopmentMonster.Rajang;encounter.developmentTestOutcomes=false;
            secondary.rajangCounterProfile=kind==DevelopmentMonster.Rajang;secondary.health=session.hunting.health;secondary.monsterSpawnPoint=secondarySpawn;
            secondary.GetComponent<KhezuAwakeningTestController>().RefreshAssemblyAnchor();
            if(secondary.gameObject.activeInHierarchy)secondary.RefreshAnimationRig();
            profileReady=true;
            ResetFilmSubject();
        }
        void LateUpdate()
        {
            if(session==null||!session.Ready||session.State.worldTime==null||canvas==null)return;
            if(MultiplayerLobbyService.IsGameplayRecovering){ReleaseIntroLocks();frame.gameObject.SetActive(false);if(warningRoot!=null)warningRoot.gameObject.SetActive(false);banner.transform.parent.gameObject.SetActive(false);ClearEntrancePresentation();return;}
            var w=session.State.worldTime;
            if(!ambientDiscovered&&Time.timeSinceLevelLoad>1)
            {
                ambientDiscovered=true;
                var previousSources=dayAmbience;var previousVolumes=dayVolumes;
                dayAmbience=dayAmbience.Concat(FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(a=>a!=wind&&a!=cracks&&a.loop&&a.clip!=null&&a.spatialBlend==0&&
                    (a.name=="MainMenuMusic"||a.outputAudioMixerGroup!=null&&a.outputAudioMixerGroup.name=="Music"))).Where(a=>a!=null).Distinct().ToArray();
                dayVolumes=dayAmbience.Select(a=>{int index=Array.IndexOf(previousSources,a);return index>=0&&index<previousVolumes.Length?previousVolumes[index]:a.volume;}).ToArray();
            }
            if(match!=session.State.matchId)
            {
                match=session.State.matchId;releasedOnce=profileReady=heardEntrance=closedReader=primaryAwakenRequested=false;
                DeactivateSecondary();
            }
            float blend=Mathf.Clamp01((float)(ArchiveTopicSession.Clock-w.changedAt)/lightingTransitionSeconds);blend=Mathf.SmoothStep(0,1,blend);
            int from=Mathf.Clamp(w.previousPhase,0,3),to=Mathf.Clamp(w.phase,0,3);
            var sky=originalSkyTint*Color.Lerp(SkyColors[from],SkyColors[to],blend);float night=Mathf.Lerp(from==3?1:0,to==3?1:0,blend);
            if(runtimeSky!=null){if(runtimeSky.HasProperty("_Tint"))runtimeSky.SetColor("_Tint",sky);if(runtimeSky.HasProperty("_Exposure"))runtimeSky.SetFloat("_Exposure",originalExposure*Mathf.Lerp(SkyExposure[from],SkyExposure[to],blend));}
            if(sun!=null){sun.color=Color.Lerp(originalSun*SunColors[from],originalSun*SunColors[to],blend);sun.intensity=originalSunIntensity*Mathf.Lerp(SunStrength[from],SunStrength[to],blend);sun.transform.rotation=Quaternion.Slerp(SunRotation(from),SunRotation(to),blend);}
            if(moon!=null)moon.intensity=night*.35f;
            RenderSettings.ambientSkyColor=Color.Lerp(originalSkyAmbient,new Color(.22f,.29f,.48f),night);RenderSettings.ambientEquatorColor=Color.Lerp(originalEquator,new Color(.20f,.24f,.34f),night);RenderSettings.ambientGroundColor=Color.Lerp(originalGround,new Color(.14f,.17f,.23f),night);
            ApplyNightFog(night);
            bool entering=w.phase==3&&w.arrivalStartedAt>=0&&!session.TeamWon&&!session.TeamDefeated;float elapsed=entering?(float)(ArchiveTopicSession.Clock-w.arrivalStartedAt):-1;
            bool captured=Player.LocalInstance!=null&&Player.LocalInstance.IsMovementLocked&&!introPlayers.Contains(Player.LocalInstance);
            RefreshEntranceToken(w.arrivalStartedAt);
            bool showFilm=entering&&elapsed<entranceSeconds&&!captured&&!suppressThreatsForVerification;
            if(entering&&!suppressThreatsForVerification)
            {
                if(!profileReady)ApplyProfile();
                if(secondary!=null&&!secondary.gameObject.activeSelf)
                {
                    secondary.gameObject.SetActive(true);
                    // Awake caches the first profile only. A reused night guardian
                    // must refresh the other rig's clip durations and active reach bones.
                    secondary.RefreshAnimationRig();
                }
                if(!closedReader){closedReader=true;if(!captured)ArchiveTopicUI.Current?.Close();}
                UpdateEntranceSound(elapsed);
                if(secondary!=null&&secondary.Initialized)
                {
                    if(!w.secondaryReleased)secondary.HoldNightEntrance(Mathf.Clamp01(elapsed/entranceSeconds));
                    else if(!releasedOnce){secondary.ReleaseNightEntrance();releasedOnce=true;}
                }
                if(!primaryAwakenRequested&&session.IsAuthority&&!session.TeamWon){primaryAwakenRequested=true;session.hunting.AwakenForNight();}
            }
            else if(secondary!=null&&secondary.gameObject.activeSelf)
            {DeactivateSecondary();releasedOnce=profileReady=heardEntrance=closedReader=primaryAwakenRequested=false;}
            if(session.TeamWon&&secondary!=null&&secondary.gameObject.activeSelf&&secondary.State!=KhezuHuntState.Sleeping)secondary.Request(KhezuCommand.Sleep);
            PresentIntroLocks(showFilm);
            frame.gameObject.SetActive(showFilm);
            if(showFilm){EnsureFilmCamera();UpdateFilmCamera(Mathf.Clamp01(elapsed/entranceSeconds));}
            else {RestoreGameplayCameraRendering();if(filmCamera!=null)filmCamera.enabled=false;if(entranceKey!=null)entranceKey.enabled=false;if(entranceRim!=null)entranceRim.enabled=false;}
            PresentEntranceEffects(entering&&!captured&&!suppressThreatsForVerification&&elapsed<entranceSeconds,elapsed);
            bool warning=entering&&elapsed<entranceSeconds+warningSeconds+3;
            UpdateNightAmbience(night,warning||captured||session.TeamWon||session.TeamDefeated);
            bool transition=w.serial>0&&ArchiveTopicSession.Clock-w.changedAt<lightingTransitionSeconds;
            banner.transform.parent.gameObject.SetActive((warning||transition)&&!captured);
            if(warning)banner.text=elapsed<entranceSeconds?"NIGHTFALL - "+SecondaryName.ToUpperInvariant()+" AWAKENS":!w.secondaryReleased?SecondaryName.ToUpperInvariant()+" HUNTS IN "+Mathf.CeilToInt(entranceSeconds+warningSeconds-elapsed)+"...":"NIGHT HUNT - TWO GUARDIANS ARE ACTIVE";
            else if(transition)banner.text=new[]{"DAYLIGHT","AFTERNOON","EVENING","NIGHT"}[to]+" - "+session.State.stations.Count(s=>s.phase==2)+" / 10 TOPICS COMPLETE";
            UpdateWarningFx(warning&&!showFilm&&!captured&&!session.TeamWon&&!session.TeamDefeated,elapsed);
        }
        void DeactivateSecondary()
        {
            if(secondary==null)return;
            // Inactive monsters are outside TickSurvival's roster. Release the hit-location
            // anchor before reset/deactivation so the next entrance starts from its spawn.
            secondary.HoldTranquilized(false);
            secondary.Request(KhezuCommand.Sleep);
            secondary.gameObject.SetActive(false);
        }
        Quaternion SunRotation(int phase) => phase==0?originalSunRotation:Quaternion.Euler(phase==1?25:phase==2?8:40,phase==1?85:phase==2?100:145,0);
        void EnsureFilmCamera()
        {
            if(filmCamera!=null){filmCamera.enabled=true;SuppressGameplayCameraRendering();return;}
            float aspect=Mathf.Clamp((float)Mathf.Max(1,Screen.width)/Mathf.Max(1,Screen.height),.6f,2.5f);
            int width=Mathf.Clamp(Screen.width,320,1280);int height=Mathf.Clamp(Mathf.RoundToInt(width/aspect),180,1280);
            filmTexture=new RenderTexture(width,height,16){name="Night_Entrance_Fullscreen",antiAliasing=1};filmTexture.Create();film.texture=filmTexture;
            var go=new GameObject("Night_Entrance_Cinematic_Camera");go.transform.SetParent(transform,false);filmCamera=go.AddComponent<Camera>();filmCamera.targetTexture=filmTexture;filmCamera.aspect=(float)width/height;filmCamera.fieldOfView=50;filmCamera.nearClipPlane=.15f;filmCamera.farClipPlane=180;filmCamera.allowHDR=false;filmCamera.allowMSAA=false;
            filmCamera.GetUniversalAdditionalCameraData().renderShadows=false;
            SuppressGameplayCameraRendering();
        }
        void OnDestroy()
        {
            ReleaseIntroLocks();
            ClearEntrancePresentation();
            if(entranceEffects!=null)entranceEffects.TearDown();
            if(filmCamera!=null)Destroy(filmCamera.gameObject);if(canvas!=null)Destroy(canvas.gameObject);
            if(entranceCharge!=null)Destroy(entranceCharge.gameObject);if(entranceHit!=null)Destroy(entranceHit.gameObject);
            if(entranceKey!=null)Destroy(entranceKey.gameObject);if(entranceRim!=null)Destroy(entranceRim.gameObject);
            if(!initialized)return;
            RestoreNightAtmosphere();
            if(RenderSettings.skybox==runtimeSky){RenderSettings.skybox=originalSky;RenderSettings.ambientMode=originalAmbientMode;RenderSettings.ambientSkyColor=originalSkyAmbient;RenderSettings.ambientEquatorColor=originalEquator;RenderSettings.ambientGroundColor=originalGround;}
            if(runtimeSky!=null)Destroy(runtimeSky);if(filmTexture!=null){filmTexture.Release();Destroy(filmTexture);}
            if(sun!=null){sun.intensity=originalSunIntensity;sun.color=originalSun;sun.transform.rotation=originalSunRotation;}
        }
    }
}
