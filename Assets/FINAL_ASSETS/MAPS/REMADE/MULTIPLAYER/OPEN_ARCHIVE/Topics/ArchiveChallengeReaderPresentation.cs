using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Graph;
using LogicLegends.Trees;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Common reader presentation. Original topic components keep every action, selection and countdown.</summary>
    [DefaultExecutionOrder(300), DisallowMultipleComponent]
    public sealed class ArchiveChallengeReaderPresentation : MonoBehaviour
    {
        static readonly Color Ink = new Color(.13f,.17f,.15f), Teal = new Color(.055f,.29f,.30f), Cream = new Color(1,.96f,.84f);
        ArchiveTopicUI owner;
        int topic, mode, entryCount = -1;
        RectTransform window, clockFace;
        Vector2 originalSize, screen;
        Rect safeArea;
        float canvasScale;
        TMP_Text title, subtitle, clock, feedback, body, overtimeCaption;
        TMP_FontAsset mathReadingFont;
        ScrollRect reading, itemList;
        ArchiveSignalIconGraphic clockIcon;
        Button taskTab, howTab, overview;
        string originalHeader = "", renderedHeader = "", originalBody = "", renderedBody = "";
        string originalFeedback = "", renderedFeedback = "";
        bool needsRender;
        readonly List<Entry> entries = new List<Entry>();
        sealed class Entry { public Button button; public TMP_Text label, location; public ArchiveSignalIconGraphic icon; public string source="", shown=""; }
        Func<bool> isReadingDetail;
        RectTransform taskCards;
        TMP_Text taskPrompt, taskCaption, taskProgress, truthOutputHeading;
        TMP_Text[] runeStatus;
        readonly List<TaskRow> taskRows = new List<TaskRow>();
        ArchiveQuestion presentedQuestion;
        int presentedRevision = -1, rowCount;
        bool animateChanges, rowsAnimating;
        static readonly Color ProofPaper = new Color(.97f, .91f, .77f);
        static readonly Color ProofInk = new Color(.21f, .23f, .35f);
        static readonly Color PlacedPaper = new Color(.82f, .91f, .86f);
        static readonly Color ChangedPaper = new Color(.98f, .83f, .53f);
        sealed class TaskRow
        {
            public RectTransform row;
            public LayoutElement layout;
            public Image tile, marker;
            public TMP_Text name, status;
            public Color paper;
            public float highlightUntil;
        }

        public static void Install(ArchiveTopicUI owner)
        {
            foreach (Transform child in owner.transform)
            {
                if (!child.name.StartsWith("T") || !child.name.EndsWith("_Reader")) continue;
                int end = child.name.IndexOf('_'); int number;
                if (end < 2 || !int.TryParse(child.name.Substring(1,end-1),out number) || number < 1 || number > 10) continue;
                if(child.GetComponent<ArchiveChallengeReaderPresentation>()!=null)continue;
                var view = child.gameObject.AddComponent<ArchiveChallengeReaderPresentation>();
                view.Initialize(owner, number-1);
            }
        }
        void Initialize(ArchiveTopicUI source, int index)
        {
            owner=source;topic=index;window=(RectTransform)transform;originalSize=window.sizeDelta;
            mathReadingFont=owner.GetComponent<ArchiveSetsUI>().ReadingText.font;
            var old=GetComponent<Image>();
            if(old!=null)
            {
                old.enabled=true;old.sprite=owner.panelArt;old.type=Image.Type.Sliced;old.color=Color.white;
                old.pixelsPerUnitMultiplier=7.5f;
                var shadow=GetComponent<Shadow>();if(shadow==null)shadow=gameObject.AddComponent<Shadow>();
                shadow.effectDistance=new Vector2(0,-5);shadow.effectColor=new Color(.015f,.025f,.025f,.28f);
            }
            var surface=Rect("Simple_Reader_Surface",transform,Vector2.zero,Vector2.one);
            surface.SetAsFirstSibling();
            var surfaceGraphic = surface.gameObject.AddComponent<ArchiveReaderSurfaceGraphic>();
            surfaceGraphic.raycastTarget = false;
            if (topic == 1) surfaceGraphic.FooterDivider = .11f;
            var texts=GetComponentsInChildren<TMP_Text>(true);
            title=texts.FirstOrDefault(t=>t.transform.parent==transform&&(t.name=="Topic_And_Challenge"||t.name=="Topic_And_Round"||t.name=="Topic_And_Stage"||t.name=="Truth_Table_And_Progress"||t.name=="Question_And_Progress"));
            if(title==null)title=texts.FirstOrDefault(t=>t.transform.parent==transform&&t.rectTransform.anchorMin.y>=.84f&&t.name!="Label");
            clock=texts.FirstOrDefault(t=>t.transform.parent==transform&&(t.name=="Team_Clock"||t.name=="Shared_Clock"||t.name=="Shared_Team_Clock"||t.name=="Shared_T1_Timer"));
            feedback=texts.FirstOrDefault(t=>t.transform.parent==transform&&t.name.Contains("Feedback"));
            var badge=Rect("Topic_Badge",transform,new Vector2(.04f,.875f),new Vector2(.092f,.95f));
            badge.gameObject.AddComponent<ArchiveSignalSectorGraphic>().raycastTarget=false;
            var number=Text("Topic_Number",badge,23,owner.headingFont,Cream);number.text="T"+(topic+1);number.alignment=TextAlignmentOptions.Center;
            if(title!=null)
            {
                Stretch(title.rectTransform,new Vector2(.108f,.88f),new Vector2(.73f,.95f));title.fontSize=title.fontSizeMax=31;title.fontSizeMin=25;title.enableAutoSizing=true;
                title.textWrappingMode=TextWrappingModes.NoWrap;title.color=Ink;
                subtitle=Text("Challenge_Detail",transform,21,owner.textFont,Teal);
                Stretch(subtitle.rectTransform,new Vector2(.108f,.816f),new Vector2(.75f,.88f));subtitle.alignment=TextAlignmentOptions.MidlineLeft;
            }
            if(clock!=null)
            {
                clockFace=Rect("Topic_Clock_Badge",transform,new Vector2(.77f,.825f),new Vector2(.895f,.925f));
                clockFace.gameObject.AddComponent<ArchiveHudCardGraphic>().raycastTarget=false;
                overtimeCaption=Text("Topic_Overtime_Caption",clockFace,15,owner.headingFont,new Color(.70f,.32f,.035f));
                Stretch(overtimeCaption.rectTransform,new Vector2(0,1.02f),new Vector2(1,1.38f));
                overtimeCaption.textWrappingMode=TextWrappingModes.NoWrap;
                overtimeCaption.alignment=TextAlignmentOptions.Center;overtimeCaption.text="OVERTIME";overtimeCaption.gameObject.SetActive(false);
                var symbol=Rect("Clock_Icon",clockFace,new Vector2(.065f,.28f),new Vector2(.29f,.72f));
                clockIcon=symbol.gameObject.AddComponent<ArchiveSignalIconGraphic>();clockIcon.icon=ArchiveSignalIcon.Clock;clockIcon.raycastTarget=false;
                clock.rectTransform.SetParent(clockFace,false);Stretch(clock.rectTransform,new Vector2(.32f,.12f),new Vector2(.95f,.88f));
                clock.alignment=TextAlignmentOptions.Center;clock.fontSize=clock.fontSizeMax=28;clock.fontSizeMin=23;
            }
            foreach(var button in GetComponentsInChildren<Button>(true))
            {
                DecorateButton(button);
                if(button.transform.parent!=transform)continue;
                var rect=(RectTransform)button.transform;
                if(button.name=="Close")
                {
                    rect.anchorMin=rect.anchorMax=new Vector2(.945f,.915f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=Vector2.one*68;
                    var label=button.GetComponentInChildren<TMP_Text>();if(label!=null)label.gameObject.SetActive(false);
                    AddIcon("Close_Icon",button.transform,ArchiveSignalIcon.Close,new Vector2(.26f,.26f),new Vector2(.74f,.74f),Cream);
                }
                else if(rect.anchorMax.y<.13f)
                {
                    rect.sizeDelta=new Vector2(Mathf.Min(rect.sizeDelta.x,260),68);
                    if(button.name=="Overview")
                    {
                        overview=button;
                        rect.sizeDelta=new Vector2(76,68);
                        var label=button.GetComponentInChildren<TMP_Text>();if(label!=null)label.gameObject.SetActive(false);
                        AddIcon("Overview_Icon",button.transform,ArchiveSignalIcon.Book,new Vector2(.26f,.22f),new Vector2(.74f,.78f),Cream);
                        button.onClick.AddListener(()=>{mode=0;needsRender=true;});
                    }
                }
            }
            if(feedback!=null)
            {
                feedback.fontSize=feedback.fontSizeMax=21;feedback.fontSizeMin=19;
                feedback.textWrappingMode=TextWrappingModes.Normal;
                if(topic>=3&&topic<=7)Stretch(feedback.rectTransform,new Vector2(.055f,.16f),new Vector2(.945f,.235f));
            }
            foreach(var label in texts)
                if(label.name.EndsWith("_Heading")&&label.transform.parent==transform)
                {
                    if(label.name=="Formula_Heading")label.text="FORMULAS";
                    else if(label.name=="Manuscript_Heading")label.text="SCROLLS";
                    else if(label.name=="Tablet_Heading")label.text="TABLETS";
                    else if(label.name=="Supply_Heading")label.text="ELEMENTS";
                    else if(label.name=="Reference_Heading")label.text="REFERENCE";
                    else if(label.name=="Reading_Heading")label.text="TASK";
                }
            foreach(var scroll in GetComponentsInChildren<ScrollRect>(true))
            {
                if(scroll.verticalScrollbar!=null)scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
                var hit=scroll.GetComponent<Image>();if(hit!=null)hit.color=new Color(.08f,.24f,.20f,.025f);
                var inset=Rect("Reader_Inset_Trim",scroll.transform,Vector2.zero,Vector2.one);
                inset.SetAsFirstSibling();inset.gameObject.AddComponent<ArchiveReaderInsetGraphic>().raycastTarget=false;
            }
            if(topic<2) StyleInlineTask();
            else
            {
                string readingName = topic == 2 ? "Premises_Scroll" : topic <= 6 ? "Proof_Reading_Scroll" :
                    topic == 7 ? "Mapping_Reading" : topic == 8 ? "Graph_Reading" : "Tree_Reading";
                reading=GetComponentsInChildren<ScrollRect>(true).FirstOrDefault(s=>s.name==readingName);
                itemList=GetComponentsInChildren<ScrollRect>(true).FirstOrDefault(s=>s.name=="Manuscript_List_Scroll");
                if(reading!=null)
                {
                    body=reading.content.GetComponent<TMP_Text>();
                    if(body!=null)
                    {
                        body.font=mathReadingFont;
                        body.fontSharedMaterial=mathReadingFont.material;
                        body.fontSize=26;body.lineSpacing=4;body.richText=false;
                    }
                    var bounds = (RectTransform)reading.transform;
                    var min = bounds.anchorMin;
                    var max = bounds.anchorMax;
                    if (itemList != null) { min = new Vector2(.055f,.255f); max.x = .59f; }
                    Stretch(bounds,min,new Vector2(max.x,.705f));
                    var oldHeading=transform.Find(topic==2?"Premise_Heading":"Reading_Heading");
                    if(oldHeading!=null)oldHeading.gameObject.SetActive(false);
                    float middle=(min.x+max.x)*.5f;
                    taskTab=Tab("Reader_Task","TASK",new Vector2(min.x,.72f),new Vector2(middle-.008f,.805f),0);
                    howTab=Tab("Reader_How_To","HOW TO",new Vector2(middle+.008f,.72f),new Vector2(max.x,.805f),1);
                    if (body != null) BuildTaskCards();
                }
                if(itemList!=null)Stretch((RectTransform)itemList.transform,new Vector2(.63f,.255f),new Vector2(.945f,.71f));
                switch (topic)
                {
                    case 3: isReadingDetail = () => owner.GetComponent<ArchiveProofUI>().IsReadingDetail; break;
                    case 4: isReadingDetail = () => owner.GetComponent<ArchiveMethodUI>().IsReadingDetail; break;
                    case 5: isReadingDetail = () => owner.GetComponent<ArchiveInductionUI>().IsReadingDetail; break;
                    case 6: isReadingDetail = () => owner.GetComponent<ArchiveSetsUI>().IsReadingDetail; break;
                }
            }
            foreach(var label in texts)
                if(label.transform.parent==transform&&label.gameObject.activeSelf&&label.name.EndsWith("_Heading")&&label.rectTransform.anchorMin.x>.6f)
                {
                    var bounds=label.rectTransform;var min=bounds.anchorMin;var max=bounds.anchorMax;
                    AddIcon("Reference_Book_Emblem",transform,ArchiveSignalIcon.Book,new Vector2(min.x+.003f,min.y+.010f),new Vector2(min.x+.027f,max.y-.010f),Teal);
                    bounds.anchorMin=new Vector2(min.x+.038f,min.y);
                }
            needsRender=true;
        }
        Button Tab(string name,string caption,Vector2 min,Vector2 max,int section)
        {
            var rect=Rect(name,transform,min,max);rect.gameObject.AddComponent<Image>().color=Teal;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.navigation=new Navigation{mode=Navigation.Mode.None};
            var label=Text(name+"_Text",rect,22,owner.headingFont,Cream);label.text=caption;label.alignment=TextAlignmentOptions.Center;
            DecorateButton(button);button.onClick.AddListener(()=>
            {
                if (isReadingDetail?.Invoke() == true && overview != null) overview.onClick.Invoke();
                mode=section;needsRender=true;
                reading.StopMovement();reading.verticalNormalizedPosition=1;
            });
            return button;
        }
        void OnEnable(){mode=0;needsRender=true;presentedRevision=-1;}
        void LateUpdate()
        {
            if(owner==null||owner.session==null||!owner.session.Ready)return;
            Resize();PresentHeader();
            if(clock!=null)
            {
                var state=owner.session.State.stations[topic];
                clock.text=ArchiveTopicTimerPresentation.Format(owner.session,state);
                var color=ArchiveTopicTimerPresentation.Tint(owner.session,state,true);
                clock.color=color;clockIcon.color=color;
                overtimeCaption.gameObject.SetActive(state.timerOvertime);
            }
            if(feedback!=null&&feedback.text!=renderedFeedback)
            {
                originalFeedback=feedback.text??"";
                renderedFeedback=originalFeedback.StartsWith("Challenge active. Accepted answers restore")?"Challenge active · solve together":originalFeedback;
                feedback.text=renderedFeedback;
            }
            if(topic<2) UpdateInlineTask();
            CaptureBody();
            if(needsRender)RenderBody();
            PresentTaskCards();
            AnimateTaskRows();
            StyleEntries();
        }
        void PresentHeader()
        {
            if(title==null)return;
            if(title.text!=renderedHeader)
            {
                originalHeader=title.text??"";int line=originalHeader.IndexOf('\n');
                subtitle.text=line>=0?originalHeader.Substring(line+1):"";
                renderedHeader=ArchiveTopicCatalog.Names[topic];title.text=renderedHeader;
            }
        }
        void CaptureBody()
        {
            if(body==null||body.text==renderedBody)return;
            originalBody=body.text??"";
            needsRender=true;
        }
        void RenderBody()
        {
            needsRender=false;if(body==null)return;
            var state=owner.session.State.stations[topic];
            renderedBody=mode==1&&(state.phase==1||state.timerExpired)?HowTo(owner.session.Question(topic)):originalBody;
            body.text=renderedBody;
            foreach(var button in new[]{taskTab,howTab})if(button!=null)
            {
                bool selected=button==(mode==0?taskTab:howTab);
                button.GetComponentInChildren<TMP_Text>().color=selected?new Color(1,.85f,.43f):Cream;
                button.GetComponent<Outline>().effectColor=selected?new Color(.91f,.67f,.30f,.9f):Color.clear;
            }
        }

        string HowTo(ArchiveQuestion question)
        {
            switch (topic)
            {
                case 2:
                    int firstParagraph = originalBody.LastIndexOf("\n\n", StringComparison.Ordinal);
                    return firstParagraph < 0 ? originalBody : originalBody.Substring(firstParagraph + 2);
                case 3: return ArchiveProofUI.Instructions(question);
                case 4: return ArchiveMethodUI.Instructions(question);
                case 5: return ArchiveInductionPresentation.FormulaText(ArchiveInductionUI.Instructions(question));
                case 6: return ArchiveSetsUI.Instructions(question);
                default: return originalBody;
            }
        }

        void StyleInlineTask()
        {
            var prompt = transform.Find(topic == 0 ? "Question" : "Logical_Expression")?.GetComponent<TMP_Text>();
            if (prompt == null) return;
            var min = prompt.rectTransform.anchorMin;
            var max = prompt.rectTransform.anchorMax;
            var card = Rect("Task_Prompt_Card", transform, new Vector2(min.x, min.y), new Vector2(max.x, .805f));
            var background = card.gameObject.AddComponent<Image>();
            background.color = ProofInk;
            background.raycastTarget = false;
            var heading = Text("Task_Prompt_Caption", card, 17, owner.headingFont, new Color(1, .81f, .49f));
            heading.text = topic == 0 ? "QUESTION" : "EXPRESSION TO EVALUATE";
            Stretch(heading.rectTransform, new Vector2(.025f,.71f), new Vector2(.975f,.95f));
            heading.alignment = TextAlignmentOptions.MidlineLeft;
            prompt.rectTransform.SetParent(card, false);
            Stretch(prompt.rectTransform, new Vector2(.025f,.05f), new Vector2(.975f,.73f));
            prompt.color = Cream;
            prompt.font = mathReadingFont;
            prompt.fontSharedMaterial = mathReadingFont.material;
            prompt.enableAutoSizing = true;
            prompt.fontSizeMax = 32;
            prompt.fontSizeMin = 19;
            prompt.alignment = TextAlignmentOptions.MidlineLeft;

            if (topic == 0)
            {
                var choices = transform.Find("World_Rune_Choices");
                runeStatus = new TMP_Text[choices.childCount];
                for (int i = 0; i < runeStatus.Length; i++)
                {
                    var face = choices.GetChild(i);
                    var label = face.GetComponentInChildren<TMP_Text>();
                    Stretch(label.rectTransform, new Vector2(.065f,.15f), new Vector2(.935f,.85f));
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 18;
                    label.fontSizeMax = 29;
                    var status = Text("Rune_Channel_Status", face, 16, owner.headingFont, Teal);
                    Stretch(status.rectTransform, new Vector2(.065f,.05f), new Vector2(.935f,.32f));
                    status.alignment = TextAlignmentOptions.MidlineRight;
                    runeStatus[i] = status;
                }
                return;
            }

            var table = transform.Find("Read_Only_Team_Table");
            truthOutputHeading = table.Find("Column_Header_2").GetComponent<TMP_Text>();
            truthOutputHeading.enableAutoSizing = true;
            truthOutputHeading.fontSizeMin = 16;
            var rowHeader = Text("Row_Heading", table, 19, owner.headingFont, Teal);
            Stretch(rowHeader.rectTransform, new Vector2(.02f,.82f), new Vector2(.13f,.98f));
            rowHeader.text = "ROW";
            rowHeader.alignment = TextAlignmentOptions.Center;
            var pHeader = table.Find("Column_Header_0").GetComponent<TMP_Text>();
            Stretch(pHeader.rectTransform, new Vector2(.15f,.82f), new Vector2(.31f,.98f));
            for (int i = 0; i < ArchiveTruthPresentation.RowNames.Length; i++)
            {
                var row = table.Find("Truth_Row_" + (i+1));
                var firstValue = row.Find("P").GetComponent<TMP_Text>();
                Stretch(firstValue.rectTransform, new Vector2(.15f,.06f), new Vector2(.31f,.94f));
                var badge = Rect("Row_Number_Badge", row, new Vector2(.025f,.12f), new Vector2(.125f,.88f));
                var badgeImage = badge.gameObject.AddComponent<Image>();
                badgeImage.color = ProofInk;
                badgeImage.raycastTarget = false;
                var number = Text("Row_Number", badge, 20, owner.headingFont, Cream);
                number.text = (i+1).ToString("00");
                number.alignment = TextAlignmentOptions.Center;
            }
        }

        void UpdateInlineTask()
        {
            var state = owner.session.State.stations[topic];
            if (presentedRevision == owner.session.State.revision) return;
            presentedRevision = owner.session.State.revision;
            if (runeStatus != null)
            {
                for (int i = 0; i < runeStatus.Length; i++)
                    runeStatus[i].text = state.channelActor != 0 && state.channelChoice == i ? "CHANNELING" : "";
            }
            if (truthOutputHeading != null)
                truthOutputHeading.text = state.phase == 1 || state.timerExpired ?
                    "OUTPUT · " + state.placements.Count(item => item == 0 || item == 1) + " / 4" : "OUTPUT";
        }

        void BuildTaskCards()
        {
            var original = body.rectTransform;
            taskCards = Rect("Challenge_Task_Cards", original.parent, original.anchorMin, original.anchorMax);
            taskCards.pivot = original.pivot;
            taskCards.offsetMin = original.offsetMin;
            taskCards.offsetMax = original.offsetMax;
            var list = taskCards.gameObject.AddComponent<VerticalLayoutGroup>();
            list.padding = new RectOffset(5, 7, 9, 14);
            list.spacing = 7;
            list.childControlWidth = list.childControlHeight = list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;
            taskCards.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var theorem = Rect("Task_Prompt_Card", taskCards, Vector2.zero, Vector2.one);
            var theoremBackground = theorem.gameObject.AddComponent<Image>();
            theoremBackground.color = ProofInk;
            theoremBackground.raycastTarget = false;
            var stack = theorem.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(17, 17, 10, 12);
            stack.spacing = 4;
            stack.childControlWidth = stack.childControlHeight = stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            taskCaption = Text("Task_Prompt_Caption", theorem, 16, owner.headingFont, new Color(1, .81f, .49f));
            taskPrompt = Text("Task_Prompt", theorem, 26, mathReadingFont, Cream);
            taskPrompt.richText = false;
            taskProgress = Text("Task_Progress", taskCards, 18, owner.headingFont, ProofInk);
            taskProgress.gameObject.AddComponent<LayoutElement>().minHeight = 27;
            taskCards.gameObject.SetActive(false);
        }

        void PresentTaskCards()
        {
            if (taskCards == null) return;
            var state = owner.session.State.stations[topic];
            bool show = mode == 0 && isReadingDetail?.Invoke() != true && (state.phase == 1 || state.timerExpired);
            var content = show ? taskCards : body.rectTransform;
            bool switched = reading.content != content;
            if (switched)
            {
                reading.StopMovement();
                reading.content = content;
                taskCards.gameObject.SetActive(show);
                body.gameObject.SetActive(!show);
            }
            if (show)
            {
                var question = owner.session.Question(topic);
                if (switched || presentedRevision != owner.session.State.revision || question != presentedQuestion)
                {
                    animateChanges = !switched && question == presentedQuestion && presentedRevision >= 0;
                    if (question != presentedQuestion) reading.verticalNormalizedPosition = 1;
                    presentedRevision = owner.session.State.revision;
                    presentedQuestion = question;
                    UpdateTaskCards(question, state);
                }
            }
            if (switched)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                reading.verticalNormalizedPosition = 1;
            }
        }

        void UpdateTaskCards(ArchiveQuestion question, ArchiveStationState state)
        {
            rowCount = 0;
            taskCaption.text = "CHALLENGE";
            taskPrompt.text = question.prompt;
            switch (topic)
            {
                case 2:
                    taskCaption.text = "PREMISES";
                    taskPrompt.text = ArchiveInferencePresentation.Premises(question, state);
                    ShowPlacements(question, state, ArchiveInferencePresentation.IsPremises(question) ? "TEAM PREMISES" : "RULE CRYSTAL");
                    break;
                case 3:
                    taskCaption.text = "PROOF TASK";
                    ShowPlacements(question, state, "TEAM PROOF");
                    break;
                case 4:
                    taskCaption.text = "PROOF TASK";
                    taskPrompt.text = ArchiveMethodPresentation.Problem(question);
                    bool chooseMethod = ArchiveMethodPresentation.NeedsMethod(question);
                    int method = chooseMethod ? state.selection : (int)question.trial.station;
                    AddTaskRow("Proof method", ArchiveMethodPresentation.MethodName(method), method >= 0);
                    if (question.slots.Length == 0) taskProgress.text = "TEAM ANSWER";
                    else ShowPlacements(question, state, "TEAM PROOF");
                    break;
                case 5:
                    taskCaption.text = "THEOREM TO PROVE";
                    taskPrompt.text = ArchiveInductionPresentation.FormulaText(question.prompt);
                    ShowPlacements(question, state, "TEAM PROOF", "ANCHORS FILLED");
                    break;
                case 6:
                    taskCaption.text = "SET PROBLEM";
                    ShowSetRegions(question, state);
                    break;
                case 7:
                    taskCaption.text = "MAPPING TASK";
                    ShowMapping(question, state);
                    break;
                case 8:
                    taskCaption.text = "GRAPH TASK";
                    ShowGraph(question, state);
                    break;
                case 9:
                    taskCaption.text = "TREE TASK";
                    ShowTree(question, state);
                    break;
            }
            for (int i = rowCount; i < taskRows.Count; i++) taskRows[i].row.gameObject.SetActive(false);
        }

        void ShowPlacements(ArchiveQuestion question, ArchiveStationState state, string label, string units = "PLACED")
        {
            var names = question.regionPlacement ? question.items : question.slots;
            var values = question.regionPlacement ? question.slots : question.items;
            int placed = 0;
            for (int i = 0; i < names.Length; i++)
            {
                int item = i < state.placements.Length ? state.placements[i] : -1;
                bool filled = item >= 0 && item < values.Length;
                if (filled) placed++;
                string name = topic == 5 ? ArchiveInductionPresentation.SlotLabel(i, question) : names[i];
                string status = filled ? values[item] : "Unplaced";
                if (topic == 5) status = filled ? status + " placed" : "Awaiting formula";
                AddTaskRow(name, status, filled);
            }
            taskProgress.text = label + "  ·  " + placed + " / " + names.Length + " " + units;
        }

        void ShowSetRegions(ArchiveQuestion question, ArchiveStationState state)
        {
            taskProgress.text = "TEAM SETS  ·  CONTENTS BY REGION";
            for (int i = 0; i < question.slots.Length; i++)
            {
                int region = i;
                string members = string.Join(", ", question.items.Where((_, item) =>
                    item < state.placements.Length && state.placements[item] == region));
                bool hasMembers = members.Length > 0;
                AddTaskRow(ArchiveSetsPresentation.RegionName(i, question), hasMembers ? "{ " + members + " }" : "∅", hasMembers);
            }
        }

        void ShowMapping(ArchiveQuestion question, ArchiveStationState state)
        {
            taskPrompt.text += "\nDomain = { " + string.Join(", ", question.inputLabels) +
                " }\nCodomain = { " + string.Join(", ", question.outputLabels) + " }";
            taskProgress.text = "TEAM MAPPING  ·  " + state.links.Count + " CONNECTIONS";
            if (question.mapping.questionKind == MappingQuestionKind.ClassifyFunction)
            {
                var names = ArchiveMappingPresentation.ClassificationNames;
                bool selected = state.selection >= 0 && state.selection < names.Length;
                AddTaskRow("Verdict", selected ? names[state.selection] : "Not selected", selected);
            }
            else if (question.mapping.questionKind == MappingQuestionKind.SelectProperties)
            {
                var names = ArchiveMappingPresentation.PropertyNames;
                string properties = string.Join(", ", names.Take(3).Where((_, index) =>
                    (state.flags & ArchiveMappingPresentation.PropertyValues[index]) != 0));
                if (state.selection == 3) properties = names[3];
                AddTaskRow("Selected properties", properties.Length > 0 ? properties : "Not selected", properties.Length > 0);
            }
            if (state.links.Count == 0) AddTaskRow("Connections", "None yet", false);
            foreach (int link in state.links)
            {
                string destination = "→ " + question.outputLabels[link%100];
                if (ArchiveMappingPresentation.Fixed(link, question)) destination += " · fixed";
                AddTaskRow(question.inputLabels[link/100], destination, true);
            }
            if (state.links.Count > 0)
                AddTaskRow("Reached outputs", ArchiveMappingPresentation.RangeText(question, state), true);
        }

        void ShowGraph(ArchiveQuestion question, ArchiveStationState state)
        {
            if (question.graph.type == GraphChallengeType.Degree)
            {
                taskProgress.text = "TEAM ANSWER";
                AddTaskRow("Degree of " + ArchiveTopicQuestions.GraphLabel(question, question.graph.degreeVertex),
                    state.selection >= 0 ? state.selection.ToString() : "Not selected", state.selection >= 0);
                return;
            }
            taskProgress.text = state.routeActor == 0 ? "TEAM ROUTE  ·  UNCLAIMED" : "TEAM ROUTE  ·  " + owner.session.ActorName(state.routeActor);
            if (state.route.Count == 0) AddTaskRow("Start", "No route recorded", false);
            for (int i = 0; i < state.route.Count; i++)
                AddTaskRow(i == 0 ? "Start" : "Next vertex", ArchiveGraphPresentation.Label(question, state.route[i]), true);
        }

        void ShowTree(ArchiveQuestion question, ArchiveStationState state)
        {
            taskProgress.text = question.tree.Editable ? "TEAM TREE  ·  " + state.links.Count + " BRANCHES" : "TEAM ANSWER";
            if (ArchiveTreeRules.NumberQuestion(question))
                AddTaskRow("Selected number", state.flags >= 0 ? state.flags.ToString() : "Not selected", state.flags >= 0);
            else if (!question.tree.Editable)
                AddTaskRow("Selected node", state.selection >= 0 ? question.nodeIds[state.selection] : "Not selected", state.selection >= 0);
            else
            {
                if (state.links.Count == 0) AddTaskRow("Branches", "None yet", false);
                foreach (int link in state.links)
                    AddTaskRow(question.nodeIds[link/100], (question.tree.Directed ? "→ " : "— ") + question.nodeIds[link%100], true);
            }
        }

        void AddTaskRow(string name, string status, bool filled)
        {
            if (rowCount == taskRows.Count) taskRows.Add(CreateTaskRow(rowCount + 1));
            var row = taskRows[rowCount++];
            bool changed = animateChanges && (row.name.text != name || row.status.text != status || !row.row.gameObject.activeSelf);
            row.row.gameObject.SetActive(true);
            row.name.text = name;
            row.status.text = status;
            row.status.color = filled ? Teal : ProofInk;
            row.paper = filled ? PlacedPaper : ProofPaper;
            if (changed)
            {
                row.highlightUntil = Time.unscaledTime + .55f;
                rowsAnimating = true;
            }
            else if (!animateChanges) row.highlightUntil = 0;
            row.tile.color = row.paper;
            row.marker.color = filled ? Teal : new Color(.60f, .47f, .27f);
            float width = Mathf.Max(120, taskCards.rect.width - 12);
            float textHeight = Mathf.Max(row.name.GetPreferredValues(name, width * .38f, 0).y,
                row.status.GetPreferredValues(status, width * .42f, 0).y);
            row.layout.preferredHeight = Mathf.Max(46, textHeight + 16);
        }

        void AnimateTaskRows()
        {
            if (!rowsAnimating || !taskCards.gameObject.activeSelf) return;
            rowsAnimating = false;
            for (int i = 0; i < rowCount; i++)
            {
                var row = taskRows[i];
                if (row.highlightUntil <= 0) continue;
                float blend = Mathf.Clamp01((row.highlightUntil - Time.unscaledTime) / .55f);
                row.tile.color = Color.Lerp(row.paper, ChangedPaper, blend);
                if (blend > 0) rowsAnimating = true;
                else row.highlightUntil = 0;
            }
        }

        TaskRow CreateTaskRow(int index)
        {
            var row = Rect("Task_Row_" + index, taskCards, Vector2.zero, Vector2.one);
            var tile = row.gameObject.AddComponent<Image>();
            tile.raycastTarget = false;
            var layout = row.gameObject.AddComponent<LayoutElement>();
            var marker = Rect("Step_Marker", row, new Vector2(.023f, .17f), new Vector2(.105f, .83f)).gameObject.AddComponent<Image>();
            marker.raycastTarget = false;
            var number = Text("Step_Number", marker.transform, 17, owner.headingFont, Cream);
            number.alignment = TextAlignmentOptions.Center;
            number.text = index.ToString("00");
            var name = Text("Step_Name", row, 19, topic == 6 ? mathReadingFont : owner.headingFont, ProofInk);
            if (topic == 6) name.fontStyle = FontStyles.Bold;
            Stretch(name.rectTransform, new Vector2(.13f, .1f), new Vector2(.53f, .9f));
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.richText = false;
            var status = Text("Step_Status", row, 17, mathReadingFont, Teal);
            Stretch(status.rectTransform, new Vector2(.54f, .10f), new Vector2(.97f, .90f));
            status.alignment = TextAlignmentOptions.MidlineRight;
            status.richText = false;
            return new TaskRow { row = row, layout = layout, tile = tile, marker = marker, name = name, status = status };
        }

        void StyleEntries()
        {
            if(itemList==null)return;
            if(itemList.content.childCount!=entryCount||entries.Any(e=>e.button==null))
            {
                entryCount=itemList.content.childCount;var previous=entries.ToArray();entries.Clear();
                foreach(var button in itemList.content.GetComponentsInChildren<Button>(false))
                {
                    var existing=previous.FirstOrDefault(e=>e.button==button);
                    if(existing!=null){entries.Add(existing);continue;}
                    var label=button.GetComponentInChildren<TMP_Text>();if(label==null)continue;
                    DecorateButton(button);var layout=button.GetComponent<LayoutElement>();if(layout!=null)layout.preferredHeight=76;
                    label.fontSize=label.fontSizeMax=23;label.fontSizeMin=20;label.enableAutoSizing=true;label.textWrappingMode=TextWrappingModes.Normal;
                    Stretch(label.rectTransform,new Vector2(.12f,.12f),new Vector2(.81f,.88f));label.alignment=TextAlignmentOptions.MidlineLeft;
                    var icon=AddIcon("Item_State",button.transform,ArchiveSignalIcon.Book,new Vector2(.86f,.32f),new Vector2(.95f,.68f),Cream);
                    Stretch(label.rectTransform, new Vector2(.10f,.40f), new Vector2(.81f,.86f));
                    var location = Text("Item_Location", button.transform, 16, owner.textFont, Cream);
                    Stretch(location.rectTransform, new Vector2(.10f,.12f), new Vector2(.81f,.45f));
                    location.alignment = TextAlignmentOptions.MidlineLeft;
                    location.overflowMode = TextOverflowModes.Ellipsis;
                    entries.Add(new Entry{button=button,label=label,icon=icon,location=location});
                    button.onClick.AddListener(()=>{mode=0;needsRender=true;});
                }
            }
            foreach(var entry in entries)
            {
                if(entry.button==null)continue;
                if(entry.label.text!=entry.shown)
                {
                    entry.source=entry.label.text??"";int last=entry.source.LastIndexOf('\n');
                    entry.shown=entry.source;
                    if(last>=0)
                    {
                        string location=entry.source.Substring(last+1);
                        bool setCount = topic == 6 && location.Contains(" placed");
                        bool reference = location == "Method reference";
                        bool available=location=="At its stand"||location=="At the supply"||location=="Starts inside U"||reference||location.StartsWith("0 placed");
                        bool carried=location.StartsWith("Carried by ")||location.Contains("· carried");
                        // The full selected-item explanation and location remain in the task reader.
                        entry.shown=entry.source.Substring(0,last);
                        entry.icon.icon=carried?ArchiveSignalIcon.Team:available?ArchiveSignalIcon.Book:ArchiveSignalIcon.Check;
                        entry.icon.color=carried?new Color(1,.82f,.42f):available?new Color(.70f,.83f,.78f):new Color(.55f,.95f,.74f);
                        if (entry.location != null)
                        {
                            entry.location.text = available || carried || setCount ? location : "Placed · " + location;
                            entry.location.color = entry.icon.color;
                        }
                        entry.icon.SetVerticesDirty();
                    }
                    entry.label.text=entry.shown;
                }
                entry.label.color=Cream;
                var normal=entry.button.colors.normalColor;
                entry.button.GetComponent<Outline>().effectColor=normal.b<.8f?new Color(1,.77f,.32f):Color.clear;
            }
        }
        void Resize()
        {
            float scale=Mathf.Max(.01f,owner.GetComponent<Canvas>().scaleFactor);
            if(screen==new Vector2(Screen.width,Screen.height)&&safeArea==Screen.safeArea&&Mathf.Approximately(canvasScale,scale))return;
            screen=new Vector2(Screen.width,Screen.height);safeArea=Screen.safeArea;
            canvasScale=scale;
            presentedRevision=-1;
            window.sizeDelta=new Vector2(Mathf.Min(originalSize.x,safeArea.width/scale-32),Mathf.Min(originalSize.y,safeArea.height/scale-32));
            Vector2 center;RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)window.parent,safeArea.center,null,out center);
            window.anchorMin=window.anchorMax=Vector2.one*.5f;window.pivot=Vector2.one*.5f;window.anchoredPosition=center;
        }
        void DecorateButton(Button button)
        {
            var image=button.GetComponent<Image>();
            if(image!=null){image.sprite=owner.buttonArt;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=6;image.color=Color.white;image.raycastTarget=true;}
            var outline=button.GetComponent<Outline>();if(outline==null)outline=button.gameObject.AddComponent<Outline>();
            outline.effectDistance=new Vector2(1,-1);outline.effectColor=Color.clear;
            button.navigation=new Navigation{mode=Navigation.Mode.None};
            var label=button.GetComponentInChildren<TMP_Text>(true);if(label!=null){label.color=Cream;label.fontSizeMax=Mathf.Min(label.fontSizeMax,25);}
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);Stretch(r,min,max);return r;}
        static void Stretch(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
        static TMP_Text Text(string name,Transform parent,float size,TMP_FontAsset font,Color tint)
        {var t=Rect(name,parent,Vector2.zero,Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.color=tint;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;}
        static ArchiveSignalIconGraphic AddIcon(string name,Transform parent,ArchiveSignalIcon kind,Vector2 min,Vector2 max,Color tint)
        {var icon=Rect(name,parent,min,max).gameObject.AddComponent<ArchiveSignalIconGraphic>();icon.icon=kind;icon.color=tint;icon.raycastTarget=false;return icon;}
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArchiveReaderSurfaceGraphic : MaskableGraphic
    {
        public float FooterDivider { get; set; } = .154f;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();
            var gold=new Color(.52f,.36f,.14f,.42f)*color;
            float top=r.yMin+r.height*.813f,bottom=r.yMin+r.height*FooterDivider;
            Bar(mesh,new Rect(r.xMin+r.width*.045f,top,r.width*.435f,1),gold);
            Bar(mesh,new Rect(r.xMin+r.width*.52f,top,r.width*.435f,1),gold);
            Diamond(mesh,new Vector2(r.center.x,top+.5f),4.5f,new Color(.08f,.35f,.33f,.8f)*color);
            Bar(mesh,new Rect(r.xMin+r.width*.055f,bottom,r.width*.89f,1),new Color(.52f,.36f,.14f,.22f)*color);
            Diamond(mesh,new Vector2(r.xMin+r.width*.045f,bottom+.5f),3,gold);
            Diamond(mesh,new Vector2(r.xMin+r.width*.955f,bottom+.5f),3,gold);
            // Small brass corner brackets enrich the existing wooden frame.
            float inset=24,length=18;
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})
            {
                float px=x<0?r.xMin+inset:r.xMax-inset,py=y<0?r.yMin+inset:r.yMax-inset;
                Bar(mesh,new Rect(x<0?px:px-length,py,length,1.5f),gold);
                Bar(mesh,new Rect(px,y<0?py:py-length,1.5f,length),gold);
                Diamond(mesh,new Vector2(px,py),2.5f,new Color(.81f,.61f,.28f,.8f)*color);
            }
        }
        internal static void Bar(VertexHelper mesh,Rect r,Color tint)
        {
            int n=mesh.currentVertCount;mesh.AddVert(new Vector2(r.xMin,r.yMin),tint,Vector2.zero);mesh.AddVert(new Vector2(r.xMax,r.yMin),tint,Vector2.zero);
            mesh.AddVert(new Vector2(r.xMax,r.yMax),tint,Vector2.zero);mesh.AddVert(new Vector2(r.xMin,r.yMax),tint,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
        }
        static void Diamond(VertexHelper mesh,Vector2 p,float radius,Color tint)
        {
            int n=mesh.currentVertCount;mesh.AddVert(p+Vector2.left*radius,tint,Vector2.zero);mesh.AddVert(p+Vector2.down*radius,tint,Vector2.zero);
            mesh.AddVert(p+Vector2.right*radius,tint,Vector2.zero);mesh.AddVert(p+Vector2.up*radius,tint,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArchiveReaderInsetGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=GetPixelAdjustedRect();
            var edge=new Color(.41f,.34f,.18f,.22f)*color;var light=new Color(1,.99f,.87f,.60f)*color;
            ArchiveReaderSurfaceGraphic.Bar(mesh,new Rect(r.xMin,r.yMin,r.width,1),light);
            ArchiveReaderSurfaceGraphic.Bar(mesh,new Rect(r.xMin,r.yMax-1,r.width,1),edge);
            ArchiveReaderSurfaceGraphic.Bar(mesh,new Rect(r.xMin,r.yMin,1,r.height),edge);
            ArchiveReaderSurfaceGraphic.Bar(mesh,new Rect(r.xMax-1,r.yMin,1,r.height),light);
            // A faint top inset shadow adds depth without competing with equations.
            int n=mesh.currentVertCount;var top=new Color(.20f,.28f,.22f,.045f)*color;var clear=new Color(top.r,top.g,top.b,0);
            mesh.AddVert(new Vector2(r.xMin+1,r.yMax-1),top,Vector2.zero);mesh.AddVert(new Vector2(r.xMax-1,r.yMax-1),top,Vector2.zero);
            mesh.AddVert(new Vector2(r.xMax-1,r.yMax-22),clear,Vector2.zero);mesh.AddVert(new Vector2(r.xMin+1,r.yMax-22),clear,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
        }
    }
}
