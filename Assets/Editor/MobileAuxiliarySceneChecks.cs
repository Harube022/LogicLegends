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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Offline menu/exploration checks. Never signs in, joins a room or purchases an item.</summary>
public static class MobileAuxiliarySceneChecks
{
    const string Flag="Mobile.AuxiliaryQA", Report="Library/MobileAuxiliarySceneChecks.txt";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static Touchscreen touch; static int finger;
    public static void Begin()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Exit Play Mode and save the current scene first.");
        SessionState.SetString(Flag+".Scene",SceneManager.GetActiveScene().path);
        SessionState.SetInt(Flag+".Size",BookMapMobileChecks.GetSize());
        SessionState.SetInt(Flag+".Safe",(int)Crystal.SafeArea.Sim);
        SessionState.SetBool(Flag,true);
        File.WriteAllText(Report,"Offline Editor touch checks. No login, network rooms, purchases, rewards or real progress writes.\n");
        EditorSceneManager.OpenScene("Assets/Scenes/Main Menu.unity");EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Register(){EditorApplication.playModeStateChanged-=Restore;EditorApplication.playModeStateChanged+=Restore;}
    static void Restore(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Flag,false))return;
        SessionState.SetBool(Flag,false);SceneManager.sceneLoaded-=Loaded;
        foreach(string field in new[]{"ValidationTransport","ValidationUid","ValidationEmail"})typeof(TrialBackendClient).GetField(field,BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
        BookMapMobileChecks.SetSize(SessionState.GetInt(Flag+".Size",6));Crystal.SafeArea.Sim=(Crystal.SafeArea.SimDevice)SessionState.GetInt(Flag+".Safe",0);
        EditorApplication.delayCall+=()=>EditorSceneManager.OpenScene(SessionState.GetString(Flag+".Scene","Assets/Scenes/TruthTable.unity"));
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Bootstrap()
    {
        if(!SessionState.GetBool(Flag,false))return;
        var fields=BindingFlags.Static|BindingFlags.NonPublic;
        typeof(TrialBackendClient).GetField("ValidationTransport",fields).SetValue(null,(Func<string,string,System.Threading.Tasks.Task<string>>)((a,b)=>System.Threading.Tasks.Task.FromResult("{}")));
        typeof(TrialBackendClient).GetField("ValidationUid",fields).SetValue(null,null);
        SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;
    }
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="Main Menu")return;SceneManager.sceneLoaded-=Loaded;
        var runner=new GameObject("Offline auxiliary mobile checks").AddComponent<MobileAuxiliaryQaRunner>();UnityEngine.Object.DontDestroyOnLoad(runner);runner.StartCoroutine(Guard(Run()));
    }
    static IEnumerator Guard(IEnumerator work)
    {
        var stack=new Stack<IEnumerator>();stack.Push(work);
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;
            try{var item=stack.Peek();if(!item.MoveNext()){stack.Pop();continue;}next=item.Current;}catch(Exception e){failure=e;}
            if(failure!=null){File.AppendAllText(Report,"FAIL "+failure+"\n");break;}
            if(next is IEnumerator child)stack.Push(child);else yield return next;
        }
        foreach(var item in stack)if(item is IDisposable d)d.Dispose();
        if(touch!=null)InputSystem.RemoveDevice(touch);Time.timeScale=1;EditorApplication.isPlaying=false;
    }
    static void Check(bool okay,string text){if(!okay)throw new InvalidOperationException(text);File.AppendAllText(Report,"PASS "+text+"\n");}
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Hidden).GetValue(owner);
    static IEnumerator Run()
    {
        Application.runInBackground=true;
        yield return new WaitForSecondsRealtime(2);
        float readyBy=Time.realtimeSinceStartup+12;
        while(TeleportManager.Instance!=null&&TeleportManager.Instance.IsTransitioning&&Time.realtimeSinceStartup<readyBy)yield return null;
        Check(TeleportManager.Instance==null||!TeleportManager.Instance.IsTransitioning,"Main Menu startup cover releases before interaction");
        touch=InputSystem.AddDevice<Touchscreen>();
        typeof(SceneReliabilityPlayChecks).GetField("touch",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,touch);
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuManager>();var lobby=UnityEngine.Object.FindFirstObjectByType<MultiplayerLobbyUI>();
        foreach(int size in new[]{2,4,6,12})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.5f);
            menu.ShowMainMenu();yield return null;
            var play=Field<GameObject>(menu,"mainMenuPanel").GetComponentsInChildren<Button>().First(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="ShowPlayMenu"));
            yield return Tap(play);Check(Field<GameObject>(menu,"playMenuPanel").activeInHierarchy,"Main Menu Play touch at "+Screen.width+"x"+Screen.height);
            // Opening the choices / invite-code panel is local. Do not press Host or Join.
            lobby.Open();yield return null;yield return Tap(Field<Button>(lobby,"joinChoice"));
            Check(Field<GameObject>(lobby,"joinPanel").activeInHierarchy,"local lobby join form opens");
            yield return Tap(Field<Button>(lobby,"cancelJoin"));Check(Field<GameObject>(lobby,"choicePanel").activeInHierarchy,"local lobby form Back works");
            yield return Tap(Field<Button>(lobby,"back"));Check(!lobby.IsOpen,"lobby exit releases overlay");
        }
        Check(!Photon.Pun.PhotonNetwork.InRoom,"menu checks never joined a network room");
        foreach(string name in new[]{"MultiplayerStage","MultiplayerGameplay"})
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/"+name+".unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSecondsRealtime(2);
            if(Player.LocalInstance==null)
            {
                var avatar=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
                avatar.GetComponent<MultiplayerAvatarSync>().enabled=false;avatar.GetComponent<PlayerEquipmentLoader>().enabled=false;
                foreach(var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true))skin.gameObject.SetActive(skin.name=="UNIFORM_MALE");
                var spawner=UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>();
                if(spawner!=null)avatar.transform.position=spawner.ResolveSpawnPosition(avatar,1);
                avatar.GetComponent<CharacterController>().enabled=true;
            }
            yield return new WaitForSecondsRealtime(1);
            foreach(string method in new[]{"PointerOwnership","NestedCameras","GestureFiltering","Capsule"})
                yield return (IEnumerator)typeof(SceneReliabilityPlayChecks).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{name});
            Check(Player.LocalInstance!=null&&Player.LocalInstance.enabled&&!GameInput.Instance.GameplayInputBlocked,name+" offline exploration leaves controls active");
        }
        File.AppendAllText(Report,"ALL AUXILIARY CHECKS PASSED. Networked match / device verification not performed.\n");
    }
    static IEnumerator Tap(Button button)
    {
        yield return null;Canvas.ForceUpdateCanvases();Check(button!=null&&button.gameObject.activeInHierarchy&&button.IsInteractable(),"available "+button?.name);
        var rect=(RectTransform)button.transform;var canvas=rect.GetComponentInParent<Canvas>().rootCanvas;
        Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"unblocked touch "+button.name+" at="+point+" hits="+string.Join(",",hits.Take(3).Select(h=>h.gameObject.name)));
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
}
public sealed class MobileAuxiliaryQaRunner:MonoBehaviour{}
#endif
