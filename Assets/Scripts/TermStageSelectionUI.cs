using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-authored term cards and topic lists, backed by unlockedStage.</summary>
public class TermStageSelectionUI : MonoBehaviour
{
    [Serializable]
    public class TermCard
    {
        public string title;
        public int firstStage;
        public int lastStage;
        public Button button;
        public TMP_Text status;
        public GameObject topics;
    }

    [Serializable]
    public class StageCard
    {
        public int stageNumber;
        [Tooltip("Exact scene name. Leave empty until the topic is ready; enable it in Build Settings.")]
        public string sceneName;
        public Button button;
        public TMP_Text status;
    }

    [SerializeField] private MainMenuManager menu;
    [SerializeField] private RectTransform board;
    [SerializeField] private GameObject termsView;
    [SerializeField] private GameObject topicsView;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private TMP_Text progress;
    [SerializeField] private Button backButton;
    [SerializeField] private TermCard[] terms;
    [SerializeField] private StageCard[] stages;

    private int selectedTerm = -1;
    private int highestUnlockedStage = 1;
    private bool loadingStage;
    private static readonly Color MutedInk = new Color(0.38f, 0.30f, 0.22f);
    private static readonly Color ReadyInk = new Color(0.12f, 0.32f, 0.20f);

    private void Awake()
    {
        for (int i = 0; i < terms.Length; i++)
        {
            int index = i;
            terms[i].button.onClick.AddListener(() => ShowTerm(index));
        }
        foreach (StageCard stage in stages)
        {
            int number = stage.stageNumber;
            stage.button.onClick.AddListener(() => menu.SelectStage(number));
        }
        backButton.onClick.AddListener(Back);
    }

    private void OnEnable() => FitBoard();
    private void OnRectTransformDimensionsChange() => FitBoard();

    private void FitBoard()
    {
        if (board == null || !(transform is RectTransform area)) return;
        float scale = Mathf.Min(1f, (area.rect.width - 48f) / 1100f,
            (area.rect.height - 32f) / 980f);
        board.localScale = Vector3.one * Mathf.Max(0.1f, scale);
    }

    public void ShowTerms()
    {
        selectedTerm = -1;
        termsView.SetActive(true);
        topicsView.SetActive(false);
        if (heading != null) heading.text = "CHOOSE YOUR TERM";
        if (subtitle != null) subtitle.text = "Your journey through Logic Legends";
    }

    public void ShowTerm(int index)
    {
        if (loadingStage || index < 0 || index >= terms.Length) return;
        selectedTerm = index;
        termsView.SetActive(false);
        topicsView.SetActive(true);
        for (int i = 0; i < terms.Length; i++) terms[i].topics.SetActive(i == index);
        if (heading != null) heading.text = terms[index].title;
        if (subtitle != null) subtitle.text = "Complete each stage to unlock the next";
    }

    public void Back()
    {
        if (loadingStage) return;
        if (selectedTerm >= 0) ShowTerms();
        else menu.ShowPlayMenu();
    }

    public string GetSceneName(int stageNumber)
    {
        foreach (StageCard stage in stages)
            if (stage.stageNumber == stageNumber) return stage.sceneName;
        return string.Empty;
    }

    public void Refresh(int unlocked, bool checking, bool loading, string message)
    {
        highestUnlockedStage = Mathf.Clamp(unlocked, 1, StageSelectionState.LastStage);
        loadingStage = loading;
        if (progress != null) progress.text = loading ? "Opening stage..." : message;
        backButton.interactable = !loading;

        foreach (TermCard term in terms)
        {
            term.button.interactable = !loading;
            if (term.status == null) continue;
            if (checking) term.status.text = "Checking progress...";
            else if (highestUnlockedStage < term.firstStage)
                term.status.text = "Finish Stage " + (term.firstStage - 1) + " First";
            else
            {
                int completed = Mathf.Clamp(highestUnlockedStage - term.firstStage,
                    0, term.lastStage - term.firstStage + 1);
                term.status.text = completed == term.lastStage - term.firstStage + 1
                    ? "Completed - revisit your stages"
                    : completed + " / " + (term.lastStage - term.firstStage + 1) + " stages completed";
            }
            term.status.color = highestUnlockedStage >= term.firstStage ? ReadyInk : MutedInk;
        }

        foreach (StageCard stage in stages)
        {
            bool unlockedStage = stage.stageNumber <= highestUnlockedStage;
            bool ready = !string.IsNullOrWhiteSpace(stage.sceneName) &&
                Application.CanStreamedLevelBeLoaded(stage.sceneName);
            stage.button.interactable = !checking && !loading && unlockedStage && ready;
            if (stage.status == null) continue;
            if (checking) stage.status.text = "Checking progress...";
            else if (!unlockedStage)
                stage.status.text = "LOCKED - Finish Stage " + (stage.stageNumber - 1) + " First";
            else if (!ready) stage.status.text = "LOCKED";
            else stage.status.text = stage.stageNumber < highestUnlockedStage
                ? "COMPLETED - Replay" : "READY - Start stage";
            stage.status.color = unlockedStage && ready ? ReadyInk : MutedInk;
        }
    }
}
