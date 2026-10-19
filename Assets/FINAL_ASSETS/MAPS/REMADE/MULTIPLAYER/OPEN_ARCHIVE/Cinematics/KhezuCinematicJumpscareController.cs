using System;
using System.Collections.Generic;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Cinematics
{
    /// <summary>Local presentation only. The network player and world monster are never moved.</summary>
    [DefaultExecutionOrder(200), DisallowMultipleComponent]
    public sealed class KhezuCinematicJumpscareController : MonoBehaviour
    {
        // Shared beat mapping keeps the host, visual copy, camera and audio on the same fast attack.
        public const float PlaybackSpeed = 1.75f;
        const float AuthoredDuration = 3.5f, AuthoredLungeMarker = 1.38f, AuthoredBarrierMarker = 2.8f;
        public const float Duration = 2, CaptureEnd = .06f, LungeMarker = .16f, ImpactMarker = .5f, BarrierMarker = 1.6f;
        public static float TimelineTime(float seconds)
        {
            if (seconds < CaptureEnd) return Remap(seconds, 0, CaptureEnd, 0, 1.2f);
            if (seconds < LungeMarker) return Remap(seconds, CaptureEnd, LungeMarker, 1.2f, AuthoredLungeMarker);
            if (seconds < ImpactMarker) return Remap(seconds, LungeMarker, ImpactMarker, AuthoredLungeMarker, 2);
            if (seconds < BarrierMarker) return Remap(seconds, ImpactMarker, BarrierMarker, 2, AuthoredBarrierMarker);
            return Remap(seconds, BarrierMarker, Duration, AuthoredBarrierMarker, AuthoredDuration);
        }
        public static float RealTime(float timelineSeconds)
        {
            if (timelineSeconds < 1.2f) return Remap(timelineSeconds, 0, 1.2f, 0, CaptureEnd);
            if (timelineSeconds < AuthoredLungeMarker) return Remap(timelineSeconds, 1.2f, AuthoredLungeMarker, CaptureEnd, LungeMarker);
            if (timelineSeconds < 2) return Remap(timelineSeconds, AuthoredLungeMarker, 2, LungeMarker, ImpactMarker);
            if (timelineSeconds < AuthoredBarrierMarker) return Remap(timelineSeconds, 2, AuthoredBarrierMarker, ImpactMarker, BarrierMarker);
            return Remap(timelineSeconds, AuthoredBarrierMarker, AuthoredDuration, BarrierMarker, Duration);
        }
        static float Remap(float value, float from, float to, float start, float end)
        { return Mathf.Lerp(start, end, Mathf.InverseLerp(from, to, value)); }
        public static float WorldClipTime(int phase, float seconds)
        {
            float t = TimelineTime(seconds);
            return phase == 1 ? Mathf.Min(t, 1.2f) : phase == 2 ? JumpscareTime(t - 1.2f) : Mathf.Max(0, t - AuthoredBarrierMarker);
        }
        public static float WorldClipSpeed(int phase, float seconds)
        {
            return Mathf.Max(.01f, (WorldClipTime(phase, seconds + .001f) - WorldClipTime(phase, seconds)) / .001f);
        }
        [Header("Existing model and clips")]
        public GameObject presentationPrefab;
        public AnimationClip capture, jumpscare, barrierCast;
        [Tooltip("Face anchors on the existing presentation rig. Defaults preserve Khezu framing.")]
        public string mouthUpperBone = "Mouth_Upper", mouthLowerBone = "Mouth_Lower";
        public Vector3 faceTargetOffset;
        [Header("Accessibility")]
        public bool reducedMotion;
        [Range(0, 1)] public float shakeIntensity = .85f;
        [Tooltip("Maximum local camera translation in meters during an impact.")]
        [Range(0, .15f)] public float shakeTranslation = .09f;
        [Tooltip("Maximum angular recoil in degrees. Reduced Motion disables all shake.")]
        [Range(0, 4)] public float shakeRotationDegrees = 2.4f;
        [Range(.5f, 1.5f)] public float framingDistance = 1;
        [Header("Optional sound assets (missing clips remain silent)")]
        public AudioClip captureImpact, lowBreathing, tenseGrowl, monsterRoar, impactAccent, barrierActivation;
        public AudioMixerGroup audioMixer;
        [Range(0, .8f)] public float masterVolume = .6f;
        [Range(0, 1)] public float captureLevel = .65f, breathingLevel = .3f, growlLevel = .4f,
            roarLevel = .85f, accentLevel = .4f, barrierLevel = .55f;
        [Header("Local timeline notifications")]
        public UnityEvent onRoarMarker = new UnityEvent();
        public UnityEvent onPresentationFinished = new UnityEvent();
        public bool IsPlaying { get; private set; }
        public float Elapsed { get; private set; }
        public int Starts { get; private set; }
        public int RoarMarkers { get; private set; }
        public int ActiveCaptureSerial { get; private set; }
        public Camera PresentationCamera { get { return output; } }
        public Transform MouthUpper { get { return upper; } }
        public Transform MouthLower { get { return lower; } }
        public GameObject PresentationInstance { get { return model; } }

        const int PresentationLayer = 31;
        const OutputChannels PresentationChannel = OutputChannels.Channel15;
        readonly List<Behaviour> suspended = new List<Behaviour>();
        readonly Dictionary<CinemachineBrain, OutputChannels> channels = new Dictionary<CinemachineBrain, OutputChannels>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        GameObject stage, model, overlay;
        Transform upper, lower;
        Camera output;
        CinemachineCamera virtualCamera;
        CinemachineBrain brain;
        RawImage picture;
        Image fade;
        VignetteGraphic vignette;
        RenderTexture texture;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] motions;
        global::Player player;
        GameInput input;
        bool inputWasBlocked, roomAtStart, roarSent, captureSent, breathingSent, growlSent, accentSent, barrierSent;
        int lastSerial, actorAtStart;
        double started;
        int screenWidth, screenHeight;
        Vector3 restMouthPosition;
        readonly Dictionary<AudioSource, float> voiceFadeEnds = new Dictionary<AudioSource, float>();

        public bool BeginConfirmedCapture(int actor, int serial, double startTime)
        {
            var h = GetComponent<KhezuHuntingController>();
            if (h == null || h.State != KhezuHuntState.Caught || h.CaughtPlayerId != actor ||
                h.CaptureSerial != serial || !KhezuCaptureEncounterController.IsCaughtLocalPlayer(actor) || serial <= lastSerial) return false;
            return Begin(actor, serial, startTime);
        }
        // Explicit offline preview: no capture, barrier, damage, or network state is fabricated.
        public void PreviewLocal()
        {
            if (PhotonNetwork.InRoom) return;
            Begin(-1, 0, KhezuHuntingController.EncounterClock);
        }
        bool Begin(int actor, int serial, double startTime)
        {
            if (IsPlaying || !enabled || presentationPrefab == null || capture == null || jumpscare == null || barrierCast == null) return false;
            if (KhezuHuntingController.EncounterClock - startTime >= Duration) return false;
            player = global::Player.LocalInstance;
            if (player == null || Camera.main == null) return false;
            lastSerial = Mathf.Max(lastSerial, serial); actorAtStart = actor;
            ActiveCaptureSerial = serial;
            roomAtStart = PhotonNetwork.InRoom; started = startTime;
            input = GameInput.Instance; inputWasBlocked = input != null && input.GameplayInputBlocked;
            try
            {
                player.AcquireMovementLock(this,"Cinematic POV",player.transform.position,90);
                foreach (var look in player.GetComponentsInChildren<CameraTargetController>(true)) Suspend(look);
                foreach (var look in FindObjectsByType<MobileLookInput>(FindObjectsSortMode.None)) Suspend(look);
                foreach (var zoom in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None)) Suspend(zoom);
                foreach (var axis in FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None)) Suspend(axis);
                foreach (var explore in FindObjectsByType<LogicLegends.OpenArchiveTesting.OpenArchiveExplorationSession>(FindObjectsSortMode.None)) Suspend(explore);
                // Pause cannot open over a cinematic and later restore a stale input lock.
                foreach (var menu in FindObjectsByType<LevelMenu>(FindObjectsSortMode.None)) Suspend(menu);
                if (input != null) input.SetGameplayInputBlocked(true);
                MobileInputUI.FindJoystick()?.ResetJoystick(); MobileLookInput.ResetDelta();
                CreatePresentation();
                Elapsed = Mathf.Max(0, (float)(KhezuHuntingController.EncounterClock - started));
                // On a late packet, do not stack past impacts/roar onto one frame.
                captureSent = Elapsed > .1f; breathingSent = Elapsed > RealTime(.5f) + .06f;
                growlSent = Elapsed > RealTime(.9f) + .05f; roarSent = Elapsed > LungeMarker + .1f;
                accentSent = Elapsed > ImpactMarker + .12f; barrierSent = Elapsed > BarrierMarker + .12f;
                IsPlaying = true; Starts++;
                EvaluatePresentation(Elapsed);
                AdvanceAudio(Elapsed);
                return true;
            }
            catch (Exception ex) { Cancel(); Debug.LogException(ex, this); return false; }
        }
        void Suspend(Behaviour item)
        {
            if (item == null || !item.enabled || suspended.Contains(item)) return;
            suspended.Add(item); item.enabled = false;
        }
        void CreatePresentation()
        {
            stage = new GameObject("Khezu_Local_Cinematic_Presentation");
            // Isolated local staging space; no PhotonView, colliders, or gameplay scripts.
            stage.transform.position = new Vector3(0, -10000, 0);
            model = Instantiate(presentationPrefab, stage.transform);
            model.name = "Existing_Khezu_Visual_Only";
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = PresentationLayer;
                if (t.name == mouthUpperBone) upper = t;
                if (t.name == mouthLowerBone) lower = t;
            }
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = null; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Khezu_Existing_Clips_Local"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            motions = new AnimationClipPlayable[3];
            var clips = new[] { capture, jumpscare, barrierCast };
            for (int i = 0; i < 3; i++)
            {
                motions[i] = AnimationClipPlayable.Create(graph, clips[i]); motions[i].SetSpeed(0);
                motions[i].SetApplyFootIK(false); motions[i].SetApplyPlayableIK(false);
                graph.Connect(motions[i], 0, mixer, i);
            }
            AnimationPlayableOutput.Create(graph, "Existing Khezu Rig", animator).SetSourcePlayable(mixer);
            graph.Play();
            mixer.SetInputWeight(0, 1); motions[0].SetTime(0); graph.Evaluate(0);
            restMouthPosition = upper != null && lower != null ? stage.transform.InverseTransformPoint(FacePosition()) : new Vector3(0, 2.07f, 2.09f);
            foreach (var existing in FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None))
            { channels[existing] = existing.ChannelMask; existing.ChannelMask &= ~PresentationChannel; }
            var camObject = new GameObject("Khezu_Local_POV_Output"); camObject.transform.SetParent(stage.transform, false);
            output = camObject.AddComponent<Camera>(); output.clearFlags = CameraClearFlags.SolidColor;
            output.backgroundColor = new Color(.014f, .009f, .016f); output.cullingMask = 1 << PresentationLayer;
            output.nearClipPlane = .04f; output.farClipPlane = 40; output.allowHDR = false; output.allowMSAA = false;
            output.useOcclusionCulling = false;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            screenWidth = Screen.width; screenHeight = Screen.height;
            int height = Mathf.Clamp(Screen.height, 360, 720), width = Mathf.Min(1280, Mathf.RoundToInt(height * aspect));
            height = Mathf.RoundToInt(width / aspect);
            texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { name = "Khezu_Local_POV_720p_Cap" };
            texture.Create(); output.targetTexture = texture;
            brain = camObject.AddComponent<CinemachineBrain>(); brain.ChannelMask = PresentationChannel;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate; brain.IgnoreTimeScale = true;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
            var vc = new GameObject("Khezu_Authored_POV_Cinemachine"); vc.transform.SetParent(stage.transform, false);
            virtualCamera = vc.AddComponent<CinemachineCamera>(); virtualCamera.OutputChannel = PresentationChannel;
            virtualCamera.Lens.FieldOfView = 54; virtualCamera.Lens.NearClipPlane = .04f; virtualCamera.Lens.FarClipPlane = 40;
            AddLight("Warm_Face_Key", new Vector3(-2, 4, 6), new Color(1, .67f, .52f), 2.2f);
            AddLight("Cool_Rim", new Vector3(3, 3, -1), new Color(.35f, .53f, 1), 1.8f);
            AddLight("Maw_Fill", new Vector3(0, 1, 6), new Color(1, .16f, .12f), .8f);
            overlay = new GameObject("Khezu_Local_Cinematic_Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
            var scaler = overlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            picture = FullRect<RawImage>("First_Person_Presentation", overlay.transform); picture.texture = texture; picture.raycastTarget = true;
            vignette = FullRect<VignetteGraphic>("Lightweight_Edge_Vignette", overlay.transform); vignette.raycastTarget = false;
            fade = FullRect<Image>("Transition_Fade", overlay.transform); fade.raycastTarget = false; fade.color = Color.clear;
        }
        static T FullRect<T>(string name, Transform parent) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            return go.AddComponent<T>();
        }
        void AddLight(string name, Vector3 position, Color color, float intensity)
        {
            var go = new GameObject(name); go.transform.SetParent(stage.transform, false); go.transform.localPosition = position;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.range = 12; l.color = color; l.intensity = intensity;
            l.shadows = LightShadows.None; l.cullingMask = 1 << PresentationLayer;
        }
        void LateUpdate()
        {
            if (!IsPlaying) return;
            if (player == null || !player.gameObject.activeInHierarchy || (roomAtStart && (!PhotonNetwork.InRoom || actorAtStart != PhotonNetwork.LocalPlayer.ActorNumber))) { Cancel(); return; }
            Elapsed = Mathf.Max(0, (float)(KhezuHuntingController.EncounterClock - started));
            if (Elapsed >= Duration) { Cancel(); onPresentationFinished.Invoke(); return; }
            // Own only the short local presentation interval. Shared player code is unchanged.
            if (input != null) input.SetGameplayInputBlocked(true);
            player.EnforceMovementLock();
            if (screenWidth != Screen.width || screenHeight != Screen.height) ResizeForScreen();
            EvaluatePresentation(Elapsed); AdvanceAudio(Elapsed);
        }
        void ResizeForScreen()
        {
            screenWidth = Screen.width; screenHeight = Screen.height;
            float aspect = (float)screenWidth / Mathf.Max(1, screenHeight);
            int height = Mathf.Clamp(screenHeight, 360, 720), width = Mathf.Min(1280, Mathf.RoundToInt(height * aspect));
            output.targetTexture = null; texture.Release(); texture.width = Mathf.Max(1, width);
            texture.height = Mathf.Max(1, Mathf.RoundToInt(width / aspect)); texture.Create(); output.targetTexture = texture;
        }
        public void EvaluatePresentation(float elapsed)
        {
            if (model == null || !graph.IsValid()) return;
            float t = TimelineTime(elapsed);
            int phase = t < 1.2f ? 0 : t < AuthoredBarrierMarker ? 1 : 2;
            float local = phase == 0 ? t : phase == 1 ? t - 1.2f : t - AuthoredBarrierMarker;
            for (int i = 0; i < 3; i++) mixer.SetInputWeight(i, 0);
            motions[phase].SetTime(phase == 1 ? JumpscareTime(local) : local); mixer.SetInputWeight(phase, 1);
            if (phase > 0 && local < .14f)
            {
                float blend = Mathf.SmoothStep(0, 1, local / .14f);
                motions[phase - 1].SetTime(phase == 1 ? 1.2 + local : JumpscareTime(1.6f + local));
                mixer.SetInputWeight(phase - 1, 1 - blend); mixer.SetInputWeight(phase, blend);
            }
            graph.Evaluate(0);
            // Move only the local presentation model. The authoritative monster stays where it caught the player.
            float thrust = elapsed < CaptureEnd ? 0 : elapsed < LungeMarker ? 1 - Mathf.Pow(1 - Mathf.InverseLerp(CaptureEnd, LungeMarker, elapsed), 3) :
                elapsed < .24f ? 1 : 1 - Smooth((elapsed - .24f) / .26f);
            float pullback = Smooth((elapsed - BarrierMarker) / .3f);
            model.transform.localPosition = reducedMotion ? Vector3.zero : new Vector3(0, 0, .42f * thrust - .25f * pullback);
            Vector3 face = FacePosition();
            // Fit the face to the narrower screen dimension, including portrait Android.
            float aspect = texture == null ? 1.777f : (float)texture.width / texture.height;
            float fit = Mathf.Max(1, 1 / aspect) * Mathf.Max(.85f, framingDistance);
            // Anchor camera depth rather than following the head forward and cancelling its lunge.
            float recoilDistance = elapsed < LungeMarker ? 0 : elapsed < .24f ? .10f * Smooth((elapsed - LungeMarker) / .08f) : .10f * (1 - Smooth((elapsed - .24f) / .26f));
            float cameraZ = stage.transform.position.z + restMouthPosition.z + 3.15f + 2.3f * (fit - 1) + recoilDistance + pullback * 1.2f;
            if (reducedMotion) cameraZ = face.z + 2.85f * fit + pullback * 1.2f;
            Vector3 shake, recoil;
            ImpactMotion(elapsed, out shake, out recoil);
            Vector3 cameraPosition = new Vector3(face.x, face.y + (reducedMotion ? -.12f : -.28f), cameraZ) + shake;
            Quaternion cameraRotation = Quaternion.LookRotation(face + Vector3.up * .05f - cameraPosition, Vector3.up) * Quaternion.Euler(recoil);
            virtualCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            virtualCamera.ForceCameraPosition(cameraPosition, cameraRotation); brain.ManualUpdate();
            float alpha = Smooth(elapsed / (reducedMotion ? .12f : .035f)) * (1 - Smooth((t - 3.15f) / .35f));
            picture.color = new Color(1, 1, 1, alpha);
            vignette.color = new Color(.035f, 0, .012f, alpha * (t < 2 ? .28f : .4f));
            fade.color = new Color(0, 0, 0, 0);
        }
        Vector3 FacePosition()
        {
            return (upper != null && lower != null ? (upper.position + lower.position) * .5f : stage.transform.position + new Vector3(0, 2, 2.3f)) + model.transform.TransformVector(faceTargetOffset);
        }
        static float Smooth(float x) { return Mathf.SmoothStep(0, 1, Mathf.Clamp01(x)); }
        // Reach the original clip's wide-mouth section exactly at the camera/roar lunge.
        // This only retimes the visual copy; the source clip and authoritative rig are untouched.
        static float JumpscareTime(float local)
        {
            const float opening = AuthoredLungeMarker - 1.2f;
            return local < opening ? Mathf.Lerp(0, .5f, Mathf.Clamp01(local / opening)) :
                Mathf.Lerp(.5f, 1.3f, Mathf.Clamp01((local - opening) / (1.6f - opening)));
        }
        void ImpactMotion(float elapsed, out Vector3 translation, out Vector3 rotation)
        {
            translation = rotation = Vector3.zero;
            if (reducedMotion || shakeIntensity <= 0) return;
            // Short impulses use real seconds, so faster clip playback does not turn them into a buzz.
            AddImpact(elapsed - RealTime(.08f), .28f, .65f, ref translation, ref rotation);
            AddImpact(elapsed - LungeMarker, .38f, 1, ref translation, ref rotation);
            AddImpact(elapsed - ImpactMarker, .30f, .5f, ref translation, ref rotation);
            translation = Vector3.ClampMagnitude(translation, 1) * shakeTranslation * shakeIntensity;
            rotation = Vector3.ClampMagnitude(rotation, 1) * shakeRotationDegrees * shakeIntensity;
        }
        static void AddImpact(float age, float duration, float strength, ref Vector3 translation, ref Vector3 rotation)
        {
            if (age <= 0 || age >= duration) return;
            float envelope = strength * Mathf.Pow(1 - age / duration, 2) * Smooth(age / .018f);
            translation += new Vector3(Mathf.Sin(age * 71), Mathf.Sin(age * 57 + .7f), Mathf.Cos(age * 43) * .4f) * envelope;
            rotation += new Vector3(-Mathf.Cos(age * 38), Mathf.Sin(age * 47 + .3f) * .55f, Mathf.Sin(age * 34 + .6f) * .8f) * envelope;
        }
        void AdvanceAudio(float elapsed)
        {
            float t = TimelineTime(elapsed);
            Cue(ref captureSent, t, 0, captureImpact, captureLevel);
            Cue(ref breathingSent, t, .5f, lowBreathing, breathingLevel);
            Cue(ref growlSent, t, .9f, tenseGrowl, growlLevel);
            if (!roarSent && t >= AuthoredLungeMarker)
            {
                roarSent = true; RoarMarkers++;
                // Clear the preparation layers quickly so the early roar remains a distinct hit.
                foreach (var voice in voices) if (voice != null && (voice.clip == captureImpact || voice.clip == lowBreathing || voice.clip == tenseGrowl))
                    voiceFadeEnds[voice] = Time.unscaledTime + .025f;
                Play(monsterRoar, roarLevel); onRoarMarker.Invoke();
            }
            Cue(ref accentSent, t, 2, impactAccent, accentLevel);
            Cue(ref barrierSent, t, AuthoredBarrierMarker, barrierActivation, barrierLevel);
        }
        void Cue(ref bool sent, float t, float at, AudioClip clip, float volume)
        { if (!sent && t >= at) { sent = true; Play(clip, volume); } }
        void Play(AudioClip clip, float volume)
        {
            if (clip == null) return;
            var source = stage.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0;
            if (audioMixer != null) source.outputAudioMixerGroup = audioMixer;
            else AudioVolumeSettings.Route(source, GameAudioChannel.SoundFX);
            source.clip = clip; source.volume = 0;
            source.loop = false; voices.Add(source); voiceLevels[source] = Mathf.Clamp01(volume) * masterVolume; voiceTimes[source] = Time.unscaledTime;
            source.Play();
        }
        readonly Dictionary<AudioSource, float> voiceLevels = new Dictionary<AudioSource, float>(), voiceTimes = new Dictionary<AudioSource, float>();
        void Update()
        {
            // One bounded gain budget across overlapping cues, with a short attack and exit fade.
            float total = 0;
            foreach (var v in voices) if (v != null && v.isPlaying)
            {
                if (voiceFadeEnds.TryGetValue(v, out var end) && Time.unscaledTime >= end) { v.Stop(); v.volume = 0; continue; }
                total += voiceLevels[v] * (voiceFadeEnds.TryGetValue(v, out end) ? Mathf.Clamp01((end - Time.unscaledTime) / .025f) : 1);
            }
            float limiter = total > masterVolume ? masterVolume / total : 1;
            foreach (var v in voices) if (v != null)
                v.volume = voiceLevels[v] * limiter * (voiceFadeEnds.TryGetValue(v, out var fadeEnd) ? Mathf.Clamp01((fadeEnd - Time.unscaledTime) / .025f) : 1) *
                    Mathf.Clamp01((Time.unscaledTime - voiceTimes[v]) / .008f) * Mathf.Clamp01((Duration - Elapsed) / .08f);
        }
        public void Cancel()
        {
            bool hadSession = stage != null || suspended.Count > 0;
            IsPlaying = false;
            if (graph.IsValid()) graph.Destroy();
            if (output != null) { output.enabled = false; output.targetTexture = null; }
            if (overlay != null) { overlay.SetActive(false); Destroy(overlay); }
            if (stage != null) { stage.SetActive(false); Destroy(stage); }
            if (texture != null) { texture.Release(); Destroy(texture); }
            foreach (var entry in channels) if (entry.Key != null) entry.Key.ChannelMask = entry.Value;
            channels.Clear();
            foreach (var item in suspended) if (item != null) item.enabled = true;
            suspended.Clear();
            if(player!=null)player.ReleaseMovementLock(this);
            if (hadSession && input != null) { input.SetMobileMovement(Vector2.zero); input.SetGameplayInputBlocked(inputWasBlocked); }
            if (hadSession) { MobileLookInput.ResetDelta(); MobileInputUI.FindJoystick()?.ResetJoystick(); }
            voices.Clear(); voiceLevels.Clear(); voiceTimes.Clear();
            voiceFadeEnds.Clear();
            stage = model = overlay = null; upper = lower = null; output = null; brain = null; virtualCamera = null; texture = null; input = null; player = null;
        }
        void OnDisable() { Cancel(); }
        void OnDestroy() { Cancel(); }
        public string MissingAudio()
        {
            var names = new List<string>();
            if (captureImpact == null) names.Add("Capture Impact"); if (lowBreathing == null) names.Add("Low Breathing");
            if (tenseGrowl == null) names.Add("Tense Growl"); if (monsterRoar == null) names.Add("Monster Roar");
            if (impactAccent == null) names.Add("Impact Accent"); if (barrierActivation == null) names.Add("Barrier Activation");
            return string.Join(", ", names);
        }
    }

    /// <summary>32 transparent-center edge quads; no full-screen shader or post-processing volume.</summary>
    public sealed class VignetteGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect; Vector2 center = r.center;
            const int count = 32;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count, b = (i + 1) * Mathf.PI * 2 / count;
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), q = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Vector2 scale = new Vector2(r.width * .72f, r.height * .72f);
                int n = vh.currentVertCount;
                vh.AddVert(center + Vector2.Scale(p, scale) * .48f, new Color(color.r, color.g, color.b, 0), Vector2.zero);
                vh.AddVert(center + Vector2.Scale(p, scale), color, Vector2.zero);
                vh.AddVert(center + Vector2.Scale(q, scale), color, Vector2.zero);
                vh.AddVert(center + Vector2.Scale(q, scale) * .48f, new Color(color.r, color.g, color.b, 0), Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
            }
        }
    }
}
