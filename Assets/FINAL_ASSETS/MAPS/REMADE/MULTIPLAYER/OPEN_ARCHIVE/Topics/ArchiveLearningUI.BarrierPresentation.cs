using LogicLegends.OpenArchive.Hunting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    // Presentation of the existing owner-only challenge. No encounter state or timer is owned here.
    public sealed partial class ArchiveLearningUI
    {
        RectTransform barrierShade;
        ScrollRect barrierChoicesScroll;
        TMP_Text barrierSelection, barrierClockCaption;
        Image barrierTimeFill, barrierHeaderLine;
        int displayedBarrierHintLevel = -1;

        void BuildBarrierPresentation()
        {
            barrierShade = Rect("Barrier_Focus_Shade", safe, Vector2.zero, Vector2.one);
            var dim = barrierShade.gameObject.AddComponent<Image>(); dim.color = new Color(.015f, .025f, .04f, .68f);
            barrierShade.gameObject.SetActive(false);
            prison = Panel("Real_Math_Barrier_Challenge", new Vector2(.055f, .07f), new Vector2(.945f, .94f));
            prisonTitle = Label("Barrier_Problem_Title", prison, "", 38, true);
            Stretch(prisonTitle.rectTransform, new Vector2(.045f, .865f), new Vector2(.76f, .95f));
            prisonTitle.alignment = TextAlignmentOptions.MidlineLeft;
            prisonTitle.enableAutoSizing = true; prisonTitle.fontSizeMin = 27; prisonTitle.fontSizeMax = 38;
            var kind = Label("Escape_Challenge_Label", prison, "DM1  /  ESCAPE CHALLENGE", 22, true);
            Stretch(kind.rectTransform, new Vector2(.047f, .82f), new Vector2(.73f, .87f)); kind.color = Teal;
            var timer = Rect("Barrier_Timer_Badge", prison, new Vector2(.79f, .835f), new Vector2(.955f, .95f));
            var badge = timer.gameObject.AddComponent<Image>(); badge.sprite = StageUITheme.ButtonArt; badge.type = Image.Type.Sliced; badge.pixelsPerUnitMultiplier = 4;
            prisonClock = Label("Existing_Barrier_Clock", timer, "02:00", 38, true);
            Stretch(prisonClock.rectTransform, new Vector2(.08f, .28f), new Vector2(.92f, .89f)); prisonClock.alignment = TextAlignmentOptions.Center;
            barrierClockCaption = Label("Barrier_Clock_Caption", timer, "TIME TO ESCAPE", 15, true);
            Stretch(barrierClockCaption.rectTransform, new Vector2(.07f, .10f), new Vector2(.93f, .31f));
            barrierClockCaption.color = StageUITheme.Ink; barrierClockCaption.alignment = TextAlignmentOptions.Center;
            barrierHeaderLine = Rect("Guardian_Accent", prison, new Vector2(.045f, .805f), new Vector2(.955f, .81f)).gameObject.AddComponent<Image>(); barrierHeaderLine.raycastTarget = false;
            var track = Rect("Barrier_Time_Track", prison, new Vector2(.045f, .792f), new Vector2(.955f, .798f));
            track.gameObject.AddComponent<Image>().color = new Color(.15f, .10f, .05f, .15f);
            barrierTimeFill = Rect("Barrier_Time_Remaining", track, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>(); barrierTimeFill.raycastTarget = false;
            var qLabel = Label("Question_Section", prison, "READ THE QUESTION", 21, true);
            Stretch(qLabel.rectTransform, new Vector2(.05f, .735f), new Vector2(.50f, .785f)); qLabel.color = Teal;
            var readQuestion = Button("Return_To_Question", prison, "Question ↑", () => { prisonScroll.StopMovement(); prisonScroll.verticalNormalizedPosition = 1; });
            Stretch((RectTransform)readQuestion.transform, new Vector2(.35f, .742f), new Vector2(.50f, .782f));
            readQuestion.GetComponentInChildren<TMP_Text>().fontSize = 18;
            readQuestion.GetComponent<Image>().color = Color.clear;
            readQuestion.GetComponentInChildren<TMP_Text>().color = Teal;
            Stretch(qLabel.rectTransform, new Vector2(.05f, .735f), new Vector2(.34f, .785f));
            var cLabel = Label("Answer_Section", prison, "CHOOSE YOUR ANSWER", 21, true);
            Stretch(cLabel.rectTransform, new Vector2(.535f, .735f), new Vector2(.95f, .785f)); cLabel.color = Teal;
            prisonScroll = Scroll("Barrier_Reading", prison, new Vector2(.045f, .245f), new Vector2(.51f, .73f), out prisonContent);
            barrierChoicesScroll = Scroll("Barrier_Choices", prison, new Vector2(.53f, .245f), new Vector2(.955f, .73f), out choiceContent);
            var layout = choiceContent.GetComponent<VerticalLayoutGroup>(); layout.spacing = 10; layout.padding = new RectOffset(6, 9, 6, 12);
            barrierSelection = Label("Barrier_Selected_Answer", prison, "Choose an answer, then confirm.", 22, false);
            Stretch(barrierSelection.rectTransform, new Vector2(.05f, .185f), new Vector2(.95f, .235f));
            barrierSelection.color = Teal; barrierSelection.alignment = TextAlignmentOptions.Center;
            var footer = Rect("Barrier_Actions", prison, new Vector2(.05f, .055f), new Vector2(.95f, .175f));
            barrierHint = Button("Barrier_Next_Hint", footer, "Hint 1 / 3", () => RequestBarrier(false));
            Stretch((RectTransform)barrierHint.transform, new Vector2(0, .06f), new Vector2(.25f, .94f));
            confirm = Button("Confirm_Barrier_Answer", footer, "Confirm answer", () => RequestBarrier(true));
            Stretch((RectTransform)confirm.transform, new Vector2(.63f, .06f), new Vector2(1, .94f));
            var notice = Label("One_Confirmed_Attempt", footer, "Wrong answer or timeout\n−1 heart + brief stun", 22, false);
            Stretch(notice.rectTransform, new Vector2(.27f, .08f), new Vector2(.61f, .92f)); notice.alignment = TextAlignmentOptions.Center;
            prison.gameObject.SetActive(false);
        }
        void StyleBarrierChoice(Button button, int index)
        {
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
            var label = button.GetComponentInChildren<TMP_Text>(); label.enableAutoSizing = false; label.fontSize = 26; label.richText = false;
            Stretch(label.rectTransform, new Vector2(.15f, .13f), new Vector2(.95f, .87f)); label.alignment = TextAlignmentOptions.MidlineLeft;
            var letter = Label("Answer_Letter", button.transform, ((char)('A' + index)).ToString(), 29, true);
            Stretch(letter.rectTransform, new Vector2(.025f, .1f), new Vector2(.13f, .9f)); letter.color = new Color(1, .83f, .43f); letter.alignment = TextAlignmentOptions.Center;
            var border = button.gameObject.AddComponent<Outline>(); border.effectDistance = new Vector2(3, -3); border.effectColor = Color.clear;
        }
        void PaintBarrierPresentation(Color accent, int outcome, int seconds, double remaining)
        {
            barrierHeaderLine.color = accent;
            barrierTimeFill.color = outcome == 0 && seconds <= 30 ? new Color(.80f, .07f, .09f) : accent;
            barrierTimeFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)(remaining / KhezuHuntingController.BarrierChallengeSeconds)), 1);
            prisonClock.color = outcome == 0 && seconds <= 30 ? new Color(1, .75f, .48f) : new Color(1, .91f, .68f);
        }
    }
}
