using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Opt-in offline client checks. Uses isolated fake identities; never authenticates or writes a real account.</summary>
public static class TrialAccountPlayChecks
{
    const string Flag="TrialAccounts.Check", Report="Library/TrialAccountChecks.txt";
    const BindingFlags StaticHidden=BindingFlags.Static|BindingFlags.NonPublic;
    static readonly Dictionary<string, Account> accounts=new Dictionary<string, Account>();
    static readonly HashSet<string> identities=new HashSet<string>();
    static string prefix;
    static bool loseCompletionResponse;
    sealed class Account { public int coins=37; public bool admin; public readonly Dictionary<int,Group> trials=new Dictionary<int,Group>(); }
    sealed class Group { public string active; public readonly Dictionary<string, Remote> runs=new Dictionary<string,Remote>(); }
    sealed class Remote { public int retries,reward;public bool admin;public string status="active"; public readonly HashSet<string> retryIds=new HashSet<string>(); }
    public static void Begin()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        System.IO.File.WriteAllText(Report,"Trial reward outbox, access policy and completion UI Play Mode checks\n");
        SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if(!SessionState.GetBool(Flag,false))return;
        prefix="trial-validation-"+Guid.NewGuid().ToString("N");accounts.Clear();identities.Clear();loseCompletionResponse=false;
        Set("ValidationTransport",(Func<string,string,Task<string>>)Transport);
        Identity("ordinary","ordinary@example.test",false);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RunAfterLoad()
    {if(SessionState.GetBool(Flag,false))new GameObject("Trial Account QA Runner").AddComponent<TrialAccountQaRunner>().StartCoroutine(Guard(Run()));}
    [InitializeOnLoadMethod]
    static void Register(){EditorApplication.playModeStateChanged-=Restore;EditorApplication.playModeStateChanged+=Restore;}
    static void Restore(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Flag,false))return;
        SessionState.SetBool(Flag,false);
        foreach(string uid in identities)for(int trial=1;trial<=10;trial++)PlayerPrefs.DeleteKey("TrialRewardOutbox.v1."+uid+"."+trial);
        PlayerPrefs.Save();Set("ValidationTransport",null);Set("ValidationUid",null);Set("ValidationEmail",null);AdminAuthorization.Refresh();
    }
    static void Set(string name,object value)=>typeof(TrialBackendClient).GetField(name,StaticHidden).SetValue(null,value);
    static void Identity(string name,string email,bool admin)
    {
        string uid=prefix+"-"+name;identities.Add(uid);
        if(!accounts.TryGetValue(uid,out var account))accounts[uid]=account=new Account();account.admin=admin;
        Set("ValidationUid",uid);Set("ValidationEmail",email);AdminAuthorization.Refresh();TrialRewards.SceneLoaded();
    }
    static Task<string> Transport(string path,string json)
    {
        string uid=TrialBackendClient.UserId;
        if(uid==null||!accounts.TryGetValue(uid,out var account))throw new InvalidOperationException("Signed out.");
        if(path=="/trials/access")return Task.FromResult(JsonUtility.ToJson(new TrialBackendClient.Access{uid=uid,admin=account.admin}));
        var request=JsonUtility.FromJson<TrialBackendClient.Request>(json);
        if(!account.trials.TryGetValue(request.trial,out var group))account.trials[request.trial]=group=new Group();
        if(path.EndsWith("/start"))
        {
            if(group.active==null||request.newAttempt)
            {
                if(!group.runs.ContainsKey(request.attemptId))group.runs[request.attemptId]=new Remote();
                group.active=request.attemptId;
            }
        }
        string id=path.EndsWith("/start")?group.active:request.attemptId;
        if(id!=group.active||!group.runs.TryGetValue(id,out var run))throw new InvalidOperationException("Attempt changed.");
        if(path.EndsWith("/retry")&&run.retryIds.Add(request.operationId))run.retries++;
        if(path.EndsWith("/admin-used")){if(!account.admin)throw new InvalidOperationException("Forbidden.");run.admin=true;}
        if(path.EndsWith("/complete")&&run.status!="completed")
        {run.reward=run.admin?0:TrialRewards.RewardForRetries(run.retries);account.coins+=run.reward;run.status="completed";}
        if(path.EndsWith("/complete")&&loseCompletionResponse){loseCompletionResponse=false;throw new TimeoutException("Response lost after commit.");}
        return Task.FromResult(JsonUtility.ToJson(new TrialBackendClient.Attempt{trial=request.trial,attemptId=id,status=run.status,retries=run.retries,reward=run.reward,coins=account.coins,adminUsed=run.admin}));
    }
    static IEnumerator Guard(IEnumerator run)
    {
        var stack=new Stack<IEnumerator>();stack.Push(run);
        while(stack.Count>0)
        {
            object next;
            try{var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}next=top.Current;}
            catch(Exception error){System.IO.File.AppendAllText(Report,"FAIL "+error+"\n");Debug.LogException(error);EditorApplication.isPlaying=false;yield break;}
            if(next is IEnumerator child)stack.Push(child);else yield return next;
        }
        Log("ALL TRIAL ACCOUNT CHECKS PASSED");EditorApplication.isPlaying=false;
    }
    static IEnumerator Run()
    {
        yield return null;yield return null;
        Check(new[]{0,1,2,3,4}.Select(TrialRewards.RewardForRetries).SequenceEqual(new[]{1000,750,562,421,316}),"cumulative reward calculation");
        Check(!AdminAuthorization.IsAuthorized&&!AdminAuthorization.TryUseGameplayAdmin(),"ordinary authenticated account cannot act as Admin");
        var adminUi=UnityEngine.Object.FindFirstObjectByType<AdminDebugController>();
        if(adminUi!=null)
        {
            adminUi.AdminButtonOn();
            var button=(Button)typeof(AdminDebugController).GetField("adminButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(adminUi);
            Check(button!=null&&!button.gameObject.activeSelf,"AdminButtonOn cannot grant access to an ordinary account");
        }
        Check(AdminQuestionAnswers.Capture().Count==0,"ordinary account cannot obtain answer snapshots");
        foreach(string email in new[]{"sngbdsa137@gmail.com","russelbarrameda593@gmail.com","famatigabarrameda@gmail.com"})
        {
            Identity(email,email.ToUpperInvariant()+"  ",true);AdminAuthorization.Tick();yield return null;
            Check(AdminAuthorization.IsAuthorized,"backend-confirmed allowlisted account gains access");
            if(adminUi!=null)
            {
                adminUi.AdminButtonOn();
                var button=(Button)typeof(AdminDebugController).GetField("adminButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(adminUi);
                Check(button.gameObject.activeSelf,"existing Admin button is available to a confirmed account");
            }
            var denied=TrialBackendClient.UserId;accounts[denied].admin=false;AdminAuthorization.Refresh();AdminAuthorization.Tick();yield return null;
            Check(!AdminAuthorization.IsAuthorized,"backend rejection overrides an allowlisted client email");
        }
        Identity("logout","sngbdsa137@gmail.com",true);AdminAuthorization.Tick();Check(AdminAuthorization.IsAuthorized,"login refresh");
        Set("ValidationUid",null);Check(!AdminAuthorization.IsAuthorized,"logout immediately revokes cached permission");
        Log("three allowed accounts, normalized emails, ordinary accounts, backend denial, login/logout refresh");
        CheckAdminEntryPoints();
        for(int trial=1;trial<=10;trial++)
        {
            Identity("rewards-"+trial,"ordinary@example.test",false);
            TrialRewards.PrepareNewAttempt(trial);TrialRewards.RecordRetry(trial);TrialRewards.RecordRetry(trial);
            TrialRewards.SceneLoaded();TrialRewards.RecordRetry(trial);
            TrialRewards.Complete(trial);TrialRewards.Complete(trial);yield return null;
            Check(accounts[TrialBackendClient.UserId].coins==599,"two retries award exactly 562 once, trial "+trial);
            ClearMemory();TrialRewards.Complete(trial);yield return null;
            Check(accounts[TrialBackendClient.UserId].coins==599,"results/reload cannot duplicate receipt, trial "+trial);
            TrialRewards.PrepareNewAttempt(trial);loseCompletionResponse=true;TrialRewards.Complete(trial);
            int committed=accounts[TrialBackendClient.UserId].coins;ClearMemory();TrialRewards.Complete(trial);yield return null;
            Check(accounts[TrialBackendClient.UserId].coins==committed&&committed==1599,"lost response and app reload retry the same receipt, trial "+trial);
        }
        Log("all ten trials: distinct retry operations, durable outbox reload, duplicate completion and uncertain commit recovery");
        Identity("pending-login","ordinary@example.test",false);TrialRewards.PrepareNewAttempt(4);loseCompletionResponse=true;TrialRewards.Complete(4);
        ClearMemory();typeof(TrialRewards).GetField("loadedAccount",StaticHidden).SetValue(null,null);TrialRewards.Tick();yield return null;
        Check(TrialRewards.Status(4).Contains("1,000")&&accounts[TrialBackendClient.UserId].coins==1037,"login recovers a pending reward from another trial without requiring that scene");
        Identity("admin-clear","sngbdsa137@gmail.com",true);AdminAuthorization.Tick();TrialRewards.PrepareNewAttempt(TrialRewards.CurrentTrial);
        var exclusion=TrialRewards.PrepareAdminAction();while(!exclusion.IsCompleted)yield return null;
        Check(exclusion.Result&&AdminAuthorization.TryUseGameplayAdmin(),"Admin waits for saved exclusion before clearing");
        TrialRewards.Complete(TrialRewards.CurrentTrial);yield return null;
        Check(accounts[TrialBackendClient.UserId].coins==37,"Admin-assisted content grants no normal coins");
        exclusion=TrialRewards.PrepareAdminAction();while(!exclusion.IsCompleted)yield return null;
        Check(exclusion.Result,"Admin can revisit a completed scene using another excluded attempt");
        TrialRewards.Complete(TrialRewards.CurrentTrial);Check(accounts[TrialBackendClient.UserId].coins==37,"revisiting with Admin cannot duplicate a prior receipt");
        Identity("switch-original","ordinary@example.test",false);TrialRewards.Tick();
        string before=TrialBackendClient.UserId;Set("ValidationUid",prefix+"-switch-other");Set("ValidationEmail","ordinary@example.test");
        identities.Add(TrialBackendClient.UserId);accounts[TrialBackendClient.UserId]=new Account();TrialRewards.Complete(TrialRewards.CurrentTrial);
        Check(accounts[before].coins==37&&accounts[TrialBackendClient.UserId].coins==37,"mid-trial account switch cannot transfer a reward");
        Identity("result-ui","ordinary@example.test",false);
        var root=new GameObject("Trial Reward UI QA",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=2000;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var view=StageResultCard.Build(root.transform,()=>{});view.Summary.text="Time Played: 03:21\nCorrect: 10\nMistakes: 2";view.Root.gameObject.SetActive(true);
        var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(type);
        var property=type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);int old=(int)property.GetValue(window);
        foreach(int size in new[]{2,5})
        {
            property.SetValue(window,size);TrialRewards.PrepareNewAttempt(10);view.Root.GetComponent<StageResultLayout>().Complete(10);
            yield return null;yield return null;Canvas.ForceUpdateCanvases();yield return null;
            var label=view.Card.Find("TrialCoinReward").GetComponent<TMP_Text>();Check(label.text.Contains("1,000"),"completion screen shows confirmed coins");
            Check(!Bounds(label.rectTransform).Overlaps(Bounds(view.Menu.GetComponent<RectTransform>())),"coin reward stays clear of navigation");
            Check(!Bounds(label.rectTransform).Overlaps(Bounds(view.Summary.rectTransform)),"coin reward stays clear of summary");
        }
        property.SetValue(window,old);UnityEngine.Object.Destroy(root);
        Log("Admin exclusion, account-switch isolation and completion reward labels in landscape/portrait");
    }
    static void ClearMemory(){var field=typeof(TrialRewards).GetField("stores",StaticHidden);((System.Collections.IDictionary)field.GetValue(null)).Clear();}
    static Rect Bounds(RectTransform rect){var p=new Vector3[4];rect.GetWorldCorners(p);return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));}
    static void CheckAdminEntryPoints()
    {
        int count=0;
        foreach(var type in typeof(AdminAuthorization).Assembly.GetTypes())
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly))
                if(method.Name=="DebugClearCurrentChallenge"||method.Name=="DebugClearCurrentCondition"||method.Name=="DebugCompleteCurrentChallenge"||method.Name=="ClearActiveBlocksForDebug")
                {
                    byte[] il=method.GetMethodBody().GetILAsByteArray();bool found=false;
                    for(int i=0;i<Math.Min(64,il.Length-4);i++)
                        if(il[i]==0x28)
                            try{var called=method.Module.ResolveMethod(BitConverter.ToInt32(il,i+1));if(called.DeclaringType==typeof(AdminAuthorization)&&called.Name=="TryUseGameplayAdmin")found=true;}catch(ArgumentException){}
                    Check(found,"shared authorization gates "+type.Name+"."+method.Name);count++;
                }
        Check(count>=12,"all stage-specific and nested Admin clear methods inspected");Log(count+" Admin mutation entry points require shared authorization and exclusion");
    }
    static void Check(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
    static void Log(string text)=>System.IO.File.AppendAllText(Report,"PASS "+text+"\n");
}
public sealed class TrialAccountQaRunner:MonoBehaviour{}
