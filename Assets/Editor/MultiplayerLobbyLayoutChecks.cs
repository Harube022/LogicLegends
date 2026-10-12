using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in visual checks of the saved lobby at desktop, phone and tablet aspect ratios.</summary>
[InitializeOnLoad]
public static class MultiplayerLobbyLayoutChecks
{
    private const string Request="Temp/MultiplayerLobbyLayoutChecks.request";
    private static readonly int[] Widths={1920,2400,1440};
    private static readonly List<string> Results=new List<string>();
    private static LoadBalancingClient[] guests;
    private static int screen;
    private static bool active,captured,connecting;
    private static double next,deadline;
    static MultiplayerLobbyLayoutChecks(){EditorApplication.update+=Tick;}
    private static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(!active)
        {
            if(!File.Exists(Request))return;
            var request=File.ReadAllText(Request).Trim();
            if(request=="prepare"&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)return;
                SessionState.SetString("MultiplayerLobbyLayoutScene",SceneManager.GetActiveScene().path);
                EditorSceneManager.OpenScene("Assets/Scenes/Main Menu.unity");
                File.WriteAllText(Request,"run");EditorApplication.isPlaying=true;
            }
            else if(request=="run"&&EditorApplication.isPlaying&&UnityEngine.Object.FindFirstObjectByType<MultiplayerLobbyUI>()!=null)
            {
                Results.Clear();active=true;screen=0;captured=connecting=false;
                File.WriteAllText(Request,"finish");
                UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().ShowPlayMenu();
                UnityEngine.Object.FindFirstObjectByType<MultiplayerMenuManager>().OnClickMultiplayerButton();
                PrepareScreen();deadline=EditorApplication.timeSinceStartup+60;
            }
            else if(request=="finish"&&EditorApplication.isPlaying&&!PhotonNetwork.InRoom)
            {File.WriteAllText(Request,"restore");MultiplayerUIValidation.Restore();EditorApplication.isPlaying=false;}
            else if(request=="restore"&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(Request);
                var previous=SessionState.GetString("MultiplayerLobbyLayoutScene","");
                if(!string.IsNullOrEmpty(previous)&&!SceneManager.GetActiveScene().isDirty)EditorSceneManager.OpenScene(previous);
            }
            return;
        }
        try
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if(guests!=null)foreach(var guest in guests)guest.Service();
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Lobby layout check timed out at screen "+screen);
            if(EditorApplication.timeSinceStartup<next)return;
            var service=MultiplayerLobbyService.Instance;
            if(screen==6&&!connecting)
            {
                connecting=true;service.HostGame();return;
            }
            if(screen==6)
            {
                if(!service.InLobby)return;
                if(guests==null)
                {
                    var settings=PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
                    settings.AppVersion=PhotonNetwork.NetworkingClient.AppVersion;
                    guests=Enumerable.Range(0,3).Select(i=>new LoadBalancingClient()).ToArray();
                    for(int i=0;i<guests.Length;i++)
                    {guests[i].NickName=i==0?"Long-name adventurer for lobby QA":"Adventure friend "+(i+1);guests[i].ConnectUsingSettings(settings);}
                    return;
                }
                foreach(var guest in guests)
                    if(guest.State==ClientState.ConnectedToMasterServer)guest.OpJoinRoom(new EnterRoomParams{RoomName=PhotonNetwork.CurrentRoom.Name});
                if(service.Players.Length!=4)return;
                service.SendChat("Welcome, party! Let's explore together.");
                screen=7;PrepareScreen();return;
            }
            if(!captured)
            {
                Validate();
                int width=Widths[screen<6?screen/2:screen-7];
                var name=screen<6?(screen%2==0?"choice":"join"):"waiting";
                ScreenCapture.CaptureScreenshot("Temp/MultiplayerQA/lobby-"+name+"-"+width+".png");
                Results.Add("PASS "+name+" "+width+"x1080: text fits, controls stay inside frames, button sizes and artwork verified");
                Save();captured=true;next=EditorApplication.timeSinceStartup+.4;return;
            }
            screen++;
            if(screen==10){Results.Add("COMPLETE: 9 lobby layouts, including a live four-player party and chat.");Finish();return;}
            if(screen==6){captured=false;return;}
            PrepareScreen();
        }
        catch(Exception error){Results.Add("FAIL "+error);Finish();Debug.LogException(error);}
    }
    private static void PrepareScreen()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<MultiplayerLobbyUI>();
        if(screen<6){if(screen%2==0)ui.ShowChoices();else ui.ShowJoin();}
        MultiplayerUIValidation.Resolution(Widths[screen<6?screen/2:screen-7],1080);
        captured=false;next=EditorApplication.timeSinceStartup+1;
        Directory.CreateDirectory("Temp/MultiplayerQA");
    }
    private static void Validate()
    {
        Canvas.ForceUpdateCanvases();
        var frame=UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).First(r=>r.name=="WoodFrame"&&r.parent.name=="SafeArea");
        MultiplayerUIValidation.InsideFrame(frame);
        foreach(var button in frame.GetComponentsInChildren<Button>())
        {
            var rect=(RectTransform)button.transform;
            if(Mathf.Abs(rect.rect.height-88)>.1f||rect.rect.width>340||rect.rect.width<220)
                throw new InvalidOperationException("Nonstandard button size: "+button.name+" "+rect.rect.size);
            if(Mathf.Abs(button.GetComponent<Image>().pixelsPerUnitMultiplier-4.5f)>.01f)
                throw new InvalidOperationException("Incorrect button artwork multiplier: "+button.name);
            var panel=rect.parent.GetComponent<RectTransform>();
            if(!Contains(panel,rect))throw new InvalidOperationException(button.name+" crosses its own panel.");
        }
        if(screen>=7)
        {
            var chat=frame.GetComponentInChildren<RoomChatPanel>();
            if(chat.scroll.viewport.rect.height<110)throw new InvalidOperationException("Chat message viewport is too short.");
            if(chat.messageList.childCount==0)throw new InvalidOperationException("Live chat message cards did not render.");
            var slots=frame.GetComponentsInChildren<TMP_Text>().Where(t=>t.name.StartsWith("PlayerSlot")).ToArray();
            if(slots.Length!=4||slots.Any(t=>t.text.StartsWith("OPEN SLOT")))throw new InvalidOperationException("Four-player roster did not render.");
        }
    }
    private static bool Contains(RectTransform parent,RectTransform child)
    {
        var corners=new Vector3[4];child.GetWorldCorners(corners);
        // Half a canvas unit allows for round-off when coincident edges are transformed.
        return corners.All(c=>{var p=parent.InverseTransformPoint(c);return p.x>=parent.rect.xMin-.5f&&p.x<=parent.rect.xMax+.5f&&p.y>=parent.rect.yMin-.5f&&p.y<=parent.rect.yMax+.5f;});
    }
    private static void Save(){Directory.CreateDirectory("Temp/MultiplayerQA");File.WriteAllLines("Temp/MultiplayerQA/lobby-layout-checks.txt",Results);}
    private static void Finish()
    {
        Save();active=false;
        if(guests!=null)foreach(var guest in guests){guest.Disconnect();guest.Service();}
        guests=null;MultiplayerLobbyService.Instance?.LeaveLobby();
    }
}
