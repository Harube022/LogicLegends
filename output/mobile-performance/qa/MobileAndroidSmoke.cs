#if LL_MOBILE_QA && DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Development QA package only. Never compiled into ordinary/release builds.
public sealed class MobileAndroidSmoke : MonoBehaviour
{
    static MobileAndroidSmoke instance;
    static readonly string[] Scenes={"TruthTable","RulesOfInference","Setssss","Formal vs. Informal Proofs","Direct&Indirect","Trees"};
    Touchscreen touch;
    int finger=5000;
    string report;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Prepare()
    {
        if(Application.identifier!="com.logiclegends.mobileqa")return;
        SceneManager.sceneLoaded+=Loaded;
        StageSelectionState.Select(2);
    }
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(Player.LocalInstance==null)
        {
            var staging=new GameObject("QA offline player staging");staging.SetActive(false);
            var avatar=Instantiate(Resources.Load<GameObject>("Multiplayer_Male"),staging.transform);
            avatar.GetComponent<MultiplayerAvatarSync>().enabled=false;avatar.GetComponent<PlayerEquipmentLoader>().enabled=false;
            foreach(var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true))skin.gameObject.SetActive(skin.name=="UNIFORM_MALE");
            avatar.transform.SetParent(null);avatar.SetActive(true);Destroy(staging);avatar.GetComponent<CharacterController>().enabled=true;
        }
        if(instance!=null)return;
        instance=new GameObject("Android offline input verification").AddComponent<MobileAndroidSmoke>();DontDestroyOnLoad(instance.gameObject);
        instance.StartCoroutine(instance.Guard(instance.Run()));
    }
    IEnumerator Guard(IEnumerator root)
    {
        var stack=new Stack<IEnumerator>();stack.Push(root);
        while(stack.Count>0)
        {
            bool more;object next=null;
            try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}
            catch(Exception e){Log("FAIL "+e);yield break;}
            if(!more){stack.Pop();continue;}if(next is IEnumerator)stack.Push((IEnumerator)next);else yield return next;
        }
    }
    IEnumerator Run()
    {
        report=Path.Combine(Application.persistentDataPath,"MobileInputSmoke.txt");File.WriteAllText(report,"ANDROID DEVELOPMENT QA. Synthetic Unity Input System fingers; not physical touch-panel verification.\n");
        Log("Report="+report+" | "+SystemInfo.deviceModel+" | "+SystemInfo.operatingSystem+" | "+SystemInfo.processorType+" | "+SystemInfo.graphicsDeviceName+" | RAM="+SystemInfo.systemMemorySize+" MiB");
        var settings=SoloGameSettings.Current.Copy();settings.graphicsQuality=0;settings.lightingQuality=0;settings.frameRate=30;settings.mute=true;SoloGameSettings.Apply(settings);
        touch=InputSystem.AddDevice<Touchscreen>();
        foreach(string scene in Scenes)
        {
            if(SceneManager.GetActiveScene().name!=scene)
            {
                StageSelectionState.Select(TrialCatalog.ForScene(scene).Number);
                Check(SoloStageLoader.Load(scene),"Load accepted");yield return Until(()=>!SoloStageLoader.IsLoading,150,"scene ready");
            }
            yield return Until(()=>Player.LocalInstance!=null&&SoloSettingsRuntime.IsReady(SceneManager.GetActiveScene()),120,"initialization");
            yield return Until(()=>TeleportManager.Instance==null||!TeleportManager.Instance.IsTransitioning,30,"startup fade");
            yield return Until(()=>Page()!=null,20,"arrival Help: "+scene);
            yield return Tap(Page().BeginButton);yield return new WaitForSecondsRealtime(1);
            if(scene!="Trees")
            {
                for(int cycle=0;cycle<2;cycle++)
                {
                    yield return Until(()=>Label("Skip Tutorial")!=null,8,"Skip visible");yield return Tap(Label("Skip Tutorial"));yield return new WaitForSecondsRealtime(1);
                    yield return Until(()=>Label("No")!=null,8,"repeat confirmation");yield return Tap(Label("No"));yield return new WaitForSecondsRealtime(1);
                }
                yield return Tap(Label("Skip Tutorial"));yield return new WaitForSecondsRealtime(1);yield return Until(()=>Label("Yes")!=null,8,"start confirmation");yield return Tap(Label("Yes"));
                yield return new WaitForSecondsRealtime(1);
            }
            for(int i=0;i<3;i++)
            {
                var skip=Label("Skip Tutorial");if(skip==null)break;yield return Tap(skip);yield return new WaitForSecondsRealtime(1);
            }
            yield return Until(()=>Time.timeScale>0&&Player.LocalInstance.enabled&&!GameInput.Instance.GameplayActionsBlocked,10,"tutorial input release");
            var joystick=MobileInputUI.FindJoystick();Check(joystick!=null,"joystick exists");
            // The joystick component is on its ring. Its visual rectangle supplies screen coordinates.
            var rect=(RectTransform)joystick.transform;var at=Point(rect);var bounds=rect.rect;
            var canvas=rect.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var moved=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(bounds.center+new Vector2(bounds.width*.28f,bounds.height*.18f)));
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=at},hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<MobileInputUI>()==joystick,"joystick hit target");
            int id=++finger;Touch(id,UnityEngine.InputSystem.TouchPhase.Began,at);yield return null;yield return null;
            Touch(id,UnityEngine.InputSystem.TouchPhase.Moved,moved);yield return null;yield return null;
            Check(joystick.CurrentInput.magnitude>.3f&&GameInput.Instance.GetMovementVectorNormalized().sqrMagnitude>.1f,"drag reaches movement");
            int jumps=0;EventHandler listener=(s,e)=>jumps++;GameInput.Instance.OnJumpAction+=listener;
            var jump=FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="JumpButton"&&b.gameObject.activeInHierarchy);
            yield return Tap(jump);GameInput.Instance.OnJumpAction-=listener;
            Check(jumps==1&&joystick.CurrentInput.magnitude>.3f,"concurrent jump preserves held drag");
            Touch(id,UnityEngine.InputSystem.TouchPhase.Ended,moved);yield return null;yield return null;
            Check(joystick.CurrentInput.sqrMagnitude<.001f,"drag release");
            yield return Tap(Label("HELP"));yield return Until(()=>Page()!=null,5,"Help reopen");yield return Tap(Page().BeginButton);
            yield return Until(()=>Page()==null&&!GameInput.Instance.GameplayActionsBlocked&&Time.timeScale>0,8,"reopened Help releases input");
            var frames=new List<float>();float until=Time.realtimeSinceStartup+4;
            while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);}
            frames.Sort();Log("PASS "+scene+": Help/two Skip-No cycles/exit, joystick + second-finger jump, one jump callback, Help reopen/exit; "+Screen.width+"x"+Screen.height+" Low 30 target frame median="+frames[frames.Count/2].ToString("F1")+"ms p95="+frames[Mathf.Min(frames.Count-1,(int)(frames.Count*.95f))].ToString("F1")+"ms Unity allocated="+(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()/1048576.0).ToString("F1")+"MiB (emulator, not Infinix)");
        }
        Log("COMPLETE. No real account, question submissions, rewards or production save data used.");
    }
    StageHelpPage Page()=>FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).FirstOrDefault(p=>p.isActiveAndEnabled&&p.BeginButton.gameObject.activeInHierarchy);
    Button Label(string text)=>FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.IsInteractable()&&b.GetComponentInChildren<TMP_Text>()?.text.Trim().Equals(text,StringComparison.OrdinalIgnoreCase)==true);
    IEnumerator Tap(Button button)
    {
        Check(button!=null&&button.IsInteractable(),"button available");var at=Point((RectTransform)button.transform);int id=++finger;
        Touch(id,UnityEngine.InputSystem.TouchPhase.Began,at);yield return null;yield return null;Touch(id,UnityEngine.InputSystem.TouchPhase.Ended,at);yield return null;yield return null;
    }
    static Vector2 Point(RectTransform r){var c=r.GetComponentInParent<Canvas>();return RectTransformUtility.WorldToScreenPoint(c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera,r.TransformPoint(r.rect.center));}
    void Touch(int id,UnityEngine.InputSystem.TouchPhase phase,Vector2 point)=>InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=phase,position=point});
    static IEnumerator Until(Func<bool> ready,float seconds,string message){float until=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Check(ready(),message);}
    static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);}
    void Log(string text){File.AppendAllText(report,text+"\n");Debug.Log("MOBILE_QA "+text);}
}
#endif
