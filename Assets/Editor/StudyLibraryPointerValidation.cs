#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Run in Play mode after focusing a shelf and allowing its camera blend to finish.
// These checks use the real EventSystem raycast order, not Button.onClick.Invoke.
public static class StudyLibraryPointerValidation
{
    [MenuItem("Logic Legends/Validate Focused Study Shelf")]
    private static void ValidateFocusedShelf()
    {
        Require(Application.isPlaying, "Enter Play mode and approach a bookshelf first.");
        var controller = UnityEngine.Object.FindFirstObjectByType<StudyLibraryController>();
        var shelf = UnityEngine.Object.FindObjectsByType<StudyShelfZone>(FindObjectsSortMode.None)
            .Single(s => controller.IsViewing(s));
        StartValidation(shelf.name.Replace(" Shelf Interaction Zone", string.Empty));
    }

    public static bool IsRunning { get; private set; }
    public static string LastResult { get; private set; }
    private static IEnumerator routine;
    private static double nextStep;

    public static void StartValidation(string period)
    {
        Require(!IsRunning, "A shelf validation is already running.");
        LastResult = "Running " + period;
        IsRunning = true;
        routine = ValidateShelf(period);
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextStep) return;
        nextStep = EditorApplication.timeSinceStartup + 0.1;
        try
        {
            if (routine.MoveNext()) return;
        }
        catch (Exception error)
        {
            LastResult = "FAIL: " + error.Message;
            Debug.LogError(LastResult);
        }
        IsRunning = false;
        EditorApplication.update -= Tick;
        routine = null;
    }

    private static IEnumerator ValidateShelf(string period)
    {
        Require(Application.isPlaying, "Validation requires Play mode.");
        var controller = UnityEngine.Object.FindFirstObjectByType<StudyLibraryController>();
        var shelf = UnityEngine.Object.FindObjectsByType<StudyShelfZone>(FindObjectsSortMode.None)
            .Single(s => s.name == period + " Shelf Interaction Zone");
        Require(controller.IsViewing(shelf), "Focus the shelf and wait for the camera blend first.");
        var definitions = StudyLibraryCatalog.GetShelf(period);
        var cards = shelf.GetComponentsInChildren<Button>().OrderBy(b => b.name).ToArray();
        Require(cards.Length == definitions.Length, "Topic card count does not match the catalog.");
        var reader = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(c => c.name == "Open Book Reader").gameObject;
        var controls = reader.GetComponentsInChildren<Button>(true);
        var next = controls.Single(b => b.name == "Next Pages");
        var previous = controls.Single(b => b.name == "Previous Pages");
        var back = controls.Single(b => b.name == "Close Book");
        var left = reader.GetComponentsInChildren<RawImage>(true).Single(i => i.name == "Source Slide Left");
        var right = reader.GetComponentsInChildren<RawImage>(true).Single(i => i.name == "Source Slide Right");
        var texts = reader.GetComponentsInChildren<TMPro.TMP_Text>(true);
        var leftText = texts.Single(t => t.name == "Left Page Content");
        var rightText = texts.Single(t => t.name == "Right Page Content");
        var leftDiagram = reader.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Native Diagram Left").gameObject;
        var rightDiagram = reader.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Native Diagram Right").gameObject;
        var result = new List<string>();
        for (int topic = 0; topic < cards.Length; topic++)
        {
            var definition = definitions[topic];
            Click(cards[topic]);
            yield return null;
            Require(reader.activeInHierarchy, definition.title + " did not open.");
            Require(controller.CurrentSpreadStartPage == 0 && !previous.interactable, "First-page state did not reset.");
            int visited = 0;
            while (true)
            {
                int page = controller.CurrentSpreadStartPage;
                ValidatePage(definition.lessonPages[page], left, leftDiagram, leftText, "left");
                visited++;
                if (page + 1 < definition.PageCount)
                {
                    ValidatePage(definition.lessonPages[page + 1], right, rightDiagram, rightText, "right");
                    visited++;
                }
                else Require(right.texture == null && !right.gameObject.activeInHierarchy, "Stale right page at end of odd-page book.");
                if (!next.interactable) break;
                Click(next);
                yield return null;
                Require(controller.CurrentSpreadStartPage == page + 2, "Next did not turn the spread.");
                Require(visited <= definition.PageCount, "Navigation did not terminate.");
            }
            Require(visited == definition.PageCount, "Not all pages were visited.");
            int lastPage = controller.CurrentSpreadStartPage;
            Click(next);
            yield return null;
            Require(controller.CurrentSpreadStartPage == lastPage, "Disabled Next changed the page.");
            while (previous.interactable)
            {
                int page = controller.CurrentSpreadStartPage;
                Click(previous);
                yield return null;
                Require(controller.CurrentSpreadStartPage == page - 2, "Previous did not turn the spread.");
            }
            Click(previous);
            yield return null;
            Require(controller.CurrentSpreadStartPage == 0, "Disabled Previous changed the page.");
            if (string.IsNullOrWhiteSpace(definition.lessonPages[0].text) && string.IsNullOrWhiteSpace(definition.lessonPages[0].diagram))
            {
                Click(left.GetComponent<Button>());
                yield return null;
                Require(reader.GetComponentsInChildren<RawImage>().Any(i => i.name == "Source Slide Detail" && i.texture == left.texture), "Page enlargement failed.");
                Click(reader.GetComponentsInChildren<RawImage>().Single(i => i.name == "Source Slide Detail").GetComponent<Button>());
                yield return null;
            }
            Click(back);
            yield return null;
            Require(!reader.activeSelf && controller.IsViewing(shelf), "Back did not return to the shelf.");
            Require(left.texture == null && right.texture == null, "Previous book's textures were retained.");
            result.Add(definition.title + ": PASS (" + visited + " source pages, both arrows, boundaries, enlargement, Back)");
        }
        LastResult = string.Join("\n", result);
        Debug.Log(LastResult);
    }

    private static void ValidatePage(StudyLessonPage page, RawImage image, GameObject diagram, TMPro.TMP_Text text, string side)
    {
        if (!string.IsNullOrWhiteSpace(page.diagram))
        {
            Require(!image.gameObject.activeInHierarchy, "Native diagram page still shows the " + side + " source image.");
            Require(!text.gameObject.activeInHierarchy, "Native diagram page still shows the " + side + " fallback text.");
            Require(diagram.activeInHierarchy && diagram.transform.childCount > 0, "Native " + side + " diagram was not built.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(page.text))
        {
            Require(!image.gameObject.activeInHierarchy, "Text page still shows the " + side + " source image.");
            Require(text.gameObject.activeInHierarchy && text.text == page.text, "Wrong " + side + " text page.");
            return;
        }

        Require(image.gameObject.activeInHierarchy, "Source image is hidden on the " + side + " page.");
        Require(AssetDatabase.GetAssetPath(image.texture) == "Assets/Resources/" + page.image + ".png", "Wrong " + side + " source page.");
    }

    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var canvas = button.GetComponentInParent<Canvas>();
        var rect = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        Require(hits.Count > 0, "No pointer hit for " + button.name);
        var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Require(target == button.gameObject, button.name + " blocked by " + hits[0].gameObject.name);
        data.pointerCurrentRaycast = hits[0];
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
        Canvas.ForceUpdateCanvases();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
