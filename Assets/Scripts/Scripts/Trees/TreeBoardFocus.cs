using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

namespace LogicLegends.Trees
{
    /// <summary>Activates the scene's board camera through the existing Cinemachine brain.</summary>
    public sealed class TreeBoardFocus : MonoBehaviour
    {
        [FormerlySerializedAs("gameplayCamera")] public Camera outputCamera;
        public CinemachineCamera focusCamera;
        public Transform board;
        public bool IsOpen { get; private set; }
        public bool Transitioning => switching || (brain != null && brain.IsBlending);
        CinemachineBrain brain;
        Rect previousViewport;
        bool switching;
        Coroutine switchGuard;
        readonly List<Behaviour> lookControllers = new List<Behaviour>();

        public bool Enter(Player player)
        {
            if (IsOpen) return true;
            if (outputCamera == null) outputCamera = Camera.main;
            if (outputCamera == null || focusCamera == null || board == null) return false;
            brain = outputCamera.GetComponent<CinemachineBrain>();
            if (brain == null || !brain.enabled) return false;
            IsOpen = true;
            previousViewport = outputCamera.rect;
            if (player != null)
                foreach (var look in player.GetComponentsInChildren<CameraTargetController>()) FreezeLook(look);
            foreach (var look in FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None)) FreezeLook(look);
            foreach (var look in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None)) FreezeLook(look);
            MobileLookInput.ResetDelta();
            outputCamera.rect = new Rect(.25f, 0, .75f, 1);
            FrameBoard();
            focusCamera.gameObject.SetActive(true);
            GuardSwitch();
            return true;
        }
        void FreezeLook(Behaviour look)
        {
            if (!look.enabled) return;
            lookControllers.Add(look);
            look.enabled = false;
        }
        void LateUpdate() { if(IsOpen) FrameBoard(); }
        void FrameBoard()
        {
            if(focusCamera==null||board==null)return;
            focusCamera.transform.SetPositionAndRotation(board.position-Vector3.up*.35f-board.forward*5,board.rotation);
            var lens=focusCamera.Lens;
            lens.OrthographicSize=Mathf.Max(5.4f,8.6f/Mathf.Max(.5f,outputCamera.aspect));
            lens.NearClipPlane=.05f;lens.FarClipPlane=150;
            focusCamera.Lens=lens;
        }
        void GuardSwitch()
        {
            if(!isActiveAndEnabled||!Application.isPlaying){switching=false;return;}
            if (switchGuard != null) StopCoroutine(switchGuard);
            switching = true;
            switchGuard = StartCoroutine(WaitForBrain());
        }
        IEnumerator WaitForBrain()
        {
            // The brain begins its blend in LateUpdate, after the UI's interaction callback.
            yield return null;
            yield return null;
            switching = false;
            switchGuard = null;
        }
        public void Exit()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (focusCamera != null) focusCamera.gameObject.SetActive(false);
            if (outputCamera != null) outputCamera.rect = previousViewport;
            foreach (var look in lookControllers) if (look != null) look.enabled = true;
            lookControllers.Clear();
            MobileLookInput.ResetDelta();
            GuardSwitch();
        }
        void OnDestroy()
        {
            if (IsOpen)
            {
                if (focusCamera != null) focusCamera.gameObject.SetActive(false);
                if (outputCamera != null) outputCamera.rect = previousViewport;
                foreach (var look in lookControllers) if (look != null) look.enabled = true;
                MobileLookInput.ResetDelta();
            }
        }
    }
}
