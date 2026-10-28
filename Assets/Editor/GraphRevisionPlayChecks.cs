#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using LogicLegends.Graph;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class GraphRevisionPlayChecks
{
    const string Report="Library/GraphPolish/Play.txt";
    static GraphStageManager stage;
    public static IEnumerator Run()
    {
        if(UnityEditor.SessionState.GetBool("Help.GraphDirectedOnly",false))
        {stage=UnityEngine.Object.FindFirstObjectByType<GraphStageManager>();Directory.CreateDirectory("Library/GraphPolish");File.WriteAllText(Report,"All ten directed variants: real collision/traversal checks.\n");yield return AllDirectedVariants();yield break;}
        if(UnityEditor.SessionState.GetBool("Help.GraphPlaylistOnly",false)){yield return GraphHelpPlaylistChecks.Run();yield break;}
        if(UnityEditor.SessionState.GetBool("Help.GraphHighlightsOnly",false)){yield return Highlights();yield break;}
        if(UnityEditor.SessionState.GetBool("Help.GraphRevisionAdminOnly",false)){yield return GraphRevisionAdminChecks.Run();yield break;}
        Directory.CreateDirectory("Library/GraphPolish");
        File.WriteAllText(Report,"Offline real input/physics Graph verification. Persistence suppressed.\n");
        stage=UnityEngine.Object.FindFirstObjectByType<GraphStageManager>();
        var picks=stage.RunChallengeIndices.ToArray();Check(picks.Length==10,"ten preselected questions");
        for(int c=0;c<5;c++)Check(stage.SelectedVariant(c,0)!=stage.SelectedVariant(c,1),"distinct questions in challenge "+(c+1));
        for(int c=0;c<5;c++)for(int q=0;q<2;q++)
        {
            Check(stage.ChallengeIndex==c&&stage.QuestionIndex==q,"correct progression C"+(c+1)+"Q"+(q+1));
            if(q==0)yield return Introduction(c);else Check(!stage.Introductions.IsOpen,"introduction does not repeat for question two");
            yield return null;
            Check(!stage.GraphRevealed,"question geometry waits for the book");
            Check(stage.arena.GetComponentsInChildren<GraphVertex>().Length==1&&stage.arena.GetComponentsInChildren<GraphEdge>().Length==0,"only safe start platform before activation");
            Check(stage.ui.TutorialTarget("Graph minimap").Find("Map").GetComponentsInChildren<Image>().Count(i=>i.GetComponentInChildren<TMP_Text>()!=null)==1,"minimap shows only start node before activation");
            if(c==0&&q==0)yield return Board("Waiting",false);
            if(c>0&&q==0)Check(stage.BoardInstructions=="Challenge "+c+" completed! Interact with the Book Statue to start Challenge "+(c+1)+".","between-challenge board prompt");
            float clock=stage.timer.Remaining;yield return new WaitForSecondsRealtime(.1f);Check(stage.timer.Remaining==clock,"book gate pauses shared countdown");
            Tap(Hand());yield return Wait(()=>!stage.Moving&&stage.State==GraphStageState.ACTIVE,10,"book activation");
            Check(stage.GraphRevealed&&stage.arena.GetComponentsInChildren<GraphVertex>().Length==stage.Challenge.vertices.Count&&stage.arena.GetComponentsInChildren<GraphEdge>().Length==stage.Challenge.edges.Count,"book reveals exactly the current graph");
            if(c==0&&q==0)yield return Board("Active",true);
            Time.timeScale=3;
            if(stage.Challenge.type==GraphChallengeType.Degree)
            {
                yield return Wait(()=>stage.QuestionView.IsOpen,3,"degree camera opens");
                if(q==0)
                {
                    var question=stage.ui.TutorialTarget("Objective");yield return null;
                    Check(Vector2.Distance(Rect(question).center,Rect(stage.ui.host).center)<80,"question begins at screen center");
                    Check(stage.ui.DegreeQuestionEntering,"question entrance runs once with overhead transition");
                    yield return Capture("QuestionPop");yield return new WaitForSecondsRealtime(.45f);
                    Check(question.localScale.x>.95f,"question pops to readable scale");
                    yield return new WaitForSecondsRealtime(1.2f);Check(!stage.ui.DegreeQuestionEntering,"question settles at top center");
                }
                else yield return new WaitForSecondsRealtime(.1f);
                Check(stage.QuestionView.IsOpen&&!stage.LocalPlayer.enabled,"degree overhead owns movement");
                foreach(string n in new[]{"Joystick","JumpButton","InteractButton"})Check(!stage.ui.host.Find("Gameplay_Interface/"+n).gameObject.activeInHierarchy,n+" hidden in degree mode");
                if(q==0)
                {
                    foreach(int size in new[]{6,12,2})
                    {
                        BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(1.2f);yield return Layout();
                        yield return Board("Degree_"+Screen.width+"x"+Screen.height,true);
                        yield return Capture("Degree_"+Screen.width+"x"+Screen.height);
                    }
                    int wrong=(stage.Challenge.Degree(stage.Challenge.degreeVertex)+1)%7;
                    Tap(Button("Answer "+wrong));int mistakes=stage.Mistakes;float before=stage.timer.Remaining;
                    Tap(Button("Confirm"));yield return null;
                    Check(stage.ui.TutorialTarget("Timer deduction").GetComponent<TMP_Text>().text=="−10s"&&stage.ui.TutorialTarget("Timer deduction").gameObject.activeInHierarchy,"red penalty is immediately visible");
                    Check(!stage.Feedback.Contains("INCORRECT")&&!stage.ui.AnnouncementText.Contains("INCORRECT"),"no redundant large error notice");
                    yield return Wait(()=>stage.State==GraphStageState.ACTIVE,5,"wrong-answer recovery");
                    Check(stage.Mistakes==mistakes+1&&before-stage.timer.Remaining>=10&&before-stage.timer.Remaining<20,"single existing 10-second penalty");
                    Check(stage.Answer==wrong&&stage.QuestionView.IsOpen,"wrong number and overhead view remain");
                    yield return new WaitForSecondsRealtime(2.6f);Check(!stage.ui.TutorialTarget("Timer deduction").gameObject.activeSelf,"penalty fades away");
                    Tap(Button("Retry"));Check(stage.Answer==null,"Clear Selection remains accessible");
                    Tap(Button("Lesson"));float frozen=stage.timer.Remaining;yield return new WaitForSecondsRealtime(.25f);
                    Check(stage.HelpOpen&&!stage.timer.Running&&stage.timer.Remaining==frozen,"Help pauses numerical question");
                    Tap(UnityEngine.Object.FindFirstObjectByType<StageHelpPage>().BeginButton);yield return null;
                    Check(!stage.HelpOpen&&stage.QuestionView.IsOpen&&!stage.LocalPlayer.enabled&&stage.timer.Running,"Help returns to numerical mode without restoring walking");
                }
                Tap(Button("Answer "+stage.Challenge.Degree(stage.Challenge.degreeVertex)));
            }
            else
            {
                if(c==3)CheckDirectedBounds();
                if(q==0){BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.3f);yield return Layout();}
                var solution=GraphAnswerValidator.FindSolution(stage.Challenge);Check(solution!=null,"validator supplies a real route");
                foreach(string destination in solution.Skip(1))yield return Walk(destination);
                Check(GraphAnswerValidator.Validate(stage.Challenge,stage.Tracker.Route,null,stage.Tracker.IsContinuous&&stage.Tracker.AtVertex,out _),"physically walked valid route");
            }
            int correct=stage.Correct;
            // An ordinary authenticated identity cannot invoke either Admin clear entry point.
            stage.DebugClearCurrentChallenge();stage.DebugClearCurrentQuestion();Check(stage.Correct==correct,"ordinary account cannot clear questions or challenges");
            Tap(Button("Confirm"));
            yield return Wait(()=>stage.State==GraphStageState.WAITING_FOR_ACTIVATION||stage.State==GraphStageState.STAGE_COMPLETE,12,"answer progression");
            Check(stage.Correct==correct+1,"one answer counts once C"+(c+1)+"Q"+(q+1));
            Log("PASS normal answer C"+(c+1)+"Q"+(q+1)+"; correct="+stage.Correct);
            Check(stage.RunChallengeIndices.SequenceEqual(picks),"question selection stays fixed");
            if(c!=4||q!=1)
            {
                Check(stage.State==GraphStageState.WAITING_FOR_ACTIVATION,"next question waits at the book");
                Check(!stage.QuestionView.IsOpen&&!stage.ui.DegreeQuestionEntering,"numerical camera and entrance restored before next book gate");
            }
        }
        Check(stage.BoardInstructions.Contains("Trial complete!"),"final board displays completion information");
        Check(stage.State==GraphStageState.STAGE_COMPLETE&&stage.Correct==10,"completion requires all ten answers");
        stage.Confirm();Check(stage.Correct==10,"duplicate confirmation cannot add completion");
        yield return Capture("Completed");Log("PASS ALL TEN QUESTIONS, FIVE INTRODUCTIONS, DEGREE INPUT/CAMERA, MOBILE LAYOUT AND PENALTIES.");
    }
    static IEnumerator AllDirectedVariants()
    {
        // Separate collision-only fixture after the normal ten-question run. No Admin actions or rewards.
        if(stage.Introductions.IsOpen){stage.Introductions.Finish();while(stage.Introductions.IsOpen)yield return null;}
        stage.enabled=false;stage.arena.gameObject.SetActive(false);stage.bookStatue.gameObject.SetActive(false);
        stage.ui.GameplayHUD.SetActive(false);stage.ui.ResultPanel.SetActive(false);
        stage.LocalPlayer.ToggleControl(true);GameInput.Instance.SetGameplayInputBlocked(false);Time.timeScale=3;
        for(int index=0;index<10;index++)
        {
            var graph=GraphGeometry.Layout(stage.catalog.variantSets[3].variants[index]);
            var root=new GameObject("Directed geometry QA only");root.transform.SetPositionAndRotation(stage.arena.position,stage.arena.rotation);
            foreach(var v in graph.vertices)UnityEngine.Object.Instantiate(stage.vertexPrefab,root.transform).Configure(v);
            var edges=new System.Collections.Generic.List<GraphEdge>();
            foreach(var e in graph.edges){var view=UnityEngine.Object.Instantiate(stage.edgePrefab,root.transform);view.Configure(e,graph.Vertex(e.source).position,graph.Vertex(e.destination).position);edges.Add(view);}
            Physics.SyncTransforms();
            Log("START physical directed variant "+index);
            var tracker=new GraphTraversalTracker();var position=root.transform.TransformPoint(graph.Vertex(graph.start).position);position.y=stage.spawnPoint.position.y;
            PrelimTeleportTransition.MovePlayerImmediately(stage.LocalPlayer.gameObject,position,Quaternion.identity);
            float footprint=(float)typeof(GraphStageManager).GetField("playerFootprint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(stage);
            tracker.Reset(graph,root.transform.InverseTransformPoint(position),footprint,GraphGeometry.VertexRadius,GraphGeometry.BridgeHalfWidth);
            foreach(var edge in edges)edge.UpdateGate(tracker.Current,stage.LocalPlayer.transform.position,footprint);
            foreach(string id in GraphAnswerValidator.FindSolution(graph).Skip(1))
            {
                var target=new GameObject("Directed QA walk target").transform;target.position=root.transform.TransformPoint(graph.Vertex(id).position)+Vector3.up;
                stage.LocalPlayer.BeginGuidedMovement(target,6,.25f);float until=Time.realtimeSinceStartup+22;
                while(Vector2.Distance(new Vector2(stage.LocalPlayer.transform.position.x,stage.LocalPlayer.transform.position.z),new Vector2(target.position.x,target.position.z))>.4f)
                {
                    Check(Time.realtimeSinceStartup<until,"directed variant "+index+" reaches "+id+" at "+stage.LocalPlayer.transform.position);
                    yield return null;tracker.Sample(root.transform.InverseTransformPoint(stage.LocalPlayer.transform.position),Time.deltaTime);
                    foreach(var edge in edges)edge.UpdateGate(tracker.Current,stage.LocalPlayer.transform.position,footprint);
                    Check(tracker.IsContinuous,"directed variant "+index+": "+tracker.InterruptionReason);
                }
                stage.LocalPlayer.EndGuidedMovement();UnityEngine.Object.Destroy(target.gameObject);
                Check(tracker.Current==id,"directed arrival recorded "+index+" "+id);
            }
            Check(GraphAnswerValidator.Validate(graph,tracker.Route,null,tracker.IsContinuous&&tracker.AtVertex,out _),"all required directed vertices reached "+index);
            root.SetActive(false);UnityEngine.Object.Destroy(root);yield return null;Log("PASS physical directed variant "+index+" required route");
        }
        Log("PASS ALL TEN DIRECTED VARIANTS: original edges/directions, all required routes physically reachable.");
    }
    static IEnumerator Highlights()
    {
        stage=UnityEngine.Object.FindFirstObjectByType<GraphStageManager>();
        yield return Introduction(0);
        var tutorial=stage.controlsTutorial;Check(tutorial.Begin(stage),"initial tutorial can use updated targets");
        typeof(GraphControlsOnboarding).GetMethod("BeginGuidedView",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(tutorial,null);
        yield return new WaitForSecondsRealtime(1);Tap(Hand());yield return null;
        Check(tutorial.CurrentStep==GraphTutorialStep.Minimap,"tutorial book practice stays isolated");
        Tap(Button("Next Button"));yield return null;Tap(Button("Next Button"));yield return null;
        Check(tutorial.CurrentStep==GraphTutorialStep.Route,"route step reached");
        foreach(int size in new[]{6,12,2})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.6f);
            var footer=stage.ui.TutorialTarget("Recorded route");var highlight=footer.Find("Graph Control Highlight") as RectTransform;
            Check(highlight!=null&&Rect(highlight).Contains(Rect(footer).center),"route spotlight follows minimap footer");
            Check(footer.parent==stage.ui.TutorialTarget("Graph minimap"),"practice route uses map footer");
            yield return Capture("TutorialRoute_"+Screen.width+"x"+Screen.height);
        }
        Tap(Button("Next Button"));yield return null;
        Check(tutorial.CurrentStep==GraphTutorialStep.Timer&&stage.ui.TutorialTarget("Clock").Find("Graph Control Highlight")!=null,"timer highlight follows compact HUD");
        Tap(Button("Next Button"));yield return null;Tap(Button("Next Button"));yield return null;
        Check(tutorial.CurrentStep==GraphTutorialStep.Degree,"practice degree controls remain available");
        var objective=stage.ui.TutorialTarget("Objective").GetComponentInChildren<TMP_Text>();objective.ForceMeshUpdate();Check(!objective.isTextOverflowing,"practice degree question fits top-center card");
        Tap(Button("Answer 1"));Tap(Button("Confirm"));yield return null;
        Check(tutorial.CurrentStep==GraphTutorialStep.Path,"degree practice Confirm works after relocation");
        tutorial.SkipTutorial();yield return null;tutorial.ConfirmYes();yield return new WaitForSecondsRealtime(.3f);
        Check(!tutorial.IsActive&&!stage.GraphRevealed&&stage.State==GraphStageState.WAITING_FOR_ACTIVATION&&stage.Correct==0&&!stage.timer.Running&&!GameInput.Instance.GameplayInputBlocked,"skip restores waiting platform without real progress");
        yield return Board("AfterTutorial",false);
        File.WriteAllText("Library/GraphPolish/Highlights.txt","PASS route-footer highlight at three sizes, compact timer target, top-center degree practice choices/Confirm, Skip cleanup, real graph hidden and board/Back available afterward.\n");
    }
    static IEnumerator Introduction(int c)
    {
        yield return Wait(()=>stage.Introductions.IsOpen,5,"challenge introduction "+(c+1));
        float clock=stage.timer.Remaining;Check(!stage.timer.Running&&Time.timeScale==0,"introduction pauses countdown and gameplay");
        Check(UnityEngine.Object.FindObjectsByType<PrelimTutorialSkipControl>(FindObjectsSortMode.None).Length==1,"one shared Skip Tutorial");
        yield return new WaitForSecondsRealtime(1.3f);yield return Capture("Introduction_"+(c+1));
        Check(UnityEngine.Object.FindFirstObjectByType<GraphStageManager>().Tracker.Route.Count==1,"practice graph does not edit the real route");
        if(c%2==0){Tap(stage.Introductions.NextButton);yield return new WaitForSecondsRealtime(1);Tap(stage.Introductions.NextButton);}
        else Tap(UnityEngine.Object.FindFirstObjectByType<PrelimTutorialSkipControl>().GetComponent<Button>());
        yield return Wait(()=>!stage.Introductions.IsOpen,5,"introduction restores camera");
        Check(stage.timer.Remaining==clock&&!stage.timer.Running&&stage.LocalPlayer.enabled&&!GameInput.Instance.GameplayInputBlocked,"introduction cleanup returns to waiting gameplay");
        Check(GameObject.Find("Graph Introduction Practice Only")==null,"practice geometry removed");
    }
    static IEnumerator Layout()
    {
        Canvas.ForceUpdateCanvases();var map=stage.ui.TutorialTarget("Graph minimap");var hud=stage.ui.TutorialTarget("Clock");
        Check(Mathf.Abs(map.rect.width-hud.rect.width)<.1f&&Rect(hud).yMax<Rect(map).yMin,"HUD matches minimap width and sits below it");
        foreach(var text in hud.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"HUD text fits "+text.name);}
        var route=stage.ui.TutorialTarget("Recorded route");var drawing=map.Find("Map") as RectTransform;
        Check(route.parent==map&&Rect(route).yMax<Rect(drawing).yMin,"route footer stays inside map beneath graph");
        Check(Rect(map).Contains(Rect(route).center),"route footer belongs to minimap");
        Check(hud.rect.width>240&&hud.rect.height>=140,"HUD enlarged with padding");
        var objective=stage.ui.TutorialTarget("Objective");
        Check(!stage.ui.TutorialTarget("Feedback").gameObject.activeInHierarchy,"redundant HUD feedback removed");
        if(stage.Challenge.type==GraphChallengeType.Degree)
            Check(objective.gameObject.activeInHierarchy&&Mathf.Abs(Rect(objective).center.x-Rect(stage.ui.host).center.x)<2&&Rect(objective).yMin>Rect(stage.ui.host).yMax*.85f,"question stays top center after entrance");
        else Check(!objective.gameObject.activeInHierarchy,"route objectives stay on the Information Board");
        if(stage.Challenge.type==GraphChallengeType.Degree)
        {
            foreach(var button in stage.ui.TutorialTarget("Degree choices").GetComponentsInChildren<Button>())
                Check(Rect((RectTransform)button.transform).height>=44,"number has mobile tap height");
            var actions=new[]{Button("Confirm"),Button("Retry"),stage.BoardView.ViewButton};
            for(int i=0;i<actions.Length;i++)
            {
                var rect=(RectTransform)actions[i].transform;
                Check(rect.sizeDelta==new Vector2(270,100),"degree action dimensions match");
                Check(Mathf.Abs(Rect(rect).yMin-Rect((RectTransform)actions[0].transform).yMin)<1,"degree actions share one row");
                if(i>0)Check(Rect(rect).xMin>Rect((RectTransform)actions[i-1].transform).xMax,"degree actions have a clear gap");
                Check(!Rect(rect).Overlaps(Rect(stage.ui.TutorialTarget("Degree choices"))),"actions clear number choices");
            }
            var camera=Camera.main;
            foreach(var v in stage.Challenge.vertices)
            {
                var screen=camera.WorldToViewportPoint(stage.arena.TransformPoint(v.position));
                Check(screen.z>0&&screen.x>.01f&&screen.x<.90f&&screen.y>.27f&&screen.y<.92f,"overhead frames vertex "+v.id);
            }
        }
        yield return null;
    }
    static void CheckDirectedBounds()
    {
        foreach(var vertex in stage.arena.GetComponentsInChildren<GraphVertex>())
            foreach(var renderer in vertex.transform.Find("Bilog artwork").GetComponentsInChildren<MeshRenderer>())
                CheckInside(renderer.bounds,"platform "+vertex.vertexId);
        foreach(var edge in stage.arena.GetComponentsInChildren<GraphEdge>())
            foreach(var renderer in edge.artwork.GetComponentsInChildren<MeshRenderer>())CheckInside(renderer.bounds,"bridge "+edge.data.source+"→"+edge.data.destination);
        Log("PASS C4 platform/bridge artwork bounds and normal reachable required route");
    }
    static void CheckInside(Bounds b,string name)
    {Check(b.min.x>-50&&b.max.x<50&&b.min.z>-34&&b.max.z<114,"inside playable chamber: "+name+" "+b);}
    static IEnumerator Board(string name,bool running)
    {
        yield return null;
        var board=stage.BoardView;var route=stage.Tracker.Route.ToArray();var answer=stage.Answer;int correct=stage.Correct;
        bool player=stage.LocalPlayer.enabled,blocked=GameInput.Instance.GameplayInputBlocked;var state=stage.State;
        Tap(board.ViewButton);float time=stage.timer.Remaining;
        Check(board.IsOpen&&!stage.CanAct&&!stage.timer.Running&&!stage.LocalPlayer.enabled,"board owns input and pauses countdown");
        stage.InteractBook();stage.Confirm();Check(stage.State==state&&stage.Correct==correct,"viewing cannot start or submit");
        yield return new WaitForSecondsRealtime(2);
        Check(stage.timer.Remaining==time&&stage.Answer==answer&&stage.Tracker.Route.SequenceEqual(route),"read-only board preserves answer and time");
        foreach(var text in new[]{board.heading,board.topic,board.instructions}){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"world board text fits "+text.name);}
        Check(board.instructions.text==stage.BoardInstructions,"board displays current real state");
        yield return Capture("Board_"+name);
        Tap(board.BackButton);yield return Wait(()=>!board.IsOpen,4,"Back restores board camera");yield return null;
        Check(stage.LocalPlayer.enabled==player&&GameInput.Instance.GameplayInputBlocked==blocked&&stage.timer.Running==running,"Back restores exact previous mode");
        Log("PASS board/Back "+name);
    }
    static IEnumerator Walk(string destination)
    {
        var go=new GameObject("Graph QA Walk Target");go.transform.position=stage.arena.TransformPoint(stage.Challenge.Vertex(destination).position)+Vector3.up;
        stage.LocalPlayer.BeginGuidedMovement(go.transform,6,.25f);float until=Time.realtimeSinceStartup+25;
        while(Vector2.Distance(new Vector2(stage.LocalPlayer.transform.position.x,stage.LocalPlayer.transform.position.z),new Vector2(go.transform.position.x,go.transform.position.z))>.4f)
        {Check(Time.realtimeSinceStartup<until,"walk "+stage.Tracker.Current+" reaches "+destination+" from "+stage.LocalPlayer.transform.position+" to "+go.transform.position);if(!stage.Tracker.IsContinuous)throw new InvalidOperationException(stage.Tracker.InterruptionReason);yield return null;}
        stage.LocalPlayer.EndGuidedMovement();UnityEngine.Object.Destroy(go);yield return null;
        Check(stage.Tracker.Current==destination&&stage.Tracker.IsContinuous,"bridge traversal records "+destination);
    }
    static IEnumerator Wait(Func<bool> condition,float seconds,string label)
    {float end=Time.realtimeSinceStartup+seconds;while(!condition()){if(Time.realtimeSinceStartup>end)throw new InvalidOperationException("Graph timeout: "+label);yield return null;}}
    static Button Hand()=>stage.ui.host.Find("Gameplay_Interface/InteractButton").GetComponent<Button>();
    static Button Button(string name)=>stage.ui.host.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
    static void Tap(Button button)
    {
        Canvas.ForceUpdateCanvases();Check(button.gameObject.activeInHierarchy&&button.interactable,"button available: "+button.name);
        var rect=(RectTransform)button.transform;var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"touch raycast reaches "+button.name);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    static Rect Rect(RectTransform t){var c=new Vector3[4];t.GetWorldCorners(c);return UnityEngine.Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
    static IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Library/GraphPolish/"+name+".png");yield return null;}
    static void Check(bool valid,string message){if(!valid)throw new InvalidOperationException("Graph revision: "+message);}
    static void Log(string message){File.AppendAllText(Report,message+"\n");}
}
#endif
