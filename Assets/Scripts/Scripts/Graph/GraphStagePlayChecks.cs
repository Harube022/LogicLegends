#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using LogicLegends.Graph;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in checks using the real player, collisions, callbacks, scene and coroutine transitions.</summary>
public sealed class GraphStagePlayChecks : MonoBehaviour
{
    public static string Status { get; private set; } = "Not run";
    private GraphStageManager m;
    private Transform walkTarget;
    private bool numberedOnly;
    private bool boundariesOnly;
    private bool feedbackOnly;
    private bool spawnMapOnly;
    [MenuItem("Tools/Logic Legends/Test Graph Spawn and Map (Play Mode)")]
    public static void RunSpawnMap()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Graph Play Mode first.");
        var check=new GameObject("Graph spawn and map validation (temporary)").AddComponent<GraphStagePlayChecks>();
        check.spawnMapOnly=true;
    }
    [MenuItem("Tools/Logic Legends/Test Graph Vertex Feedback (Play Mode)")]
    public static void RunFeedback()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Graph Play Mode first.");
        var check=new GameObject("Graph vertex feedback validation (temporary)").AddComponent<GraphStagePlayChecks>();
        check.feedbackOnly=true;
    }
    [MenuItem("Tools/Logic Legends/Test Graph Flow (Play Mode)")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Graph Play Mode first.");
        new GameObject("Graph validation (temporary)").AddComponent<GraphStagePlayChecks>();
    }
    [MenuItem("Tools/Logic Legends/Test Numbered Graph Flow (Play Mode)")]
    public static void RunNumbered()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Graph Play Mode first.");
        var check=new GameObject("Numbered graph validation (temporary)").AddComponent<GraphStagePlayChecks>();
        check.numberedOnly=true;
    }
    [MenuItem("Tools/Logic Legends/Test Graph Walking Boundaries (Play Mode)")]
    public static void RunBoundaries()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Graph Play Mode first.");
        var check=new GameObject("Graph boundary validation (temporary)").AddComponent<GraphStagePlayChecks>();
        check.boundariesOnly=true;
    }
    private void Start()
    {
        m=FindFirstObjectByType<GraphStageManager>();
        walkTarget=new GameObject("Validation walk target").transform;
        GraphCompletion.SuppressPersistenceForValidation=true;
        Status="Running";StartCoroutine(Guard(spawnMapOnly?SpawnMapFlow():feedbackOnly?FeedbackFlow():boundariesOnly?BoundaryFlow():Flow()));
    }
    private IEnumerator Guard(IEnumerator routine)
    {
        var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(routine);
        while(stack.Count>0)
        {
            object next=null;bool moved=false;Exception failure=null;
            try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}catch(Exception e){failure=e;}
            if(failure!=null){Status="FAIL: "+failure.Message;Debug.LogError(Status);Cleanup();yield break;}
            if(!moved){stack.Pop();continue;}
            if(next is IEnumerator nested){stack.Push(nested);continue;}
            yield return next;
        }
        Status=spawnMapOnly?"PASS: initial spawn and retry rest on the generated vertex floor without upward correction; map pointer tip matches every vertex and bridge midpoint at four headings; readable HUD colors; no duplicate walk colliders.":feedbackOnly?"PASS: all vertex circles hidden initially, red after a wrong answer, cleared after feedback, green after a correct answer, and hidden on the next challenge. Point effects disabled only on Graph copies. Account writes suppressed.":
            boundariesOnly?"PASS: actual player walked offset directed bridges A -> B -> D, the B platform rim, and a platform turn without route interruption. Orange until arrival, blue afterward; reverse gate closes after capsule clearance. Account writes suppressed.":
            "PASS: five "+(numberedOnly?"numbered":"randomly selected")+" challenges, physical routes, book/mobile callbacks, penalty + stun, retry, preserved selection, victory after five and zero-time game over. Account writes suppressed.";
        Debug.Log(Status);Cleanup();
    }
    private void Check(bool value,string message){if(!value)throw new Exception(message);}
    private IEnumerator WaitReady()
    {
        float end=Time.realtimeSinceStartup+20;
        while(!m.Ready || m.Moving || TeleportManager.Instance.IsTransitioning)
        {Check(Time.realtimeSinceStartup<end,"ready timeout");yield return null;}
        yield return null;
    }
    private IEnumerator Activate()
    {
        yield return WaitReady();Check(m.State==GraphStageState.WAITING_FOR_ACTIVATION,"next challenge requires book activation");
        float remaining=m.timer.Remaining;yield return new WaitForSeconds(.15f);Check(m.timer.Remaining==remaining,"waiting timer paused");
        GameInput.Instance.MobileInteract();GameInput.Instance.MobileInteract();
        yield return WaitReady();Check(m.State==GraphStageState.ACTIVE && m.timer.Running,"mobile book activation starts clock once");
    }
    private void CheckGroundedSpawn()
    {
        var vertex=m.arena.GetComponentsInChildren<GraphVertex>().First(v=>v.vertexId==m.Challenge.start);
        var player=m.LocalPlayer.transform;
        var capsule=player.GetComponent<CharacterController>();
        Check(vertex.TryGetSurfaceHeight(player.position,out float floor),"generated floor raycast");
        float bottom=(player.position+player.TransformVector(capsule.center-Vector3.up*capsule.height*.5f)).y;
        Check(bottom>=floor-.01f && bottom-floor<=capsule.skinWidth*Mathf.Abs(player.lossyScale.y)+.02f,"capsule rests above generated floor");
    }
    private IEnumerator SpawnMapFlow()
    {
        yield return WaitReady(); CheckGroundedSpawn();
        float initialY=m.LocalPlayer.transform.position.y;
        yield return new WaitForSeconds(.5f);
        Check(m.LocalPlayer.transform.position.y<=initialY+.01f,"initial spawn has no upward pop");
        foreach(var vertex in m.arena.GetComponentsInChildren<GraphVertex>())
            Check(vertex.transform.Find("Vertex platform").GetComponent<Collider>()==null,"no duplicate vertex walk collider");
        foreach(var edge in m.arena.GetComponentsInChildren<GraphEdge>())
            Check(edge.bridge.GetComponent<Collider>()==null && edge.reverseGate!=null,"model floor with one-way gate retained");
        var marker=m.ui.host.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Player marker");
        var points=m.Challenge.vertices.Select(v=>v.position).Concat(m.Challenge.edges.Select(e=>
            (m.Challenge.Vertex(e.source).position+m.Challenge.Vertex(e.destination).position)*.5f));
        var original=m.LocalPlayer.transform.position; var rotation=m.LocalPlayer.transform.rotation;
        m.LocalPlayer.ToggleControl(false);
        foreach(var point in points)
        foreach(float heading in new[]{0f,90f,180f,270f})
        {
            m.LocalPlayer.transform.SetPositionAndRotation(m.arena.TransformPoint(point)+Vector3.up*2,Quaternion.Euler(0,heading,0));
            m.ui.Refresh();
            var expected=new Vector2(point.x/GraphGeometry.LayoutScale*6.2f,(point.z/GraphGeometry.LayoutScale-12)*6.2f);
            Check(Vector2.Distance(marker.anchoredPosition,expected)<.001f && marker.pivot==new Vector2(.5f,0),"pointer tip at exact map coordinate");
            var forward=m.arena.InverseTransformDirection(m.LocalPlayer.transform.forward);
            var screenForward=(Vector2)(marker.localRotation*Vector3.down);
            Check(Vector2.Dot(screenForward.normalized,new Vector2(forward.x,forward.z).normalized)>.999f,"pointer heading matches world");
        }
        PrelimTeleportTransition.MovePlayerImmediately(m.LocalPlayer.gameObject,original,rotation);
        m.LocalPlayer.ToggleControl(true);
        m.ui.Refresh();
        var title=m.ui.host.GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.name=="Map title");
        Check(((Color32)title.color).Equals(new Color32(41,26,15,255)),"dark brown map title");
        yield return Activate(); m.RetryRoute(); yield return WaitReady(); CheckGroundedSpawn();
        float retryY=m.LocalPlayer.transform.position.y;
        yield return new WaitForSeconds(.5f);
        Check(m.LocalPlayer.transform.position.y<=retryY+.01f,"retry has no upward pop");
        ScreenCapture.CaptureScreenshot("Temp/GraphQA/graph-map-spawn-fixed.png");
    }
    private GraphVertex[] FeedbackVertices() => m.arena.GetComponentsInChildren<GraphVertex>();
    private void CheckCircles(bool visible, bool correct)
    {
        var views=FeedbackVertices();Check(views.Length==m.Challenge.vertices.Count,"every vertex has feedback");
        foreach(var vertex in views)
        {
            Check(vertex.answerArea!=null && vertex.answerArea.activeSelf==visible,"circle visibility on "+vertex.vertexId);
            Check(vertex.answerArea.transform.localScale==Vector3.one*.5f,"circle fits white floor");
            foreach(var particles in vertex.answerArea.GetComponentsInChildren<ParticleSystem>(true))
            {
                if(particles.name=="points_large" || particles.name=="points_small")
                    Check(!particles.gameObject.activeSelf,"Graph point effect disabled");
                else if(visible)
                {
                    var color=particles.main.startColor.color;
                    Check(correct?color.g>color.r:color.r>color.g,"answer color on "+vertex.vertexId);
                    Check(particles.isPlaying,"circle plays on "+vertex.vertexId);
                }
                else Check(particles.particleCount==0,"hidden feedback clears particles");
            }
        }
    }
    private IEnumerator FeedbackFlow()
    {
        yield return WaitReady();Check(m.ChallengeIndex==0,"start a fresh Graph play session");
        typeof(GraphStageManager).GetField("runChallengeIndices",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            .SetValue(m,new System.Collections.Generic.List<int>{1,0,2,3,4});
        typeof(GraphStageManager).GetMethod("BuildChallenge",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(m,null);
        yield return null; CheckCircles(false,false); yield return Activate();
        m.Confirm(); CheckCircles(true,false); Check(m.Mistakes==1,"wrong answer counted once");
        yield return new WaitForSeconds(.7f); ScreenCapture.CaptureScreenshot("Temp/GraphQA/vertex-feedback-red.png");
        yield return new WaitForSeconds(m.feedbackSeconds-.6f); CheckCircles(false,false);
        Check(m.State==GraphStageState.ACTIVE,"wrong feedback restores challenge");
        m.SelectAnswer(m.Challenge.Degree(m.Challenge.degreeVertex));m.Confirm();CheckCircles(true,true);
        Check(m.correctSound.loadState==AudioDataLoadState.Loaded && m.GetComponent<AudioSource>().isPlaying,"correct sound starts in the confirmation frame");
        var onset=new float[(m.correctSound.frequency/50)*m.correctSound.channels];
        Check(m.correctSound.GetData(onset,0) && onset.Any(sample=>Mathf.Abs(sample)>.001f),"correct sound is audible within its first 20 milliseconds");
        yield return new WaitForSeconds(.7f); ScreenCapture.CaptureScreenshot("Temp/GraphQA/vertex-feedback-green.png");
        var oldVertices=FeedbackVertices(); bool swapped=false;
        float transitionEnd=Time.realtimeSinceStartup+10;
        while(m.State==GraphStageState.CHALLENGE_COMPLETE || m.Moving)
        {
            Check(Time.realtimeSinceStartup<transitionEnd,"covered challenge transition timeout");
            if(m.ChallengeIndex==0)
                Check(oldVertices.All(vertex=>vertex!=null && vertex.gameObject.activeInHierarchy),"old platforms remain until fade is opaque");
            else if(!swapped)
            {
                Check(PrelimTeleportTransition.Instance.FadeAlpha>=.999f,"challenge swaps only under opaque fade");
                CheckGroundedSpawn(); swapped=true;
            }
            yield return null;
        }
        Check(swapped,"next challenge built while covered");
        yield return WaitReady();CheckCircles(false,true);
        Check(m.Correct==1 && m.ChallengeIndex==1,"correct feedback advances once");
        var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lana Studio/Hyper Casual FX/Prefabs/Area/Area_circles_blue.prefab");
        Check(original.transform.Find("points_large").gameObject.activeSelf && original.transform.Find("points_small").gameObject.activeSelf,"original point effects remain enabled");
    }
    private IEnumerator Walk(string destination)
    {
        string source=m.Tracker.Current;
        var initialEdge=source==destination?null:FindObjectsByType<GraphEdge>(FindObjectsSortMode.None).First(e=>e.data.Allows(source,destination));
        bool previouslyWalked=initialEdge!=null && initialEdge.Walked;
        Status="Walking challenge "+(m.ChallengeIndex+1)+" to "+destination;
        walkTarget.position=m.arena.TransformPoint(m.Challenge.Vertex(destination).position)+Vector3.up;
        m.LocalPlayer.BeginGuidedMovement(walkTarget,6,.25f);
        float end=Time.realtimeSinceStartup+12;
        while(Vector2.Distance(new Vector2(m.LocalPlayer.transform.position.x,m.LocalPlayer.transform.position.z),new Vector2(walkTarget.position.x,walkTarget.position.z))>.4f)
        {
            Check(Time.realtimeSinceStartup<end,"physical walk timeout to "+destination);
            Check(m.Tracker.IsContinuous,"normal walking interrupted at "+m.arena.InverseTransformPoint(m.LocalPlayer.transform.position)+": "+m.Tracker.InterruptionReason);
            if(initialEdge!=null && !previouslyWalked && m.Tracker.Current==source)
                Check(!initialEdge.Walked,"edge stays orange until destination arrival");
            yield return null;
        }
        m.LocalPlayer.EndGuidedMovement();yield return null;
        Check(m.Tracker.Current==destination && m.Tracker.IsContinuous,"physical bridge recorded "+destination+": "+m.Tracker.InterruptionReason);
        // Returning from a blocked reverse gate stays on the same vertex.
        if(source==destination)yield break;
        var crossed=FindObjectsByType<GraphEdge>(FindObjectsSortMode.None).First(e=>e.data.Allows(source,destination));
        Check(crossed.Walked,"crossed bridge lighting turns blue");
        var properties=new MaterialPropertyBlock();crossed.pathLighting[0].GetPropertyBlock(properties);
        Check(properties.GetColor("_BaseColor")==GraphEdge.WalkedColor,"blue is applied to the original highlight mesh");
    }
    private IEnumerator BoundaryFlow()
    {
        yield return WaitReady();Check(m.ChallengeIndex==0,"start a fresh Graph play session");
        typeof(GraphStageManager).GetField("runChallengeIndices",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            .SetValue(m,new System.Collections.Generic.List<int>{3,0,1,2,4});
        typeof(GraphStageManager).GetMethod("BuildChallenge",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(m,null);
        yield return null;yield return Activate();
        // Imported rails/posts now collide; stay within their usable lane.
        yield return OffsetCrossing("B",1f);
        var b=m.Challenge.Vertex("B").position;
        yield return WalkPoint(b+new Vector3(-GraphGeometry.VertexRadius+.05f,0,0));
        Check(m.Tracker.Current=="B" && m.Tracker.IsContinuous,"capsule-supported B rim preserves the recorded route");
        yield return WalkPoint(b);
        yield return OffsetCrossing("D",-1f);
        Check(m.Tracker.Route.SequenceEqual(new[]{"A","B","D"}),"directed route survives rim and turn");
        Check(m.Mistakes==0,"ordinary walking costs no mistakes");
    }
    private IEnumerator OffsetCrossing(string destination,float offset)
    {
        string source=m.Tracker.Current;
        var from=m.Challenge.Vertex(source).position;var to=m.Challenge.Vertex(destination).position;
        var side=Vector3.Cross(Vector3.up,(to-from).normalized);
        var edge=FindObjectsByType<GraphEdge>(FindObjectsSortMode.None).First(e=>e.data.Allows(source,destination));
        // Center the capsule through the imported entrance posts before
        // exercising sideways movement on the open bridge deck.
        yield return WalkPoint(Vector3.Lerp(from,to,.3f));
        Check(m.Tracker.Current==source && !edge.Walked,"offset bridge entrance stays orange");
        yield return WalkPoint(Vector3.Lerp(from,to,.6f)+side*offset);
        Check(m.Tracker.Current==source && !edge.Walked,"offset mid-bridge stays orange");
        yield return WalkPoint(Vector3.Lerp(from,to,.75f));
        yield return WalkPoint(to);
        Check(m.Tracker.Current==destination && edge.Walked,"offset crossing records arrival and turns blue");
        yield return null;Check(edge.reverseGate.enabled,"directed gate closes after capsule clears it");
    }
    private IEnumerator WalkPoint(Vector3 point)
    {
        Status="Walking boundary at "+point;
        walkTarget.position=m.arena.TransformPoint(point)+Vector3.up;
        m.LocalPlayer.BeginGuidedMovement(walkTarget,6,.08f);
        float end=Time.realtimeSinceStartup+12;
        while(Vector2.Distance(new Vector2(m.LocalPlayer.transform.position.x,m.LocalPlayer.transform.position.z),new Vector2(walkTarget.position.x,walkTarget.position.z))>.12f)
        {
            Check(Time.realtimeSinceStartup<end,"boundary walk timeout at "+point);
            Check(m.Tracker.IsContinuous,"boundary walk interrupted at "+m.arena.InverseTransformPoint(m.LocalPlayer.transform.position)+": "+m.Tracker.InterruptionReason);
            yield return null;
        }
        m.LocalPlayer.EndGuidedMovement();yield return null;
        Check(m.Tracker.IsContinuous,"boundary arrival continuous: "+m.Tracker.InterruptionReason);
    }
    private IEnumerator Complete()
    {
        int correct=m.Correct; m.Confirm();m.Confirm();
        Check(m.Correct==correct+1 && !m.timer.Running,"one correct count and immediate timer pause");
        Check(m.State==GraphStageState.CHALLENGE_COMPLETE,"completion feedback before transition");
        float end=Time.realtimeSinceStartup+12;
        while(m.State==GraphStageState.CHALLENGE_COMPLETE || m.Moving)
        {Check(Time.realtimeSinceStartup<end,"completion transition timeout");yield return null;}
    }
    private IEnumerator Flow()
    {
        yield return WaitReady();Check(m.ChallengeIndex==0,"start a fresh Graph play session");
        if(numberedOnly)
        {
            // Editor-only coverage of every newly added graph. Production selection
            // remains untouched; the regular test exercises its actual random run.
            typeof(GraphStageManager).GetField("runChallengeIndices",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .SetValue(m,Enumerable.Range(5,5).ToList());
            typeof(GraphStageManager).GetMethod("BuildChallenge",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(m,null);
            yield return null;
        }
        var selection=m.RunChallengeIndices.ToArray();
        Check(m.ChallengeCount==5 && selection.Distinct().Count()==5,"five unique selected challenges");
        Check(FindObjectsByType<LevelManager>(FindObjectsSortMode.None).Length==0,"no legacy LevelManager");
        Check(FindObjectsByType<StageCompleteManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==0,"no StageCompleteManager in Graph");
        Check(m.ui.pointer!=null && m.ui.pointer.name=="Pointer_0","original pointer sprite assigned");
        Check(FindObjectsByType<Player>(FindObjectsSortMode.None).Length==1,"single player");
        int mistakes=0;
        for(int round=0;round<m.ChallengeCount;round++)
        {
            Check(m.ChallengeIndex==round && m.CatalogIndex==selection[round],"selected order is used");
            yield return Activate();
            Check(FindObjectsByType<GraphEdge>(FindObjectsSortMode.None).All(e=>!e.Walked),"fresh graph and retry use available orange lighting");
            var challenge=m.Challenge;
            if(round==0)
            {
                float before=m.timer.Remaining;float active=m.timer.ActiveSeconds;m.Confirm();m.Confirm();mistakes++;
                Check(Mathf.Abs(m.timer.Remaining-(before-10))<.001f && m.Mistakes==mistakes,"wrong confirmation costs exactly ten once");
                Check(m.timer.ActiveSeconds==active && !m.LocalPlayer.enabled,"penalty is not time played; stun applied");
                Check(m.ui.host.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t=>t.name=="Timer deduction").gameObject.activeSelf,"deduction shown beside timer");
                ScreenCapture.CaptureScreenshot("Temp/GraphQA/graph-assets-penalty.png");
                yield return new WaitForSeconds(m.feedbackSeconds+.1f);
                Check(m.State==GraphStageState.ACTIVE && m.Tracker.Route.Count==1 && m.LocalPlayer.enabled,"wrong answer preserves route, restores player");
                m.RetryRoute();yield return WaitReady();
                Check(m.Mistakes==mistakes && m.Correct==0 && m.timer.Remaining<before,"retry preserves clock and stats");
                Check(m.RunChallengeIndices.SequenceEqual(selection),"retry never reshuffles the run");
                Check(m.Feedback.Contains(challenge.start),"retry feedback uses configured start");
            }
            if(challenge.type==GraphChallengeType.Degree)
            {
                int degree=challenge.Degree(challenge.degreeVertex);
                m.SelectAnswer(0);m.Confirm();mistakes++;yield return new WaitForSeconds(m.feedbackSeconds+.1f);
                Check(m.Answer==0 && m.Mistakes==mistakes,"wrong degree preserved");
                m.ClearSelection();Check(!m.Answer.HasValue,"clear selection");m.SelectAnswer(degree);
            }
            else
            {
                var path=Solve(challenge);
                Check(path!=null,"graph has a valid route");
                for(int step=1;step<path.Count;step++)
                {
                    yield return Walk(path[step]);
                    if(challenge.type==GraphChallengeType.DirectedPath && step==1)
                    {
                        var reverse=FindObjectsByType<GraphEdge>(FindObjectsSortMode.None).First(e=>e.data.source==path[0] && e.data.destination==path[1]);
                        yield return null;Check(reverse.reverseGate.enabled,"reverse gate closes after arrival");
                        walkTarget.position=m.arena.TransformPoint(challenge.Vertex(path[0]).position)+Vector3.up;
                        m.LocalPlayer.BeginGuidedMovement(walkTarget,6,.25f);yield return new WaitForSeconds(1);m.LocalPlayer.EndGuidedMovement();
                        Check(m.Tracker.Route.Count==2 && m.Tracker.IsContinuous,"reverse travel blocked without recording or penalty");
                        yield return Walk(path[step]);
                    }
                }
            }
            yield return Complete();
            Check(m.RunChallengeIndices.SequenceEqual(selection),"progression preserves sampled selection");
            if(round<4)Check(m.State==GraphStageState.WAITING_FOR_ACTIVATION,"victory waits for fifth selected challenge");
        }
        Check(m.State==GraphStageState.STAGE_COMPLETE && m.Correct==5 && m.Mistakes==mistakes && m.ui.ResultPanel.activeSelf,"victory after exactly five with correct stats");
        m.Confirm();Check(m.Correct==5,"result ignores extra confirms");
        ScreenCapture.CaptureScreenshot("Temp/GraphQA/graph-"+(numberedOnly?"numbered":"random")+"-victory.png");yield return null;
        // Check terminal failure on a fresh clock without reloading or writing account state.
        var property=typeof(GraphStageManager).GetProperty("State");property.SetValue(m,GraphStageState.ACTIVE);
        m.timer.Initialize(1,m.HandleGameOver);m.timer.Resume();m.timer.Penalize(10);
        Check(m.State==GraphStageState.GAME_OVER && m.timer.Remaining==0 && !m.LocalPlayer.enabled,"zero triggers game over and locks interactions");
    }
    private static System.Collections.Generic.List<string> Solve(GraphChallengeData challenge)
    {
        var path=new System.Collections.Generic.List<string>{challenge.start};
        return FindRoute(challenge,path)?path:null;
    }
    private static bool FindRoute(GraphChallengeData challenge,System.Collections.Generic.List<string> path)
    {
        string current=path[path.Count-1];
        if(challenge.type!=GraphChallengeType.Cycle && current==challenge.destination)return true;
        if(challenge.type==GraphChallengeType.Cycle && path.Count>=3 && challenge.Connected(current,challenge.start))
        {path.Add(challenge.start);return true;}
        foreach(var vertex in challenge.vertices)
        {
            if(path.Contains(vertex.id) || !challenge.Connected(current,vertex.id))continue;
            path.Add(vertex.id);if(FindRoute(challenge,path))return true;path.RemoveAt(path.Count-1);
        }
        return false;
    }
    private void Cleanup(){GraphCompletion.SuppressPersistenceForValidation=false;if(m!=null && m.LocalPlayer!=null)m.LocalPlayer.EndGuidedMovement();if(walkTarget!=null)Destroy(walkTarget.gameObject);}
    private void OnDestroy()=>GraphCompletion.SuppressPersistenceForValidation=false;
}
#endif
