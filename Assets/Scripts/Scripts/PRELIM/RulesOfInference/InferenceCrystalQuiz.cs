using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Inference
{
    /// <summary>One modal shared by both pickup inputs and the isolated tutorial practice.</summary>
    [DisallowMultipleComponent]
    public sealed class InferenceCrystalQuiz : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public bool IsPractice { get; private set; }
        public bool Answered { get; private set; }
        public InferencePickupQuestions.Draw Current { get; private set; }
        public Button[] AnswerButtons { get; private set; }
        public Button CollectButton { get; private set; }
        public Button BackButton { get; private set; }
        public TMP_Text FeedbackLabel { get; private set; }

        InferencePickupQuestions.Deck deck;
        RectTransform root, safe, card;
        TMP_Text prompt, title;
        Player player;
        GameObject gameplayHud;
        GameInput input;
        bool previousControl, previousBlocked, previousCursorVisible, previousHudActive;
        CursorLockMode previousCursorLock;
        readonly List<Behaviour> cameraInputs = new List<Behaviour>();
        Action collect, cancel;
        Canvas practiceSkipCanvas;

        public bool Open(Action onCollect, bool practice = false, Button sharedSkip = null, Action onCancel = null)
        {
            if (IsOpen || onCollect == null || Player.LocalInstance == null || GameInput.Instance == null) return false;
            if (!practice && (StageJourneyUI.Instance == null || StageJourneyUI.Instance.InferenceEnded)) return false;
            if (root == null) BuildUI();
            if (deck == null) deck = new InferencePickupQuestions.Deck(Guid.NewGuid().GetHashCode());
            // Practice gets its own deck, so it cannot consume or reveal the real pickup sequence.
            Current = practice ? new InferencePickupQuestions.Deck(Guid.NewGuid().GetHashCode()).Next() : deck.Next();
            IsPractice = practice; Answered = false; collect = onCollect; cancel = onCancel;
            player = Player.LocalInstance; input = GameInput.Instance;
            previousControl = player.enabled; previousBlocked = input.GameplayInputBlocked;
            previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible;
            IsOpen = true;
            StageJourneyUI.Instance?.SetInferenceQuizOpen(true);
            input.SetGameplayInputBlocked(true); player.ToggleControl(false);
            MobileInputUI.FindJoystick()?.ResetJoystick();
            gameplayHud = GameObject.Find("Canvas 1")?.transform.Find("SafeArea/Gameplay_Interface")?.gameObject;
            previousHudActive = gameplayHud != null && gameplayHud.activeSelf;
            if (gameplayHud != null) gameplayHud.SetActive(false);
            foreach (var control in FindObjectsByType<Unity.Cinemachine.CinemachineInputAxisController>(FindObjectsSortMode.None))
                if (control.enabled) { cameraInputs.Add(control); control.enabled = false; }
            foreach (var control in FindObjectsByType<CinemachinePinchZoom>(FindObjectsSortMode.None))
                if (control.enabled) { cameraInputs.Add(control); control.enabled = false; }
            foreach (var control in FindObjectsByType<ThirdPersonCameraController>(FindObjectsSortMode.None))
                if (control.enabled) { cameraInputs.Add(control); control.enabled = false; }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            PresentQuestion();
            root.gameObject.SetActive(true);
            // Reuse the single tutorial skip control above the modal, without making a duplicate.
            if (practice && sharedSkip != null)
            {
                practiceSkipCanvas = sharedSkip.GetComponent<Canvas>();
                if (practiceSkipCanvas == null) practiceSkipCanvas = sharedSkip.gameObject.AddComponent<Canvas>();
                practiceSkipCanvas.overrideSorting = true; practiceSkipCanvas.sortingOrder = 310;
                if (sharedSkip.GetComponent<GraphicRaycaster>() == null) sharedSkip.gameObject.AddComponent<GraphicRaycaster>();
            }
            return true;
        }

        void PresentQuestion()
        {
            Answered = false;
            title.text = IsPractice ? "PRACTICE CRYSTAL QUIZ" : "RULE CRYSTAL PICKUP QUIZ";
            prompt.text = Current.Question.Prompt;
            FeedbackLabel.text = "";
            FeedbackLabel.color = StageUITheme.Ink;
            for (int i = 0; i < 4; i++)
            {
                AnswerButtons[i].interactable = true;
                AnswerButtons[i].GetComponentInChildren<TMP_Text>().text = (char)('A' + i) + ".  " + Current.Question.Choices[Current.ChoiceOrder[i]];
            }
            CollectButton.gameObject.SetActive(false);
        }

        public void Answer(int choice)
        {
            if (!IsOpen || Answered || choice < 0 || choice >= 4) return;
            Answered = true; // Latch before sound, penalty or any other callback.
            bool correct = choice == Current.CorrectChoice;
            foreach (var button in AnswerButtons) button.interactable = false;
            FeedbackLabel.text = (correct ? "Correct!" : "Incorrect.") + "\n" + Current.Question.Explanation +
                (IsPractice ? "\nPractice only: no time is deducted and no gameplay crystal is awarded."
                    : correct ? "\nYou may collect this Rule Crystal." : "\nYou may still collect this Rule Crystal.");
            FeedbackLabel.color = correct ? new Color(.25f, .95f, .35f) : new Color(1f, .45f, .4f);
            WorldStageAudio.PlayAnswerFeedback(correct);
            if (!correct && !IsPractice) StageJourneyUI.Instance?.DeductInferenceSeconds(10);
            CollectButton.GetComponentInChildren<TMP_Text>().text = IsPractice ? "Continue" : "Collect Rule Crystal";
            CollectButton.gameObject.SetActive(true);
        }

        public void CollectAnsweredCrystal()
        {
            if (!IsOpen || !Answered) return;
            var action = collect;
            bool canCollect = IsPractice || (StageJourneyUI.Instance != null && !StageJourneyUI.Instance.InferenceEnded);
            Close();
            if (canCollect) action?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; collect = null; cancel = null;
            if (root != null) root.gameObject.SetActive(false);
            // Reuse these components until the tutorial destroys its shared button.
            // Deferred Destroy followed by a same-frame reopen can otherwise add a
            // duplicate Canvas or remove the new quiz's Skip rendering at frame end.
            if (practiceSkipCanvas != null) { practiceSkipCanvas.overrideSorting = false; practiceSkipCanvas.sortingOrder = 0; }
            practiceSkipCanvas = null;
            bool ended = StageJourneyUI.Instance != null && StageJourneyUI.Instance.InferenceEnded;
            if (!ended)
            {
                if (input != null) input.SetGameplayInputBlocked(previousBlocked);
                if (player != null) player.ToggleControl(previousControl);
                if (gameplayHud != null) gameplayHud.SetActive(previousHudActive);
            }
            foreach (var control in cameraInputs) if (control != null) control.enabled = true;
            cameraInputs.Clear();
            Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
            StageJourneyUI.Instance?.SetInferenceQuizOpen(false);
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        void Back()
        {
            var action = cancel;
            Close();
            action?.Invoke();
        }

        void BuildUI()
        {
            root = StageUITheme.Rect("InferenceCrystalQuizCanvas", null);
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
            var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var shade = StageUITheme.Rect("Input blocker", root); StageUITheme.Stretch(shade, Vector2.zero, Vector2.one);
            shade.gameObject.AddComponent<Image>().color = new Color(0, .025f, .035f, .55f);
            safe = StageUITheme.Rect("SafeArea", root); StageUITheme.Stretch(safe, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<Crystal.SafeArea>();
            card = StageUITheme.Rect("BlankPanel", safe); card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, 1);
            StageUITheme.Surface(card.gameObject.AddComponent<Image>());
            title = Label("Quiz title", "", new Vector2(.07f, .86f), new Vector2(.93f, .94f), 38);
            title.color = StageUITheme.Gold;
            prompt = Label("Question", "", new Vector2(.08f, .67f), new Vector2(.92f, .85f), 34);
            prompt.font = StageUITheme.ReadingFont;
            AnswerButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                int index = i; int col = i % 2, row = i / 2;
                AnswerButtons[i] = StageUITheme.Button("Answer " + (char)('A' + i), card, "", () => Answer(index));
                StageUITheme.Stretch((RectTransform)AnswerButtons[i].transform, new Vector2(.07f + col * .44f, .53f - row * .14f), new Vector2(.49f + col * .44f, .65f - row * .14f));
                var text = AnswerButtons[i].GetComponentInChildren<TMP_Text>();
                text.enableAutoSizing = true; text.fontSizeMin = 24; text.fontSizeMax = 31;
                text.textWrappingMode = TextWrappingModes.Normal;
            }
            FeedbackLabel = Label("Quiz feedback", "", new Vector2(.09f, .20f), new Vector2(.91f, .38f), 29);
            FeedbackLabel.font = StageUITheme.ReadingFont;
            BackButton = StageUITheme.Button("Quiz Back", card, "Back", Back);
            StageUITheme.Stretch((RectTransform)BackButton.transform, new Vector2(.08f, .075f), new Vector2(.35f, .175f));
            CollectButton = StageUITheme.Button("Quiz Collect", card, "Collect Rule Crystal", CollectAnsweredCrystal);
            StageUITheme.Stretch((RectTransform)CollectButton.transform, new Vector2(.40f, .075f), new Vector2(.92f, .175f));
            root.gameObject.SetActive(false);
        }

        TMP_Text Label(string name, string text, Vector2 min, Vector2 max, float size)
        {
            var label = StageUITheme.Text(name, card, text, size, true);
            StageUITheme.Stretch(label.rectTransform, min, max);
            label.enableAutoSizing = true; label.fontSizeMin = 24; label.fontSizeMax = size;
            label.textWrappingMode = TextWrappingModes.Normal; label.raycastTarget = false;
            return label;
        }
        void LateUpdate()
        {
            if (!IsOpen || safe == null) return;
            // Reserve the upper HUD/penalty area and lower mobile controls in landscape.
            float height = Mathf.Min(740, safe.rect.height - 255);
            card.sizeDelta = new Vector2(Mathf.Min(1100, safe.rect.width - 60), height);
            card.anchoredPosition = new Vector2(0, -235);
        }
        void OnDisable() { Close(); }
        void OnDestroy() { Close(); if (root != null) Destroy(root.gameObject); }
    }
}
