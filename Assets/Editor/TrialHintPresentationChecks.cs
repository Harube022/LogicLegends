#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only/offline hint presentation checks; never answers or completes a trial.</summary>
public static class TrialHintPresentationChecks
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    const BindingFlags Statics=BindingFlags.Static|BindingFlags.NonPublic;
    public const string Report="Library/TrialHintPresentationChecks.txt";
    public static void Begin()=>File.WriteAllText(Report,"Shared hint presentation. Saved-scene offline checks; no account writes/rewards.\n");
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Fields).GetValue(owner);
    static void Check(bool pass,string text){if(!pass)throw new InvalidOperationException(text);}
    static void Reset(TrialContextHints hints)=>typeof(TrialContextHints).GetMethod("ResetVisual",Fields).Invoke(hints,null);
    static void Render(TrialContextHints hints,string key,string message,bool visible,float delta)=>typeof(TrialContextHints).GetMethod("RenderNotification",Fields).Invoke(hints,new object[]{key,message,visible,delta});
    static Rect ScreenRect(RectTransform rect)
    {
        var points=new Vector3[4];rect.GetWorldCorners(points);var canvas=rect.GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var a=RectTransformUtility.WorldToScreenPoint(camera,points[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,points[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
    }
    public static void Layout(string scene,TrialContextHints hints)
    {
        var settings=Field<RectTransform>(hints,"settingsButton");var help=Field<RectTransform>(hints,"helpButton");
        Check(settings!=null&&help!=null,scene+" has actual Settings/Help dock references");
        Rect panel=ScreenRect(hints.HintPanel),a=ScreenRect(settings),b=ScreenRect(help);
        Check(Mathf.Abs(panel.xMin-a.xMin)<2&&Mathf.Abs(panel.xMax-b.xMax)<2,scene+" hint left/right match Settings/Help at "+Screen.width+"x"+Screen.height+" panel="+panel+" settings="+a+" help="+b);
        Check(panel.yMax<Mathf.Min(a.yMin,b.yMin)-1,scene+" hint below both controls");
        var text=Field<TMP_Text>(hints,"label");text.ForceMeshUpdate();Check(!text.enableAutoSizing&&!text.isTextOverflowing,scene+" wraps without clipping/shrinking");
        var badge=ScreenRect(Field<RectTransform>(hints,"badge"));var label=ScreenRect(text.rectTransform);
        Check(label.xMin>badge.xMax+3&&badge.xMin>panel.xMin+5&&label.xMax<panel.xMax-5&&label.yMin>panel.yMin+5&&label.yMax<panel.yMax-5,scene+" notice and text have internal padding");
        Check(hints.HintPanel.localScale==Vector3.one,scene+" panel does not scale to pulse");
        File.AppendAllText(Report,"PASS "+scene+" "+Screen.width+"x"+Screen.height+": exact dock width, below controls, safe wrapped text and padded notice.\n");
    }
    public static IEnumerator Animations(string scene,TrialContextHints hints)
    {
        hints.enabled=false;
        try
        {
            Reset(hints);int sounds=hints.NotificationPlayCount;
            const string longText="Use the hand button to collect an element, then place it in the required region before submitting your answer.";
            Render(hints,"qa/first",longText,true,0);
            Check(hints.SlideProgress==0&&ScreenRect(hints.HintPanel).xMax<0,scene+" begins fully off-screen left");
            Render(hints,"qa/first",longText,true,.12f);Check(hints.SlideProgress>0&&hints.SlideProgress<1,scene+" smooth partial entrance");
            Check(Field<Image>(hints,"borderPulse").color.a==0,scene+" pulse waits for entrance");
            Render(hints,"qa/replacement","Tap Submit to check your answer.",true,.05f);
            Check(Field<TMP_Text>(hints,"label").text==longText,scene+" replacement waits until old text slides out");
            Render(hints,"qa/replacement","Tap Submit to check your answer.",true,.5f);
            Render(hints,"qa/replacement","Tap Submit to check your answer.",true,.1f);
            Check(hints.NotificationPlayCount==sounds+2,scene+" one sound for each entered hint, none for exit");
            Render(hints,"qa/replacement","Tap Submit to check your answer.",true,.2f);
            Render(hints,"qa/replacement","Tap Submit to check your answer.",true,.1f);
            Check(hints.SlideProgress==1&&Field<Image>(hints,"borderPulse").color.a>0,scene+" border pulses after settling");
            var message=Field<TMP_Text>(hints,"label");var notice=Field<TMP_Text>(hints,"icon");
            Color textColor=message.color;Vector2 textPosition=message.rectTransform.anchoredPosition,textSize=message.rectTransform.sizeDelta,panelSize=hints.HintPanel.sizeDelta;
            float low=1,high=0,previous=0;int peaks=0;bool rising=false;float iconLow=1,iconHigh=0;
            for(int i=0;i<84;i++)
            {
                Render(hints,"qa/replacement","Tap Submit to check your answer.",true,.1f);
                float glow=Field<Image>(hints,"borderPulse").color.a;low=Mathf.Min(low,glow);high=Mathf.Max(high,glow);
                if(rising&&glow<previous-.005f)peaks++;rising=glow>previous+.005f;previous=glow;
                iconLow=Mathf.Min(iconLow,notice.color.g);iconHigh=Mathf.Max(iconHigh,notice.color.g);
                Check(message.color==textColor&&message.rectTransform.anchoredPosition==textPosition&&message.rectTransform.sizeDelta==textSize&&hints.HintPanel.sizeDelta==panelSize&&hints.HintPanel.localScale==Vector3.one&&notice.rectTransform.localScale==Vector3.one,scene+" heartbeat keeps text and geometry steady");
            }
            Check(peaks>=5&&low<.06f&&high>.35f&&high<=.401f&&iconHigh-iconLow>.05f,scene+" border and icon sustain gentle double beats for three cycles");
            Check(hints.NotificationPlayCount==sounds+2,scene+" continuous heartbeat does not replay audio");
            Render(hints,"qa/replacement","Tap Submit to check your connections.",true,.01f);Check(hints.NotificationPlayCount==sounds+2,scene+" text refresh is silent");
            Render(hints,"qa/replacement","Tap Submit to check your connections.",false,.08f);float partial=hints.SlideProgress;
            Check(Field<Image>(hints,"borderPulse").color.a==0&&notice.color==StageUITheme.Gold,scene+" heartbeat stops before slide-out");
            Render(hints,"qa/replacement","Tap Submit to check your connections.",true,.02f);
            Check(hints.SlideProgress>partial&&hints.NotificationPlayCount==sounds+2,scene+" reverse an interrupted exit without duplicate cue");
            Render(hints,"qa/replacement","",false,.5f);Check(hints.Opacity==0&&ScreenRect(hints.HintPanel).xMax<0,scene+" completed action exits to left");
            Render(hints,"qa/pending",longText,false,0);Check(hints.NotificationPlayCount==sounds+2,scene+" pending delay is silent");
            Render(hints,"qa/pending",longText,true,.5f);Check(hints.NotificationPlayCount==sounds+3,scene+" delayed new action has one new cue");
            Layout(scene,hints);
            var sound=Field<AudioSource>(hints,"notification");var clip=Field<AudioClip>(hints,"notificationClip");
            Check(sound.outputAudioMixerGroup!=null&&sound.outputAudioMixerGroup.name=="SFX"&&clip!=null&&clip.name=="Notification",scene+" supplied audio follows saved SFX mixer gain");
            Check(!sound.playOnAwake&&!sound.loop&&sound.spatialBlend==0,scene+" one-shot UI audio source");
            Check(hints.GetComponents<AudioSource>().Length==1&&hints.GetComponentsInChildren<TMP_Text>().Count(t=>t.name=="Action")==1,scene+" one panel and source despite replacements");
        }
        finally{Reset(hints);hints.enabled=true;hints.State.Clear();hints.AdvanceForValidation(0);hints.AdvanceForValidation(30.1f);}
        yield return new WaitForSecondsRealtime(.45f);
        // Exercise the real authenticated Show Hint gate using the existing
        // local backend seam, restoring it without touching a Firebase account.
        var type=typeof(TrialBackendClient);var saved=new Dictionary<string,object>();
        foreach(string field in new[]{"ValidationTransport","ValidationUid","ValidationEmail"})saved[field]=type.GetField(field,Statics).GetValue(null);
        try
        {
            type.GetField("ValidationUid",Statics).SetValue(null,"hint-notification-qa");
            type.GetField("ValidationEmail",Statics).SetValue(null,"ordinary-hint-qa@example.test");
            AdminAuthorization.Refresh();Check(!hints.ShowCurrentForAdmin(),scene+" ordinary account cannot invoke Admin hint");
            type.GetField("ValidationEmail",Statics).SetValue(null,"sngbdsa137@gmail.com");
            type.GetField("ValidationTransport",Statics).SetValue(null,(Func<string,string,Task<string>>)((path,json)=>Task.FromResult(path=="/trials/access"?"{\"uid\":\"hint-notification-qa\",\"admin\":true}":"{}")));
            AdminAuthorization.Refresh();AdminAuthorization.Tick();
            hints.State.Clear();hints.AdvanceForValidation(0);Check(!hints.State.Visible,scene+" fresh hint has delay");
            Check(hints.ShowCurrentForAdmin()&&hints.State.Visible,scene+" authenticated Admin bypasses 30-second wait");
            int sounds=hints.NotificationPlayCount;Check(hints.ShowCurrentForAdmin()&&hints.NotificationPlayCount==sounds,scene+" repeated Admin request does not duplicate notification");
        }
        finally{foreach(var pair in saved)type.GetField(pair.Key,Statics).SetValue(null,pair.Value);AdminAuthorization.Refresh();}
        File.AppendAllText(Report,"PASS "+scene+": interrupt/reverse/replace/refresh, one-shot SFX, no duplicate UI, Admin permission and Show Hint behavior.\n");
    }
}
#endif
