using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using LogicLegends.Inference;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Opt-in checks on the current scene; no account changes or scene saves.</summary>
public static class InferenceStagePlayChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (!SessionState.GetBool("Inference.StageChecks", false)) return;
        SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
    }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "RulesOfInference") return;
        SceneManager.sceneLoaded -= Loaded;
        var avatar = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Multiplayer_Male"));
        var sync = avatar.GetComponent<MultiplayerAvatarSync>(); if (sync != null) sync.enabled = false;
        avatar.GetComponent<CharacterController>().enabled = true;
        var book = UnityEngine.Object.FindFirstObjectByType<InferenceBookStart>();
        Move(avatar.GetComponent<Player>(), book.transform.position + Vector3.forward * 3f, Quaternion.Euler(0, 180, 0));
        new GameObject("Inference QA Runner").AddComponent<InferenceStageCheckRunner>().StartCoroutine(Run());
    }
    static IEnumerator Run()
    {
        var type = Type.GetType("UnityEditor.GameView,UnityEditor"); var view = EditorWindow.GetWindow(type);
        int size = SessionState.GetInt("Inference.CheckSize", 2);
        type.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, size, null); view.Focus();
        Crystal.SafeArea.Sim = size == 12 ? Crystal.SafeArea.SimDevice.Pixel3XL_LSR : size == 4 ? Crystal.SafeArea.SimDevice.iPhoneX : Crystal.SafeArea.SimDevice.None;
        float readyUntil = Time.unscaledTime + 45f;
        while ((StageJourneyUI.Instance == null || !Field<bool>(StageJourneyUI.Instance, "tutorialOpen")) && Time.unscaledTime < readyUntil) yield return null;
        var stage = UnityEngine.Object.FindFirstObjectByType<InferenceChallenge>(); var journey = StageJourneyUI.Instance;
        Check(stage.requiredRounds == 5 && !stage.HasStarted && stage.Question == null, "spawn has five book-gated challenges");
        Check(Field<bool>(journey, "tutorialOpen") && Time.timeScale == 0f, "initial Help pauses gameplay");
        var title = Field<TMP_Text>(journey, "tutorialTitle"); var body = Field<TMP_Text>(journey, "tutorialBody");
        var next = Field<Button>(journey, "tutorialNext");
        string all = "";
        for (int page = 0; page < 7; page++)
        {
            yield return null; yield return new WaitForEndOfFrame(); title.ForceMeshUpdate(); body.ForceMeshUpdate();
            Check(!title.isTextOverflowing && !body.isTextOverflowing, "Help page fits mobile: " + page); all += body.text;
            if (page == 3) yield return Capture("HelpCrystals");
            if (page < 6) Click(next);
        }
        Check(all.Contains("Premises are") && all.Contains("rule of inference") && all.Contains("Rule Crystal") && all.Contains("conclusion") && !all.ToLowerInvariant().Contains("diamond"), "terminology and five-question flow are taught");
        journey.CloseInferenceTutorial(); yield return null;
        var tutorial = InferenceControlsOnboarding.Active; Check(tutorial != null, "guide launches existing tutorial");
        Invoke(tutorial, "BeginBoardApproach"); yield return null;
        Check(!tutorial.SuppressMovement && tutorial.AllowsJump && !GameInput.Instance.GameplayInputBlocked, "altar approach keeps movement and jump");
        var player = Player.LocalInstance; var controller = player.GetComponent<CharacterController>();
        var access = stage.GetComponent<InferenceAltarAccess>(); Check(access != null && GameObject.Find("Altar Invisible Stair Ramp") != null, "invisible stair support exists");
        Vector3 start = access.Center + Vector3.forward * (access.OuterRadius + 1f);
        var hits = Physics.RaycastAll(start + Vector3.up * 30f, Vector3.down, 70f, ~0, QueryTriggerInteraction.Ignore).Where(h => h.normal.y > .7f && h.collider.GetComponentInParent<Player>() == null).OrderBy(h => h.distance).ToArray();
        Check(hits.Length > 0, "floor at stair entry"); start.y = hits[0].point.y + .1f; Move(player, start, Quaternion.Euler(0,180,0));
        yield return new WaitForSecondsRealtime(.25f);
        bool jumped = false; Action jumpEvent = () => jumped = true; player.OnJumpStarted += jumpEvent;
        GameInput.Instance.MobileJump(); yield return new WaitForSecondsRealtime(.2f); player.OnJumpStarted -= jumpEvent;
        Check(jumped && player.IsJumping(), "real jump succeeds during altar approach");
        yield return new WaitForSecondsRealtime(1.2f);
        float until = Time.unscaledTime + 8f;
        while (player.transform.position.z > access.Center.z + 6f && Time.unscaledTime < until)
        {
            controller.Move((Vector3.back * 9f + Vector3.down * 8f) * Time.deltaTime); yield return null;
        }
        Check(player.transform.position.z < access.Center.z + 7f && player.transform.position.y > access.TopHeight - 1f, "CharacterController walks continuously up existing stairs");
        yield return new WaitForSecondsRealtime(.25f);
        var altar = UnityEngine.Object.FindFirstObjectByType<InferenceAltarTrigger>();
        var viewBoard = Field<Button>(altar, "viewBoardButton"); Check(viewBoard.gameObject.activeInHierarchy, "walk reaches altar board interaction");
        Click(viewBoard); yield return null;
        Check(tutorial.CurrentStep == "BoardPremises", "existing View Board opens tutorial explanation");
        var explanation = Field<TextMeshProUGUI>(tutorial, "boardExplanation");
        for (int step = 0; step < 3; step++)
        {
            yield return null; yield return new WaitForEndOfFrame(); explanation.ForceMeshUpdate();
            Check(!explanation.isTextOverflowing && !explanation.text.ToLowerInvariant().Contains("diamond"), "board definition remains readable");
            Click(Field<Button>(tutorial, "boardNextButton"));
        }
        Click(Field<Button>(tutorial, "boardBackButton")); yield return null; yield return new WaitForEndOfFrame();
        Check(tutorial.IsCrystalPractice, "crystal lesson uses practice data");
        var interaction = stage.GetComponent<InferenceCrystalInteraction>();
        yield return null; Click(interaction.CollectButton); yield return null;
        Check(tutorial.CurrentStep == "Confirm" && !stage.HasStarted && stage.Question == null, "dedicated practice pickup does not start real challenge");
        tutorial.ConfirmYes(); yield return null;
        var correctRules = new HashSet<string>();
        for (int challenge = 0; challenge < 5; challenge++)
        {
            var book = UnityEngine.Object.FindFirstObjectByType<InferenceBookStart>();
            Move(player, book.transform.position + Vector3.forward * 2f, Quaternion.Euler(0,180,0)); yield return new WaitForSecondsRealtime(.25f);
            GameInput.Instance.MobileInteract(); yield return null;
            Check(stage.HasStarted && stage.SolvedRounds == challenge, "real hand starts challenge " + (challenge + 1));
            stage.OpenBoard(player); float deadline = Time.unscaledTime + 4f;
            while (!stage.boardInput.interactable && Time.unscaledTime < deadline) yield return null;
            Check(stage.Question != null && stage.Question.Spec.Difficulty == challenge + 1 && correctRules.Add(stage.Question.Rule.abbreviation), "one valid question from the correct bank, without repeated rule");
            // Fill the actual draggable tiles and keep the existing validation.
            var tiles = stage.dragPuzzle.wordBank.GetComponentsInChildren<DraggableWord>().ToList();
            foreach (var zone in stage.dragPuzzle.Zones)
            {
                string expected = stage.dragPuzzle.Puzzle.CorrectPlacements[stage.dragPuzzle.Zones.ToList().IndexOf(zone)];
                var tile = tiles.First(x => x.Word == expected); tiles.Remove(tile); zone.Place(tile);
            }
            stage.ValidateBoard(); yield return null;
            int crystalCount = Mathf.Clamp(stage.ruleDiamondCount, 4, stage.pillarCrystalPrefab != null ? 8 : 5);
            Check(stage.Stage == InferencePuzzleStage.FindingDiamond && stage.Crystals.Count == crystalCount, "correct premises enable rule-crystal search");
            stage.CloseBoard(); yield return null;
            var correct = stage.Crystals.First(x => x.GetComponent<RuleDiamond>().Abbreviation == stage.Question.Rule.abbreviation);
            // Stand at the pillar base within the configured pickup volume.
            Vector3 pickup = correct.PickupOrigin + Vector3.back * 2f; pickup.y = correct.PickupOrigin.y - 7.5f;
            Move(player, pickup, Quaternion.identity); yield return new WaitForSecondsRealtime(.3f);
            Check(interaction.CollectButton.gameObject.activeInHierarchy, "nearby crystal has dedicated prompt");
            if (challenge % 2 == 0) Click(interaction.CollectButton); else GameInput.Instance.MobileInteract();
            yield return null;
            Check(player.GetHeldObject() != null && player.GetHeldObject().GetComponent<InferenceCrystal>() != null, "both pickup methods use the existing carry system");
            var held = player.GetHeldObject().GetComponent<InferenceCrystal>();
            GameInput.Instance.MobileInteract(); yield return null;
            Check(player.GetHeldObject() == null && !held.GetComponent<SphereCollider>().isTrigger, "immediate drop has solid collision synchronously");
            yield return new WaitForSecondsRealtime(.8f);
            Check(held.transform.position.y >= held.PickupOrigin.y - 12f && stage.Crystals.Count == crystalCount, "immediate drop remains accessible without duplicates");
            // Explicitly exercise fall and embedded-geometry recovery of this same instance.
            held.GetComponent<Rigidbody>().position = held.PickupOrigin + Vector3.down * 20f;
            held.transform.position = held.PickupOrigin + Vector3.down * 20f;
            Physics.SyncTransforms(); yield return null; yield return new WaitForEndOfFrame();
            Check(Vector3.Distance(held.transform.position, held.PickupOrigin) < .1f && stage.Crystals.Count == crystalCount, "out-of-bounds recovery returns the same crystal");
            var trap = GameObject.CreatePrimitive(PrimitiveType.Cube); trap.name = "QA Crystal Trap"; trap.transform.position = held.PickupOrigin + Vector3.down * 4f; trap.transform.localScale = Vector3.one * 8f;
            var constraints = held.GetComponent<Rigidbody>().constraints;
            held.GetComponent<Rigidbody>().isKinematic = false; held.GetComponent<Rigidbody>().useGravity = false;
            held.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            held.GetComponent<SphereCollider>().isTrigger = false; held.transform.position = trap.transform.position;
            Physics.SyncTransforms(); yield return new WaitForSecondsRealtime(.9f); UnityEngine.Object.Destroy(trap);
            Check(held.GetComponent<Rigidbody>().isKinematic && Vector3.Distance(held.transform.position, held.PickupOrigin) < .1f, "embedded crystal returns to reachable origin");
            held.GetComponent<Rigidbody>().constraints = constraints;
            correct.GetComponent<GrabbableObject>().Grab(player.GetHoldPoint()); player.SetHeldObjectSilently(correct.GetComponent<GrabbableObject>());
            stage.OpenBoard(player); deadline = Time.unscaledTime + 4f;
            while (!stage.boardInput.interactable && Time.unscaledTime < deadline) yield return null;
            if (challenge == 4) stage.requiredRounds = 6; // Prevent the production account unlock call in QA.
            stage.PlaceHeldCrystal(); yield return null;
            Check(stage.SolvedRounds == challenge + 1, "selected rule validates and completes one question");
            stage.PlaceHeldCrystal(); Check(stage.SolvedRounds == challenge + 1, "repeated validation cannot duplicate progress");
            if (challenge < 4) Check(!stage.HasStarted && !stage.IsBoardOpen, "next challenge waits for book");
        }
        stage.requiredRounds = 5;
        typeof(InferenceChallenge).GetField("<PuzzleCompleted>k__BackingField", Private).SetValue(stage, true);
        Invoke(journey, "OnInferenceComplete"); yield return null;
        Check(Field<bool>(journey, "resultOpen"), "existing completion UI for five successes");
        yield return Capture("StageComplete");
        Debug.Log("INFERENCE_STAGE_PLAY_PASS " + Screen.width + "x" + Screen.height + " five banks/no repeated rule, readable Help/tutorial, stair walking/jump, both pickup methods, safe drops, fall/trap recovery, book/timer/board progression");
    }
    static IEnumerator Capture(string name) { System.IO.Directory.CreateDirectory("Temp/InferenceQA"); yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Temp/InferenceQA/" + Screen.width + "x" + Screen.height + "_" + name + ".png"); yield return null; }
    static void Move(Player p, Vector3 pos, Quaternion rot)
    {
        var controller = p.GetComponent<CharacterController>();
        var floor = Physics.RaycastAll(pos + Vector3.up * 20f, Vector3.down, 50f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.normal.y > .7f && h.collider.GetComponentInParent<Player>() == null && h.collider.GetComponentInParent<InferenceCrystal>() == null).OrderBy(h => h.distance).ToArray();
        if (floor.Length > 0) pos.y = floor[0].point.y + controller.height * .5f - controller.center.y + .12f;
        PrelimTeleportTransition.MovePlayerImmediately(p.gameObject, pos, rot);
    }
    static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
    static object Invoke(object obj, string name) => obj.GetType().GetMethod(name, Private).Invoke(obj, null);
    static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases(); Check(button != null && button.gameObject.activeInHierarchy && button.interactable, "button available " + (button == null ? "null" : button.name));
        var rect = (RectTransform)button.transform; var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        var data = new PointerEventData(EventSystem.current) { position = (corners[0] + corners[2]) * .5f };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "button unobscured " + button.name);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    static void Check(bool value, string message) { if (!value) throw new Exception("INFERENCE_QA: " + message); }
}
public sealed class InferenceStageCheckRunner : MonoBehaviour { }
