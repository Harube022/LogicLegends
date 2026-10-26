using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opt-in real hand/button, geometry, mobile layout, and validation checks.</summary>
public static class FunctionsRelationsInteractionPlayChecks
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool("FR.RunChecks", false)) return;
        for (int i = 0; i < 5; i++)
            SessionState.SetInt("FR.SavedVariant." + i, PlayerPrefs.GetInt("FunctionsRelations.LastVariant." + i, -1));
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }

    [InitializeOnLoadMethod]
    static void RestoreHook()
    {
        EditorApplication.playModeStateChanged -= Restore; EditorApplication.playModeStateChanged += Restore;
    }

    static void Restore(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("FR.RunChecks", false)) return;
        for (int i = 0; i < 5; i++)
        {
            string key = "FunctionsRelations.LastVariant." + i;
            int value = SessionState.GetInt("FR.SavedVariant." + i, -1);
            if (value < 0) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetInt(key, value);
        }
        PlayerPrefs.Save(); SessionState.SetBool("FR.RunChecks", false); SessionState.SetBool("FR.VisualOnly", false);
        Crystal.SafeArea.Sim = Crystal.SafeArea.SimDevice.None;
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Functions and Relations") return;
        SceneManager.sceneLoaded -= Loaded;
        // Supply the existing local avatar before LevelSpawner.Start; avoid account/database effects in QA.
        var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
        // The offline QA avatar has no Photon owner; the actual stage's Player
        // prefab is local. Do not run the multiplayer-only remote bootstrap.
        var sync = avatar.GetComponent<MultiplayerAvatarSync>();
        if (sync != null) sync.enabled = false;
        avatar.name = "FR QA Local Avatar";
        var spawn = GameObject.Find("FR_SpawnPoint").transform;
        PrelimTeleportTransition.MovePlayerImmediately(avatar, spawn.position, spawn.rotation);
        avatar.GetComponent<CharacterController>().enabled = true;
        var stage = UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>();
        stage.StartCoroutine(Run(stage));
    }

    static IEnumerator Run(FunctionsRelationsStageManager stage)
    {
        yield return null; yield return null;
        var view = Type.GetType("UnityEditor.GameView,UnityEditor");
        var window = EditorWindow.GetWindow(view);
        view.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(window, SessionState.GetInt("FR.CheckSize", 2), null); window.Focus();
        Crystal.SafeArea.Sim = SessionState.GetBool("FR.CheckSafeArea", false) ?
            (SessionState.GetInt("FR.CheckSize", 2) == 12 ? Crystal.SafeArea.SimDevice.Pixel3XL_LSR : Crystal.SafeArea.SimDevice.iPhoneX) : Crystal.SafeArea.SimDevice.None;
        yield return new WaitForSecondsRealtime(.3f);
        while (stage.Help == null || stage.Help.HelpButton == null) yield return null;
        if (SessionState.GetBool("FR.OnboardingChecks", false))
            yield return FunctionsRelationsOnboardingPlayChecks.Run(stage);
        else { stage.Help.CloseHelp(); stage.FinishOnboarding(); }
        var player = Player.LocalInstance;
        Check(player != null, "single local avatar");
        var ui = UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsPresentation>();
        var mapping = UnityEngine.Object.FindFirstObjectByType<LaserMappingController>();
        var book = GameObject.Find("Book_Statue/FR_BookInteractionZone").GetComponent<Collider>();
        var cameraView = stage.GetComponent<FunctionsRelationsBoardView>();
        float initial = stage.TimeRemaining;
        yield return new WaitForSecondsRealtime(.2f);
        Check(stage.State == FunctionsRelationsState.WAITING_FOR_ACTIVATION && stage.TimeRemaining == initial, "spawn waits with frozen timer");
        Check(ui.progress.text == "CHALLENGE 1 OF 5", "initial challenge number");
        Check(GameObject.Find("Board") == null, "no duplicate active board");
        Check(!GameObject.Find("InformationBoard").GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("induction")), "old induction text replaced");
        yield return Capture("Spawn");
        if (SessionState.GetBool("FR.VisualOnly", false))
        {
            yield return new WaitForSecondsRealtime(2f); yield return Capture("SpawnStable");
            Debug.Log("FUNCTIONS_RELATIONS_SPAWN_VISUAL_CHECK_READY"); yield break;
        }
        Check(!ui.submit.gameObject.activeSelf, "Submit hidden without an answer");
        bool initialPlayerEnabled = player.enabled;
        yield return ReadBoard(stage, ui, cameraView, false);
        // Back can safely cancel a camera transition without changing the challenge or timer.
        Click(ui.viewBoard.GetComponent<Button>()); yield return new WaitForSecondsRealtime(.1f);
        Click(ui.boardBack.GetComponent<Button>()); yield return Settled(cameraView);
        yield return null; yield return new WaitForEndOfFrame();
        Check(!stage.IsBoardOpen && player.enabled == initialPlayerEnabled && stage.TimeRemaining == initial, "Back cancels incoming transition safely");
        Click(ui.viewBoard.GetComponent<Button>()); yield return null;
        cameraView.enabled = false; yield return null;
        Check(!stage.IsBoardOpen && player.enabled == initialPlayerEnabled && !GameInput.Instance.GameplayInputBlocked, "interruption restores camera and controls");
        cameraView.enabled = true;
        Move(player, book.bounds.center + Vector3.back * 2f);
        GameInput.Instance.MobileInteract(); yield return null;
        Check(stage.State == FunctionsRelationsState.ACTIVE, "hand activates existing book start");
        yield return new WaitForSecondsRealtime(.2f);
        Check(stage.TimeRemaining < initial, "real shared timer counts after book");

        var hand = GameObject.Find("Canvas 1").GetComponentsInChildren<Button>(true).Single(b => b.name == "InteractButton");
        var context = Field<Button>(stage, "nearbyInteractionButton");
        for (int side = 0; side < 2; side++)
        {
            var target = GameObject.Find(side == 0 ? "Domain" : "Range").transform;
            var panel = side == 0 ? ui.domainPanel : ui.rangePanel;
            Bounds footprint = target.GetComponentsInChildren<Collider>().First().bounds;
            foreach (var col in target.GetComponentsInChildren<Collider>()) footprint.Encapsulate(col.bounds);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var edge = new Vector3(footprint.center.x + direction.x * footprint.extents.x, player.transform.position.y,
                    footprint.center.z + direction.z * footprint.extents.z);
                Move(player, edge + direction * 3.5f);
                yield return null; yield return null;
                // Deliberately face away. Proximity must not depend on camera aim or last movement direction.
                var brain = Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>(); brain.enabled = false;
                Camera.main.transform.rotation = Quaternion.LookRotation(-direction);
                if (i % 2 == 0) hand.onClick.Invoke(); else Click(context);
                yield return null;
                Check(panel.gameObject.activeInHierarchy, target.name + " opens at angle " + i + " with " + (i % 2 == 0 ? "hand" : "context"));
                Check(!((side == 0 ? ui.rangePanel : ui.domainPanel).gameObject.activeInHierarchy), "one choice panel at a time");
                Check(mapping.ConnectionCount == 0, "opening does not create a connection");
                CheckLayout(ui, panel);
                stage.CloseDiamondChoices(); yield return null;
            }
            Move(player, new Vector3(footprint.center.x, player.transform.position.y, footprint.min.z - 4.7f));
            yield return null; hand.onClick.Invoke(); yield return null;
            Check(panel.gameObject.activeInHierarchy, target.name + " near configured range boundary");
            Camera.main.transform.position = new Vector3(footprint.center.x, 10f, footprint.min.z - 15f);
            Camera.main.transform.LookAt(footprint.center);
            yield return Capture(target.name);
            stage.CloseDiamondChoices();
            Move(player, new Vector3(footprint.center.x, player.transform.position.y, footprint.min.z - 6f));
            yield return null; hand.onClick.Invoke(); yield return null;
            Check(!panel.gameObject.activeInHierarchy, target.name + " rejects out-of-range interaction");
        }

        // Build a deliberately incomplete mapping through the two real number panels.
        yield return Choose(stage, ui, player, 0, 0);
        Check(mapping.ConnectionCount == 1, "one selected pair creates exactly one laser");
        if (SessionState.GetBool("FR.OnboardingChecks", false))
            yield return FunctionsRelationsOnboardingPlayChecks.CheckActiveHelp(stage);
        Check(ui.submit.gameObject.activeInHierarchy, "Submit appears after creating a connection");
        Move(player, book.bounds.center + Vector3.back * 2f); yield return null;
        int mistakes = stage.MistakeCount;
        hand.onClick.Invoke(); yield return null;
        Check(stage.State == FunctionsRelationsState.ACTIVE && stage.MistakeCount == mistakes, "active Book never submits or restarts");
        Check(!context.gameObject.activeInHierarchy, "active Book has no start or submit prompt");
        float beforeWrong = stage.TimeRemaining;
        Click(ui.submit.GetComponent<Button>()); stage.Submit();
        Check(stage.MistakeCount == 1 && Mathf.Abs(stage.TimeRemaining - (beforeWrong - 10f)) < .05f, "incorrect answer deducts ten seconds once");
        Check(mapping.ConnectionCount == 1, "incorrect keeps placements");
        yield return new WaitForSecondsRealtime(.9f);
        Check(stage.State == FunctionsRelationsState.ACTIVE, "retry resumes unchanged flow");
        MoveNear(player, GameObject.Find("Range")); yield return null;
        hand.onClick.Invoke(); yield return null; Click(Field<Button>(stage, "undoConnectionButton"));
        Check(mapping.ConnectionCount == 0, "range Undo remains functional"); stage.CloseDiamondChoices();
        foreach (var pair in stage.CurrentChallenge.requiredPairs)
            yield return Choose(stage, ui, player, Array.IndexOf(stage.CurrentChallenge.domain, pair.domain), Array.IndexOf(stage.CurrentChallenge.codomain, pair.codomain));
        yield return ReadBoard(stage, ui, cameraView, true);
        yield return Capture("SubmitReady");
        Click(ui.submit.GetComponent<Button>());
        Check(stage.State == FunctionsRelationsState.CHALLENGE_COMPLETE && stage.CorrectCount == 1, "correct complete arrangement");
        float paused = stage.TimeRemaining;
        yield return new WaitForSecondsRealtime(1.8f);
        Check(stage.State == FunctionsRelationsState.WAITING_FOR_ACTIVATION && stage.TimeRemaining == paused && stage.ChallengeIndex == 1, "completion pauses shared timer and waits for book");
        Check(ui.progress.text == "CHALLENGE 2 OF 5", "HUD follows next challenge");
        Move(player, book.bounds.center + Vector3.back * 2f); yield return null; yield return new WaitForEndOfFrame(); Click(context); yield return null;
        Check(stage.State == FunctionsRelationsState.ACTIVE && stage.TimeRemaining <= paused && stage.TimeRemaining > paused - .2f, "next book resumes rather than resets timer");
        Check(Field<TMP_Text>(stage, "worldProblemText").text == stage.CurrentChallenge.instruction.Replace("→", "to"), "InformationBoard follows active variant instructions");
        // Complete the remaining challenge types through the HUD Submit, keeping QA out of saved progression.
        for (int challenge = 1; challenge < 5; challenge++)
        {
            Check(stage.ChallengeIndex == challenge && stage.State == FunctionsRelationsState.ACTIVE, "expected active challenge " + (challenge + 1));
            if (stage.CurrentChallenge.questionKind == MappingQuestionKind.BuildExactRelation)
                foreach (var pair in stage.CurrentChallenge.requiredPairs.Where(p => !mapping.Pairs.Contains(p)).ToArray())
                    yield return Choose(stage, ui, player, Array.IndexOf(stage.CurrentChallenge.domain, pair.domain), Array.IndexOf(stage.CurrentChallenge.codomain, pair.codomain));
            else if (stage.CurrentChallenge.questionKind == MappingQuestionKind.ClassifyFunction)
                stage.ChooseClassificationPedestal(stage.CurrentChallenge.correctClassificationIndex);
            else
            {
                var properties = new[] { MappingProperty.Injective, MappingProperty.Surjective, MappingProperty.Bijective };
                for (int i = 0; i < properties.Length; i++)
                    if ((stage.CurrentChallenge.correctProperties & properties[i]) != 0) stage.ChooseClassificationPedestal(i);
            }
            yield return null; Check(ui.submit.gameObject.activeInHierarchy, "Submit available for challenge " + (challenge + 1));
            CheckActionButtons(ui); Click(ui.submit.GetComponent<Button>());
            Check(stage.State == FunctionsRelationsState.CHALLENGE_COMPLETE && stage.CorrectCount == challenge + 1, "Submit validates challenge " + (challenge + 1));
            if (challenge == 4)
            {
                // Stop before the existing delayed cloud unlock: no QA rewards or progression writes.
                stage.StopAllCoroutines();
                Debug.Log("FUNCTIONS_RELATIONS_PLAY_CHECKS_PASS: camera framing/Back/cancellation, read-only view, active Book start-only, HUD Submit for all five challenges, one penalty per evaluation, preserved shared clock, eight-position interactions, mobile safe-area layout. Size=" + Screen.width + "x" + Screen.height);
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.8f);
            Check(stage.State == FunctionsRelationsState.WAITING_FOR_ACTIVATION && !ui.submit.gameObject.activeSelf, "next challenge waits for Book");
            Move(player, book.bounds.center + Vector3.back * 2f); yield return null; yield return new WaitForEndOfFrame();
            Click(context); yield return null;
        }
    }

    static IEnumerator ReadBoard(FunctionsRelationsStageManager stage, FunctionsRelationsPresentation ui, FunctionsRelationsBoardView view, bool active)
    {
        var camera = Camera.main; var player = Player.LocalInstance;
        bool oldPlayerEnabled = player.enabled;
        var oldPosition = camera.transform.position; var oldRotation = camera.transform.rotation; float oldFov = camera.fieldOfView;
        float time = stage.TimeRemaining; int pairs = UnityEngine.Object.FindFirstObjectByType<LaserMappingController>().ConnectionCount;
        int mistakes = stage.MistakeCount, correct = stage.CorrectCount, challenge = stage.ChallengeIndex;
        var lasers = view.connectionBeams.GetComponentsInChildren<LineRenderer>();
        var visibility = lasers.Select(l => l.enabled).ToArray();
        Click(ui.viewBoard.GetComponent<Button>()); yield return Settled(view);
        Check(stage.IsBoardOpen && view.Viewing && !player.enabled && GameInput.Instance.GameplayInputBlocked, "board camera blocks gameplay controls");
        Check(Field<CanvasGroup>(stage, "board").alpha == 0f, "legacy full-screen board stays hidden");
        Check(lasers.All(l => !l.enabled), "foreground lasers do not cover board text");
        Check(!ui.hud.gameObject.activeSelf && !ui.submit.gameObject.activeSelf, "reading view clears gameplay HUD and Submit");
        var b = view.BoardBounds; var min = Vector2.positiveInfinity; var max = Vector2.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var p = camera.WorldToScreenPoint(corner); Check(p.z > 0f, "board in front of camera");
            min = Vector2.Min(min, p); max = Vector2.Max(max, p);
        }
        Rect framed = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        var safe = ScreenRect(ui.safeArea);
        Check(safe.Contains(framed.min) && safe.Contains(framed.max - Vector2.one), "entire board inside safe area");
        Check(!framed.Overlaps(ScreenRect(ui.boardBack)), "Back clear of board");
        stage.ActivateChallenge(); stage.Submit(); GameInput.Instance.MobileInteract();
        yield return Capture(active ? "ActiveBoardView" : "WaitingBoardView");
        Check(stage.ChallengeIndex == challenge && stage.CorrectCount == correct && stage.MistakeCount == mistakes &&
            UnityEngine.Object.FindFirstObjectByType<LaserMappingController>().ConnectionCount == pairs, "view never submits, starts, or edits answers");
        Check(active ? stage.TimeRemaining < time : stage.TimeRemaining == time, "original countdown rule while viewing");
        Click(ui.boardBack.GetComponent<Button>()); yield return Settled(view); yield return null; yield return new WaitForEndOfFrame();
        Check(!stage.IsBoardOpen && player.enabled == oldPlayerEnabled && !GameInput.Instance.GameplayInputBlocked && ui.hud.gameObject.activeSelf, "Back restores controls");
        Check(lasers.Select((l, i) => l.enabled == visibility[i]).All(v => v), "Back restores laser visibility without editing connections");
        Check(Vector3.Distance(camera.transform.position, oldPosition) < .2f && Quaternion.Angle(camera.transform.rotation, oldRotation) < .3f && Mathf.Abs(camera.fieldOfView - oldFov) < .1f, "Back restores gameplay camera pose");
    }

    static IEnumerator Settled(FunctionsRelationsBoardView view)
    { float end = Time.realtimeSinceStartup + 3f; do { yield return null; Check(Time.realtimeSinceStartup < end, "camera transition terminates"); } while (view.Transitioning); }

    static void CheckActionButtons(FunctionsRelationsPresentation ui)
    {
        foreach (var button in new[] { ui.interaction, ui.viewBoard, ui.submit })
        {
            if (!button.gameObject.activeInHierarchy) continue;
            var text = button.GetComponentInChildren<TMP_Text>(); text.ForceMeshUpdate(); Check(!text.isTextOverflowing, button.name + " text fits");
            foreach (var control in ui.mobileControls)
                if (control != null && control.gameObject.activeInHierarchy) Check(!ScreenRect(button).Overlaps(ScreenRect(control)), button.name + " clear of " + control.name);
        }
    }

    static IEnumerator Choose(FunctionsRelationsStageManager stage, FunctionsRelationsPresentation ui, Player player, int domain, int range)
    {
        stage.CloseDiamondChoices(); MoveNear(player, GameObject.Find("Domain")); yield return null;
        GameInput.Instance.MobileInteract(); yield return null;
        CheckLayout(ui, ui.domainPanel);
        Click(Field<Button[]>(stage, "domainChoiceButtons")[domain]);
        MoveNear(player, GameObject.Find("Range")); yield return null;
        GameInput.Instance.MobileInteract(); yield return null;
        Click(Field<Button[]>(stage, "rangeChoiceButtons")[range]); yield return null; yield return new WaitForEndOfFrame();
        CheckLayout(ui, ui.rangePanel); stage.CloseDiamondChoices();
    }

    static void MoveNear(Player player, GameObject target)
    {
        Bounds b = target.GetComponentsInChildren<Collider>().First().bounds;
        foreach (var col in target.GetComponentsInChildren<Collider>()) b.Encapsulate(col.bounds);
        Move(player, new Vector3(b.center.x, player.transform.position.y, b.min.z - 3f));
    }
    static void Move(Player player, Vector3 position)
    { position.y = 1.5f; PrelimTeleportTransition.MovePlayerImmediately(player.gameObject, position, Quaternion.identity); }
    static void Click(Button button)
    {
        Check(button.gameObject.activeInHierarchy && button.interactable, button.name + " tappable");
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
        var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, button.name + " unobstructed touch target at " + point + " (hits: " + string.Join(", ", hits.Take(3).Select(h => h.gameObject.name)) + ")");
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    static void CheckLayout(FunctionsRelationsPresentation ui, RectTransform panel)
    {
        foreach (var text in panel.GetComponentsInChildren<TMP_Text>().Concat(ui.hud.GetComponentsInChildren<TMP_Text>())
                     .Concat(ui.interaction.GetComponentsInChildren<TMP_Text>()))
        { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, text.name + " no clipped text"); }
        Rect panelBounds = ScreenRect(panel), safe = ScreenRect(ui.safeArea);
        Check(safe.Contains(panelBounds.min) && safe.Contains(panelBounds.max - Vector2.one), "panel within safe area");
        foreach (var target in ui.mobileControls.Concat(new[] { ui.hud, ui.interaction, ui.viewBoard, ui.submit, ui.status }))
            if (target != null && target.gameObject.activeInHierarchy) Check(!panelBounds.Overlaps(ScreenRect(target)), "panel clear of " + target.name);
        foreach (var button in new[] { ui.interaction, ui.viewBoard, ui.submit })
            foreach (var control in ui.mobileControls)
                if (control != null && control.gameObject.activeInHierarchy && button.gameObject.activeInHierarchy)
                    Check(!ScreenRect(button).Overlaps(ScreenRect(control)), button.name + " clear of " + control.name);
    }
    static Rect ScreenRect(RectTransform rect)
    { var c = new Vector3[4]; rect.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
    static IEnumerator Capture(string name)
    {
        System.IO.Directory.CreateDirectory("Temp/FunctionsQA"); yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("Temp/FunctionsQA/" + name + "-" + Screen.width + "x" + Screen.height + ".png");
        yield return null; yield return null;
    }
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Functions and Relations Play check failed: " + message); }
}
