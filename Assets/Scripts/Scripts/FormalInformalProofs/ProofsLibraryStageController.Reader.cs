using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public sealed partial class ProofsLibraryStageController
{
    private bool readerInputHeld, readerWasBlocked, readerPlayerEnabled, readerCursorVisible;
    private CursorLockMode readerCursorLock;
    private GameInput readerInput;
    private Player readerPlayer;
    private GameObject readerControls;
    private bool readerControlsVisible;
    private readonly List<Behaviour> readerCameraInputs = new List<Behaviour>();

    // Reading keeps the existing countdown rule, but suspends movement, look, and click-through.
    private void HoldReaderInput()
    {
        if (readerInputHeld) return;
        readerInputHeld = true;
        readerInput = GameInput.Instance;
        readerPlayer = Player.LocalInstance;
        readerWasBlocked = readerInput != null && readerInput.GameplayInputBlocked;
        readerPlayerEnabled = readerPlayer != null && readerPlayer.enabled;
        readerCursorLock = Cursor.lockState; readerCursorVisible = Cursor.visible;
        foreach (var behaviour in FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None)) PauseReaderCamera(behaviour);
        foreach (var behaviour in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None)) PauseReaderCamera(behaviour);
        foreach (var behaviour in FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None)) PauseReaderCamera(behaviour);
        readerControls = GameObject.Find("Canvas 1")?.transform.Find("SafeArea/Gameplay_Interface")?.gameObject;
        if (readerControls != null) { readerControlsVisible = readerControls.activeSelf; readerControls.SetActive(false); }
        if (readerInput != null) readerInput.SetGameplayInputBlocked(true);
        if (readerPlayer != null) readerPlayer.ToggleControl(false);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    private void PauseReaderCamera(Behaviour behaviour)
    {
        if (!behaviour.enabled) return;
        readerCameraInputs.Add(behaviour); behaviour.enabled = false;
    }

    private void RestoreReaderInput()
    {
        if (!readerInputHeld) return;
        readerInputHeld = false;
        if (readerControls != null) readerControls.SetActive(readerControlsVisible);
        if (readerInput != null) readerInput.SetGameplayInputBlocked(readerWasBlocked);
        if (readerPlayer != null) readerPlayer.ToggleControl(readerPlayerEnabled);
        foreach (var behaviour in readerCameraInputs) if (behaviour != null) behaviour.enabled = true;
        readerCameraInputs.Clear(); readerControls = null; readerInput = null; readerPlayer = null;
        Cursor.lockState = readerCursorLock; Cursor.visible = readerCursorVisible;
    }
}
