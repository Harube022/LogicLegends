#if UNITY_EDITOR
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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Opt-in, offline touch checks for Direct Proofs board and arrows's five recorded Help actions.</summary>
public static class DirectProofsWorldChecks
{
    const string Flag = "DirectProofs.WorldQA";
    const string Report = "Library/DirectProofsWorldChecks.txt";
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Captions = new string[0];
    static Touchscreen touchscreen;
    static int finger;

    public static void BeginHud() { SessionState.SetBool(Flag+".HudOnly",true); Begin(); }

    [MenuItem("Logic Legends/Help/Check Direct Proofs board and arrows Five-Clip Playback")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Exit Play Mode and save the current scene before checking the Help playlist.");
        SessionState.SetString(Flag + ".Scene", SceneManager.GetActiveScene().path);
        SessionState.SetInt(Flag + ".Size", BookMapMobileChecks.GetSize());
        SessionState.SetInt(Flag + ".SafeArea", (int)Crystal.SafeArea.Sim);
        SessionState.SetBool(Flag + ".Persistence", StageCompleteManager.SuppressPersistenceForValidation);
        SessionState.SetBool(Flag + ".Proofs", DirectIndirectProofStageController.SuppressProgressionForValidation);
        SessionState.SetInt(Flag + ".Topic", LevelTimerManager.savedTopicIndex);
        SessionState.SetFloat(Flag + ".Time", LevelTimerManager.savedRemainingTime);
        SessionState.SetBool(Flag + ".Retry", LevelTimerManager.isRespawningFromFail);
        SessionState.SetInt(Flag + ".RetryCount", LevelTimerManager.TryAgainCount);
        SessionState.SetBool(Flag, true);
        File.WriteAllText(Report, "Offline Direct Proofs board and arrows Help playlist checks; Input System touch input.\n");
        EditorSceneManager.OpenScene("Assets/Scenes/Direct&Indirect.unity");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.playModeStateChanged -= Restore;
        EditorApplication.playModeStateChanged += Restore;
    }

    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(SharedProofDecorationsChecks.Flag, false);
        SessionState.SetBool("DirectProofs.BoardReplacementQA",false);
        SessionState.SetBool(Flag, false); SessionState.SetBool(Flag+".HudOnly",false);
        SceneManager.sceneLoaded -= Loaded;
        foreach (string field in new[] { "ValidationTransport", "ValidationUid", "ValidationEmail" })
            typeof(TrialBackendClient).GetField(field, Statics)?.SetValue(null, null);
        StageCompleteManager.SuppressPersistenceForValidation = SessionState.GetBool(Flag + ".Persistence", false);
        DirectIndirectProofStageController.SuppressProgressionForValidation = SessionState.GetBool(Flag + ".Proofs", false);
        LevelTimerManager.savedTopicIndex = SessionState.GetInt(Flag + ".Topic", 0);
        LevelTimerManager.savedRemainingTime = SessionState.GetFloat(Flag + ".Time", -1);
        LevelTimerManager.isRespawningFromFail = SessionState.GetBool(Flag + ".Retry", false);
        typeof(LevelTimerManager).GetField("tryAgainCount", Statics)?.SetValue(null, SessionState.GetInt(Flag + ".RetryCount", 0));
        BookMapMobileChecks.SetSize(SessionState.GetInt(Flag + ".Size", 6));
        Crystal.SafeArea.Sim = (Crystal.SafeArea.SimDevice)SessionState.GetInt(Flag + ".SafeArea", 0);
        string original = SessionState.GetString(Flag + ".Scene", "Assets/Scenes/Main Menu.unity");
        if (!string.IsNullOrEmpty(original)) EditorApplication.delayCall += () => EditorSceneManager.OpenScene(original);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        typeof(TrialBackendClient).GetField("ValidationTransport", Statics)?.SetValue(null,
            (Func<string, string, System.Threading.Tasks.Task<string>>)((route, body) => System.Threading.Tasks.Task.FromResult("{}")));
        typeof(TrialBackendClient).GetField("ValidationUid", Statics)?.SetValue(null, null);
        typeof(TrialBackendClient).GetField("ValidationEmail", Statics)?.SetValue(null, null);
        StageCompleteManager.SuppressPersistenceForValidation = true;
        DirectIndirectProofStageController.SuppressProgressionForValidation = true;
        StageSelectionState.SelectForEditorSpawn(5);
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if ((component.GetType().Namespace ?? "").StartsWith("Photon.Pun.Demo.Cockpit")) component.enabled = false;
        if (Player.LocalInstance == null)
        {
            var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
            avatar.GetComponent<MultiplayerAvatarSync>().enabled = false;
            avatar.GetComponent<PlayerEquipmentLoader>().enabled = false;
            foreach (var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
            avatar.GetComponent<CharacterController>().enabled = true;
            var spawner = UnityEngine.Object.FindFirstObjectByType<LevelSpawner>();
            var spawn = Read(spawner, "spawnPoint") as Transform;
            if (spawn == null) spawn = GameObject.Find("Trial Player Spawn")?.transform;
            if (spawn != null) avatar.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }
        if (UnityEngine.Object.FindFirstObjectByType<DirectProofsWorldQaRunner>() == null)
        {
            var runner = new GameObject("Offline Direct Proofs board and arrows Help Playlist QA").AddComponent<DirectProofsWorldQaRunner>();
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.StartCoroutine(Guard(RunFixture()));
        }
    }

    static IEnumerator Guard(IEnumerator work)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(work);
        while (stack.Count > 0)
        {
            object next;
            try
            {
                var item = stack.Peek();
                if (!item.MoveNext()) { stack.Pop(); continue; }
                next = item.Current;
            }
            catch (Exception error)
            {
                File.AppendAllText(Report, "FAIL " + error + "\n"); Debug.LogException(error);
                RemoveTouchscreen(); EditorApplication.isPlaying = false; yield break;
            }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        Log("ALL DIRECT PROOFS BOARD AND ARROW CHECKS PASSED");
        RemoveTouchscreen(); SceneManager.sceneLoaded -= Loaded; EditorApplication.isPlaying = false;
    }

    static DirectIndirectProofStageController stage;
    static DirectProofsWorldGuidance world;
    static Button hand;
    static IEnumerator RunFixture()
    {
        bool hudOnly=SessionState.GetBool(Flag+".HudOnly",false);
        Application.runInBackground=true;
        touchscreen=InputSystem.AddDevice<Touchscreen>();finger=2100;BookMapMobileChecks.SetSize(6);
        yield return Until(()=>Player.LocalInstance!=null&&GameInput.Instance!=null&&(TeleportManager.Instance==null||!TeleportManager.Instance.IsTransitioning),30,"arrival");
        yield return Until(()=>UnityEngine.Object.FindFirstObjectByType<StageHelpPage>()!=null,25,"first Help");
        yield return StageHelpPlayChecks.CompleteInitialGuide("Direct&Indirect");
        stage=UnityEngine.Object.FindFirstObjectByType<DirectIndirectProofStageController>();world=stage.WorldGuidance;
        hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton");
        Check(stage.State==DirectIndirectProofStageController.TrialState.WaitingForActivation&&!stage.GuidanceBlocksGameplay,"onboarding ends at waiting book");
        if(SessionState.GetBool(SharedProofDecorationsChecks.Flag,false))yield return SharedProofDecorationsChecks.WalkRoutes(stage,false);
        if(!hudOnly)foreach(int size in new[]{6,12,3})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.35f);
            Move(new Vector3(56.7f,stage.Book.position.y,48));yield return new WaitForSecondsRealtime(.1f);
            yield return Board(false,false);yield return Board(false,true);
        }
        BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.4f);
        var catalog=(DirectIndirectProofCatalog)Read(stage,"catalog");
        for(int challenge=1;challenge<=5;challenge++)
        {
            if(stage.Guide.IsIntroduction){stage.Guide.Skip();yield return new WaitForSecondsRealtime(.85f);}
            if(!hudOnly)yield return Introduction(challenge);
            if(challenge==4&&SessionState.GetBool(SharedProofDecorationsChecks.Flag,false))yield return SharedProofDecorationsChecks.WalkRoutes(stage,true);
            int previous=-1;
            for(int question=1;question<=2;question++)
            {
                Check(stage.ChallengeNumber==challenge&&stage.QuestionNumber==question,"two questions required before challenge advancement");
                Check(world.Arrows.All(a=>!a.gameObject.activeInHierarchy),"no arrows while waiting for book");
                Move(stage.Book.position+Vector3.forward*2);yield return null;yield return Tap(hand);
                Check(stage.State==DirectIndirectProofStageController.TrialState.Active&&stage.ActiveVariantIndex!=previous,"book starts selected distinct question");
                previous=stage.ActiveVariantIndex;
                var current=(TrialProofChallenge)Read(stage,"activeChallenge");
                Move(new Vector3(56.7f,stage.Book.position.y,48));yield return new WaitForSecondsRealtime(.35f);
                if(!hudOnly)
                {
                    if(question==1&&SessionState.GetBool("DirectProofs.BoardReplacementQA",false))
                    {
                        foreach(int size in new[]{6,12,3})
                        {
                            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.4f);
                            yield return Board(true,false);
                        }
                        BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.4f);
                    }
                    else yield return Board(true,false);
                }
                if(hudOnly&&question==1)yield return HudMessages(challenge);
                foreach(int available in Enumerable.Range(0,3).Where(i=>world.Arrows[i].gameObject.activeSelf))
                {
                    var center=(Vector3)typeof(DirectIndirectProofStageController).GetMethod("AltarCenter",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{stage.Altars[available]});
                    Check(Math.Abs(world.Arrows[available].position.x-center.x)<.02f&&Math.Abs(world.Arrows[available].position.z-center.z)<.02f,"arrow is centered horizontally above its altar");
                }
                int target=challenge<=2?0:challenge==3?1:2;
                if(challenge==1)
                {
                    Check(world.Arrows[0].gameObject.activeSelf&&world.Arrows[1].gameObject.activeSelf&&!world.Arrows[2].gameObject.activeSelf,"both classification altars have neutral arrows");
                    yield return Interact(0);yield return new WaitForSecondsRealtime(.28f);
                    Check(world.Arrows.All(a=>!a.gameObject.activeSelf),"selecting Direct pops all selection arrows out");
                    yield return Interact(0);yield return new WaitForSecondsRealtime(.28f);
                    Check(stage.SelectedMethod==-1&&world.Arrows[0].gameObject.activeSelf,"deselecting restores Direct arrow");
                    if(hudOnly){Check(stage.Selection.text.Contains("Selection cleared"),"actual deselection feedback is retained");CheckHudMessages();}
                    yield return Interact(1);yield return new WaitForSecondsRealtime(.28f);
                    Check(world.Arrows.All(a=>!a.gameObject.activeSelf),"selecting Indirect pops all selection arrows out");
                    yield return Interact(1);yield return new WaitForSecondsRealtime(.28f);
                    Check(stage.SelectedMethod==-1&&world.Arrows[1].gameObject.activeSelf,"deselecting restores Indirect arrow");
                    yield return Interact(current.correctMethod==TrialProofMethod.Direct?0:1);
                }
                else
                {
                    Check(world.Arrows[target].gameObject.activeSelf,"active ordering altar has an arrow");
                    if(challenge>=4)
                    {
                        var redundant=(TMP_Text)Read(stage,"neutralTabletText");
                        Check(!redundant.transform.parent.gameObject.activeSelf&&stage.InformationBoard.gameObject.activeInHierarchy,"redundant altar tablet retired; main board retained");
                    }
                    yield return Interact(target);yield return new WaitForSecondsRealtime(.35f);
                    Check(stage.OrderingBoard.IsOpen&&!world.Arrows[target].gameObject.activeSelf,"opening ordering target pops its arrow out");
                    var order=stage.OrderingBoard.CurrentOrder;var expected=current.acceptedOrders[0].tabletIds;
                    for(int row=0;row<expected.Length;row++)
                    {
                        int from=Array.IndexOf(order,expected[row]);
                        if(from!=row)yield return Drag(from,row);
                        order=stage.OrderingBoard.CurrentOrder;
                    }
                    if(!(bool)Read(stage,"stepsReordered")){yield return Drag(0,1);yield return Drag(0,1);}
                    if(challenge==5)
                    {
                        var strategies=(Button[])Read(stage.OrderingBoard,"strategyButtons");
                        yield return Tap(strategies[(int)current.correctMethod]);
                    }
                    Check(current.IsCorrect(stage.SelectedMethod,stage.OrderingBoard.CurrentOrder),"real drag handlers create a valid complete proof");
                    yield return Tap(stage.OrderingBoard.Panel.GetComponentsInChildren<Button>().Single(b=>b.name=="Close Altar Board"));
                    yield return new WaitForSecondsRealtime(.3f);
                    Check(!stage.OrderingBoard.IsOpen&&world.Arrows[target].gameObject.activeSelf,"Back to Room deselects target and restores arrow");
                    Check(expected.SequenceEqual(Enumerable.Range(0,5).Select(stage.PlacedTabletId)),"deselecting keeps exact draft placements");
                }
                ScreenCapture.CaptureScreenshot("Library/DirectWorld_Challenge"+challenge+".png");
                Move(stage.Book.position+Vector3.forward*2);yield return null;int before=stage.CorrectCount;
                yield return Tap(hand);yield return new WaitForSecondsRealtime(.1f);
                Check(stage.CorrectCount==before+1,"real book submission validates exactly once");
                Check(world.Arrows.All(a=>!a.gameObject.activeSelf),"completion hides all arrows");
                yield return Until(()=>stage.State!=DirectIndirectProofStageController.TrialState.ChallengeComplete,5,"completion feedback advances to next question or result");
                yield return null;
                if(stage.Guide.IsIntroduction){stage.Guide.Skip();yield return new WaitForSecondsRealtime(.85f);}
                Log("Challenge "+challenge+" question "+question+": actual book, arrows, draft and validation");
            }
        }
        Check(stage.Finished&&stage.CorrectCount==10&&stage.MistakeCount==0,"five challenges still require ten correct questions");
        Check(world.Arrows.All(a=>!a.gameObject.activeSelf)&&!world.ViewButton.gameObject.activeSelf,"stage completion hides guidance");
    }
    static IEnumerator HudMessages(int challenge)
    {
        Move(new Vector3(56.7f,stage.Book.position.y,55));yield return null;
        for(int size=0;size<3;size++)
        {
            BookMapMobileChecks.SetSize(new[]{6,12,3}[size]);yield return new WaitForSecondsRealtime(.35f);
            CheckHudMessages();
            yield return new WaitForEndOfFrame();
            var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
            System.IO.File.WriteAllBytes("Library/DirectHudMessages_"+challenge+"_"+Screen.width+"x"+Screen.height+".png",image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            Log("Challenge "+challenge+" "+Screen.width+"x"+Screen.height+": HUD-width message containers, wrapping, transparent vertical stack and mobile control clearance");
        }
        BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.35f);
    }
    static void CheckHudMessages()
    {
        var hud=(RectTransform)Read(stage.Guide,"hud");Rect previous=Rect(hud),safe=Rect(stage.Guide.SafeArea);
        foreach(var text in new[]{stage.Prompt,stage.Selection})
        {
            text.ForceMeshUpdate();var box=Rect(text.rectTransform);
            Check(text.gameObject.activeInHierarchy,"gameplay message stays visible: "+text.name);
            Check(Math.Abs(box.width-Rect(hud).width)<1.5f&&Math.Abs(box.xMax-Rect(hud).xMax)<1.5f,"message width and right edge follow actual HUD");
            Check(box.yMax<previous.yMin&&previous.yMin-box.yMax<30,"messages stack immediately below the HUD with consistent gaps");
            Check(box.xMin>=safe.xMin-1&&box.xMax<=safe.xMax+1&&box.yMin>=safe.yMin&&box.yMax<=safe.yMax,"messages fit safe screen bounds");
            Check(text.fontSize>=24&&text.textWrappingMode==TextWrappingModes.Normal&&!text.isTextOverflowing&&text.textBounds.size.y<=text.rectTransform.rect.height+1,"readable message wraps without vertical clipping");
            Check(text.GetComponent<UnityEngine.UI.Image>()==null&&!text.raycastTarget,"message is transparent and does not block touch");
            foreach(var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>b.name=="JumpButton"||b.name=="InteractButton"||b==world.ViewButton))
                Check(!box.Overlaps(Rect((RectTransform)button.transform)),"message clears "+button.name);
            previous=box;
        }
        if(stage.Prompt.text.StartsWith("Choose the Direct"))Check(stage.Prompt.textInfo.lineCount>1,"long challenge instruction wraps within HUD width");
    }
    static IEnumerator Introduction(int challenge)
    {
        var guide=stage.Guide;
        yield return new WaitForSecondsRealtime(1.1f);
        var room=GameObject.Find("Stage").GetComponent<Renderer>();var materials=room.sharedMaterials;
        var suspended=GameObject.Find("ChamberDecorations")?.GetComponentsInChildren<Transform>(true)
            .Where(t=>t.name=="Overhead Stone Rib"||t.name=="Suspension Chain"||t.name=="Hanging Chandelier")
            .SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray()??new Renderer[0];
        var activeRenderers=stage.Altars.Where(a=>a.gameObject.activeInHierarchy).SelectMany(a=>a.GetComponentsInChildren<Renderer>()).Where(r=>r.enabled).ToArray();
        float time=stage.SecondsLeft;int correct=stage.CorrectCount,variant=stage.ActiveVariantIndex;
        bool[] seen=(bool[])Read(guide,"seen");seen[challenge-1]=false;
        yield return Until(()=>guide.IsIntroduction,3,"challenge introduction opens");
        Vector3 right=Vector3.ProjectOnPlane(Camera.main.transform.right,Vector3.up).normalized;
        float near=Camera.main.nearClipPlane,far=Camera.main.farClipPlane;
        for(int page=0;page<3;page++)
        {
            float until=Time.realtimeSinceStartup+1.8f;
            while(Time.realtimeSinceStartup<until)
            {
                Check(Vector3.Distance(Camera.main.transform.position,stage.Book.position)<300,"page flips cannot send tutorial framing outside the chamber");
                Check(room.enabled&&room.sharedMaterials.SequenceEqual(materials),"full chamber keeps original materials; no height clipping");
                Check(activeRenderers.All(r=>r.enabled),"active altars remain rendered throughout transitions");
                Check(stage.InformationBoard.GetComponent<Renderer>().enabled,"main board stays rendered");
                Check(suspended.All(r=>!r.enabled),"suspended decorations cannot obscure tutorial objects");
                Check(Vector3.Dot(Camera.main.transform.right,right)>.65f,"overview and close-up retain viewing direction without 180-degree roll");
                Check(Camera.main.nearClipPlane<=near+.01f&&Camera.main.farClipPlane>=far-1,"tutorial preserves safe camera clipping planes");
                MobileInstructionAssetChecks.VerifyVisibleCopy("Direct&Indirect tutorial");
                yield return null;
            }
            Check(Time.timeScale==0&&Math.Abs(stage.SecondsLeft-time)<.01f,"introduction pauses input and countdown");
            Log("Tutorial camera " + challenge+"/"+page+": at "+Camera.main.transform.position+" bounds="+Read(guide,"focusBounds")+" goal="+Read(guide,"goalPosition"));
            yield return new WaitForEndOfFrame();
            CaptureTutorial("Library/DirectIntroduction_"+challenge+"_"+page+".png");
            yield return Tap(guide.NextButton);
        }
        yield return Until(()=>!guide.IsBusy&&guide.IntroductionSeen(challenge)&&UnityEngine.Object.FindObjectsByType<PrelimTutorialCameraRig>(FindObjectsSortMode.None).Length==0,4,"Next finishes introduction and restores gameplay camera; busy="+guide.IsBusy+" seen="+guide.IntroductionSeen(challenge)+" rigs="+UnityEngine.Object.FindObjectsByType<PrelimTutorialCameraRig>(FindObjectsSortMode.None).Length);
        Check(suspended.All(r=>r.enabled),"Next restores all suspended decoration renderers");
        Check(Player.LocalInstance.enabled&&!GameInput.Instance.GameplayInputBlocked&&Time.timeScale==1&&room.sharedMaterials.SequenceEqual(materials),"introduction restores input, environment and timer state");
        Check(stage.State==DirectIndirectProofStageController.TrialState.WaitingForActivation&&stage.CorrectCount==correct&&stage.ActiveVariantIndex==variant&&stage.SecondsLeft==time,"introduction leaves question waiting for real book without progression");
        // An explicit fixture reset exercises Skip during the entry movement too.
        seen[challenge-1]=false;yield return Until(()=>guide.IsIntroduction,3,"repeat fixture introduction");yield return new WaitForSecondsRealtime(.08f);
        var skip=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.GetComponentInChildren<TMP_Text>()!=null&&b.GetComponentInChildren<TMP_Text>().text=="Skip Tutorial");
        yield return Tap(skip);yield return new WaitForSecondsRealtime(1.1f);
        Check(!guide.IsBusy&&guide.IntroductionSeen(challenge)&&Player.LocalInstance.enabled&&!GameInput.Instance.GameplayInputBlocked&&room.sharedMaterials.SequenceEqual(materials)&&UnityEngine.Object.FindObjectsByType<PrelimTutorialCameraRig>(FindObjectsSortMode.None).Length==0,"Skip during transition restores camera, chamber and input");
        Check(suspended.All(r=>r.enabled),"Skip restores all suspended decoration renderers");
        Log("Challenge "+challenge+": all three tutorial views, preserved room materials/lens, consistent heading, real touch Next and Skip cleanup; timer/progress unchanged");
    }
    static void CaptureTutorial(string path)
    {
        var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
        System.IO.File.WriteAllBytes(path,image.EncodeToPNG());
        float min=1,max=0;
        // Ignore the instruction card and Skip button: the world itself must render.
        for(int x=Screen.width*3/10;x<Screen.width*7/10;x+=16)
        for(int y=Screen.height*6/10;y<Screen.height*8/10;y+=16)
        {var p=image.GetPixel(x,y);float l=(p.r+p.g+p.b)/3;min=Mathf.Min(min,l);max=Mathf.Max(max,l);}
        UnityEngine.Object.Destroy(image);
        Check(max-min>.03f,"actual tutorial frame renders the world rather than an empty ground/sky image");
    }
    static IEnumerator Board(bool running,bool interrupt)
    {
        yield return Until(()=>world.ViewButton.gameObject.activeInHierarchy,2,"nearby View Board is offered");
        var state=stage.State;int correct=stage.CorrectCount;int method=stage.SelectedMethod;int variant=stage.ActiveVariantIndex;
        var order=Enumerable.Range(0,5).Select(stage.PlacedTabletId).ToArray();float time=stage.SecondsLeft;
        yield return Tap(world.ViewButton);
        Check(stage.IsViewingBoard&&GameInput.Instance.GameplayInputBlocked&&!Player.LocalInstance.enabled,"main board holds gameplay input");
        if(!interrupt)
        {
            yield return Until(()=>!world.BoardView.Transitioning,3,"smooth board transition completes");
            var text=stage.InformationBoard.GetComponent<TMP_Text>();text.ForceMeshUpdate();
            if(SessionState.GetBool("DirectProofs.BoardReplacementQA",false))
            {
                Check(world.BoardView.informationBoard==stage.InformationBoardDisplay,"board camera frames the replacement model");
                Check(Vector3.Dot(text.transform.forward,Camera.main.transform.forward)>.9f,"camera sees the readable front of the parchment");
                Check(stage.InformationBoardDisplay.GetComponentsInChildren<TMP_Text>().Length==0,"art pack sample labels remain hidden; original dynamic text is authoritative");
            }
            Check(!text.isTextOverflowing,"full main-board text wraps without clipping");
            var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
            Check(corners.Select(p=>Camera.main.WorldToViewportPoint(p)).All(p=>p.z>0&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1),"main board fits camera frame");
            ScreenCapture.CaptureScreenshot("Library/DirectBoard_"+Screen.width+"x"+Screen.height+"_"+stage.ChallengeNumber+".png");
            yield return new WaitForSecondsRealtime(.3f);
        }
        yield return Tap(world.BackButton);
        yield return Until(()=>!stage.IsViewingBoard,3,"Back restores gameplay camera");
        yield return new WaitForSecondsRealtime(.3f); // Allow the normal guidance pop-in to finish.
        Check(Player.LocalInstance.enabled&&!GameInput.Instance.GameplayInputBlocked,"Back restores controls");
        Check(stage.State==state&&stage.CorrectCount==correct&&stage.SelectedMethod==method&&stage.ActiveVariantIndex==variant&&order.SequenceEqual(Enumerable.Range(0,5).Select(stage.PlacedTabletId)),"View Board never starts, submits or modifies answers");
        Check(running?stage.SecondsLeft<time-.2f:Math.Abs(stage.SecondsLeft-time)<.01f,"board view preserves existing countdown rules");
        Log(Screen.width+"x"+Screen.height+": readable main board, touch Back"+(interrupt?" during transition":"")+", input/timer/proof preservation");
    }
    static IEnumerator Interact(int target)
    {
        var altar=stage.Altars[target];
        var center=(Vector3)typeof(DirectIndirectProofStageController).GetMethod("AltarCenter",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{altar});
        Move(new Vector3(center.x,stage.Book.position.y,center.z+3.7f));yield return null;yield return Tap(hand);
    }
    static void Move(Vector3 point)=>PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,point,Quaternion.identity);
    static IEnumerator Drag(int from,int to)
    {
        var cards=(ProofStepDragCard[])Read(stage.OrderingBoard,"cards");
        Vector2 start=Rect((RectTransform)cards[from].transform).center,end=Rect((RectTransform)cards[to].transform).center;
        int id=++finger;InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=start});
        yield return null;yield return null;
        for(int i=1;i<=12;i++)
        {InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=Vector2.Lerp(start,end,i/12f)});yield return new WaitForSecondsRealtime(.02f);}
        InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=end});yield return null;yield return null;
    }
    static IEnumerator Tap(Button button)
    {
        Check(button!=null&&button.gameObject.activeInHierarchy&&button.IsInteractable(),"button available: "+button?.name);
        yield return null;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
        Vector2 point=Rect((RectTransform)button.transform).center;
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"touch reaches "+button.name);
        int id=++finger;InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});yield return null;yield return null;
        InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
    static Rect Rect(RectTransform target)
    {
        var corners=new Vector3[4];target.GetWorldCorners(corners);return UnityEngine.Rect.MinMaxRect(corners.Min(p=>p.x),corners.Min(p=>p.y),corners.Max(p=>p.x),corners.Max(p=>p.y));
    }
    static object Read(object target,string name)=>target?.GetType().GetProperty(name,Members)?.GetValue(target)??target?.GetType().GetField(name,Members)?.GetValue(target);
    static IEnumerator Until(Func<bool> condition,float seconds,string description)
    {float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)yield return null;Check(condition(),description);}
    static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    static void Log(string text)=>File.AppendAllText(Report,"PASS "+text+"\n");
    static void RemoveTouchscreen(){if(touchscreen!=null&&touchscreen.added)InputSystem.RemoveDevice(touchscreen);touchscreen=null;}
}
public sealed class DirectProofsWorldQaRunner : MonoBehaviour { }
#endif
