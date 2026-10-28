using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Graph
{
    /// <summary>Isolated examples introduce each topic once; real graphs and answers stay untouched.</summary>
    public sealed class GraphChallengeIntroductions : MonoBehaviour
    {
        static readonly string[][] Lessons={
            new[]{"Platforms represent vertices. Bridges represent edges.\nCross a connecting bridge to record a move.","Practice: travel from X to Y.\nFor your question, stop at the named destination and tap Confirm."},
            new[]{"Degree counts edges incident to a vertex.\nAn edge is incident when it meets that vertex.","Practice: X has two incident edges, so its degree is 2.\nUse the overhead view. Choose a number, then Confirm. Clear Selection lets you change it."},
            new[]{"A simple path follows connected edges without repeating a vertex.","Practice: X → Y → Z.\nFollow your question’s start and destination. Confirm at the destination. Retry Route clears only the route."},
            new[]{"A directed edge has an arrow. Cross only in its direction.\nPlan through the required checkpoint before choosing a branch.","Practice: X → Y → Z reaches Z via Y.\nA bridge pointing toward you cannot be crossed backward. Avoid dead ends and repeated vertices."},
            new[]{"A simple cycle returns to its starting vertex.\nOnly the starting vertex repeats, to close the cycle.","Practice: X → Y → Z → X has three distinct vertices.\nYour question requires a longer cycle and a named vertex. Count distinct vertices; ignore dead-end branches."}
        };
        GraphStageManager stage; readonly bool[] seen=new bool[5];
        PrelimTutorialCameraRig rig; GameObject examples; GraphChallengeData practice;
        RectTransform root,card,subject;TMP_Text body,title;PrelimTutorialSkipControl skip;
        readonly Dictionary<GameObject,bool> visibility=new Dictionary<GameObject,bool>();
        readonly Dictionary<Renderer,bool> playerRenderers=new Dictionary<Renderer,bool>();
        readonly List<Behaviour> cameraInputs=new List<Behaviour>();
        Dictionary<Renderer,bool> roof;
        Button settings;bool settingsBefore;
        bool playerBefore,blockedBefore,runningBefore,returning;float scaleBefore,pageAt;int topic;
        public bool IsOpen {get;private set;}
        public int PageIndex {get;private set;}
        public Button NextButton {get;private set;}
        public bool HasSeen(int challenge)=>seen[challenge];
        public void Initialize(GraphStageManager owner){stage=owner;}
        void Update()
        {
            if(stage==null)return;
            if(!IsOpen&&stage.Ready&&!stage.OnboardingPending&&!stage.TutorialActive&&!stage.HelpOpen&&!stage.Moving&&
                stage.State==GraphStageState.WAITING_FOR_ACTIVATION&&!seen[stage.ChallengeIndex]&&Time.timeScale>0&&
                GameInput.Instance!=null&&!GameInput.Instance.GameplayInputBlocked&&
                (TeleportManager.Instance==null||!TeleportManager.Instance.IsTransitioning))Begin();
            if(IsOpen&&!returning)NextButton.interactable=Time.unscaledTime-pageAt>.9f;
        }
        void Begin()
        {
            topic=stage.ChallengeIndex;seen[topic]=true;PageIndex=0;IsOpen=true;
            playerBefore=stage.LocalPlayer.enabled;blockedBefore=GameInput.Instance.GameplayInputBlocked;scaleBefore=Time.timeScale;runningBefore=stage.timer.Running;
            stage.timer.Pause();stage.LocalPlayer.ToggleControl(false);GameInput.Instance.SetGameplayInputBlocked(true);Time.timeScale=0;
            MobileInputUI.FindJoystick()?.ResetJoystick();
            foreach(var t in new[]{stage.arena,stage.bookStatue})Hide(t.gameObject);
            foreach(var r in stage.LocalPlayer.GetComponentsInChildren<Renderer>(true)){playerRenderers[r]=r.enabled;r.enabled=false;}
            foreach(string name in new[]{"Joystick","JumpButton","InteractButton"}){var t=stage.ui.host.Find("Gameplay_Interface/"+name);if(t!=null)Hide(t.gameObject);}
            stage.ui.SetControlsTutorialVisible(true);
            settings=stage.ui.settingsButton!=null?stage.ui.settingsButton.GetComponent<Button>():null;
            if(settings!=null){settingsBefore=settings.interactable;settings.interactable=false;}
            foreach(var b in FindObjectsByType<Behaviour>(FindObjectsSortMode.None))
                if(b.enabled&&(b is CameraTargetController||b is Unity.Cinemachine.CinemachineInputAxisController||b is CinemachinePinchZoom||b is ThirdPersonCameraController))
                {cameraInputs.Add(b);b.enabled=false;}
            BuildPractice();BuildUI();rig=PrelimTutorialCameraRig.Create("Graph Challenge Introduction Camera");
            roof=GraphOverheadVisibility.HideCeiling();
            var lens=rig.Camera.Lens;lens.NearClipPlane=.3f;lens.FarClipPlane=2000;rig.Camera.Lens=lens;ShowPage();
        }
        void Hide(GameObject go){visibility[go]=go.activeSelf;go.SetActive(false);}
        void BuildPractice()
        {
            practice=new GraphChallengeData{start="X",destination="Z",vertices=new List<GraphVertexData>{
                new GraphVertexData("X",-12,0),new GraphVertexData("Y",0,12),new GraphVertexData("Z",12,0)}};
            practice.edges.Add(new GraphEdgeData("X","Y",topic==3));practice.edges.Add(new GraphEdgeData("Y","Z",topic==3));
            if(topic==1||topic==4)practice.edges.Add(new GraphEdgeData("Z","X"));
            practice=GraphGeometry.Layout(practice);
            examples=new GameObject("Graph Introduction Practice Only");examples.transform.SetPositionAndRotation(stage.arena.position,stage.arena.rotation);
            foreach(var v in practice.vertices){var view=Instantiate(stage.vertexPrefab,examples.transform);view.Configure(v);}
            foreach(var e in practice.edges){var view=Instantiate(stage.edgePrefab,examples.transform);view.Configure(e,practice.Vertex(e.source).position,practice.Vertex(e.destination).position);}
            foreach(var c in examples.GetComponentsInChildren<Collider>(true))c.enabled=false;
        }
        void BuildUI()
        {
            root=StageUITheme.Rect("Graph Challenge Introduction",stage.ui.host);StageUITheme.Stretch(root,Vector2.zero,Vector2.one);
            subject=StageUITheme.Rect("Practice Graph Highlight",root);subject.gameObject.AddComponent<Image>().color=Color.clear;
            card=StageUITheme.Rect("Challenge Reading Card",root);card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,0);
            card.anchoredPosition=new Vector2(0,24);card.gameObject.AddComponent<Image>().color=new Color(.06f,.13f,.19f,.97f);
            title=StageUITheme.Text("Title",card,"",34);title.color=StageUITheme.Gold;
            body=StageUITheme.Text("Explanation",card,"",32,true);body.color=Color.white;
            NextButton=StageUITheme.Button("Next",card,"Next",Next);
            skip=PrelimTutorialSkipControl.Create(stage.ui.host,Finish);
        }
        void ShowPage(){title.text="Challenge "+(topic+1)+" — "+new[]{"Vertices and edges","Vertex degree","Simple paths","Directed paths","Cycles"}[topic];body.text=Lessons[topic][PageIndex];pageAt=Time.unscaledTime;}
        public void Next(){if(!IsOpen||returning)return;if(++PageIndex>=Lessons[topic].Length)Finish();else ShowPage();}
        void LateUpdate()
        {
            if(!IsOpen||returning||rig==null)return;
            GraphQuestionView.Frame(rig,practice,examples.transform,.5f,.67f,.9f,.50f);
            float width=Mathf.Min(760,stage.ui.host.rect.width-48);float height=body.GetPreferredValues(body.text,width-48,0).y+160;
            card.sizeDelta=new Vector2(width,height);
            StageUITheme.Stretch(title.rectTransform,new Vector2(.03f,1),new Vector2(.97f,1));title.rectTransform.offsetMin=new Vector2(0,-58);title.rectTransform.offsetMax=new Vector2(0,-10);
            StageUITheme.Stretch(body.rectTransform,Vector2.zero,Vector2.one);body.rectTransform.offsetMin=new Vector2(24,100);body.rectTransform.offsetMax=new Vector2(-24,-64);
            var next=(RectTransform)NextButton.transform;next.anchorMin=next.anchorMax=next.pivot=new Vector2(.5f,0);next.anchoredPosition=new Vector2(0,14);next.sizeDelta=new Vector2(220,76);
            subject.anchorMin=new Vector2(.1f,.43f);subject.anchorMax=new Vector2(.9f,.93f);subject.offsetMin=subject.offsetMax=Vector2.zero;
            TutorialSpotlight.Follow(card,subject,null);
        }
        public void Finish()
        {
            if(!IsOpen||returning)return;returning=true;skip?.Release();skip=null;
            root.gameObject.SetActive(false);rig.ReturnToGameplay(Restore);
        }
        void Restore()
        {
            rig=null;foreach(var item in visibility)if(item.Key!=null)item.Key.SetActive(item.Value);visibility.Clear();
            GraphOverheadVisibility.Restore(roof);
            foreach(var item in playerRenderers)if(item.Key!=null)item.Key.enabled=item.Value;playerRenderers.Clear();
            foreach(var b in cameraInputs)if(b!=null)b.enabled=true;cameraInputs.Clear();
            bool terminal=stage.State==GraphStageState.GAME_OVER||stage.State==GraphStageState.STAGE_COMPLETE;
            if(settings!=null)settings.interactable=settingsBefore;
            stage.ui.SetControlsTutorialVisible(false);Time.timeScale=terminal?0:scaleBefore;
            stage.LocalPlayer.ToggleControl(!terminal&&playerBefore);GameInput.Instance.SetGameplayInputBlocked(terminal?true:blockedBefore);MobileLookInput.ResetDelta();
            if(runningBefore&&stage.State==GraphStageState.ACTIVE)stage.timer.Resume();
            if(examples!=null){examples.SetActive(false);Destroy(examples);}if(root!=null)Destroy(root.gameObject);IsOpen=false;returning=false;
        }
        void OnDisable(){if(!IsOpen)return;skip?.Release();if(rig!=null)rig.FinishReturn();if(IsOpen)Restore();}
    }
}
