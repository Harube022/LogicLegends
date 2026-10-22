#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Water;
using LogicLegends.OpenArchiveTesting;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Opt-in native physics/animation checks, never attached to a saved gameplay scene.</summary>
    public sealed class MonsterCloseCounterPlayChecks : MonoBehaviour
    {
        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope = "Both existing rigs in native offline Play Mode, actual navigation/physics and existing capture. Live remote clients and Android remain unverified.";
            public List<Check> checks = new List<Check>();
        }
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Folder = "Temp/CloseCounter";
        readonly Report report = new Report();
        KhezuHuntingController hunt;
        KhezuCaptureEncounterController encounter;
        Player player;
        CharacterController capsule;
        object target;
        void Record(string name, bool pass, string detail = "")
        { report.checks.Add(new Check { name = name, passed = pass, detail = detail }); Save(); }
        void Save() { Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/checks.json", JsonUtility.ToJson(report, true)); }
        object Call(string name, params object[] args) => typeof(KhezuHuntingController).GetMethod(name, Private).Invoke(hunt, args);
        void TickCounter() { Physics.SyncTransforms(); Call("UpdateCloseCounter", target); Call("UpdateCloseCounterPresentation"); }
        void Place(Vector3 point)
        {
            capsule.enabled = false; player.transform.position = point;
            capsule.enabled = true; player.ResetMotionAfterTeleport(); Physics.SyncTransforms();
        }
        Vector3 AtGap(Vector3 direction, float gap)
        {
            direction.y = 0; direction.Normalize();
            var body = hunt.GetComponent<BoxCollider>();
            Vector3 point = body.ClosestPoint(body.bounds.center + direction * 200) + direction * (capsule.radius * player.transform.lossyScale.x + gap);
            RaycastHit floor;
            if (Physics.Raycast(point + Vector3.up * 25, Vector3.down, out floor, 70, hunt.catchObstructionMask, QueryTriggerInteraction.Ignore)) point.y = floor.point.y;
            else point.y = hunt.transform.position.y;
            point.y -= (capsule.center.y - capsule.height * .5f - capsule.skinWidth) * player.transform.lossyScale.y;
            return point;
        }
        void Prepare(DevelopmentMonsterSelection selection, DevelopmentMonster monster)
        {
            hunt.Request(KhezuCommand.Sleep); encounter.SynchronizeBarrierMovement(); selection.Select(monster);
            var filter = new NavMeshQueryFilter { agentTypeID = hunt.Agent.agentTypeID, areaMask = hunt.Agent.areaMask };
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(Vector3.zero, out hit, 15, filter)) throw new InvalidOperationException("Missing existing nest navigation");
            hunt.Agent.enabled = true; hunt.Agent.Warp(hit.position); hunt.Agent.isStopped = true; hunt.transform.rotation = Quaternion.identity;
            Place(AtGap(Vector3.forward, 5));
            var state = hunt.GetSnapshot(); state.state = (int)KhezuHuntState.Ready; state.awake = true;
            state.mistakes = state.stage = 4; state.animation = "Idle"; state.normalizedTime = 0; state.pending = false;
            hunt.ApplySnapshot(state, true); hunt.AuthorityChanged(); hunt.Request(KhezuCommand.StartHunting);
            // AuthorityChanged intentionally disables navigation; a normal chase
            // enables it on its first destination update. This isolated counter fixture must do the same.
            hunt.Agent.enabled = true; hunt.Agent.Warp(hit.position); hunt.Agent.isStopped = true;
            var type = typeof(KhezuHuntingController).GetNestedType("Target", BindingFlags.NonPublic);
            target = Activator.CreateInstance(type); type.GetField("transform").SetValue(target, player.transform);
            type.GetField("capsule").SetValue(target, capsule); type.GetField("name").SetValue(target, "Counter fixture");
        }
        void StrikeFixture()
        {
            var state = hunt.GetSnapshot(); state.counterPhase = 2; state.counterTarget = state.target;
            state.counterStartedAt = KhezuHuntingController.EncounterClock - hunt.counterWarningSeconds - .05;
            state.counterAim = hunt.transform.forward; state.animation = "Attack"; hunt.ApplySnapshot(state, false);
        }
        public static void Begin()
        {
            if (!Application.isPlaying || PhotonNetwork.InRoom || FindFirstObjectByType<MonsterCloseCounterPlayChecks>() != null)
                throw new InvalidOperationException("Use direct offline Play Mode in MultiplayerGameplay");
            Directory.CreateDirectory(Folder);
            new GameObject("Monster_Close_Counter_Checks").AddComponent<MonsterCloseCounterPlayChecks>();
        }
        IEnumerator Start()
        {
            yield return null;
            hunt = FindFirstObjectByType<KhezuHuntingController>(); player = Player.LocalInstance; capsule = player.GetComponent<CharacterController>();
            encounter = hunt.GetComponent<KhezuCaptureEncounterController>();
            var cinematic = hunt.GetComponent<KhezuCinematicJumpscareController>();
            var selection = FindFirstObjectByType<DevelopmentMonsterSelection>();
            var exploration = FindFirstObjectByType<OpenArchiveExplorationSession>();
            var sinking = FindFirstObjectByType<OpenArchiveWaterSinking>();
            bool oldHunt = hunt.enabled, oldPlayer = player.enabled, oldExploration = exploration.enabled, oldEncounter = encounter.enabled,
                oldCinematic = cinematic.enabled, oldSinking = sinking.enabled, oldInput = GameInput.Instance.GameplayInputBlocked;
            Vector3 oldPosition = player.transform.position; Quaternion oldRotation = player.transform.rotation;
            var oldMonster = selection.Selected;
            bool completed = false;
            try
            {
                hunt.enabled = player.enabled = exploration.enabled = encounter.enabled = cinematic.enabled = sinking.enabled = false;
                GameInput.Instance.SetGameplayInputBlocked(true);
                foreach (var monster in new[] { DevelopmentMonster.Rajang, DevelopmentMonster.Khezu })
                {
                    File.WriteAllText(Folder + "/status.txt", monster + " timing and physics");
                    Prepare(selection, monster); int hearts = hunt.health.CurrentHealth, serial = hunt.CaptureSerial;
                    double until = KhezuHuntingController.EncounterClock + 2.1;
                    while (KhezuHuntingController.EncounterClock < until) { TickCounter(); yield return null; }
                    Record(monster + ": standing nearby does not trigger anti-circle counter", hunt.CloseCounterPhase == MonsterCloseCounterPhase.None);
                    Prepare(selection, monster);
                    double orbitAt = KhezuHuntingController.EncounterClock;
                    while (hunt.CloseCounterPhase == MonsterCloseCounterPhase.None && KhezuHuntingController.EncounterClock - orbitAt < 3.2)
                    {
                        float angle = (float)(KhezuHuntingController.EncounterClock - orbitAt) * 130;
                        Place(AtGap(Quaternion.Euler(0, angle, 0) * Vector3.forward, 4));
                        TickCounter(); yield return null;
                    }
                    Record(monster + ": sustained circling triggers warning", hunt.CloseCounterPhase == MonsterCloseCounterPhase.Warning,
                        "elapsed=" + (KhezuHuntingController.EncounterClock - orbitAt));
                    var warning = hunt.transform.Find("Close_Counter_Warning");
                    Record(monster + ": visible colored warning and existing growl are available", warning != null && warning.GetComponent<LineRenderer>().enabled && cinematic.tenseGrowl != null);
                    bool rajangProfile = (bool)typeof(KhezuHuntingController).GetProperty("IsRajangCounter", Private).GetValue(hunt);
                    Color warningColor = warning.GetComponent<LineRenderer>().sharedMaterial.GetColor("_BaseColor");
                    Record(monster + ": counter uses the selected monster profile and palette", rajangProfile == (monster == DevelopmentMonster.Rajang) &&
                        (monster == DevelopmentMonster.Rajang ? warningColor.g > .3f && warningColor.b < .05f : warningColor.g < .1f && warningColor.b > .1f), warningColor.ToString());
                    ScreenCapture.CaptureScreenshot(Folder + "/" + monster + "-warning.png");
                    Record(monster + ": warning cannot capture or deduct hearts", hunt.CaptureSerial == serial && hunt.health.CurrentHealth == hearts && !player.IsMovementLocked);
                    var checkpoint = JsonUtility.FromJson<KhezuSnapshot>(JsonUtility.ToJson(hunt.GetSnapshot()));
                    double originalStart = hunt.CloseCounterStartedAt;
                    hunt.ApplySnapshot(checkpoint, false);
                    Record(monster + ": snapshot restores warning without resetting its deadline", hunt.CloseCounterStartedAt == originalStart && hunt.CloseCounterPhase == MonsterCloseCounterPhase.Warning);
                    Vector3 strikeOrigin = hunt.transform.position;
                    until = KhezuHuntingController.EncounterClock + 1.2;
                    while (hunt.CloseCounterPhase == MonsterCloseCounterPhase.Warning && KhezuHuntingController.EncounterClock < until)
                    { Place(AtGap(Vector3.forward, 18)); TickCounter(); yield return null; }
                    Vector3 aim = hunt.CloseCounterAim;
                    Place(AtGap(-aim, 1)); TickCounter();
                    Record(monster + ": strike aim stays committed when target dodges behind", Vector3.Angle(aim, hunt.CloseCounterAim) < .1f && hunt.State != KhezuHuntState.Caught);
                    until = KhezuHuntingController.EncounterClock + 2;
                    while (hunt.CloseCounterPhase != MonsterCloseCounterPhase.Recovery && hunt.CloseCounterPhase != MonsterCloseCounterPhase.None && KhezuHuntingController.EncounterClock < until)
                    { Place(AtGap(-aim, 1)); TickCounter(); yield return null; }
                    double recoveryAt = KhezuHuntingController.EncounterClock;
                    float strikeTravel = Vector3.Distance(strikeOrigin, hunt.transform.position);
                    Record(monster + ": strike uses the correct sweep or short lunge", monster == DevelopmentMonster.Rajang ? strikeTravel < .15f :
                        strikeTravel > hunt.khezuCounterLunge * .65f && strikeTravel <= hunt.khezuCounterLunge + .5f, "travel=" + strikeTravel);
                    while (hunt.CloseCounterPhase == MonsterCloseCounterPhase.Recovery && KhezuHuntingController.EncounterClock - recoveryAt < 1.3)
                    { Place(AtGap(hunt.transform.forward, .2f)); TickCounter(); yield return null; }
                    Record(monster + ": missed counter grants recovery without a capture", hunt.CaptureSerial == serial && KhezuHuntingController.EncounterClock - recoveryAt >= .65);
                    Record(monster + ": cooldown remains after recovery", hunt.CloseCounterPhase == MonsterCloseCounterPhase.None && hunt.CloseCounterCooldownUntil - KhezuHuntingController.EncounterClock > 3);
                    Prepare(selection, monster); Place(AtGap(Vector3.forward, 1)); StrikeFixture();
                    Record(monster + ": visible target inside strike reach can be hit", (bool)Call("CanCounterHit", target));
                    Place(AtGap(Vector3.forward, 14));
                    Record(monster + ": distant target is outside strike reach", !(bool)Call("CanCounterHit", target));
                    Place(AtGap(Vector3.forward, 1));
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Counter_QA_Wall";
                    wall.transform.position = (player.transform.position + hunt.transform.position) * .5f + Vector3.up * 12;
                    wall.transform.localScale = new Vector3(100, 70, .8f); Physics.SyncTransforms();
                    StrikeFixture(); Record(monster + ": solid wall prevents a strike hit", !(bool)Call("CanCounterHit", target));
                    wall.SetActive(false); Destroy(wall);
                    var high = AtGap(Vector3.forward, 1); high.y = hunt.GetComponent<BoxCollider>().bounds.max.y + 3; Place(high); StrikeFixture();
                    Record(monster + ": platform above the body is outside strike reach", !(bool)Call("CanCounterHit", target));
                    Prepare(selection, monster); Place(AtGap(Vector3.forward, 1));
                    Call("BeginCloseCounter", target); serial = hunt.CaptureSerial;
                    until = KhezuHuntingController.EncounterClock + 1.4;
                    while (hunt.State != KhezuHuntState.Caught && KhezuHuntingController.EncounterClock < until)
                    { TickCounter(); yield return null; }
                    Record(monster + ": counter hit uses the existing capture once", hunt.State == KhezuHuntState.Caught && hunt.CaptureSerial == serial + 1 && hunt.CaughtPlayerId == -1);
                    Record(monster + ": hit waits for the complete warning", hunt.CaptureStartedAt - hunt.CloseCounterStartedAt >= hunt.counterWarningSeconds);
                    Record(monster + ": counter adds no heart penalty", hunt.health.CurrentHealth == hearts);
                }
                Prepare(selection, DevelopmentMonster.Khezu); Place(AtGap(Vector3.forward, 1));
                hunt.enabled = encounter.enabled = cinematic.enabled = true;
                double handoff = KhezuHuntingController.EncounterClock + 12;
                while ((encounter.Phase != KhezuEncounterPhase.BarrierChallenge || cinematic.IsPlaying) && KhezuHuntingController.EncounterClock < handoff) yield return null;
                Record("Native chase capture still completes cinematic and barrier handoff", encounter.Phase == KhezuEncounterPhase.BarrierChallenge && !cinematic.IsPlaying && player.IsMovementLocked);
                completed = true;
            }
            finally
            {
                hunt.Request(KhezuCommand.Sleep); encounter.SynchronizeBarrierMovement(); cinematic.Cancel(); selection.Select(oldMonster);
                capsule.enabled = false; player.transform.SetPositionAndRotation(oldPosition, oldRotation); capsule.enabled = true; player.ResetMotionAfterTeleport();
                hunt.enabled = oldHunt; player.enabled = oldPlayer; exploration.enabled = oldExploration; encounter.enabled = oldEncounter;
                cinematic.enabled = oldCinematic; sinking.enabled = oldSinking; GameInput.Instance.SetGameplayInputBlocked(oldInput);
                report.passed = completed && report.checks.Count > 0 && report.checks.TrueForAll(c => c.passed); Save();
                File.WriteAllText(Folder + "/status.txt", report.passed ? "Passed" : "Failed");
                Debug.Log("Monster close counter checks: " + report.checks.FindAll(c => c.passed).Count + "/" + report.checks.Count);
                Destroy(gameObject);
            }
        }
    }
}
#endif
