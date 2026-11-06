using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local film presentation of the existing shared countdown and victory receipts.</summary>
    [DefaultExecutionOrder(3300),DisallowMultipleComponent]
    public sealed class ArchiveMatchCinematic : MonoBehaviour
    {
        public ArchiveTopicSession session;
        [Header("Reusable cinematic materials")]
        public Material energyMaterial,heroSummonMaterial;
        [Header("Sound")]
        public AudioClip summoningRise,summoningArrival,victoryRise,victoryImpact;
        [Header("Presentation only")]
        public bool playIntro=true,playVictory=true,reducedMotion;
        public bool playGuardianIntro=true;
        [Range(3,8)] public float victorySeconds=6;
        public static ArchiveMatchCinematic Current {get;private set;}
        public bool IsPlaying=>frame!=null&&frame.gameObject.activeInHierarchy;
        public string ShotName=>mode.ToString();
        public float Progress {get;private set;}
        public int LocksHeld=>locks.Count;
        public int HeroOverridesCount=>heroes.Count;
        public Camera FilmCamera=>filmCamera;
        public bool GuardianIntroVisible=>guardianIntro!=null&&guardianIntro.Visible;
        public string GuardianName=>guardianIntro?.SpeciesName??"";
        public string GuardianAnimation=>guardianIntro?.AnimationName??"";
        public int GuardianRendererCount=>guardianIntro?.RendererCount??0;
        public Bounds GuardianBounds {get;private set;}
        public bool PreviewPlaying=>preview!=Shot.None&&IsPlaying;
        public bool HidesResults=>isActiveAndEnabled&&AssetsReady&&session!=null&&session.Ready&&playVictory&&
            !MultiplayerLobbyService.IsGameplayRecovering&&session.TeamWon&&WindowProgress(ArchiveTopicSession.Clock,session.State.exit.wonAt,victorySeconds)>=0;
        public static bool Covers(ArchiveTopicSession target)=>Current!=null&&Current.session==target&&Current.IsPlaying;
        public static bool CoversVictoryResults(ArchiveTopicSession target)=>Current!=null&&Current.session==target&&Current.HidesResults;
        public static float WindowProgress(double now,double started,float duration)
        {double elapsed=now-started;return started<0||duration<=0||elapsed<0||elapsed>=duration?-1:Mathf.Clamp01((float)(elapsed/duration));}

        enum Shot {None,Waiting,Summoning,Victory}
        Shot mode,preview;
        string match="";
        double previewStarted;
        bool risePlayed,impactPlayed;
        Canvas canvas;
        RectTransform frame,safeRoot;
        RawImage film;
        Image fade,accent;
        TMP_Text title,subtitle,heroTitle;
        Camera filmCamera;
        RenderTexture texture;
        AudioSource rise,impact;
        ArchiveMatchCinematicFx fx;
        ArchiveActiveMonsterIntro guardianIntro;
        bool guardianRoarPlayed;
        readonly List<Player> players=new List<Player>();
        readonly Dictionary<Player,Vector3> locks=new Dictionary<Player,Vector3>();
        readonly List<Hero> heroes=new List<Hero>();
        readonly List<CoveredCamera> coveredCameras=new List<CoveredCamera>();
        readonly List<Vector3> feet=new List<Vector3>();
        readonly Dictionary<Behaviour,bool> nameplates=new Dictionary<Behaviour,bool>();
        Bounds teamBounds;
        Vector3 forward=Vector3.forward;
        float nextRoster,nextHeroScan;
        bool AssetsReady=>energyMaterial!=null&&heroSummonMaterial!=null&&heroSummonMaterial.shader!=null&&heroSummonMaterial.shader.isSupported;
        bool ReducedMotion=>reducedMotion||(session?.hunting?.GetComponent<KhezuCinematicJumpscareController>()?.reducedMotion??false);
        sealed class Hero
        {
            public SkinnedMeshRenderer renderer;
            public Material[] original,reveal;
            public MaterialPropertyBlock properties;
            public float bottom,top;
        }
        struct CoveredCamera {public Camera camera;public int mask;public CameraClearFlags clear;public Color color;}

        void Awake(){if(Current!=null&&Current!=this){enabled=false;return;}Current=this;}
        void Start(){if(session==null)session=GetComponent<ArchiveTopicSession>();}
        void Update()
        {
            if(session==null||!session.Ready||!AssetsReady||MultiplayerLobbyService.IsGameplayRecovering)
            {Finish();return;}
            if(match!=session.State.matchId){Finish();preview=Shot.None;match=session.State.matchId;}
            var next=Shot.None;float progress=0;
            if(preview!=Shot.None)
            {
                progress=WindowProgress(ArchiveTopicSession.Clock,previewStarted,preview==Shot.Victory?victorySeconds:ArchiveTopicSession.MatchCountdownSeconds);
                if(progress>=0)next=preview;else preview=Shot.None;
            }
            if(next==Shot.None&&HidesResults)
            {next=Shot.Victory;progress=WindowProgress(ArchiveTopicSession.Clock,session.State.exit.wonAt,victorySeconds);}
            else if(next==Shot.None&&playIntro&&!session.TeamWon&&!session.TeamDefeated)
            {
                if(session.MatchTimePhase==ArchiveMatchTimePhase.WaitingForPlayers)next=Shot.Waiting;
                else if(session.MatchTimePhase==ArchiveMatchTimePhase.Countdown)
                {
                    progress=WindowProgress(ArchiveTopicSession.Clock,session.State.matchTime.countdownStartedAt,ArchiveTopicSession.MatchCountdownSeconds);
                    if(progress>=0)next=Shot.Summoning;
                }
            }
            if(next==Shot.None){Finish();return;}
            if(next!=mode){Finish();EnsurePresentation();mode=next;risePlayed=impactPlayed=guardianRoarPlayed=false;rise.pitch=impact.pitch=1;nextRoster=nextHeroScan=0;}
            Progress=progress;
            if(Time.unscaledTime>=nextRoster){nextRoster=Time.unscaledTime+.2f;ReadTeam();}
            if(mode==Shot.Waiting||mode==Shot.Summoning||preview==Shot.Victory)HoldOwnedPlayers();
            if(mode==Shot.Summoning)RevealHeroes(progress);
            else RestoreHeroes();
            UpdateSound(progress);
        }
        void LateUpdate()
        {
            if(mode==Shot.None||session==null||!session.Ready||MultiplayerLobbyService.IsGameplayRecovering)return;
            EnsurePresentation();EnsureTexture();CoverGameplayRendering();
            frame.gameObject.SetActive(true);filmCamera.enabled=true;
            float guardianProgress=mode==Shot.Waiting&&playGuardianIntro?
                WindowProgress(ArchiveTopicSession.Clock,session.State.matchTime.createdAt,ArchiveTopicSession.MatchGuardianIntroSeconds):-1;
            if(guardianProgress>=0&&guardianIntro.Present(session.hunting,guardianProgress,ReducedMotion,out var guardianBounds))
            {GuardianBounds=guardianBounds;PresentGuardian(guardianProgress,guardianBounds);}
            else
            {
                guardianIntro.Hide();
                if(mode==Shot.Victory)PresentVictory(Progress);else PresentSummoning(Progress);
            }
            var safe=Screen.safeArea;
            safeRoot.anchorMin=new Vector2(safe.xMin/Mathf.Max(1,Screen.width),safe.yMin/Mathf.Max(1,Screen.height));
            safeRoot.anchorMax=new Vector2(safe.xMax/Mathf.Max(1,Screen.width),safe.yMax/Mathf.Max(1,Screen.height));
        }
        void ReadTeam()
        {
            players.Clear();feet.Clear();
            players.AddRange(FindObjectsByType<Player>(FindObjectsSortMode.None).Where(p=>p.gameObject.scene==gameObject.scene&&p.gameObject.activeInHierarchy)
                .OrderBy(p=>p.GetComponent<PhotonView>()?.OwnerActorNr??0).ThenBy(p=>p.GetInstanceID()).Take(4));
            bool first=true;var heading=Vector3.zero;
            foreach(var player in players)
            {
                foreach(var label in player.GetComponentsInChildren<TMP_Text>(true))
                {if(!nameplates.ContainsKey(label))nameplates[label]=label.enabled;label.enabled=false;}
                foreach(var worldCanvas in player.GetComponentsInChildren<Canvas>(true).Where(c=>c.renderMode==RenderMode.WorldSpace))
                {if(!nameplates.ContainsKey(worldCanvas))nameplates[worldCanvas]=worldCanvas.enabled;worldCanvas.enabled=false;}
                var capsule=player.GetComponent<CharacterController>();
                var point=player.transform.position;
                if(capsule!=null)point.y=capsule.bounds.min.y;
                feet.Add(point+Vector3.up*.04f);heading+=player.transform.forward;
                var skins=player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
                foreach(var skin in skins){if(first){teamBounds=skin.bounds;first=false;}else teamBounds.Encapsulate(skin.bounds);}
            }
            if(first)
            {
                var spawn=GameObject.Find("OpenArchive/PlayerSpawnArea/PlayerSpawn_03");
                teamBounds=new Bounds(spawn!=null?spawn.transform.position+Vector3.up*1.5f:Vector3.zero,new Vector3(4,3,4));
            }
            heading.y=0;if(heading.sqrMagnitude>.01f)forward=heading.normalized;
        }
        void HoldOwnedPlayers()
        {
            foreach(var player in players)
            {
                if(PhotonNetwork.InRoom&&player!=session.ActorPlayer(session.LocalActor))continue;
                if(!locks.TryGetValue(player,out var anchor))locks[player]=anchor=player.transform.position;
                player.AcquireMovementLock(this,"Archive summoning",anchor,90);
            }
        }
        void RevealHeroes(float progress)
        {
            if(Time.unscaledTime>=nextHeroScan)
            {
              nextHeroScan=Time.unscaledTime+.2f;
              foreach(var player in players)
              {
                var skins=player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
                if(skins.Length==0)continue;
                float bottom=skins.Min(s=>s.bounds.min.y),top=skins.Max(s=>s.bounds.max.y);
                foreach(var skin in skins)
                {
                if(!skin.enabled||!skin.gameObject.activeInHierarchy||heroes.Any(h=>h.renderer==skin))continue;
                var properties=new MaterialPropertyBlock();skin.GetPropertyBlock(properties);
                var originals=skin.sharedMaterials;var reveal=new Material[originals.Length];
                for(int i=0;i<reveal.Length;i++)
                {
                    var source=originals[i];var material=new Material(heroSummonMaterial){name="Archive_Summon_Temporary"};
                    if(source!=null)
                    {
                        string map=source.HasProperty("_BaseMap")?"_BaseMap":source.HasProperty("_MainTex")?"_MainTex":null;
                        if(map!=null){material.SetTexture("_BaseMap",source.GetTexture(map));material.SetTextureScale("_BaseMap",source.GetTextureScale(map));material.SetTextureOffset("_BaseMap",source.GetTextureOffset(map));}
                        if(source.HasProperty("_BaseColor"))material.SetColor("_BaseColor",source.GetColor("_BaseColor"));
                        else if(source.HasProperty("_Color"))material.SetColor("_BaseColor",source.GetColor("_Color"));
                    }
                    var textureOverride=properties.GetTexture("_BaseMap")??properties.GetTexture("_MainTex");
                    if(textureOverride!=null)material.SetTexture("_BaseMap",textureOverride);
                    var tintOverride=properties.GetColor("_BaseColor");if(tintOverride.a>0)material.SetColor("_BaseColor",tintOverride);
                    reveal[i]=material;
                }
                heroes.Add(new Hero{renderer=skin,original=originals,reveal=reveal,properties=properties,bottom=bottom,top=top});
                skin.sharedMaterials=reveal;skin.SetPropertyBlock(null);
                }
              }
            }
            float amount=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.62f,progress));
            foreach(var hero in heroes)foreach(var material in hero.reveal)
            {
                material.SetFloat("_RevealY",Mathf.Lerp(hero.bottom-.35f,hero.top+.35f,amount));
                material.SetColor("_GlowColor",new Color(.20f,1.25f,1.05f));
            }
        }
        void RestoreHeroes()
        {
            foreach(var hero in heroes)
            {
                if(hero.renderer!=null)
                {
                    var current=hero.renderer.sharedMaterials;bool allOwned=current.SequenceEqual(hero.reveal),changed=false;
                    // An async avatar/profile update may replace only some slots
                    // during loading. Restore our remaining slots without undoing it.
                    for(int i=0;i<Mathf.Min(current.Length,hero.reveal.Length);i++)
                        if(current[i]==hero.reveal[i]){current[i]=hero.original[i];changed=true;}
                    if(changed)hero.renderer.sharedMaterials=current;
                    if(allOwned)hero.renderer.SetPropertyBlock(hero.properties);
                }
                foreach(var material in hero.reveal)if(material!=null)Destroy(material);
            }
            heroes.Clear();
        }
        void PresentSummoning(float t)
        {
            heroTitle.color=new Color(1,.88f,.59f);
            bool waiting=mode==Shot.Waiting;
            var center=teamBounds.center;var side=Vector3.Cross(Vector3.up,forward);
            float span=Mathf.Max(10,teamBounds.size.magnitude*1.05f);
            float motion=ReducedMotion?0:Mathf.SmoothStep(0,1,t);
            var eye=center+forward*Mathf.Lerp(span*1.5f,span,motion)+side*Mathf.Lerp(span*.48f,-span*.22f,motion)+Vector3.up*Mathf.Lerp(7,2.7f,motion);
            var rotation=Quaternion.LookRotation(center-eye);
            float handoff=!waiting?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1,t)):0;
            var gameplay=coveredCameras.Select(c=>c.camera).FirstOrDefault(c=>c!=null&&c.CompareTag("MainCamera"));
            if(gameplay!=null){eye=Vector3.Lerp(eye,gameplay.transform.position,handoff);rotation=Quaternion.Slerp(rotation,gameplay.transform.rotation,handoff);}
            filmCamera.transform.SetPositionAndRotation(eye,rotation);filmCamera.fieldOfView=Mathf.Lerp(52,gameplay!=null?gameplay.fieldOfView:60,handoff);
            if(waiting)fx.Hide();else fx.ShowSummon(feet,t,new Color(.10f,.92f,.78f,1));
            title.text="THE OPEN ARCHIVE";subtitle.text=waiting?"GATHERING YOUR TEAM...":t<.68f?"THE ARCHIVE HAS SUMMONED YOUR TEAM":"STAY TOGETHER  ·  RESTORE ALL 10 TOPICS";
            heroTitle.text=waiting?"TEAM PREPARATION":t>.73f?"BEGIN":"";
            heroTitle.alpha=waiting?.8f:Mathf.Sin(Mathf.InverseLerp(.73f,1,t)*Mathf.PI);
            accent.color=new Color(.15f,.9f,.77f,.9f);
            fade.color=new Color(.005f,.025f,.03f,waiting?0:Mathf.Clamp01(1-t/.065f));
        }
        void PresentVictory(float t)
        {
            heroTitle.color=new Color(1,.88f,.59f);
            var portal=session.exitPortal;
            var center=portal!=null?portal.Center:teamBounds.center;
            var inward=-new Vector3(center.x,0,center.z).normalized;if(inward.sqrMagnitude<.1f)inward=Vector3.back;
            var side=Vector3.Cross(Vector3.up,inward);
            float motion=ReducedMotion?.5f:Mathf.SmoothStep(0,1,t);
            float distance=Mathf.Max(35,(portal!=null?portal.height:26)*1.55f);
            var eye=center+inward*Mathf.Lerp(distance*1.12f,distance*.80f,motion)+side*Mathf.Lerp(distance*.27f,-distance*.13f,motion)+Vector3.up*Mathf.Lerp(6,1.5f,motion);
            filmCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(center-eye));filmCamera.fieldOfView=53;
            fx.ShowVictory(portal!=null?portal.transform.position:center,portal!=null?portal.transform.rotation:Quaternion.identity,t,new Color(1,.70f,.18f,1));
            title.text="THE OPEN ARCHIVE RESTORED";
            subtitle.text=preview==Shot.Victory?"CINEMATIC PREVIEW":"EVERY SURVIVING EXPLORER ESCAPED  ·  10 / 10 TOPICS COMPLETE";
            heroTitle.text="VICTORY";heroTitle.alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.34f,.57f,t));
            accent.color=new Color(1,.72f,.26f,1);
            float opening=Mathf.Clamp01(1-t/.09f),closing=Mathf.SmoothStep(0,.93f,Mathf.InverseLerp(.88f,1,t));
            fade.color=new Color(.008f,.035f,.045f,Mathf.Max(opening,closing));
        }
        void PresentGuardian(float t,Bounds bounds)
        {
            fx.Hide();
            var heading=session.hunting.transform.forward;heading.y=0;heading.Normalize();
            float turn=ReducedMotion?0:Mathf.Lerp(-16,12,Mathf.SmoothStep(0,1,t));
            var direction=Quaternion.AngleAxis(turn,Vector3.up)*heading;
            float halfAngle=26*Mathf.Deg2Rad,aspect=Mathf.Max(.6f,filmCamera.aspect);
            float fit=Mathf.Max(bounds.extents.y/Mathf.Tan(halfAngle),bounds.extents.x/(Mathf.Tan(halfAngle)*aspect));
            float distance=Mathf.Max(12,fit*1.35f)+bounds.size.z*.30f;
            var target=bounds.center+Vector3.up*(bounds.size.y*.05f);
            var eye=target+direction*distance+Vector3.up*Mathf.Max(1.2f,bounds.size.y*.16f);
            filmCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));filmCamera.fieldOfView=52;
            title.text="GUARDIAN OF THE OPEN ARCHIVE";
            subtitle.text=guardianIntro.SpeciesTitle+"  ·  YOUR MISTAKES WILL AWAKEN IT";
            heroTitle.text=guardianIntro.SpeciesName.ToUpperInvariant();
            heroTitle.color=Color.Lerp(guardianIntro.Tint,Color.white,.45f);
            heroTitle.alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.30f,t));accent.color=guardianIntro.Tint;
            fade.color=new Color(.008f,.015f,.024f,Mathf.Max(Mathf.Clamp01(1-t/.10f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.87f,1,t))));
            if(!guardianRoarPlayed&&t>=.38f)
            {
                guardianRoarPlayed=true;
                if(t<.67f&&guardianIntro.Roar!=null){impact.clip=guardianIntro.Roar;impact.pitch=guardianIntro.SpeciesName=="Rajang"?.84f:1;impact.volume=.28f;impact.Play();}
            }
            if(guardianRoarPlayed)impact.volume=.28f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.80f,1,t)));
        }
        void EnsurePresentation()
        {
            if(frame!=null)return;
            var go=new GameObject("Archive_Match_Film",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=11000;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            frame=Rect("Fullscreen_Film",go.transform,Vector2.zero,Vector2.one);frame.gameObject.AddComponent<Image>().color=Color.black;
            film=Rect("World_Film",frame,Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>();film.raycastTarget=false;
            fade=Rect("Gentle_Fade",frame,Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();fade.raycastTarget=false;
            foreach(var range in new[]{new Vector4(0,0,1,.12f),new Vector4(0,.88f,1,1)})
            {var bar=Rect("Letterbox",frame,new Vector2(range.x,range.y),new Vector2(range.z,range.w)).gameObject.AddComponent<Image>();bar.color=new Color(.005f,.02f,.026f,.98f);bar.raycastTarget=false;}
            safeRoot=Rect("Safe_Film_Titles",frame,Vector2.zero,Vector2.one);
            title=Label("Film_Title",safeRoot,new Vector2(.07f,.895f),new Vector2(.93f,.985f),36,StageUITheme.HeadingFont);
            subtitle=Label("Film_Subtitle",safeRoot,new Vector2(.07f,.02f),new Vector2(.93f,.10f),26,StageUITheme.ReadingFont);
            heroTitle=Label("Hero_Reveal",safeRoot,new Vector2(.10f,.19f),new Vector2(.90f,.34f),68,StageUITheme.HeadingFont);
            heroTitle.color=new Color(1,.88f,.59f);
            accent=Rect("Film_Accent",safeRoot,new Vector2(.27f,.877f),new Vector2(.73f,.881f)).gameObject.AddComponent<Image>();accent.raycastTarget=false;
            var cameraObject=new GameObject("Archive_Match_Film_Camera");cameraObject.transform.SetParent(transform,false);
            filmCamera=cameraObject.AddComponent<Camera>();filmCamera.enabled=false;filmCamera.nearClipPlane=.12f;filmCamera.farClipPlane=900;
            filmCamera.allowHDR=false;filmCamera.allowMSAA=false;filmCamera.GetUniversalAdditionalCameraData().renderShadows=false;
            fx=new ArchiveMatchCinematicFx(transform,energyMaterial);
            guardianIntro=new ArchiveActiveMonsterIntro(transform);
            rise=Voice("Summoning_And_Victory_Rise",.26f);impact=Voice("Summoning_And_Victory_Accent",.34f);
            frame.gameObject.SetActive(false);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;}
        static TMP_Text Label(string name,Transform parent,Vector2 min,Vector2 max,float size,TMP_FontAsset font)
        {
            var label=Rect(name,parent,min,max).gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.fontSize=size;
            label.enableAutoSizing=true;label.fontSizeMin=size*.72f;label.fontSizeMax=size;label.alignment=TextAlignmentOptions.Center;
            label.textWrappingMode=TextWrappingModes.NoWrap;label.richText=false;label.color=new Color(1,.94f,.78f);label.raycastTarget=false;return label;
        }
        AudioSource Voice(string name,float volume)
        {var go=new GameObject(name);go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.volume=volume;source.priority=100;return source;}
        void UpdateSound(float t)
        {
            if(mode==Shot.Waiting)return;
            rise.volume=.26f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.84f,1,t)));
            impact.volume=.34f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.88f,1,t)));
            if(!risePlayed){risePlayed=true;if(t<.18f){rise.clip=mode==Shot.Victory?victoryRise:summoningRise;if(rise.clip!=null)rise.Play();}}
            float impactAt=mode==Shot.Victory?.38f:.54f;
            if(!impactPlayed&&t>=impactAt){impactPlayed=true;if(t<impactAt+.20f){impact.clip=mode==Shot.Victory?victoryImpact:summoningArrival;if(impact.clip!=null)impact.Play();}}
        }
        void EnsureTexture()
        {
            float aspect=(float)Mathf.Max(1,Screen.width)/Mathf.Max(1,Screen.height);
            int width=Mathf.Clamp(Screen.width,320,1280),height=Mathf.Clamp(Mathf.RoundToInt(width/aspect),180,1280);
            if(texture!=null&&texture.width==width&&texture.height==height)return;
            if(texture!=null){filmCamera.targetTexture=null;texture.Release();Destroy(texture);}
            texture=new RenderTexture(width,height,16){name="Archive_Match_Film_Texture",antiAliasing=1};texture.Create();film.texture=texture;
            filmCamera.targetTexture=texture;filmCamera.aspect=aspect;
        }
        void CoverGameplayRendering()
        {
            foreach(var camera in Camera.allCameras)
            {
                if(camera==filmCamera||camera.targetTexture!=null||camera.gameObject.scene!=gameObject.scene||coveredCameras.Any(c=>c.camera==camera))continue;
                coveredCameras.Add(new CoveredCamera{camera=camera,mask=camera.cullingMask,clear=camera.clearFlags,color=camera.backgroundColor});
                camera.cullingMask=0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            }
        }
        void Finish()
        {
            if(mode==Shot.None&&heroes.Count==0&&locks.Count==0&&coveredCameras.Count==0&&nameplates.Count==0)return;
            RestoreHeroes();
            foreach(var entry in nameplates)if(entry.Key!=null)entry.Key.enabled=entry.Value;nameplates.Clear();
            foreach(var player in locks.Keys)if(player!=null)player.ReleaseMovementLock(this);locks.Clear();
            foreach(var state in coveredCameras)if(state.camera!=null){state.camera.cullingMask=state.mask;state.camera.clearFlags=state.clear;state.camera.backgroundColor=state.color;}
            coveredCameras.Clear();fx?.Hide();
            guardianIntro?.Hide();
            if(frame!=null)frame.gameObject.SetActive(false);if(filmCamera!=null)filmCamera.enabled=false;
            if(rise!=null)rise.Stop();if(impact!=null)impact.Stop();mode=Shot.None;
        }
        [ContextMenu("Preview Summoning (Play Mode, Offline)")]
        public void PreviewSummoning()=>Preview(Shot.Summoning);
        [ContextMenu("Preview Victory (Play Mode, Offline)")]
        public void PreviewVictory()=>Preview(Shot.Victory);
        void Preview(Shot shot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!Application.isPlaying||PhotonNetwork.InRoom||session==null||!session.Ready||session.NightEntranceActive||Player.LocalInstance!=null&&Player.LocalInstance.IsMovementLocked)return;
            Finish();preview=shot;previewStarted=ArchiveTopicSession.Clock;
#endif
        }
        [ContextMenu("Cancel Cinematic Preview")]
        public void CancelPreview(){if(preview==Shot.None)return;preview=Shot.None;Finish();}
        void OnApplicationPause(bool paused){if(paused)Finish();}
        void OnDisable(){preview=Shot.None;Finish();}
        void OnDestroy()
        {
            Finish();fx?.Dispose();guardianIntro?.Dispose();if(texture!=null){texture.Release();Destroy(texture);}
            if(filmCamera!=null)Destroy(filmCamera.gameObject);if(canvas!=null)Destroy(canvas.gameObject);
            if(rise!=null)Destroy(rise.gameObject);if(impact!=null)Destroy(impact.gameObject);if(Current==this)Current=null;
        }
    }
}
