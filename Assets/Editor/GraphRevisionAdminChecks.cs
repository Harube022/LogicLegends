#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LogicLegends.Graph;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Uses the existing Editor-only transport seam. No live account or backend writes.</summary>
public static class GraphRevisionAdminChecks
{
    const string Report="Library/GraphRevision/Admin.txt",Uid="graph-revision-qa";
    static GraphStageManager stage;
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Offline authenticated Admin and ordinary-account checks.\n");
        stage=UnityEngine.Object.FindFirstObjectByType<GraphStageManager>();
        yield return LeaveIntroduction();yield return Activate();
        int before=stage.Correct;stage.DebugClearCurrentQuestion();stage.DebugClearCurrentChallenge();
        Check(stage.Correct==before&&AdminQuestionAnswers.Capture().Count==0,"ordinary account cannot clear or read solutions");
        try
        {
            Set("ValidationUid",Uid);Set("ValidationEmail","sngbdsa137@gmail.com");
            Set("ValidationTransport",(Func<string,string,Task<string>>)Transport);
            AdminAuthorization.Refresh();TrialRewards.SceneLoaded();
            float until=Time.realtimeSinceStartup+8;
            while(!AdminAuthorization.IsAuthorized&&Time.realtimeSinceStartup<until){AdminAuthorization.Tick();yield return null;}
            Check(AdminAuthorization.IsAuthorized,"existing backend verification authorizes the test identity");yield return null;
            var pages=AdminQuestionAnswers.Capture();
            Check(pages.Count==6,"viewer includes current question and five banks");
            Check(pages.Sum(p=>Regex.Matches(p.Body,"SELECTED").Count)==10,"both selected questions marked in each bank");
            Check(pages.Sum(p=>Regex.Matches(p.Body,"CURRENT").Count)==1,"only the actual current bank question is marked current");
            yield return Press("AdminButton");yield return Press("ViewQuestionsAndAnswers");yield return Press("NextGroup");yield return null;
            var scroll=UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None).First(s=>s.name=="AnswerScroll"||s.GetComponentInParent<AdminAnswerViewer>()!=null);
            scroll.verticalNormalizedPosition=0;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Library/GraphRevision/AdminViewer.png");
            yield return Press("BackToAdmin");Check(stage.Correct==before,"opening and scrolling viewer never completes an answer");
            yield return Press("ClearCurrentCondition");yield return Wait(()=>stage.State==GraphStageState.WAITING_FOR_ACTIVATION&&!stage.Moving,"clear question");
            Check(stage.Correct==1&&stage.ChallengeIndex==0&&stage.QuestionIndex==1,"Clear Question advances only to question two");
            Check(TrialRewards.AdminExclusionConfirmed,"Admin actions exclude normal gameplay rewards");
            yield return Activate();yield return Press("AdminButton");yield return Press("ClearCurrentChallenge");
            yield return Wait(()=>stage.ChallengeIndex==1&&!stage.Moving,"clear remaining challenge question");
            Check(stage.Correct==2&&stage.QuestionIndex==0,"Clear Challenge skips the remaining question");
            yield return LeaveIntroduction();yield return Activate();yield return new WaitForSecondsRealtime(1.1f);
            Check(stage.QuestionView.IsOpen,"Admin test enters numerical mode");
            yield return Press("AdminButton");yield return Press("ClearCurrentChallenge");yield return Wait(()=>stage.ChallengeIndex==2&&!stage.Moving,"clear whole degree challenge");
            Check(stage.Correct==4&&stage.QuestionIndex==0&&!stage.QuestionView.IsOpen,"Clear Challenge skips both questions and restores degree camera");
            yield return LeaveIntroduction();yield return Activate();
            yield return Press("AdminButton");yield return Press("ClearCurrentChallenge");yield return Wait(()=>stage.ChallengeIndex==3&&!stage.Moving,"directed example available");
            yield return LeaveIntroduction();yield return Activate();
            yield return ReverseGate();
            Set("ValidationEmail","ordinary@example.test");AdminAuthorization.Refresh();AdminAuthorization.Tick();yield return null;
            before=stage.Correct;stage.DebugClearCurrentQuestion();stage.DebugClearCurrentChallenge();
            Check(!AdminAuthorization.IsAuthorized&&AdminQuestionAnswers.Capture().Count==0&&stage.Correct==before,"account switching immediately revokes all Graph Admin actions");
            File.AppendAllText(Report,"PASS ALL ADMIN QUESTION/CHALLENGE CLEARING, VIEWER, AUTHORIZATION AND REWARD-EXCLUSION CHECKS.\n");
        }
        finally
        {
            foreach(string field in new[]{"ValidationUid","ValidationEmail","ValidationTransport"})Set(field,null);
            AdminAuthorization.Refresh();PlayerPrefs.DeleteKey("TrialRewardOutbox.v1."+Uid+".9");PlayerPrefs.Save();
        }
    }
    static Task<string> Transport(string path,string json)
    {
        if(path=="/trials/access")return Task.FromResult(JsonUtility.ToJson(new TrialBackendClient.Access{uid=Uid,admin=true}));
        var request=JsonUtility.FromJson<TrialBackendClient.Request>(json);
        return Task.FromResult(JsonUtility.ToJson(new TrialBackendClient.Attempt{trial=9,attemptId=request.attemptId,status="active",adminUsed=true,coins=0}));
    }
    static void Set(string field,object value)=>typeof(TrialBackendClient).GetField(field,BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,value);
    static IEnumerator ReverseGate()
    {
        var solution=GraphAnswerValidator.FindSolution(stage.Challenge);string from=solution[0],to=solution[1];
        var target=new GameObject("Reverse Gate QA Target");target.transform.position=stage.arena.TransformPoint(stage.Challenge.Vertex(to).position)+Vector3.up;
        stage.LocalPlayer.BeginGuidedMovement(target.transform,6,.25f);
        yield return Wait(()=>Vector2.Distance(new Vector2(stage.LocalPlayer.transform.position.x,stage.LocalPlayer.transform.position.z),new Vector2(target.transform.position.x,target.transform.position.z))<.4f,"walk directed bridge");
        stage.LocalPlayer.EndGuidedMovement();yield return null;Check(stage.Tracker.Current==to&&stage.Tracker.IsContinuous,"directed bridge forward walk succeeds");
        target.transform.position=stage.arena.TransformPoint(stage.Challenge.Vertex(from).position)+Vector3.up;
        stage.LocalPlayer.BeginGuidedMovement(target.transform,6,.25f);yield return new WaitForSeconds(2.4f);stage.LocalPlayer.EndGuidedMovement();
        Check(stage.Tracker.Current==to&&stage.Tracker.IsContinuous,"narrowed gate blocks reverse travel without adding a false crossing");
        UnityEngine.Object.Destroy(target);
    }
    static IEnumerator Activate()
    {yield return null;yield return new WaitForEndOfFrame();Tap("InteractButton");yield return Wait(()=>!stage.Moving&&stage.State==GraphStageState.ACTIVE,"book activation");yield return new WaitForSecondsRealtime(.25f);}
    static IEnumerator LeaveIntroduction()
    {
        yield return Wait(()=>stage.Introductions.IsOpen,"introduction opens");
        yield return Press("PRELIM Shared Skip Tutorial");yield return Wait(()=>!stage.Introductions.IsOpen,"skip restores gameplay");
    }
    static IEnumerator Wait(Func<bool> ready,string label)
    {float end=Time.realtimeSinceStartup+12;while(!ready()){if(Time.realtimeSinceStartup>end)throw new InvalidOperationException("Admin timeout: "+label);yield return null;}}
    static IEnumerator Press(string name)
    {
        float end=Time.realtimeSinceStartup+6;
        while(!UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b=>b.name==name&&b.interactable))
        {
            if(Time.realtimeSinceStartup>end)throw new InvalidOperationException("Admin button unavailable: "+name+" state="+stage.State+" moving="+stage.Moving+" blocked="+GameInput.Instance.GameplayInputBlocked+" auth="+AdminAuthorization.IsAuthorized);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(.15f);
        Tap(name);yield return null;yield return new WaitForEndOfFrame();yield return null;
    }
    static void Tap(string name)
    {
        Canvas.ForceUpdateCanvases();var b=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(x=>x.name==name);
        Check(b.interactable,"button enabled: "+name);var r=(RectTransform)b.transform;
        var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"touch reaches "+name+" ("+string.Join(", ",hits.Take(3).Select(h=>h.gameObject.name))+")");ExecuteEvents.Execute(b.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    static void Check(bool valid,string message){if(!valid)throw new InvalidOperationException("Graph Admin: "+message);File.AppendAllText(Report,"PASS "+message+"\n");}
}
#endif
