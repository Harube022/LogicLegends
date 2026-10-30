using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using Photon.Pun;
using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    [Serializable] public sealed class ArchiveDownedActor
    {
        public int actor, phase, serial;
        public string carrierId = "";
        public bool carrierAssigned;
        public double createdAt, liftAt, arrivedAt, revivedAt;
        public Vector3 origin, anchor;
    }

    public sealed partial class ArchiveTopicSession
    {
        public const float RescueUseDistance = 4.5f;
        readonly Dictionary<int, Player> rescueBodies = new Dictionary<int, Player>();
        readonly Dictionary<int, TMP_Text> rescueMarkers = new Dictionary<int, TMP_Text>();
        double nextRescueCheckpoint;
        public ArchiveDownedActor Downed(int actor) => Ready && State.survival?.downed != null ?
            State.survival.downed.FirstOrDefault(d => d != null && d.actor == actor && d.phase < 3) : null;

        void NormalizeRescue()
        {
            if (State.survival.downed == null) State.survival.downed = new List<ArchiveDownedActor>();
            State.survival.downed.RemoveAll(d => d == null || d.actor == 0);
        }
        public bool IsRevivalTargetAvailable(int target)
        {
            var fallen=Downed(target);
            return Ready&&target!=0&&fallen!=null&&fallen.phase==2&&SharingActorConnected(target)&&
                !HasEscapedActor(target)&&!TeamWon&&!TeamDefeated&&hunting?.health!=null&&
                hunting.health.HasAuthoritativeActorHealth&&!hunting.health.IsActorAlive(target);
        }
        public bool TryReviveActor(int rescuer, int target)
        {
            if (!IsAuthority || rescuer == target || !CanAct(rescuer) || !IsRevivalTargetAvailable(target)) return false;
            var fallen = Downed(target); var rescuerBody = ActorPlayer(rescuer); var victim = ActorPlayer(target);
            if (fallen == null || fallen.phase != 2 || rescuerBody == null || victim == null || hunting?.health == null ||
                Vector3.Distance(rescuerBody.transform.position, fallen.anchor) > RescueUseDistance ||
                !hunting.health.RestoreAuthoritativeActorHeart(target, true)) return false;
            fallen.phase = 3; fallen.revivedAt = Clock;
            ReleaseRescueBody(target);
            foreach (var monster in KhezuHuntingController.ActiveMonsters) monster.PublishRescueHealth();
            return true;
        }
        void TickRescue()
        {
            if (!Ready || State.survival == null || hunting?.health == null) return;
            NormalizeRescue(); bool dirty = false;
            var monsters = KhezuHuntingController.ActiveMonsters;
            if (IsAuthority && !TeamWon)
            {
                int[] actors = PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Select(p => p.ActorNumber).ToArray() : DebugPracticeActors;
                foreach (int actor in actors)
                {
                    if (hunting.health.IsActorAlive(actor) || HasEscapedActor(actor) || Downed(actor) != null) continue;
                    var victim = ActorPlayer(actor); if (victim == null) continue;
                    int serial = State.survival.downed.Where(d => d.actor == actor).Select(d => d.serial).DefaultIfEmpty(0).Max() + 1;
                    State.survival.downed.RemoveAll(d => d.actor == actor);
                    State.survival.downed.Add(new ArchiveDownedActor { actor = actor, phase = 0, serial = serial,
                        createdAt = Clock, origin = victim.transform.position, anchor = victim.transform.position });
                    ReleaseCarried(actor); dirty = true;
                }
                foreach (var fallen in State.survival.downed.Where(d => d.phase < 3))
                {
                    var victim = ActorPlayer(fallen.actor);
                    var carrier = fallen.carrierAssigned ? monsters.FirstOrDefault(m => m.encounterId == fallen.carrierId) : null;
                    if (!actors.Contains(fallen.actor))
                    {
                        carrier?.EndRescueCarry(fallen.actor); fallen.phase = 3; dirty = true; continue;
                    }
                    if (hunting.health.IsActorAlive(fallen.actor)) { fallen.phase = 3; dirty = true; continue; }
                    if (fallen.phase == 2) continue;
                    // A loading secondary is still the carrier. Keep its checkpoint anchor until it resumes.
                    if(fallen.carrierAssigned&&(carrier==null||!carrier.Initialized))continue;
                    if (carrier == null && victim != null)
                    {
                        // Prefer the actual attacker; otherwise a free monster must walk to the victim.
                        carrier = monsters.Where(m => m.Initialized && !m.CarryingRescueActor && !m.EntranceHeld && !m.IsTranquilized &&
                            m.State!=KhezuHuntState.Waking&&m.State!=KhezuHuntState.ReturningToNest&&m.State!=KhezuHuntState.NestSleeping&&
                            m.State != KhezuHuntState.Caught && !m.FailureStunActive &&
                            !State.survival.downed.Any(d => d != fallen && d.phase < 2 && d.carrierAssigned && d.carrierId == m.encounterId))
                            .OrderByDescending(m => m.ResolvedActor == fallen.actor).ThenBy(m => Vector3.SqrMagnitude(m.transform.position - victim.transform.position)).FirstOrDefault();
                        if (carrier != null && carrier.BeginRescueCarry(fallen.actor))
                        { fallen.carrierAssigned = true; fallen.carrierId = carrier.encounterId; dirty = true; }
                        else carrier = null;
                    }
                    if (carrier == null) continue;
                    if (carrier.RescueActor != fallen.actor && !carrier.BeginRescueCarry(fallen.actor)) continue;
                    if(fallen.phase==0&&carrier.TryRescueWaterRetrieval(fallen.actor,fallen.anchor,out var retrieved))fallen.anchor=retrieved;
                    if (fallen.phase == 1 && !carrier.RescuePickedUp)
                        carrier.RestoreRescueCarry(fallen.actor, false, true); // Resume an in-flight checkpoint after authority handoff.
                    if (fallen.phase == 0 && carrier.RescuePickedUp)
                    { fallen.phase = 1; fallen.liftAt = Clock; fallen.origin = victim != null ? victim.transform.position : fallen.anchor; dirty = true; }
                    if (fallen.phase == 1)
                    {
                        fallen.anchor = carrier.RescueCarryAnchor;
                        if (carrier.RescueAtNest)
                        {
                            fallen.phase = 2; fallen.arrivedAt = Clock;
                            fallen.origin = carrier.RescueCarryAnchor; fallen.anchor = carrier.RescueDropPoint(fallen.actor);
                            carrier.EndRescueCarry(fallen.actor); dirty = true;
                        }
                    }
                }
                // A removed/reset checkpoint cannot leave a controller stranded in carry mode.
                foreach (var monster in monsters.Where(m => m.CarryingRescueActor))
                    if (!State.survival.downed.Any(d => d.actor == monster.RescueActor && d.phase < 2)) monster.EndRescueCarry(monster.RescueActor);
                if (!dirty && State.survival.downed.Any(d => d.phase == 1||d.phase==0&&Vector3.SqrMagnitude(d.anchor-d.origin)>.01f) && Clock >= nextRescueCheckpoint)
                { nextRescueCheckpoint = Clock + 1; dirty = true; }
            }
            foreach (int actor in rescueBodies.Keys.ToArray()) if (Downed(actor) == null || rescueBodies[actor] == null) ReleaseRescueBody(actor);
            foreach (var fallen in State.survival.downed.Where(d => d.phase < 3)) PresentRescue(fallen, monsters);
            if (dirty) Commit();
        }
        void PresentRescue(ArchiveDownedActor fallen, KhezuHuntingController[] monsters)
        {
            var victim = ActorPlayer(fallen.actor); if (victim == null) return;
            if (rescueBodies.TryGetValue(fallen.actor, out var old) && old != victim) ReleaseRescueBody(fallen.actor);
            rescueBodies[fallen.actor] = victim;
            var anchor = fallen.anchor;
            if(fallen.phase==0&&Vector3.SqrMagnitude(fallen.anchor-fallen.origin)>.01f)
                anchor=Vector3.MoveTowards(victim.transform.position,fallen.anchor,1.5f*Time.unscaledDeltaTime);
            if (fallen.phase == 1)
            {
                var carrier = monsters.FirstOrDefault(m => m.encounterId == fallen.carrierId);
                if (carrier != null&&carrier.Initialized&&carrier.CarryingRescueActor&&carrier.RescueActor==fallen.actor) anchor = carrier.RescueCarryAnchor;
                anchor = Vector3.Lerp(fallen.origin, anchor, Mathf.SmoothStep(0, 1, Mathf.Clamp01((float)(Clock - fallen.liftAt))));
            }
            else if (fallen.phase == 2) anchor = Vector3.Lerp(fallen.origin, fallen.anchor, Mathf.SmoothStep(0, 1, Mathf.Clamp01((float)(Clock - fallen.arrivedAt))));
            victim.AcquireMovementLock(this, fallen.phase == 2 ? "Downed at nest: teammate Revival Kit required" : "Fallen teammate: monster carrying to nest", anchor, 250);
            foreach (var skin in victim.GetComponentsInChildren<PlayerAnimator>(true)) { if(fallen.phase==2)skin.ReleaseStun(this);else skin.AcquireStun(this); }
            if (!rescueMarkers.TryGetValue(fallen.actor, out var marker) || marker == null)
            {
                var go = new GameObject("Downed_Rescue_" + fallen.actor); go.transform.SetParent(transform, false);
                marker = go.AddComponent<TextMeshPro>(); marker.fontSize = 3; marker.alignment = TextAlignmentOptions.Center;
                marker.color = new Color(1, .6f, .36f); marker.rectTransform.sizeDelta = new Vector2(11, 3.5f);
                rescueMarkers[fallen.actor] = marker;
            }
            var member = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.GetPlayer(fallen.actor) : null;
            string name = ActorName(fallen.actor);
            marker.text = name + " — DOWNED\n" + (fallen.phase == 2 ? "AT NEST · REVIVAL KIT REQUIRED" : fallen.phase == 1 ? "CARRIED TO NEST" : "WAITING FOR MONSTER PICKUP");
            marker.transform.position = anchor + Vector3.up * 3.4f;
            var cam = Camera.main; if (cam != null) marker.transform.rotation = cam.transform.rotation;
            bool near = Player.LocalInstance != null && Vector3.SqrMagnitude(Player.LocalInstance.transform.position - anchor) < 10000;
            if (marker.gameObject.activeSelf != near) marker.gameObject.SetActive(near);
        }
        void ReleaseRescueBody(int actor)
        {
            if (rescueBodies.TryGetValue(actor, out var victim) && victim != null)
            {
                victim.ReleaseMovementLock(this);
                foreach (var skin in victim.GetComponentsInChildren<PlayerAnimator>(true)) skin.ReleaseStun(this);
            }
            rescueBodies.Remove(actor);
            if (rescueMarkers.TryGetValue(actor, out var marker) && marker != null) Destroy(marker.gameObject);
            rescueMarkers.Remove(actor);
        }
        void CleanupRescue() { foreach (int actor in rescueBodies.Keys.ToArray()) ReleaseRescueBody(actor); }
    }
}
