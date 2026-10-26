using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Graph
{
    /// <summary>Read-only view of the existing board; never activates or validates a question.</summary>
    [DisallowMultipleComponent]
    public sealed class GraphBoardView : MonoBehaviour
    {
        public Transform board;
        public TMP_Text heading, topic, instructions;
        GraphStageManager stage;
        PrelimTutorialCameraRig rig;
        Button view, back, settings;
        RectTransform overlay;
        readonly Dictionary<GameObject,bool> visibility = new Dictionary<GameObject,bool>();
        readonly List<Behaviour> cameraInputs = new List<Behaviour>();
        bool playerBefore, blockedBefore, runningBefore, cursorBefore, settingsBefore, returning;
        CursorLockMode cursorLock;
        public bool IsOpen { get; private set; }
        public Button ViewButton => view;
        public Button BackButton => back;

        public void Initialize(GraphStageManager owner)
        {
            stage = owner;
            EnsureBoardText();
            view = StageUITheme.Button("View Board", stage.ui.GameplayHUD.transform, "View Board", Open);
            overlay = StageUITheme.Rect("Graph Board Controls", stage.ui.host);
            StageUITheme.Stretch(overlay, Vector2.zero, Vector2.one);
            // A separate canvas keeps Back above every gameplay graphic and input blocker.
            var canvas = overlay.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 600;
            overlay.gameObject.AddComponent<GraphicRaycaster>();
            back = StageUITheme.Button("Board Back", overlay, "Back", Close);
            var rect = (RectTransform)back.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,0);
            rect.anchoredPosition = new Vector2(0,24); rect.sizeDelta = new Vector2(260,100);
            overlay.gameObject.SetActive(false);
        }
        public void EnsureBoardText()
        {
            if (board == null) { var found = GameObject.Find("InformationBoard"); if (found != null) board = found.transform; }
            if (board == null) return;
            heading = EnsureText(heading,"ChallengeTitleAnchor",new Vector2(1.45f,.38f),2.6f);
            topic = EnsureText(topic,"ChallengeFormulaAnchor",new Vector2(1.45f,.34f),2.4f);
            instructions = EnsureText(instructions,"ChallengeInstructionsAnchor",new Vector2(1.45f,1.05f),2.25f);
            heading.text="Challenge 1 of 5\nQuestion 1 of 2"; topic.text="Trial 9 / Graphs";
            instructions.text="Interact with the Book Statue to start the challenge.";
        }
        TMP_Text EnsureText(TMP_Text text,string anchor,Vector2 size,float font)
        {
            if (text == null)
            {
                var parent = board.Find(anchor); if (parent == null) return null;
                text = parent.GetComponentInChildren<TMP_Text>(true);
                if (text == null) { var go = new GameObject("Graph Board Text",typeof(TextMeshPro)); go.transform.SetParent(parent,false); text=go.GetComponent<TMP_Text>(); }
            }
            text.font=StageUITheme.ReadingFont; text.fontStyle=FontStyles.Bold; text.color=StageUITheme.ReadingInk;
            text.alignment=TextAlignmentOptions.Center; text.textWrappingMode=TextWrappingModes.Normal;
            text.enableAutoSizing=true; text.fontSizeMin=1.25f; text.fontSizeMax=font; text.fontSize=font;
            text.rectTransform.sizeDelta=size; text.transform.localScale=Vector3.one;
            text.transform.rotation=Quaternion.LookRotation(-board.forward,board.up);
            var surface=Surface(); var position=text.transform.parent.position;
            // Project onto the parchment front, independent of imported anchor depth.
            position+=board.forward*(Vector3.Dot(surface.center-position,board.forward)+ProjectedExtent(surface,board.forward)+.08f);
            text.transform.position=position; text.raycastTarget=false;
            return text;
        }
        Bounds Surface()
        {
            foreach(var r in board.GetComponentsInChildren<Renderer>(true))
                if(r.name=="Info_ParchmentSurface")return r.bounds;
            return new Bounds(board.position+Vector3.up*17,new Vector3(17,20,1));
        }
        static float ProjectedExtent(Bounds bounds,Vector3 axis) => Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(axis.x),Mathf.Abs(axis.y),Mathf.Abs(axis.z)));
        void LateUpdate()
        {
            if(stage==null||!stage.Ready||board==null)return;
            if(heading!=null)heading.text="Challenge "+(stage.ChallengeIndex+1)+" of 5\nQuestion "+(stage.QuestionIndex+1)+" of 2";
            if(instructions!=null)instructions.text=stage.BoardInstructions;
            bool allowed=stage.CanAct&&(stage.State==GraphStageState.ACTIVE||stage.State==GraphStageState.WAITING_FOR_ACTIVATION);
            // The safe start platform can view this board even before the other platforms exist.
            bool nearby=!stage.GraphRevealed||stage.NearBook||Vector3.Distance(stage.LocalPlayer.transform.position,board.position)<24;
            view.gameObject.SetActive(allowed&&nearby);view.interactable=allowed&&nearby;
            stage.ui.LayoutBoardButton((RectTransform)view.transform);
            if(!IsOpen||returning||rig==null)return;
            var bounds=Surface();var center=bounds.center+board.forward*.3f;
            float aspect=Camera.main!=null?Camera.main.aspect:1.777f;
            float halfHeight=ProjectedExtent(bounds,board.up)+2;
            float halfWidth=ProjectedExtent(bounds,board.right)+2;
            float distance=Mathf.Max(halfHeight/(Mathf.Tan(21*Mathf.Deg2Rad)*.78f),halfWidth/(Mathf.Tan(21*Mathf.Deg2Rad)*aspect*.84f));
            rig.MoveTowards(center+board.forward*distance,Quaternion.LookRotation(-board.forward,board.up),42);
        }
        public void Open()
        {
            if(IsOpen||board==null||!stage.CanAct||!view.gameObject.activeInHierarchy)return;
            IsOpen=true;returning=false;
            playerBefore=stage.LocalPlayer.enabled;blockedBefore=GameInput.Instance.GameplayInputBlocked;runningBefore=stage.timer.Running;
            cursorLock=Cursor.lockState;cursorBefore=Cursor.visible;
            stage.timer.Pause();stage.LocalPlayer.ToggleControl(false);GameInput.Instance.SetGameplayInputBlocked(true);
            MobileInputUI.FindJoystick()?.ResetJoystick();
            Hide(stage.ui.GameplayHUD);
            foreach(string name in new[]{"Joystick","JumpButton","InteractButton"})
            {var t=stage.ui.host.Find("Gameplay_Interface/"+name);if(t!=null)Hide(t.gameObject);}
            settings=stage.ui.settingsButton==null?null:stage.ui.settingsButton.GetComponent<Button>();
            if(settings!=null){settingsBefore=settings.interactable;settings.interactable=false;}
            foreach(var b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
                if(b.enabled&&(b is CameraTargetController||b is Unity.Cinemachine.CinemachineInputAxisController||b is CinemachinePinchZoom||b is ThirdPersonCameraController))
                {cameraInputs.Add(b);b.enabled=false;}
            rig=PrelimTutorialCameraRig.Create("Graph Information Board Camera");
            var priority=rig.Camera.Priority;priority.Value=2000;rig.Camera.Priority=priority;
            var lens=rig.Camera.Lens;lens.NearClipPlane=.1f;lens.FarClipPlane=2000;rig.Camera.Lens=lens;
            overlay.gameObject.SetActive(true);overlay.SetAsLastSibling();back.interactable=true;
            MobileLookInput.ResetDelta();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void Hide(GameObject go){visibility[go]=go.activeSelf;go.SetActive(false);}
        public void Close()
        {
            if(!IsOpen||returning)return;returning=true;back.interactable=false;
            rig.ReturnToGameplay(Restore);
        }
        public void CloseImmediately()
        {
            if(!IsOpen)return;
            if(rig!=null)rig.FinishReturn();
            if(IsOpen)Restore();
        }
        void Restore()
        {
            rig=null;IsOpen=false;returning=false;if(overlay!=null)overlay.gameObject.SetActive(false);
            foreach(var item in visibility)if(item.Key!=null)item.Key.SetActive(item.Value);visibility.Clear();
            foreach(var b in cameraInputs)if(b!=null)b.enabled=true;cameraInputs.Clear();
            if(settings!=null)settings.interactable=settingsBefore;
            bool playable=stage!=null&&(stage.State==GraphStageState.ACTIVE||stage.State==GraphStageState.WAITING_FOR_ACTIVATION);
            if(stage!=null&&stage.LocalPlayer!=null)stage.LocalPlayer.ToggleControl(playable&&playerBefore);
            GameInput.Instance?.SetGameplayInputBlocked(playable?blockedBefore:true);
            if(playable&&runningBefore&&stage.State==GraphStageState.ACTIVE)stage.timer.Resume();
            MobileLookInput.ResetDelta();Cursor.lockState=cursorLock;Cursor.visible=cursorBefore;
        }
        void OnDisable(){CloseImmediately();}
        void OnDestroy(){CloseImmediately();if(overlay!=null)Destroy(overlay.gameObject);}
    }
}
