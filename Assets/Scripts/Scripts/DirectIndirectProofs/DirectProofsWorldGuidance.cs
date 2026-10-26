using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Main-board viewing and neutral altar guidance. Neither action validates an answer.</summary>
[DefaultExecutionOrder(1500), DisallowMultipleComponent]
public sealed class DirectProofsWorldGuidance : MonoBehaviour
{
    public ProofsBoardView BoardView { get; private set; }
    public UnityEngine.UI.Button ViewButton { get; private set; }
    public UnityEngine.UI.Button BackButton { get; private set; }
    public Transform[] Arrows { get; private set; }
    DirectIndirectProofStageController stage;
    Transform boardArea;
    RectTransform safe;
    Mesh arrowMesh;
    Material yellow, outline;
    readonly float[] amounts = new float[3];
    readonly Dictionary<GameObject, bool> hidden = new Dictionary<GameObject, bool>();
    bool held, inputWasBlocked, playerWasEnabled;
    GameInput input;
    Player player;

    public void Initialize(DirectIndirectProofStageController owner, RectTransform safeArea, Transform interactionArea)
    {
        stage = owner; safe = safeArea; boardArea = interactionArea;
        ViewButton = StageUITheme.Button("Direct Proofs View Board", safe, "View Board", OpenBoard);
        BackButton = StageUITheme.Button("Direct Proofs Board Back", safe, "Back", null);
        ProofsStageUI.Bottom((RectTransform)ViewButton.transform, new Vector2(0, 95), new Vector2(280, 88));
        ProofsStageUI.Bottom((RectTransform)BackButton.transform, new Vector2(0, 28), new Vector2(240, 88));
        ViewButton.image.pixelsPerUnitMultiplier = BackButton.image.pixelsPerUnitMultiplier = 4.5f;
        BackButton.gameObject.SetActive(false);
        BoardView = gameObject.AddComponent<ProofsBoardView>();
        // Frame the existing main board's backer, excluding the separate altar tablets.
        var backer = stage.InformationBoard.parent.Find("InformationBoard Visuals") ?? stage.InformationBoard.parent.Find("Board Wall Backer");
        BoardView.informationBoard = backer != null ? backer : stage.InformationBoard;
        BoardView.boardInstructions = stage.InformationBoard.GetComponent<TMP_Text>();
        BoardView.viewSafeArea = safe; BoardView.backButton = BackButton;
        BuildArrows();
    }

    public bool NearBoard
    {
        get
        {
            var local = Player.LocalInstance;
            if (local == null) return false;
            Vector3 delta = local.transform.position - boardArea.position; delta.y = 0;
            if (delta.sqrMagnitude <= 100) return true;
            foreach (var altar in stage.Altars)
            {
                if (!altar.gameObject.activeInHierarchy) continue;
                delta = local.transform.position - BoundsOf(altar).center; delta.y = 0;
                if (delta.sqrMagnitude <= 64) return true;
            }
            return false;
        }
    }

    void Update()
    {
        if (stage == null) return;
        if (BoardView.Viewing && (stage.Finished || !stage.isActiveAndEnabled)) Close();
        bool available = !stage.GuidanceBlocksGameplay && !stage.Finished && !stage.IsReaderOpen &&
            !BoardView.Viewing && Time.timeScale > 0 && NearBoard && GameInput.Instance != null && !GameInput.Instance.GameplayInputBlocked;
        ViewButton.gameObject.SetActive(available);
        if (BoardView.Viewing) BackButton.transform.SetAsLastSibling();
    }

    public void OpenBoard()
    {
        if (stage.GuidanceBlocksGameplay || stage.Finished || stage.IsReaderOpen || BoardView.Viewing || !NearBoard ||
            GameInput.Instance == null || GameInput.Instance.GameplayInputBlocked || Player.LocalInstance == null) return;
        if (!BoardView.OpenView(ReleaseInput)) return;
        input = GameInput.Instance; player = Player.LocalInstance;
        inputWasBlocked = input.GameplayInputBlocked; playerWasEnabled = player.enabled; held = true;
        var controls = GameObject.Find("Canvas 1")?.transform.Find("SafeArea/Gameplay_Interface");
        if (controls != null) Hide(controls.gameObject);
        foreach (var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            if (button.name == "AdminButton") Hide(button.gameObject);
        input.SetGameplayInputBlocked(true); player.ToggleControl(false);
        ViewButton.gameObject.SetActive(false);
        BackButton.transform.SetAsLastSibling();
        // Match normal problem reading: the active countdown continues while the board is viewed.
    }

    void Hide(GameObject item) { if (!hidden.ContainsKey(item)) hidden.Add(item, item.activeSelf); item.SetActive(false); }
    void ReleaseInput()
    {
        if (!held) return;
        held = false;
        foreach (var item in hidden) if (item.Key != null) item.Key.SetActive(item.Value);
        hidden.Clear();
        bool finished = stage != null && stage.Finished;
        if (input != null) input.SetGameplayInputBlocked(finished || inputWasBlocked);
        if (player != null) player.ToggleControl(!finished && playerWasEnabled);
        input = null; player = null;
    }
    public void Close() { if (BoardView != null) BoardView.RestoreImmediately(); ReleaseInput(); }

    void BuildArrows()
    {
        arrowMesh = new Mesh { name = "Direct Proofs Down Arrow" };
        arrowMesh.vertices = new[] { new Vector3(-.16f,.65f,0), new Vector3(.16f,.65f,0), new Vector3(.16f,0,0),
            new Vector3(.5f,0,0), new Vector3(0,-.6f,0), new Vector3(-.5f,0,0), new Vector3(-.16f,0,0) };
        arrowMesh.triangles = new[] { 0,1,2,0,2,6,5,3,4 }; arrowMesh.RecalculateNormals(); arrowMesh.RecalculateBounds();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        yellow = new Material(shader) { name = "Direct Proofs Guidance Yellow" }; yellow.SetColor("_BaseColor", new Color(1,.86f,.08f));
        outline = new Material(shader) { name = "Direct Proofs Arrow Outline" }; outline.SetColor("_BaseColor", new Color(.12f,.085f,.02f));
        Arrows = new Transform[3];
        for (int i = 0; i < Arrows.Length; i++)
        {
            var arrow = new GameObject("Proof Altar Guidance Arrow " + i).transform;
            // These are siblings, so arrow bounds cannot change altar centers, proximity or camera framing.
            arrow.SetParent(transform, false); Arrows[i] = arrow;
            Shape(arrow, "Outline", outline, 1.16f, .025f); Shape(arrow, "Yellow Arrow", yellow, 1, 0);
            arrow.gameObject.SetActive(false);
        }
    }
    void Shape(Transform parent, string label, Material material, float scale, float depth)
    {
        var item = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer)); item.transform.SetParent(parent, false);
        item.transform.localScale = Vector3.one * scale; item.transform.localPosition = Vector3.forward * depth;
        item.GetComponent<MeshFilter>().sharedMesh = arrowMesh;
        var renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }

    public bool HasSelection()
    {
        if (stage.ChallengeNumber == 1)
            return stage.SelectedMethod >= 0;
        // Opening an ordering altar selects it. Back to Room releases that target without changing its draft.
        return stage.OrderingBoard != null && stage.OrderingBoard.IsOpen && !stage.OrderingBoard.IsPractice;
    }
    void LateUpdate()
    {
        if (stage == null || Arrows == null) return;
        var camera = Camera.main;
        var altars = stage.Altars;
        bool active = stage.isActiveAndEnabled && stage.State == DirectIndirectProofStageController.TrialState.Active &&
            !stage.GuidanceBlocksGameplay && !stage.IsViewingBoard;
        for (int i = 0; i < Arrows.Length; i++)
        {
            bool eligible = active && altars[i].gameObject.activeInHierarchy;
            bool visible = eligible && !HasSelection();
            amounts[i] = eligible ? Mathf.MoveTowards(amounts[i], visible ? 1 : 0, Time.unscaledDeltaTime * 5) : 0;
            var arrow = Arrows[i]; arrow.gameObject.SetActive(amounts[i] > .001f);
            if (!arrow.gameObject.activeSelf) continue;
            Bounds bounds = BoundsOf(altars[i]);
            float top = bounds.max.y + 1.25f;
            var label = stage.AltarLabel(i);
            if (label != null && label.gameObject.activeInHierarchy) top = Mathf.Max(top, BoundsOf(label).max.y + 1.1f);
            arrow.position = new Vector3(bounds.center.x, top + Mathf.Sin(Time.unscaledTime * 2.4f) * .12f, bounds.center.z);
            if (camera != null) arrow.rotation = camera.transform.rotation;
            float pop = Mathf.SmoothStep(0, 1, amounts[i]) + Mathf.Sin(amounts[i] * Mathf.PI) * .12f;
            arrow.localScale = Vector3.one * (.95f * pop);
        }
    }
    static Bounds BoundsOf(Transform target)
    {
        var bounds = new Bounds(target.position, Vector3.zero); bool found = false;
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is TMP_SubMesh || renderer.GetComponent<TMP_Text>() != null) continue;
            if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }
    void OnDisable() { Close(); if (Arrows != null) foreach (var arrow in Arrows) if (arrow != null) arrow.gameObject.SetActive(false); }
    void OnDestroy()
    {
        Close(); if (arrowMesh != null) Destroy(arrowMesh); if (yellow != null) Destroy(yellow); if (outline != null) Destroy(outline);
    }
}
