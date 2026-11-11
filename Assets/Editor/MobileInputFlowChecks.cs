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

// Real saved scenes + shared loader + Input System finger events. Never answers puzzles.
public static class MobileInputFlowChecks
{
    const string Flag="MobileInput.FlowChecks", Report="output/mobile-performance/InputFlowChecks.txt";
    static readonly string[] Scenes={"TruthTable","RulesOfInference","Setssss","Formal vs. Informal Proofs","Direct&Indirect","Trees"};
    static readonly string[] Histories={"FunctionsRelations.LastVariant.","LogicLegends.Induction.LastVariant.","LogicLegends.ProofsLibrary.LastVariant.","LogicLegends.Graph.LastVariant.","LogicLegends.Trees.LastVariant."};
    static Touchscreen touch;
    static InputSettings original, isolated;
    static Keyboard keyboard;
    static int finger=3000;
    public static void Begin()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Flag+".Start",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Flag+".Background",Application.runInBackground);
        SessionState.SetInt(Flag+".Size",BookMapMobileChecks.GetSize());SessionState.SetInt(Flag+".Safe",(int)Crystal.SafeArea.Sim);
        foreach(var prefix in Histories)for(int i=0;i<5;i++){string key=prefix+i;SessionState.SetBool(Flag+key,PlayerPrefs.HasKey(key));SessionState.SetInt(Flag+key+".Value",PlayerPrefs.GetInt(key));}
        SessionState.SetBool(Flag,true);Directory.CreateDirectory("output/mobile-performance");Directory.CreateDirectory("output/AndroidUIAudit");
        File.WriteAllText(Report,"EDITOR ONLY: six saved trials, direct/normal async entries, real Input System touch events. No authenticated account, answers or reward events.\n");
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/TruthTable.unity");EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Hook(){EditorApplication.playModeStateChanged-=Restore;EditorApplication.playModeStateChanged+=Restore;}
    static void Restore(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Flag,false))return;
        SessionState.SetBool(Flag,false);SceneManager.sceneLoaded-=Loaded;
        SessionState.SetBool(AndroidTrialUiChecks.Flag,false);
        if(keyboard!=null)InputSystem.EnableDevice(keyboard);
        if(original!=null){InputSystem.settings=original;UnityEngine.Object.DestroyImmediate(isolated);}
        if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);
        BookMapMobileChecks.SetSize(SessionState.GetInt(Flag+".Size",2));Crystal.SafeArea.Sim=(Crystal.SafeArea.SimDevice)SessionState.GetInt(Flag+".Safe",0);
        Application.runInBackground=SessionState.GetBool(Flag+".Background",false);
        string path=SessionState.GetString(Flag+".Start","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        foreach(var prefix in Histories)for(int i=0;i<5;i++){string key=prefix+i;if(SessionState.GetBool(Flag+key,false))PlayerPrefs.SetInt(key,SessionState.GetInt(Flag+key+".Value",0));else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Isolate()
    {
        if(!SessionState.GetBool(Flag,false))return;
        var f=BindingFlags.Static|BindingFlags.NonPublic;
        typeof(TrialBackendClient).GetField("ValidationTransport",f).SetValue(null,(Func<string,string,System.Threading.Tasks.Task<string>>)((a,b)=>System.Threading.Tasks.Task.FromResult("{}")));
        typeof(TrialBackendClient).GetField("ValidationUid",f).SetValue(null,null);typeof(TrialBackendClient).GetField("ValidationEmail",f).SetValue(null,null);
        Application.runInBackground=true;StageSelectionState.SelectForEditorSpawn(2);SceneManager.sceneLoaded+=Loaded;
    }
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(Player.LocalInstance==null)
        {
            var staging=new GameObject("Offline input QA staging");staging.SetActive(false);
            var avatar=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"),staging.transform);
            avatar.GetComponent<MultiplayerAvatarSync>().enabled=false;avatar.GetComponent<PlayerEquipmentLoader>().enabled=false;
            foreach(var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true))skin.gameObject.SetActive(skin.name=="UNIFORM_MALE");
            avatar.transform.SetParent(null);avatar.SetActive(true);UnityEngine.Object.Destroy(staging);avatar.GetComponent<CharacterController>().enabled=true;
        }
        if(UnityEngine.Object.FindFirstObjectByType<MobileInputFlowRunner>()!=null)return;
        var runner=new GameObject("Offline mobile input checks").AddComponent<MobileInputFlowRunner>();UnityEngine.Object.DontDestroyOnLoad(runner);runner.StartCoroutine(Guard(Run()));
    }
    static IEnumerator Guard(IEnumerator work)
    {
        var stack=new Stack<IEnumerator>();stack.Push(work);
        while(stack.Count>0)
        {
            bool more;object next=null;
            try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}
            catch(Exception e){Log("FAIL "+e);EditorApplication.isPlaying=false;yield break;}
            if(!more){stack.Pop();continue;}if(next is IEnumerator)stack.Push((IEnumerator)next);else yield return next;
        }
    }
    static IEnumerator Run()
    {
        original=InputSystem.settings;isolated=UnityEngine.Object.Instantiate(original);isolated.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        isolated.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings=isolated;
        keyboard=Keyboard.current;if(keyboard!=null&&keyboard.enabled)InputSystem.DisableDevice(keyboard);else keyboard=null;
        touch=InputSystem.AddDevice<Touchscreen>();
        for(int mode=0;mode<2;mode++)foreach(string scene in Scenes)
        {
            if(SceneManager.GetActiveScene().name!=scene||mode==1)
            {
                StageSelectionState.SelectForEditorSpawn(TrialCatalog.ForScene(scene).Number);
                if(mode==0){Check(SoloStageLoader.Load(scene),"normal async load accepted");yield return Until(()=>!SoloStageLoader.IsLoading,60,"shared loading finishes");}
                else yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/"+scene+".unity",new LoadSceneParameters(LoadSceneMode.Single));
            }
            BookMapMobileChecks.SetSize(2);
            yield return Until(()=>Player.LocalInstance!=null&&SoloSettingsRuntime.IsReady(SceneManager.GetActiveScene()),30,"stage initialization");
            yield return Until(()=>TeleportManager.Instance==null||!TeleportManager.Instance.IsTransitioning,12,"startup camera/fade finishes");
            yield return Until(()=>Page()!=null,12,"arrival Help visible: "+scene);
            Log("ARRIVAL "+scene+" "+(mode==0?"normal":"direct")+" blocked="+GameInput.Instance.GameplayInputBlocked+" loading="+GameInput.Instance.LoadingInputBlocked+" scale="+Time.timeScale);
            SessionState.SetBool(AndroidTrialUiChecks.Flag,true);SessionState.SetBool(AndroidTrialUiChecks.Flag+".Baseline",false);SessionState.SetBool(AndroidTrialUiChecks.Flag+".Extras",false);SessionState.SetBool(AndroidTrialUiChecks.Flag+".Reentry",mode==1);
            yield return StageHelpPlayChecks.CompleteInitialGuide(scene);
            yield return Until(()=>Time.timeScale>0&&Player.LocalInstance.enabled&&!GameInput.Instance.GameplayActionsBlocked,8,"Help/tutorial input recovery: "+scene);
            foreach(int size in new[]{2,6})
            {
                BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.2f);
                Check(EventSystem.current!=null&&UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(e=>e.isActiveAndEnabled)==1,"one EventSystem");
                yield return DragAndJump(scene);
                var help=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.IsInteractable()&&b.GetComponentInChildren<TMP_Text>()?.text.Trim().Equals("HELP",StringComparison.OrdinalIgnoreCase)==true);
                Check(help!=null,"Help button available: "+scene);yield return Tap(help);yield return Until(()=>Page()!=null,3,"Help reopens");
                Check(GameInput.Instance.GameplayInputBlocked,"Help owns its input block");yield return Tap(Page().BeginButton);
                yield return Until(()=>Page()==null&&!GameInput.Instance.GameplayActionsBlocked&&Time.timeScale>0&&Player.LocalInstance.enabled,5,"reopened Help releases input");
                Log("PASS "+scene+" "+(mode==0?"normal":"direct")+" "+Screen.width+"x"+Screen.height+": drag + concurrent jump + Help touch/exit; no stuck input");
            }
        }
        Log("COMPLETE. Six normal/direct entries, two landscape sizes, tutorial Skip/No cycles, Help reopening, independent touch pointers.");EditorApplication.isPlaying=false;
    }
    static StageHelpPage Page()=>UnityEngine.Object.FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).FirstOrDefault(p=>p.isActiveAndEnabled&&p.BeginButton.gameObject.activeInHierarchy);
    static IEnumerator DragAndJump(string scene)
    {
        var joystick=MobileInputUI.FindJoystick();Check(joystick!=null&&joystick.isActiveAndEnabled,"joystick active");
        var field=typeof(MobileInputUI).GetField("joystickBackground",BindingFlags.Instance|BindingFlags.NonPublic);var rect=(RectTransform)field.GetValue(joystick);
        var at=Point(rect,Vector2.zero);var moved=Point(rect,new Vector2(rect.rect.width*.28f,rect.rect.height*.18f));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=at},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<MobileInputUI>()==joystick,"joystick raycast target; top="+(hits.Count>0?hits[0].gameObject.name:"none"));
        int id=++finger;Touch(id,UnityEngine.InputSystem.TouchPhase.Began,at);yield return null;yield return null;
        Touch(id,UnityEngine.InputSystem.TouchPhase.Moved,moved);yield return null;yield return null;
        Check(joystick.CurrentInput.magnitude>.3f&&GameInput.Instance.GetMovementVectorNormalized().sqrMagnitude>.1f,"actual drag reaches GameInput: "+scene);
        var jump=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="JumpButton"&&b.gameObject.activeInHierarchy);
        int jumps=0;EventHandler listener=(s,e)=>jumps++;GameInput.Instance.OnJumpAction+=listener;
        yield return Tap(jump);GameInput.Instance.OnJumpAction-=listener;
        Check(jumps==1&&joystick.CurrentInput.magnitude>.3f,"second finger triggers one jump without releasing drag");
        Touch(id,UnityEngine.InputSystem.TouchPhase.Ended,moved);yield return null;yield return null;
        Check(joystick.CurrentInput.sqrMagnitude<.001f,"owning finger releases movement");
    }
    static Vector2 Point(RectTransform rect,Vector2 offset)
    {
        var canvas=rect.GetComponentInParent<Canvas>();return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center+offset));
    }
    static IEnumerator Tap(Button button)
    {
        Check(button!=null&&button.IsInteractable(),"button tappable");var at=Point((RectTransform)button.transform,Vector2.zero);int id=++finger;
        Touch(id,UnityEngine.InputSystem.TouchPhase.Began,at);yield return null;yield return null;Touch(id,UnityEngine.InputSystem.TouchPhase.Ended,at);yield return null;yield return null;
    }
    static void Touch(int id,UnityEngine.InputSystem.TouchPhase phase,Vector2 point)=>InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=phase,position=point});
    static IEnumerator Until(Func<bool> ready,float seconds,string reason){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Check(ready(),reason);}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Log(string text)=>File.AppendAllText(Report,text+"\n");
}
public sealed class MobileInputFlowRunner:MonoBehaviour{}
#endif
