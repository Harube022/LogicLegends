#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Opt-in saved-scene checks with fake accounts and real mobile input callbacks.</summary>
public static class TrialInteractionChecks
{
    const string Report = "Library/TrialInteractionRepairsChecks.txt";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    static Touchscreen touch; static int finger;
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static T Field<T>(object owner,string name) => (T)owner.GetType().GetField(name,Hidden).GetValue(owner);
    static object Call(object owner,string name,params object[] args) => owner.GetType().GetMethod(name,Hidden).Invoke(owner,args);
    static void SetBackend(string name, object value) => typeof(TrialBackendClient).GetField(name,Static).SetValue(null,value);
    static void Check(bool valid,string message) { if(!valid)throw new InvalidOperationException(message); File.AppendAllText(Report,"PASS "+message+"\n"); }
    public static IEnumerator Run(string scene)
    {
        if(scene=="PRELIM"||!File.Exists(Report))File.WriteAllText(Report,"Isolated saved-scene Play Mode checks. No real account writes, rewards or recordings.\n");
        int trial=TrialCatalog.ForScene(scene).Number;
        string uid="interaction-qa-"+Guid.NewGuid().ToString("N");
        var previous=typeof(TrialBackendClient).GetField("ValidationTransport",Static).GetValue(null);
        SetBackend("ValidationTransport",(Func<string,string,Task<string>>)((path,json)=>Task.FromResult(path=="/trials/access"?JsonUtility.ToJson(new TrialBackendClient.Access{uid=uid,admin=true}):"{}")));
        SetBackend("ValidationUid",uid);SetBackend("ValidationEmail","ordinary@example.test");
        AdminAuthorization.Refresh();TrialRewards.SceneLoaded();
        var previousInputSettings=InputSystem.settings;
        var testInputSettings=UnityEngine.Object.Instantiate(previousInputSettings);
        testInputSettings.hideFlags=HideFlags.HideAndDontSave;
        testInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        testInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=testInputSettings;
        touch=InputSystem.AddDevice<Touchscreen>();finger=500;
        GameObject resultRoot=null;
        try
        {
            var hints=TrialContextHints.Instance;float by=Time.realtimeSinceStartup+15;
            do{hints.AdvanceForValidation(0);yield return null;}while(hints.Suspended&&Time.realtimeSinceStartup<by);
            Check(!hints.Suspended,scene+": Help and tutorial restore active hint input");
            hints.State.Clear();hints.AdvanceForValidation(0);
            Check(!hints.ShowCurrentForAdmin()&&!hints.State.Visible,scene+": ordinary account cannot force a hint");
            SetBackend("ValidationEmail","sngbdsa137@gmail.com");AdminAuthorization.Refresh();AdminAuthorization.Tick();yield return null;
            Check(AdminAuthorization.IsAuthorized,scene+": authenticated backend-confirmed Admin");
            var admin=Find<AdminDebugController>();Check(admin!=null,scene+": existing Admin panel reused");
            admin.AdminButtonOn();yield return null;
            string action=hints.State.Key;
            yield return Tap(Field<Button>(admin,"adminButton"));
            Check(Field<bool>(admin,"panelOpen"),scene+": Admin opens by mobile touch");
            var show=Field<RectTransform>(admin,"card").GetComponentsInChildren<Button>(true).Single(b=>b.name=="ShowCurrentHint");
            yield return Tap(show);yield return new WaitForSecondsRealtime(.4f);
            Check(!Field<bool>(admin,"panelOpen")&&hints.State.Visible&&hints.Opacity>.9f&&hints.State.Key==action,scene+": Show Hint bypasses delay and restores gameplay");
            Check(!TrialRewards.AdminExclusionConfirmed,scene+": read-only hint does not mark the run as Admin-cleared");
            foreach(int size in new[]{6,12,3})
            {
                yield return Size(size);
                Call(admin,"OpenPanel");yield return null;yield return null;yield return Tap(show);
                Check(!Field<bool>(admin,"panelOpen"),scene+": hint touch remains accessible at "+Screen.width+"x"+Screen.height);
            }
            yield return Size(6);
            if(scene=="TruthTable") { yield return Paths();yield return Stairs();yield return Water(); }
            if(scene=="Setssss") yield return SetsRelease();
            resultRoot=new GameObject("Isolated Completion Presentation QA",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            resultRoot.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;resultRoot.GetComponent<Canvas>().sortingOrder=3000;
            var scaler=resultRoot.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var view=StageResultCard.Build(resultRoot.transform,()=>{});view.Stage.text=TrialCatalog.Get(trial).Title;view.Heading.text="TRIAL COMPLETE";view.Summary.text="Time Played: 03:21\nCorrect: 10\nMistakes: 2";view.Root.gameObject.SetActive(true);
            var layout=view.Root.GetComponent<StageResultLayout>();layout.Complete(trial);layout.Complete(trial);
            foreach(int size in new[]{6,12,3})
            {
                yield return Size(size);Canvas.ForceUpdateCanvases();
                // Canvas bounds do not require an Editor Game-view render. EOF
                // can wait indefinitely when another desktop window has focus.
                yield return null;yield return new WaitForSecondsRealtime(.05f);
                var cardBounds=Bounds(view.Card);
                Check(cardBounds.xMin>=0&&cardBounds.xMax<=Screen.width&&cardBounds.yMin>=0&&cardBounds.yMax<=Screen.height,scene+": completion card fits screen at "+Screen.width+"x"+Screen.height);
                var labels=view.Card.GetComponentsInChildren<TMP_Text>(true);var reward=labels.Single(t=>t.name=="TrialCoinReward");
                var icon=view.Card.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Coin Icon");
                Check(reward.text==TrialRewards.CompletionCoins(trial).ToString("N0")&&reward.text.All(c=>char.IsDigit(c)||c==','),scene+": one numeric coin amount");
                Check(icon.sprite==TrialRewardArt.Coin&&icon.sprite!=null&&Bounds(icon.rectTransform).xMax<=Bounds(reward.rectTransform).xMin,scene+": original coin icon beside earned amount");
                Check(!labels.Any(t=>t.gameObject.activeInHierarchy&&(t.text.Contains("Reduction:")||t.text.Contains("Final payout")||t.text.Contains("Save pending"))),scene+": no redundant completion reward text");
                reward.ForceMeshUpdate();Check(!reward.isTextOverflowing&&!Bounds(reward.rectTransform).Overlaps(Bounds(view.Summary.rectTransform)),scene+": reward fits at "+Screen.width+"x"+Screen.height);
                if(scene=="TruthTable"){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Library/CompletionCompact_"+Screen.width+"x"+Screen.height+".png");}
            }
        }
        finally
        {
            Mobile(Vector2.zero);if(resultRoot!=null)UnityEngine.Object.Destroy(resultRoot);
            if(touch!=null)InputSystem.RemoveDevice(touch);
            InputSystem.settings=previousInputSettings;UnityEngine.Object.Destroy(testInputSettings);
            for(int i=1;i<=10;i++)PlayerPrefs.DeleteKey("TrialRewardOutbox.v1."+uid+"."+i);PlayerPrefs.Save();
            SetBackend("ValidationTransport",previous);SetBackend("ValidationUid",null);SetBackend("ValidationEmail",null);AdminAuthorization.Refresh();TrialRewards.SceneLoaded();
            Time.timeScale=1;BookMapMobileChecks.SetSize(6);
        }
        File.AppendAllText(Report,"SCENE PASSED "+scene+"\n");
    }
    static void Mobile(Vector2 direction)
    {
        var ui=Find<MobileInputUI>();if(ui!=null)typeof(MobileInputUI).GetField("inputVector",Hidden).SetValue(ui,direction);
        GameInput.Instance?.SetMobileMovement(direction);
    }
    static void Place(Vector3 at)
    {
        var cc=Player.LocalInstance.GetComponent<CharacterController>();cc.enabled=false;Player.LocalInstance.transform.position=at;cc.enabled=true;Physics.SyncTransforms();
    }
    static IEnumerator Paths()
    {
        var paths=UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("Pathway",StringComparison.Ordinal)||c.name=="PATH1").ToArray();
        var floor=GameObject.Find("FLOOR (1)").GetComponent<MeshCollider>();var cc=Player.LocalInstance.GetComponent<CharacterController>();int checks=0;
        Check(paths.Length==10&&paths.All(c=>!c.convex&&c.sharedMesh==c.GetComponent<MeshFilter>().sharedMesh),"all ten stone paths use exact visible mesh collision");
        foreach(var path in paths)
        {
            bool sampled=false;
            for(float x=path.bounds.min.x;x<path.bounds.max.x&&!sampled;x+=1)
                for(float z=path.bounds.min.z;z<path.bounds.max.z&&!sampled;z+=1)
                {
                    var at=new Vector3(x,path.bounds.max.y+5,z);RaycastHit ground;if(!floor.Raycast(new Ray(at,Vector3.down),out ground,50))continue;
                    bool clear=true;
                    foreach(var offset in new[]{Vector3.zero,Vector3.left,Vector3.right,Vector3.forward,Vector3.back})
                    {RaycastHit stone;if(path.Raycast(new Ray(at+offset*.9f,Vector3.down),out stone,50))clear=false;}
                    if(!clear||ground.point.y<144)continue;
                    var obstacles=Physics.OverlapCapsule(ground.point+Vector3.up*1,ground.point+Vector3.up*5,.8f,~0,QueryTriggerInteraction.Ignore);
                    if(obstacles.Any(c=>c!=floor&&c!=cc))continue;
                    Place(ground.point+Vector3.up*(cc.height*.5f-cc.center.y+.15f));yield return new WaitForSecondsRealtime(.7f);
                    Check(Mathf.Abs(cc.bounds.min.y-ground.point.y)<.2f&&cc.isGrounded,"grounding beside "+path.name+" follows grass, not an invisible hull: gap="+(cc.bounds.min.y-ground.point.y));
                    sampled=true;checks++;
                }
        }
        Check(checks>=8,"real player grounding sampled beside at least eight separate paths");
    }
    static IEnumerator Stairs()
    {
        var access=Find<TruthTableStairAccess>();Check(access!=null&&access.ramps.Length==6,"six fitted entrance ramps, one per house");
        var player=Player.LocalInstance;var cc=player.GetComponent<CharacterController>();var camera=Camera.main;var brain=camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        bool brainOn=brain!=null&&brain.enabled;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
        var interaction=Find<TruthTableDoorInteraction>();var floor=GameObject.Find("FLOOR (1)").GetComponent<MeshCollider>();
        Find<TruthTableStageClock>().StartFromBook();interaction.ResetAfterColumnEvaluation();yield return null;
        if(brain!=null)brain.enabled=false;
        try
        {
            for(int i=0;i<6;i++)
            {
                var ramp=access.ramps[i];var b=ramp.sharedMesh.bounds;Vector3 outwards=ramp.transform.forward;
                var toe=ramp.transform.TransformPoint(new Vector3(0,0,b.max.z+1));RaycastHit ground;
                Check(floor.Raycast(new Ray(new Vector3(toe.x,180,toe.z),Vector3.down),out ground,60),"house "+(i+1)+" exterior ground");
                Place(ground.point+Vector3.up*(cc.height*.5f-cc.center.y+.1f));camera.transform.rotation=Quaternion.LookRotation(-outwards);yield return new WaitForSecondsRealtime(.3f);
                Mobile(Vector2.up);float by=Time.realtimeSinceStartup+4;
                while(ramp.transform.InverseTransformPoint(player.transform.position).z>b.min.z-1&&Time.realtimeSinceStartup<by)yield return null;
                Mobile(Vector2.zero);
                Check(ramp.transform.InverseTransformPoint(player.transform.position).z<b.min.z-.8f&&cc.bounds.min.y>ramp.bounds.max.y-.25f,"house "+(i+1)+" stairs climbed using joystick without jumping: local="+ramp.transform.InverseTransformPoint(player.transform.position)+" feet="+cc.bounds.min.y+" slope="+cc.slopeLimit);
                Vector3 closedCenter=interaction.GetTutorialDoor(i).bounds.center;
                Mobile(Vector2.up);by=Time.realtimeSinceStartup+2;
                while(Time.realtimeSinceStartup<by)
                {var toward=closedCenter-player.transform.position;toward.y=0;if(toward.sqrMagnitude>.01f)camera.transform.rotation=Quaternion.LookRotation(toward);yield return null;}
                Mobile(Vector2.zero);
                Check(ramp.transform.InverseTransformPoint(player.transform.position).z>0,"house "+(i+1)+" closed door still blocks entry: local="+ramp.transform.InverseTransformPoint(player.transform.position)+" doorEnabled="+interaction.GetTutorialDoor(i).enabled+" layer="+cc.gameObject.layer+" door="+interaction.GetTutorialDoor(i).bounds+" capsule="+cc.bounds);
                Call(interaction,"OpenDoor",i);yield return new WaitForSecondsRealtime(.8f);
                Mobile(Vector2.up);by=Time.realtimeSinceStartup+3;
                while(ramp.transform.InverseTransformPoint(player.transform.position).z>-.8f&&Time.realtimeSinceStartup<by)
                {var toward=closedCenter-outwards*3-player.transform.position;toward.y=0;camera.transform.rotation=Quaternion.LookRotation(toward);yield return null;}
                Mobile(Vector2.zero);Check(ramp.transform.InverseTransformPoint(player.transform.position).z<-.8f,"house "+(i+1)+" open entrance accessible");
            }
        }
        finally{Mobile(Vector2.zero);camera.transform.SetPositionAndRotation(oldPosition,oldRotation);if(brain!=null)brain.enabled=brainOn;}
    }
    static IEnumerator Water()
    {
        Find<TruthTableStageClock>().StartFromBook();var interaction=Find<TruthTableDoorInteraction>();for(int i=0;i<6;i++)Call(interaction,"OpenDoor",i);
        yield return new WaitForSecondsRealtime(1);
        var water=GameObject.Find("Ocean50x50W750H750").GetComponent<Renderer>().bounds;var floor=GameObject.Find("FLOOR (1)").GetComponent<MeshCollider>();
        var points=new List<Vector3>();
        for(float x=water.min.x+3;x<water.max.x-3;x+=4)
            for(float z=water.min.z+3;z<water.max.z-3;z+=4)
            {
                RaycastHit hit;if(floor.Raycast(new Ray(new Vector3(x,180,z),Vector3.down),out hit,100)&&hit.point.y<water.max.y-.5f)
                    points.Add(new Vector3(x,water.max.y,z));
            }
        points=points.Where(p=>new[]{Vector3.zero,Vector3.left,Vector3.right,Vector3.forward,Vector3.back}.All(offset=>{RaycastHit bed;return floor.Raycast(new Ray(p+offset*2+Vector3.up*35,Vector3.down),out bed,100)&&bed.point.y<water.max.y-.7f;})&&
            !Physics.OverlapBox(p+Vector3.up*1.5f,new Vector3(2,1.5f,2),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>c.name!="Ocean50x50W750H750")&&
            Enumerable.Range(0,12).Any(a=>{RaycastHit land;var direction=Quaternion.Euler(0,a*30,0)*Vector3.forward*12;return floor.Raycast(new Ray(p+direction+Vector3.up*35,Vector3.down),out land,100)&&land.point.y>water.max.y+.5f;})).ToList();
        Check(points.Count>0,"actual submerged river floor near accessible shoreline located");
        var blocks=TruthBlock.ActiveBlocks.Where(b=>b.UsesHandBubble).ToArray();Check(blocks.Any(b=>b.value)&&blocks.Any(b=>!b.value),"actual True and False elements available");
        foreach(var block in new[]{blocks.First(b=>b.value),blocks.First(b=>!b.value)})
        {
            int id=block.GetInstanceID(),count=TruthBlock.ActiveBlocks.Count;bool value=block.value;var item=block.GetComponent<GrabbableObject>();
            for(int pass=0;pass<2;pass++)
            {
                block.MarkCollectedFromSpawn();item.ConfigureInventoryState(false,null,false);var col=block.GetComponent<Collider>();col.enabled=true;
                block.transform.position=points[points.Count/2]+Vector3.up*(col.bounds.extents.y+.3f);var rb=block.GetComponent<Rigidbody>();rb.isKinematic=false;rb.useGravity=true;rb.linearVelocity=Vector3.down*2;Physics.SyncTransforms();
                File.AppendAllText(Report,"WATER DROP initial="+block.transform.position+" bedPoint="+points[points.Count/2]+" extent="+col.bounds.extents+" original="+Field<Vector3>(block,"originalPosition")+" parent="+block.transform.parent+"\n");
                float by=Time.realtimeSinceStartup+3;while(!block.IsRecovering&&Time.realtimeSinceStartup<by)yield return null;
                Check(block.IsRecovering,"water starts surface recovery for "+value+", drop "+pass+": at="+block.transform.position+" bottom="+col.bounds.min.y+" water="+water.max.y+" kinematic="+rb.isKinematic+" scene="+block.gameObject.scene.name+" placed="+block.IsPlacedInColumn+" held="+item.IsHeld+" stored="+item.isStoredInInventory+" recoveries="+Field<int>(block,"waterRecoveryCount"));
                Check(block.GetComponent<Renderer>().enabled,"recovering block remains visible");
                by=Time.realtimeSinceStartup+3;while(block.IsRecovering&&Time.realtimeSinceStartup<by)yield return null;yield return null;
                Check(!block.IsRecovering&&col.enabled&&rb.isKinematic&&col.bounds.min.y>water.max.y+.3f,"recovered element rests above safe dry land");
                Check(block.GetInstanceID()==id&&block.value==value&&TruthBlock.ActiveBlocks.Count==count&&block.CanCollectNow,"recovery preserves identity, pickup and object count");
                block.CollectBlock();yield return null;Check(item.isStoredInInventory||item.IsHeld,"recovered element can be collected normally");
                if(!InventoryManager.Instance.HasBlockSelected())InventoryManager.Instance.SetSelectedSlotDirectly(0);
                float playerY=Player.LocalInstance.transform.position.y;
                InventoryManager.Instance.DropSelectedBlock();yield return new WaitForSecondsRealtime(.3f);
                Check(!item.isStoredInInventory&&!item.IsHeld,"normal inventory drop releases the same recovered element");
                Check(Player.LocalInstance.transform.position.y-playerY<.12f,"real TruthTable inventory drop does not lift the player");
            }
            block.CollectBlock();yield return new WaitForSecondsRealtime(.2f);
            var puzzle=Find<DynamicLogicPuzzle>();var point=puzzle.GetColumnSnapPoints(0)[0];
            float beforePlacement=Player.LocalInstance.transform.position.y;
            puzzle.LockBlock(block,point);yield return new WaitForSecondsRealtime(.3f);
            Check(!item.IsHeld&&!block.GetComponent<Collider>().enabled&&Vector3.Distance(block.transform.position,point.position)<.01f,"real TruthTable inventory placement detaches at the exact slot");
            Check(Player.LocalInstance.transform.position.y-beforePlacement<.12f,"real TruthTable placement does not lift the player");
        }
    }
    static IEnumerator SetsRelease()
    {
        var stage=Find<SetssssChallengeOne>();
        var blocks=Field<GrabbableObject[]>(stage,"numberedBlocks");var block=blocks.First(b=>b!=null);
        var player=Player.LocalInstance;var scale=block.transform.lossyScale;
        for(int i=0;i<3;i++)
        {
            block.Grab(player.HoldPoint);player.SetHeldObjectSilently(block);yield return new WaitForSecondsRealtime(.3f);
            float y=player.transform.position.y;block.Drop();player.SetHeldObjectSilently(null);
            yield return new WaitForSecondsRealtime(.4f);
            Check(player.transform.position.y-y<.12f,"Sets repeated release does not lift the player");
            Check(block.GetComponent<Collider>().enabled&&!block.GetComponent<Rigidbody>().isKinematic&&Vector3.Distance(scale,block.transform.lossyScale)<.01f,"Sets release preserves physical size and world collision");
        }
    }
    static IEnumerator Tap(Button button)
    {
        Check(button!=null&&button.gameObject.activeInHierarchy&&button.IsInteractable(),"touch target available: "+button?.name);
        Canvas.ForceUpdateCanvases();var point=Bounds((RectTransform)button.transform).center;
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"top UI raycast reaches "+button.name+" at="+point+" screen="+Screen.width+"x"+Screen.height+" hits="+string.Join(",",hits.Take(4).Select(h=>h.gameObject.name)));
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
    static IEnumerator Size(int index)
    {
        BookMapMobileChecks.SetSize(index);int width=index==6?1920:index==12?2960:720;int height=index==6?1080:index==12?1440:1280;
        float by=Time.realtimeSinceStartup+5;
        do {yield return null;}while((Screen.width!=width||Screen.height!=height)&&Time.realtimeSinceStartup<by);
        Check(Screen.width==width&&Screen.height==height,"Game view settled at "+width+"x"+height);
        yield return null;yield return null;yield return new WaitForSecondsRealtime(.2f);
    }
    static Rect Bounds(RectTransform rect){var p=new Vector3[4];rect.GetWorldCorners(p);return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));}
}
#endif
