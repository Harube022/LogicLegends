#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class LatestSkinPlaybackChecks
{
    static void Check(bool good,string text){if(!good)throw new InvalidOperationException(text);}
    static void Log(string line)=>File.AppendAllText(LatestSkinPlaybackSetup.Report,line+"\n");
    public static string Static(){return LatestSkinSourceMotionChecks.Static();}
    public static IEnumerator Observe(Player player,IntegratedSkinProfile skin,string action)
    {
        yield return new WaitForSeconds(.4f);
        var a=skin.GetComponent<Animator>();var driver=new SerializedObject(skin.GetComponent<PlayerAnimator>());var clip=LatestSkinPlaybackSetup.Clip(a,action);
        int cycles=driver.FindProperty(action.ToLowerInvariant()+"Cycles").intValue;
        float actual=a.GetFloat(action=="Walk"?PlayerAnimator.WALK_PLAYBACK:PlayerAnimator.RUN_PLAYBACK)*2*cycles/clip.length;
        float fraction=Mathf.Clamp01(skin.GetComponent<PlayerAnimator>().PlanarAnimationSpeed/(player.moveSpeed*(action=="Run"?player.RunSpeedMultiplier:1)));
        float expected=driver.FindProperty(action.ToLowerInvariant()+"StepsPerSecond").floatValue*fraction;
        Check(Mathf.Abs(actual-expected)<.12f,skin.itemID+" stable natural "+action+" cadence: "+actual+" / "+expected);
        Check(skin.GetComponent<PlayerStrideWarp>()==null,skin.itemID+" no procedural stride writer");
        var first=a.GetCurrentAnimatorStateInfo(0);float started=Time.time;
        yield return new WaitForSeconds(.35f);
        var last=a.GetCurrentAnimatorStateInfo(0);float advanced=last.normalizedTime-first.normalizedTime;
        float expectedAdvance=(Time.time-started)*a.GetFloat(action=="Walk"?PlayerAnimator.WALK_PLAYBACK:PlayerAnimator.RUN_PLAYBACK)/clip.length;
        float frameAllowance=Time.deltaTime*a.GetFloat(action=="Walk"?PlayerAnimator.WALK_PLAYBACK:PlayerAnimator.RUN_PLAYBACK)/clip.length;
        Check(first.fullPathHash==last.fullPathHash&&Mathf.Abs(advanced-expectedAdvance)<.04f+frameAllowance,skin.itemID+" no duplicated animation tick or repeated transition");
        Log("PLAY CADENCE "+skin.gender+"_"+skin.category+" "+action+"="+actual.ToString("F2")+" steps/s; Animator clock verified; source stride unchanged.");
    }
    public static IEnumerator Idle(IntegratedSkinProfile skin)
    {
        var a=skin.GetComponent<Animator>();var clip=LatestSkinPlaybackSetup.Clip(a,"Idle");
        var start=a.GetCurrentAnimatorStateInfo(0);float time=Time.time;yield return new WaitForSeconds(.35f);
        var end=a.GetCurrentAnimatorStateInfo(0);
        Check(Mathf.Abs(a.speed-1)<.001f&&Mathf.Abs((end.normalizedTime-start.normalizedTime)-(Time.time-time)/clip.length)<.02f+Time.deltaTime/clip.length,skin.itemID+" Idle advances at its original 1x timing");
        Log("PLAY IDLE "+skin.gender+"_"+skin.category+": original 1x clock, no double update.");
    }
    public static void Jump(Player player,IntegratedSkinProfile skin)
    {
        var a=skin.GetComponent<Animator>();var clip=LatestSkinPlaybackSetup.Clip(a,"Jump");
        Check(Mathf.Abs(a.GetFloat(PlayerAnimator.JUMP_PLAYBACK)-clip.length/player.JumpFlightDuration)<.001f,skin.itemID+" Jump fits existing flight time");
        Log("PLAY JUMP "+skin.gender+"_"+skin.category+": one clip over unchanged "+player.JumpFlightDuration.ToString("F2")+"s physics flight.");
    }
    public static IEnumerator Weights(Player player,IntegratedSkinProfile skin)
    {
        var owner=skin.GetComponent<PlayerAnimator>();player.SetRunningSuppressed(owner,true);
        foreach(float weight in new[]{.95f,.88f,.8f,.75f})
        {
            player.SetMovementSpeedModifier(owner,weight);yield return new WaitForSeconds(.5f);
            var velocity=player.GetComponent<CharacterController>().velocity;velocity.y=0;
            Check(Mathf.Abs(velocity.magnitude-player.moveSpeed*weight)<.15f,"carrying speed penalty unchanged");
            yield return Observe(player,skin,"Walk");
        }
        player.RemoveMovementSpeedModifier(owner);player.SetRunningSuppressed(owner,false);
        Log("PLAY WEIGHTS "+skin.gender+"_"+skin.category+": 95/88/80/75 percent retain slower cadence, speed penalties and original item size.");
    }
}
#endif
