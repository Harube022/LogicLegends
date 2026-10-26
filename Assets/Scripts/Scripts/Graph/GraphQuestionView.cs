using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.Graph
{
    /// <summary>Owns only the numerical-question camera and the input state it temporarily replaces.</summary>
    public sealed class GraphQuestionView : MonoBehaviour
    {
        GraphStageManager stage; PrelimTutorialCameraRig rig;
        readonly Dictionary<GameObject,bool> controls=new Dictionary<GameObject,bool>();
        readonly List<Behaviour> cameraInputs=new List<Behaviour>();
        Dictionary<Renderer,bool> roof;
        RectTransform labels;
        readonly Dictionary<GraphVertexData,TMPro.TMP_Text> vertexLabels=new Dictionary<GraphVertexData,TMPro.TMP_Text>();
        readonly Dictionary<TMPro.TMP_Text,bool> worldLabels=new Dictionary<TMPro.TMP_Text,bool>();
        bool playerBefore,blockedBefore,closing;
        public bool IsOpen => rig!=null;
        public void Initialize(GraphStageManager owner) { stage=owner; }
        void LateUpdate()
        {
            if(stage==null||closing||stage.BoardView!=null&&stage.BoardView.IsOpen)return;
            bool numerical=stage.Ready&&stage.Challenge.type==GraphChallengeType.Degree&&
                (stage.State==GraphStageState.ACTIVE||stage.State==GraphStageState.VALIDATING);
            if(numerical&&!IsOpen)Open();
            if(!numerical&&IsOpen){StartCoroutine(CloseView());return;}
            if(IsOpen)
            {
                FrameNumerical();
                foreach(var item in vertexLabels)
                {
                    var screen=UnityEngine.Camera.main.WorldToScreenPoint(stage.arena.TransformPoint(item.Key.position)+Vector3.up*GraphGeometry.LabelHeight);
                    Vector2 point;RectTransformUtility.ScreenPointToLocalPointInRectangle(labels,screen,null,out point);item.Value.rectTransform.anchoredPosition=point;
                }
            }
        }
        void FrameNumerical()
        {
            var bounds = new Bounds(stage.arena.TransformPoint(stage.Challenge.vertices[0].position), Vector3.zero);
            foreach (var vertex in stage.Challenge.vertices) bounds.Encapsulate(stage.arena.TransformPoint(vertex.position));
            // A wider lens keeps this camera below the chamber walls. The graph sits
            // between the fixed top question and the bottom answer controls.
            const float fov = 60, centerY = .59f, usableHeight = .64f;
            float tangent = Mathf.Tan(fov * Mathf.Deg2Rad * .5f);
            float aspect = Camera.main != null ? Camera.main.aspect : 1.777f;
            float altitude = Mathf.Max(110, (bounds.extents.z + GraphGeometry.VertexRadius + 2) / (tangent * usableHeight),
                (bounds.extents.x + GraphGeometry.VertexRadius + 2) / (tangent * aspect * .72f));
            var position = bounds.center + Vector3.up * altitude;
            position.z -= altitude * tangent * 2 * (centerY - .5f);
            rig.MoveTowards(position, Quaternion.Euler(90, 0, 0), fov);
        }
        void Open()
        {
            stage.ui.BeginDegreeQuestionEntrance();
            playerBefore=stage.LocalPlayer.enabled;blockedBefore=GameInput.Instance.GameplayInputBlocked;
            stage.LocalPlayer.ToggleControl(false);GameInput.Instance.SetGameplayInputBlocked(true);
            MobileInputUI.FindJoystick()?.ResetJoystick();
            foreach(string name in new[]{"Joystick","JumpButton","InteractButton"})
            {var t=stage.ui.host.Find("Gameplay_Interface/"+name);if(t!=null){controls[t.gameObject]=t.gameObject.activeSelf;t.gameObject.SetActive(false);}}
            foreach(var b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
                if(b.enabled&&(b is CameraTargetController||b is Unity.Cinemachine.CinemachineInputAxisController||b is CinemachinePinchZoom||b is ThirdPersonCameraController))
                {cameraInputs.Add(b);b.enabled=false;}
            MobileLookInput.ResetDelta();rig=PrelimTutorialCameraRig.Create("Graph Degree Overhead Camera");
            roof=GraphOverheadVisibility.HideCeiling();
            foreach(var vertex in stage.arena.GetComponentsInChildren<GraphVertex>())
                if(vertex.label!=null){worldLabels[vertex.label]=vertex.label.enabled;vertex.label.enabled=false;}
            labels=StageUITheme.Rect("Overhead Vertex Labels",stage.ui.TutorialTarget("Clock").parent);StageUITheme.Stretch(labels,Vector2.zero,Vector2.one);
            foreach(var v in stage.Challenge.vertices)
            {
                var text=StageUITheme.Text("Vertex "+v.id,labels,v.label,36);text.color=v.id==stage.Challenge.degreeVertex?StageUITheme.Gold:Color.white;
                text.outlineColor=Color.black;text.outlineWidth=.2f;text.fontStyle=TMPro.FontStyles.Bold;text.raycastTarget=false;
                var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(84,50);vertexLabels.Add(v,text);
            }
            var lens=rig.Camera.Lens;lens.NearClipPlane=.3f;lens.FarClipPlane=2000;rig.Camera.Lens=lens;
        }
        public IEnumerator CloseView()
        {
            if(closing){while(closing)yield return null;yield break;}
            if(rig==null)yield break;closing=true;stage?.ui?.CancelDegreeQuestionEntrance();
            rig.ReturnToGameplay(Restore);
            while(closing)yield return null;
        }
        void Restore()
        {
            stage?.ui?.CancelDegreeQuestionEntrance();
            rig=null;closing=false;
            GraphOverheadVisibility.Restore(roof);
            foreach(var label in worldLabels)if(label.Key!=null)label.Key.enabled=label.Value;worldLabels.Clear();
            if(labels!=null){labels.gameObject.SetActive(false);Destroy(labels.gameObject);}vertexLabels.Clear();
            foreach(var pair in controls)if(pair.Key!=null)pair.Key.SetActive(pair.Value);controls.Clear();
            foreach(var b in cameraInputs)if(b!=null)b.enabled=true;cameraInputs.Clear();
            bool playable=stage!=null&&(stage.State==GraphStageState.ACTIVE||stage.State==GraphStageState.WAITING_FOR_ACTIVATION);
            if(stage!=null&&stage.LocalPlayer!=null)stage.LocalPlayer.ToggleControl(playable&&playerBefore);
            bool terminal=stage==null||stage.State==GraphStageState.GAME_OVER||stage.State==GraphStageState.STAGE_COMPLETE;
            GameInput.Instance?.SetGameplayInputBlocked(terminal?true:blockedBefore);MobileLookInput.ResetDelta();
        }
        public static void Frame(PrelimTutorialCameraRig camera,GraphChallengeData graph,Transform arena,float x,float y,float width,float height)
        {
            var bounds=new Bounds(arena.TransformPoint(graph.vertices[0].position),Vector3.zero);
            foreach(var v in graph.vertices)bounds.Encapsulate(arena.TransformPoint(v.position));
            float tangent=Mathf.Tan(42*Mathf.Deg2Rad*.5f),aspect=Camera.main!=null?Camera.main.aspect:1.777f;
            float altitude=Mathf.Max((bounds.extents.z+GraphGeometry.VertexRadius+5)/(tangent*height),
                (bounds.extents.x+GraphGeometry.VertexRadius+5)/(tangent*aspect*width));
            var position=bounds.center+Vector3.up*Mathf.Max(100,altitude);
            position.x-=altitude*tangent*aspect*2*(x-.5f);position.z-=altitude*tangent*2*(y-.5f);
            camera.MoveTowards(position,Quaternion.Euler(90,0,0),42);
        }
        void OnDisable(){if(rig!=null){rig.FinishReturn();Restore();}}
    }
}
