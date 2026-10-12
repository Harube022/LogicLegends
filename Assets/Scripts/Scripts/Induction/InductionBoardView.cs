using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Cinemachine;

namespace LogicLegends.Induction
{
    // An explicit, reversible viewCamera view. Proximity only offers the button.
    [DisallowMultipleComponent]
    public sealed class InductionBoardView : MonoBehaviour
    {
        [Min(.5f)] public float altarRange = 5;
        [Min(.1f)] public float transitionSeconds = .8f;
        [Range(25,65)] public float boardFieldOfView = 42;
        [Min(1)] public float framingPadding = 1.08f;
        public bool Viewing { get; private set; }
        public bool Transitioning { get; private set; }
        InductionStageManager stage;
        Camera viewCamera;
        Vector3 previousPosition;
        Quaternion previousRotation;
        float previousFieldOfView;
        Coroutine transition;
        readonly List<Behaviour> suspended = new List<Behaviour>();
        public void Initialize(InductionStageManager owner) { stage = owner; }
        public bool NearAltars
        {
            get
            {
                var player = Player.LocalInstance;
                if (stage == null || player == null) return false;
                var position = player.transform.position;
                return stage.stations.Any(s =>
                {
                    var mesh = s.GetComponent<Renderer>();
                    var bounds = mesh != null ? mesh.bounds : new Bounds(s.position, Vector3.one);
                    var closest = bounds.ClosestPoint(position);
                    var delta = closest - position;
                    return Mathf.Abs(delta.y) < altarRange && new Vector2(delta.x,delta.z).magnitude <= altarRange;
                });
            }
        }
        public bool Available => !Viewing && stage != null && stage.CanAct && NearAltars;
        public void ViewBoard()
        {
            if(stage!=null&&stage.onboarding!=null&&stage.onboarding.IsActive){stage.onboarding.HandlePracticeAction("board");return;}
            if (!Available || Camera.main == null || !stage.BeginModal()) return;
            viewCamera = Camera.main; Viewing = true;
            previousPosition = viewCamera.transform.position; previousRotation = viewCamera.transform.rotation;
            previousFieldOfView = viewCamera.fieldOfView;
            var brain = viewCamera.GetComponent<CinemachineBrain>();
            if (brain != null && brain.enabled) { suspended.Add(brain); brain.enabled = false; }
            foreach (var zoom in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))
                if (zoom.enabled) { suspended.Add(zoom); zoom.enabled = false; }
            stage.ui.ShowBoardView(true);
            // Fit the existing gold frame, including the board's current transform.
            var frame = stage.informationBoard.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "Info_GoldFrame");
            var bounds = frame != null ? frame.bounds : stage.boardInstructions.bounds;
            Vector3 normal = stage.informationBoard.forward.normalized;
            Vector3 up = stage.informationBoard.up.normalized;
            Vector3 right = Vector3.Cross(up, normal).normalized;
            float halfHeight = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(up.x),Mathf.Abs(up.y),Mathf.Abs(up.z)));
            float halfWidth = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(right.x),Mathf.Abs(right.y),Mathf.Abs(right.z)));
            float distance = Mathf.Max(halfHeight,halfWidth/Mathf.Max(.3f,viewCamera.aspect))/Mathf.Tan(boardFieldOfView*Mathf.Deg2Rad*.5f)*framingPadding;
            transition = StartCoroutine(Blend(bounds.center+normal*distance,Quaternion.LookRotation(-normal,up),boardFieldOfView,false));
        }
        public void Back()
        {
            if(stage!=null&&stage.onboarding!=null&&stage.onboarding.IsActive){stage.onboarding.HandlePracticeAction("back");return;}
            if (!Viewing) return;
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(Blend(previousPosition,previousRotation,previousFieldOfView,true));
        }
        IEnumerator Blend(Vector3 position,Quaternion rotation,float fieldOfView,bool returning)
        {
            Transitioning = true;
            Vector3 start = viewCamera.transform.position; Quaternion startRotation = viewCamera.transform.rotation;
            float startFov = viewCamera.fieldOfView;
            for (float elapsed=0;elapsed<transitionSeconds;elapsed+=Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0,1,elapsed/transitionSeconds);
                viewCamera.transform.SetPositionAndRotation(Vector3.Lerp(start,position,t),Quaternion.Slerp(startRotation,rotation,t));
                viewCamera.fieldOfView = Mathf.Lerp(startFov,fieldOfView,t); yield return null;
            }
            viewCamera.transform.SetPositionAndRotation(position,rotation);viewCamera.fieldOfView=fieldOfView;
            transition = null;Transitioning = false;
            if (returning) stage.CloseReading();
        }
        public void RestoreImmediately()
        {
            if (!Viewing) return;
            if (transition != null) StopCoroutine(transition);
            transition = null; Transitioning = false; Viewing = false;
            if (viewCamera != null) {viewCamera.transform.SetPositionAndRotation(previousPosition,previousRotation);viewCamera.fieldOfView=previousFieldOfView;}
            foreach(var behaviour in suspended) if(behaviour!=null) behaviour.enabled=true;
            suspended.Clear(); stage.ui?.ShowBoardView(false);
        }
        void OnDisable() { if (Viewing && stage != null) stage.CloseReading(); }
    }
}
