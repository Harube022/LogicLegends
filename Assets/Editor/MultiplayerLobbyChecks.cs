using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Opt-in live integration checks. Uses temporary private rooms and four independent clients.</summary>
public static class MultiplayerLobbyChecks
{
    private static readonly List<string> results = new List<string>();
    private static LoadBalancingClient[] clients;
    private static short lastJoinError;
    private static int phase;
    private static double deadline;
    private static bool hostMessageReceived, running;
    private static string room, firstRoom;
    public static string Results => string.Join("\n", results);

    [MenuItem("Logic Legends/Multiplayer/Run Live Lobby Checks (Play Mode)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "Main Menu")
            throw new InvalidOperationException("Run in Play Mode from Main Menu.");
        Stop(); results.Clear(); phase=0; running=true;
        var ui=UnityEngine.Object.FindFirstObjectByType<MultiplayerMenuManager>();
        UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().ShowPlayMenu();
        ui.OnClickMultiplayerButton();
        var s=MultiplayerLobbyService.Instance;
        s.JoinGame(" invalid ");
        Check(!s.Busy && !s.InLobby,"Invalid code is rejected before connecting");
        Check(LobbyRules.NormalizeCode("  ab3d7k  ")=="AB3D7K" && LobbyRules.IsValidCode("AB3D7K"),"Invite codes trim and normalize");
        Check(!LobbyRules.IsValidCode("ABC0I1"),"Ambiguous code characters are rejected");
        Check(Enumerable.Range(0,200).Select(_=>LobbyRules.NewCode()).All(LobbyRules.IsValidCode),"Generated codes have six allowed symbols");
        s.HostGame();
        deadline=EditorApplication.timeSinceStartup+45;
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=PlayState;
        Save();
    }
    private static void PlayState(PlayModeStateChange state) { if(state==PlayModeStateChange.ExitingPlayMode) Stop(); }
    private static void Check(bool condition,string name)
    {
        results.Add((condition?"PASS ":"FAIL ")+name);
        Save();
        if(!condition) throw new InvalidOperationException(name);
    }
    private static void Next(int next)
    {
        phase=next; deadline=EditorApplication.timeSinceStartup+40; Save();
    }
    private static void Save()
    {
        Directory.CreateDirectory("Temp/MultiplayerQA");
        File.WriteAllText("Temp/MultiplayerQA/live-checks.txt",Results+"\nPhase: "+phase);
    }
    private static bool Master(LoadBalancingClient c) => c.State==ClientState.ConnectedToMasterServer;
    private static void Join(LoadBalancingClient c,string code)
    {
        lastJoinError=0;
        c.OpJoinRoom(new EnterRoomParams { RoomName=code });
    }
    private static void Tick()
    {
        if(!running) return;
        try
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if(clients!=null) foreach(var client in clients) client.Service();
            var s=MultiplayerLobbyService.Instance;
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Phase "+phase+" PUN="+PhotonNetwork.NetworkClientState+" status="+s.Status+" clients="+(clients==null?"none":string.Join(",",clients.Select(c=>c.State.ToString()))));
            switch(phase)
            {
                case 0:
                    if(!s.InLobby) return;
                    room=firstRoom=PhotonNetwork.CurrentRoom.Name;
                    Check(LobbyRules.IsValidCode(room)&&!PhotonNetwork.CurrentRoom.IsVisible&&PhotonNetwork.CurrentRoom.IsOpen&&PhotonNetwork.CurrentRoom.MaxPlayers==4,"Host creates a private open 4-player room");
                    Check(!s.CanStart&&!LobbyRules.IsReady(PhotonNetwork.LocalPlayer),"One unready host cannot start");
                    s.ToggleReady();
                    var settings=PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
                    settings.AppVersion=PhotonNetwork.NetworkingClient.AppVersion;
                    clients=Enumerable.Range(0,4).Select(i=>new LoadBalancingClient()).ToArray();
                    foreach(var c in clients)
                    {
                        c.NickName=PhotonNetwork.NickName; // Duplicate names must remain distinct actors.
                        c.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey]=false });
                        c.OpResponseReceived+=response=> { if(response.OperationCode==OperationCode.JoinGame&&response.ReturnCode!=0) lastJoinError=response.ReturnCode; };
                        c.EventReceived+=data=> { if(data.Code==LobbyRules.ChatEvent&&Equals(data.CustomData,"Host test message")) hostMessageReceived=true; };
                        c.ConnectUsingSettings(settings);
                    }
                    Next(1); break;
                case 1:
                    if(!clients.All(Master)) return;
                    Join(clients[3],LobbyRules.NewCode()); Next(2); break;
                case 2:
                    if(lastJoinError==0) return;
                    Check(lastJoinError==ErrorCode.GameDoesNotExist,"Nonexistent code never creates a room");
                    if(!Master(clients[0])) return;
                    Join(clients[0],room); Next(3); break;
                case 3:
                    if(!clients[0].InRoom||s.Players.Length!=2) return;
                    Check(!s.CanStart,"Two players cannot start while a guest is unready");
                    Check(s.Players.Select(p=>p.ActorNumber).Distinct().Count()==2,"Duplicate display names use distinct actor identifiers");
                    clients[0].LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey]=true });
                    Next(4); break;
                case 4:
                    if(!s.CanStart) return;
                    Check(SceneManager.GetActiveScene().name=="Main Menu","Two ready players wait for explicit host start");
                    Check(!s.SendChat("   ")&&!s.SendChat(new string('x',201)),"Empty and overlong chat are rejected");
                    Check(s.SendChat("Host test message")&&!s.SendChat("Too soon"),"Chat has a send cooldown");
                    clients[0].OpRaiseEvent(LobbyRules.ChatEvent,"<b>Plain text</b>",new RaiseEventOptions { Receivers=ReceiverGroup.All },SendOptions.SendReliable);
                    Next(5); break;
                case 5:
                    if(!hostMessageReceived||!s.Messages.Any(m=>m.Contains("<b>Plain text</b>"))) return;
                    Check(true,"Chat delivered in both directions with sender names");
                    var chat=UnityEngine.Object.FindFirstObjectByType<RoomChatPanel>();
                    Check(!chat.history.richText&&!chat.input.richText&&chat.input.characterLimit==200,"Chat renders plain text and limits input to 200 characters");
                    chat.input.ActivateInputField();
                    Join(clients[1],room); Next(6); break;
                case 6:
                    if(!clients[1].InRoom||s.Players.Length!=3) return;
                    Check(GameInput.Instance==null||GameInput.Instance.GameplayInputBlocked,"Lobby/chat isolates gameplay input");
                    Join(clients[2],room); Next(7); break;
                case 7:
                    if(!clients[2].InRoom||s.Players.Length!=4) return;
                    Check(!s.CanStart,"Four players do not automatically start");
                    clients[1].LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey]=true });
                    clients[2].LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey]=true });
                    Next(8); break;
                case 8:
                    if(!s.CanStart) return;
                    Check(s.Players.Length==4,"Four ready players allow host start");
                    ScreenCapture.CaptureScreenshot("Temp/MultiplayerQA/waiting-four.png");
                    Join(clients[3],room); Next(9); break;
                case 9:
                    if(lastJoinError==0) return;
                    Check(lastJoinError==ErrorCode.GameFull,"Fifth player is rejected as full");
                    clients[2].OpLeaveRoom(false); Next(10); break;
                case 10:
                    if(s.Players.Length!=3||!Master(clients[2])||!Master(clients[3])) return;
                    Check(s.Messages.Any(m=>m.Contains("left the lobby")),"Guest departure updates roster and system chat");
                    PhotonNetwork.CurrentRoom.IsOpen=false;
                    Join(clients[3],room); Next(11); break;
                case 11:
                    if(lastJoinError==0) return;
                    Check(lastJoinError==ErrorCode.GameClosed,"Closed-room join is rejected");
                    PhotonNetwork.CurrentRoom.IsOpen=true;
                    s.LeaveLobby(); Next(12); break;
                case 12:
                    if(s.InLobby||s.Busy||!clients[0].InRoom||!clients[0].LocalPlayer.IsMasterClient) return;
                    Check(s.Messages.Count==0,"Leaving clears room chat");
                    Check(clients[0].CurrentRoom.PlayerCount==2,"Host departure migrates master role to remaining guest");
                    s.HostGame(); Next(13); break;
                case 13:
                    if(!s.InLobby) return;
                    Check(PhotonNetwork.CurrentRoom.Name!=firstRoom&&s.Players.Length==1&&!LobbyRules.IsReady(PhotonNetwork.LocalPlayer),"Reopening creates a fresh lobby with no stale players/readiness");
                    Check(UnityEngine.Object.FindObjectsByType<MultiplayerLobbyService>(FindObjectsSortMode.None).Length==1,"Reopening keeps one network service");
                    PhotonNetwork.Disconnect(); Next(14); break;
                case 14:
                    if(PhotonNetwork.NetworkClientState!=ClientState.Disconnected||s.Busy) return;
                    Check(s.Status.Contains("Disconnected")&&s.Messages.Count==0,"Disconnection clears state and gives a usable retry path");
                    results.Add("COMPLETE live lobby checks"); Save(); Stop(); break;
            }
        }
        catch(Exception error)
        {
            results.Add("FAIL "+error.Message); Save(); Stop(); Debug.LogException(error);
        }
    }
    public static void Stop()
    {
        running=false;
        EditorApplication.update-=Tick;
        EditorApplication.playModeStateChanged-=PlayState;
        if(clients!=null) foreach(var client in clients) { client.Disconnect(); client.Service(); }
        clients=null;
    }
}
