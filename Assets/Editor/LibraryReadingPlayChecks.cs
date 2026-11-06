#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>All library books, isolated milestones, real pointer/touch and animation cancellation.</summary>
public static class LibraryReadingPlayChecks
{
    const string Flag="Library.ReaderQA", Report="Library/LibraryReadingPlayChecks.txt";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static Touchscreen touch; static int finger;
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden).GetValue(o);
    static void Check(bool okay,string message){if(!okay)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
    [MenuItem("Logic Legends/Library/Check Shared Reading and Page Turns")]
    public static void Begin() { Begin("PRELIM"); }
    public static void Begin(string fromPeriod)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty&&SceneManager.GetActiveScene().path!="Assets/Scenes/LogicGarden.unity")throw new InvalidOperationException("Exit Play Mode and save the scene first.");
        SessionState.SetString(Flag+".Scene",SceneManager.GetActiveScene().path);
        SessionState.SetInt(Flag+".Size",BookMapMobileChecks.GetSize());SessionState.SetInt(Flag+".Safe",(int)Crystal.SafeArea.Sim);
        SessionState.SetString(Flag+".From",fromPeriod);
        SessionState.SetBool(Flag,true);
        if(fromPeriod=="PRELIM"||!File.Exists(Report))File.WriteAllText(Report,"Shared library reader: isolated Editor profile; no real progress/rewards. Simulated touch, not an Android device.\n");
        else File.WriteAllLines(Report,File.ReadAllLines(Report).TakeWhile(l=>!l.StartsWith("FAIL")));
        // Entering Play Mode on the same open scene preserves its unsaved Editor
        // buffer. Never reload it just to run these isolated tests.
        if(SceneManager.GetActiveScene().path!="Assets/Scenes/LogicGarden.unity")EditorSceneManager.OpenScene("Assets/Scenes/LogicGarden.unity");
        EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Restore;EditorApplication.playModeStateChanged+=Restore;}
    static void Restore(PlayModeStateChange mode)
    {
        if(mode!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Flag,false))return;
        SessionState.SetBool(Flag,false);SceneManager.sceneLoaded-=Loaded;LogicGardenProgress.ValidationProfile=null;
        BookMapMobileChecks.SetSize(SessionState.GetInt(Flag+".Size",6));Crystal.SafeArea.Sim=(Crystal.SafeArea.SimDevice)SessionState.GetInt(Flag+".Safe",0);
        EditorApplication.delayCall+=()=>{var path=SessionState.GetString(Flag+".Scene","Assets/Scenes/LogicGarden.unity");if(SceneManager.GetActiveScene().path!=path)EditorSceneManager.OpenScene(path);};
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Bootstrap()
    {
        if(!SessionState.GetBool(Flag,false))return;
        LogicGardenProgress.ValidationProfile="codex.libraryReading.validation";LogicGardenProgress.ResetForNewGame();
        LogicGardenProgress.CompleteControlsTutorial();LogicGardenProgress.CompleteConversation();LogicGardenProgress.VisitLibrary();LogicGardenProgress.SeeIntroduction();LogicGardenProgress.CompleteLibraryTour();
        SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;
    }
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="LogicGarden")return;SceneManager.sceneLoaded-=Loaded;
        var avatar=scene.GetRootGameObjects().Single(g=>g.name=="Player");
        var equipment=avatar.GetComponent<PlayerEquipmentLoader>();if(equipment!=null)equipment.enabled=false;
        avatar.SetActive(true);
        PrelimTeleportTransition.MovePlayerImmediately(avatar,scene.GetRootGameObjects().Single(g=>g.name=="Spawn").transform.position,Quaternion.identity);
        new GameObject("Offline Library Reader Checks").AddComponent<LibraryReadingQaRunner>().StartCoroutine(Guard(Run()));
    }
    static IEnumerator Guard(IEnumerator work)
    {
        var stack=new Stack<IEnumerator>();stack.Push(work);
        while(stack.Count>0)
        {
            object next=null;Exception error=null;
            try{var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}next=current.Current;}catch(Exception e){error=e;}
            if(error!=null){File.AppendAllText(Report,"FAIL "+error+"\n");Debug.LogException(error);break;}
            if(next is IEnumerator child)stack.Push(child);else yield return next;
        }
        foreach(var current in stack)if(current is IDisposable d)d.Dispose();
        if(touch!=null)InputSystem.RemoveDevice(touch);Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static IEnumerator Run()
    {
        Application.runInBackground=true;BookMapMobileChecks.SetSize(4);touch=InputSystem.AddDevice<Touchscreen>();
        var intro=UnityEngine.Object.FindFirstObjectByType<LogicGardenIntroduction>();
        yield return new WaitForSecondsRealtime(2);
        yield return Wait(()=>TeleportManager.Instance==null||!TeleportManager.Instance.IsTransitioning,15,"garden spawn");
        var door=Field<InteractPortal>(intro,"libraryDoor");Face(door.transform.position+Vector3.back*2,door.transform.position);
        yield return new WaitForSecondsRealtime(.4f);intro.Interact();
        yield return Wait(()=>intro.InLibrary&&!intro.Transitioning,12,"library entrance");
        yield return MinimalLibraryReadingChecks.Run();
        Check(LogicGardenProgress.ControlsTutorialCompleted&&LogicGardenProgress.ConversationCompleted&&LogicGardenProgress.LibraryTourCompleted,"reading does not alter milestones");
        File.AppendAllText(Report,"ALL LIBRARY READING CHECKS PASSED\n");
    }
    internal static void Face(Vector3 position,Vector3 target)
    {
        var direction=target-position;direction.y=0;
        PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,position,Quaternion.LookRotation(direction));
        Player.LocalInstance.GetComponentInChildren<CameraTargetController>()?.SetViewAngles(18,Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg);
    }
    static IEnumerator Wait(Func<bool> ready,float seconds,string label)
    {float end=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Check(ready(),label);}
    internal static IEnumerator Tap(UnityEngine.UI.Button button)
    {
        yield return null;Canvas.ForceUpdateCanvases();Check(button!=null&&button.gameObject.activeInHierarchy&&button.IsInteractable(),"available touch "+button?.name);
        var canvas=button.GetComponentInParent<Canvas>();var rect=(RectTransform)button.transform;
        Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"unobstructed touch "+button.name);
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
}
public sealed class LibraryReadingQaRunner:MonoBehaviour{}
#endif
