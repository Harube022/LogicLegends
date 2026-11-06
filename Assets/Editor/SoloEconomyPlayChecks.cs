#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LogicLegends.Inference;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>Actual saved-scene failure and quiz UI, isolated identities and wallet transport.</summary>
public static class SoloEconomyPlayChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    const string Report = "Library/SoloEconomyPlayChecks.txt";
    sealed class Receipt
    {
        public string id, status = "active"; public int retries, reward;
        public readonly HashSet<string> retryIds = new HashSet<string>();
        public readonly Dictionary<string, TrialBackendClient.QuizAnswer> answers = new Dictionary<string, TrialBackendClient.QuizAnswer>();
    }
    static readonly Dictionary<string, Receipt> remote = new Dictionary<string, Receipt>();
    static string current, navigation;
    static int coins;
    static Touchscreen touch; static int finger;
    static T Field<T>(object target,string name) => (T)target.GetType().GetField(name,Hidden).GetValue(target);
    static object Call(object target,string method,params object[] args) => target.GetType().GetMethod(method,Hidden).Invoke(target,args);
    static void Set(object target,string name,object value) => target.GetType().GetField(name,Hidden).SetValue(target,value);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Check(bool ok,string text) { if(!ok)throw new InvalidOperationException(text);File.AppendAllText(Report,"PASS "+text+"\n"); }
    [MenuItem("Logic Legends/Economy/Check All Solo Scenes in Play Mode")]
    public static void Begin() => StageHelpGameplayRecorder.CheckSoloEconomy();
    static Task<string> Transport(string path,string json)
    {
        if(path=="/trials/access")return Task.FromResult("{\"admin\":false}");
        var request=JsonUtility.FromJson<TrialBackendClient.Request>(json);var p=SoloEconomy.Trial(request.trial);
        if(path.EndsWith("/start")) {
            if(current==null||request.newAttempt){if(!remote.ContainsKey(request.attemptId))remote[request.attemptId]=new Receipt{id=request.attemptId};current=request.attemptId;}
        }
        var run=remote[path.EndsWith("/start")?current:request.attemptId];
        if(path.EndsWith("/retry")&&run.retryIds.Add(request.operationId)){if(run.retries>=2)throw new InvalidOperationException("Retry limit");run.retries++;}
        if(path.EndsWith("/quiz-answer")&&!run.answers.ContainsKey(request.eventKey)) {
            int correct=0;
            if(request.trial==2) {var bank=AssetDatabase.LoadAssetAtPath<TruthTableDoorQuestionBank>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:TruthTableDoorQuestionBank").Single()));correct=bank.questions[int.Parse(request.questionId.Substring(3))].correctChoice;}
            bool right=request.choice==correct;int pending=run.answers.Values.Sum(q=>q.bonus);
            run.answers[request.eventKey]=new TrialBackendClient.QuizAnswer{eventKey=request.eventKey,questionId=request.questionId,choice=request.choice,correct=right,bonus=right?Math.Min(p.quizReward,p.quizBonusLimit-pending):0};
        }
        if(path.EndsWith("/complete")&&run.status!="completed"){run.reward=SoloEconomy.BaseAfterRetries(request.trial,run.retries)+run.answers.Values.Sum(a=>a.bonus);run.status="completed";coins+=run.reward;}
        return Task.FromResult(JsonUtility.ToJson(new TrialBackendClient.Attempt { economyVersion=2,attemptId=run.id,status=run.status,trial=request.trial,retries=run.retries,reward=run.reward,coins=coins,
            baseReward=p.baseReward,maxRetries=2,retryReductionPercent=25,quizReward=p.quizReward,quizBonusLimit=p.quizBonusLimit,quizAnswers=run.answers.Values.ToArray() }));
    }
    public static IEnumerator Run(string scene)
    {
        if(scene=="PRELIM")File.WriteAllText(Report,"Isolated Play Mode: real failure callbacks, touch input and quiz hooks in ten saved solo scenes. No real accounts or rewards.\n");
        int trial=TrialCatalog.ForScene(scene).Number;
        string uid="solo-economy-qa-"+Guid.NewGuid().ToString("N");
        var fields=BindingFlags.Static|BindingFlags.NonPublic;
        var oldTransport=typeof(TrialBackendClient).GetField("ValidationTransport",fields).GetValue(null);
        typeof(TrialBackendClient).GetField("ValidationTransport",fields).SetValue(null,(Func<string,string,Task<string>>)Transport);
        typeof(TrialBackendClient).GetField("ValidationUid",fields).SetValue(null,uid);
        typeof(TrialBackendClient).GetField("ValidationEmail",fields).SetValue(null,"ordinary@example.test");
        remote.Clear();current=null;coins=37;navigation=null;TrialRewards.SceneLoaded();TrialRewards.PrepareNewAttempt(trial);
        touch=InputSystem.AddDevice<Touchscreen>();finger=100;
        TeleportManager.ValidationNavigation=destination=>navigation=destination;
        GameObject extraCanvas=null;
        try
        {
            if(trial==2)yield return TruthBonus();
            if(trial==3)yield return CrystalBonus();
            if(trial==1)LevelTimerManager.savedTopicIndex=2;
            if(trial==7){Set(Find<SetssssChallengeOne>(),"challengeNumber",3);Set(Find<SetssssChallengeOne>(),"conditionIndex",1);}
            TriggerFailure(trial);yield return null;yield return null;
            var view=UnityEngine.Object.FindObjectsByType<StageResultLayout>(FindObjectsSortMode.None).First(v=>v.gameObject.activeInHierarchy&&v.Extra!=null&&v.Extra.gameObject.activeSelf);
            Check(view.Primary.GetComponentInChildren<TMP_Text>().text=="START OVER",scene+": Start Over is distinct from retry");
            Check(view.Extra.GetComponentInChildren<TMP_Text>().text.Contains("25%"),scene+": first failure offers 25% reduction");
            foreach(int size in new[]{6,12,3}) {
                BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();
                var reward=view.Card.Find("TrialCoinReward").GetComponent<TMP_Text>();reward.ForceMeshUpdate();
                Check(!reward.isTextOverflowing&&!Bounds(reward.rectTransform).Overlaps(Bounds(view.Summary.rectTransform)),scene+": readable reward summary at "+Screen.width+"x"+Screen.height);
                Check(!Bounds(view.Extra.transform as RectTransform).Overlaps(Bounds(view.Primary.transform as RectTransform)),scene+": retry action stays clear of Start Over");
                ScreenCapture.CaptureScreenshot("Library/SoloEconomy_"+trial+"_"+Screen.width+"x"+Screen.height+".png");
            }
            BookMapMobileChecks.SetSize(6);yield return null;yield return null;
            yield return Tap(view.Extra);
            Check(TrialRewards.RetryCount(trial)==1,scene+": actual Try Again callback records one retry");
            Check(trial==7||navigation==scene,scene+": stage retains its existing retry destination or in-place checkpoint");
            if(trial==1)Check(LevelTimerManager.savedTopicIndex==2&&LevelTimerManager.isRespawningFromFail, "Propositional checkpoint and late/early retry timing preserved");
            if(trial==7)Check(Field<int>(Find<SetssssChallengeOne>(),"challengeNumber")==3&&Field<int>(Find<SetssssChallengeOne>(),"conditionIndex")==0,"Sets retry keeps the timed-out challenge");
            TrialRewards.SceneLoaded();yield return null;
            // The remaining result states use the exact same production card after simulating a scene reload.
            extraCanvas=new GameObject("Isolated Economy Result QA",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            extraCanvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;extraCanvas.GetComponent<Canvas>().sortingOrder=3000;
            var scale=extraCanvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
            var result=StageResultCard.Build(extraCanvas.transform,()=>{});result.Stage.text=TrialCatalog.Get(trial).Title;result.Heading.text="TIME IS UP";result.Root.gameObject.SetActive(true);
            var layout=result.Root.GetComponent<StageResultLayout>();layout.Fail(trial,()=>TrialRewards.TryRecordRetry(trial,true));
            yield return null;yield return null;
            Check(result.Extra.GetComponentInChildren<TMP_Text>().text.Contains("50%"),scene+": second failure offers 50% reduction");
            yield return Tap(result.Extra);Check(TrialRewards.RetryCount(trial)==2&&!TrialRewards.CanRetry(trial),scene+": second retry accepted; third blocked");
            layout.Fail(trial,()=>{});yield return null;
            Check(result.Extra.GetComponentInChildren<TMP_Text>().text.Contains("LIBRARY"),scene+": third failure offers Library instead of a third retry");
            yield return Tap(result.Extra);Check(navigation=="LogicGarden"&&TrialRewards.RetryCount(trial)==2,scene+": Study navigates without erasing retry history");
            string old=TrialRewards.AttemptId(trial);layout.Fail(trial,()=>{});yield return null;
            yield return Tap(result.Primary);
            Check(TrialRewards.RetryCount(trial)==0&&TrialRewards.AttemptId(trial)!=old&&navigation==scene,scene+": Start Over creates a genuinely fresh attempt");
            Check(coins==37,scene+": no rewards for failure, retry, Study or Start Over");
            layout.Complete(trial);layout.Complete(trial);yield return null;
            Check(coins==1037,scene+": completion/reopened results pay a fresh run exactly once");
        }
        finally
        {
            TeleportManager.ValidationNavigation=null;
            if(extraCanvas!=null)UnityEngine.Object.Destroy(extraCanvas);
            if(touch!=null)InputSystem.RemoveDevice(touch);
            for(int i=1;i<=10;i++)PlayerPrefs.DeleteKey("TrialRewardOutbox.v1."+uid+"."+i);PlayerPrefs.Save();
            typeof(TrialBackendClient).GetField("ValidationTransport",fields).SetValue(null,oldTransport);
            typeof(TrialBackendClient).GetField("ValidationUid",fields).SetValue(null,null);
            typeof(TrialBackendClient).GetField("ValidationEmail",fields).SetValue(null,null);
            Time.timeScale=1;BookMapMobileChecks.SetSize(6);
        }
        File.AppendAllText(Report,"SCENE PASSED "+scene+"\n");
    }
    static void TriggerFailure(int trial)
    {
        switch(trial) {
            case 1:Call(Find<LevelTimerManager>(),"TriggerGameOver");break;
            case 2:Call(Find<TruthTableStageClock>(),"TriggerGameOver");break;
            case 3:StageJourneyUI.Instance.ShowFailure(3,"Verification",()=>Call(StageJourneyUI.Instance,"RestartInference"));break;
            case 4:Call(Find<ProofsLibraryStageController>(),"Finish",false);break;
            case 5:Call(Find<DirectIndirectProofStageController>(),"Finish",false);break;
            case 6:Find<LogicLegends.Induction.InductionStageManager>().HandleGameOver();break;
            case 7:Call(Find<SetssssChallengeOne>(),"TriggerGameOver");break;
            case 8:Call(Find<FunctionsRelationsStageManager>(),"EnterGameOver");break;
            case 9:Find<LogicLegends.Graph.GraphStageManager>().HandleGameOver();break;
            case 10:Find<LogicLegends.Trees.TreeStageManager>().HandleGameOver();break;
        }
    }
    static IEnumerator TruthBonus()
    {
        var owner=TruthTableDoorInteraction.Active;var clock=Find<TruthTableStageClock>();clock.StartFromBook();
        var door=Field<Collider[]>(owner,"doors")[0];var assigned=Field<TruthTableDoorQuestionBank.Question[]>(owner,"assignedQuestions");
        var at=door.bounds.center+door.transform.forward*2;at.y=Player.LocalInstance.transform.position.y;
        PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,at,Quaternion.identity);yield return null;
        Set(owner,"focusedDoor",door);owner.OpenFocusedDoor();yield return null;
        Check(owner.IsQuizOpen,"Truth Table actual door quiz opens");
        int correct=assigned[0].correctChoice;yield return Tap(Field<Button[]>(owner,"answerButtons")[correct]);
        owner.SubmitAnswer(correct);Check(remote[current].answers.Count==1&&remote[current].answers.Values.Sum(a=>a.bonus)==10&&coins==37,"Truth Table correct answer queues 10 Coins once, without paying immediately");
        Check(TrialRewards.CompletionCoins(2)==1010,"compact Truth Table reward amount includes its pending quiz bonus");
        owner.ClosePanel();owner.ResetAfterColumnEvaluation();Set(owner,"focusedDoor",door);owner.OpenFocusedDoor();yield return null;
        assigned=Field<TruthTableDoorQuestionBank.Question[]>(owner,"assignedQuestions");owner.SubmitAnswer(assigned[0].correctChoice);owner.ClosePanel();
        Check(remote[current].answers.Count==1&&remote[current].answers.Values.Sum(a=>a.bonus)==10,"Truth Table repeated house quiz in the same column cannot farm bonuses");
    }
    static IEnumerator CrystalBonus()
    {
        var stage=Find<InferenceChallenge>();stage.StartFromBook();stage.OpenBoard(Player.LocalInstance);
        yield return (IEnumerator)typeof(InferencePickupPlayChecks).GetMethod("BoardReady",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{stage});
        typeof(InferencePickupPlayChecks).GetMethod("FillPremises",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{stage});stage.validateButton.onClick.Invoke();stage.CloseBoard();
        yield return new WaitForSecondsRealtime(1.2f);
        var crystal=stage.Crystals[0];typeof(InferencePickupPlayChecks).GetMethod("MoveToCrystal",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Player.LocalInstance,crystal});yield return null;
        GameInput.Instance.MobileInteract();yield return null;var quiz=stage.GetComponent<InferenceCrystalQuiz>();
        Check(quiz.IsOpen&&!quiz.IsPractice,"Rule Crystal hand interaction opens the real pickup quiz");
        yield return Tap(quiz.AnswerButtons[quiz.Current.CorrectChoice]);quiz.Answer(quiz.Current.CorrectChoice);
        Check(remote[current].answers.Count==1&&remote[current].answers.Values.Sum(a=>a.bonus)==30&&coins==37,"Rule Crystal correct answer queues 30 Coins once");
        Check(TrialRewards.CompletionCoins(3)==1030,"compact Rule Crystal reward amount includes its pending quiz bonus");
        yield return Tap(quiz.CollectButton);yield return null;
        var held=Player.LocalInstance.GetHeldObject();Check(held!=null,"answered crystal remains collectible");
        held.Drop();Player.LocalInstance.SetHeldObjectSilently(null);yield return null;
        Check(TrialRewards.HasQuizAnswer(3,InferenceCrystalInteraction.QuizEvent(crystal))&&crystal.PickupQuizCompleted,"dropping retains the answered event and crystal quiz latch");
        string eventKey=InferenceCrystalInteraction.QuizEvent(crystal);
        ((System.Collections.IDictionary)typeof(TrialRewards).GetField("stores",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)).Clear();
        Check(TrialRewards.HasQuizAnswer(3,eventKey),"crystal quiz eligibility survives client outbox reload");
        // Practice never queues a reward or changes the remaining time.
        float before=StageJourneyUI.Instance.InferenceRemainingSeconds;int count=remote[current].answers.Count;
        quiz.Open(()=>{},true);quiz.Answer(quiz.Current.CorrectChoice);quiz.Close();
        Check(remote[current].answers.Count==count&&StageJourneyUI.Instance.InferenceRemainingSeconds==before,"practice quiz adds no coins and consumes no countdown time");
    }
    static IEnumerator Tap(Button button)
    {
        Check(button!=null&&button.gameObject.activeInHierarchy&&button.IsInteractable(),"touch target available: "+button?.name);
        Canvas.ForceUpdateCanvases();var point=Bounds((RectTransform)button.transform).center;
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"top UI raycast reaches "+button.name);
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
    static Rect Bounds(RectTransform rect){var c=new Vector3[4];rect.GetWorldCorners(c);var p=c.Select(v=>RectTransformUtility.WorldToScreenPoint(null,v)).ToArray();return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));}
}
#endif
