using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local learning presentation. Owns no progress, outcome, movement token or countdown.</summary>
    [DefaultExecutionOrder(175), DisallowMultipleComponent]
    public sealed class ArchiveLearningUI : MonoBehaviour
    {
        const string PreferencePrefix = "OpenArchive.Learning.v1.";
        public ArchiveTopicSession session;
        public ArchiveTopicUI topicUI;
        public TMP_FontAsset readingFont;
        public static ArchiveLearningUI Current { get; private set; }
        RectTransform safe, guide, prison, prisonContent, choiceContent;
        TMP_Text entryText, guideTitle, guideBody, hintCaption, autoCaption, prisonTitle, prisonClock, prisonHint, prisonFeedback, selectedDetail;
        Button entry, hintButton, confirm, barrierHint;
        Button[] choices = Array.Empty<Button>();
        ScrollRect guideScroll, prisonScroll;
        KhezuBarrierStatusUI encounterStatus;
        int guideTopic, shownRevision = -1, shownHintLevel = -1, shownReset = -1;
        int displayedCapture = -1, displayedHunt = -1, selected = -1, pendingRevision = -1, reviewedCapture = -1, displayedBarrierRevision = -1;
        bool guideOpen, pending;
        float retryAt;
        public bool GuideVisible => guide != null && guide.gameObject.activeSelf;
        public bool BarrierVisible => prison != null && prison.gameObject.activeSelf;
        public int GuideTopic => guideTopic;
        public bool AutoTips => PlayerPrefs.GetInt(PreferencePrefix + "AutoTips", 1) != 0;
        TMP_FontAsset ReadingFont => readingFont != null ? readingFont : topicUI != null && topicUI.textFont != null ? topicUI.textFont : StageUITheme.ReadingFont;
        TMP_FontAsset HeadingFont => topicUI != null && topicUI.headingFont != null ? topicUI.headingFont : StageUITheme.HeadingFont;
        static readonly Color Ink = new Color(.16f, .10f, .06f), Teal = new Color(.035f, .29f, .30f);
        void Awake()
        {
            Current = this;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 145;
            var scale = gameObject.AddComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1920, 1080); scale.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            safe = Rect("Safe_Area", transform, Vector2.zero, Vector2.one);
            entry = Button("Learn_And_Hints", safe, "LEARN / HINTS", () => OpenGuide(NearestTopic()));
            var entryRect = (RectTransform)entry.transform; entryRect.anchorMin = entryRect.anchorMax = new Vector2(.5f, 0);
            entryRect.pivot = new Vector2(.5f, 0); entryRect.anchoredPosition = new Vector2(0, 24); entryRect.sizeDelta = new Vector2(290, 66);
            entryText = entry.GetComponentInChildren<TMP_Text>();
            BuildGuide(); BuildBarrier();
            ArchiveReadingScrollbars.Install(transform);
        }
        void BuildGuide()
        {
            guide = Panel("Learning_Guide", new Vector2(.26f, .47f), new Vector2(.74f, .88f));
            guideTitle = Label("Guide_Title", guide, "", 30, true); Stretch(guideTitle.rectTransform, new Vector2(.045f, .85f), new Vector2(.77f, .95f));
            var close = Button("Close_Learning_Guide", guide, "Got it", DismissGuide); Stretch((RectTransform)close.transform, new Vector2(.80f, .85f), new Vector2(.96f, .96f));
            guideScroll = Scroll("Guide_Reading", guide, new Vector2(.05f, .22f), new Vector2(.95f, .82f), out var content);
            guideBody = Paragraph("Guide_Text", content, "", 26); guideBody.richText = true;
            var previous = Button("Previous_Topic_Guide", guide, "< Topic", () => OpenGuide((guideTopic + 9) % 10));
            Stretch((RectTransform)previous.transform, new Vector2(.05f, .055f), new Vector2(.22f, .17f));
            var next = Button("Next_Topic_Guide", guide, "Topic >", () => OpenGuide((guideTopic + 1) % 10));
            Stretch((RectTransform)next.transform, new Vector2(.23f, .055f), new Vector2(.40f, .17f));
            var auto = Button("Toggle_First_Visit_Tips", guide, "", () => { PlayerPrefs.SetInt(PreferencePrefix + "AutoTips", AutoTips ? 0 : 1); PlayerPrefs.Save(); UpdateAutoCaption(); });
            Stretch((RectTransform)auto.transform, new Vector2(.41f, .055f), new Vector2(.67f, .17f)); autoCaption = auto.GetComponentInChildren<TMP_Text>();
            hintButton = Button("Next_Learning_Hint", guide, "", () => { if (session != null) session.RequestLearningHint(guideTopic); });
            Stretch((RectTransform)hintButton.transform, new Vector2(.68f, .055f), new Vector2(.95f, .17f)); hintCaption = hintButton.GetComponentInChildren<TMP_Text>();
            UpdateAutoCaption(); guide.gameObject.SetActive(false);
        }
        void BuildBarrier()
        {
            prison = Panel("Real_Math_Barrier_Challenge", new Vector2(.26f, .47f), new Vector2(.74f, .90f));
            prisonTitle = Label("Barrier_Problem_Title", prison, "", 29, true); Stretch(prisonTitle.rectTransform, new Vector2(.045f, .85f), new Vector2(.79f, .96f));
            prisonClock = Label("Existing_Barrier_Clock", prison, "", 30, true); Stretch(prisonClock.rectTransform, new Vector2(.80f, .85f), new Vector2(.955f, .96f)); prisonClock.alignment = TextAlignmentOptions.Right;
            prisonScroll = Scroll("Barrier_Reading", prison, new Vector2(.05f, .23f), new Vector2(.56f, .83f), out prisonContent);
            Scroll("Barrier_Choices", prison, new Vector2(.58f, .23f), new Vector2(.95f, .83f), out choiceContent);
            var optionLayout = choiceContent.GetComponent<VerticalLayoutGroup>(); optionLayout.spacing = 8; optionLayout.padding = new RectOffset(2, 3, 4, 4);
            var footer = Rect("Barrier_Actions", prison, new Vector2(.05f, .055f), new Vector2(.95f, .20f));
            barrierHint = Button("Barrier_Next_Hint", footer, "Next hint", () => RequestBarrier(false));
            Stretch((RectTransform)barrierHint.transform, new Vector2(0, .08f), new Vector2(.31f, .92f));
            confirm = Button("Confirm_Barrier_Answer", footer, "Confirm answer", () => RequestBarrier(true));
            Stretch((RectTransform)confirm.transform, new Vector2(.58f, .08f), new Vector2(1, .92f));
            var notice = Label("One_Confirmed_Attempt", footer, "Wrong/timeout:\n−1 heart + stun", 20, false);
            Stretch(notice.rectTransform, new Vector2(.32f, .08f), new Vector2(.57f, .92f)); notice.alignment = TextAlignmentOptions.Center;
            prison.gameObject.SetActive(false);
        }
        void UpdateAutoCaption() { autoCaption.text = "First-visit tips: " + (AutoTips ? "ON" : "OFF"); }
        public void OpenGuide(int topic)
        {
            if (session == null || !session.Ready || !session.CanAct(session.LocalActor)) return;
            if (topicUI != null && topicUI.ReadingStation != null) topicUI.Close();
            guideTopic = Mathf.Clamp(topic, 0, 9); guideOpen = true; shownRevision = -1; guide.gameObject.SetActive(true);
            RefreshGuide(); guideScroll.verticalNormalizedPosition = 1;
        }
        public void DismissGuide()
        {
            if (guideOpen)
            {
                PlayerPrefs.SetInt(PreferencePrefix + "Seen." + guideTopic, 1); PlayerPrefs.Save();
                if (session != null && session.Ready && session.State.barrier.actor == session.LocalActor) reviewedCapture = session.State.barrier.captureSerial;
            }
            guideOpen = false; guide.gameObject.SetActive(false);
        }
        int NearestTopic()
        {
            if (topicUI != null && topicUI.ReadingStation != null) return topicUI.ReadingStation.topic;
            if (session == null || !session.Ready || Player.LocalInstance == null) return 0;
            int nearest = -1; float best = float.MaxValue;
            foreach (var station in session.stations)
            {
                float d = (Player.LocalInstance.transform.position - station.transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = station.topic; }
            }
            if (best < 45 * 45) return nearest;
            return session.ActiveTopic >= 0 ? session.ActiveTopic : 0;
        }
        void Update()
        {
            if (session == null) session = ArchiveTopicSession.Current;
            if (topicUI == null) topicUI = ArchiveTopicUI.Current;
            if (Screen.width > 0 && Screen.height > 0)
            {
                var a = Screen.safeArea; safe.anchorMin = new Vector2(a.xMin / Screen.width, a.yMin / Screen.height); safe.anchorMax = new Vector2(a.xMax / Screen.width, a.yMax / Screen.height);
            }
            bool ready = session != null && session.Ready && Player.LocalInstance != null && Time.timeScale > 0;
            bool captive = ready && session.IsLocalBarrierQuestion;
            prison.gameObject.SetActive(captive);
            if (session != null && session.learningEncounter != null)
            {
                var status = session.learningEncounter.GetComponent<KhezuBarrierStatusUI>();
                if (encounterStatus != status) { if (encounterStatus != null) encounterStatus.detailedChallengeVisible = false; encounterStatus = status; }
            }
            if (encounterStatus != null) encounterStatus.detailedChallengeVisible = captive;
            bool available = ready && !captive && session.CanAct(session.LocalActor);
            if (!available) guideOpen = false;
            if (guideOpen && topicUI != null && topicUI.ReadingStation != null) guideOpen = false;
            guide.gameObject.SetActive(available && guideOpen); entry.gameObject.SetActive(available && !guideOpen);
            if (captive) RefreshBarrier();
            if (!available) return;
            int topic = NearestTopic();
            bool first = AutoTips && PlayerPrefs.GetInt(PreferencePrefix + "Seen." + topic, 0) == 0 &&
                session.State.stations[topic].phase == 0 && session.Near(session.LocalActor, topic, Vector3.zero, 14);
            bool review = session.State.barrier.actor == session.LocalActor && session.State.barrier.outcome != 0 && reviewedCapture != session.State.barrier.captureSerial;
            entryText.text = first ? "FIRST VISIT · T" + (topic + 1) + "\nLearn how to play" : review ? "BARRIER REVIEW\nLearn / Hints" : "LEARN / HINTS";
            if (guideOpen) RefreshGuide();
        }
        void RefreshGuide()
        {
            var s = session.State.stations[guideTopic]; int level = session.HintLevel(guideTopic, session.LocalActor);
            hintButton.interactable = session.CanRequestHint(guideTopic); hintCaption.text = level >= 3 ? "All hints shown" : "Next hint " + (level + 1) + " / 3";
            if (shownRevision == session.State.revision && shownHintLevel == level && shownReset == s.testResetSerial) return;
            bool newHint = shownRevision >= 0 && level > shownHintLevel;
            shownRevision = session.State.revision; shownHintLevel = level; shownReset = s.testResetSerial;
            guideTitle.text = "T" + (guideTopic + 1) + " · " + ArchiveTopicCatalog.Names[guideTopic];
            string lesson = ArchiveLearningFeedback.Guide(guideTopic, session.Question(guideTopic));
            string body = "";
            if (s.phase == 1)
            {
                body += "<b>YOUR HINTS · " + level + " / 3</b>\n";
                if (level == 0) body += ArchiveLearningFeedback.Hint(guideTopic, session.Question(guideTopic), s, 0);
                for (int i = level; i >= 1; i--) body += session.LearningHintText(guideTopic,session.LocalActor,i) + "\n\n";
                if (!session.Near(session.LocalActor, guideTopic, Vector3.zero, 14)) body += "\nReturn to this topic's arena to request another hint.";
                body += "\n\n<b>TEAM FEEDBACK</b>\n" + s.feedback + "\n\n";
            }
            else body += "Hints become available after Book activation in this topic. You can read these examples anytime.\n\n";
            var b = session.State.barrier;
            if (b.actor == session.LocalActor && b.outcome != 0)
                body = "<b>LAST BARRIER REVIEW</b>\n" + session.BarrierProblem().prompt + "\n\n" + b.feedback + "\n\n" + body;
            body += "<b>COOPERATIVE PLAY</b>\n" + session.CooperationGuide(guideTopic) + "\n\n";
            float position = newHint ? 1 : guideScroll.verticalNormalizedPosition; guideBody.text = body + lesson; Canvas.ForceUpdateCanvases(); guideScroll.verticalNormalizedPosition = position;
        }
        void RefreshBarrier()
        {
            var b = session.State.barrier; var problem = session.BarrierProblem(); var hunt = session.hunting;
            if (b.captureSerial != displayedCapture || b.huntSerial != displayedHunt)
            {
                displayedCapture = b.captureSerial; displayedHunt = b.huntSerial; selected = -1; pending = false; displayedBarrierRevision = -1;
                foreach (Transform child in prisonContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                foreach (Transform child in choiceContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                Paragraph("Barrier_Instructions", prisonContent, problem.prompt, 27);
                choices = new Button[problem.options.Length];
                for (int i = 0; i < choices.Length; i++)
                {
                    int choice = i;
                    choices[i] = Button("Barrier_Choice_" + i, choiceContent, "", () => { if (!pending && b.outcome == 0) selected = choice; });
                    choices[i].gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
                    var choiceLabel = choices[i].GetComponentInChildren<TMP_Text>(); choiceLabel.enableAutoSizing = true;
                    choiceLabel.fontSizeMin = 22; choiceLabel.fontSizeMax = 24;
                }
                selectedDetail = Paragraph("Selected_Rule_Definition", prisonContent, "", 24);
                prisonHint = Paragraph("Barrier_Progressive_Hints", prisonContent, "", 24); prisonHint.color = Teal;
                prisonFeedback = Paragraph("Validated_Barrier_Feedback", prisonContent, "", 26);
                Canvas.ForceUpdateCanvases(); prisonScroll.verticalNormalizedPosition = 1;
            }
            if (pending && (b.revision != pendingRevision || Time.unscaledTime >= retryAt)) pending = false;
            Color accent = b.kind == 1 ? new Color(.61f, .24f, .015f) : new Color(.65f, .025f, .10f);
            prisonTitle.text = problem.title;
            int seconds = Mathf.CeilToInt(hunt.BarrierSecondsRemaining);
            prisonClock.text = b.outcome == 0 ? (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") : b.outcome == 1 ? "SOLVED" : "FAILED";
            prisonClock.color = prisonTitle.color = accent;
            for (int i = 0; i < choices.Length; i++)
            {
                bool picked = (b.outcome == 0 ? selected : b.selected) == i;
                choices[i].interactable = !pending && b.outcome == 0 && hunt.BarrierSecondsRemaining > 0;
                var label = choices[i].GetComponentInChildren<TMP_Text>();
                label.text = (picked ? "> " : "") + (char)('A' + i) + ". " + problem.options[i];
                label.color = picked ? new Color(1, .88f, .42f) : StageUITheme.Ink;
            }
            selectedDetail.text = selected < 0 ? "Select one option, read it, then confirm. Only you can answer your capture." : problem.OptionDetail(selected);
            if (displayedBarrierRevision != b.revision)
            {
                bool scrollToReceipt = displayedBarrierRevision >= 0;
                displayedBarrierRevision = b.revision;
                string hints = "";
                for (int level = 1; level <= b.hintLevel; level++) hints += "HINT " + level + " / 3\n" + problem.Hint(level) + "\n\n";
                prisonHint.text = b.hintLevel == 0 ? problem.Hint(0) : hints;
                prisonFeedback.text = b.feedback;
                Canvas.ForceUpdateCanvases(); if (scrollToReceipt) prisonScroll.verticalNormalizedPosition = 0;
            }
            confirm.interactable = !pending && b.outcome == 0 && selected >= 0 && hunt.BarrierSecondsRemaining > 0;
            barrierHint.interactable = !pending && b.outcome == 0 && b.hintLevel < 3 && hunt.BarrierSecondsRemaining > 0;
            barrierHint.GetComponentInChildren<TMP_Text>().text = b.hintLevel >= 3 ? "Hints shown" : "Hint " + (b.hintLevel + 1) + " / 3";
        }
        void RequestBarrier(bool answer)
        {
            if (pending || session == null) return;
            pendingRevision = session.State.barrier.revision;
            pending = answer ? session.RequestBarrierAnswer(selected) : session.RequestBarrierHint();
            retryAt = Time.unscaledTime + 2;
        }
        RectTransform Panel(string name, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, safe, min, max); var image = rect.gameObject.AddComponent<Image>();
            image.sprite = topicUI != null && topicUI.panelArt != null ? topicUI.panelArt : StageUITheme.Panel;
            image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 2.8f; return rect;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            Stretch(rect, min, max); return rect;
        }
        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        TMP_Text Label(string name, Transform parent, string text, int size, bool heading)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.one); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = heading ? HeadingFont : ReadingFont; label.text = text; label.fontSize = size; label.color = Ink;
            label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal; return label;
        }
        TMP_Text Paragraph(string name, Transform parent, string text, int size)
        {
            var label = Label(name, parent, text, size, false); label.margin = new Vector4(4, 4, 4, 8); return label;
        }
        Button Button(string name, Transform parent, string caption, UnityEngine.Events.UnityAction callback)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.one); var image = rect.gameObject.AddComponent<Image>();
            image.sprite = topicUI != null && topicUI.buttonArt != null ? topicUI.buttonArt : StageUITheme.ButtonArt;
            image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 4;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(callback);
            var label = Label(name + "_Label", rect, caption, 26, false); label.color = StageUITheme.Ink; label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, new Vector2(.045f, .08f), new Vector2(.955f, .92f));
            return button;
        }
        ScrollRect Scroll(string name, Transform parent, Vector2 min, Vector2 max, out RectTransform content)
        {
            var host = Rect(name, parent, min, max); var hit = host.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, .025f);
            var view = Rect("Viewport", host, Vector2.zero, Vector2.one); view.gameObject.AddComponent<RectMask2D>();
            content = Rect("Content", view, new Vector2(0, 1), Vector2.one); content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(8, 12, 8, 16); layout.spacing = 14;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = host.gameObject.AddComponent<ScrollRect>(); scroll.content = content; scroll.viewport = view; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36; return scroll;
        }
        void OnDisable()
        {
            if (encounterStatus != null) encounterStatus.detailedChallengeVisible = false;
            if (guide != null) guide.gameObject.SetActive(false); if (prison != null) prison.gameObject.SetActive(false);
            if (entry != null) entry.gameObject.SetActive(false);
        }
        void OnDestroy() { if (encounterStatus != null) encounterStatus.detailedChallengeVisible = false; if (Current == this) Current = null; }
    }
}
