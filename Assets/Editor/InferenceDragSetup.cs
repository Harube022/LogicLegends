using System;
using System.Linq;
using LogicLegends.Inference;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Update only the standalone altar board, preserving shared assets and the other stages.</summary>
public static class InferenceDragSetup
{
    public static string Configure()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/RulesOfInference.unity")
            throw new InvalidOperationException("Open RulesOfInference outside Play Mode.");
        var c = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<InferenceChallenge>(true)).Single();
        var b = c.dragPuzzle;
        if (b == null) throw new InvalidOperationException("Existing standalone drag board required.");
        var board = (RectTransform)c.board.transform;
        board.sizeDelta = new Vector2(1600, 1000);
        var body = (RectTransform)board.Find("BoardContent");
        var work = (RectTransform)body.Find("PuzzleWorkspace");
        var left = (RectTransform)work.Find("ArgumentWorkspace");
        var right = (RectTransform)work.Find("RuleSelection");
        if (right == null) right = (RectTransform)work.Find("WordBankWorkspace");
        right.name = "WordBankWorkspace";
        Height(work.gameObject, 620);
        var columns = work.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>(); columns.spacing = 28;
        var leftSize = left.GetComponent<UnityEngine.UI.LayoutElement>();
        leftSize.preferredWidth = 1180; leftSize.minWidth = 1180; leftSize.flexibleWidth = 1;
        var rightSize = right.GetComponent<UnityEngine.UI.LayoutElement>(); rightSize.preferredWidth = rightSize.minWidth = 260;
        c.legend.gameObject.SetActive(false);
        Height(c.premiseContainer.gameObject, 440);
        var premiseLayout = c.premiseContainer.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        premiseLayout.spacing = 0; premiseLayout.childForceExpandHeight = false;
        var backdrop = left.GetComponent<UnityEngine.UI.Image>();
        if (backdrop == null) backdrop = left.gameObject.AddComponent<UnityEngine.UI.Image>();
        backdrop.color = new Color(0.08f, 0.14f, 0.16f); backdrop.raycastTarget = false;
        var leftLayout = left.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        leftLayout.padding = new RectOffset(20, 20, 20, 20); leftLayout.spacing = 18;
        var conclusion = c.conclusionLabel.transform.parent;
        conclusion.SetParent(left, false); Height(conclusion.gameObject, 112);
        c.conclusionLabel.fontSize = 30;
        c.conclusionLabel.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 1000;
        var icon = conclusion.GetComponentsInChildren<TMPro.TMP_Text>().First(t => t != c.conclusionLabel);
        icon.text = "?"; icon.fontSize = 38;
        icon.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 50;
        var oldTitle = right.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(t => t != c.wordBank && t.text == "SELECT A RULE");
        if (oldTitle != null) oldTitle.gameObject.SetActive(false);
        b.ruleChoices.gameObject.SetActive(false);
        c.wordBank.transform.SetParent(right, false); c.wordBank.transform.SetAsFirstSibling();
        c.wordBank.text = "WORD BANK"; c.wordBank.fontSize = 29; Height(c.wordBank.gameObject, 50);
        b.wordBank.SetParent(right, false); b.wordBank.SetSiblingIndex(1); Height(b.wordBank.gameObject, 510);
        var grid = b.wordBank.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.constraintCount = 1; grid.cellSize = new Vector2(180, 58); grid.spacing = new Vector2(0, 14);
        grid.childAlignment = TextAnchor.UpperCenter;
        var intro = body.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t => t.text.StartsWith("Build the premises.") || t.text.StartsWith("Complete the words."));
        intro.text = "Complete the words. Then bring a rule diamond to the altar.";
        Height(c.feedback.gameObject, 72);
        c.placeButton.GetComponentInChildren<TMPro.TMP_Text>().text = "Place diamond here";
        c.exploreButton.GetComponentInChildren<TMPro.TMP_Text>().text = "Explore / return";
        c.placeButton.gameObject.SetActive(false); c.nextButton.gameObject.SetActive(false);
        c.validateButton.gameObject.SetActive(true);
        c.ruleDiamondCount = 4;

        // The altar socket is physical and reachable, independent of the camera-facing UI.
        var altar = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<InferenceAltarTrigger>(true)).Single();
        var socket = c.transform.Find("RuleDiamondSocket");
        if (socket == null) { socket = new GameObject("RuleDiamondSocket").transform; socket.SetParent(c.transform); }
        RaycastHit altarSurface;
        var tabletop = altar.transform.position + Vector3.back * 8;
        if (!Physics.Raycast(tabletop + Vector3.up * 40, Vector3.down, out altarSurface, 100, ~0, QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException("No altar surface for diamond placement.");
        socket.position = altarSurface.point + Vector3.up * 0.75f;
        socket.rotation = Quaternion.identity;
        if (b.conclusion.successParticles != null)
        {
            b.conclusion.successParticles.transform.SetParent(socket, false);
            b.conclusion.successParticles.transform.localPosition = Vector3.zero;
            b.conclusion.successParticles.transform.localScale = Vector3.one;
        }
        c.conclusionSocket = socket;
        if (c.diamondPlacementMarker == null)
        {
            var marker = new GameObject("RuleDiamondPlacementMarker", typeof(RectTransform));
            marker.transform.SetParent(socket, false);
            var text = marker.AddComponent<TMPro.TextMeshPro>();
            text.font = c.font; text.text = "PLACE DIAMOND HERE";
            text.fontSize = 12; text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = c.heading.color; text.rectTransform.sizeDelta = new Vector2(16, 3);
            marker.transform.localPosition = Vector3.up * 2.3f;
            marker.AddComponent<FaceCamera>();
            c.diamondPlacementMarker = marker;
        }
        c.diamondPlacementMarker.GetComponent<TMPro.TMP_Text>().fontSize = 12;
        c.diamondPlacementMarker.SetActive(false);
        Vector3[] offsets = { new Vector3(-11,0,7), new Vector3(11,0,7), new Vector3(-10,0,-11), new Vector3(10,0,-11), new Vector3(0,0,13) };
        var spawns = c.crystalSpawns.ToList();
        while (spawns.Count < 5) { var spawn = new GameObject("CrystalSpawn_" + (spawns.Count + 1)).transform; spawn.SetParent(c.transform); spawns.Add(spawn); }
        Physics.SyncTransforms();
        for (int i = 0; i < 5; i++)
        {
            var position = altar.transform.position + offsets[i];
            RaycastHit hit;
            if (!Physics.Raycast(position + Vector3.up * 40, Vector3.down, out hit, 100, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("No floor at diamond spawn " + i);
            spawns[i].position = hit.point + Vector3.up * 0.75f;
        }
        c.crystalSpawns = spawns.ToArray();
        // Keep the user's board orientation/location and frame it through the existing camera.
        c.focusCamera.transform.position = board.position - board.forward * 15.5f;
        c.focusCamera.transform.rotation = board.rotation;
        c.board.SetActive(true); Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(body);
        b.Build(new InferenceQuestion(c.rules.First(r => r.abbreviation == "MP"), 0, 0), c.rules);
        c.feedback.text = "Drag the words into the blanks, then check the argument.";
        c.board.SetActive(false); c.focusCamera.SetActive(false);
        EditorUtility.SetDirty(c); EditorUtility.SetDirty(b);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Updated: simple sentence blanks, small vertical word bank, physical altar socket and five possible spawn locations (four diamonds per puzzle).";
    }
    static void Height(GameObject obj, float height)
    {
        var size = obj.GetComponent<UnityEngine.UI.LayoutElement>();
        if (size == null) size = obj.AddComponent<UnityEngine.UI.LayoutElement>();
        size.preferredHeight = size.minHeight = height;
    }
}
