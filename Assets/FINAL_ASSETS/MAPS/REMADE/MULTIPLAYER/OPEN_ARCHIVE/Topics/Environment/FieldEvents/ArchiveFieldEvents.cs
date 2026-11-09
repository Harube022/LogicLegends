using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    [Serializable] public sealed class ArchiveBeaconState
    { public int id,serial;public double cooldownUntil; }
    [Serializable] public sealed class ArchiveFieldEventState
    {
        public int serial,site=-1,kind,triggeredMilestone;
        public double warnedAt=-1,activeAt=-1,endsAt=-1,nextAllowedAt;
        public List<ArchiveBeaconState> beacons=new List<ArchiveBeaconState>();
        public double globalBeaconCooldownUntil,beaconStartedAt=-1,beaconEndsAt=-1;
        public int activeBeacon=-1,beaconActor,beaconMonster,beaconHuntSerial,beaconTarget;
    }
    public static class ArchiveFieldRules
    {
        public const float WarningSeconds=3,ActiveSeconds=18,DissolveSeconds=2,SlowMultiplier=.68f;
        public const float BeaconSeconds=6.5f,BeaconWarningSeconds=.85f,BeaconCooldownSeconds=75;
        public static int Phase(ArchiveFieldEventState state,double now)
        {
            if(state==null||state.site<0||state.warnedAt<0||now<state.warnedAt)return 0;
            if(now<state.activeAt)return 1;
            if(now<state.endsAt)return 2;
            return now<state.endsAt+DissolveSeconds?3:0;
        }
        public static bool Contains(ArchivePathDisturbanceView site,Vector3 feet)
        {
            if(site==null)return false;
            var local=site.transform.InverseTransformPoint(feet);
            return Mathf.Abs(local.y)<=1.4f&&Mathf.Abs(local.x)<=site.halfWidth&&Mathf.Abs(local.z)<=site.halfLength;
        }
    }
    /// <summary>Scene-owned presentation and a local movement modifier. The session owns all shared decisions.</summary>
    [DefaultExecutionOrder(220),DisallowMultipleComponent]
    public sealed class ArchiveFieldEvents : MonoBehaviour
    {
        public ArchiveTopicSession session;
        public ArchivePathDisturbanceView[] hazards=Array.Empty<ArchivePathDisturbanceView>();
        public ArchiveResonanceBeaconView[] beacons=Array.Empty<ArchiveResonanceBeaconView>();
        [Header("Road disturbances")]
        public bool disturbancesEnabled=true;
        [Range(30,180)]public float disturbanceCooldown=75;
        [Header("Resonance beacons")]
        [Range(25,80)]public float guardianHearingRange=55;
        [Range(2,6)]public float interactionRange=4;
        public int FocusedBeacon{get;private set;}=-1;
        public bool LocalSlowed{get;private set;}
        public string FocusNotice{get;private set;}="";
        Player modifiedPlayer;
        int presentedEvent=-1,presentedBeacon=-1;
        int lastFocus=-1;
        float nextFocusPoll,nextTellPoll;
        string cachedFocusNotice="";
        readonly Dictionary<Hunting.KhezuHuntingController,int> shownTells=new Dictionary<Hunting.KhezuHuntingController,int>();
        AudioSource eventVoice,beaconVoice;
        AudioClip warningClip,beaconClip;
        bool Reduced=>session?.hunting?.GetComponent<KhezuCinematicJumpscareController>()?.reducedMotion??false;
        public ArchivePathDisturbanceView Site(int id)=>hazards.FirstOrDefault(v=>v!=null&&v.id==id);
        public ArchiveResonanceBeaconView Beacon(int id)=>beacons.FirstOrDefault(v=>v!=null&&v.id==id);
        void Update()
        {
            if(session==null)session=ArchiveTopicSession.Current;
            var local=Player.LocalInstance;
            if(modifiedPlayer!=null&&modifiedPlayer!=local)ClearSlow();
            bool available=session!=null&&session.Ready&&!session.TeamWon&&!session.TeamDefeated&&!MultiplayerLobbyService.IsGameplayRecovering&&!session.NightEntranceActive;
            var state=available?session.State.fieldEvents:null;double now=ArchiveTopicSession.Clock;
            var feet=local!=null?Feet(local):Vector3.one*10000;
            var activeSite=state==null?null:Site(state.site);
            int phase=available&&activeSite!=null&&!session.PortalOpened?ArchiveFieldRules.Phase(state,now):0;
            bool slow=local!=null&&available&&Time.timeScale>0&&session.CanAct(session.LocalActor)&&phase==2&&ArchiveFieldRules.Contains(Site(state.site),feet);
            if(slow){modifiedPlayer=local;modifiedPlayer.SetMovementSpeedModifier(this,ArchiveFieldRules.SlowMultiplier);}else ClearSlow();
            LocalSlowed=slow;FocusedBeacon=-1;FocusNotice="";
            foreach(var site in hazards)
            {
                if(site==null)continue;
                int shown=phase>0&&state.site==site.id?phase:0;
                float progress=shown==1?(float)((now-state.warnedAt)/ArchiveFieldRules.WarningSeconds):shown==2?(float)((now-state.activeAt)/ArchiveFieldRules.ActiveSeconds):shown==3?(float)((now-state.endsAt)/ArchiveFieldRules.DissolveSeconds):0;
                site.Present(shown,state?.kind??0,Mathf.Clamp01(progress),(float)now,local!=null&&(feet-site.transform.position).sqrMagnitude<110*110,Reduced);
            }
            foreach(var beacon in beacons)
            {
                if(beacon==null)continue;
                var receipt=state?.beacons?.FirstOrDefault(b=>b.id==beacon.id);
                bool active=available&&state!=null&&state.activeBeacon==beacon.id&&now<state.beaconEndsAt;
                double cooldown=Math.Max(receipt?.cooldownUntil??0,state?.globalBeaconCooldownUntil??0);
                float remaining=active?(float)(state.beaconEndsAt-now):Mathf.Max(0,(float)(cooldown-now));
                bool nearby=local!=null&&(feet-beacon.transform.position).sqrMagnitude<18*18;
                beacon.Present(active?2:remaining>0?0:1,remaining,1-Mathf.Clamp01(remaining/ArchiveFieldRules.BeaconCooldownSeconds),(float)now,nearby,Reduced);
                if(available&&local!=null&&session.CanAct(session.LocalActor)&&Time.timeScale>0&&
                    Mathf.Abs(feet.y-beacon.transform.position.y)<2.5f&&Horizontal(feet,beacon.transform.position)<=interactionRange&&
                    ArchiveTopicUI.Current?.ReadingStation==null)
                {
                    FocusedBeacon=beacon.id;
                    if(lastFocus!=beacon.id||Time.unscaledTime>=nextFocusPoll)
                    {
                        lastFocus=beacon.id;nextFocusPoll=Time.unscaledTime+.2f;
                        cachedFocusNotice=session.CanUseBeacon(session.LocalActor,beacon.id,out string reason)?"Tap hand: resonate · distract an awake guardian":"Resonance beacon · "+reason;
                    }
                    FocusNotice=cachedFocusNotice;
                }
            }
            if(phase==1&&activeSite!=null&&state.serial!=presentedEvent&&local!=null&&(feet-activeSite.transform.position).sqrMagnitude<85*85)
            {
                presentedEvent=state.serial;
                if(now-state.warnedAt<1.5){Play(ref eventVoice,ref warningClip,activeSite.transform.position,false);
                    if(string.IsNullOrEmpty(session.LocalNotice))session.ShowLocalNotice("UNSTABLE RUNES · 3-second warning. Keep to the clear side of the path.",4);}
            }
            if(available&&state!=null&&state.activeBeacon>=0&&state.activeBeacon!=presentedBeacon)
            {
                presentedBeacon=state.activeBeacon;
                var beacon=Beacon(state.activeBeacon);
                if(beacon!=null&&local!=null&&(feet-beacon.transform.position).sqrMagnitude<85*85&&now-state.beaconStartedAt<1.5)
                    Play(ref beaconVoice,ref beaconClip,beacon.transform.position,true);
            }
            if(state==null||state.activeBeacon<0)presentedBeacon=-1;
            if(available&&Time.unscaledTime>=nextTellPoll)
            {
                nextTellPoll=Time.unscaledTime+.2f;
                foreach(var monster in Hunting.KhezuHuntingController.ActiveMonsters)
                    if(monster.TargetPlayerId==session.LocalActor&&monster.HuntingPersonalityPhase==Hunting.MonsterHuntingPersonalityPhase.Warning&&
                        (!shownTells.TryGetValue(monster,out int serial)||serial!=monster.HuntingPersonalitySerial))
                    {shownTells[monster]=monster.HuntingPersonalitySerial;if(string.IsNullOrEmpty(session.LocalNotice))session.ShowLocalNotice(monster.HuntingPersonalityHint,2.5f);}
            }
        }
        public bool HandleHand()
        {
            if(FocusedBeacon<0||session==null)return false;
            session.RequestBeacon(FocusedBeacon);return true;
        }
        public static Vector3 Feet(Player player)
        {
            var capsule=player.GetComponent<CharacterController>();
            // Replicas deliberately disable their controllers. Bounds would be
            // empty there; the existing water helper also works for remote feet.
            return capsule!=null?LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(capsule):player.transform.position;
        }
        public static float Horizontal(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        void ClearSlow(){if(modifiedPlayer!=null)modifiedPlayer.RemoveMovementSpeedModifier(this);modifiedPlayer=null;LocalSlowed=false;}
        void Play(ref AudioSource voice,ref AudioClip clip,Vector3 point,bool beacon)
        {
            if(voice==null){var go=new GameObject(beacon?"Resonance_Chime":"Rune_Warning");go.transform.SetParent(transform);voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=1;voice.minDistance=5;voice.maxDistance=70;voice.rolloffMode=AudioRolloffMode.Linear;AudioVolumeSettings.Route(voice,GameAudioChannel.SoundFX);}
            if(clip==null)
            {
                const int rate=22050;var samples=new float[(int)(rate*(beacon?.9f:.55f))];
                for(int i=0;i<samples.Length;i++){float t=(float)i/rate,fade=Mathf.Pow(1-(float)i/samples.Length,2),hz=beacon?330:190;
                    samples[i]=Mathf.Clamp((Mathf.Sin(t*hz*Mathf.PI*2)+.35f*Mathf.Sin(t*hz*1.5f*Mathf.PI*2))*fade*.18f,-1,1);}
                clip=AudioClip.Create(beacon?"Resonance bell":"Rune warning",samples.Length,1,rate,false);clip.SetData(samples,0);
            }
            voice.transform.position=point+Vector3.up*1.2f;voice.PlayOneShot(clip,.65f);
        }
        void OnDisable(){ClearSlow();FocusedBeacon=-1;eventVoice?.Stop();beaconVoice?.Stop();foreach(var site in hazards)if(site!=null)site.Clear();foreach(var beacon in beacons)if(beacon!=null)beacon.Clear();}
        void OnDestroy(){ClearSlow();if(warningClip!=null)Destroy(warningClip);if(beaconClip!=null)Destroy(beaconClip);}
    }
}
