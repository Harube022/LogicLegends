using Unity.Cinemachine;
using UnityEngine;

/// <summary>Clear follow history for the actual targets of the local player's cameras.</summary>
public static class TeleportCameraSync
{
    public static void ResetAfterMove(Transform player, Vector3 displacement)
    {
        if (player == null || !TeleportManager.UsesCoveredTransitions) return;
        foreach (var look in player.GetComponentsInChildren<CameraTargetController>(true))
            look.RestoreLookAfterTeleport();
        foreach (var camera in Object.FindObjectsByType<CinemachineVirtualCameraBase>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform follow = camera.Follow;
            Transform lookAt = camera.LookAt;
            bool followsPlayer = BelongsToPlayer(follow, player);
            bool looksAtPlayer = BelongsToPlayer(lookAt, player);
            if (!followsPlayer && !looksAtPlayer) continue;
            // CameraTarget is a different target identity from the player root.
            if (followsPlayer) camera.OnTargetObjectWarped(follow, displacement);
            if (looksAtPlayer && lookAt != follow) camera.OnTargetObjectWarped(lookAt, displacement);
            camera.PreviousStateIsValid = false;
        }
        foreach (var camera in Object.FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None))
            if (camera.isActiveAndEnabled) camera.WarpCamera(player);
        MobileLookInput.ResetDelta();
    }

    private static bool BelongsToPlayer(Transform target, Transform player)
    {
        return target != null && (target == player || target.IsChildOf(player));
    }
}
