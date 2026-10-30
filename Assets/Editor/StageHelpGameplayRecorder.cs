#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LogicLegends.Inference;

/// <summary>Records real Game view actions in an isolated, offline Editor test attempt.</summary>
public static class StageHelpGameplayRecorder
{
    const string Flag="Help.RecordGameplay", Folder="Assets/Resources/StageHelpVideos", Report="Library/StageHelpGameplayRecording.txt";
    const string CaptureFolder="Library/StageHelpCaptures";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly string[] Scenes={"PRELIM","TruthTable","RulesOfInference","Formal vs. Informal Proofs","Direct&Indirect","Induction","Setssss","Functions and Relations","Graph","Trees"};
    static readonly string[] History={"FunctionsRelations.LastVariant.","LogicLegends.Induction.LastVariant.","LogicLegends.ProofsLibrary.LastVariant.","LogicLegends.Graph.LastVariant.","LogicLegends.Trees.LastVariant."};
    static RecorderController recorder;
    static RecorderControllerSettings settings;
    static List<StageHelpVideoCatalog.Cue> cues;
    static int frames;
    static string sceneName;
    static StageHelpRecordingRunner runner;
    static double nextImportTry;
    [MenuItem("Logic Legends/Help/Record Stage Gameplay Demonstrations")]
    public static void RecordAll()=>Begin();
    public static void CheckInduction()
    {SessionState.SetBool("Help.InductionVerify",true);Begin("Induction");}
    public static void CheckInductionPresentation()
    {SessionState.SetBool("Help.InductionPresentationVerify",true);Begin("Induction");}
    public static void CheckSets()
    {SessionState.SetBool("Help.SetsVerify",true);Begin("Setssss");}
    public static void CheckFunctionsTutorial()
    {SessionState.SetBool("Help.FunctionsTutorialVerify",true);Begin("Functions and Relations");}
    public static void CheckFunctionsGarden()
    {SessionState.SetBool("Help.FunctionsGardenVerify",true);CheckFunctionsTutorial();}
    public static void CheckTreesPlaylist()
    {SessionState.SetBool("Help.TreesPlaylistVerify",true);CheckTreesRevision();}
    public static void CheckTreesRevision()
    {SessionState.SetBool("Help.TreesRevisionVerify",true);Begin("Trees");}
    public static void CheckTruthSunset()
    {SessionState.SetBool("Help.TruthNightOnly",true);Begin("TruthTable");}
    public static void CheckTruthNight()
    {SessionState.SetBool("Help.TruthNightOnly",true);Begin("TruthTable");}
    public static void CheckTruthHolograms()
    {SessionState.SetBool("Help.TruthHologramVerify",true);Begin("TruthTable");}
    public static void CheckGraphRevision()
    {SessionState.SetBool("Help.GraphRevisionVerify",true);Begin("Graph");}
    public static void CheckGraphHighlights()
    {SessionState.SetBool("Help.GraphHighlightsOnly",true);CheckGraphRevision();}
    public static void CheckGraphPlaylist()
    {SessionState.SetBool("Help.GraphPlaylistOnly",true);CheckGraphRevision();}
    public static void CheckGraphDirected()
    {SessionState.SetBool("Help.GraphDirectedOnly",true);CheckGraphRevision();}
    public static void CheckGraphAdmin()
    {SessionState.SetBool("Help.GraphRevisionAdminOnly",true);CheckGraphRevision();}
    public static void CheckFunctions()
    {SessionState.SetBool("Help.FunctionsVerify",true);Begin("Functions and Relations");}
    public static void Begin(string onlyScene="")
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Exit Play Mode and save the scene first.");
        SessionState.SetBool(Flag+".PriorStagePersistence",StageCompleteManager.SuppressPersistenceForValidation);
        SessionState.SetBool(Flag+".PriorGraphPersistence",LogicLegends.Graph.GraphCompletion.SuppressPersistenceForValidation);
        SessionState.SetBool(Flag+".PriorProofsProgression",ProofsLibraryStageController.SuppressProgressionForValidation);
        SessionState.SetBool(Flag+".PriorDirectProgression",DirectIndirectProofStageController.SuppressProgressionForValidation);
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(CaptureFolder); File.WriteAllText(Report,"Actual Game view recordings; offline first-question test attempts, no completion rewards.\n");
        SessionState.SetString(Flag+".Scene",SceneManager.GetActiveScene().path);SessionState.SetInt(Flag+".Size",BookMapMobileChecks.GetSize());
        SessionState.SetInt(Flag+".SafeArea",(int)Crystal.SafeArea.Sim);
        SessionState.SetString(Flag+".Only",onlyScene);SessionState.SetBool(Flag,true);
        foreach(string prefix in History)for(int i=0;i<5;i++){string key=prefix+i;SessionState.SetBool(Flag+key,PlayerPrefs.HasKey(key));SessionState.SetInt(Flag+key+".Value",PlayerPrefs.GetInt(key));}
        EditorSceneManager.OpenScene("Assets/Scenes/"+(string.IsNullOrEmpty(onlyScene)?Scenes[0]:onlyScene)+".unity");EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Register()
    {
        EditorApplication.playModeStateChanged-=Restore;EditorApplication.playModeStateChanged+=Restore;
        EditorApplication.update-=ImportPendingClips;EditorApplication.update+=ImportPendingClips;
    }
    static void Restore(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Flag,false))return;
        SessionState.SetBool(Flag,false);SceneManager.sceneLoaded-=Loaded;
        SessionState.SetBool("Help.InductionVerify",false);
        SessionState.SetBool("Help.InductionPresentationVerify",false);
        SessionState.SetBool("Help.SetsVerify",false);
        SessionState.SetBool("Help.FunctionsVerify",false);
        SessionState.SetBool("Help.FunctionsTutorialVerify",false);
        SessionState.SetBool("Help.FunctionsGardenVerify",false);
        SessionState.SetBool("Help.TreesRevisionVerify",false);
        SessionState.SetBool("Help.TreesPlaylistVerify",false);
        SessionState.SetBool("Help.TreesInspectOnly",false);
        SessionState.SetBool("Help.TruthHologramVerify",false);
        SessionState.SetBool("Help.TruthNightOnly",false);
        SessionState.SetBool("Help.GraphRevisionVerify",false);
        SessionState.SetBool("Help.GraphHighlightsOnly",false);
        SessionState.SetBool("Help.GraphPlaylistOnly",false);
        SessionState.SetBool("Help.GraphDirectedOnly",false);
        SessionState.SetBool("Help.GraphRevisionAdminOnly",false);
        // Restore these explicitly as Editor statics may survive a Play Mode transition.
        StageCompleteManager.SuppressPersistenceForValidation=SessionState.GetBool(Flag+".PriorStagePersistence",false);
        LogicLegends.Graph.GraphCompletion.SuppressPersistenceForValidation=SessionState.GetBool(Flag+".PriorGraphPersistence",false);
        ProofsLibraryStageController.SuppressProgressionForValidation=SessionState.GetBool(Flag+".PriorProofsProgression",false);
        DirectIndirectProofStageController.SuppressProgressionForValidation=SessionState.GetBool(Flag+".PriorDirectProgression",false);
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        foreach(string field in new[]{"ValidationTransport","ValidationUid","ValidationEmail"})typeof(TrialBackendClient).GetField(field,flags).SetValue(null,null);
        foreach(string prefix in History)for(int i=0;i<5;i++){string key=prefix+i;if(SessionState.GetBool(Flag+key,false))PlayerPrefs.SetInt(key,SessionState.GetInt(Flag+key+".Value",0));else PlayerPrefs.DeleteKey(key);}
        PlayerPrefs.Save();BookMapMobileChecks.SetSize(SessionState.GetInt(Flag+".Size",6));Crystal.SafeArea.Sim=(Crystal.SafeArea.SimDevice)SessionState.GetInt(Flag+".SafeArea",0);
        SessionState.SetBool(Flag+".ImportPending",true);
    }
    static void ImportPendingClips()
    {
        if(!SessionState.GetBool(Flag+".ImportPending",false)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<nextImportTry)return;
        try
        {
            AssetDatabase.ReleaseCachedFileHandles();
            // Publish the four-clip Propositional sequence only after every segment succeeded.
            string propsManifest=CaptureFolder+"/PRELIM.json";
            var props=File.Exists(propsManifest)?JsonUtility.FromJson<StageHelpVideoCatalog.Recording>(File.ReadAllText(propsManifest)):null;
            bool completeProps=props!=null&&props.clips!=null&&props.clips.Length==4&&props.clips.All(k=>File.Exists(CaptureFolder+"/"+k+".mp4")&&File.Exists(CaptureFolder+"/"+k+".json"));
            string truthManifest=CaptureFolder+"/TruthTable.json";
            var truth=File.Exists(truthManifest)?JsonUtility.FromJson<StageHelpVideoCatalog.Recording>(File.ReadAllText(truthManifest)):null;
            bool completeTruth=truth!=null&&truth.clips!=null&&truth.clips.Length==5&&truth.clips.All(k=>File.Exists(CaptureFolder+"/"+k+".mp4")&&File.Exists(CaptureFolder+"/"+k+".json"));
            string inferenceManifest=CaptureFolder+"/RulesOfInference.json";
            var inference=File.Exists(inferenceManifest)?JsonUtility.FromJson<StageHelpVideoCatalog.Recording>(File.ReadAllText(inferenceManifest)):null;
            bool completeInference=inference!=null&&inference.clips!=null&&inference.clips.Length==5&&inference.clips.All(k=>File.Exists(CaptureFolder+"/"+k+".mp4")&&File.Exists(CaptureFolder+"/"+k+".json"));
            string formalManifest=CaptureFolder+"/FormalProofs.json";
            var formal=File.Exists(formalManifest)?JsonUtility.FromJson<StageHelpVideoCatalog.Recording>(File.ReadAllText(formalManifest)):null;
            bool completeFormal=formal!=null&&formal.clips!=null&&formal.clips.Length==5&&formal.clips.All(k=>File.Exists(CaptureFolder+"/"+k+".mp4")&&File.Exists(CaptureFolder+"/"+k+".json"));
            string directManifest=CaptureFolder+"/DirectProofs.json";
            var direct=File.Exists(directManifest)?JsonUtility.FromJson<StageHelpVideoCatalog.Recording>(File.ReadAllText(directManifest)):null;
            bool completeDirect=direct!=null&&direct.clips!=null&&direct.clips.Length==5&&direct.clips.All(k=>File.Exists(CaptureFolder+"/"+k+".mp4")&&File.Exists(CaptureFolder+"/"+k+".json"));
            string inductionManifest=CaptureFolder+"/Induction.json";
            var induction=File.Exists(inductionManifest)?JsonUtility.FromJson<StageHelpVideoCatalog.Recording>(File.ReadAllText(inductionManifest)):null;
            bool completeInduction=induction!=null&&induction.clips!=null&&induction.clips.Length==6&&induction.clips.All(k=>File.Exists(CaptureFolder+"/"+k+".mp4")&&File.Exists(CaptureFolder+"/"+k+".json"));
            string setsManifest=CaptureFolder+"/Sets.json";
            var sets=File.Exists(setsManifest)?JsonUtility.FromJson<StageHelpVideoCatalog.Recording>(File.ReadAllText(setsManifest)):null;
            bool completeSets=sets!=null&&sets.clips!=null&&sets.clips.Length==4&&sets.clips.All(k=>File.Exists(CaptureFolder+"/"+k+".mp4")&&File.Exists(CaptureFolder+"/"+k+".json"));
            string functionsManifest=CaptureFolder+"/FunctionsRelations.json";
            var functions=File.Exists(functionsManifest)?JsonUtility.FromJson<StageHelpVideoCatalog.Recording>(File.ReadAllText(functionsManifest)):null;
            bool completeFunctions=functions!=null&&functions.clips!=null&&functions.clips.Length==4&&functions.clips.All(k=>File.Exists(CaptureFolder+"/"+k+".mp4")&&File.Exists(CaptureFolder+"/"+k+".json"));
            string graphManifest=CaptureFolder+"/Graph.json";
            var graph=File.Exists(graphManifest)?JsonUtility.FromJson<StageHelpVideoCatalog.Recording>(File.ReadAllText(graphManifest)):null;
            bool completeGraph=graph!=null&&graph.clips!=null&&graph.clips.Length==4&&graph.clips.All(k=>File.Exists(CaptureFolder+"/"+k+".mp4")&&File.Exists(CaptureFolder+"/"+k+".json"));
            string treesManifest=CaptureFolder+"/Trees.json";
            var trees=File.Exists(treesManifest)?JsonUtility.FromJson<StageHelpVideoCatalog.Recording>(File.ReadAllText(treesManifest)):null;
            bool completeTrees=trees!=null&&trees.clips!=null&&trees.clips.Length==2&&trees.clips.All(k=>File.Exists(CaptureFolder+"/"+k+".mp4")&&File.Exists(CaptureFolder+"/"+k+".json"));
            foreach(string manifest in Directory.GetFiles(CaptureFolder,"*.json"))
            {
                string key=Path.GetFileNameWithoutExtension(manifest);
                string only=SessionState.GetString(Flag+".Only","");
                if(!string.IsNullOrEmpty(only))
                {
                    string requested=StageHelpVideoCatalog.ResourceKey(only);
                    if(key!=requested&&!key.StartsWith(requested+"_",StringComparison.Ordinal))continue;
                }
                if(key.StartsWith("PRELIM",StringComparison.Ordinal)&&!completeProps)continue;
                if(key.StartsWith("TruthTable",StringComparison.Ordinal)&&!completeTruth)continue;
                if(key.StartsWith("RulesOfInference",StringComparison.Ordinal)&&!completeInference)continue;
                if(key.StartsWith("FormalProofs",StringComparison.Ordinal)&&!completeFormal)continue;
                if(key.StartsWith("DirectProofs",StringComparison.Ordinal)&&!completeDirect)continue;
                if(key.StartsWith("Induction",StringComparison.Ordinal)&&!completeInduction)continue;
                if(key.StartsWith("Sets",StringComparison.Ordinal)&&!completeSets)continue;
                if(key.StartsWith("FunctionsRelations",StringComparison.Ordinal)&&!completeFunctions)continue;
                if(key.StartsWith("Graph",StringComparison.Ordinal)&&!completeGraph)continue;
                if(key.StartsWith("Trees",StringComparison.Ordinal)&&!completeTrees)continue;
                string clip=CaptureFolder+"/"+key+".mp4",destination=Folder+"/"+key+".mp4";
                if(!File.Exists(clip)||new FileInfo(clip).Length<1024)continue;
                if(!File.Exists(destination)||new FileInfo(destination).Length!=new FileInfo(clip).Length)
                {
                    var cached=AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(destination);
                    if(cached!=null)Resources.UnloadAsset(cached);
                    File.Copy(clip,destination,true);
                }
                File.Copy(manifest,Folder+"/"+key+".json",true);
            }
            SessionState.SetBool(Flag+".ImportPending",false);
            AssetDatabase.Refresh();
            string original=SessionState.GetString(Flag+".Scene","Assets/Scenes/TruthTable.unity");
            if(!SceneManager.GetActiveScene().isDirty)EditorSceneManager.OpenScene(original);
            File.AppendAllText(Report,"Imported completed clips and restored the Editor session.\n");
        }
        catch(IOException){nextImportTry=EditorApplication.timeSinceStartup+.5;}
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Bootstrap()
    {
        if(!SessionState.GetBool(Flag,false))return;
        var f=BindingFlags.Static|BindingFlags.NonPublic;
        typeof(TrialBackendClient).GetField("ValidationTransport",f).SetValue(null,(Func<string,string,System.Threading.Tasks.Task<string>>)((a,b)=>System.Threading.Tasks.Task.FromResult("{}")));
        typeof(TrialBackendClient).GetField("ValidationUid",f).SetValue(null,null);
        StageCompleteManager.SuppressPersistenceForValidation=true;LogicLegends.Graph.GraphCompletion.SuppressPersistenceForValidation=true;
        ProofsLibraryStageController.SuppressProgressionForValidation=true;DirectIndirectProofStageController.SuppressProgressionForValidation=true;
        StageSelectionState.SelectForEditorSpawn(1);SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;
    }
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(!SessionState.GetBool(Flag,false))return;
        foreach(var b in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if((b.GetType().Namespace??"").StartsWith("Photon.Pun.Demo.Cockpit"))b.enabled=false;
        if(Player.LocalInstance==null)
        {
            var avatar=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
            avatar.GetComponent<MultiplayerAvatarSync>().enabled=false;avatar.GetComponent<PlayerEquipmentLoader>().enabled=false;
            foreach(var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true))skin.gameObject.SetActive(skin.name=="UNIFORM_MALE");
            avatar.GetComponent<CharacterController>().enabled=true;
            var spawner=Find<LevelSpawner>();var spawn=spawner!=null?Field<Transform>(spawner,"spawnPoint"):null;
            if(spawn==null)spawn=GameObject.Find("Proofs Player Spawn")?.transform??GameObject.Find("Trial Player Spawn")?.transform;
            if(spawn!=null)avatar.transform.SetPositionAndRotation(spawn.position,spawn.rotation);
        }
        if(runner==null){runner=new GameObject("Offline Help Recording Session").AddComponent<StageHelpRecordingRunner>();UnityEngine.Object.DontDestroyOnLoad(runner);runner.StartCoroutine(Guard(Run()));}
    }
    static IEnumerator Guard(IEnumerator work)
    {
        var stack=new Stack<IEnumerator>();stack.Push(work);
        while(stack.Count>0)
        {
            object next=null;Exception failure=null;
            try{var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}next=current.Current;}catch(Exception error){failure=error;}
            if(failure!=null){foreach(var item in stack)if(item is IDisposable disposable)disposable.Dispose();if(recorder!=null&&recorder.IsRecording())recorder.StopRecording();File.AppendAllText(Report,"FAIL "+sceneName+": "+failure+"\n");Debug.LogException(failure);EditorApplication.isPlaying=false;yield break;}
            if(next is IEnumerator child)stack.Push(child);else yield return next;
        }
        File.AppendAllText(Report,"ALL REQUESTED RECORDINGS COMPLETE\n");SceneManager.sceneLoaded-=Loaded;EditorApplication.isPlaying=false;
    }
    static IEnumerator Run()
    {
        Application.runInBackground=true;BookMapMobileChecks.SetSize(6);
        string only=SessionState.GetString(Flag+".Only","");
        foreach(string scene in Scenes.Where(s=>string.IsNullOrEmpty(only)||s==only))
        {
            if(string.IsNullOrEmpty(only)&&File.Exists(Folder+"/"+StageHelpVideoCatalog.ResourceKey(scene)+".mp4")&&File.Exists(Folder+"/"+StageHelpVideoCatalog.ResourceKey(scene)+".json"))
            {File.AppendAllText(Report,"KEPT existing actual recording "+scene+"\n");continue;}
            sceneName=scene;Time.timeScale=1;GameInput.Instance?.SetGameplayInputBlocked(false);StageSelectionState.SelectForEditorSpawn(TrialCatalog.ForScene(scene).Number);
            if(SceneManager.GetActiveScene().name!=scene)yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/"+scene+".unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSecondsRealtime(3.5f);
            float by=Time.realtimeSinceStartup+35;while(Player.LocalInstance==null&&Time.realtimeSinceStartup<by)yield return null;
            while(TeleportManager.Instance!=null&&TeleportManager.Instance.IsTransitioning&&Time.realtimeSinceStartup<by)yield return null;
            yield return Prepare(scene);yield return new WaitForSecondsRealtime(.6f);
            if(scene=="Trees"&&SessionState.GetBool("Help.TreesRevisionVerify",false))
            {yield return TreesRevisionPlayChecks.Run();continue;}
            if(scene=="Graph"&&SessionState.GetBool("Help.GraphRevisionVerify",false))
            {yield return GraphRevisionPlayChecks.Run();continue;}
            if(scene=="PRELIM")
            {
                yield return PropositionalHelpClipCapture.Record();
                continue;
            }
            if(scene=="TruthTable")
            {
                if(SessionState.GetBool("Help.TruthNightOnly",false))yield return TruthTableNightChecks.Run();
                else if(SessionState.GetBool("Help.TruthHologramVerify",false))yield return TruthTableHologramChecks.Run();
                else yield return TruthTableHelpClipCapture.Record();
                continue;
            }
            if(scene=="RulesOfInference")
            {
                yield return InferenceHelpClipCapture.Record();
                continue;
            }
            if(scene=="Formal vs. Informal Proofs")
            {
                yield return FormalHelpClipCapture.Record();
                continue;
            }
            if(scene=="Direct&Indirect")
            {
                yield return DirectHelpClipCapture.Record();
                continue;
            }
            if(scene=="Induction")
            {
                if(SessionState.GetBool("Help.InductionPresentationVerify",false))yield return InductionPresentationChecks.Run();
                else if(SessionState.GetBool("Help.InductionVerify",false))yield return InductionHelpVerification.Run();
                else yield return InductionHelpClipCapture.Record();
                continue;
            }
            if(scene=="Setssss")
            {
                if(SessionState.GetBool("Help.SetsVerify",false))yield return SetsHelpVerification.Run();
                else yield return SetsHelpClipCapture.Record();
                continue;
            }
            if(scene=="Functions and Relations")
            {
                if(SessionState.GetBool("Help.FunctionsGardenVerify",false))yield return FunctionsGardenPlayChecks.Run();
                if(SessionState.GetBool("Help.FunctionsTutorialVerify",false))continue;
                if(SessionState.GetBool("Help.FunctionsVerify",false))yield return FunctionsRelationsPlayVerification.Run();
                else yield return FunctionsRelationsHelpClipCapture.Record();
                continue;
            }
            if(scene=="Graph"){yield return GraphHelpClipCapture.Record();continue;}
            if(scene=="Trees"){yield return TreesHelpClipCapture.Record();continue;}
            StartRecording(scene);Cue("Approach the Book Statue and use the hand button.");
            switch(scene)
            {
                case "Induction":yield return Induction();break;
                case "Setssss":yield return Sets();break;
                case "Functions and Relations":yield return Functions();break;
                case "Graph":yield return Graph();break;
                case "Trees":yield return Trees();break;
            }
            recorder.StopRecording();recorder=null;
            var record=new StageHelpVideoCatalog.Recording{scene=scene,source="Unity Game view; offline Editor test session; first question only; account writes disabled",cues=cues.ToArray()};
            string key=StageHelpVideoCatalog.ResourceKey(scene);File.WriteAllText(CaptureFolder+"/"+key+".json",JsonUtility.ToJson(record,true));
            File.AppendAllText(Report,"PASS "+scene+": "+frames+" frames; "+cues.Count+" action captions; "+new FileInfo(CaptureFolder+"/"+key+".mp4").Length+" bytes\n");
            Debug.Log("HELP_GAMEPLAY_RECORDING_COMPLETE "+scene);yield return null;
        }
    }
    static IEnumerator Prepare(string scene)
    {
        if (scene != "Trees")
        {
            float by = Time.realtimeSinceStartup + 18;
            while (UnityEngine.Object.FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).Length == 0 && Time.realtimeSinceStartup < by) yield return null;
            if (UnityEngine.Object.FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).Length == 0) throw new InvalidOperationException("Automatic first Help did not finish opening: " + scene);
        }
        if(scene=="Graph")
        {
            var s=Find<LogicLegends.Graph.GraphStageManager>();
            while(!s.Ready)yield return null;
            s.BeginFromHelp();yield return null;
            var tutorial=s.controlsTutorial;
            tutorial.GetType().GetMethod("BeginConfirmation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(tutorial,null);
            yield return null;tutorial.ConfirmYes();
            if(SessionState.GetBool("Help.GraphRevisionVerify",false))yield break;
            while(!s.Introductions.IsOpen)yield return null;
            s.Introductions.Finish();while(s.Introductions.IsOpen)yield return null;
            yield break;
        }
        if(scene=="Trees"){var s=Find<LogicLegends.Trees.TreeStageManager>();while(!s.Ready)yield return null;
            if(SessionState.GetBool("Help.TreesRevisionVerify",false))yield break;
            if(s.LessonOpen)s.SetLesson(false);
            while(!s.Introductions.IsOpen)yield return null;s.Introductions.Finish();while(s.Introductions.IsOpen)yield return null;}

        else if(scene=="Functions and Relations"&&SessionState.GetBool("Help.FunctionsTutorialVerify",false))
        {
            yield return FunctionsRelationsOnboardingPlayChecks.Run(Find<FunctionsRelationsStageManager>());
            File.AppendAllText(Report,"PASS complete Functions onboarding: real movement, jump, hand, board, Domain/Range choices, practice Remove, repeat/skip and camera/input cleanup.\n");
        }
        else
        {
            // Reuse the already-tested cleanup path: Help closes, practice confirms without starting a question.
            var type=typeof(MobileTutorialInteractionChecks);object coach=type.GetMethod("Prepare",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{scene});
            yield return null;yield return (IEnumerator)type.GetMethod("Cleanup",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{coach,scene});
        }
        Time.timeScale=1;GameInput.Instance.SetGameplayInputBlocked(false);Player.LocalInstance.ToggleControl(true);yield return null;
        if (UnityEngine.Object.FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).Length != 0) throw new InvalidOperationException("Help remains open before recording " + scene);
    }
    internal static void StartRecording(string scene)
    {
        string manifest=Path.GetFullPath(CaptureFolder+"/"+StageHelpVideoCatalog.ResourceKey(scene)+".json");
        if(File.Exists(manifest))File.Delete(manifest); // Incomplete captures can never replace a valid shipped clip.
        settings=ScriptableObject.CreateInstance<RecorderControllerSettings>();settings.SetRecordModeToManual();settings.FrameRate=24;settings.CapFrameRate=true;settings.ExitPlayMode=false;
        var movie=ScriptableObject.CreateInstance<MovieRecorderSettings>();movie.name="Actual "+scene+" gameplay";movie.Enabled=true;movie.CaptureAudio=false;
        movie.ImageInputSettings=new GameViewInputSettings{OutputWidth=1280,OutputHeight=720};movie.OutputFile=Path.GetFullPath(CaptureFolder+"/"+StageHelpVideoCatalog.ResourceKey(scene));
        settings.AddRecorderSettings(movie);recorder=new RecorderController(settings);frames=0;cues=new List<StageHelpVideoCatalog.Cue>();
        recorder.PrepareRecording();if(!recorder.StartRecording())throw new InvalidOperationException("Unity Recorder could not start "+scene);
    }
    internal static int RecordedFrames => frames;
    internal static void StopSegment(string key,string caption)
    {
        recorder.StopRecording();recorder=null;
        if(key!="PRELIM"&&key!="TruthTable")File.WriteAllText(CaptureFolder+"/"+key+".json",JsonUtility.ToJson(new StageHelpVideoCatalog.Recording{scene=key.StartsWith("TruthTable")?"TruthTable":"PRELIM",source="Actual Unity gameplay, offline test session",cues=new[]{new StageHelpVideoCatalog.Cue{seconds=0,caption=caption}}},true));
        File.AppendAllText(Report,"PASS "+key+": "+frames+" frames; "+new FileInfo(CaptureFolder+"/"+key+".mp4").Length+" bytes\n");
    }
    public static void CountFrame(){if(recorder!=null&&recorder.IsRecording())frames++;}
    static void Cue(string text){cues.Add(new StageHelpVideoCatalog.Cue{seconds=frames/24f,caption=text});}
    static IEnumerator Hold(float seconds=1.5f)
    {
        for(int i=0;i<Mathf.CeilToInt(seconds*24);i++)yield return new WaitForEndOfFrame();
        // Save the actual recorded render for human-readable camera/content QA.
        if(cues!=null&&cues.Count>0)ScreenCapture.CaptureScreenshot("Library/HelpRecording_"+StageHelpVideoCatalog.ResourceKey(sceneName)+"_"+cues.Count+".png");
    }
    static IEnumerator Frame(Transform target,float distance=10,float height=4)
    {
        if(target==null)throw new InvalidOperationException("Missing gameplay camera target");
        if(sceneName=="Formal vs. Informal Proofs"){distance=Mathf.Min(distance,7);height=Mathf.Min(height,3);}
        var camera=Camera.main;var brain=camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();if(brain!=null)brain.enabled=false;
        var center=target.position+Vector3.up*(sceneName=="Formal vs. Informal Proofs"?1.5f:height*.25f);var from=camera.transform.position;var rot=camera.transform.rotation;var to=center+new Vector3(sceneName=="Formal vs. Informal Proofs"?3f:0,height,-distance);var end=Quaternion.LookRotation(center-to);
        for(int i=0;i<18;i++){float t=Mathf.SmoothStep(0,1,(i+1)/18f);camera.transform.SetPositionAndRotation(Vector3.Lerp(from,to,t),Quaternion.Slerp(rot,end,t));yield return new WaitForEndOfFrame();}
    }
    static void UseGameplayCamera()
    {
        var brain=Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        if(brain!=null){brain.DefaultBlend=new Unity.Cinemachine.CinemachineBlendDefinition(Unity.Cinemachine.CinemachineBlendDefinition.Styles.EaseInOut,.6f);brain.enabled=true;}
    }
    static void Move(Vector3 p){PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,p,Quaternion.identity);}
    static void At(Transform t,float offset=2){Move(t.position+Vector3.back*offset);}
    static IEnumerator Induction()
    {
        var s=Find<LogicLegends.Induction.InductionStageManager>();yield return Frame(s.book,10,4);At(s.book);yield return Hold(.8f);s.InteractBook();yield return Hold(1.2f);
        Cue("View Board shows the statement and current induction task.");s.OpenReading("board");yield return Hold(2);s.CloseReading();yield return Hold(.6f);
        Cue("Choose a formula scroll, then press Get This Scroll.");At(s.scrollStand);s.OpenScrollCollection();var tablet=s.Tablets.First(t=>t.Data.id==s.Challenge.answers["Base:1"]);s.ui.SelectScroll(tablet);yield return Hold(2);s.CollectScroll(tablet);s.CloseReading();
        var slot=s.slots.Single(t=>t.key=="Base:1");Cue("Follow the altar arrow and place the selected scroll.");At(slot.transform);yield return Frame(slot.transform,10,4);s.Place(slot);yield return Hold(1.5f);
        Cue("Submit checks the entire current arrangement.");s.Submit();yield return Hold(2);
    }
    static IEnumerator Sets()
    {
        var s=Find<SetssssChallengeOne>();var p=Player.LocalInstance;var book=Field<Transform>(s,"bookStatue");var hand=Button("InteractButton");yield return Frame(book,10,4);At(book);yield return Hold(.8f);hand.onClick.Invoke();yield return Hold(1.3f);
        Cue("Read the required set elements on the Information Board.");var text=Field<TextMeshPro>(s,"boardText");yield return Frame(text.transform,14,2);yield return Hold(2);
        var blocks=Field<GrabbableObject[]>(s,"numberedBlocks");int required=int.Parse(s.ActiveCondition.setA.Concat(s.ActiveCondition.setB).First());var block=blocks.Single(b=>b.GetComponent<SetssssNumberedBlock>().Value==required);
        Cue("Collect the requested element block from its spawner.");yield return Frame(block.transform,8,3);yield return (IEnumerator)typeof(SetssssPlacementPlayChecks).GetMethod("Pickup",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{s,p,block,Field<Button>(s,"pickUpBlockButton")});yield return Hold(1);
        var region=Field<Bounds>(s,s.ActiveCondition.setA.Length>0?"aCircleBounds":"bCircleBounds");Cue("Drop the element inside the requested region and let it settle.");yield return (IEnumerator)typeof(SetssssPlacementPlayChecks).GetMethod("Drop",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{s,p,block,region.center,hand});yield return Frame(block.transform,10,8);yield return Hold(1.8f);
        Cue("Submit checks each element's actual position and shows feedback.");yield return (IEnumerator)typeof(SetssssPlacementPlayChecks).GetMethod("Submit",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{s,p,Field<Button>(s,"submitButton"),region.center});yield return Hold(1.5f);
    }
    static IEnumerator Functions()
    {
        var s=Find<FunctionsRelationsStageManager>();var ui=Find<FunctionsRelationsPresentation>();var book=s.BookTarget;yield return Frame(book,10,4);At(book);yield return Hold(.8f);s.ActivateChallenge();yield return Hold(1.2f);
        Cue("View Board explains the required input-output relation.");s.OpenBoard();yield return Hold(2);s.CloseBoard();yield return Hold(.7f);
        var domain=GameObject.Find("Domain").transform;Cue("Open Domain and select an input value.");At(domain);yield return Frame(domain,10,3);s.InteractNearby();yield return Hold(1.5f);s.ChooseDomain(0);yield return Hold(.4f);
        var range=GameObject.Find("Range").transform;Cue("Select an available output to create a connection.");At(range);yield return Frame(range,10,3);s.InteractNearby();yield return Hold(1.4f);s.ChooseRange(0);yield return Hold(.6f);s.CloseDiamondChoices();
        var mapping=Find<LaserMappingController>();foreach(var pair in s.CurrentChallenge.requiredPairs)mapping.AddConnection(pair,true);
        Cue("Submit checks every connection; Undo lets you correct a link.");yield return Frame(domain,17,7);s.Submit();yield return Hold(2);
    }
    static IEnumerator Graph()
    {
        var s=Find<LogicLegends.Graph.GraphStageManager>();yield return Frame(s.bookStatue,14,6);At(s.bookStatue);yield return Hold(.7f);s.InteractBook();yield return Hold(1.5f);
        Cue("Read the graph question: circles are vertices and bridges are edges.");yield return Frame(s.arena,26,24);yield return Hold(1.5f);
        Cue("Cross the connecting bridge toward the requested vertex.");
        while(s.Moving)yield return null;
        var target=new GameObject("Recording Walk Target").transform;
        target.position=s.arena.TransformPoint(s.Challenge.Vertex(s.Challenge.destination).position)+Vector3.up;
        s.LocalPlayer.BeginGuidedMovement(target,6,.25f);
        float deadline=Time.realtimeSinceStartup+20;
        while(Vector2.Distance(new Vector2(s.LocalPlayer.transform.position.x,s.LocalPlayer.transform.position.z),new Vector2(target.position.x,target.position.z))>.4f&&Time.realtimeSinceStartup<deadline)yield return null;
        s.LocalPlayer.EndGuidedMovement();UnityEngine.Object.Destroy(target.gameObject);yield return Hold(.7f);
        if(!s.Tracker.IsContinuous||s.Tracker.Current!=s.Challenge.destination)throw new InvalidOperationException("Recorded graph walk did not reach its connected destination");
        Cue("Confirm checks the whole route and shows feedback.");s.Confirm();yield return Hold(2.2f);
    }
    static IEnumerator Trees()
    {
        var s=Find<LogicLegends.Trees.TreeStageManager>();yield return Frame(s.bookStatue,10,4);At(s.bookStatue);yield return Hold(.8f);s.InteractBook();yield return Hold(2);
        Cue("Read the tree question and inspect its nodes and branches.");yield return Hold(1.5f);
        string reason;var q=s.Challenge;string answer=q.nodes.First(n=>LogicLegends.Trees.TreeAnswerValidator.Validate(q,q.initial,n.id,null,out reason)).id;
        Cue("Tap the node requested by the question.");s.SelectNode(answer);yield return Hold(1.5f);
        Cue("Confirm checks the answer and pauses after a correct solution.");s.Confirm();yield return Hold(2);
    }
    static T Find<T>()where T:UnityEngine.Object=>UnityEngine.Object.FindFirstObjectByType<T>() ?? UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    static Button Button(string name)=>UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.name==name);
    static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Flags).GetValue(owner);
    static void FieldSet(object owner,string name,object value)=>owner.GetType().GetField(name,Flags).SetValue(owner,value);
    static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Flags).Invoke(owner,args);
    static void CallOptional(object owner,string name){var m=owner.GetType().GetMethod(name,Flags);if(m!=null)m.Invoke(owner,null);}
}
public sealed class StageHelpRecordingRunner:MonoBehaviour{void Update(){StageHelpGameplayRecorder.CountFrame();}}
#endif
