using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Draws checkpointed supply equipment. Counts, selection and use remain owned by ArchiveTopicSession.</summary>
    [DefaultExecutionOrder(10040), DisallowMultipleComponent]
    public sealed class ArchiveEquippedItems : MonoBehaviour
    {
        public ArchiveTopicSession session;
        public ArchiveRewardView view;
        [Tooltip("Grip target for the existing 5.5-unit explorer. Scaled to the current capsule height.")]
        public Vector3 explorerGripPoint = new Vector3(.65f, 2.75f, 1.1f);
        [Header("Local dart gun framing")]
        [Tooltip("Moves the existing follow camera beside the avatar so the reticle and dart flight remain visible.")]
        public Vector3 gunShoulderOffset = new Vector3(1.6f, .25f, 0);
        [Min(1)] public float gunShoulderBlendSpeed = 10;

        sealed class Equipment
        {
            public Player player;
            public GameObject model;
            public GameObject source;
            public int item = -1;
            public Transform shoulder, elbow, wrist;
            public Quaternion shoulderLocal, elbowLocal, wristLocal;
            public bool posed;
            public bool gripCalibrated;
            public Quaternion wristToItem;
        }

        readonly Dictionary<int, Equipment> equipment = new Dictionary<int, Equipment>();
        readonly HashSet<int> observedActors = new HashSet<int>();
        CinemachineCamera gunCamera;
        CinemachineThirdPersonFollow gunFollow;
        Transform gunCameraTarget;
        Vector3 originalShoulderOffset, appliedShoulderOffset;
        float originalCameraSide, appliedCameraSide;
        public bool GunShoulderActive => gunFollow != null;
        public CinemachineCamera GunShoulderCamera => gunCamera;
        public int VisibleModelCount => equipment.Values.Count(e => e.model != null && e.model.activeInHierarchy);
        public GameObject EquippedModel(int actor) => equipment.TryGetValue(actor, out var e) &&
            e.model != null && e.model.activeInHierarchy ? e.model : null;
        public int VisibleItem(int actor) => equipment.TryGetValue(actor, out var e) &&
            e.model != null && e.model.activeInHierarchy ? e.item : -1;

        void Awake()
        {
            if (session == null) session = GetComponent<ArchiveTopicSession>();
            if (view == null) view = GetComponent<ArchiveRewardView>();
        }

        void LateUpdate()
        {
            if (session == null || !session.Ready || view == null || session.State.survival == null)
            { HideAll(); return; }
            observedActors.Clear();
            Player localGunOwner = null;
            foreach (var inventory in session.State.survival.players)
            {
                int actor = inventory.actor;
                observedActors.Add(actor);
                var player = session.ActorPlayer(actor);
                if (equipment.TryGetValue(actor, out var e) && (player == null || e.player != player))
                { Dispose(e); equipment.Remove(actor); e = null; }
                if (player == null) continue;
                if (e == null) { e = new Equipment { player = player }; FindArm(e); equipment.Add(actor, e); }

                int item = inventory.equippedItem;
                bool academicProp = session.State.stations != null &&
                    session.State.stations.Any(s => s.carries != null && s.carries.Any(c => c.actor == actor));
                bool reading = actor == session.LocalActor && ArchiveTopicUI.Current != null &&
                    ArchiveTopicUI.Current.ReadingStation != null;
                bool available = item >= 0 && item < 6 && inventory.items != null && inventory.items.Length > item &&
                    inventory.items[item] > 0 && player.gameObject.activeInHierarchy && session.CanAct(actor) &&
                    session.Downed(actor) == null && !session.NightEntranceActive && !academicProp && !reading;
                var prefab = available && view.itemPrefabs != null && item < view.itemPrefabs.Length ? view.itemPrefabs[item] : null;
                if (!available || prefab == null) { Hide(e); continue; }
                if (e.model == null || e.item != item || e.source != prefab)
                {
                    RestoreArm(e);
                    if (e.model != null) { e.model.SetActive(false); Destroy(e.model); }
                    // Pure render prefabs never use the solo GrabbableObject or Player.HeldObject channel.
                    e.model = Instantiate(prefab, transform);
                    e.model.name = "Actor_" + actor + "_Equipped_" + ((ArchiveSurvivalItem)item);
                    e.source = prefab; e.item = item;
                    RemoveInteractionComponents(e.model);
                }
                e.model.SetActive(true);
                PoseArm(e);
                float size = ExplorerSize(player);
                Vector3 grip = e.wrist != null ? e.wrist.position :
                    player.HoldPoint != null ? player.HoldPoint.position : player.transform.TransformPoint(explorerGripPoint * size);
                float worldScale = size * Mathf.Max(.01f, Mathf.Abs(player.transform.lossyScale.y));
                var orientation=Quaternion.LookRotation(player.transform.forward,player.transform.up);
                if(e.wrist!=null)
                {
                    // Calibrate once on the posed rig. Free camera orbit must never spin a
                    // prop independently inside an otherwise stationary hand.
                    if(!e.gripCalibrated){e.wristToItem=Quaternion.Inverse(e.wrist.rotation)*orientation;e.gripCalibrated=true;}
                    orientation=e.wrist.rotation*e.wristToItem;
                }
                if(item==(int)ArchiveSurvivalItem.Flashlight)orientation*=Quaternion.Euler(12,0,0);
                // The prefab origin is not its handle. Place the authored grip in the palm.
                grip+=orientation*Vector3.forward*(.22f*worldScale);
                e.model.transform.SetPositionAndRotation(grip-orientation*(ItemGrip(item)*worldScale),orientation);
                e.model.transform.localScale = Vector3.one * worldScale / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.y));
                if (actor == session.LocalActor && item == (int)ArchiveSurvivalItem.Tranquilizer)
                    localGunOwner = player;
            }
            foreach (int actor in equipment.Keys.Where(a => !observedActors.Contains(a)).ToArray())
            { Dispose(equipment[actor]); equipment.Remove(actor); }
            UpdateGunShoulder(localGunOwner);
        }

        static Vector3 ItemGrip(int item)
        {
            switch(item)
            {
                case 0:return new Vector3(0,0,.2f);
                case 1:return new Vector3(0,.18f,0);
                case 2:case 3:return new Vector3(0,.36f,.15f);
                case 4:return new Vector3(0,-.15f,.02f);
                default:return new Vector3(0,-.02f,.13f);
            }
        }

        public void FaceLocalShot(Vector3 aim)
        {
            var local=Player.LocalInstance;
            if(local==null||session==null||!session.CanAct(session.LocalActor)||VisibleItem(session.LocalActor)!=(int)ArchiveSurvivalItem.Tranquilizer)return;
            var horizontal=Vector3.ProjectOnPlane(aim,local.transform.up);
            if(horizontal.sqrMagnitude<.001f)return;
            var target=local.GetComponentInChildren<CameraTargetController>();
            var look=target!=null?target.transform.rotation:Quaternion.identity;
            local.transform.rotation=Quaternion.LookRotation(horizontal,local.transform.up);
            if(target!=null)target.transform.rotation=look;
        }

        void UpdateGunShoulder(Player local)
        {
            // Availability is the same as the visible equipment above: captured, reading,
            // downed, academic-prop and night entrance presentations release this camera.
            if (local == null) { ReleaseGunShoulder(); return; }
            var output = Camera.main;
            var brain = output != null ? output.GetComponent<CinemachineBrain>() : null;
            if (brain == null || !brain.enabled) { ReleaseGunShoulder(); return; }
            if (gunFollow != null && (gunCamera == null || !gunCamera.isActiveAndEnabled ||
                !gunFollow.enabled || gunCamera.Follow != gunCameraTarget ||
                !TracksPlayer(gunCameraTarget, local) || !ReferenceEquals(brain.ActiveVirtualCamera, gunCamera)))
                ReleaseGunShoulder();
            if (gunFollow == null)
            {
                // Resolve only the currently live local follow camera. Never rebind targets,
                // alter a cinematic/spectator camera, or scan every scene camera each frame.
                var camera = brain.ActiveVirtualCamera as CinemachineCamera;
                if (camera == null || !camera.isActiveAndEnabled || !TracksPlayer(camera.Follow, local)) return;
                var follow = camera.GetComponent<CinemachineThirdPersonFollow>();
                if (follow == null || !follow.enabled) return;
                gunCamera = camera; gunFollow = follow; gunCameraTarget = camera.Follow;
                originalShoulderOffset = appliedShoulderOffset = follow.ShoulderOffset;
                originalCameraSide = appliedCameraSide = follow.CameraSide;
            }
            float size = ExplorerSize(local) * Mathf.Max(.01f, Mathf.Abs(local.transform.lossyScale.y));
            float blend = 1 - Mathf.Exp(-Mathf.Max(1, gunShoulderBlendSpeed) * Time.unscaledDeltaTime);
            appliedShoulderOffset = Vector3.Lerp(gunFollow.ShoulderOffset, originalShoulderOffset + gunShoulderOffset * size, blend);
            appliedCameraSide = Mathf.Lerp(gunFollow.CameraSide, 1, blend);
            gunFollow.ShoulderOffset = appliedShoulderOffset; gunFollow.CameraSide = appliedCameraSide;
        }

        static bool TracksPlayer(Transform target, Player player) => target != null &&
            (target == player.transform || target.IsChildOf(player.transform));

        void ReleaseGunShoulder()
        {
            if (gunFollow != null)
            {
                // Restore only fields still owned by this presentation. Another camera
                // controller may have taken over; its new settings and target stay intact.
                if ((gunFollow.ShoulderOffset - appliedShoulderOffset).sqrMagnitude < .000001f)
                    gunFollow.ShoulderOffset = originalShoulderOffset;
                if (Mathf.Abs(gunFollow.CameraSide - appliedCameraSide) < .0001f)
                    gunFollow.CameraSide = originalCameraSide;
            }
            gunCamera = null; gunFollow = null; gunCameraTarget = null;
        }

        static float ExplorerSize(Player player)
        {
            var capsule = player.GetComponent<CharacterController>();
            return capsule != null ? Mathf.Clamp(capsule.height / 5.5f, .3f, 3) : 1;
        }

        static void FindArm(Equipment e)
        {
            var animator = e.player.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.gameObject.activeInHierarchy);
            if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid)
            {
                e.shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                e.elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                e.wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            else
            {
                var bones = animator != null ? animator.GetComponentsInChildren<Transform>(true) : e.player.GetComponentsInChildren<Transform>(true);
                e.shoulder = bones.FirstOrDefault(t => t.name == "Bone.005.R");
                e.elbow = bones.FirstOrDefault(t => t.name == "Bone.006.R");
                e.wrist = bones.FirstOrDefault(t => t.name == "Bone.006.R_end");
            }
        }

        void PoseArm(Equipment e)
        {
            if (e.shoulder == null || e.elbow == null || e.wrist == null) return;
            if (!e.posed)
            {
                e.shoulderLocal = e.shoulder.localRotation; e.elbowLocal = e.elbow.localRotation;
                e.wristLocal = e.wrist.localRotation; e.posed = true;
            }
            Vector3 a = e.shoulder.position, b = e.elbow.position, c = e.wrist.position;
            float upper = Vector3.Distance(a, b), lower = Vector3.Distance(b, c);
            if (upper < .001f || lower < .001f) return;
            Vector3 requested = e.player.transform.TransformPoint(explorerGripPoint * ExplorerSize(e.player));
            Vector3 direction = requested - a;
            if (direction.sqrMagnitude < .0001f) return;
            float reach = Mathf.Clamp(direction.magnitude, Mathf.Abs(upper - lower) + .001f, upper + lower - .001f);
            direction.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(e.player.transform.right + e.player.transform.forward * .08f - e.player.transform.up * .12f, direction);
            if (bend.sqrMagnitude < .0001f) bend = Vector3.Cross(direction, e.player.transform.up);
            bend.Normalize();
            float along = (upper * upper - lower * lower + reach * reach) / (2 * reach);
            float height = Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            Vector3 elbowGoal = a + direction * along + bend * height;
            Vector3 wristGoal = a + direction * reach;
            e.shoulder.rotation = Quaternion.FromToRotation(b - a, elbowGoal - a) * e.shoulder.rotation;
            Vector3 forearm = e.wrist.position - e.elbow.position;
            Vector3 toward = wristGoal - e.elbow.position;
            if (forearm.sqrMagnitude > .0001f && toward.sqrMagnitude > .0001f)
                e.elbow.rotation = Quaternion.FromToRotation(forearm, toward) * e.elbow.rotation;
        }

        static void RestoreArm(Equipment e)
        {
            if (!e.posed) return;
            if (e.shoulder != null) e.shoulder.localRotation = e.shoulderLocal;
            if (e.elbow != null) e.elbow.localRotation = e.elbowLocal;
            if (e.wrist != null) e.wrist.localRotation = e.wristLocal;
            e.posed = false;
        }

        static void RemoveInteractionComponents(GameObject model)
        {
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) { collider.enabled = false; Destroy(collider); }
            foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; Destroy(body); }
            foreach (var network in model.GetComponentsInChildren<PhotonView>(true)) { network.enabled = false; Destroy(network); }
            foreach (var grabbable in model.GetComponentsInChildren<GrabbableObject>(true)) { grabbable.enabled = false; Destroy(grabbable); }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            { renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; }
        }

        public bool TryGetMuzzle(int actor, out Vector3 position, out Vector3 direction)
        {
            position = direction = Vector3.zero;
            if (!equipment.TryGetValue(actor, out var e) || e.model == null || !e.model.activeInHierarchy) return false;
            position = e.model.transform.TransformPoint(new Vector3(0, e.item == 4 ? .12f : 0, e.item == 4 ? 1.08f : .72f));
            direction = e.model.transform.forward; return true;
        }

        static void Hide(Equipment e) { RestoreArm(e); if (e.model != null) e.model.SetActive(false); }
        void HideAll() { ReleaseGunShoulder(); foreach (var e in equipment.Values) Hide(e); }
        void Dispose(Equipment e) { Hide(e); if (e.model != null) Destroy(e.model); }
        void OnDisable() { HideAll(); }
        void OnDestroy() { ReleaseGunShoulder(); foreach (var e in equipment.Values) Dispose(e); equipment.Clear(); }
    }
}
