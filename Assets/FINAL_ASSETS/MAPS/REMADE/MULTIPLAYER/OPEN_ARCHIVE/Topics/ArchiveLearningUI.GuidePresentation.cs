using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Guide layout only. Reads the existing topic, hint and cooperation owners.</summary>
    public sealed partial class ArchiveLearningUI
    {
        readonly Button[] guideTabs = new Button[3];
        TMP_Text guideStatus, guideReadingCue;
        int guideSection;
        bool guideContentDirty = true;
        Vector2 guideReadingSize;
        readonly RectTransform[] guideMovementControls = new RectTransform[3];
        readonly Vector3[] guideControlCorners = new Vector3[4];
        float guideControlLookupAt;
        ArchiveSignalSectorGraphic learningMedallion;
        RectTransform guideInventorySlots;
        float guideInventoryLookupAt;

        void StyleGuideEntry()
        {
            entryText.gameObject.SetActive(false);
            entry.GetComponent<Image>().enabled = false;
            var face = Rect("Learning_Medallion", entry.transform, Vector2.zero, Vector2.one);
            learningMedallion = face.gameObject.AddComponent<ArchiveSignalSectorGraphic>();
            entry.targetGraphic = learningMedallion;
            var symbol = Rect("Learn_Book_Icon", entry.transform, new Vector2(.22f, .22f), new Vector2(.78f, .78f));
            var icon = symbol.gameObject.AddComponent<ArchiveSignalIconGraphic>();
            icon.icon = ArchiveSignalIcon.Book; icon.color = new Color(1, .96f, .82f); icon.raycastTarget = false;
        }

        void FitGuideEntryBelowInventory()
        {
            if (guideInventorySlots == null && Time.unscaledTime >= guideInventoryLookupAt)
            {
                guideInventoryLookupAt = Time.unscaledTime + 1;
                var inventory = ArchiveRewardView.Current;
                if (inventory != null)
                    foreach (var rect in inventory.GetComponentsInChildren<RectTransform>(true))
                        if (rect.name == "Collected_Item_Slots") { guideInventorySlots = rect; break; }
            }
            if (guideInventorySlots == null) return;
            guideInventorySlots.GetWorldCorners(guideControlCorners);
            float inventoryBottom = safe.InverseTransformPoint(guideControlCorners[0]).y - safe.rect.yMin;
            var button = (RectTransform)entry.transform;
            float y = Mathf.Clamp(inventoryBottom - button.rect.height - 8, 4, 24);
            if (Mathf.Abs(button.anchoredPosition.y - y) > .1f) button.anchoredPosition = new Vector2(0, y);
        }

        void BuildGuidePresentation()
        {
            guide = Panel("Learning_Guide", new Vector2(.235f, .42f), new Vector2(.795f, .95f));
            guideTitle = Label("Guide_Title", guide, "", 34, true);
            Stretch(guideTitle.rectTransform, new Vector2(.045f, .837f), new Vector2(.79f, .927f));
            guideTitle.alignment = TextAlignmentOptions.MidlineLeft;
            guideTitle.enableAutoSizing = true; guideTitle.fontSizeMin = 27; guideTitle.fontSizeMax = 34;
            var close = Button("Close_Learning_Guide", guide, "Close", DismissGuide);
            Stretch((RectTransform)close.transform, new Vector2(.81f, .86f), new Vector2(.955f, .958f));
            StyleGuideButton(close, "Close", 24);

            var previous = Button("Previous_Topic_Guide", guide, "<", () => OpenGuide((guideTopic + 9) % 10));
            Stretch((RectTransform)previous.transform, new Vector2(.045f, .728f), new Vector2(.105f, .83f));
            StyleGuideButton(previous, "<", 28);
            var next = Button("Next_Topic_Guide", guide, ">", () => OpenGuide((guideTopic + 1) % 10));
            Stretch((RectTransform)next.transform, new Vector2(.895f, .728f), new Vector2(.955f, .83f));
            StyleGuideButton(next, ">", 28);
            string[] captions = { "HOW TO PLAY", "HINTS", "TEAM PLAY" };
            for (int i = 0; i < guideTabs.Length; i++)
            {
                int section = i;
                var tab = Button("Guide_Tab_" + i, guide, captions[i], () => SelectGuideSection(section));
                guideTabs[i] = tab;
                float left = .12f + i * .258f;
                Stretch((RectTransform)tab.transform, new Vector2(left, .728f), new Vector2(left + .244f, .83f));
                StyleGuideButton(tab, captions[i], 23);
                var outline = tab.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(2, -2); outline.effectColor = Color.clear;
            }

            guideStatus = Label("Guide_Context", guide, "", 20, false);
            Stretch(guideStatus.rectTransform, new Vector2(.052f, .66f), new Vector2(.947f, .713f));
            guideStatus.color = Teal; guideStatus.alignment = TextAlignmentOptions.MidlineLeft;
            guideScroll = Scroll("Guide_Reading", guide, new Vector2(.045f, .245f), new Vector2(.955f, .65f), out var content);
            guideScroll.GetComponent<Image>().color = new Color(.22f, .13f, .05f, .035f);
            content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(14, 16, 12, 22);
            guideBody = Paragraph("Guide_Text", content, "", 26);
            guideBody.richText = true; guideBody.lineSpacing = 5;
            guideReadingCue = Label("Guide_Scroll_Cue", guide, "", 18, false);
            Stretch(guideReadingCue.rectTransform, new Vector2(.052f, .188f), new Vector2(.947f, .235f));
            guideReadingCue.color = Teal; guideReadingCue.alignment = TextAlignmentOptions.MidlineLeft;

            var line = Rect("Guide_Footer_Divider", guide, new Vector2(.05f, .18f), new Vector2(.95f, .183f)).gameObject.AddComponent<Image>();
            line.color = new Color(.55f, .39f, .16f, .32f); line.raycastTarget = false;
            var auto = Button("Toggle_First_Visit_Tips", guide, "", () =>
            {
                PlayerPrefs.SetInt(PreferencePrefix + "AutoTips", AutoTips ? 0 : 1);
                PlayerPrefs.Save(); UpdateAutoCaption();
            });
            Stretch((RectTransform)auto.transform, new Vector2(.335f, .055f), new Vector2(.64f, .165f));
            StyleGuideButton(auto, "", 23); autoCaption = auto.GetComponentInChildren<TMP_Text>();
            hintButton = Button("Next_Learning_Hint", guide, "", () =>
            {
                SelectGuideSection(1);
                if (session != null) session.RequestLearningHint(guideTopic);
            });
            Stretch((RectTransform)hintButton.transform, new Vector2(.655f, .055f), new Vector2(.955f, .165f));
            StyleGuideButton(hintButton, "", 23); hintCaption = hintButton.GetComponentInChildren<TMP_Text>();
            UpdateAutoCaption(); PaintGuideTabs(); guide.gameObject.SetActive(false);
        }

        void StyleGuideButton(Button button, string caption, int size)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            label.text = caption; label.font = HeadingFont; label.fontSize = size;
            label.enableAutoSizing = true; label.fontSizeMin = size - 3; label.fontSizeMax = size;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(label.rectTransform, new Vector2(.07f, .12f), new Vector2(.93f, .88f));
            var colors = button.colors;
            colors.highlightedColor = new Color(.87f, 1, .96f);
            colors.pressedColor = new Color(.74f, .90f, .85f);
            colors.disabledColor = new Color(.74f, .72f, .64f, .76f);
            button.colors = colors;
        }

        void SelectGuideSection(int section)
        {
            guideSection = Mathf.Clamp(section, 0, 2); guideContentDirty = true;
            PaintGuideTabs(); RefreshGuide();
            guideScroll.StopMovement(); guideScroll.verticalNormalizedPosition = 1;
        }

        void PaintGuideTabs()
        {
            for (int i = 0; i < guideTabs.Length; i++)
            {
                var tab = guideTabs[i]; if (tab == null) continue;
                bool active = guideSection == i;
                tab.GetComponent<Image>().color = active ? new Color(.78f, 1, .93f) : Color.white;
                tab.GetComponentInChildren<TMP_Text>().color = active ? new Color(1, .86f, .45f) : StageUITheme.Ink;
                var border = tab.GetComponent<Outline>();
                if (border != null) border.effectColor = active ? new Color(.87f, .64f, .24f, .9f) : Color.clear;
            }
        }

        static string GuideHeading(string heading) => "<b><color=#155C60>" + heading + "</color></b>\n";

        void FitGuideAboveControls()
        {
            if ((guideMovementControls[0] == null || guideMovementControls[1] == null || guideMovementControls[2] == null) && Time.unscaledTime >= guideControlLookupAt)
            {
                guideControlLookupAt = Time.unscaledTime + 1;
                foreach (var candidate in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate.name == "Joystick") guideMovementControls[0] = candidate;
                    else if (candidate.name == "JumpButton") guideMovementControls[1] = candidate;
                    else if (candidate.name == "InteractButton") guideMovementControls[2] = candidate;
                }
            }
            float bottom = .42f;
            var area = safe.rect;
            if (area.width <= 0 || area.height <= 0) return;
            foreach (var control in guideMovementControls)
            {
                if (control == null || !control.gameObject.activeInHierarchy) continue;
                control.GetWorldCorners(guideControlCorners);
                var low = safe.InverseTransformPoint(guideControlCorners[0]);
                var high = safe.InverseTransformPoint(guideControlCorners[2]);
                float left = (low.x - area.xMin) / area.width, right = (high.x - area.xMin) / area.width;
                if (right > .235f && left < .795f)
                    bottom = Mathf.Max(bottom, (high.y - area.yMin) / area.height + .018f);
            }
            // Use the actual mobile hit rectangles, including wider phone aspect ratios.
            bottom = Mathf.Clamp(bottom, .42f, .56f);
            if (Mathf.Abs(guide.anchorMin.y - bottom) > .0005f)
            { guide.anchorMin = new Vector2(.235f, bottom); guideContentDirty = true; }
        }

        void RefreshGuidePresentation()
        {
            FitGuideAboveControls();
            var station = session.State.stations[guideTopic];
            int level = session.HintLevel(guideTopic, session.LocalActor);
            bool canHint = session.CanRequestHint(guideTopic);
            hintButton.interactable = canHint;
            hintCaption.color = canHint ? StageUITheme.Ink : Ink;
            hintCaption.text = level >= 3 ? "All 3 hints shown" : station.phase == 2 ? "Topic complete" :
                station.phase == 0 ? "Read Book for hints" : canHint ? "Next hint " + (level + 1) + " / 3" : "Return for hints";
            var readingSize = guideScroll.viewport.rect.size;
            bool resized = (guideReadingSize - readingSize).sqrMagnitude > 1;
            if (!guideContentDirty && !resized && shownRevision == session.State.revision && shownHintLevel == level && shownReset == station.testResetSerial) return;
            bool newHint = shownRevision >= 0 && level > shownHintLevel;
            shownRevision = session.State.revision; shownHintLevel = level; shownReset = station.testResetSerial;
            guideContentDirty = false;
            guideReadingSize = readingSize;
            guideTitle.text = "T" + (guideTopic + 1) + "  /  " + ArchiveTopicCatalog.Names[guideTopic];
            guideStatus.text = guideSection == 0 ? "Review the lesson, controls and worked example." :
                guideSection == 1 ? "Your hints stay with this topic. Hints are free." : "Share the work and help restore this topic's crystal.";
            if(station.timerOvertime&&station.phase!=2)guideStatus.text="OVERTIME · Keep solving. Hints are free; shared match time still runs.";
            string body;
            if (guideSection == 0)
                body = ArchiveLearningFeedback.Guide(guideTopic, session.Question(guideTopic)).Replace("<b>", "<b><color=#155C60>").Replace("</b>", "</color></b>");
            else if (guideSection == 2)
                body = session.CooperationGuide(guideTopic)
                    .Replace("PACE:", "Match pace:")
                    .Replace("TEAM ROLES · rotate each problem", GuideHeading("TEAM ROLES") + "Rotate roles each problem.")
                    .Replace("YOUR PARTICIPATION\n", GuideHeading("YOUR PARTICIPATION"))
                    .Replace("TOPIC CRYSTAL\n", GuideHeading("RESTORE THE CRYSTAL"));
            else
            {
                body = GuideHeading("YOUR HINTS  /  " + level + " OF 3");
                if (station.phase == 1)
                {
                    if (level == 0) body += ArchiveLearningFeedback.Hint(guideTopic, session.Question(guideTopic), station, 0) + "\n\nTap Next hint for a clue about the current problem.";
                    for (int i = level; i >= 1; i--) body += session.LearningHintText(guideTopic, session.LocalActor, i) + "\n\n";
                    if (!session.Near(session.LocalActor, guideTopic, Vector3.zero, 14)) body += "\nReturn to this topic's arena to request another hint.";
                    if (!string.IsNullOrWhiteSpace(station.feedback)) body += "\n\n" + GuideHeading("TEAM FEEDBACK") + station.feedback;
                }
                else body += station.phase == 2 ? "Your team completed this topic. You can still review its lesson and worked example in How to Play." :
                    "Read this topic's Book to activate its challenge and unlock hints. You can review How to Play before starting.";
                var barrier = session.State.barrier;
                if (barrier.actor == session.LocalActor && barrier.outcome != 0)
                    body += "\n\n" + GuideHeading("LAST BARRIER REVIEW") + session.BarrierProblem().prompt + "\n\n" + barrier.feedback;
            }
            float position = newHint && guideSection == 1 ? 1 : guideScroll.verticalNormalizedPosition;
            guideBody.text = body;
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(guideScroll.content);
            guideScroll.verticalNormalizedPosition = position;
            guideReadingCue.text = "TOPIC " + (guideTopic + 1) + " OF 10" +
                (guideScroll.content.rect.height > guideScroll.viewport.rect.height + 1 ? "    /    Swipe up or down to read more" : "    /    All content shown");
        }
    }
}
