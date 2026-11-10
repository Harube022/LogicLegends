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
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit offline checks. Never use a real identity, wallet, or production transport.
public static class GuidanceAndProofAnswersChecks
{
    const string Flag = "GuidanceProofs.QA", Report = "Library/GuidanceProofsChecks.txt";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Hidden).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Hidden).SetValue(o, value);
    static void Check(bool value, string description)
    { if (!value) throw new Exception(description); File.AppendAllText(Report, "PASS " + description + "\n"); }

    [MenuItem("Logic Legends/Guidance/Check all Challenge 5 solutions")]
    public static void CheckCatalog()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Formal vs. Informal Proofs.unity");
        var random = UnityEngine.Random.state;
        try
        {
            var board = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ProofWorldBoardController>(true)).First();
            var bank = Resources.Load<ProofChallengeCatalog>("ProofChallenges");
            var formatter = typeof(ProofsLibraryStageController).GetMethod("AdminCurrentAnswer", BindingFlags.Static | BindingFlags.NonPublic);
            int count = 0;
            foreach (var q in bank.variantSets[4].variants)
            {
                board.Configure(q);
                var order = q.acceptableOrders.First().stepIds;
                Set(board, "assignedSteps", order.ToArray()); Set(board, "assignedRules", order.ToArray());
                if (!board.IsCorrect()) throw new Exception("Displayed canonical solution fails gameplay validation.");
                string answer = (string)formatter.Invoke(null, new object[] { q });
                if (answer.Split('\n').Count(l => l.StartsWith("Statement slot ") || l.StartsWith("Rule slot ")) != 10) throw new Exception("Expected ten labeled answer slots.");
                for (int i = 0; i < order.Length; i++)
                    if (!answer.Contains("Statement slot " + (i + 1) + ": " + q.steps[order[i]].statement) ||
                        !answer.Contains("Rule slot " + (i + 1) + ": " + q.steps[order[i]].justification)) throw new Exception("Statement/rule slot mismatch.");
                string labeled=(string)typeof(ProofsLibraryStageController).GetMethod("AdminFormalizeAnswer", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,new object[]{q,board});
                var stepMap=Get<int[]>(board,"optionToStep");var ruleMap=Get<int[]>(board,"optionToRule");
                for(int i=0;i<order.Length;i++)
                    if(!labeled.Contains("Statement slot "+(i+1)+": STATEMENT "+(char)('A'+Array.IndexOf(stepMap,order[i])))||
                        !labeled.Contains("Rule slot "+(i+1)+": RULE "+(char)('A'+Array.IndexOf(ruleMap,order[i]))))throw new Exception("Shuffled scroll label mismatch.");
                count++;
            }
            File.WriteAllText("Library/GuidanceProofsCatalogChecks.txt", "PASS all " + count + " randomized Challenge 5 variants: ten labeled slots and actual IsCorrect validation.\n");
        }
        finally { UnityEngine.Random.state = random; EditorSceneManager.ClosePreviewScene(scene); }
    }
    [MenuItem("Logic Legends/Guidance/Verify proof arrows and Admin scrolling in Play Mode")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Finish Play Mode first.");
        SessionState.SetString(Flag + ".Start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetInt(Flag + ".Size", BookMapMobileChecks.GetSize());
        SessionState.SetInt(Flag + ".SafeArea", (int)Crystal.SafeArea.Sim);
        SessionState.SetBool(Flag + ".Background", Application.runInBackground);
        for (int c = 0; c < 5; c++)
        {
            string key = "LogicLegends.ProofsLibrary.LastVariant." + c;
            SessionState.SetBool(Flag + key, PlayerPrefs.HasKey(key)); SessionState.SetInt(Flag + key + ".Value", PlayerPrefs.GetInt(key));
        }
        File.WriteAllText(Report, "Offline fresh proof-stage run, C1 → C2, read/collect/placement, shared glow and Admin answer scrolling. Editor touch simulation, not Android hardware.\n");
        SessionState.SetBool(Flag, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Formal vs. Informal Proofs.unity");
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    static void Hook() { EditorApplication.playModeStateChanged -= Restore; EditorApplication.playModeStateChanged += Restore; }
    static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false);
        for (int c = 0; c < 5; c++)
        {
            string key = "LogicLegends.ProofsLibrary.LastVariant." + c;
            if (SessionState.GetBool(Flag + key, false)) PlayerPrefs.SetInt(key, SessionState.GetInt(Flag + key + ".Value", 0)); else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save(); BookMapMobileChecks.SetSize(SessionState.GetInt(Flag + ".Size", 2));
        Crystal.SafeArea.Sim = (Crystal.SafeArea.SimDevice)SessionState.GetInt(Flag + ".SafeArea", 0);
        Application.runInBackground = SessionState.GetBool(Flag + ".Background", false);
        string start = SessionState.GetString(Flag + ".Start", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(start) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(start);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate()
    {
        if (!SessionState.GetBool(Flag, false)) return;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        string uid = "guidance-qa-not-a-real-account";
        typeof(TrialBackendClient).GetField("ValidationUid", flags).SetValue(null, uid);
        typeof(TrialBackendClient).GetField("ValidationEmail", flags).SetValue(null, "sngbdsa137@gmail.com");
        typeof(TrialBackendClient).GetField("ValidationTransport", flags).SetValue(null,
            (Func<string, string, System.Threading.Tasks.Task<string>>)((path, body) => System.Threading.Tasks.Task.FromResult(path == "/trials/access" ? "{\"admin\":true,\"uid\":\"" + uid + "\"}" : "{}")));
        SceneManager.sceneLoaded += Loaded;
    }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Formal vs. Informal Proofs") return;
        SceneManager.sceneLoaded -= Loaded; Application.runInBackground = true;
        var staging = new GameObject("Inactive QA avatar"); staging.SetActive(false);
        var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"), staging.transform);
        avatar.GetComponent<MultiplayerAvatarSync>().enabled = false; avatar.GetComponent<PlayerEquipmentLoader>().enabled = false;
        foreach (var skin in avatar.GetComponentsInChildren<PlayerAnimator>(true)) skin.gameObject.SetActive(skin.name == "UNIFORM_MALE");
        avatar.transform.SetParent(null, false); UnityEngine.Object.Destroy(staging);
        var spawn = GameObject.Find("Proofs Player Spawn"); if (spawn != null) avatar.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        ProofsLibraryStageController.SuppressProgressionForValidation = true;
        new GameObject("Guidance QA runner").AddComponent<GuidanceProofsQaRunner>().StartCoroutine(Guard(Run()));
    }
    static IEnumerator Guard(IEnumerator work)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(work);
        while (stack.Count > 0)
        {
            object next = null; bool more;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception error) { File.AppendAllText(Report, "FAIL " + error + "\n"); Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
            if (!more) { stack.Pop(); continue; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        File.AppendAllText(Report, "COMPLETE\n"); EditorApplication.isPlaying = false;
    }
    static IEnumerator Wait(Func<bool> predicate, string message, float seconds = 8)
    { double until = Time.realtimeSinceStartupAsDouble + seconds; while (!predicate() && Time.realtimeSinceStartupAsDouble < until) yield return null; Check(predicate(), message); }
    static IEnumerator Run()
    {
        BookMapMobileChecks.SetSize(2);
        yield return Wait(() => Player.LocalInstance != null && UnityEngine.Object.FindFirstObjectByType<ProofsStageUI>()?.Help?.IsOpen == true, "fresh scene and offline avatar ready", 25);
        var stage = UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();
        stage.Presentation.Help.CloseHelp(); stage.FinishOnboarding();
        yield return Intro(stage);
        // Complete both real C1 questions so C2 receives the same reset as a fresh playthrough.
        for (int question = 0; question < 2; question++)
        {
            stage.Activate(); var q = Get<ProofChallengeData>(stage, "activeChallenge");
            for (int i = 0; i < q.scrolls.Length; i++)
            { yield return Collect(stage, i); stage.PlaceOnPedestal(q.scrolls[i].isFormal ? 0 : 1); }
            stage.Submit(); yield return Wait(() => stage.WaitingForBook, "C1 question completed and waiting at book");
        }
        Check(stage.ChallengeNumber == 2, "fresh run advances to Challenge 2"); yield return Intro(stage);
        stage.Activate(); var challenge = Get<ProofChallengeData>(stage, "activeChallenge");
        var order = new List<int> { Array.FindIndex(challenge.scrolls, s => s.isFormal), Array.FindIndex(challenge.scrolls, s => !s.isFormal) };
        order.AddRange(Enumerable.Range(0, challenge.scrolls.Length).Where(i => !order.Contains(i)));
        var indicators = stage.GetComponent<ProofsPedestalIndicators>();
        Check(indicators.Arrows.Length == 2 && stage.AltarTargets[0] != stage.AltarTargets[1], "two distinct altar references and arrow instances");
        foreach (int i in order)
        {
            yield return Collect(stage, i); yield return null;
            Check(indicators.Arrows.All(a => a.gameObject.activeInHierarchy), "C2 both arrows visible carrying scroll " + i + ", including occupied altars");
            float y = indicators.Arrows[0].position.y; yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Abs(y - indicators.Arrows[0].position.y) > .001f, "subtle floating movement preserved");
            Check(indicators.Arrows.All(a => a.GetComponentInChildren<GuidanceArrowGlow>() != null), "both altar arrows use shared glow");
            stage.PlaceOnPedestal(challenge.scrolls[i].isFormal ? 0 : 1); yield return null;
            Check(indicators.Arrows.All(a => !a.gameObject.activeSelf), "placing clears carrying guidance");
        }
        stage.Submit(); yield return null; Check(indicators.Arrows.All(a => !a.gameObject.activeSelf), "question completion hides both arrows");
        yield return Wait(() => stage.WaitingForBook, "C2 progression cleanup complete");
        yield return GlowCheck();
        // Select this run's actual C5 variant inside the sandbox; no live progress/save is touched.
        var catalog = Get<ProofChallengeCatalog>(stage, "catalog"); int selected = Get<int[][]>(stage, "selectedVariants")[4][0];
        var question5 = catalog.variantSets[4].variants[selected]; Set(stage, "challengeIndex", 4); Set(stage, "questionIndex", 0); Set(stage, "activeChallenge", question5);
        Set(stage, "started", true);
        stage.WorldBoard.Configure(question5);
        AdminAuthorization.Refresh(); AdminAuthorization.Tick(); yield return Wait(() => AdminAuthorization.IsAuthorized, "offline backend-authorized Admin fixture");
        var pages = new List<AdminAnswerPage>(); stage.AppendAdminAnswers(pages);
        Check(System.Text.RegularExpressions.Regex.Matches(pages[0].Body, @"(?:Statement|Rule) slot \d+:").Count == 10, "current randomized Admin answer contains ten slots");
        var admin = UnityEngine.Object.FindFirstObjectByType<AdminDebugController>(FindObjectsInactive.Include);
        admin.GetType().GetMethod("OpenPanel", Hidden).Invoke(admin, null); yield return null;
        var showAnswers = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == "ViewQuestionsAndAnswers");
        yield return ProofsHandVerification.Touch(showAnswers);
        var viewer = Get<AdminAnswerViewer>(admin, "answerViewer"); yield return null;
        var scroll = Get<ScrollRect>(viewer, "scroll"); var body = Get<TextMeshProUGUI>(viewer, "body");
        body.fontSize = 38; yield return null; yield return null;
        Check(scroll.content.rect.height >= body.GetPreferredValues(body.text, scroll.viewport.rect.width - 44, float.PositiveInfinity).y + 36, "all answers fit scroll content at mobile landscape size");
        scroll.verticalNormalizedPosition = 0; yield return null; body.ForceMeshUpdate();
        var last = body.textInfo.characterInfo.Last(c => c.isVisible); Vector3 local = scroll.viewport.InverseTransformPoint(body.transform.TransformPoint(last.bottomLeft));
        Check(local.y >= scroll.viewport.rect.yMin - 2 && local.y <= scroll.viewport.rect.yMax + 2, "last rule answer readable at bottom of viewport");
        yield return ProofsHandVerification.Touch(viewer.GetComponentsInChildren<Button>().First(b => b.name == "BackToAdmin"));
        Check(!Get<bool>(viewer, "visible") && Time.timeScale == 0 && GameInput.Instance.GameplayInputBlocked, "Admin answer Back accepts touch and retains Admin pause");
        admin.ClosePanel();
    }
    static IEnumerator Intro(ProofsLibraryStageController stage)
    {
        yield return Wait(() => stage.Introductions.IsOpen, "challenge introduction available"); stage.Introductions.Skip();
        yield return Wait(() => !stage.InputSuppressed && Time.timeScale > 0, "introduction restores gameplay");
    }
    static IEnumerator Collect(ProofsLibraryStageController stage, int index)
    {
        stage.ReadScroll(index); yield return null;
        Check(stage.ReaderPanel.activeSelf, "scroll reader opens before collection");
        yield return ProofsHandVerification.Touch(stage.CollectButton); yield return null;
        Check(stage.CarriedScroll == index && !stage.HasReaderOpen, "Get This Scroll touch collects exact scroll");
    }
    static IEnumerator GlowCheck()
    {
        var root = new GameObject("Isolated yellow glow check", typeof(MeshFilter), typeof(MeshRenderer)); root.layer = 31;
        var arrows = UnityEngine.Object.FindFirstObjectByType<ProofsPedestalIndicators>();
        root.GetComponent<MeshFilter>().sharedMesh = arrows.Arrows[0].Find("Yellow Arrow").GetComponent<MeshFilter>().sharedMesh;
        root.transform.position = Vector3.one * 2000; root.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("Guidance/YellowArrow");
        GuidanceArrowGlow.Attach(root); yield return null; yield return null;
        for (int i = 0; i < 20; i++) GuidanceArrowGlow.AttachHierarchy(root.transform);
        yield return null;
        Check(root.GetComponentsInChildren<GuidanceArrowGlow>().Length == 1 && root.GetComponentsInChildren<Renderer>().Length == 3, "repeated styling creates exactly two halos, no recursive duplicates");
        var uiHost = new GameObject("Temporary glow UI", typeof(RectTransform), typeof(Canvas));
        var pointer = StageUITheme.Text("Arrow", uiHost.transform, "▼", 48, true);
        var imageArrow = StageUITheme.Rect("Off-screen arrow", uiHost.transform).gameObject.AddComponent<Image>();
        GuidanceArrowGlow.Attach(imageArrow.gameObject);
        var sprite = new GameObject("Temporary sprite arrow", typeof(SpriteRenderer));
        sprite.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PREFABS/WORLD_UI/WorldPointer.prefab").GetComponent<SpriteRenderer>().sprite;
        GuidanceArrowGlow.AttachHierarchy(sprite.transform);
        yield return null; yield return null;
        Check(pointer.fontSharedMaterial.IsKeywordEnabled("UNDERLAY_ON") && pointer.extraPadding && !pointer.raycastTarget, "tutorial glyph uses a padded non-blocking SDF halo");
        Check(imageArrow.GetComponentsInChildren<Image>().Length == 3 && imageArrow.GetComponentsInChildren<Image>().All(i => !i.raycastTarget), "UI arrow halos do not block taps");
        for (int i = 0; i < 20; i++) GuidanceArrowGlow.AttachHierarchy(sprite.transform);
        yield return null;
        Check(sprite.GetComponentsInChildren<SpriteRenderer>().Length == 3 && sprite.GetComponentsInChildren<GuidanceArrowGlow>().Length == 1, "authored sprite arrow uses one shared glow setup without duplicates");
        UnityEngine.Object.Destroy(uiHost); UnityEngine.Object.Destroy(sprite);
        var camera = new GameObject("No-bloom glow camera", typeof(Camera)).GetComponent<Camera>(); camera.enabled = false;
        camera.transform.position = root.transform.position - Vector3.forward * 5; camera.orthographic = true; camera.orthographicSize = 1;
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var target = new RenderTexture(128, 128, 24); camera.targetTexture = target; var old = RenderTexture.active;
        var image = new Texture2D(128, 128, TextureFormat.RGB24, false);
        try
        {
            camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
            int bright = image.GetPixels32().Count(c => c.r > 180 && c.g > 140 && c.b < 100);
            int halo = image.GetPixels32().Count(c => c.r > 8 && c.r < 150 && c.g > 5);
            Check(bright > 500 && halo > 100, "yellow fill and visible halo render against black with bloom disabled");
            File.WriteAllBytes("Library/GuidanceArrow_NoBloom.png", image.EncodeToPNG());
        }
        finally { RenderTexture.active = old; camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(camera.gameObject); UnityEngine.Object.Destroy(root); }
    }
}
public sealed class GuidanceProofsQaRunner : MonoBehaviour { }
#endif
