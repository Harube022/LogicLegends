using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in regressions for terminal UI precedence and queued party return.</summary>
    public static class ArchiveMultiplayerHardeningChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope="Fresh saved MultiplayerGameplay offline Editor Play. The real exit UI update and return-request/tick code use explicit terminal/recovery fixtures. This synchronous check does not claim an actual transport loss, remote rendering, or physical Android verification.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static string Status{get;private set;}="Idle";
        static void Check(Report report,bool pass,string label)
        {
            report.checks.Add((pass?"PASS ":"FAIL ")+label);
            if(!pass)throw new InvalidOperationException(label);
        }
        static object Invoke(object target,string method,params object[] args)
        {
            var member=target.GetType().GetMethod(method,Private);
            if(member==null)throw new MissingMethodException(target.GetType().Name,method);
            try{return member.Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}
        }
        static FieldInfo Field(string name)=>typeof(MultiplayerLobbyService).GetField(name,Private)??throw new MissingFieldException(typeof(MultiplayerLobbyService).Name,name);

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Multiplayer Hardening (Offline Play)")]
        public static void Run()
        {
            var session=ArchiveTopicSession.Current;
            var ui=UnityEngine.Object.FindFirstObjectByType<ArchiveExitUI>();
            if(!Application.isPlaying||PhotonNetwork.InRoom||session==null||!session.Ready||ui==null||ui.session!=session)
                throw new InvalidOperationException("Use fresh saved MultiplayerGameplay offline Play with its exit UI ready.");
            var report=new Report();var state=session.State;
            var originalExit=state.exit;bool originalDefeat=state.survival.defeated,originalSuppression=session.suppressDefeatForVerification;
            string originalReason=state.survival.defeatReason;double originalDefeatedAt=state.survival.defeatedAt;
            var originalCursor=Cursor.lockState;bool originalCursorVisible=Cursor.visible;
            var service=MultiplayerLobbyService.Instance;bool createdService=service==null;
            Dictionary<FieldInfo,object> savedServiceFields=null;
            try
            {
                Status="Running";session.suppressDefeatForVerification=true;
                state.exit=new ArchiveExitState{opened=true,openedAt=ArchiveTopicSession.Clock};
                state.exit.escaped.Add(new ArchiveEscape{actor=session.LocalActor,time=ArchiveTopicSession.Clock});
                state.survival.defeated=false;
                Invoke(ui,"Update");
                var card=(StageResultCard)typeof(ArchiveExitUI).GetField("result",Private).GetValue(ui);
                Check(report,ui.ResultVisible&&!ui.PortalNoticeVisible&&card.Heading.text=="YOU ESCAPED","An escaped survivor sees the real waiting card while the team is unfinished");
                Check(report,!card.Menu.gameObject.activeInHierarchy,"An early escaped survivor cannot abandon remaining teammates through the shared-victory return action");

                state.survival.defeated=true;state.survival.defeatReason=ArchiveTopicSession.MatchTimeoutReason;state.survival.defeatedAt=ArchiveTopicSession.Clock;
                Invoke(ui,"Update");
                Check(report,!ui.ResultVisible&&!ui.PortalNoticeVisible,"Shared timeout hides an escaped survivor's waiting card so it cannot cover the defeat message");
                state.exit.escaped.Clear();Invoke(ui,"Update");
                Check(report,!ui.ResultVisible&&!ui.PortalNoticeVisible,"An unescaped survivor also loses portal notices and victory UI after team defeat");
                state.exit.won=true;Invoke(ui,"Update");
                Check(report,!ui.ResultVisible&&!ui.PortalNoticeVisible,"Defeat takes precedence even if a stale victory flag accompanies the terminal snapshot");

                state.survival.defeated=false;Invoke(ui,"Update");
                Check(report,ui.ResultVisible&&card.Heading.text=="VICTORY"&&!ui.PortalNoticeVisible,"A genuine team victory still displays its proper result");
                Check(report,card.Menu.gameObject.activeInHierarchy&&card.Menu.IsInteractable(),"The team victory retains an available return-to-party button");

                service=MultiplayerLobbyService.EnsureExists();
                savedServiceFields=new Dictionary<FieldInfo,object>();
                foreach(string name in new[]{"recovering","requestedPartyReturn","requestedPartyMatch","nextPartyRequest"})
                {var field=Field(name);savedServiceFields[field]=field.GetValue(service);}
                Field("recovering").SetValue(service,true);Field("requestedPartyReturn").SetValue(service,false);Field("requestedPartyMatch").SetValue(service,null);
                Check(report,!service.InLobby&&service.IsRecovering,"The recovery fixture is outside the Photon room and explicitly marked as recovering");
                service.ReturnToLobbyAfterMatch();
                Check(report,(bool)Field("requestedPartyReturn").GetValue(service)&&(string)Field("requestedPartyMatch").GetValue(service)==state.matchId,"A terminal return requested during recovery queues this match instead of leaving the party");
                Invoke(service,"TickRetainedPartyReturn");Invoke(service,"TickRetainedPartyReturn");
                Check(report,(bool)Field("requestedPartyReturn").GetValue(service)&&(string)Field("requestedPartyMatch").GetValue(service)==state.matchId&&service.IsRecovering,"Repeated disconnected recovery ticks preserve the queued terminal return until transport restoration");
                report.passed=true;Status="Passed";
            }
            catch(Exception e){report.errors.Add(e.ToString());Status="Failed";}
            finally
            {
                if(service!=null&&savedServiceFields!=null)foreach(var saved in savedServiceFields)saved.Key.SetValue(service,saved.Value);
                state.exit=originalExit;state.survival.defeated=originalDefeat;state.survival.defeatReason=originalReason;state.survival.defeatedAt=originalDefeatedAt;
                session.suppressDefeatForVerification=originalSuppression;
                if(ui!=null)Invoke(ui,"Update");Cursor.lockState=originalCursor;Cursor.visible=originalCursorVisible;
                if(createdService&&service!=null)UnityEngine.Object.Destroy(service.gameObject);
                Directory.CreateDirectory("Temp/ArchiveTeamCoordination");File.WriteAllText("Temp/ArchiveTeamCoordination/Hardening.json",JsonUtility.ToJson(report,true));
            }
            Debug.Log("Multiplayer hardening native checks: "+Status+" ("+report.checks.Count+").");
        }
    }
}
