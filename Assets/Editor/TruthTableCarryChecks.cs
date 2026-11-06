#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Actual TruthTable scenes, real quizzes/inventory/physics; account writes and rewards are isolated.</summary>
public static class TruthTableCarryChecks
{
    const string Folder="Assets/Editor/__TruthTableCarryQA";
    public const string MenuPath=Folder+"/Main Menu.unity";
    const string Report="Library/TruthTableCarryChecks.txt",Uid="truth-carry-qa";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const BindingFlags Statics=BindingFlags.Static|BindingFlags.NonPublic;
    static Touchscreen touch;static int finger=900;
    static readonly Dictionary<TruthBlock,Vector3> sizes=new Dictionary<TruthBlock,Vector3>(),scales=new Dictionary<TruthBlock,Vector3>();
    static T Find<T>()where T:Object=>Object.FindFirstObjectByType<T>();
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden).GetValue(o);
    static void SetBackend(string name,object value)=>typeof(TrialBackendClient).GetField(name,Statics).SetValue(null,value);
    static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
    public static string PreviewArmReach()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode.");
        var scene=EditorSceneManager.NewPreviewScene();var lines=new List<string>();
        try
        {
            foreach(var path in LatestSkinIntegration.BasePlayers)
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);
                var player=root.GetComponent<Player>();var rig=root.GetComponent<PlayerCarryRig>();rig.anchor=player.GetHoldPoint();
                typeof(PlayerCarryRig).GetField("player",Hidden).SetValue(rig,player);
                foreach(var skin in root.GetComponentsInChildren<IntegratedSkinProfile>(true))
                {
                    for(var t=skin.transform;t!=null;t=t.parent)t.gameObject.SetActive(true);
                    var clips=skin.GetComponent<Animator>().runtimeAnimatorController.animationClips;
                    var carry=clips.First(c=>c.name.Contains("CarryIdle"));carry.SampleAnimation(skin.gameObject,0);
                    rig.SelectSkin(skin);
                    var upper=skin.GetComponentsInChildren<Transform>().Where(t=>{var p=AnimationUtility.CalculateTransformPath(t,skin.transform);return p.Contains("Spine")&&!p.Contains("Neck")&&!p.Contains("Head");}).ToArray();
                    var positions=upper.Select(t=>t.localPosition).ToArray();var rotations=upper.Select(t=>t.localRotation).ToArray();
                    var item=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("36067833529ba71479501f67d2464d57")),scene);
                    var grab=item.GetComponent<GrabbableObject>();grab.ConfigureInventoryState(true,rig.anchor,false);player.SetHeldObjectSilently(grab);
                    typeof(PlayerCarryRig).GetField("handWeight",Hidden).SetValue(rig,1f);
                    foreach(string action in new[]{"Walk","Run","Jump"})
                    {
                        var clip=clips.First(c=>c.name.EndsWith("_"+action+"_Gameplay"));
                        for(int sample=0;sample<=24;sample++)
                        {
                            clip.SampleAnimation(skin.gameObject,clip.length*sample/24);
                            for(int i=0;i<upper.Length;i++){upper[i].localPosition=positions[i];upper[i].localRotation=rotations[i];}
                            rig.RefreshAnchor();item.GetComponent<CarriedObjectAlignment>().Apply();
                            var left=root.transform.InverseTransformPoint(skin.leftHand.position);var right=root.transform.InverseTransformPoint(skin.rightHand.position);
                            if(Mathf.Abs(left.x+right.x)>.12f||Mathf.Abs(left.y-right.y)>.08f||Mathf.Abs(left.z-right.z)>.08f)
                                throw new InvalidOperationException(skin.gender+"_"+skin.category+" "+action+" sample "+sample+" L="+left+" R="+right);
                        }
                    }
                    lines.Add("PASS "+skin.gender+"_"+skin.category+": 25 samples each of Walk, Run and Jump; symmetric full-size support, original bone lengths.");
                    Object.DestroyImmediate(item);player.SetHeldObjectSilently(null);
                }
                Object.DestroyImmediate(root);
            }
            lines.Add("ALL 12 SKINS / 900 LOCOMOTION POSES PASSED.");
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);File.WriteAllLines("Library/TruthCarryPoseReach.txt",lines);}
        return string.Join("\n",lines);
    }
    public static void PrepareMenuFixture()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Use saved Edit Mode scenes.");
        if(AssetDatabase.IsValidFolder(Folder))throw new InvalidOperationException("Reserved menu fixture already exists; no files were replaced.");
        AssetDatabase.CreateFolder("Assets/Editor","__TruthTableCarryQA");SessionState.SetBool("TruthCarry.OwnMenu",true);
        var original=SceneManager.GetActiveScene();var source=EditorSceneManager.OpenScene("Assets/Scenes/Main Menu.unity",OpenSceneMode.Additive);
        try{EditorSceneManager.SaveScene(source,MenuPath,true);}finally{EditorSceneManager.CloseScene(source,true);}
        var scene=EditorSceneManager.OpenScene(MenuPath,OpenSceneMode.Additive);
        try
        {
            // Keep the actual menu presentation, removing application/account code
            // before its Awake can execute. Production scene and settings stay intact.
            foreach(var root in scene.GetRootGameObjects())foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true).Reverse().ToArray())
            {
                if(b==null)continue;var ns=b.GetType().Namespace??"";
                if(ns.StartsWith("UnityEngine.UI")||ns.StartsWith("UnityEngine.EventSystems")||ns.StartsWith("UnityEngine.InputSystem.UI")||ns.StartsWith("TMPro"))continue;
                Object.DestroyImmediate(b);
            }
            EditorSceneManager.SaveScene(scene);
        }
        finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(original);}
    }
    public static void CleanupMenuFixture()
    {
        if(!SessionState.GetBool("TruthCarry.OwnMenu",false))return;
        string full=Path.GetFullPath(Folder),assets=Path.GetFullPath(Application.dataPath)+Path.DirectorySeparatorChar;
        if(!full.StartsWith(assets,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Fixture path left Assets.");
        AssetDatabase.DeleteAsset(MenuPath);
        if(Directory.Exists(Folder)&&!Directory.EnumerateFileSystemEntries(Folder).Any())AssetDatabase.DeleteAsset(Folder);
        SessionState.SetBool("TruthCarry.OwnMenu",false);
    }
    public static IEnumerator Run()
    {
        bool stackOnly=SessionState.GetBool("Help.TruthCarryStackOnly",false);
        sizes.Clear();scales.Clear();
        File.WriteAllText(Report,"TruthTable carry/session regression. Real TruthTable scene and source-menu presentation copy (application/backend scripts removed). No live account writes or completion rewards.\n");
        var previous=InputSystem.settings;var settings=Object.Instantiate(previous);settings.hideFlags=HideFlags.HideAndDontSave;
        settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings=settings;
        touch=InputSystem.AddDevice<Touchscreen>();
        SetBackend("ValidationUid",Uid);SetBackend("ValidationEmail","sngbdsa137@gmail.com");
        SetBackend("ValidationTransport",(Func<string,string,Task<string>>)((path,json)=>Task.FromResult(path=="/trials/access"?JsonUtility.ToJson(new TrialBackendClient.Access{uid=Uid,admin=true}):"{}")));
        AdminAuthorization.Refresh();AdminAuthorization.Tick();
        try
        {
            int visits=stackOnly?1:3;
            for(int visit=0;visit<visits;visit++)
            {
                if(visit>0)
                {
                    StageSelectionState.SelectForEditorSpawn(2);
                    yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/TruthTable.unity",new LoadSceneParameters(LoadSceneMode.Single));
                    yield return Wait(()=>Player.LocalInstance!=null&&Find<TruthTableTutorial>()!=null&&Find<TruthTableTutorial>().IsOpen,"arrival Help",15);
                    yield return (IEnumerator)typeof(StageHelpGameplayRecorder).GetMethod("Prepare",Statics).Invoke(null,new object[]{"TruthTable"});
                }
                yield return FreshDoors(visit);
                var blocks=WorldBlocks();
                foreach(var b in blocks){scales[b]=b.transform.lossyScale;var mesh=b.GetComponent<MeshFilter>();sizes[b]=Vector3.Scale(mesh.sharedMesh.bounds.size,b.transform.lossyScale);}
                foreach(var b in blocks.Take(2))b.CollectBlock();yield return new WaitForSeconds(.25f);
                Check(InventoryManager.Instance.CarriedCount==2&&Player.LocalInstance.GetComponent<PlayerCarryRig>().IsCarrying,"arrival "+visit+": collection immediately carries two real identities");
                if(visit==visits-1)break;
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MenuPath,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
                Check(InventoryManager.Instance==null&&TruthTableDoorInteraction.Active==null&&TruthBlock.ActiveBlocks.Count==0,"menu visit "+visit+": inventory, doors and blocks release all scene references");
            }
            if(!stackOnly)File.WriteAllText("Library/TruthTableCarryReentryChecks.txt",File.ReadAllText(Report)+"ALL THREE ENTRIES AND EIGHTEEN HOUSE QUIZZES PASSED.\n");
            yield return SkinsAndWeights();
            yield return Submission();
            File.AppendAllText(Report,"ALL REQUESTED SKINS / FOUR STACK SIZES / SUBMISSION CHECKS PASSED. Physical Android and live authenticated accounts are separate checks.\n");
        }
        finally
        {
            Move(Vector2.zero);if(touch!=null)InputSystem.RemoveDevice(touch);InputSystem.settings=previous;Object.Destroy(settings);
            foreach(string f in new[]{"ValidationUid","ValidationEmail"})SetBackend(f,null);
            SetBackend("ValidationTransport",(Func<string,string,Task<string>>)((p,j)=>Task.FromResult("{}")));AdminAuthorization.Refresh();
            for(int i=1;i<=10;i++)PlayerPrefs.DeleteKey("TrialRewardOutbox.v1."+Uid+"."+i);PlayerPrefs.Save();
        }
    }
    static TruthBlock[] WorldBlocks()=>TruthBlock.ActiveBlocks.Where(b=>b.BelongsTo(Find<TruthTableDoorInteraction>())).ToArray();
    static IEnumerator FreshDoors(int visit)
    {
        yield return Wait(()=>Player.LocalInstance!=null&&GameInput.Instance!=null,"player/input",15);
        var owner=Find<TruthTableDoorInteraction>();var answered=Field<bool[]>(owner,"answered");var opened=Field<bool[]>(owner,"doorOpened");
        Check(answered.All(a=>!a)&&opened.All(a=>!a)&&InventoryManager.Instance.CarriedCount==0,"arrival "+visit+": all six houses and inventory are fresh");
        var book=owner.TutorialBook;At(book.position+Vector3.forward*3);yield return Tap(Button("InteractButton"));
        Check(owner.IsStageRunning,"arrival "+visit+": hand activates the real book");yield return new WaitForSeconds(.1f);
        foreach(var b in WorldBlocks())Check(!owner.IsBlockAvailable(b)&&!b.GetComponentsInChildren<HandThoughtBubble>(true).Any(x=>x.gameObject.activeInHierarchy),"closed doors conceal block prompts");
        var bubbles=Field<HandThoughtBubble[]>(owner,"doorBubbles");
        Check(bubbles.Length==6&&bubbles.All(b=>b!=null&&b.GetComponentInParent<TruthBlock>()==null),"six door bubbles remain attached to houses");
        for(int i=0;i<6;i++)
        {
            var door=owner.GetTutorialDoor(i);At(door.bounds.center-Vector3.up*1.5f+door.transform.forward*2);
            yield return Tap(Button("InteractButton"));Check(owner.IsQuizOpen,"arrival "+visit+" house "+i+": hand opens quiz");
            float time=owner.GetComponent<TruthTableStageClock>().RemainingSeconds;
            var question=Field<TruthTableDoorQuestionBank.Question[]>(owner,"assignedQuestions")[i];int answer=i==5?(question.correctChoice+1)%4:question.correctChoice;
            yield return Tap(Field<Button[]>(owner,"answerButtons")[answer]);
            if(i==5)
            {
                owner.SubmitAnswer(answer);
                Check(Mathf.Abs(time-owner.GetComponent<TruthTableStageClock>().RemainingSeconds-10)<.03f,"wrong quiz applies its existing ten-second penalty once");
            }
            yield return Tap(Field<Button>(owner,"closeButton"));yield return Wait(()=>opened[i],"house opens",4);
            Check(!bubbles[i].gameObject.activeSelf,"answered house hides its door prompt");
        }
        Check(answered.All(a=>a)&&opened.All(a=>a),"arrival "+visit+": all six houses work");
    }
    static IEnumerator SkinsAndWeights()
    {
        var player=Player.LocalInstance;var root=player.gameObject;var cc=player.GetComponent<CharacterController>();var inventory=InventoryManager.Instance;
        Check(Field<float[]>(inventory,"carryingSpeedMultipliers").SequenceEqual(new[]{.95f,.88f,.8f,.75f}),"requested 95/88/80/75 percent weight configuration");
        foreach(var b in inventory.CarriedBlocks.ToArray())b.ReturnToOrigin(false);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="QA Only Carry Physics Lane";floor.transform.position=new Vector3(2000,499.5f,2000);floor.transform.localScale=new Vector3(500,1,500);
        var originalCamera=Camera.main;originalCamera.tag="Untagged";originalCamera.enabled=false;
        var camera=new GameObject("QA Carry Camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=40;
        var preview=root.GetComponent<AdminCharacterOverride>();if(preview==null)preview=root.AddComponent<AdminCharacterOverride>();
        try
        {
            foreach(int skin in new[]{11,2}.Concat(Enumerable.Range(0,12).Where(i=>i!=11&&i!=2)))
            {
                Check(preview.Select(skin),"test Admin selects "+AdminCharacterOverride.Catalog.skins[skin].name);
                var label=preview.SelectedName;At(new Vector3(2000,501.03f,2000));player.ResetMotionAfterTeleport();player.SetRunningEnabled(true);
                camera.transform.SetPositionAndRotation(player.transform.position+new Vector3(0,6,-18),Quaternion.Euler(12,0,0));
                var blocks=WorldBlocks();Check(blocks.Length==4,label+": exactly four original question blocks");
                for(int count=1;count<=4;count++)
                {
                    blocks[count-1].CollectBlock();yield return new WaitForSeconds(.25f);
                    Check(inventory.CarriedCount==count&&player.GetComponent<TruthTableCarryStack>().Count==count,label+": automatic stack size "+count);
                    VerifyStack(player,inventory,label);
                    int selected=Field<int>(inventory,"selectedSlotIndex");inventory.SetSelectedSlotDirectly(selected);inventory.SetSelectedSlotDirectly(selected);
                    Check(inventory.HasBlockSelected()&&inventory.CarriedBlocks.All(b=>b.gameObject.activeInHierarchy),label+": repeated selection cannot hide carried blocks");
                    Move(Vector2.up);yield return new WaitForSeconds(.25f);player.OnRun();yield return new WaitForSeconds(.1f);
                    Vector3 v=cc.velocity;v.y=0;
                    Check(!player.IsRunning()&&Mathf.Abs(v.magnitude-player.moveSpeed*inventory.CarrySpeedMultiplier)<.15f,label+": weight speed "+count+" and no run bypass");Move(Vector2.zero);
                    VerifyStack(player,inventory,label+" walking "+count);
                }
                var fifth=Object.Instantiate(blocks[0].gameObject);fifth.transform.SetParent(null,true);
                Check(inventory.TryPickupBlock(fifth.GetComponent<TruthBlock>().value,fifth.GetComponent<TruthBlock>())<0&&inventory.CarriedCount==4,label+": fifth identity rejected");Object.Destroy(fifth);
                yield return new WaitForSeconds(.3f);
                var rig=player.GetComponent<PlayerCarryRig>();var profile=rig.ActiveSkin;var animator=profile.GetComponent<Animator>();
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("CarryIdle"),label+": stable full-body Carry idle");
                var position=rig.anchor.localPosition;var size=VisibleBounds(inventory.CarriedBlocks[0]).size;
                for(int sample=0;sample<12;sample++){yield return null;Check(Vector3.Distance(position,rig.anchor.localPosition)<.005f&&Vector3.Distance(size,VisibleBounds(inventory.CarriedBlocks[0]).size)<.005f,label+": no idle socket or scale shaking");}
                Check(!TorsoIntersects(profile,inventory.CarriedBlocks),label+": carried blocks clear torso mesh");
                Capture(label,camera,player);
                Move(Vector2.right);yield return new WaitForSeconds(.5f);VerifyStack(player,inventory,label+" turning");
                Move(Vector2.up);yield return new WaitForSeconds(3.3f);Check(!player.IsRunning(),label+": sustained run input stays weighted");
                GameInput.Instance.MobileJump();yield return new WaitForSeconds(.2f);Check(player.IsJumping(),label+": jumping works while carrying");VerifyStack(player,inventory,label+" jumping");
                yield return Wait(()=>cc.isGrounded,"land",4);Move(Vector2.zero);yield return new WaitForSeconds(.2f);
                float y=player.transform.position.y;var dropped=player.GetHeldObject().GetComponent<TruthBlock>();inventory.DropSelectedBlock();yield return new WaitForSeconds(.4f);
                Check(inventory.CarriedCount==3&&player.transform.position.y-y<.08f,label+": drop compacts the stack without player lift");VerifyStack(player,inventory,label+" compacted");
                dropped.ReturnToOrigin(false);foreach(var b in inventory.CarriedBlocks.ToArray())b.ReturnToOrigin(false);
                yield return new WaitForSeconds(.25f);Check(inventory.CarriedCount==0&&inventory.CarrySpeedMultiplier==1&&!player.IsRunningSuppressed&&!rig.IsCarrying,label+": empty restores normal movement/pose");
                Move(Vector2.up);yield return new WaitForSeconds(3.4f);Check(player.IsRunning(),label+": empty-handed running still unlocks normally");Move(Vector2.zero);yield return new WaitForSeconds(.2f);
            }
        }
        finally{Move(Vector2.zero);preview.RestoreEquipped();originalCamera.tag="MainCamera";originalCamera.enabled=true;Object.Destroy(camera.gameObject);Object.Destroy(floor);}
    }
    static IEnumerator Submission()
    {
        Move(Vector2.zero);var puzzle=Find<DynamicLogicPuzzle>();var phase=Field<TruthTableChallengePhase>(puzzle,"challengePhase");
        var answers=Field<bool[]>(phase,"currentAnswers");var all=WorldBlocks().ToList();var order=new List<TruthBlock>();
        foreach(bool value in answers){var b=all.First(x=>x.value==value);all.Remove(b);order.Add(b);}
        int t=order.FindIndex(b=>b.value),f=order.FindIndex(b=>!b.value);var temp=order[t];order[t]=order[f];order[f]=temp;
        var inventory=InventoryManager.Instance;foreach(var b in order)b.CollectBlock();yield return new WaitForSeconds(.25f);
        foreach(var b in order){puzzle.TryPlace(b,0);yield return new WaitForSeconds(.2f);}
        Check(inventory.CarriedCount==0,"placing four blocks removes their stack identities");
        var clock=Find<TruthTableStageClock>();clock.SetPlayerAtBoard(true);float before=clock.RemainingSeconds;puzzle.SubmitCurrentColumn();yield return new WaitForSeconds(.25f);
        Check(inventory.CarriedCount==4&&Mathf.Abs(before-clock.RemainingSeconds-20)<.03f&&phase.ColumnNumber==1,"incorrect column keeps the question, restores four visible blocks and deducts twenty seconds");
        VerifyStack(Player.LocalInstance,inventory,"incorrect return");
        foreach(bool value in answers)
        {
            var b=inventory.CarriedBlocks.First(x=>x.value==value);int slot=Array.FindIndex(Field<InventorySlot[]>(inventory,"slots"),s=>s.physicalBlocks.Contains(b));
            inventory.SetSelectedSlotDirectly(slot);puzzle.TryPlace(Player.LocalInstance.GetHeldObject().GetComponent<TruthBlock>(),0);yield return new WaitForSeconds(.15f);
        }
        puzzle.SubmitCurrentColumn();yield return new WaitForSeconds(.25f);
        Check(phase.ColumnNumber==2&&inventory.CarriedCount==0,"correct retry advances once with no leftover carry penalty");
    }
    static void VerifyStack(Player player,InventoryManager inventory,string label)
    {
        float previous=float.MinValue;
        foreach(var block in inventory.CarriedBlocks)
        {
            var b=VisibleBounds(block);var local=player.transform.InverseTransformPoint(b.center);
            Check(Mathf.Abs(local.x)<.02f&&local.z>1&&local.y>previous,label+": visible block centred and ordered vertically");previous=local.y;
            var size=SizeInPlayerSpace(block,player);
            Check(Vector3.Distance(size,sizes[block])<.03f&&Vector3.Distance(block.transform.lossyScale,scales[block])<.001f,label+": original visible and physical world size "+size);
            Check(block.GetComponentsInChildren<Collider>().All(c=>!c.enabled)&&block.GetComponent<Rigidbody>().isKinematic&&block.GetComponent<Rigidbody>().interpolation==RigidbodyInterpolation.None,label+": no carried physics conflict");
        }
        if(inventory.CarriedCount>0)
        {
            var profile=player.GetComponent<PlayerCarryRig>().ActiveSkin;
            var left=player.transform.InverseTransformPoint(profile.leftHand.position);var right=player.transform.InverseTransformPoint(profile.rightHand.position);
            Check(Mathf.Abs((left.x+right.x)*.5f)<.08f&&Mathf.Abs(left.y-right.y)<.08f&&Mathf.Abs(left.z-right.z)<.08f,label+": both hands symmetrically support the centred bottom block; L="+left+" R="+right);
            var centre=player.transform.InverseTransformPoint(VisibleBounds(inventory.CarriedBlocks[0]).center);
            Check(left.x<-.2f&&right.x>.2f&&Mathf.Abs((left.y+right.y)*.5f-centre.y)<.3f,label+": full-size block is supported between both hands");
        }
    }
    static Bounds VisibleBounds(TruthBlock block)
    {var rs=block.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&!(r is ParticleSystemRenderer)&&r.GetComponentInParent<TMPro.TMP_Text>()==null).ToArray();if(rs.Length==0)throw new Exception("Carried block is invisible");var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    static Vector3 SizeInPlayerSpace(TruthBlock block,Player player)
    {
        bool found=false;Bounds result=new Bounds();
        foreach(var r in block.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.GetComponentInParent<TMPro.TMP_Text>()==null))
        {
            var mesh=r.GetComponent<MeshFilter>();if(mesh==null||mesh.sharedMesh==null)continue;var b=mesh.sharedMesh.bounds;
            for(int i=0;i<8;i++){var p=player.transform.InverseTransformPoint(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));if(!found){result=new Bounds(p,Vector3.zero);found=true;}else result.Encapsulate(p);}
        }
        return result.size;
    }
    static bool TorsoIntersects(IntegratedSkinProfile profile,IReadOnlyList<TruthBlock> blocks)
    {
        var bounds=blocks.Select(VisibleBounds).ToArray();var mesh=new Mesh();
        try{foreach(var renderer in profile.GetComponentsInChildren<SkinnedMeshRenderer>())
        {renderer.BakeMesh(mesh,true);var weights=renderer.sharedMesh.boneWeights;var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)
        {if(i>=weights.Length)continue;var w=weights[i];string bone=renderer.bones[w.boneIndex0].name;if(!bone.Contains("Spine")&&!bone.Contains("Hips"))continue;var p=renderer.transform.TransformPoint(vertices[i]);if(bounds.Any(b=>b.Contains(p)))return true;}mesh.Clear();}}finally{Object.Destroy(mesh);}return false;
    }
    static void Capture(string name,Camera camera,Player player)
    {
        Directory.CreateDirectory("Library/TruthCarryPreviews");camera.transform.position=player.transform.position+new Vector3(13,8,23);camera.transform.LookAt(player.transform.position+Vector3.up*5.8f);
        var old=RenderTexture.active;var target=RenderTexture.GetTemporary(640,640,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();File.WriteAllBytes("Library/TruthCarryPreviews/"+name+".png",image.EncodeToPNG());Object.Destroy(image);camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);
        camera.transform.SetPositionAndRotation(player.transform.position+new Vector3(0,6,-18),Quaternion.Euler(12,0,0));
    }
    static void At(Vector3 point){var p=Player.LocalInstance;var cc=p.GetComponent<CharacterController>();cc.enabled=false;p.transform.SetPositionAndRotation(point,Quaternion.identity);cc.enabled=true;p.ResetMotionAfterTeleport();Physics.SyncTransforms();}
    static void Move(Vector2 value){var ui=Find<MobileInputUI>();if(ui!=null)typeof(MobileInputUI).GetField("inputVector",Hidden).SetValue(ui,value);GameInput.Instance?.SetMobileMovement(value);}
    static Button Button(string name)=>Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name==name);
    static IEnumerator Tap(Button b)
    {
        Check(b!=null&&b.gameObject.activeInHierarchy&&b.interactable,"touch target available: "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;
        var point=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"touch ray reaches "+b.name);
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
    static IEnumerator Wait(Func<bool> ready,string label,float seconds)
    {float by=Time.realtimeSinceStartup+seconds;while(!ready()){if(Time.realtimeSinceStartup>by)throw new InvalidOperationException("Timeout: "+label);yield return null;}}
}
#endif
