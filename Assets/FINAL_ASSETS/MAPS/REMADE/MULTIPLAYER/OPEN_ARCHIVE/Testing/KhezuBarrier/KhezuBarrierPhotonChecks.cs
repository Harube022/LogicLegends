#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Explicit offline-room transport/checkpoint tests; never attached to a saved scene.</summary>
    public sealed class KhezuBarrierPhotonChecks : MonoBehaviour
    {
        [Serializable] class Check {public string name,detail;public bool passed;}
        [Serializable] class Report {
            public bool passed;public string checkedUtc=DateTime.UtcNow.ToString("o");
            public string scope="One genuine PUN OfflineMode room and one spawned network owner. Confirmed-capture snapshots are explicit fixtures; outcome events use PUN RaiseEvent and its actual sender. Does not establish live 2–4-client transport, late joins, Master migration, remote latency or a physical Android device.";
            public List<Check> checks=new List<Check>();public List<string> errors=new List<string>();
        }
        readonly Report report=new Report();
        public string Stage {get;private set;}
        void Save(){Directory.CreateDirectory(KhezuBarrierPlayChecks.Reports);File.WriteAllText(KhezuBarrierPlayChecks.Reports+"/BarrierPhotonOffline.json",JsonUtility.ToJson(report,true));}
        void Record(string name,bool pass,string detail=""){report.checks.Add(new Check{name=name,passed=pass,detail=detail});Save();}
        void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)report.errors.Add(message);}
        public static void Begin(){if(!Application.isPlaying || PhotonNetwork.InRoom || PhotonNetwork.IsConnected)throw new InvalidOperationException("Start disconnected in Play Mode.");new GameObject("Khezu_Barrier_Photon_Checks").AddComponent<KhezuBarrierPhotonChecks>();}
        static void Send(object[] data){PhotonNetwork.RaiseEvent(KhezuCaptureEncounterController.DevelopmentOutcomeEvent,data,new RaiseEventOptions{Receivers=ReceiverGroup.MasterClient},SendOptions.SendReliable);}
        static object[] Packet(int serial,bool success){return new object[]{KhezuCaptureEncounterController.DevelopmentOutcomeChannel,serial,success};}
        static KhezuSnapshot Capture(KhezuHuntingController h,int actor,int serial,Vector3 anchor){
            var s=h.GetSnapshot();s.state=(int)KhezuHuntState.Caught;s.caught=s.target=actor;s.captureSerial=serial;s.capturePosition=anchor;s.captureAnchorValid=true;s.captureStartedAt=KhezuHuntingController.EncounterClock-3;s.encounterPhase=4;s.awake=true;s.pending=false;s.animation="Idle";s.animationSpeed=1;return s;
        }
        IEnumerator Start(){
            DontDestroyOnLoad(gameObject);string scene=SceneManager.GetActiveScene().name;bool complete=false;Application.logMessageReceived+=Log;
            try{
                Stage="PUN room and real network avatar";PhotonNetwork.OfflineMode=true;bool created=PhotonNetwork.CreateRoom("Khezu_Phase3B_OfflineChecks");
                float end=Time.realtimeSinceStartup+5;while(!PhotonNetwork.InRoom && Time.realtimeSinceStartup<end)yield return null;
                Record("Actual PUN offline room has an authoritative Master",created && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient);
                yield return SceneManager.LoadSceneAsync(scene);yield return new WaitForSecondsRealtime(1.2f);
                var h=FindFirstObjectByType<KhezuHuntingController>();var e=h.GetComponent<KhezuCaptureEncounterController>();var ui=FindFirstObjectByType<KhezuBarrierDevelopmentUI>();var p=Player.LocalInstance;var view=p.GetComponent<PhotonView>();var sync=p.GetComponent<MultiplayerAvatarSync>();int actor=PhotonNetwork.LocalPlayer.ActorNumber;
                Record("Existing spawner creates one real owned network character",view!=null && view.ViewID!=0 && view.IsMine && view.OwnerActorNr==actor && sync!=null && FindObjectsByType<Player>(FindObjectsSortMode.None).Length==1);
                h.health.TakeDamage();int hp=h.health.CurrentHealth;
                Record("Existing TakeDamage and barrier outcomes share one authoritative heart count",hp==h.health.MaximumHealth-1 && h.health.ActorHearts(actor)==hp);
                var anchor=p.transform.position;int serial=h.CaptureSerial+1;
                h.ApplySnapshot(Capture(h,actor,serial,anchor),true);yield return new WaitForSeconds(.3f);
                Record("Master-confirmed capture locks the network owner and opens its UI",p.IsMovementLocked && ui.Visible && e.LocalChallengeActive && p.enabled && p.GetComponent<CharacterController>().enabled,
                    "locked="+p.IsMovementLocked+", ui="+ui.Visible+", localChallenge="+e.LocalChallengeActive+", playerEnabled="+p.enabled+", capsuleEnabled="+p.GetComponent<CharacterController>().enabled+", state="+h.State+", phase="+e.Phase+", cinematic="+e.cinematic.IsPlaying);
                Stage="Owner movement reconciliation";
                var stream=new PhotonStream(false,new object[]{anchor+new Vector3(50,10,50),Quaternion.identity,true,true,true});
                sync.OnPhotonSerializeView(stream,new PhotonMessageInfo(PhotonNetwork.LocalPlayer,PhotonNetwork.ServerTimestamp,view));
                var target=(Vector3)typeof(MultiplayerAvatarSync).GetField("targetPosition",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sync);
                Record("Authoritative avatar receiver clamps even an owner-authored escape packet",Vector3.Distance(target,anchor)<.001f && Vector3.Distance(p.transform.position,anchor)<.001f);
                bool wasEnabled=p.enabled;p.enabled=false;p.transform.position+=Vector3.right*30;yield return new WaitForEndOfFrame();
                Record("Capture enforcement still holds when the movement script is disabled on a replica",Vector3.Distance(p.transform.position,anchor)<.001f);p.enabled=wasEnabled;
                var write=new PhotonStream(true,null);
                // This PUN version initializes its internal write buffer via SetWriteStream.
                typeof(PhotonStream).GetMethod("SetWriteStream",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(write,new object[]{new List<object>(),0});
                sync.OnPhotonSerializeView(write,new PhotonMessageInfo(PhotonNetwork.LocalPlayer,PhotonNetwork.ServerTimestamp,view));var fields=write.ToArray();
                Record("Existing five-field owner serialization sends the anchor and stopped animation flags",fields.Length==5 && Vector3.Distance((Vector3)fields[0],anchor)<.001f && !(bool)fields[2] && !(bool)fields[3] && !(bool)fields[4]);
                sync.OnPhotonSerializeView(new PhotonStream(false,new object[]{anchor+Vector3.one*100,Quaternion.identity,true,true,true}),new PhotonMessageInfo(null,0,view));
                Record("Non-owner movement packets are ignored",Vector3.Distance((Vector3)typeof(MultiplayerAvatarSync).GetField("targetPosition",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sync),anchor)<.001f);
                Stage="Actual Photon outcome events";
                e.developmentTestOutcomes=false;Send(Packet(serial,false));Record("Actual sender event is rejected when host development testing is disabled",!h.BarrierResolving);e.developmentTestOutcomes=true;
                Send(new object[]{KhezuCaptureEncounterController.DevelopmentOutcomeChannel,serial,false,actor});Send(Packet(serial-1,false));
                Record("PUN dispatch rejects payload identity injection and stale serials",!h.BarrierResolving);
                var forged=new EventData{Code=KhezuCaptureEncounterController.DevelopmentOutcomeEvent};forged.Parameters[ParameterCode.Data]=Packet(serial,false);forged.Parameters[ParameterCode.ActorNr]=999;e.OnEvent(forged);
                Record("Unknown sender fixture cannot resolve the captured player's encounter",!h.BarrierResolving);
                h.ApplySnapshot(Capture(h,999,serial+1,anchor),true);yield return null;Send(Packet(serial+1,false));
                Record("Real owner cannot resolve another actor's challenge",!h.BarrierResolving && !p.IsMovementLocked && !ui.Visible);
                h.ApplySnapshot(Capture(h,actor,serial+2,anchor),true);yield return new WaitForSeconds(.15f);serial=h.CaptureSerial;
                Send(Packet(serial,false));Send(Packet(serial,false));Send(Packet(serial,true));
                Record("PUN-authenticated sender produces one accepted failure, rejecting duplicate/opposite results",h.BarrierResolving && h.ResolvedOutcome==2 && h.ResolvedActor==actor && h.ResolvedSerial==serial && p.IsMovementLocked);
                end=Time.realtimeSinceStartup+3;while(h.State==KhezuHuntState.Caught && Time.realtimeSinceStartup<end)yield return null;yield return new WaitForEndOfFrame();
                Record("Network actor receives exactly one heart loss and a five-second stun",h.health.CurrentHealth==hp-1 && h.health.ActorHearts(actor)==hp-1 && p.IsMovementLocked && h.FailureStunActive && h.State==KhezuHuntState.AttackRecovery);
                object saved;var packet=PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(KhezuPhotonSync.SnapshotKey,out saved)?saved as object[]:null;
                var checkpoint=packet!=null && packet.Length==4?JsonUtility.FromJson<KhezuSnapshot>((string)packet[3]):null;
                Record("Room checkpoint carries canonical anchor, processed outcome and absolute per-actor health",checkpoint!=null && checkpoint.captureAnchorValid && Vector3.Distance(checkpoint.capturePosition,anchor)<.001f && checkpoint.resolvedSerial==serial && checkpoint.resolvedActor==actor && checkpoint.resolvedOutcome==2 && checkpoint.healthStates.Any(v=>v.actor==actor && v.hearts==hp-1 && v.lastPenaltySerial==serial),checkpoint==null?"No checkpoint":"animation="+checkpoint.animation);
                if(checkpoint!=null){h.ApplySnapshot(checkpoint,true);h.AuthorityChanged();}
                Send(Packet(serial,false));yield return null;Record("Checkpoint restoration cannot replay a heart penalty or skip the owner's remaining stun",h.health.CurrentHealth==hp-1 && p.IsMovementLocked && h.FailureStunActive);
                h.Request(KhezuCommand.Sleep);h.ApplySnapshot(Capture(h,actor,serial+1,p.transform.position),true);yield return null;e.OnDisconnected(DisconnectCause.DisconnectByClientLogic);yield return null;
                Record("Disconnected owner releases the barrier token and hides the UI",!p.IsMovementLocked && !ui.Visible);complete=true;
            }finally{
                Record("Offline transport checks completed",complete);Application.logMessageReceived-=Log;report.passed=complete && report.errors.Count==0 && report.checks.All(c=>c.passed);Save();
                Debug.Log("Khezu Phase 3B Photon offline checks: "+report.checks.Count(c=>c.passed)+"/"+report.checks.Count);StartCoroutine(Cleanup(scene));
            }
        }
        IEnumerator Cleanup(string scene){
            Stage="Cleanup";if(PhotonNetwork.InRoom)PhotonNetwork.LeaveRoom();float end=Time.realtimeSinceStartup+5;while(PhotonNetwork.InRoom && Time.realtimeSinceStartup<end)yield return null;
            if(!PhotonNetwork.InRoom)PhotonNetwork.OfflineMode=false;yield return SceneManager.LoadSceneAsync(scene);Destroy(gameObject);
        }
    }
}
#endif
