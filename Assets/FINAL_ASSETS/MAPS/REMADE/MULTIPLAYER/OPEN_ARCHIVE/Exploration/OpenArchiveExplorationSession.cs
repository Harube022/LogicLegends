using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LogicLegends.OpenArchiveTesting
{
    /// <summary>Local exploration conveniences. Movement and animation use the existing Player scripts.</summary>
    public sealed class OpenArchiveExplorationSession : MonoBehaviour
    {
        [SerializeField] private Player explorer;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private CinemachineCamera followCamera;

        public Player Explorer => explorer;
        public Transform SpawnPoint => spawnPoint;

        public void Configure(Player player, Transform spawn, CinemachineCamera camera)
        {
            explorer = player; spawnPoint = spawn; followCamera = camera;
        }

        private void Start()
        {
            explorer.SetRunningEnabled(true);
            ReturnToSpawn();
        }

        private void Update()
        {
            if (explorer == null || Time.timeScale <= 0) return;
            if (explorer.transform.position.y < -8 ||
                (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)) ReturnToSpawn();
        }

        public void ReturnToSpawn()
        {
            if (explorer == null || spawnPoint == null || explorer.IsMovementLocked) return;
            if (GameInput.Instance != null) GameInput.Instance.SetMobileMovement(Vector2.zero);
            var joystick = MobileInputUI.FindJoystick();
            if (joystick != null) joystick.ResetJoystick();
            MobileLookInput.ResetDelta();
            var controller = explorer.GetComponent<CharacterController>();
            controller.enabled = false;
            explorer.transform.SetPositionAndRotation(ResolveSpawnPosition(controller), spawnPoint.rotation);
            controller.enabled = true;
            explorer.ResetMotionAfterTeleport();
            SetViewAtSpawn();
        }

        private Vector3 ResolveSpawnPosition(CharacterController controller)
        {
            Vector3 position = spawnPoint.position;
            // The exploration map can be scaled independently of the existing player.
            // Use the real plaza surface instead of scaling the stored character-root Y offset.
            var plaza = spawnPoint.parent.Find("Spawn_StonePlaza_18m");
            var surface = plaza != null ? plaza.GetComponent<Collider>() : null;
            if (surface != null && surface.Raycast(new Ray(position + Vector3.up * 20, Vector3.down), out var hit, 50))
            {
                float scaleY = Mathf.Abs(explorer.transform.lossyScale.y);
                float bottom = (controller.center.y - controller.height * .5f) * scaleY;
                position.y = hit.point.y - bottom + controller.skinWidth * scaleY;
            }
            return position;
        }

        private void SetViewAtSpawn()
        {
            var look = explorer.GetComponentInChildren<CameraTargetController>();
            if (look != null) look.SetViewAngles(12, spawnPoint.eulerAngles.y);
            if (followCamera != null) followCamera.PreviousStateIsValid = false;
        }

    }
}
