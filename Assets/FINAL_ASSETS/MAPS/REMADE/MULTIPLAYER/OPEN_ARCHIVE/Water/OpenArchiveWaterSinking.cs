using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Water
{
    /// <summary>Timed water exposure. Sinks the existing visual while retaining riverbed collision and the original jump.</summary>
    [DefaultExecutionOrder(200), RequireComponent(typeof(OpenArchiveWater))]
    public sealed class OpenArchiveWaterSinking : MonoBehaviour
    {
        public Transform returnSpawn;
        [Min(1)] public float safeWaterSeconds = 5;
        [Min(1)] public float sinkingSeconds = 4;
        [Min(.1f)] public float dryRecoverySeconds = 5;
        [Min(0)] public float submergedHoldSeconds = .35f;
        [Min(8)] public float maximumShoreReturnDistance = 36;
        public bool showWarning = true;
        public int ForcedReturnCount { get; private set; }
        public event Action<Player> PlayerSubmerged;
        public float LocalWaterSeconds => Local != null ? Local.exposure : 0;
        public float LocalSinkProgress => Local != null ? Progress(Local) : 0;
        public float LocalVisualDepth => Local != null ? Local.offset : 0;
        public bool WarningVisible => panel != null && panel.gameObject.activeInHierarchy;
        sealed class Actor
        {
            public Player player; public CharacterController capsule; public Transform visual;
            public Renderer[] renderers; public Vector3 originalVisualPosition, previousPosition;
            public float exposure, drySeconds, offset, offsetVelocity, nextShoreRetry;
            public bool hasPosition, overWater, safeGround, active, hasDryBank;
            public Vector3 lastDryBank;
            public float surface;
        }
        OpenArchiveWater water;
        readonly List<Actor> actors = new List<Actor>();
        Actor Local => actors.Find(a => a.player != null && a.player == Player.LocalInstance);
        float nextScan;
        GameObject hud;
        RectTransform panel;
        Text warning;
        Image meter, background;
        void Awake() { water = GetComponent<OpenArchiveWater>(); }
        void Update()
        {
            if (Time.time >= nextScan) { Scan(); nextScan = Time.time + .5f; }
            foreach (var actor in actors)
            {
                if (actor.player == null) continue;
                var position = actor.player.transform.position;
                bool teleported = actor.hasPosition && Vector3.Distance(position, actor.previousPosition) > Mathf.Max(6, actor.player.moveSpeed * Time.deltaTime * 4);
                actor.previousPosition = position; actor.hasPosition = true;
                if (teleported) { ResetExposure(actor); RestoreVisual(actor); }
                actor.active = water.isActiveAndEnabled && actor.player.isActiveAndEnabled && !actor.player.IsMovementLocked && actor.capsule.enabled;
                if (!actor.active) { RestoreVisual(actor); continue; }
                var feet = OpenArchiveWater.Feet(actor.capsule);
                actor.overWater = water.SampleSurface(feet, out actor.surface);
                bool wet = actor.overWater && feet.y <= actor.surface + .12f;
                actor.safeGround = actor.capsule.isGrounded && !wet;
                if (Time.timeScale <= 0) continue;
                // Once wet, jumping over the same water does not refill the grace period.
                if (wet || (actor.exposure > 0 && actor.overWater && !actor.safeGround))
                {
                    actor.exposure += Time.deltaTime; actor.drySeconds = 0;
                    if (actor.player == Player.LocalInstance && Time.time>=actor.nextShoreRetry && actor.exposure >= safeWaterSeconds + sinkingSeconds + submergedHoldSeconds)
                        ReturnFromWater(actor);
                }
                else if (actor.safeGround)
                {
                    actor.lastDryBank = feet; actor.hasDryBank = true;
                    actor.drySeconds += Time.deltaTime;
                    if (actor.drySeconds >= dryRecoverySeconds) ResetExposure(actor);
                }
                else actor.drySeconds = 0;
            }
            UpdateWarning();
        }
        float Progress(Actor actor) => Mathf.Clamp01((actor.exposure - safeWaterSeconds) / Mathf.Max(1, sinkingSeconds));
        void Scan()
        {
            actors.RemoveAll(a => a.player == null);
            foreach (var player in FindObjectsByType<Player>(FindObjectsSortMode.None))
            {
                if (player.gameObject.scene != gameObject.scene || actors.Exists(a => a.player == player)) continue;
                var animator = player.GetComponentInChildren<PlayerAnimator>(true);
                var visual = animator != null && animator.transform != player.transform ? animator.transform : null;
                var capsule = player.GetComponent<CharacterController>();
                if (capsule == null) continue;
                actors.Add(new Actor { player = player, capsule = capsule, visual = visual,
                    originalVisualPosition = visual != null ? visual.localPosition : Vector3.zero,
                    renderers = visual != null ? visual.GetComponentsInChildren<Renderer>(true) : new Renderer[0] });
            }
        }
        void LateUpdate()
        {
            if (Time.deltaTime <= 0) return;
            foreach (var actor in actors)
            {
                if (actor.player == null || actor.visual == null) continue;
                if (!actor.active) { RestoreVisual(actor); continue; }
                float target = 0;
                if (actor.overWater && !actor.safeGround && actor.exposure > safeWaterSeconds)
                {
                    float originalHead = float.NegativeInfinity;
                    foreach (var renderer in actor.renderers)
                        if (renderer != null) originalHead = Mathf.Max(originalHead, renderer.bounds.max.y + actor.offset);
                    if (float.IsNegativeInfinity(originalHead)) originalHead = OpenArchiveWater.Feet(actor.capsule).y + actor.capsule.height;
                    float fullyBelow = Mathf.Clamp(originalHead - actor.surface + .3f, .75f, actor.capsule.height + 1);
                    target = fullyBelow * Mathf.SmoothStep(0, 1, Progress(actor));
                }
                actor.offset = Mathf.SmoothDamp(actor.offset, target, ref actor.offsetVelocity, .18f, Mathf.Infinity, Time.deltaTime);
                actor.visual.localPosition = actor.originalVisualPosition + actor.visual.parent.InverseTransformVector(Vector3.down * actor.offset);
            }
        }
        static void RestoreVisual(Actor actor)
        {
            if (actor.visual != null) actor.visual.localPosition = actor.originalVisualPosition;
            actor.offset = actor.offsetVelocity = 0;
        }
        static void ResetExposure(Actor actor)
        {
            actor.exposure = actor.drySeconds = 0;
        }
        void ReturnFromWater(Actor actor)
        {
            var player = actor.player; var capsule = actor.capsule;
            if (!TryFindShore(actor, out var position)) { actor.nextShoreRetry=Time.time+.5f; return; }
            Vector3 before = player.transform.position;
            // Recovery stays local to the river. Keep exhaustion until five seconds on land;
            // repeatedly diving back in cannot grant a fresh grace period or a map-wide escape.
            RestoreVisual(actor); actor.drySeconds = 0;
            if (GameInput.Instance != null) GameInput.Instance.SetMobileMovement(Vector2.zero);
            var joystick = MobileInputUI.FindJoystick(); if (joystick != null) joystick.ResetJoystick();
            MobileLookInput.ResetDelta();
            capsule.enabled = false; player.transform.position = position; capsule.enabled = true;
            player.ResetMotionAfterTeleport(); player.RemoveMovementSpeedModifier(water);
            TeleportCameraSync.ResetAfterMove(player.transform, player.transform.position - before);
            foreach (var camera in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None))
                if (camera.Follow != null && (camera.Follow == player.transform || camera.Follow.IsChildOf(player.transform)))
                    camera.PreviousStateIsValid = false;
            actor.previousPosition = player.transform.position; actor.overWater = false; actor.safeGround = true;
            ForcedReturnCount++; PlayerSubmerged?.Invoke(player);
        }
        readonly Collider[] shoreOverlaps = new Collider[32];
        readonly RaycastHit[] shoreHits = new RaycastHit[32];
        bool TryFindShore(Actor actor, out Vector3 rootPosition)
        {
            Vector3 feet = OpenArchiveWater.Feet(actor.capsule);
            float preferred = actor.hasDryBank ? Mathf.Atan2(actor.lastDryBank.x-feet.x, actor.lastDryBank.z-feet.z) : 0;
            for (float radius = 2; radius <= maximumShoreReturnDistance; radius += 2)
                for (int i = 0; i < 16; i++)
                {
                    float angle = preferred + i * Mathf.PI / 8;
                    var candidate = feet + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                    if (TryGroundShore(actor, candidate, out rootPosition)) return true;
                }
            if (actor.hasDryBank && Vector3.Distance(feet,actor.lastDryBank) <= maximumShoreReturnDistance &&
                TryGroundShore(actor, actor.lastDryBank, out rootPosition)) return true;
            rootPosition = feet; return false;
        }
        bool TryGroundShore(Actor actor, Vector3 candidate, out Vector3 rootPosition)
        {
            rootPosition = candidate;
            float scaleY = Mathf.Abs(actor.player.transform.lossyScale.y);
            float height = actor.capsule.height * scaleY;
            candidate.y = actor.surface + height + 3;
            int count = Physics.RaycastNonAlloc(candidate, Vector3.down, shoreHits, height + 6, ~(1<<7), QueryTriggerInteraction.Ignore);
            int nearest = -1; float distance = float.MaxValue;
            for (int i=0;i<count;i++)
            {
                var hit=shoreHits[i];
                if(hit.collider.GetComponentInParent<Player>()!=null || hit.distance>=distance)continue;
                nearest=i;distance=hit.distance;
            }
            if(nearest<0)return false;
            var floor=shoreHits[nearest];
            if(floor.normal.y < .7f || floor.point.y > actor.surface + height + 1 ||
                water.SampleSurface(floor.point,out float surface) && floor.point.y <= surface + .15f)return false;
            rootPosition = actor.player.transform.position + floor.point - OpenArchiveWater.Feet(actor.capsule) + Vector3.up * (actor.capsule.skinWidth * scaleY + .08f);
            float radius=actor.capsule.radius*Mathf.Max(Mathf.Abs(actor.player.transform.lossyScale.x),Mathf.Abs(actor.player.transform.lossyScale.z));
            Vector3 center=rootPosition+actor.player.transform.TransformVector(actor.capsule.center);
            float half=Mathf.Max(0,height*.5f-radius);
            count=Physics.OverlapCapsuleNonAlloc(center+Vector3.up*half,center-Vector3.up*half,radius,shoreOverlaps,~(1<<7),QueryTriggerInteraction.Ignore);
            if(count>=shoreOverlaps.Length)return false;
            for(int i=0;i<count;i++)if(!shoreOverlaps[i].transform.IsChildOf(actor.player.transform))return false;
            return true;
        }
        void CreateWarning()
        {
            hud = new GameObject("OpenArchive_Water_Warning", typeof(Canvas), typeof(CanvasScaler));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(hud, gameObject.scene);
            hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; hud.GetComponent<Canvas>().sortingOrder = 80;
            var scaler = hud.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var go = new GameObject("Warning",typeof(RectTransform),typeof(Image)); panel = go.GetComponent<RectTransform>(); panel.SetParent(hud.transform,false);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one; panel.anchoredPosition = new Vector2(-24,-160); panel.sizeDelta = new Vector2(330,100);
            background = go.GetComponent<Image>(); background.raycastTarget = false;
            var label = new GameObject("Message",typeof(RectTransform),typeof(Text)); label.transform.SetParent(panel,false);
            var rect = label.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(12,22); rect.offsetMax = new Vector2(-12,-7);
            warning = label.GetComponent<Text>(); warning.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); warning.fontSize = 23;
            warning.alignment = TextAnchor.MiddleCenter; warning.color = Color.white; warning.raycastTarget = false;
            var bar = new GameObject("Remaining_Time",typeof(RectTransform),typeof(Image)); bar.transform.SetParent(panel,false);
            rect = bar.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0,0); rect.anchorMax = new Vector2(1,0); rect.pivot = new Vector2(.5f,0); rect.offsetMin = new Vector2(12,8); rect.offsetMax = new Vector2(-12,16);
            meter = bar.GetComponent<Image>(); meter.raycastTarget = false;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(12,8); rect.sizeDelta = new Vector2(306,8);
        }
        void UpdateWarning()
        {
            var actor = Local;
            bool visible = showWarning && actor != null && actor.active && actor.exposure > 0 && Time.timeScale > 0;
            if (visible && hud == null) CreateWarning();
            if (panel == null) return;
            panel.gameObject.SetActive(visible); if (!visible) return;
            float progress = Progress(actor);
            background.color = progress > 0 ? new Color(.28f,.045f,.025f,.91f) : new Color(.025f,.10f,.14f,.9f);
            meter.color = progress > 0 ? new Color(1,.3f,.13f) : new Color(.95f,.78f,.2f);
            meter.rectTransform.sizeDelta = new Vector2(306 * (1 - Mathf.Clamp01(actor.exposure / (safeWaterSeconds + sinkingSeconds))), 8);
            if (actor.safeGround) warning.text = "Catching your breath...\n" + Mathf.CeilToInt(Mathf.Max(0,dryRecoverySeconds-actor.drySeconds)) + "s on land to recover";
            else if (progress > 0) warning.text = "SINKING! " + Mathf.CeilToInt(Mathf.Max(0,safeWaterSeconds+sinkingSeconds-actor.exposure)) + "s to get out\nReach the bank and jump!";
            else warning.text = "WATER: " + Mathf.CeilToInt(Mathf.Max(0,safeWaterSeconds-actor.exposure)) + "s before sinking\nHead back to shore.";
        }
        void OnDisable()
        {
            foreach (var actor in actors) { RestoreVisual(actor); ResetExposure(actor); actor.hasPosition = false; }
            if (hud != null) Destroy(hud); hud = null; panel = null;
        }
    }
}
