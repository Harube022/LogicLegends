#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Opt-in offline test. Normal book, placement and Submit APIs; saved rewards/account writes are suppressed.</summary>
public static class TruthTableHologramChecks
{
    const string Report="Library/TruthTableHologramChecks.txt";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static T Read<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags).GetValue(o);
    static DynamicLogicPuzzle puzzle;static TruthTableDoorInteraction doors;static TruthTableStageClock clock;
    static TruthTableChallengePhase phase;static TruthTableColumnHologram[] effects;static Touchscreen touch;static int finger=9500;
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Offline Truth Table holograms: actual phase, placement, Submit button callbacks, camera zone, all nine columns and saved-scene collision.\nLIMITATION: existing board-mode Submit UI was not raycastable in the test (Gameplay_Interface/LookArea overlaps, then no target after excluding it). Submission is tested through the unchanged button callback; shipped UI is untouched.\n");
        doors=UnityEngine.Object.FindFirstObjectByType<TruthTableDoorInteraction>();clock=doors.GetComponent<TruthTableStageClock>();
        puzzle=UnityEngine.Object.FindObjectsByType<DynamicLogicPuzzle>(FindObjectsSortMode.None).First(p=>Read<TruthTableDoorInteraction>(p,"houseDoors")==doors);
        phase=Read<TruthTableChallengePhase>(puzzle,"challengePhase");
        effects=Enumerable.Range(1,3).Select(i=>Read<GameObject>(puzzle,"col"+i+"Barrier").GetComponent<TruthTableColumnHologram>()).ToArray();
        touch=InputSystem.AddDevice<Touchscreen>();
        try
        {
            yield return TruthTableNightChecks.Run();
            Check(clock.IsWaitingForBook&&!puzzle.PuzzleCompleted,"normal book-controlled start retained");
            foreach(var h in effects)
            {
                Check(h.GetComponents<Collider>().Length==1&&h.GetComponent<BoxCollider>()!=null,"original single BoxCollider retained");
                Check(h.GetComponentsInChildren<Collider>(true).Length==1,"no visual-child colliders");
                Check(h.GetComponentsInChildren<ParticleSystem>(true).Length==0&&h.GetComponentsInChildren<Light>(true).Length==0,"no particle/light cost");
                var m=h.veil.GetComponent<MeshFilter>().sharedMesh;Check(m.vertexCount==5&&m.vertices.Count(v=>Mathf.Abs(v.y-.5f)<.001f)==1,"five-sided profile has one pointed apex");
                Check(m.bounds.size.z<.001f&&h.veil.sharedMaterial.shader.name=="LogicLegends/CrimsonMath/Veil","single translucent surface uses multiplayer shader");
            }
            for(int challenge=0;challenge<3;challenge++)
            {
                yield return StartBook();
                // Enter the actual existing camera/clock zone; keep the test avatar stationary during UI touches.
                var zone=UnityEngine.Object.FindObjectsByType<TruthTableCameraTrigger>(FindObjectsSortMode.None).First(t=>Read<DynamicLogicPuzzle>(t,"dynamicLogicPuzzle")==puzzle);
                var at=zone.GetComponent<Collider>().bounds.center-Vector3.up*1.5f;
                PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,at,Quaternion.Euler(0,180,0));Player.LocalInstance.ToggleControl(false);
                yield return new WaitForSecondsRealtime(1.2f);Check(clock.IsPlayerAtBoard&&clock.IsCountdownPaused,"real board camera zone pauses countdown");
                for(int column=0;column<3;column++)
                {
                    State(column);
                    Check(phase.ChallengeNumber==challenge+1&&phase.ColumnNumber==column+1,"three columns per challenge retained");
                    if(challenge==0&&column==0)
                    {
                        var wrong=GetCurrentBlocks();bool original=wrong[0].value;wrong[0].value=!original;
                        foreach(var block in wrong)puzzle.TryPlace(block,column);
                        Check(phase.CanSubmitColumn,"four placements enable actual Submit");
                        float before=clock.RemainingSeconds;yield return SubmitViaButton();
                        Check(phase.ColumnNumber==1&&!phase.CanSubmitColumn&&Mathf.Abs(clock.RemainingSeconds-(before-20))<.01f,"wrong column keeps question and costs existing twenty seconds");
                        Check(effects[0].FeedbackVisible&&effects[0].veil.enabled&&!effects[0].GetComponent<Collider>().enabled,"red visible only after confirmed wrong submission, active slot unblocked");
                        wrong[0].value=original;
                        // Rebuild the test fixture after deliberately mutating a block value.
                        puzzle.RespawnBlocksForRetry(Read<bool[]>(phase,"currentAnswers"));
                        yield return null;
                        Log("PASS incorrect submission preserves current column and exact existing −20s penalty.");
                    }
                    var blocks=GetCurrentBlocks();var used=new HashSet<TruthBlock>();var answers=Read<bool[]>(phase,"currentAnswers");
                    for(int row=0;row<4;row++)
                    {
                        var b=blocks.FirstOrDefault(x=>!used.Contains(x)&&x.value==answers[row]);Check(b!=null,"fixture has expected row "+row+" value="+answers[row]+" blocks="+string.Join(",",blocks.Select(x=>x.value.ToString()).ToArray()));used.Add(b);puzzle.TryPlace(b,column);
                        yield return null;
                        var target=Read<Transform>(puzzle,"placementIndicator");
                        if(row<3)Check(target.gameObject.activeSelf&&Vector3.Distance(target.position,puzzle.GetColumnSnapPoints(column)[row+1].position)<.001f,"arrow follows exact next slot");
                        else Check(!target.gameObject.activeSelf,"no arrow after all four placements");
                        if(row==0)Check(!effects[column].FeedbackVisible,"editing a new attempt clears prior red");
                        Check(b.IsPlacedInColumn&&Vector3.Distance(b.transform.position,puzzle.GetColumnSnapPoints(column)[row].position)<.001f,"real LockBlock snap/data preserved");
                    }
                    yield return null;Check(phase.CanSubmitColumn,"Submit available after all four placements");
                    yield return SubmitViaButton();yield return null;
                    if(challenge==0&&column==0)
                    {
                        State(1);yield return Readability(blocks);
                        Log("PASS gameplay/board views, red and green, live placed blocks, transparent compositing and animation at three mobile sizes.");
                    }
                    if(challenge<2||column<2)Check(!puzzle.PuzzleCompleted,"not completed prematurely");
                    Log("PASS challenge "+(challenge+1)+" column "+(column+1)+": placement, existing Submit callback and state transitions.");
                }
            }
            Check(puzzle.PuzzleCompleted&&clock.IsCompleted&&effects.All(h=>!h.gameObject.activeSelf&&!h.GetComponent<Collider>().enabled),"stage completion hides holograms and restores original no-barrier state");
            Log("ALL TRUTH TABLE HOLOGRAM CHECKS PASSED");
        }
        finally{if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);}
    }
    static TruthBlock[] GetCurrentBlocks()=>TruthBlock.ActiveBlocks.Where(b=>b.UsesHandBubble&&!b.IsPlacedInColumn).ToArray();
    static IEnumerator StartBook()
    {
        Player.LocalInstance.ToggleControl(true);GameInput.Instance.SetGameplayInputBlocked(false);
        PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,doors.TutorialBook.position+new Vector3(2.5f,1.5f,2.5f),Quaternion.Euler(0,180,0));
        yield return new WaitForSecondsRealtime(.3f);
        yield return Tap(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton"));
        Check(clock.IsRunning,"real hand touch starts/resumes challenge");yield return null;
    }
    static void State(int activeColumn)
    {
        for(int i=0;i<3;i++)
        {
            var h=effects[i];Check(h.gameObject.activeSelf==(i!=activeColumn)&&h.GetComponent<Collider>().enabled==(i!=activeColumn),"unchanged barrier availability/collision "+i);
            Check(h.FeedbackVisible==(i<activeColumn),"only submitted correct columns show green; unanswered columns have no feedback "+i);
            Check(h.veil.enabled==(i<activeColumn)&&h.outline.enabled==(i<activeColumn)&&h.symbols.enabled==(i<activeColumn),"all visual parts share confirmed-feedback visibility");
            if(i>=activeColumn)continue;
            var tint=h.veil.sharedMaterial.GetColor("_Crimson");Check(tint.g>tint.r,"completed answer stays green");
            var b=new MaterialPropertyBlock();h.outline.GetPropertyBlock(b);var color=b.GetColor("_Crimson");Check(Vector4.Distance(tint,color)<.001f,"outline follows current state immediately");
        }
    }
    static IEnumerator Readability(TruthBlock[] placed)
    {
        var h=effects[0];var ids=h.GetComponentsInChildren<Renderer>().Select(r=>r.sharedMaterial.GetInstanceID()).ToArray();
        var prop=new MaterialPropertyBlock();h.veil.GetPropertyBlock(prop);float old=prop.GetFloat("_Clock");
        yield return new WaitForSecondsRealtime(.3f);h.veil.GetPropertyBlock(prop);Check(prop.GetFloat("_Clock")>old&&Mathf.Abs(h.symbols.transform.localPosition.y)<=.0081f,"bounded unscaled animation updates");
        Check(ids.SequenceEqual(h.GetComponentsInChildren<Renderer>().Select(r=>r.sharedMaterial.GetInstanceID())),"no per-frame material clones");
        foreach(int size in new[]{6,12,2})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Library/TruthHologram_Board_"+Screen.width+"x"+Screen.height+".png");
            // Render the existing board-camera pose without UI. Compare overlay-on/off with the same placed data.
            var camera=UnityEngine.Object.FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(c=>c.name=="VCam_TruthTable");
            var on=TreesChamberSetup.Capture("Library/TruthHologram_BoardWorld.png",camera.transform.position,camera.transform.position+camera.transform.forward*20,camera.Lens.FieldOfView);
            foreach(var r in h.GetComponentsInChildren<Renderer>())r.enabled=false;
            var off=TreesChamberSetup.Capture("Library/TruthHologram_BoardWithout.png",camera.transform.position,camera.transform.position+camera.transform.forward*20,camera.Lens.FieldOfView);
            foreach(var r in h.GetComponentsInChildren<Renderer>())r.enabled=true;
            var a=on.GetPixels();var b=off.GetPixels();double delta=0;for(int i=0;i<a.Length;i++)delta+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);
            delta/=a.Length*3;UnityEngine.Object.Destroy(on);UnityEngine.Object.Destroy(off);
            Check(delta>.0001&&delta<.07,"hologram adds a quiet transparent overlay, mean difference="+delta);
            Check(placed.All(block=>block!=null&&block.IsPlacedInColumn),"readability comparison cannot alter placed blocks");
        }
        BookMapMobileChecks.SetSize(6);
        var view=TreesChamberSetup.Capture("Library/TruthHologram_GameplayAngle.png",new Vector3(646,155,767),new Vector3(631,153,742),48);UnityEngine.Object.Destroy(view);
    }
    static IEnumerator SubmitViaButton()
    {
        // DynamicLogicPuzzle exposes the button in Update after its fourth placement.
        yield return null;
        // Visual-only regression: exercise the unchanged Button callback. The pre-existing
        // board-mode UI raycast limitation is recorded above; do not patch input as part of art.
        var button=Read<Button>(puzzle,"submitColumnButton");
        Check(button.isActiveAndEnabled&&button.interactable,"existing Submit callback available after four placements");
        button.onClick.Invoke();yield return null;
    }
    static IEnumerator Tap(Button b)
    {
        yield return null;Canvas.ForceUpdateCanvases();var rt=(RectTransform)b.transform;var canvas=rt.GetComponentInParent<Canvas>();
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera!=null?canvas.worldCamera:Camera.main;
        var at=RectTransformUtility.WorldToScreenPoint(camera,rt.TransformPoint(rt.rect.center));
        Check(b.isActiveAndEnabled&&b.interactable,"tap target available: "+b.name);
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=at},hits);Check(hits.Count>0&&(hits[0].gameObject.transform==rt||hits[0].gameObject.transform.IsChildOf(rt)),"real raycast reaches "+b.name+" at "+at+"; got "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=at});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=at});yield return null;yield return null;
    }
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static void Log(string message)=>File.AppendAllText(Report,message+"\n");
}
#endif
