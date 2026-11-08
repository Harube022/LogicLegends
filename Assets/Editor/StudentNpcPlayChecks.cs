using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LogicLegends.Students;
using LogicLegends.Graph;

// Explicit, offline sandbox. Real trial controllers, questions and saves are not loaded.
public static class StudentNpcPlayChecks
{
    const string Report="output/Students/PlayModeChecks.txt";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static Touchscreen touch;
    static int touchId;
    static InputSettings previousInputSettings, isolatedInputSettings;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Hidden).GetValue(target);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,Hidden).SetValue(target,value);
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);File.AppendAllText(Report,"PASS "+message+"\n");}
    [InitializeOnLoadMethod]
    static void RestoreHook(){EditorApplication.playModeStateChanged-=Restore;EditorApplication.playModeStateChanged+=Restore;}
    static void Restore(PlayModeStateChange mode)
    {
        if(mode!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool("Students.QA",false))return;
        SessionState.SetBool("Students.QA",false);
        string previous=SessionState.GetString("Students.PreviousPlayStart","");
        EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        SetResolution(SessionState.GetInt("Students.PreviousResolution",2));
    }
    [MenuItem("Logic Legends/Students/Run isolated Play Mode checks")]
    public static void Begin()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.sceneCount!=1)throw new Exception("Finish the current Play Mode or additive-scene operation first.");
        var original=SceneManager.GetActiveScene();
        var gameView=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
        SessionState.SetInt("Students.PreviousResolution",(int)gameView.GetType().GetProperty("selectedSizeIndex",Hidden).GetValue(gameView,null));
        var actualHand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(b=>b.name=="InteractButton"&&b.image!=null&&b.image.sprite!=null);
        var handSprite=actualHand!=null?actualHand.image.sprite:null;
        if(original.isDirty)
        {
            Directory.CreateDirectory("Assets/Tests/Students/Backups");
            EditorSceneManager.SaveScene(original,"Assets/Tests/Students/Backups/"+original.name+"_BeforeNpcChecks.unity",true);
        }
        SessionState.SetString("Students.PreviousScene",original.path);
        SessionState.SetString("Students.PreviousPlayStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));SessionState.SetBool("Students.QA",true);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        var camera=new GameObject("Main Camera",typeof(Camera));camera.tag="MainCamera";camera.transform.position=new Vector3(0,26,-35);camera.transform.rotation=Quaternion.Euler(27,0,0);
        var light=new GameObject("Preview daylight",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(50,-30,0);
        var controls=new GameObject("Actual hand button preview",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        controls.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=controls.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var hand=new GameObject("InteractButton",typeof(RectTransform),typeof(Image),typeof(Button));hand.transform.SetParent(controls.transform,false);
        var handRect=(RectTransform)hand.transform;handRect.anchorMin=handRect.anchorMax=new Vector2(.85f,.15f);handRect.anchoredPosition=Vector2.zero;handRect.sizeDelta=new Vector2(170,170);
        hand.GetComponent<Image>().sprite=handSprite;hand.GetComponent<Image>().preserveAspect=true;
        for(int i=0;i<5;i++){var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Preview floor "+i;floor.transform.position=new Vector3(i*60,-.25f,0);floor.transform.localScale=new Vector3(42,.5f,42);}
        Directory.CreateDirectory("Assets/Tests/Students");EditorSceneManager.SaveScene(scene,"Assets/Tests/Students/StudentNpcPreview.unity");
        EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(original);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Tests/Students/StudentNpcPreview.unity");
        EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(!SessionState.GetBool("Students.QA",false)||SceneManager.GetActiveScene().name!="StudentNpcPreview")return;
        new GameObject("Student QA runner").AddComponent<StudentNpcQaRunner>().StartCoroutine(Guard());
    }
    static IEnumerator Guard()
    {
        var tasks=new Stack<IEnumerator>();tasks.Push(Run());
        while(tasks.Count>0)
        {
            object next=null;bool more=false;
            try{more=tasks.Peek().MoveNext();if(more)next=tasks.Peek().Current;}
            catch(Exception e)
            {
                var active=StudentNpcDirector.Active;
                File.AppendAllText(Report,"FAILED "+e+"\nState: active="+(active!=null)+", paused="+(active!=null&&active.Paused)+", blocked="+(GameInput.Instance!=null&&GameInput.Instance.GameplayInputBlocked)+", time="+Time.timeScale+", focused="+Application.isFocused+"\n");
                Debug.LogException(e);break;
            }
            if(!more){tasks.Pop();continue;}
            if(next is IEnumerator nested)tasks.Push(nested);else yield return next;
        }
        if(touch!=null){InputSystem.RemoveDevice(touch);touch=null;}
        if(previousInputSettings!=null){InputSystem.settings=previousInputSettings;UnityEngine.Object.Destroy(isolatedInputSettings);}
        EditorApplication.ExitPlaymode();
    }
    static IEnumerator Wait(Func<bool> condition,string message,float seconds=35)
    {float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;Check(condition(),message);}
    static IEnumerator Run()
    {
        Directory.CreateDirectory("output/Students");File.WriteAllText(Report,"Isolated Editor Play Mode; real skin controllers, input and dialogue; synthetic safe floors and trial action source. No account/progress/reward writes.\n");
        Application.runInBackground=true;
        SetResolution(2);
        // Automation may run with another desktop window focused. Clone rather
        // than edit the project's input asset, and route only this sandbox's input.
        previousInputSettings=InputSystem.settings;isolatedInputSettings=UnityEngine.Object.Instantiate(previousInputSettings);
        isolatedInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        isolatedInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=isolatedInputSettings;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        var input=new GameObject("Preview hand input").AddComponent<GameInput>();
        var hand=GameObject.Find("InteractButton").GetComponent<Button>();hand.onClick.AddListener(input.MobileInteract);
        var playerObject=new GameObject("Preview player",typeof(CharacterController));var player=playerObject.AddComponent<Player>();player.enabled=false;playerObject.transform.position=new Vector3(-15,0,-15);
        var profile=playerObject.AddComponent<IntegratedSkinProfile>();profile.gender="Female";profile.category="Common";
        new GameObject("Preview EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        var fake=new GameObject("Read-only trial snapshot").AddComponent<StudentNpcQaSource>();
        var config=UnityEngine.Object.Instantiate(Resources.Load<StudentNpcCatalog>("Students/StudentNpcCatalog"));config.maximumCells=600;config.hintChance=1;config.buildCellsPerFrame=30;
        StudentNpcDirector director=null;
        var areas=Enumerable.Range(0,5).Select(i=>GameObject.Find("Preview floor "+i).transform).ToArray();
        for(int trial=1;trial<=10;trial++)
        {
            fake.State=new StudentTrialState{Trial=trial,Challenge=0,QuestionKey="0",Concept="The intersection contains elements shared by both sets.",Action=new TrialHintAction("qa/collect","Collect an element needed for this question."),Areas=trial==1?areas:Array.Empty<Transform>()};
            director=new GameObject("Preview student director").AddComponent<StudentNpcDirector>();
            typeof(StudentNpcDirector).GetProperty("Config").SetValue(director,config);
            if(trial==9)foreach(var area in areas)
            {
                area.gameObject.AddComponent<GraphVertex>();area.GetComponent<Collider>().enabled=false;
                var floor=new GameObject("Bilog artwork");floor.transform.SetParent(area,false);floor.AddComponent<BoxCollider>();
            }
            yield return Wait(()=>Field<Coroutine>(director,"building")==null&&Field<string>(director,"populationKey")!=null,"Trial "+trial+" population built");
            var students=Field<List<StudentNpcAgent>>(director,"students");
            Check(students.Select(s=>s.Identity).Distinct().Count()==students.Count&&students.All(s=>s.Identity!="Female_Common"),"Trial "+trial+" unique skins and equipped-skin exclusion");
            if(trial==1)
                foreach(var area in areas)Check(students.Count(s=>Mathf.Abs(s.transform.position.x-area.position.x)<25)>=1&&students.Count(s=>Mathf.Abs(s.transform.position.x-area.position.x)<25)<=2,"1–2 in each chamber "+area.name);
            else Check(trial==2||trial==3?students.Count>=2&&students.Count<=3:trial==9?students.Count>=1&&students.Count<=2:students.Count<=2,"Trial "+trial+" spawn count");
            if(trial>=4&&trial!=9)
            {
                var sameQuestionStudents=students.ToArray();fake.State.QuestionKey="second";yield return null;yield return null;
                Check(students.SequenceEqual(sameQuestionStudents),"Trial "+trial+" retains population within a challenge");
                for(int challenge=1;challenge<5;challenge++)
                {
                    var old=students.Select(s=>s.gameObject).ToArray();fake.State.Challenge=challenge;yield return null;
                    yield return Wait(()=>Field<Coroutine>(director,"building")==null,"Trial "+trial+" challenge "+(challenge+1)+" rebuilt");
                    Check(old.All(g=>g==null)&&students.Count<=2,"Trial "+trial+" previous population cleaned, new count 0–2");
                }
            }
            if(trial==2||trial==3)
            {
                var ids=students.Select(s=>s.GetInstanceID()).ToArray();var before=students.Select(s=>s.transform.position).ToArray();
                yield return new WaitForSecondsRealtime(5);
                Check(students.Where((s,i)=>Vector3.Distance(s.transform.position,before[i])>1).Any(),"Trial "+trial+" actually roams");
                if(trial==2)
                {
                    var walker=students[0];Set(walker,"rest",0f);
                    yield return Wait(()=>Field<List<int>>(walker,"route").Count>Field<int>(walker,"step"),"Walking route available for obstruction test");
                    var route=Field<List<int>>(walker,"route");int step=Field<int>(walker,"step");var target=walker.Map.Nodes[route[step]].Point;
                    var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Temporary NPC obstruction";obstacle.transform.position=target+Vector3.up*3;
                    obstacle.transform.localScale=new Vector3(2.4f,6,2.4f);Physics.SyncTransforms();int previous=walker.ObstructionReplans;
                    yield return Wait(()=>walker.ObstructionReplans>previous,"NPC detects obstruction and replans",8);
                    UnityEngine.Object.Destroy(obstacle);yield return null;
                }
                fake.State.Challenge=1;yield return null;yield return null;
                Check(students.Select(s=>s.GetInstanceID()).SequenceEqual(ids),"Trial "+trial+" retains same students between challenges");
                var student=students[0];player.transform.position=student.transform.position-student.transform.forward*3.3f;player.transform.rotation=student.transform.rotation;
                Set(student,"rest",15f);yield return null;
                Camera.main.transform.position=student.transform.position+new Vector3(0,8,-14);Camera.main.transform.LookAt(student.transform.position+Vector3.up*3);
                yield return new WaitForSecondsRealtime(.4f);
                ScreenCapture.CaptureScreenshot("output/Students/InteractionBubble_Trial"+trial+".png");yield return new WaitForEndOfFrame();
                Check(Field<HandThoughtBubble>(student,"bubble").HandIcon.sprite!=null,"Thought bubble reuses actual hand artwork");
                var prompt=Field<HandThoughtBubble>(student,"bubble");var promptCorners=new Vector3[4];prompt.Rect.GetWorldCorners(promptCorners);
                float promptWidth=Vector3.Distance(Camera.main.WorldToScreenPoint(promptCorners[0]),Camera.main.WorldToScreenPoint(promptCorners[1]));
                // Corner 0->1 is height; both axes remain bounded on the phone.
                Check(promptWidth>=70&&promptWidth<120,"Expanded student hand prompt uses enlarged mobile-readable height");
                Check(prompt.Expansion>.95f&&prompt.Instruction.alpha>.95f,"Enlarged hand instruction is fully expanded in range");
                yield return Tap(hand);yield return null;
                var dialogue=director.GetComponent<StudentNpcDialogue>();Check(dialogue.IsOpen&&!input.GameplayInputBlocked&&Time.timeScale==1,"Hand opens head bubble without blocking movement or changing time");
                var text=dialogue.Text;
                Check(text.color.b>.6f&&text.color.r<.1f&&text.text.Contains(fake.State.Action.Message),"Hint is blue and uses current action");
                Check(!director.HintAvailable(student),"Hint consumed before dialogue can reopen");
                input.SetMobileMovement(Vector2.right);Check(input.GetMovementVectorNormalized().sqrMagnitude>.5f,"Mobile movement remains available during speech");input.SetMobileMovement(Vector2.zero);
                string first=text.text;float progress=dialogue.PopProgress;
                for(int rapid=0;rapid<5;rapid++)input.MobileInteract();
                Check(dialogue.Text.text==first&&dialogue.PopProgress==progress&&director.GetComponentsInChildren<Canvas>(true).Count(c=>c.name=="Student speech bubbles")==1,"Rapid taps preserve a single bubble and animation");
                yield return new WaitForSecondsRealtime(.5f);
                Check(student.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Speaking student reaches Idle while the player remains free");
                Check(student.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.enabled),"Speaking student remains visible");
                CheckBubble(dialogue,"Hint");ScreenCapture.CaptureScreenshot("output/Students/HeadBubbleHint_Trial"+trial+".png");yield return new WaitForEndOfFrame();
                player.transform.position=student.transform.position+Vector3.forward*(config.talkRange+1);
                yield return null;yield return null;Check(!dialogue.IsOpen&&dialogue.PopProgress<1,"Leaving range runs the pop-out controller");
                yield return Wait(()=>!dialogue.IsVisible&&!student.IsTalking,"Pop-out releases the speaking NPC",2);
                Check(!input.GameplayInputBlocked&&Time.timeScale==1,"Range exit preserves gameplay input and countdown state");
                Set(student,"rest",15f);player.transform.position=student.transform.position-student.transform.forward*3.3f;player.transform.rotation=student.transform.rotation;yield return null;
                var carried=new GameObject("QA carried item").AddComponent<GrabbableObject>();Set(carried,"isHeld",true);Set(player,"heldObject",carried);
                yield return Tap(hand);yield return null;
                Check(dialogue.IsOpen,"Repeat conversation available");
                text=dialogue.Text;
                Check(text.color==Color.black&&StudentDialogueBank.Lines(student.Identity).Contains(text.text),"Repeat conversation uses the skin's supplied text in black");
                yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot("output/Students/HeadBubbleChat_Trial"+trial+".png");yield return new WaitForEndOfFrame();
                dialogue.CloseImmediate();Check(player.GetHeldObject()==carried&&carried.IsHeld,"Conversation preserves carried item without dropping it");Set(player,"heldObject",null);UnityEngine.Object.Destroy(carried.gameObject);
                fake.State.Challenge=2;yield return null;Check(director.HintAvailable(student),"New challenge resets hint eligibility");
                input.SetGameplayInputBlocked(true);fake.State.Action=fake.State.Action.Suspend(true);yield return null;yield return null;
                Check(students.All(s=>s.GetComponentsInChildren<Renderer>().All(r=>!r.enabled)),"Student models hide during board/tutorial modal views");
                input.SetGameplayInputBlocked(false);fake.State.Action=new TrialHintAction("qa/collect","Collect an element needed for this question.");yield return null;yield return null;
                Check(students.All(s=>s.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.enabled)),"Student models restore after modal views");
                player.transform.position=new Vector3(-15,0,-15);
            }
            if(trial==9)
            {fake.State.Challenge=1;yield return null;yield return null;Check(students.Count==0,"Graph challenge 2 has no students");}
            UnityEngine.Object.Destroy(director.gameObject);yield return null;yield return null;
            foreach(var area in areas)
            {
                var vertex=area.GetComponent<GraphVertex>();if(vertex!=null)UnityEngine.Object.Destroy(vertex);
                var floor=area.Find("Bilog artwork");if(floor!=null)UnityEngine.Object.Destroy(floor.gameObject);
                area.GetComponent<Collider>().enabled=true;
            }
            yield return null;
        }
        // Evaluate the actual matching Idle/Walk controller on every prepared costume.
        fake.State.Trial=2;fake.State.Challenge=5;
        director=new GameObject("Skin controller preview owner").AddComponent<StudentNpcDirector>();typeof(StudentNpcDirector).GetProperty("Config").SetValue(director,config);director.enabled=false;
        Set(director,"state",fake.State);
        typeof(StudentNpcDirector).GetProperty("Paused").SetValue(director,false);
        player.transform.SetPositionAndRotation(Vector3.back*3.3f,Quaternion.identity);
        Camera.main.transform.position=new Vector3(0,8,-14);Camera.main.transform.LookAt(Vector3.up*3);
        var dummyMap=new StudentWalkMap(config,new List<Vector3>(),new List<Bounds>());
        for(int i=0;i<config.skins.Length;i++)
        {
            var root=new GameObject("Preview "+config.skins[i].id);root.transform.position=new Vector3(0,0,0);var actor=root.AddComponent<StudentNpcAgent>();actor.Configure(director,config.skins[i],i,dummyMap);actor.enabled=false;
            var anim=root.GetComponentInChildren<Animator>();var driver=root.GetComponentInChildren<PlayerAnimator>();
            driver.DriveStudentMotion(3.2f);yield return new WaitForSecondsRealtime(1.1f);
            Check(anim.GetBool("IsWalking")&&!anim.applyRootMotion&&anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"),config.skins[i].id+" matching Walk plays without root motion");
            driver.DriveStudentMotion(config.runSpeed,true);yield return new WaitForSecondsRealtime(.65f);
            Check(anim.GetBool("IsRunning")&&anim.GetCurrentAnimatorStateInfo(0).IsName("Run"),config.skins[i].id+" matching existing Run plays");
            Check(Mathf.Abs(driver.PlanarAnimationSpeed-config.runSpeed)<.01f&&anim.GetFloat(PlayerAnimator.RUN_PLAYBACK)>0,config.skins[i].id+" run playback follows measured NPC speed");
            driver.DriveStudentMotion(config.walkSpeed);yield return new WaitForSecondsRealtime(.45f);
            Check(!anim.GetBool("IsRunning")&&anim.GetCurrentAnimatorStateInfo(0).IsName("Walk"),config.skins[i].id+" blends back from Run to Walk");
            foreach(var bone in root.GetComponentsInChildren<Transform>())Check(!float.IsNaN(bone.position.x)&&!float.IsInfinity(bone.position.y),config.skins[i].id+" finite pose "+bone.name);
            driver.DriveStudentMotion(0);yield return new WaitForSecondsRealtime(.4f);Check(!anim.GetBool("IsWalking"),config.skins[i].id+" returns to Idle");
            var speech=director.GetComponent<StudentNpcDialogue>();
            foreach(string supplied in StudentDialogueBank.Lines(config.skins[i].id))
            {
                actor.enabled=true;speech.Show(actor,supplied,false);yield return new WaitForSecondsRealtime(.25f);
                Check(speech.IsOpen&&speech.Text.text==supplied&&speech.Text.color==Color.black,"Exact supplied line displayed for "+config.skins[i].id);
                CheckBubble(speech,config.skins[i].id);speech.CloseImmediate();actor.enabled=false;
            }
            UnityEngine.Object.Destroy(root);yield return null;
        }
        // Wide phone and narrow portrait checks use the longest supplied line
        // for each skin. Keep text size fixed rather than shrinking to fit.
        foreach(int resolution in new[]{8,1})
        {
            SetResolution(resolution);yield return new WaitForSecondsRealtime(.4f);
            for(int i=0;i<config.skins.Length;i++)
            {
                var root=new GameObject("Layout "+config.skins[i].id);var actor=root.AddComponent<StudentNpcAgent>();actor.Configure(director,config.skins[i],i,dummyMap);
                var speech=director.GetComponent<StudentNpcDialogue>();string longest=StudentDialogueBank.Lines(config.skins[i].id).OrderByDescending(s=>s.Length).First();
                speech.Show(actor,longest,false);yield return new WaitForSecondsRealtime(.25f);
                CheckBubble(speech,config.skins[i].id+" / "+Screen.width+"x"+Screen.height);
                if(i==config.skins.Length-1){ScreenCapture.CaptureScreenshot("output/Students/SpeechBubble_"+Screen.width+"x"+Screen.height+".png");yield return new WaitForEndOfFrame();}
                speech.CloseImmediate();UnityEngine.Object.Destroy(root);yield return null;
            }
        }
        yield return ElevationRoaming(director,config,player);
        UnityEngine.Object.Destroy(director.gameObject);UnityEngine.Object.Destroy(config);
        File.AppendAllText(Report,"COMPLETE — Editor sandbox, simulated mobile hand taps and all sixty supplied dialogue lines; physical-device checks remain separate.\n");Debug.Log("STUDENT_NPC_PLAY_CHECKS_PASS");
    }
    static IEnumerator ElevationRoaming(StudentNpcDirector director,StudentNpcCatalog config,Player player)
    {
        SetResolution(2);yield return null;
        var fixture=new GameObject("Isolated elevation/navigation fixture");
        Action<string,Vector3,Vector3> box=(name,position,scale)=>{var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(fixture.transform);g.transform.position=position;g.transform.localScale=scale;};
        box("Lower field",new Vector3(360,-.25f,0),new Vector3(40,.5f,40));
        box("Raised field",new Vector3(360,2.75f,45),new Vector3(40,.5f,26));
        for(int i=0;i<6;i++)box("Walkable tread "+i,new Vector3(370,(i+1)*.25f,21+i*2),new Vector3(6,(i+1)*.5f,2));
        var ramp=new GameObject("Walkable slope");ramp.transform.SetParent(fixture.transform);
        var mesh=new Mesh();mesh.vertices=new[]{new Vector3(350,0,20),new Vector3(358,0,20),new Vector3(350,3,32),new Vector3(358,3,32)};mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();ramp.AddComponent<MeshCollider>().sharedMesh=mesh;
        box("Obstacle",new Vector3(360,3,2),new Vector3(3,6,3));
        var hazard=new Bounds(new Vector3(349,0,-10),new Vector3(5,4,5));
        var map=new StudentWalkMap(config,new List<Vector3>(),new List<Bounds>{hazard},null,SceneManager.GetActiveScene());
        Physics.SyncTransforms();yield return map.Build(new Vector3(360,0,-15));
        Check(map.Ready&&map.Nodes.Count>150,"Elevation fixture scans broad lower and upper field");
        Check(map.Nodes.All(n=>!hazard.Contains(n.Point)),"Roaming avoids water even over a supporting floor");
        var path=new List<int>();int lower=map.Nearest(new Vector3(360,0,-15)),upper=map.Nearest(new Vector3(360,3,48));
        Check(map.Path(lower,upper,path),"Connected field route climbs a walkable elevation");
        foreach(bool reverse in new[]{false,true})
        {
            var a=reverse?upper:lower;var b=reverse?lower:upper;
            Check(map.Path(a,b,path),reverse?"Route descends to lower field":"Route ascends to upper field");
            var previous=map.Nodes[a].Point;
            foreach(int index in path)
            {
                var end=map.Nodes[index].Point;int count=Mathf.CeilToInt(Vector3.Distance(previous,end)/.12f);var begin=previous;
                for(int k=1;k<=count;k++)
                {var desired=Vector3.Lerp(begin,end,(float)k/count);if(!map.GroundedStep(previous,desired,out var foot))throw new Exception("Blocked elevation substep "+previous+" -> "+desired);previous=foot;}
            }
            Check(Mathf.Abs(previous.y-map.Nodes[b].Point.y)<.1f,"Every movement-sized ascent/descent substep stays grounded");
        }
        // Exercise both the actual stairs and slope rather than only A*'s preferred route.
        foreach(float x in new[]{354f,370f})foreach(bool reverse in new[]{false,true})
        {
            Vector3 previous=new Vector3(x,reverse?3:0,reverse?34:18);
            for(int i=1;i<=160;i++)
            {
                var desired=new Vector3(x,previous.y,Mathf.Lerp(reverse?34:18,reverse?18:34,i/160f));
                if(!map.GroundedStep(previous,desired,out var foot))throw new Exception("Stairs/slope movement blocked "+previous+" -> "+desired);previous=foot;
            }
            Check(Mathf.Abs(previous.y-(reverse?0:3))<.1f,"Smooth grounded "+(reverse?"descent":"ascent")+" at "+(x==354?"slope":"stairs"));
        }
        player.transform.position=new Vector3(340,0,-18);
        var actor=new GameObject("Long trip actual student").AddComponent<StudentNpcAgent>();actor.transform.position=map.Nodes[lower].Point;actor.Configure(director,config.skins[0],0,map);
        map.Path(lower,upper,path);Field<List<int>>(actor,"route").AddRange(path);Set(actor,"step",0);Set(actor,"rest",0f);Set(actor,"longTrip",true);Set(actor,"tripDeadline",Time.time+80);
        yield return Wait(()=>actor.RunSegments>0,"Long outdoor trip uses existing Run animation",12);
        Check(actor.MovementSpeed>config.walkSpeed,"Long trip accelerates above walk speed");
        actor.PauseForConversation(player.transform);yield return null;
        Check(actor.MovementSpeed==0&&!actor.IsRunning&&actor.IsTalking,"Interaction immediately stops running and enters conversation Idle");actor.Resume();
        Check(!actor.IsTalking,"Closing dialogue releases roaming after a running segment");
        UnityEngine.Object.Destroy(actor.gameObject);UnityEngine.Object.Destroy(fixture);UnityEngine.Object.Destroy(mesh);yield return null;
    }

    static void SetResolution(int index)
    {
        var window=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
        window.GetType().GetProperty("selectedSizeIndex",Hidden).SetValue(window,index,null);
    }
    static void CheckBubble(StudentNpcDialogue dialogue,string name)
    {
        var rect=dialogue.BubbleRect;var safe=(RectTransform)rect.parent;
        var surface=rect.GetComponent<Image>().color;
        Check(surface.a>=.85f&&surface.a<1&&Mathf.Abs(surface.r-surface.g)<.05f,name+" translucent gray dialogue surface with black/blue text");
        Check(rect.rect.width<=safe.rect.width*.441f&&rect.rect.height<=safe.rect.height*.621f,name+" controlled maximum bubble size");
        Check(dialogue.Text.fontSize>=34&&dialogue.Text.textWrappingMode==TextWrappingModes.Normal,name+" readable wrapped text without shrinking");
        Check(!dialogue.GetComponentsInChildren<Button>().Any(),name+" has no parchment-panel exit or duplicate interaction button");
        var corners=new Vector3[4];rect.GetWorldCorners(corners);var bounds=new Vector3[4];safe.GetWorldCorners(bounds);
        Check(corners[0].x>=bounds[0].x-1&&corners[0].y>=bounds[0].y-1&&corners[2].x<=bounds[2].x+1&&corners[2].y<=bounds[2].y+1,name+" bubble stays inside safe area");
        var face=Camera.main.WorldToScreenPoint(dialogue.Speaker.FacePosition);
        Check(!Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y).Contains(face),name+" does not cover the speaker's face");
        dialogue.Text.ForceMeshUpdate();Check(dialogue.Text.textInfo.characterCount>0,name+" text mesh renders");
    }
    static IEnumerator Tap(Button button)
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)button.transform;Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,button.name+" has an unobstructed mobile raycast");
        if(touch==null)touch=InputSystem.AddDevice<Touchscreen>();int id=++touchId;
        string buttonName=button.name;int clicks=0;UnityEngine.Events.UnityAction observed=()=>clicks++;button.onClick.AddListener(observed);
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,position=point,phase=UnityEngine.InputSystem.TouchPhase.Began});yield return new WaitForSecondsRealtime(.08f);yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,position=point,phase=UnityEngine.InputSystem.TouchPhase.Ended});yield return new WaitForSecondsRealtime(.08f);yield return null;
        File.AppendAllText(Report,"Touch "+id+" "+buttonName+" clicks="+clicks+", update="+InputSystem.settings.updateMode+", focus="+Application.isFocused+"\n");
        if(button!=null)button.onClick.RemoveListener(observed);
    }
}
public sealed class StudentNpcQaRunner : MonoBehaviour { }
public sealed class StudentNpcQaSource : MonoBehaviour,IStudentTrialSource
{public StudentTrialState State;public StudentTrialState StudentState()=>State??new StudentTrialState{Ready=false};}
