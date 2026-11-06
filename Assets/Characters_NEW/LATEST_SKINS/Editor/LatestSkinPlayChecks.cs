#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Offline fixtures: equipment loader Start is disabled; no account, wallet or progression code runs.</summary>
public static class LatestSkinPlayChecks
{
    const string Flag="LatestSkins.IntegrationQA";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    public const string Report=LatestSkinIntegration.Reports+"/PlayChecks.txt";
    static string CurrentReport => InventoryOnly?InventoryAndAdminPresentationChecks.Report:SourceOnly?LatestSkinSourceMotionChecks.Report:FootJitterOnly?LatestSkinFootJitterChecks.Report:SessionState.GetBool(Flag+".PlaybackOnly",false)?LatestSkinPlaybackSetup.Report:SessionState.GetBool(Flag+".ReuseOnly",false)?LatestSkinAnimationReuse.Report:SessionState.GetBool(Flag+".ImpactOnly",false)?LatestSkinIntegration.Reports+"/ImpactChecks.txt":SessionState.GetBool(Flag+".SocketOnly",false)?LatestSkinIntegration.Reports+"/SocketChecks.txt":Report;
    static bool InventoryOnly=>SessionState.GetBool(Flag+".InventoryOnly",false);
    public static void BeginInventory()
    {SessionState.SetBool(Flag+".InventoryOnly",true);try{Begin();}catch{SessionState.SetBool(Flag+".InventoryOnly",false);throw;}}
    public static bool SourceOnly=>SessionState.GetBool(Flag+".SourceOnly",false);
    public static void BeginSourceMotion()
    {SessionState.SetBool(Flag+".SourceOnly",true);try{Begin();}catch{SessionState.SetBool(Flag+".SourceOnly",false);throw;}}
    public static bool FootJitterOnly=>SessionState.GetBool(Flag+".FootJitterOnly",false);
    public static void BeginFootJitter(){BeginSourceMotion();}
    public static bool PlaybackOnly=>SessionState.GetBool(Flag+".PlaybackOnly",false);
    public static void BeginPlayback(){BeginSourceMotion();}
    public static void BeginReuse()
    {
        SessionState.SetBool(Flag+".ReuseOnly",true);
        try{Begin();}catch{SessionState.SetBool(Flag+".ReuseOnly",false);throw;}
    }
    public static void BeginImpact()
    {
        var source=SceneManager.GetActiveScene();bool previewed=source.path!="Assets/Scenes/PRELIM.unity";
        if(previewed)source=EditorSceneManager.OpenPreviewScene("Assets/Scenes/PRELIM.unity");
        try
        {
            var traps=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DynamicDoorTrigger>(true)).ToArray();
            foreach(string field in new[]{"knockbackDelay","stunDuration","knockbackDistance"})
            {
                float value=new SerializedObject(traps[0]).FindProperty(field).floatValue;
                Check(traps.All(t=>Mathf.Abs(new SerializedObject(t).FindProperty(field).floatValue-value)<.001f),"all PRELIM doors share "+field);
                SessionState.SetFloat(Flag+".Impact."+field,value);
            }
        }
        finally{if(previewed)EditorSceneManager.ClosePreviewScene(source);}
        SessionState.SetBool(Flag+".ImpactOnly",true);
        try{Begin();}catch{SessionState.SetBool(Flag+".ImpactOnly",false);throw;}
    }
    public static void Begin(bool socketOnly=false)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)))throw new InvalidOperationException("Open a saved scene before starting this fixture; no scene state was changed.");
        if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Keep unsaved scenes open; save them before starting this opt-in fixture.");
        var paths=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i).path).ToArray();
        SessionState.SetString(Flag+".Paths",string.Join("|",paths));SessionState.SetString(Flag+".Active",SceneManager.GetActiveScene().path);
        SessionState.SetBool(Flag+".SocketOnly",socketOnly);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        new GameObject("Offline Fixture Input",typeof(GameInput));
        var proofScene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Formal vs. Informal Proofs.unity");
        try
        {
            var stage=proofScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProofsLibraryStageController>(true)).First();
            var source=new SerializedObject(stage).FindProperty("carriedVisuals").GetArrayElementAtIndex(0).objectReferenceValue as GameObject;
            var scroll=Object.Instantiate(source);scroll.name="Offline Real Proof Scroll";scroll.SetActive(false);SceneManager.MoveGameObjectToScene(scroll,scene);
        }
        finally{EditorSceneManager.ClosePreviewScene(proofScene);}
        foreach(var path in new[]{"Assets/Resources/Multiplayer_Male.prefab","Assets/Resources/Multiplayer_Female.prefab"})
        {
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);root.SetActive(false);
            foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(b is PlayerEquipmentLoader){b.enabled=false;continue;}
                if(!(b is Player)&&!(b is PlayerAnimator)&&!(b is PlayerCarryRig)&&!(b is IntegratedSkinProfile)&&!(b is SharedSkinAnimationRetarget)&&!(b is PlayerStrideWarp))Object.DestroyImmediate(b);
            }
        }
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Offline Test Ground";floor.transform.position=Vector3.down*.5f;floor.transform.localScale=new Vector3(2000,1,2000);
        var camera=new GameObject("Offline Test Camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=40;
        var light=new GameObject("Offline Test Sun",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,-25,0);light.intensity=1;
        foreach(var path in paths)EditorSceneManager.CloseScene(SceneManager.GetSceneByPath(path),true);
        string description=InventoryOnly?"Offline inventory: touch slot toggles, identity/count/scale, weight, placement, return and reset. No account scripts or rewards.\n":SourceOnly?"Offline source-motion checks: actual GameInput, equipment IDs, CharacterController, source poses, playback clocks, turns, transitions and carrying penalties.\n":"Offline integrated skins: actual GameInput, equipment IDs, CharacterController, Animator layers, pickup/drop and hammer stun.\n";
        if(SessionState.GetBool(Flag+".ReuseOnly",false))File.AppendAllText(CurrentReport,description);
        else File.WriteAllText(CurrentReport,description);
        SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Register()
    {
        EditorApplication.playModeStateChanged-=Restore;EditorApplication.playModeStateChanged+=Restore;
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&SessionState.GetBool(Flag,false))QueueRestore();
    }
    static void Restore(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Flag,false))return;
        QueueRestore();
    }
    static void QueueRestore(){EditorApplication.update-=RestorePending;EditorApplication.update+=RestorePending;}
    public static void RestorePending()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!SessionState.GetBool(Flag,false))return;
        EditorApplication.update-=RestorePending;
            var paths=SessionState.GetString(Flag+".Paths","").Split('|');
            for(int i=0;i<paths.Length;i++)if(!string.IsNullOrEmpty(paths[i]))EditorSceneManager.OpenScene(paths[i],i==0?OpenSceneMode.Single:OpenSceneMode.Additive);
            var active=SceneManager.GetSceneByPath(SessionState.GetString(Flag+".Active",""));if(active.IsValid())SceneManager.SetActiveScene(active);
            SessionState.SetBool(Flag,false);
            SessionState.SetBool(Flag+".ImpactOnly",false);
            SessionState.SetBool(Flag+".ReuseOnly",false);
            SessionState.SetBool(Flag+".PlaybackOnly",false);
            SessionState.SetBool(Flag+".FootJitterOnly",false);
            SessionState.SetBool(Flag+".SourceOnly",false);
            SessionState.SetBool(Flag+".InventoryOnly",false);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Start()
    {if(SessionState.GetBool(Flag,false))new GameObject("Latest Skins Offline Checks").AddComponent<LatestSkinCheckRunner>().StartCoroutine(Guard(InventoryOnly?InventoryAndAdminPresentationChecks.Inventory():SourceOnly?LatestSkinSourceMotionChecks.Run():SessionState.GetBool(Flag+".ReuseOnly",false)?LatestSkinAnimationReuseChecks.Run():SessionState.GetBool(Flag+".ImpactOnly",false)?ImpactChecks():SessionState.GetBool(Flag+".SocketOnly",false)?SocketChecks():Run()));}
    static IEnumerator Guard(IEnumerator sequence)
    {
        var stack=new Stack<IEnumerator>();stack.Push(sequence);
        while(stack.Count>0)
        {
            object next;try{var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}next=top.Current;}
            catch(Exception e){File.AppendAllText(CurrentReport,"FAIL "+e+"\n");Debug.LogException(e);if(SessionState.GetBool(Flag+".Inspect",false))EditorApplication.isPaused=true;else EditorApplication.isPlaying=false;yield break;}
            if(next is IEnumerator child)stack.Push(child);else yield return next;
        }
        File.AppendAllText(CurrentReport,InventoryOnly?"INVENTORY TOGGLE CHECKS PASSED\n":"ALL 12 INTEGRATED SKINS PASSED\n");EditorApplication.isPlaying=false;
    }
    static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
    static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Hidden).GetValue(obj);
    static void Select(PlayerEquipmentLoader loader,string id)
    {typeof(PlayerEquipmentLoader).GetMethod("ApplyEquipment",Hidden).Invoke(loader,new object[]{id,Field<PlayerEquipmentLoader.EquippableModel[]>(loader,"clothesModels")});}
    static bool State(Animator animator,string name)=>animator.GetCurrentAnimatorStateInfo(0).IsName(name)||animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).IsName(name);
    static IEnumerator ImpactChecks()
    {
        Application.runInBackground=true;Application.targetFrameRate=60;Time.timeScale=1;yield return null;int total=0;
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.GetComponent<Player>()!=null).ToArray())
        {
            root.SetActive(true);yield return null;var player=root.GetComponent<Player>();var cc=root.GetComponent<CharacterController>();var loader=root.GetComponent<PlayerEquipmentLoader>();
            foreach(var entry in Field<PlayerEquipmentLoader.EquippableModel[]>(loader,"clothesModels").Where(e=>e!=null&&LatestSkinIntegration.Category(e.itemID)!=null))
            {
                cc.enabled=false;root.transform.SetPositionAndRotation(new Vector3(0,1.03f,0),Quaternion.identity);cc.enabled=true;player.ResetMotionAfterTeleport();GameInput.Instance.SetMobileMovement(Vector2.zero);
                Select(loader,entry.itemID);yield return new WaitForSeconds(.2f);
                var profile=entry.model.GetComponentInChildren<IntegratedSkinProfile>();var driver=profile.GetComponent<PlayerAnimator>();var animator=profile.GetComponent<Animator>();var label=profile.gender+"_"+profile.category;
                // Same-call state entry must interrupt locomotion/carry without a frame or exit-time wait.
                foreach(string state in new[]{"Idle","Walk","Run","Jump","CarryIdle"})
                {
                    animator.Play(state,0,0);animator.SetLayerWeight(1,1);animator.Update(0);
                    driver.BeginStun(5);
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Stun")&&!animator.IsInTransition(0)&&animator.GetLayerWeight(1)==0,label+" immediate interruption from "+state);
                    driver.SetStunned(false);
                }
                animator.Play("Idle",0,0);animator.Update(0);yield return new WaitForSeconds(.2f);
                var trap=new GameObject("Offline impact timing check").AddComponent<DynamicDoorTrigger>();
                foreach(string field in new[]{"knockbackDelay","stunDuration","knockbackDistance"})typeof(DynamicDoorTrigger).GetField(field,Hidden).SetValue(trap,SessionState.GetFloat(Flag+".Impact."+field,0));
                float windup=Field<float>(trap,"knockbackDelay"),distance=Field<float>(trap,"knockbackDistance"),duration=Field<float>(trap,"stunDuration");
                float expected=windup+.25f+Mathf.Max(.1f,duration-windup-.25f,driver.MinimumStunDuration);
                float began=Time.time;var start=root.transform.position;
                trap.StartCoroutine((IEnumerator)typeof(DynamicDoorTrigger).GetMethod("HammerTrapSequence",Hidden).Invoke(trap,new object[]{root}));
                float deadline=Time.time+2;
                while(new Vector2(root.transform.position.x-start.x,root.transform.position.z-start.z).sqrMagnitude<.00001f)
                {
                    Check(!driver.IsStunned,label+" no premature reaction during hammer wind-up");
                    Check(Time.time<deadline,label+" impact occurs");yield return null;
                }
                Check(driver.IsStunned&&animator.GetCurrentAnimatorStateInfo(0).IsName("Stun")&&animator.GetLayerWeight(1)==0,label+" first knockback frame already displays Stun");
                float impact=Time.time-began;
                Check(impact>=windup-.02f&&impact<windup+Mathf.Max(.1f,Time.deltaTime*3),label+" impact timing unchanged");
                float stamp=Field<float>(driver,"stunStarted"),progress=animator.GetFloat(PlayerAnimator.STUN_TIME),lastGeometry=0;bool duplicated=false;
                while(Field<bool>(trap,"isProcessingTrap"))
                {
                    Check(player.enabled&&GameInput.Instance.HasGameplayActionBlock&&GameInput.Instance.GameplayActionsBlocked,label+" actions remain restricted while camera controller stays active");
                    float next=animator.GetFloat(PlayerAnimator.STUN_TIME);
                    Check(next>=progress-.001f&&next<=1&&Field<float>(driver,"stunStarted")==stamp,label+" one monotonic reaction per impact");progress=next;
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Stun")&&animator.GetLayerWeight(1)==0&&!animator.applyRootMotion,label+" exclusive grounded stun state");
                    if(!duplicated&&Time.time-began>windup+.15f){driver.BeginStun(30);Check(Field<float>(driver,"stunStarted")==stamp,label+" duplicate signal does not restart");duplicated=true;}
                    if(Time.time-lastGeometry>.4f)
                    {
                        Bounds b;Check(IntegratedSkinProfile.TryVisualBounds(profile.transform,out b)&&b.min.y>-.12f&&b.min.y<.2f&&b.size.magnitude<profile.referenceHeight*2,label+" grounded mesh without deformation: floor="+b.min.y+" size="+b.size+" progress="+next+" root="+root.transform.position);
                        Check(cc.isGrounded,label+" grounded capsule");lastGeometry=Time.time;
                    }
                    Check(Time.time-began<expected+.3f,label+" recovery is not extended");yield return null;
                }
                float actual=Time.time-began;
                Check(Mathf.Abs(actual-expected)<.15f&&!driver.IsStunned&&player.enabled&&!GameInput.Instance.GameplayInputBlocked,label+" unchanged lock duration and restored controls");
                Check(Mathf.Abs(Vector3.Distance(new Vector3(start.x,0,start.z),new Vector3(root.transform.position.x,0,root.transform.position.z))-distance)<.08f,label+" unchanged knockback distance");
                yield return new WaitForSeconds(.2f);Check(State(animator,"Idle"),label+" recovery exits to Idle");
                File.AppendAllText(CurrentReport,"PASS "+label+": same-call entry from five states; Stun on first displaced frame at "+impact.ToString("F3")+"s, lock="+actual.ToString("F3")+"s, duplicate-safe, grounded mesh/capsule and recovery.\n");
                Object.Destroy(trap.gameObject);total++;
            }
            root.SetActive(false);Object.Destroy(root);yield return null;
        }
        Check(total==12,"all twelve hammer reactions");
    }
    static IEnumerator SocketChecks()
    {
        Application.runInBackground=true;Time.timeScale=1;yield return null;
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.GetComponent<Player>()!=null).ToArray())
        {
            root.SetActive(true);yield return null;var player=root.GetComponent<Player>();var loader=root.GetComponent<PlayerEquipmentLoader>();
            var cc=root.GetComponent<CharacterController>();cc.enabled=false;root.transform.position=new Vector3(0,1.03f,0);cc.enabled=true;
            foreach(var e in Field<PlayerEquipmentLoader.EquippableModel[]>(loader,"clothesModels").Where(e=>e!=null&&LatestSkinIntegration.Category(e.itemID)!=null))
            {
                Select(loader,e.itemID);yield return new WaitForSeconds(.15f);
                var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.transform.localScale=Vector3.one*2.6f;item.AddComponent<Rigidbody>();var grab=item.AddComponent<GrabbableObject>();
                for(int cycle=0;cycle<2;cycle++)
                {
                    grab.Grab(player.HoldPoint);player.SetHeldObjectSilently(grab);
                    // This is the same synchronous clone timing used by the Sets replenisher.
                    var copy=Object.Instantiate(item);copy.GetComponent<GrabbableObject>().ConfigureInventoryState(false,null,false);
                    Check(copy.GetComponent<Renderer>().enabled&&Vector3.Distance(copy.transform.localScale,Vector3.one*2.6f)<.001f,"replacement inherits original appearance/size");
                    Check(copy.GetComponentsInChildren<GrabbableObject>(true).Length==1,"one replacement item identity");Object.Destroy(copy);
                    yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();CheckGrip(item,player,root.GetComponent<PlayerCarryRig>(),e.itemID);
                    var socket=new GameObject("Non-player altar socket");socket.transform.position=root.transform.position+Vector3.forward*7;
                    grab.ConfigureInventoryState(true,socket.transform,false);player.SetHeldObjectSilently(null);
                    yield return null;yield return new WaitForEndOfFrame();
                    Check(Vector3.Distance(item.transform.position,socket.transform.position)<.01f,"altar socket retains placement");
                    Check(item.GetComponent<Renderer>().enabled&&!root.GetComponent<PlayerCarryRig>().IsCarrying,"altar restores original appearance and clears carry pose state");
                    grab.ConfigureInventoryState(false,null,false);Object.Destroy(socket);
                }
                Check(item.transform.Find("__CarryPresentation")==null&&item.GetComponentsInChildren<CarriedObjectAlignment>(true).Length==1,"recollection retains the original mesh without a resized presentation");
                Object.Destroy(item);File.AppendAllText(LatestSkinIntegration.Reports+"/SocketChecks.txt","PASS "+e.itemID+": repeated pickup, synchronous spawner clone, original scale/visibility, one identity, altar socket ownership, no carry reattachment.\n");
            }
            root.SetActive(false);Object.Destroy(root);yield return null;
        }
    }
    static IEnumerator Run()
    {
        Application.runInBackground=true;Application.targetFrameRate=60;Time.timeScale=1;
        var roots=SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.GetComponent<Player>()!=null).ToArray();
        yield return null;int total=0;
        foreach(var root in roots)
        {
            root.SetActive(true);var player=root.GetComponent<Player>();var cc=root.GetComponent<CharacterController>();var loader=root.GetComponent<PlayerEquipmentLoader>();var rig=root.GetComponent<PlayerCarryRig>();
            yield return null;
            if(total==0)yield return LatestSkinDebugChecks.CharacterPreview(root);
            var entries=Field<PlayerEquipmentLoader.EquippableModel[]>(loader,"clothesModels").Where(e=>e!=null&&LatestSkinIntegration.Category(e.itemID)!=null).ToArray();
            foreach(var entry in entries)
            {
                cc.enabled=false;root.transform.SetPositionAndRotation(new Vector3(0,1.03f,0),Quaternion.identity);cc.enabled=true;player.ResetMotionAfterTeleport();GameInput.Instance.SetMobileMovement(Vector2.zero);
                Select(loader,entry.itemID);yield return new WaitForSeconds(.25f);
                var profile=entry.model.GetComponentInChildren<IntegratedSkinProfile>();var driver=profile.GetComponent<PlayerAnimator>();var animator=profile.GetComponent<Animator>();var label=profile.gender+"_"+profile.category;
                Check(profile.category==LatestSkinIntegration.Category(entry.itemID),label+" ID/category mapping");Check(profile.gameObject.activeInHierarchy&&rig.ActiveSkin==profile,label+" active profile");
                Check(!animator.applyRootMotion&&State(animator,"Idle"),label+" idle/no root motion");
                Check(Mathf.Abs(profile.transform.lossyScale.x-profile.transform.lossyScale.y)<.01f&&Mathf.Abs(profile.transform.lossyScale.y-profile.transform.lossyScale.z)<.01f,label+" uniform scale");
                var start=root.transform.position;yield return new WaitForSeconds(.2f);Check(Vector3.Distance(start,root.transform.position)<.15f,label+" idle no drift");
                player.SetRunningEnabled(false);GameInput.Instance.SetMobileMovement(Vector2.up);yield return new WaitForSeconds(.35f);
                Check(State(animator,"Walk")&&!player.IsRunning(),label+" walk with run disabled");CheckSpeed(cc,player.moveSpeed,label+" walk speed");
                Check(animator.GetFloat(PlayerAnimator.WALK_PLAYBACK)<=Field<Vector2>(driver,"walkPlaybackLimits").y+.01f&&Mathf.Abs(animator.speed-1)<.001f,label+" calibrated walk playback cap / global speed one");
                player.SetRunningEnabled(true);yield return new WaitForSeconds(.3f);Check(!player.IsRunning(),label+" run not immediate");
                yield return new WaitForSeconds(3.05f);Check(player.IsRunning()&&State(animator,"Run"),label+" delayed run");CheckSpeed(cc,player.moveSpeed*1.5f,label+" run speed");
                Check(animator.GetFloat(PlayerAnimator.RUN_PLAYBACK)<=Field<Vector2>(driver,"runPlaybackLimits").y+.01f,label+" calibrated run playback cap");
                GameInput.Instance.MobileJump();yield return new WaitForSeconds(.2f);Check(player.IsJumping()&&State(animator,"Jump"),label+" actual jump");
                var jumpClip=animator.runtimeAnimatorController.animationClips.Single(c=>c.name.Contains("_Jump_Gameplay"));
                Check(Mathf.Abs(animator.GetFloat(PlayerAnimator.JUMP_PLAYBACK)-jumpClip.length/player.JumpFlightDuration)<.001f,label+" jump clip fits existing physics duration");
                float until=Time.time+3;while(player.IsJumping()&&Time.time<until)yield return null;yield return new WaitForSeconds(.2f);Check(cc.isGrounded&&State(animator,"Run"),label+" landing returns to run");
                GameInput.Instance.SetMobileMovement(Vector2.zero);yield return new WaitForSeconds(.25f);Check(State(animator,"Idle")&&!player.IsRunning(),label+" stop");
                yield return LatestSkinDebugChecks.ReleaseAndScroll(root,label);

                var held=GameObject.CreatePrimitive(PrimitiveType.Cube);held.name="Offline held mesh with offset pivot";held.transform.localScale=Vector3.one*2.6f;
                // An off-centre mesh deliberately exercises pivot compensation.
                var filter=held.GetComponent<MeshFilter>();var mesh=Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]+=new Vector3(.35f,.1f,-.2f);mesh.vertices=vertices;mesh.RecalculateBounds();filter.sharedMesh=mesh;
                held.AddComponent<Rigidbody>();var grab=held.AddComponent<GrabbableObject>();grab.Grab(player.HoldPoint);player.SetHeldObjectSilently(grab);
                yield return new WaitForSeconds(.25f);int carry=animator.GetLayerIndex("Carry Upper Body");Check(rig.IsCarrying&&animator.GetLayerWeight(carry)>.95f&&State(animator,"CarryIdle"),label+" carry idle blend");
                yield return new WaitForEndOfFrame();CheckGrip(held,player,rig,label);
                GameInput.Instance.SetMobileMovement(Vector2.up);yield return new WaitForSeconds(.3f);Check(State(animator,"Walk")&&animator.GetLayerWeight(carry)>.95f,label+" carry walk");
                GameInput.Instance.MobileJump();yield return new WaitForSeconds(.2f);Check(State(animator,"Jump")&&animator.GetLayerWeight(carry)>.95f,label+" carry jump");
                yield return new WaitForSeconds(3.05f);Check(State(animator,"Run")&&animator.GetLayerWeight(carry)>.95f,label+" carry run");yield return new WaitForEndOfFrame();CheckGrip(held,player,rig,label);
                Capture(label+"_Carry",player);
                GameInput.Instance.SetMobileMovement(Vector2.zero);grab.Drop();player.SetHeldObjectSilently(null);yield return new WaitForSeconds(.25f);Check(!rig.IsCarrying&&animator.GetLayerWeight(carry)<.05f,label+" drop clears carry pose");
                Check(held.GetComponent<Renderer>().enabled&&Vector3.Distance(held.transform.localScale,Vector3.one*2.6f)<.001f,label+" drop restores original rendering and preserves physics scale");
                var released=held.transform.position;root.transform.position+=Vector3.right*2;yield return new WaitForSeconds(.1f);Check(Vector3.Distance(held.transform.position,rig.anchor.position)>1,label+" dropped object not attached");Object.Destroy(held);Object.Destroy(mesh);

                var visual=GameObject.CreatePrimitive(PrimitiveType.Cylinder);visual.GetComponent<Collider>().enabled=false;visual.name="Offline scroll proxy";visual.transform.SetParent(player.HoldPoint,false);visual.transform.localScale=new Vector3(.5f,.4f,.5f);CarriedObjectAlignment.Follow(visual.transform,player.HoldPoint,true);
                yield return new WaitForSeconds(.25f);Check(rig.IsCarrying&&animator.GetLayerWeight(carry)>.95f,label+" scroll proxy carry");yield return new WaitForEndOfFrame();CheckGrip(visual,player,rig,label);visual.transform.SetParent(null,true);Object.Destroy(visual);yield return new WaitForSeconds(.25f);Check(!rig.IsCarrying,label+" return/place clears scroll carry");

                var trap=new GameObject("Offline hammer stun").AddComponent<DynamicDoorTrigger>();var sequence=(IEnumerator)typeof(DynamicDoorTrigger).GetMethod("HammerTrapSequence",Hidden).Invoke(trap,new object[]{root});trap.StartCoroutine(sequence);
                yield return new WaitForSeconds(.85f);Check(driver.IsStunned&&State(animator,"Stun")&&GameInput.Instance.HasGameplayActionBlock,label+" matching hammer stun after knockback");
                float stunProgress=animator.GetFloat(PlayerAnimator.STUN_TIME), groundedY=root.transform.position.y;
                for(int sample=0;sample<10;sample++)
                {
                    yield return new WaitForSeconds(.25f);
                    float next=animator.GetFloat(PlayerAnimator.STUN_TIME);
                    Check(next>=stunProgress-.001f&&next<=1f,label+" stun never loops/restarts");stunProgress=next;
                    Check(cc.isGrounded&&Mathf.Abs(root.transform.position.y-groundedY)<.08f,label+" capsule remains grounded during stun");
                    Check(animator.GetLayerWeight(carry)<.001f,label+" no conflicting carry layer during stun");
                }
                Check(driver.IsStunned&&State(animator,"Stun"),label+" stun grounded hold respects penalty duration");
                yield return new WaitForSeconds(1.9f);Check(!driver.IsStunned&&player.enabled&&!GameInput.Instance.GameplayInputBlocked,label+" hammer restores controls");Object.Destroy(trap.gameObject);
                yield return new WaitForSeconds(.2f);Check(State(animator,"Idle"),label+" stun exits to idle");
                File.AppendAllText(Report,"PASS "+label+" ID="+entry.itemID+": idle/walk/run delay and speed/jump/landing/stop, carry idle/walk/run/jump, off-centre item alignment, drop/return, actual hammer knockback and full stun cleanup.\n");total++;
            }
            root.SetActive(false);Object.Destroy(root);yield return null;
        }
        Check(total==12,"all 12 category/gender combinations");
    }
    static void CheckSpeed(CharacterController cc,float expected,string label)
    {var velocity=cc.velocity;velocity.y=0;Check(Mathf.Abs(velocity.magnitude-expected)<.2f,label+" expected="+expected+" actual="+velocity.magnitude);}
    static void CheckGrip(GameObject held,Player player,PlayerCarryRig rig,string label)
    {
        var renderers=held.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();Check(renderers.Length>0,label+" held presentation visible");
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);var center=bounds.center;
        Check(Vector3.Distance(center,rig.anchor.position)<.03f,label+" mesh centred on carry anchor; object="+held.name+" center="+center+" anchor="+rig.anchor.position+" distance="+Vector3.Distance(center,rig.anchor.position));var local=player.transform.InverseTransformPoint(center);
        Check(Mathf.Abs(local.x)<.03f&&local.z>=.6f,label+" held object centred in front");
        var skin=rig.ActiveSkin;
        var midpoint=player.transform.InverseTransformPoint((skin.leftHand.position+skin.rightHand.position)*.5f);
        Check(Mathf.Abs(midpoint.x)<.1f,label+" both hands remain centred around the unshrunk item");
        Check(held.GetComponent<Renderer>().enabled&&!held.GetComponentsInChildren<Transform>().Any(t=>t.name=="__CarryPresentation"&&t.gameObject.activeInHierarchy),label+" original mesh stays visible without a reduced-size substitute");
    }
    static void Capture(string name,Player player)
    {
        Directory.CreateDirectory(LatestSkinIntegration.Reports+"/Previews");var camera=Camera.main;
        camera.transform.position=player.transform.position+new Vector3(9,5,14);camera.transform.LookAt(player.transform.position+Vector3.up*1.8f);
        var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(640,640,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();File.WriteAllBytes(LatestSkinIntegration.Reports+"/Previews/"+name+".png",image.EncodeToPNG());
        Object.Destroy(image);camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);
    }
}
public sealed class LatestSkinCheckRunner:MonoBehaviour
{
    void LateUpdate(){var player=Player.LocalInstance;if(player==null||Camera.main==null)return;Camera.main.transform.position=player.transform.position+new Vector3(0,6,-18);Camera.main.transform.LookAt(player.transform.position+Vector3.up*2);}
}
#endif
