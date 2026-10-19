using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;

public partial class HealthManager
{
    [Serializable] public sealed class ActorHealth {public int actor,hearts,lastPenaltySerial;}
    [SerializeField] bool useAuthoritativeActorHealth;
    [SerializeField] bool pauseOnGameOver=true;
    readonly Dictionary<int,ActorHealth> actorHealth=new Dictionary<int,ActorHealth>();
    bool healthInitialized;
    Player deathLockedPlayer;
    readonly HashSet<Player> deathRestrictedAvatars=new HashSet<Player>();
    public int CurrentHealth {get{InitializeHealth();return currentHealth;}}
    public int MaximumHealth=>hearts==null?0:hearts.Length;
    public bool HasAuthoritativeActorHealth=>useAuthoritativeActorHealth && MaximumHealth>0;
    public event Action<int> HealthChanged;
    int LocalActor=>PhotonNetwork.InRoom?PhotonNetwork.LocalPlayer.ActorNumber:-1;
    void InitializeHealth(){if(healthInitialized)return;healthInitialized=true;currentHealth=MaximumHealth;PaintHealth();}
    public void ConfigureBarrierHealth(GameObject[] heartObjects,GameObject outOfHearts){
        hearts=heartObjects;gameOverPanel=outOfHearts;useAuthoritativeActorHealth=true;pauseOnGameOver=false;healthInitialized=false;
    }
    public void RegisterActors(IEnumerable<int> actors){
        InitializeHealth();var valid=new HashSet<int>(actors);
        foreach(int actor in valid)if(actor!=0 && !actorHealth.ContainsKey(actor))actorHealth[actor]=new ActorHealth{actor=actor,hearts=actor==LocalActor?currentHealth:MaximumHealth};
        foreach(int old in actorHealth.Keys.Where(k=>!valid.Contains(k)).ToArray())actorHealth.Remove(old);
    }
    public bool IsActorAlive(int actor){ActorHealth state;return !actorHealth.TryGetValue(actor,out state) || state.hearts>0;}
    public int ActorHearts(int actor){ActorHealth state;return actorHealth.TryGetValue(actor,out state)?state.hearts:MaximumHealth;}
    public bool DamageAuthoritativeActor(int actor){
        if(!HasAuthoritativeActorHealth || (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) || actor==0)return false;
        InitializeHealth();ActorHealth state;if(!actorHealth.TryGetValue(actor,out state))actorHealth[actor]=state=new ActorHealth{actor=actor,hearts=actor==LocalActor?currentHealth:MaximumHealth};
        if(state.hearts<=0)return false;
        state.hearts--;if(actor==LocalActor)SetAuthoritativeLocalHealth(state.hearts);return true;
    }
    public bool DeductAuthoritativeBarrierHeart(int actor,int captureSerial){
        if(!HasAuthoritativeActorHealth || (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) || actor==0 || captureSerial<=0)return false;
        ActorHealth state;if(!actorHealth.TryGetValue(actor,out state))actorHealth[actor]=state=new ActorHealth{actor=actor,hearts=MaximumHealth};
        if(captureSerial<=state.lastPenaltySerial)return captureSerial==state.lastPenaltySerial;
        state.lastPenaltySerial=captureSerial;DamageAuthoritativeActor(actor);
        return true;
    }
    public ActorHealth[] ExportActorHealth()=>actorHealth.Values.OrderBy(s=>s.actor).Select(s=>new ActorHealth{actor=s.actor,hearts=s.hearts,lastPenaltySerial=s.lastPenaltySerial}).ToArray();
    public void ApplyActorHealthSnapshot(ActorHealth[] states){
        if(!HasAuthoritativeActorHealth || states==null)return;InitializeHealth();
        foreach(var incoming in states){
            if(incoming==null || incoming.actor==0 || incoming.hearts<0 || incoming.hearts>MaximumHealth || incoming.lastPenaltySerial<0)continue;
            ActorHealth previous;if(actorHealth.TryGetValue(incoming.actor,out previous) && previous.lastPenaltySerial>incoming.lastPenaltySerial)continue;
            actorHealth[incoming.actor]=new ActorHealth{actor=incoming.actor,hearts=incoming.hearts,lastPenaltySerial=incoming.lastPenaltySerial};
            if(incoming.actor==LocalActor)SetAuthoritativeLocalHealth(incoming.hearts);
        }
        foreach(var avatar in deathRestrictedAvatars.ToArray()){
            if(avatar==null){deathRestrictedAvatars.Remove(avatar);continue;}
            var view=avatar.GetComponent<PhotonView>();int actor=PhotonNetwork.InRoom && view!=null?view.OwnerActorNr:-1;
            if(IsActorAlive(actor)){avatar.ReleaseMovementLock(this);deathRestrictedAvatars.Remove(avatar);}
        }
    }
    void SetAuthoritativeLocalHealth(int value){currentHealth=Mathf.Clamp(value,0,MaximumHealth);PaintHealth();HealthChanged?.Invoke(currentHealth);}
    void PaintHealth(){
        if(hearts!=null)for(int i=0;i<hearts.Length;i++)if(hearts[i]!=null)hearts[i].SetActive(i<currentHealth);
        if(gameOverPanel!=null)gameOverPanel.SetActive(currentHealth<=0);
        if(!useAuthoritativeActorHealth)return;
        if(currentHealth<=0 && Player.LocalInstance!=null){deathLockedPlayer=Player.LocalInstance;deathLockedPlayer.AcquireMovementLock(this,"Health depleted",deathLockedPlayer.transform.position,200);}
        else if(deathLockedPlayer!=null){deathLockedPlayer.ReleaseMovementLock(this);deathLockedPlayer=null;}
    }
    public void HoldDeathMovement(Player avatar,Vector3 anchor){if(avatar==null || !useAuthoritativeActorHealth)return;deathRestrictedAvatars.Add(avatar);avatar.AcquireMovementLock(this,"Health depleted",anchor,200);}
    void Update(){if(useAuthoritativeActorHealth && currentHealth<=0 && deathLockedPlayer==null)PaintHealth();}
    void OnDestroy(){if(deathLockedPlayer!=null)deathLockedPlayer.ReleaseMovementLock(this);foreach(var p in deathRestrictedAvatars)if(p!=null)p.ReleaseMovementLock(this);}
}
