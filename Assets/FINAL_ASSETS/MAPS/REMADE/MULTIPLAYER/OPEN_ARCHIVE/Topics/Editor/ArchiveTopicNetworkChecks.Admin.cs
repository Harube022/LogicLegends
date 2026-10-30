using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static partial class ArchiveTopicNetworkChecks
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Topic Admin (Live Photon)")]
        public static void BeginAdmin(){StartChecks(false,admin:true);}
        static Button AdminButton(string name)=>UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(b=>b.name==name);
        static IEnumerator AdminClick(bool complete)
        {
            var toggle=AdminButton("Topic_Admin_Toggle");
            var button=AdminButton(complete?"Admin_Complete_Active_Topic":"Admin_Reset_Active_Topic");
            if(!button.gameObject.activeInHierarchy){toggle.onClick.Invoke();yield return .1f;button=AdminButton(complete?"Admin_Complete_Active_Topic":"Admin_Reset_Active_Topic");}
            yield return Wait(()=>button.isActiveAndEnabled&&button.interactable,"Host Admin action available",8);
            // Verify the real on-screen target is hit before dispatching its normal pointer callback.
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
                position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform)),"Admin action has an unobstructed UI click target");
            int before=session.State.revision;ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            yield return Wait(()=>session.State.revision>before,"Host Admin button commits",8);yield return .12f;
        }
        static IEnumerator AdminBook(Peer peer,int topic)
        {
            yield return Move(peer,session.stations[topic],ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            yield return Send(peer,topic,ArchiveAction.Start);yield return .1f;
        }
        static IEnumerator AdminFlow(Peer a,string room)
        {
            report.scope="Live private Photon host/independent Realtime peer. Native pointer hit tests, guest-forged command rejection, all ten Admin topic skips/resets and checkpointing. Monster threats and their full-screen entrance are suppressed in this isolated UI test; native AdminLab Play and separate WorldPhoton checks verify the actual night event.";
            if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=true;
            Status="Admin UI and authenticated guest rejection";
            yield return Wait(()=>AdminButton("Topic_Admin_Toggle")!=null,"Saved scene builds the Admin entry button");
            Check(session.CanUseTopicAdmin&&AdminButton("Topic_Admin_Toggle").gameObject.activeInHierarchy,"Development host can open Admin while the monster test drawer is collapsed");
            Check(!session.RequestTopicAdmin(0,0,true)&&session.State.stations.All(s=>s.phase==0),"Admin cannot complete a topic that has never been activated");
            yield return AdminBook(a,0);
            var q=session.Question(0);int wrong=Enumerable.Range(0,q.items.Length).First(i=>i!=q.correctChoice);
            yield return Move(a,session.stations[0],session.stations[0].ItemPosition(wrong,q));yield return Send(a,0,ArchiveAction.BeginChoice,wrong);
            yield return Wait(()=>session.State.mistakes.Count==1,"One original wrong answer recorded",8);
            int revision=session.State.revision,actorSequence=session.State.actors.First(p=>p.actor==a.Actor).lastSequence;
            Packet(a,Command(a,0,ArchiveAction.AdminCompleteTopic),true);yield return .2f;
            Packet(a,Command(a,0,ArchiveAction.AdminResetTopic),true);yield return .2f;
            Check(session.State.revision==revision&&session.State.stations[0].phase==1&&session.State.actors.First(p=>p.actor==a.Actor).lastSequence==actorSequence,"Guest-forged complete/reset commands cannot mutate progress or consume its valid command sequence");
            string mistakes=JsonUtility.ToJson(session.State.mistakes[0]);int monsterMistakes=session.hunting.Mistakes;
            string health=JsonUtility.ToJson(session.hunting.GetSnapshot());
            var otherTopics=session.State.stations.Skip(1).Select(JsonUtility.ToJson).ToArray();
            yield return AdminClick(true);
            yield return Wait(()=>a.state.stations[0].phase==2,"Admin completion replicated to guest");
            Check(session.State.stations[0].round==5&&session.ActiveTopic==-1&&!session.State.stations[0].timerRunning&&session.CanStartTopic(1),"Complete active topic skips every remaining challenge, stops its clock and releases the team topic reservation");
            Check(session.State.stations.Skip(1).Select(JsonUtility.ToJson).SequenceEqual(otherTopics)&&JsonUtility.ToJson(session.State.mistakes[0])==mistakes&&session.hunting.Mistakes==monsterMistakes,"Completing a topic preserves other topic states, recorded mistake identity and monster mistake count");
            Check(!session.RequestTopicAdmin(0,session.State.stations[0].revision,false),"Admin cannot silently reset a finished inactive topic");
            Status="Reset carried work, stale packets and expired Book gates";
            yield return AdminBook(a,1);q=session.Question(1);
            yield return Move(a,session.stations[1],session.stations[1].CurrentItemPosition(0,q,session.State.stations[1]));yield return Send(a,1,ArchiveAction.Pick,0);
            yield return .15f;
            Check(session.State.stations[1].carries.Any(c=>c.actor==a.Actor),"Guest holds a real Truth Mosaic tile before reset");
            var stale=Command(a,1,ArchiveAction.Place,0);var oldTile=session.stations[1].workRoot.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Carried_Truth_Tile_Actor_"+a.Actor);
            yield return AdminClick(false);yield return Wait(()=>a.state.stations[1].phase==0,"Admin reset checkpoint replicated");
            var s=session.State.stations[1];
            Check(s.round==0&&s.phase==0&&!s.timerRunning&&!s.timerExpired&&s.timerAttempt==0&&s.carries.Count==0&&session.ActiveTopic==-1,"Reset returns only the active topic to its initial Book gate with a fresh stopped clock");
            Check(oldTile==null&&session.State.stations[0].phase==2&&session.State.mistakes.Count==1,"Reset removes carried tile visuals while preserving completed topics and previous mistakes");
            revision=session.State.revision;Packet(a,stale);yield return .2f;
            Check(session.State.revision==revision&&session.State.stations[1].carries.Count==0,"A pre-reset delayed placement cannot restore stale work");
            yield return AdminBook(a,1);s=session.State.stations[1];
            Check(s.timerRunning&&session.TimerRemaining(s)>358&&s.timerAttempt==1,"Native Book starts the reset topic again using its full six-minute clock");
            s.timerDeadline=ArchiveTopicSession.TimerClock-.01;yield return Wait(()=>session.State.stations[1].timerExpired,"Original shared timer expires",8);yield return .15f;
            Check(session.stations[1].board.text.Contains("TIME UP"),"Timed-out Book gate shows its original timeout presentation");
            int mistakeCount=session.State.mistakes.Count;
            yield return AdminClick(false);yield return .15f;
            Check(!session.stations[1].board.text.Contains("TIME UP")&&!session.State.stations[1].timerExpired&&session.State.mistakes.Count==mistakeCount,"Resetting an expired Book gate rebuilds its visuals and keeps its already recorded timeout mistake");
            Status="All ten topics and final portal through Admin UI";
            for(int topic=1;topic<10;topic++)
            {
                yield return AdminBook(a,topic);
                Check(!session.PortalOpened,"Portal remains closed before Admin completes T"+(topic+1));
                yield return AdminClick(true);
                yield return Wait(()=>a.state.stations[topic].phase==2,"Admin topic completion snapshot arrives");
                Check(session.State.stations[topic].round==ArchiveTopicCatalog.RoundCount(topic)&&session.ActiveTopic==-1&&session.State.mistakes.Count==mistakeCount,"Admin UI clears every remaining source challenge of T"+(topic+1)+" without charging a mistake");
            }
            Check(session.AllTopicsComplete&&session.PortalOpened&&a.state.exit.opened&&!session.TeamWon,"Clearing all ten activated topics opens the existing portal and still requires survivor entry");
            int finalRevision=session.State.revision;
            Check(!session.RequestTopicAdmin(9,session.State.stations[9].revision,true)&&session.State.revision==finalRevision,"A repeated clear cannot duplicate completion or reopen the portal");
            Check(session.hunting.health.ExportActorHealth().All(h=>h.hearts==session.hunting.health.MaximumHealth),"Admin actions do not deduct hearts");
            session.Initialize(314159);session.hunting.Request(LogicLegends.OpenArchive.Hunting.KhezuCommand.Sleep);
            if(session.worldProgression!=null)session.worldProgression.suppressThreatsForVerification=false;
            Status="Completed topic Admin transport checks";
        }
    }
}
