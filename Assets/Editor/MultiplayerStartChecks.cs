using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Opt-in scene-start checks against independent live Photon clients.</summary>
[InitializeOnLoad]
public static class MultiplayerStartChecks
{
    private static readonly List<string> log = new List<string>();
    private static LoadBalancingClient guest;
    private static ConnectionHandler heartbeat;
    private static readonly List<LoadBalancingClient> companions = new List<LoadBalancingClient>();
    private static readonly List<ConnectionHandler> companionKeepers = new List<ConnectionHandler>();
    private static int phase, sent;
    private static double deadline, nextChat;
    private static bool active, includeHistory, gameplayChatDelivered;
    private static int remoteViewID, localUpdates;
    private static Vector3 localBefore;
    static MultiplayerStartChecks() { EditorApplication.update += RequestedCheck; }
    private static void RequestedCheck()
    {
        const string path="Temp/MultiplayerStartChecks.request";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(path))return;
        string request=File.ReadAllText(path).Trim();
        if(!EditorApplication.isPlayingOrWillChangePlaymode&&request=="prepare")
        {
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty)return;
            EditorSceneManager.OpenScene("Assets/Scenes/Main Menu.unity");
            File.WriteAllText(path,"run"); EditorApplication.isPlaying=true;
        }
        else if(EditorApplication.isPlaying&&request=="run"&&UnityEngine.Object.FindFirstObjectByType<MultiplayerMenuManager>()!=null)
        { File.WriteAllText(path,"finish"); RunSceneOnly(); }
        else if(EditorApplication.isPlaying&&request=="finish"&&!active&&!PhotonNetwork.InRoom&&SceneManager.GetActiveScene().name=="Main Menu")
        { File.WriteAllText(path,"restore"); EditorApplication.isPlaying=false; }
        else if(!EditorApplication.isPlayingOrWillChangePlaymode&&request=="restore")
        {
            File.Delete(path);
            if(!SceneManager.GetActiveScene().isDirty)EditorSceneManager.OpenScene(MultiplayerSceneRoutes.GameplayPath);
        }
    }
    public static string Results => string.Join("\n",log);
    [MenuItem("Logic Legends/Multiplayer/Run Start And Chat Checks (Play Mode)")]
    public static void Run() { Begin(true); }
    public static void RunSceneOnly() { Begin(false); }
    private static void Begin(bool history)
    {
        if(!EditorApplication.isPlaying||SceneManager.GetActiveScene().name!="Main Menu") throw new InvalidOperationException("Run from Main Menu in Play Mode.");
        Stop(); log.Clear(); phase=sent=localUpdates=0; active=true; includeHistory=history;
        var menu=UnityEngine.Object.FindFirstObjectByType<MultiplayerMenuManager>();
        UnityEngine.Object.FindFirstObjectByType<MainMenuManager>().ShowPlayMenu(); menu.OnClickMultiplayerButton();
        phase=-3;gameplayChatDelivered=false;
        MultiplayerUIValidation.Resolution(1920,1080);nextChat=EditorApplication.timeSinceStartup+1;
        deadline=EditorApplication.timeSinceStartup+40;
        EditorApplication.update+=Tick; EditorApplication.playModeStateChanged+=PlayState;
    }
    private static void PlayState(PlayModeStateChange state) { if(state==PlayModeStateChange.ExitingPlayMode) Stop(); }
    private static void Check(bool ok,string name)
    {
        log.Add((ok?"PASS ":"FAIL ")+name); Save();
        if(!ok) throw new InvalidOperationException(name);
    }
    private static void Save()
    {
        Directory.CreateDirectory("Temp/MultiplayerQA"); File.WriteAllText("Temp/MultiplayerQA/start-checks.txt",Results+"\nPhase: "+phase);
    }
    private static void Next(int value) { phase=value; deadline=EditorApplication.timeSinceStartup+80; Save(); }
    private static void Tick()
    {
        if(!active) return;
        try
        {
            EditorApplication.QueuePlayerLoopUpdate(); guest?.Service();
            foreach(var peer in companions)peer.Service();
            var s=MultiplayerLobbyService.Instance;
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Phase "+phase+": "+s.Status);
            switch(phase)
            {
                case -3:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    MultiplayerUIValidation.InsideFrame(UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).First(r=>r.name=="WoodFrame"&&r.parent.name=="SafeArea"));
                    ScreenCapture.CaptureScreenshot("Temp/MultiplayerQA/lobby-choice-1920.png");
                    nextChat=EditorApplication.timeSinceStartup+.3;Next(-4);break;
                case -4:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    UnityEngine.Object.FindFirstObjectByType<MultiplayerLobbyUI>().ShowJoin();nextChat=EditorApplication.timeSinceStartup+.5;Next(-2);break;
                case -2:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    ScreenCapture.CaptureScreenshot("Temp/MultiplayerQA/lobby-join-1920.png");
                    nextChat=EditorApplication.timeSinceStartup+.3;Next(-1);break;
                case -1:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    UnityEngine.Object.FindFirstObjectByType<MultiplayerLobbyUI>().ShowChoices();s.HostGame();Next(0);break;
                case 0:
                    if(!s.InLobby) return;
                    s.ToggleReady();
                    var settings=PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings()); settings.AppVersion=PhotonNetwork.NetworkingClient.AppVersion;
                    guest=new LoadBalancingClient(); guest.NickName="Long-name adventurer for lobby QA";
                    guest.EventReceived += ev => { if(ev.Code==201||ev.Code==206) localUpdates++;if(ev.Code==LobbyRules.ChatEvent&&Equals(ev.CustomData,"Meet at the book!"))gameplayChatDelivered=true; };
                    // Scene loading can block the editor thread. Real PUN clients have this fallback too.
                    var keeper=new GameObject("Lobby QA Guest Heartbeat"); UnityEngine.Object.DontDestroyOnLoad(keeper);
                    heartbeat=keeper.AddComponent<ConnectionHandler>(); heartbeat.Client=guest;
                    heartbeat.StartFallbackSendAckThread();
                    guest.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey]=false }); guest.ConnectUsingSettings(settings); Next(1); break;
                case 1:
                    if(guest.State!=ClientState.ConnectedToMasterServer) return;
                    Check(!s.CanStart,"Even a ready host cannot start alone");
                    guest.OpJoinRoom(new EnterRoomParams { RoomName=PhotonNetwork.CurrentRoom.Name }); Next(2); break;
                case 2:
                    if(!guest.InRoom||s.Players.Length!=2) return;
                    guest.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey]=true }); Next(3); break;
                case 3:
                    if(!s.CanStart) return;
                    PhotonNetwork.SetMasterClient(PhotonNetwork.CurrentRoom.GetPlayer(guest.LocalPlayer.ActorNumber)); Next(4); break;
                case 4:
                    if(PhotonNetwork.IsMasterClient) return;
                    s.StartGame();
                    Check(!s.Starting&&!s.CanStart,"A guest cannot initiate starting even with everyone ready");
                    guest.CurrentRoom.SetMasterClient(guest.CurrentRoom.GetPlayer(PhotonNetwork.LocalPlayer.ActorNumber)); Next(5); break;
                case 5:
                    if(!s.CanStart) return;
                    Check(s.Messages.Any(m=>m.Contains("is now the host")),"Host changes update local controls and system messages");
                    s.StartGame(); guest.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey]=false }); Next(6); break;
                case 6:
                    if(s.Starting||s.Busy) return;
                    Check(SceneManager.GetActiveScene().name=="Main Menu"&&PhotonNetwork.CurrentRoom.IsOpen,"Readiness change during start cancels and reopens the room");
                    if(!includeHistory)
                    {
                        guest.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey]=true }); Next(11); break;
                    }
                    nextChat=EditorApplication.timeSinceStartup; Next(7); break;
                case 7:
                    if(EditorApplication.timeSinceStartup<nextChat) return;
                    guest.OpRaiseEvent(LobbyRules.ChatEvent,"History message "+sent,new RaiseEventOptions { Receivers=ReceiverGroup.All },SendOptions.SendReliable);
                    sent++; nextChat=EditorApplication.timeSinceStartup+1.05;
                    if(sent>=62) Next(8);
                    break;
                case 8:
                    if(!s.Messages.Any(m=>m.Contains("History message 61"))) return;
                    Check(s.Messages.Count==LobbyRules.HistoryLimit&&!s.Messages.Any(m=>m.Contains("History message 0")),"Live chat history is capped at 60 visible messages");
                    var panel=UnityEngine.Object.FindFirstObjectByType<RoomChatPanel>();
                    Check(!panel.history.richText,"History stays plain text after scrolling");
                    guest.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey]=true }); Next(11); break;
                case 11:
                    if(!s.CanStart) return;
                    s.StartGame(); guest.OpLeaveRoom(false); Next(12); break;
                case 12:
                    if(s.Starting||s.Busy||s.Players.Length!=1||guest.State!=ClientState.ConnectedToMasterServer) return;
                    Check(PhotonNetwork.CurrentRoom.IsOpen&&!s.CanStart&&SceneManager.GetActiveScene().name=="Main Menu","Guest departure during start cancels launch and reopens the lobby");
                    guest.OpJoinRoom(new EnterRoomParams { RoomName=PhotonNetwork.CurrentRoom.Name }); Next(13); break;
                case 13:
                    if(!guest.InRoom||s.Players.Length!=2) return;
                    guest.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey]=true });
                    ConnectCompanion("Archive QA third player");ConnectCompanion("Archive QA fourth player");Next(27);break;
                case 27:
                    foreach(var peer in companions)
                    {
                        if(peer.State==ClientState.ConnectedToMasterServer)peer.OpJoinRoom(new EnterRoomParams { RoomName=PhotonNetwork.CurrentRoom.Name });
                        if(peer.InRoom&&!LobbyRules.IsReady(peer.LocalPlayer))peer.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyRules.ReadyKey]=true });
                    }
                    if(companions.Any(p=>!p.InRoom)||s.Players.Length!=4||!s.CanStart)return;
                    Check(s.Players.Length==4&&s.CanStart,"The real lobby admits four independent clients and requires everyone ready");Next(9);break;
                case 9:
                    if(!s.CanStart) return;
                    MultiplayerUIValidation.InsideFrame(UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).First(r=>r.name=="WoodFrame"&&r.parent.name=="SafeArea"));
                    ScreenCapture.CaptureScreenshot("Temp/MultiplayerQA/ready-two.png");
                    s.StartGame(); s.StartGame(); Next(10); break;
                case 10:
                    if(!MultiplayerSceneRoutes.IsOpenArchive(SceneManager.GetActiveScene())||Player.LocalInstance==null) return;
                    if(guest.CurrentRoom==null) throw new InvalidOperationException("Remote client lost its room during scene load: "+guest.DisconnectedCause);
                    if(!guest.CurrentRoom.CustomProperties.TryGetValue("curScn",out var scene)) return;
                    Check(Equals(scene,SceneUtility.GetBuildIndexByScenePath(MultiplayerSceneRoutes.GameplayPath)),"Lobby Start resolves the exact Open Archive path and synchronizes its build index to peers");
                    Check(Equals(PhotonNetwork.CurrentRoom.CustomProperties[LobbyRules.SceneKey],MultiplayerSceneRoutes.GameplayPath),"The private room advertises the exact Open Archive destination");
                    Check(!guest.CurrentRoom.IsOpen&&Equals(guest.CurrentRoom.CustomProperties[LobbyRules.StateKey],LobbyRules.Loading),"Started room is closed and marked loading");
                    Check(UnityEngine.Object.FindObjectsByType<MultiplayerLobbyService>(FindObjectsSortMode.None).Length==1,"Duplicate Start presses preserve one session/service");
                    Check(s.Messages.Count>0&&s.Messages.Count<=LobbyRules.HistoryLimit,"Room chat survives into the multiplayer scene");
                    Check(GameInput.Instance==null||!GameInput.Instance.GameplayInputBlocked,"Closing the lobby restores gameplay input");
                    Check(Player.LocalInstance.GetComponent<PhotonView>().IsMine&&Player.LocalInstance.enabled,"One spawned player owns local movement");
                    Check(Player.LocalInstance.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy),"Owned avatar renders its inherited character and equipped outfit");
                    var cm=GameObject.Find("CM_ThirdPersonCam").GetComponent<Unity.Cinemachine.CinemachineCamera>();
                    Check(cm.Target.TrackingTarget==Player.LocalInstance.transform.Find("CameraTarget"),"Cinemachine tracks the inherited local CameraTarget");
                    remoteViewID=guest.LocalPlayer.ActorNumber*PhotonNetwork.MAX_VIEW_IDS+1;
                    // PUN 2's documented-in-source Instantiate event layout (PhotonNetwork.SendInstantiate).
                    SpawnPeer(guest,"Multiplayer_Female");
                    foreach(var peer in companions)SpawnPeer(peer,peer==companions[0]?"Multiplayer_Male":"Multiplayer_Female");
                    Next(14);break;
                case 14:
                    var remote=PhotonView.Find(remoteViewID);
                    if(remote==null||!remote.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy))return;
                    if(companions.Any(p=>PhotonView.Find(p.LocalPlayer.ActorNumber*PhotonNetwork.MAX_VIEW_IDS+1)==null))return;
                    var spawner=UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>();
                    var expected=spawner.ResolveSpawnPosition(Resources.Load<GameObject>("Multiplayer_Male"),PhotonNetwork.LocalPlayer.ActorNumber);
                    var delta=Player.LocalInstance.transform.position-expected;delta.y=0;
                    Check(delta.magnitude<.1f,"Online exploration preserves the host's separate spawn slot instead of teleporting to the center");
                    var avatars=UnityEngine.Object.FindObjectsByType<MultiplayerAvatarSync>(FindObjectsSortMode.None);
                    Check(avatars.Length==4&&avatars.Select(a=>a.GetComponent<PhotonView>().OwnerActorNr).Distinct().Count()==4,"Four visible avatars retain four distinct network owners");
                    Check(avatars.All(a=>avatars.All(b=>a==b||Vector3.Distance(a.transform.position,b.transform.position)>4)),"All four room slots are separated on the Open Archive spawn plaza");
                    Check(avatars.All(a=>Physics.Raycast(a.transform.position+Vector3.up*.2f,Vector3.down,2f,~(1<<7),QueryTriggerInteraction.Ignore)),"Every room slot has solid ground beneath the avatar");
                    var bootstrap=UnityEngine.Object.FindFirstObjectByType<LogicLegends.Tests.Khezu.KhezuMultiplayerTestSession>();
                    Check(bootstrap!=null&&!bootstrap.offlineExplorer.activeSelf&&bootstrap.exploration.Explorer==Player.LocalInstance,"Online setup disables the offline explorer and binds the owned network player");
                    var topics=LogicLegends.OpenArchive.Topics.ArchiveTopicSession.Current;
                    Check(topics!=null&&topics.Ready&&topics.stations.Length==10&&topics.ActiveTopic==-1,"The existing ten-topic team session initializes without auto-starting a challenge");
                    Check(topics.hunting!=null&&UnityEngine.Object.FindFirstObjectByType<LogicLegends.OpenArchive.Topics.ArchiveLearningUI>()!=null,"Existing monster encounter and learning interface load with the environment");
                    Check(remote.OwnerActorNr==guest.LocalPlayer.ActorNumber&&!remote.IsMine,"Remote female instantiates with the correct guest ownership");
                    Check(!remote.GetComponent<Player>().enabled&&!remote.GetComponent<CharacterController>().enabled,"Remote player cannot consume local controls or run local character physics");
                    Check(Player.LocalInstance.gameObject!=remote.gameObject,"Remote spawn leaves the local player and camera owner intact");
                    // Match PUN OnSerializeWrite's three-value view header, then the avatar's six fields.
                    var values=new object[] { remoteViewID,false,null,expected+Vector3.right*6,Quaternion.Euler(0,90,0),true,false,false,8f };
                    guest.OpRaiseEvent(201,new object[] { PhotonNetwork.ServerTimestamp,null,values },new RaiseEventOptions { Receivers=ReceiverGroup.Others },SendOptions.SendReliable);
                    localBefore=Player.LocalInstance.transform.position; GameInput.Instance.SetMobileMovement(Vector2.right);
                    nextChat=EditorApplication.timeSinceStartup+.6;Next(15);break;
                case 15:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    GameInput.Instance.SetMobileMovement(Vector2.zero);
                    var moved=PhotonView.Find(remoteViewID);
                    var remoteTarget=UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>().ResolveSpawnPosition(Resources.Load<GameObject>("Multiplayer_Male"),PhotonNetwork.LocalPlayer.ActorNumber)+Vector3.right*6;
                    if(Vector3.Distance(moved.transform.position,remoteTarget)>.3f)return;
                    Check(Vector3.Distance(localBefore,Player.LocalInstance.transform.position)>.1f,"The local avatar moves using the shared GameInput");
                    Check(localUpdates>0,"The guest receives the local avatar's PUN synchronization updates");
                    Check(moved.GetComponentsInChildren<Animator>().Any(a=>a.GetBool("IsWalking")),"Remote movement and walking animation arrive over the live connection");
                    Check(Player.LocalInstance.GetComponent<CharacterController>().isGrounded,"The local avatar stands on the Open Archive spawn plaza collider");
                    Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a=>a.enabled)==1,"Open Archive has exactly one active audio listener");
                    Check(TeleportManager.Instance!=null&&TeleportManager.UsesCoveredTransitions,"The new stage uses covered teleport transitions");
                    if(TeleportManager.Instance.IsTransitioning)return;
                    Check(TeleportManager.Instance.FadeAlpha<.01f,"The startup fade reveals the spawned players");
                    ScreenCapture.CaptureScreenshot("Temp/MultiplayerQA/multiplayer-stage.png");
                    nextChat=EditorApplication.timeSinceStartup+.5;Next(16);break;
                case 16:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    var previous=Player.LocalInstance.gameObject;var position=previous.transform.position;
                    PhotonNetwork.Destroy(previous);
                    PhotonNetwork.Instantiate("Multiplayer_Female",position,Quaternion.identity);
                    guest.OpRaiseEvent(200,new Hashtable { [(byte)0]=remoteViewID,[(byte)2]=PhotonNetwork.ServerTimestamp,
                        [(byte)3]="SyncEquipmentRPC",[(byte)4]=new object[]{"female_tuxedo",""} },new RaiseEventOptions { Receivers=ReceiverGroup.Others },SendOptions.SendReliable);
                    nextChat=EditorApplication.timeSinceStartup+.5;Next(17);break;
                case 17:
                    if(EditorApplication.timeSinceStartup<nextChat||Player.LocalInstance==null)return;
                    var female=Player.LocalInstance;
                    Check(female.name.StartsWith("Multiplayer_Female")&&female.GetComponent<PhotonView>().IsMine&&female.enabled,"Inherited female prefab also spawns as the locally controlled player");
                    Check(female.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.gameObject.activeInHierarchy),"Local female default outfit remains visible");
                    var femaleCamera=GameObject.Find("CM_ThirdPersonCam").GetComponent<Unity.Cinemachine.CinemachineCamera>();
                    Check(femaleCamera.Target.TrackingTarget==female.transform.Find("CameraTarget"),"Camera follows the female player after changing the owned avatar");
                    var equipment=new SerializedObject(PhotonView.Find(remoteViewID).GetComponent<PlayerEquipmentLoader>()).FindProperty("clothesModels");
                    bool tuxedo=false;
                    for(int i=0;i<equipment.arraySize;i++)
                    {
                        var entry=equipment.GetArrayElementAtIndex(i);
                        if(entry.FindPropertyRelative("itemID").stringValue=="female_tuxedo")tuxedo=((GameObject)entry.FindPropertyRelative("model").objectReferenceValue).activeInHierarchy;
                    }
                    if(!tuxedo)return;
                    Check(tuxedo,"Remote equipment RPC selects the inherited outfit mapping");
                    Check(!Camera.main.orthographic&&Camera.main.clearFlags==CameraClearFlags.Skybox,"Gameplay remains perspective with the assigned skybox");
                    guest.OpRaiseEvent(LobbyRules.ChatEvent,"<b>Ready for the next adventure?</b>",new RaiseEventOptions { Receivers=ReceiverGroup.All },SendOptions.SendReliable);
                    nextChat=EditorApplication.timeSinceStartup+.5;Next(18);break;
                case 18:
                    if(EditorApplication.timeSinceStartup<nextChat||!s.Messages.Any(m=>m.Contains("Ready for the next adventure?")))return;
                    var hud=UnityEngine.Object.FindFirstObjectByType<MultiplayerGameChat>();
                    Check(hud!=null&&!hud.IsOpen&&hud.UnreadCount==1,"Open Archive keeps the collapsed party chat and shows an unread guest message");
                    hud.Open();
                    Check(hud.IsOpen&&hud.UnreadCount==0&&GameInput.Instance.GameplayInputBlocked&&Time.timeScale==1,"Opening chat clears unread, blocks local controls and leaves the multiplayer world running");
                    Check(!hud.GetComponentInParent<Canvas>().transform.Find("SafeArea/Gameplay_Interface").gameObject.activeSelf,"Expanded chat hides the touch HUD so tablet controls do not cross its edge");
                    Check(!Player.LocalInstance.GetComponentInChildren<CameraTargetController>().enabled,"The chat panel suspends local camera look");
                    Check(hud.Conversation.history.text.Contains("<b>Ready")&&!hud.Conversation.history.richText,"Gameplay shows retained history and literal plain-text markup");
                    nextChat=EditorApplication.timeSinceStartup+.5;Next(19);break;
                case 19:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    ValidateChat("chat-open-1920.png");
                    var conversation=UnityEngine.Object.FindFirstObjectByType<MultiplayerGameChat>().Conversation;
                    conversation.input.text="Meet at the book!";conversation.send.onClick.Invoke();
                    nextChat=EditorApplication.timeSinceStartup+.3;Next(24);break;
                case 24:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    Crystal.SafeArea.Sim=Crystal.SafeArea.SimDevice.iPhoneX;
                    MultiplayerUIValidation.Resolution(2400,1080);nextChat=EditorApplication.timeSinceStartup+.5;Next(20);break;
                case 20:
                    if(EditorApplication.timeSinceStartup<nextChat||!gameplayChatDelivered)return;
                    Check(s.ChatMessages.Any(m=>m.Text=="Meet at the book!"&&m.Actor==PhotonNetwork.LocalPlayer.ActorNumber),"In-game Send delivers a named message to both the guest and the host");
                    ValidateChat("chat-open-phone.png");
                    nextChat=EditorApplication.timeSinceStartup+.3;Next(25);break;
                case 25:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    MultiplayerUIValidation.Resolution(1440,1080);nextChat=EditorApplication.timeSinceStartup+.5;Next(21);break;
                case 21:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    ValidateChat("chat-open-tablet.png");
                    nextChat=EditorApplication.timeSinceStartup+.3;Next(26);break;
                case 26:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    var chatHud=UnityEngine.Object.FindFirstObjectByType<MultiplayerGameChat>();chatHud.Close();
                    Check(!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.GetComponentInChildren<CameraTargetController>().enabled,"Closing chat restores movement and camera input");
                    Check(chatHud.GetComponentInParent<Canvas>().transform.Find("SafeArea/Gameplay_Interface").gameObject.activeSelf,"Closing chat restores the original touch HUD");
                    chatHud.Open();UnityEngine.Object.FindFirstObjectByType<LevelMenu>().OpenPause();
                    Check(!chatHud.IsOpen&&GameInput.Instance.GameplayInputBlocked,"Settings closes chat before acquiring its own input block");
                    UnityEngine.Object.FindFirstObjectByType<LevelMenu>().ClosePause();
                    Check(!GameInput.Instance.GameplayInputBlocked,"Closing Settings after chat restores gameplay input");
                    Crystal.SafeArea.Sim=Crystal.SafeArea.SimDevice.None;
                    MultiplayerUIValidation.Resolution(1920,1080);nextChat=EditorApplication.timeSinceStartup+.5;Next(22);break;
                case 22:
                    if(EditorApplication.timeSinceStartup<nextChat)return;
                    ScreenCapture.CaptureScreenshot("Temp/MultiplayerQA/chat-collapsed-1920.png");
                    s.LeaveLobby();Next(23);break;
                case 23:
                    if(s.InLobby||s.Busy||SceneManager.GetActiveScene().name!="Main Menu")return;
                    Check(UnityEngine.Object.FindFirstObjectByType<MultiplayerGameChat>()==null&&s.ChatMessages.Count==0,"Leaving removes the stage-only chat HUD and clears the room conversation");
                    log.Add("COMPLETE live Open Archive lobby start: one rendered PUN client and three independent Realtime clients; second rendered device and native mobile keyboard not exercised");Save();Stop();break;
            }
        }
        catch(Exception e) { log.Add("FAIL "+e.Message); Save(); Stop();MultiplayerLobbyService.Instance?.LeaveLobby(); Debug.LogException(e); }
    }
    public static void Stop()
    {
        Crystal.SafeArea.Sim=Crystal.SafeArea.SimDevice.None;
        MultiplayerUIValidation.Restore();
        active=false; EditorApplication.update-=Tick; EditorApplication.playModeStateChanged-=PlayState;
        if(guest!=null) { guest.Disconnect(); guest.Service(); guest=null; }
        if(heartbeat!=null) { heartbeat.StopFallbackSendAckThread(); UnityEngine.Object.Destroy(heartbeat.gameObject); heartbeat=null; }
        foreach(var peer in companions){peer.Disconnect();peer.Service();}companions.Clear();
        foreach(var keeper in companionKeepers)if(keeper!=null){keeper.StopFallbackSendAckThread();UnityEngine.Object.Destroy(keeper.gameObject);}companionKeepers.Clear();
    }
    private static void ConnectCompanion(string name)
    {
        var settings=PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
        settings.AppVersion=PhotonNetwork.NetworkingClient.AppVersion;settings.FixedRegion=PhotonNetwork.CloudRegion;
        var peer=new LoadBalancingClient();peer.NickName=name;
        var go=new GameObject(name+" heartbeat");UnityEngine.Object.DontDestroyOnLoad(go);
        var keeper=go.AddComponent<ConnectionHandler>();keeper.Client=peer;keeper.StartFallbackSendAckThread();
        companions.Add(peer);companionKeepers.Add(keeper);peer.ConnectUsingSettings(settings);
    }
    private static void SpawnPeer(LoadBalancingClient peer,string prefab)
    {
        var spawner=UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>();
        var position=spawner.ResolveSpawnPosition(Resources.Load<GameObject>(prefab),peer.LocalPlayer.ActorNumber);
        peer.OpRaiseEvent(202,new Hashtable { [(byte)0]=prefab,[(byte)1]=position,[(byte)6]=PhotonNetwork.ServerTimestamp,[(byte)7]=peer.LocalPlayer.ActorNumber*PhotonNetwork.MAX_VIEW_IDS+1 },
            new RaiseEventOptions { Receivers=ReceiverGroup.Others,CachingOption=EventCaching.AddToRoomCache },SendOptions.SendReliable);
    }
    private static void ValidateChat(string screenshot)
    {
        var hud=UnityEngine.Object.FindFirstObjectByType<MultiplayerGameChat>();
        var canvas=hud.GetComponentInParent<Canvas>().transform;
        // Screen.width/height can describe another editor pane during EditorApplication.update.
        // Use the rendered Canvas dimensions when reporting these Game View checks.
        var pixels=((RectTransform)canvas).rect.size*canvas.GetComponent<Canvas>().scaleFactor;
        string resolution=Mathf.RoundToInt(pixels.x)+" x "+Mathf.RoundToInt(pixels.y);
        var safe=canvas.Find("SafeArea");
        var edge=safe.Find("MultiplayerChatEdgeControls");
        Check(hud.transform.parent==canvas&&hud.transform.Find("ChatOverlay")!=null&&edge!=null&&edge.Find("ChatToggle")!=null&&
            Vector2.Distance(((RectTransform)hud.transform).rect.size,((RectTransform)canvas).rect.size)<.1f,
            "Centered chat belongs to the full Canvas; only its edge controls belong to SafeArea at "+resolution);
        MultiplayerUIValidation.InsideFrame((RectTransform)hud.transform.Find("ChatOverlay/ChatWoodFrame"));
        var actions=canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b=>b.name=="ChatToggle"||b.name=="CloseChat"||b.name=="SendMessage").ToArray();
        Check(actions.Length==3&&actions.All(b=>
        {
            var rect=(RectTransform)b.transform;
            float width=b.name=="ChatToggle"&&MultiplayerSceneRoutes.IsOpenArchive(hud.gameObject.scene)?132:220;
            return Mathf.Abs(rect.rect.width-width)<.1f&&Mathf.Abs(rect.rect.height-88)<.1f&&
                Mathf.Abs(b.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier-4.5f)<.01f;
        }),"Chat, Close and Send use matching readable buttons and artwork at "+resolution);
        Check(hud.Conversation.messageList.childCount>0,"Chat cards and controls fit at "+resolution);
        ScreenCapture.CaptureScreenshot("Temp/MultiplayerQA/"+screenshot);
    }
}
