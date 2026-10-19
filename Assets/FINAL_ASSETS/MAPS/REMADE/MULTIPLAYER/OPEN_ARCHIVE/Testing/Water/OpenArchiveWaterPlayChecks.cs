#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Water;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.Tests.Water
{
    public sealed class OpenArchiveWaterPlayChecks : MonoBehaviour
    {
        const string Reports = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/Water/Reports";
        [Serializable] sealed class Check { public string name, detail; public bool passed; }
        [Serializable] sealed class Report {
            public string checkedUtc, scope = "Offline Unity Editor. Uses the enabled original Player, original CharacterController, GameInput mobile movement and MobileJump events. No simulated vertical movement or replacement controller. Multiplayer clients and Android hardware not exercised.";
            public bool passed; public List<Check> checks = new List<Check>();
        }
        readonly Report report = new Report();
        Player player; CharacterController capsule; OpenArchiveWater water; Transform root;
        bool driveComplete;
        int jumps;
        float measuredSpeed;
        public static void Begin() {
            if (!Application.isPlaying || PhotonNetwork.InRoom) throw new InvalidOperationException("Use offline Play Mode.");
            new GameObject("OpenArchive_Water_Play_Checks").AddComponent<OpenArchiveWaterPlayChecks>();
        }
        void Record(string name, bool pass, string detail = "") { report.checks.Add(new Check {name = name, passed = pass, detail = detail}); }
        static Vector3 Polar(float a, float r) { a *= Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a) * r,0,Mathf.Cos(a) * r); }
        void Jumped() { jumps++; }
        void Input(Vector3 direction) {
            direction.y = 0;
            var forward = Camera.main.transform.forward; forward.y = 0; forward.Normalize();
            var right = Camera.main.transform.right; right.y = 0; right.Normalize();
            GameInput.Instance.SetMobileMovement(new Vector2(Vector3.Dot(direction.normalized,right),Vector3.Dot(direction.normalized,forward)));
        }
        void Place(Vector3 point, bool riverbed, float above = 0) {
            Input(Vector3.zero); float height;
            var origin = point + Vector3.up * 30;
            if (riverbed && water.SampleSurface(point,out height)) origin.y = height + .1f;
            RaycastHit floor;
            if (!Physics.Raycast(origin,Vector3.down,out floor,70,~(1<<7),QueryTriggerInteraction.Ignore)) throw new InvalidOperationException("Missing water test support.");
            float bottom = (capsule.center.y - capsule.height * .5f) * player.transform.lossyScale.y;
            point.y = floor.point.y - bottom + capsule.skinWidth + above;
            capsule.enabled = false; player.transform.position = point; capsule.enabled = true; player.ResetMotionAfterTeleport();
        }
        IEnumerator Drive(Vector3 target, float timeout) {
            float end = Time.time + timeout; driveComplete = false;
            while (Time.time < end) {
                var delta = target - player.transform.position; delta.y = 0;
                if (delta.magnitude < .3f) { driveComplete = true; break; }
                Input(delta); yield return null;
            }
            Input(Vector3.zero);
        }
        IEnumerator Measure(Vector3 direction) {
            player.OffRun(); Input(direction); yield return new WaitForSeconds(.15f);
            var start = player.transform.position; float time = Time.time;
            while (Time.time - time < .6f) { Input(direction); yield return null; }
            var delta = player.transform.position-start; delta.y = 0; measuredSpeed = delta.magnitude / (Time.time-time); Input(Vector3.zero);
        }
        float InnerBank(float angle) {
            float lo = 23, hi = 35.5f, height;
            for (int i = 0; i < 24; i++) {
                float mid = (lo+hi)*.5f;
                if (water.SampleSurface(root.TransformPoint(Polar(angle,mid)),out height)) hi=mid; else lo=mid;
            }
            return hi;
        }
        IEnumerator Start() {
            yield return new WaitForSecondsRealtime(.6f);
            report.checkedUtc = DateTime.UtcNow.ToString("o");
            player = Player.LocalInstance; capsule = player.GetComponent<CharacterController>();
            root = GameObject.Find("OpenArchive").transform; water = FindFirstObjectByType<OpenArchiveWater>();
            var session = FindFirstObjectByType<OpenArchiveExplorationSession>();
            bool originalSession = session.enabled, originalBlock = GameInput.Instance.GameplayInputBlocked;
            var originalPosition = player.transform.position; var originalRotation = player.transform.rotation;
            float normalSpeed = player.moveSpeed; player.OnJumpStarted += Jumped;
            try {
                session.enabled = false; GameInput.Instance.SetGameplayInputBlocked(false);
                FindFirstObjectByType<KhezuHuntingController>().Request(KhezuCommand.Sleep);
                Place(root.TransformPoint(new Vector3(0,0,54)),false); yield return new WaitForSeconds(.3f);
                yield return Measure(Vector3.right); float dry = measuredSpeed;
                Record("Original dry movement speed", Mathf.Abs(dry-normalSpeed)<.5f && player.MovementSpeedMultiplier==1, dry.ToString("F2")+"m/s");
                Place(root.TransformPoint(Polar(30,35.5f)),true); yield return new WaitForSeconds(.35f);
                yield return Measure(Polar(120,1)); float wet = measuredSpeed;
                Record("Water slows the real player by 15 percent", water.IsInWater(capsule) && Mathf.Abs(wet-normalSpeed*.85f)<.5f, wet.ToString("F2")+"m/s versus dry "+dry.ToString("F2"));
                water.enabled=false; yield return null;
                Record("Disabling water releases its speed modifier", player.MovementSpeedMultiplier==1);
                water.enabled=true; yield return new WaitForSeconds(.15f);
                for(int i=0;i<4;i++) {
                    float angle=30+i*90; float bank=InnerBank(angle);
                    Place(root.TransformPoint(Polar(angle,bank-1)),false); yield return new WaitForSeconds(.35f);
                    int before=water.LandingSplashCount, jumpBefore=jumps; bool particles=false,audio=false,ripple=false;
                    GameInput.Instance.MobileJump(); float end=Time.time+3;
                    while(Time.time<end && water.LandingSplashCount==before) { Input(Polar(angle,1)); yield return null; }
                    Input(Vector3.zero);
                    if(water.LandingSplashCount>before && water.LastSplash!=null) {
                        particles=water.LastSplash.DropletCount>0; audio=water.LastSplash.sound.isPlaying;
                        ripple=water.LastSplash.ripple.enabled;
                        if(i==0) ScreenCapture.CaptureScreenshot(Reports+"/WaterEntry.png");
                    }
                    Record("Pond "+i+" real jump entry creates splash, ripple and sound", jumps>jumpBefore && water.LandingSplashCount==before+1 && particles && audio && ripple,
                        "jump="+(jumps>jumpBefore)+", droplets="+particles+", ripple="+ripple+", sound="+audio);
                    yield return new WaitForSeconds(.5f);
                    int idleCount=water.LandingSplashCount;
                    yield return new WaitForSeconds(.45f);
                    Record("Pond "+i+" no repeated landing splash while stationary",water.LandingSplashCount==idleCount);
                    var shelf=root.TransformPoint(Polar(angle,bank+1.2f/root.lossyScale.x));
                    yield return Drive(shelf,6);
                    Record("Pond "+i+" walk from deep water up to the shore shelf",driveComplete && capsule.isGrounded,root.InverseTransformPoint(player.transform.position).ToString("F2"));
                    Input(Vector3.zero); yield return new WaitForSeconds(.25f);
                    jumpBefore=jumps; GameInput.Instance.MobileJump();
                    yield return Drive(root.TransformPoint(Polar(angle,bank-2)),3);
                    yield return new WaitForSeconds(.5f);
                    float feetY=OpenArchiveWater.Feet(capsule).y;
                    Record("Pond "+i+" original jump exits water onto ground",driveComplete && jumps>jumpBefore && capsule.isGrounded && feetY>-.25f && player.MovementSpeedMultiplier==1,
                        "jump="+(jumps>jumpBefore)+", feetY="+feetY.ToString("F2")+", arrived="+driveComplete);
                }
                for(int i=0;i<4;i++) {
                    var bridge=root.Find("Environment/Bridges").GetChild(i);
                    var start=bridge.TransformPoint(new Vector3(-7.5f,0,0));
                    var finish=bridge.TransformPoint(new Vector3(7.5f,0,0));
                    Place(start,true); yield return new WaitForSeconds(.35f);
                    var floorBox=bridge.GetComponents<BoxCollider>()[0];
                    float ceiling=bridge.TransformPoint(floorBox.center-Vector3.up*floorBox.size.y*.5f).y;
                    float end=Time.time+7,minGap=999; bool crossed=false; driveComplete=false;
                    while(Time.time<end) {
                        var delta=finish-player.transform.position;delta.y=0;
                        if(delta.magnitude<.3f){driveComplete=true;break;}
                        var local=bridge.InverseTransformPoint(player.transform.position);
                        if(Mathf.Abs(local.x)<1 && Mathf.Abs(local.z)<1) {
                            crossed=true; minGap=Mathf.Min(minGap,ceiling-(OpenArchiveWater.Feet(capsule).y+capsule.height));
                        }
                        Input(delta); yield return null;
                    }
                    Input(Vector3.zero);
                    Record("Bridge "+i+" actual player walks underneath without hitting deck",driveComplete && crossed && minGap>.5f,
                        "arrived="+driveComplete+", under deck="+crossed+", head clearance="+minGap.ToString("F2")+"m");
                }
                var first=root.Find("Environment/Bridges").GetChild(0);
                Place(first.position,false); yield return new WaitForSeconds(.35f);
                Record("Standing on bridge above water keeps full land speed",!water.IsInWater(capsule) && player.MovementSpeedMultiplier==1);
                int count=water.LandingSplashCount;
                Place(root.TransformPoint(new Vector3(0,0,54)),false); yield return new WaitForSeconds(.3f);
                Record("Dry teleport restores speed without a fake splash",player.MovementSpeedMultiplier==1 && water.LandingSplashCount==count);
                Record("Water audio uses the existing sound effects mixer",water.LastSplash!=null && water.LastSplash.sound.outputAudioMixerGroup!=null && water.LastSplash.sound.outputAudioMixerGroup.name=="SFX");
                Record("Serialized walk speed remains unchanged",player.moveSpeed==normalSpeed);
            }
            finally {
                Input(Vector3.zero); player.OnJumpStarted-=Jumped;
                capsule.enabled=false;player.transform.SetPositionAndRotation(originalPosition,originalRotation);capsule.enabled=true;player.ResetMotionAfterTeleport();
                session.enabled=originalSession;GameInput.Instance.SetGameplayInputBlocked(originalBlock);
                report.passed=report.checks.Count>0 && report.checks.TrueForAll(c=>c.passed);
                Directory.CreateDirectory(Reports);File.WriteAllText(Reports+"/WaterPlayMode.json",JsonUtility.ToJson(report,true));
                Debug.Log("Open Archive water checks: "+report.checks.FindAll(c=>c.passed).Count+"/"+report.checks.Count);
                Destroy(gameObject);
            }
        }
    }
}
#endif
