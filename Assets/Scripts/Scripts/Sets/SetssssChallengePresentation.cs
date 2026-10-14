using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Setssss penalty feedback and the paused Challenge 4 explanation.</summary>
[DisallowMultipleComponent]
public sealed class SetssssChallengePresentation : MonoBehaviour
{
    private TMP_Text timer;
    private RectTransform safeArea, card, deductionsRoot;
    private RectTransform deductionArea;
    private GameObject overlay;
    private ScrollRect scroll;
    private readonly List<Behaviour> pausedCameraInputs = new List<Behaviour>();
    private readonly List<Deduction> deductions = new List<Deduction>();
    private readonly TMP_Text[] deductionLabels = new TMP_Text[3];
    private readonly CanvasGroup[] deductionGroups = new CanvasGroup[3];
    private GameInput input;
    private Player player;
    private bool inputWasBlocked, playerWasEnabled, cursorWasVisible;
    private float previousTimeScale;
    private CursorLockMode previousCursorLock;
    private Action onContinue;
    private Vector3 lastScale;
    private struct Deduction { public float amount, shownAt; }

    public bool IsIntroductionOpen { get; private set; }
    public bool HasVisibleDeductions => deductions.Count > 0;
    public Button ContinueButton { get; private set; }
    public const string Explanation =
        "Two additional placement regions have appeared: A − B and B − A.\n\n" +
        "<b>A − B:</b> Elements that belong to A but do not belong to B.\n" +
        "<b>B − A:</b> Elements that belong to B but do not belong to A.\n\n" +
        "Shared elements belong in <b>A ∩ B</b>. A and B are sets; this challenge's marked A and B answer regions also require separate copies of every listed element, plus copies in the appropriate difference regions. Each physical element block fills only one placement.\n\n" +
        "<b>Example only — not the current question:</b>\nA = {1, 2}, B = {2, 3}.\n" +
        "Use copies of 1 in A and A − B; copies of 3 in B and B − A; and three copies of 2, one each in A, B, and A ∩ B.\n\n" +
        "Every Challenge 4 pickup from the spawner immediately leaves another element block with the same number. Picking up a placed element block does not create a copy.\n\n" +
        "Drop element blocks where you want them to land. Let them settle, then press Submit. Incorrect placements stay in position and receive red marks; each incorrect submission costs 10 seconds.";

    public void Initialize(TMP_Text timerText)
    {
        if (overlay != null || timerText == null) return;
        timer = timerText;
        Canvas canvas = timer.GetComponentInParent<Canvas>();
        safeArea = canvas != null ? canvas.transform.Find("SafeArea") as RectTransform : null;
        if (safeArea == null) safeArea = canvas != null ? canvas.transform as RectTransform : timer.transform.parent as RectTransform;

        deductionsRoot = StageUITheme.Rect("Setssss Time Deductions", timer.transform.parent);
        deductionsRoot.pivot = new Vector2(.5f, 1f);
        for (int i = 0; i < deductionLabels.Length; i++)
        {
            RectTransform row = StageUITheme.Rect("Deduction " + (i + 1), deductionsRoot);
            row.anchorMin = row.anchorMax = new Vector2(.5f, 1f);
            row.pivot = new Vector2(.5f, 1f);
            row.sizeDelta = new Vector2(230f, 46f);
            row.anchoredPosition = new Vector2(0f, -i * 50f);
            deductionGroups[i] = row.gameObject.AddComponent<CanvasGroup>();
            deductionGroups[i].blocksRaycasts = false;
            Image background = row.gameObject.AddComponent<Image>();
            background.color = new Color(.035f, .06f, .065f, .85f);
            background.raycastTarget = false;
            deductionLabels[i] = StageUITheme.Text("Amount", row, "", 38f, true);
            StageUITheme.Stretch(deductionLabels[i].rectTransform, Vector2.zero, Vector2.one);
            deductionLabels[i].color = new Color(1f, .42f, .35f);
            deductionLabels[i].enableAutoSizing = true;
            deductionLabels[i].fontSizeMin = 30f; deductionLabels[i].fontSizeMax = 38f;
            row.gameObject.SetActive(false);
        }

        RectTransform root = StageUITheme.Rect("Setssss Challenge 4 Introduction", safeArea);
        StageUITheme.Stretch(root, Vector2.zero, Vector2.one);
        Image dimmer = root.gameObject.AddComponent<Image>();
        dimmer.color = new Color(.012f, .025f, .03f, .92f);
        overlay = root.gameObject;
        card = StageUITheme.Rect("Explanation Card", root);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
        StageUITheme.CompactSurface(card.gameObject.AddComponent<Image>());
        TMP_Text title = StageUITheme.Text("Warning Title", card, "Warning: Difficulty has increased!", 44f);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.pivot = new Vector2(.5f, 1f);
        title.rectTransform.offsetMin = new Vector2(36f, -130f);
        title.rectTransform.offsetMax = new Vector2(-36f, -24f);
        title.enableAutoSizing = true; title.fontSizeMin = 34f; title.fontSizeMax = 44f;

        RectTransform viewport = StageUITheme.Rect("Explanation Viewport", card);
        StageUITheme.Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.offsetMin = new Vector2(42f, 142f);
        viewport.offsetMax = new Vector2(-42f, -142f);
        Image hitArea = viewport.gameObject.AddComponent<Image>(); hitArea.color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        TMP_Text body = StageUITheme.Text("Explanation", viewport, Explanation, 34f, true);
        RectTransform content = body.rectTransform;
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f); content.offsetMin = content.offsetMax = Vector2.zero;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.margin = new Vector4(0f, 0f, 10f, 12f);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;
        TMP_Text scrollHint = StageUITheme.Text("Scroll Hint", card, "Swipe to read the full explanation", 24f, true);
        scrollHint.rectTransform.anchorMin = scrollHint.rectTransform.anchorMax = new Vector2(.5f, 0f);
        scrollHint.rectTransform.sizeDelta = new Vector2(600f, 28f);
        scrollHint.rectTransform.anchoredPosition = new Vector2(0f, 127f);
        ContinueButton = StageUITheme.Button("Continue Challenge 4", card, "Continue", Continue);
        RectTransform button = (RectTransform)ContinueButton.transform;
        button.anchorMin = button.anchorMax = new Vector2(.5f, 0f);
        button.pivot = new Vector2(.5f, 0f);
        button.sizeDelta = new Vector2(280f, 84f);
        button.anchoredPosition = new Vector2(0f, 28f);
        overlay.SetActive(false);
        FitLayout();
    }

    public void ShowIntroduction(Action continuation)
    {
        if (IsIntroductionOpen || overlay == null) return;
        input = GameInput.Instance;
        player = Player.LocalInstance;
        inputWasBlocked = input != null && input.GameplayInputBlocked;
        playerWasEnabled = player != null && player.enabled;
        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState; cursorWasVisible = Cursor.visible;
        pausedCameraInputs.Clear();
        foreach (var cameraInput in FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None)) Pause(cameraInput);
        foreach (var cameraInput in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None)) Pause(cameraInput);
        foreach (var cameraInput in FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None)) Pause(cameraInput);
        if (input != null) input.SetGameplayInputBlocked(true);
        if (player != null) player.ToggleControl(false);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        IsIntroductionOpen = true; onContinue = continuation;
        FitLayout(); overlay.SetActive(true); overlay.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1f;
    }

    private void Pause(Behaviour behaviour)
    {
        if (!behaviour.enabled) return;
        pausedCameraInputs.Add(behaviour); behaviour.enabled = false;
    }

    public void Continue()
    {
        if (!IsIntroductionOpen) return;
        Action continuation = onContinue;
        CancelIntroduction();
        continuation?.Invoke();
    }

    public void CancelIntroduction()
    {
        onContinue = null;
        if (!IsIntroductionOpen) return;
        IsIntroductionOpen = false;
        if (overlay != null) overlay.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (input != null) input.SetGameplayInputBlocked(inputWasBlocked);
        if (player != null) player.enabled = playerWasEnabled;
        foreach (Behaviour behaviour in pausedCameraInputs) if (behaviour != null) behaviour.enabled = true;
        pausedCameraInputs.Clear();
        Cursor.lockState = previousCursorLock; Cursor.visible = cursorWasVisible;
    }

    public void ShowDeduction(float actualSeconds)
    {
        if (timer == null || actualSeconds <= 0f) return;
        // Newest first, with separate rows and independent unscaled fades.
        // Submit's debounce ensures each of the three rows has time to be read.
        if (deductions.Count == deductionLabels.Length) deductions.RemoveAt(deductions.Count - 1);
        deductions.Insert(0, new Deduction { amount = actualSeconds, shownAt = Time.unscaledTime });
        RefreshDeductions();
    }

    public void SetDeductionArea(RectTransform area)
    {
        if (area == null || deductionsRoot == null) return;
        deductionArea = area;
        deductionsRoot.SetParent(area, false);
        StageUITheme.Stretch(deductionsRoot, Vector2.zero, Vector2.one);
        for (int i = 0; i < deductionLabels.Length; i++)
        {
            RectTransform row = (RectTransform)deductionGroups[i].transform;
            row.anchorMin = new Vector2(i / 3f, 1f); row.anchorMax = new Vector2((i + 1) / 3f, 1f);
            row.pivot = new Vector2(.5f, 1f);
            row.sizeDelta = new Vector2(-6f, 24f);
            row.anchoredPosition = Vector2.zero;
            deductionLabels[i].fontSizeMin = 20f; deductionLabels[i].fontSizeMax = 22f;
        }
    }

    public void ClearDeductions() { deductions.Clear(); RefreshDeductions(); }

    private void Update()
    {
        deductions.RemoveAll(d => Time.unscaledTime - d.shownAt >= 1.8f);
        RefreshDeductions();
    }

    private void RefreshDeductions()
    {
        for (int i = 0; i < deductionLabels.Length; i++)
        {
            if (deductionLabels[i] == null) continue;
            deductionGroups[i].gameObject.SetActive(i < deductions.Count);
            if (i >= deductions.Count) continue;
            deductionLabels[i].text = "−" + deductions[i].amount.ToString("0.##", CultureInfo.InvariantCulture) + "s";
            float age = Time.unscaledTime - deductions[i].shownAt;
            deductionGroups[i].alpha = 1f - Mathf.Clamp01((age - 1.1f) / .7f);
        }
    }

    private void LateUpdate()
    {
        FitLayout();
        if (safeArea == null || lastScale == safeArea.lossyScale) return;
        lastScale = safeArea.lossyScale;
        foreach (TMP_Text label in overlay.GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate(true);
        foreach (TMP_Text label in deductionLabels) if (label != null) label.ForceMeshUpdate(true);
    }

    private void FitLayout()
    {
        if (card != null && safeArea != null)
            card.sizeDelta = new Vector2(Mathf.Min(1140f, safeArea.rect.width - 48f), Mathf.Min(920f, safeArea.rect.height - 48f));
        if (deductionsRoot == null || timer == null || deductionArea != null) return;
        RectTransform target = timer.rectTransform;
        deductionsRoot.anchorMin = target.anchorMin; deductionsRoot.anchorMax = target.anchorMax;
        deductionsRoot.sizeDelta = new Vector2(target.rect.width, 150f);
        deductionsRoot.anchoredPosition = target.anchoredPosition + Vector2.down * (target.rect.height * target.pivot.y + 8f);
    }

    private void OnDisable() { CancelIntroduction(); ClearDeductions(); }
    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay);
        if (deductionsRoot != null) Destroy(deductionsRoot.gameObject);
    }
}
