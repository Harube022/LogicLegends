using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveExitUI
    {
        TMP_Text topicsValue,survivorsValue,crystalsValue,personalStats,rewardStatus;
        ArchiveResultEmblemGraphic emblem;
        CanvasGroup resultGroup;
        float resultFade;
        static readonly Color VictoryInk=new Color(.08f,.32f,.28f),DefeatInk=new Color(.62f,.16f,.10f);
        public string ResultMetricsText => topicsValue==null?"":topicsValue.text+" | "+survivorsValue.text+" | "+crystalsValue.text+" | "+personalStats.text;

        void BuildMatchPresentation()
        {
            // Own only this instance. Solo retains its common layout and reward presentation.
            var soloLayout=result.Root.GetComponent<StageResultLayout>();
            if(soloLayout!=null)soloLayout.enabled=false;
            resultGroup=result.Root.gameObject.AddComponent<CanvasGroup>();
            result.Root.GetComponent<Image>().color=new Color(.01f,.035f,.045f,.96f);
            var mark=StageUITheme.Rect("Archive_Victory_Seal",result.Card);
            StageUITheme.Stretch(mark,new Vector2(.445f,.78f),new Vector2(.555f,.91f));
            emblem=mark.gameObject.AddComponent<ArchiveResultEmblemGraphic>();emblem.raycastTarget=false;
            topicsValue=Metric("Topics_Restored","TOPICS COMPLETE",.07f,.34f);
            survivorsValue=Metric("Team_Escape","SURVIVORS ESCAPED",.365f,.635f);
            crystalsValue=Metric("Crystal_Restoration","CRYSTALS RESTORED",.66f,.93f);
            personalStats=ResultText("Personal_Contribution",result.Card,25,true);
            StageUITheme.Stretch(personalStats.rectTransform,new Vector2(.07f,.315f),new Vector2(.93f,.395f));
            var receipt=StageUITheme.Rect("Match_Reward_Status",result.Card);
            StageUITheme.Stretch(receipt,new Vector2(.07f,.18f),new Vector2(.93f,.30f));
            receipt.gameObject.AddComponent<Image>().color=new Color(.12f,.25f,.22f,.075f);
            receipt.GetComponent<Image>().raycastTarget=false;
            rewardStatus=ResultText("Authoritative_Reward_Receipt",receipt,22,false);
            StageUITheme.Stretch(rewardStatus.rectTransform,new Vector2(.025f,.05f),new Vector2(.975f,.95f));
            LayoutMatchPresentation();
        }

        TMP_Text Metric(string name,string caption,float left,float right)
        {
            var box=StageUITheme.Rect(name,result.Card);
            StageUITheme.Stretch(box,new Vector2(left,.42f),new Vector2(right,.575f));
            var image=box.gameObject.AddComponent<Image>();image.color=new Color(.10f,.30f,.27f,.10f);image.raycastTarget=false;
            var top=StageUITheme.Rect("Gold_Rim",box);
            StageUITheme.Stretch(top,new Vector2(0,.97f),Vector2.one);
            var rim=top.gameObject.AddComponent<Image>();rim.color=new Color(.61f,.45f,.19f,.8f);rim.raycastTarget=false;
            var label=ResultText("Metric_Caption",box,19,true);label.text=caption;
            StageUITheme.Stretch(label.rectTransform,new Vector2(.025f,.07f),new Vector2(.975f,.33f));
            var value=ResultText("Metric_Value",box,43,true);
            StageUITheme.Stretch(value.rectTransform,new Vector2(.04f,.35f),new Vector2(.96f,.95f));
            value.color=VictoryInk;
            return value;
        }

        static TMP_Text ResultText(string name,Transform parent,float size,bool heading)
        {
            var text=StageUITheme.Text(name,parent,"",size,!heading);
            text.font=heading?StageUITheme.HeadingFont:StageUITheme.ReadingFont;
            text.color=StageUITheme.ReadingInk;text.outlineWidth=0;text.alignment=TextAlignmentOptions.Center;
            text.enableAutoSizing=true;text.fontSizeMin=heading?size*.7f:17;text.fontSizeMax=size;
            text.textWrappingMode=TextWrappingModes.Normal;text.raycastTarget=false;text.richText=false;
            return text;
        }

        void PresentMatchStats(bool won,bool defeated)
        {
            int complete=session.State.stations?.Count(s=>s!=null&&s.phase==2)??0;
            int restored=session.State.crystals?.Count(c=>c.fullyRestored)??0;
            topicsValue.text=complete+" / 10";
            survivorsValue.text=session.EscapedSurvivorCount+" / "+session.SurvivorCount;
            crystalsValue.text=restored+" / 10";
            personalStats.text="YOUR CONTRIBUTIONS  "+session.RewardContributionCount(session.LocalActor)+" / 3"+
                "     ·     TEAM MISTAKES  "+(session.State.mistakes?.Count??0);
            rewardStatus.text=won||defeated?session.MatchRewardSummary:"Your escape is recorded. Waiting for every surviving teammate.";
            var report=session.RewardReport(session.LocalActor);
            bool practice=report!=null&&(report.adminUsed||!report.online);
            rewardStatus.color=practice?new Color(.54f,.29f,.08f):VictoryInk;
            result.Heading.color=defeated?DefeatInk:VictoryInk;
            emblem.color=defeated?DefeatInk:new Color(.18f,.49f,.36f);
        }

        void LayoutMatchPresentation()
        {
            var area=Screen.safeArea;
            result.Root.anchorMin=new Vector2(area.xMin/Mathf.Max(1,Screen.width),area.yMin/Mathf.Max(1,Screen.height));
            result.Root.anchorMax=new Vector2(area.xMax/Mathf.Max(1,Screen.width),area.yMax/Mathf.Max(1,Screen.height));
            result.Root.offsetMin=result.Root.offsetMax=Vector2.zero;
            result.Card.anchorMin=result.Card.anchorMax=result.Card.pivot=Vector2.one*.5f;
            result.Card.anchoredPosition=Vector2.zero;
            result.Card.sizeDelta=new Vector2(Mathf.Max(320,Mathf.Min(1060,result.Root.rect.width-36)),Mathf.Max(420,Mathf.Min(770,result.Root.rect.height-30)));
            result.Card.GetComponent<Image>().pixelsPerUnitMultiplier=3.5f;
            ResultLabel(result.Stage,new Vector2(.07f,.92f),new Vector2(.93f,.965f),19,25);
            ResultLabel(result.Heading,new Vector2(.10f,.68f),new Vector2(.90f,.79f),35,60);
            ResultLabel(result.Summary,new Vector2(.08f,.595f),new Vector2(.92f,.67f),18,24);
            bool both=result.Primary.gameObject.activeSelf&&result.Menu.gameObject.activeSelf;
            StageUITheme.Stretch((RectTransform)result.Primary.transform,new Vector2(.07f,.045f),new Vector2(both?.49f:.93f,.15f));
            StageUITheme.Stretch((RectTransform)result.Menu.transform,new Vector2(both?.51f:.24f,.045f),new Vector2(both?.93f:.76f,.15f));
            foreach(var button in new[]{result.Primary,result.Menu})
            {
                var label=button.GetComponentInChildren<TMP_Text>();
                label.enableAutoSizing=true;label.fontSizeMin=20;label.fontSizeMax=30;
            }
        }

        static void ResultLabel(TMP_Text text,Vector2 min,Vector2 max,float low,float high)
        {
            StageUITheme.Stretch(text.rectTransform,min,max);text.enableAutoSizing=true;
            text.fontSizeMin=low;text.fontSizeMax=high;text.raycastTarget=false;text.outlineWidth=0;
            text.textWrappingMode=TextWrappingModes.Normal;text.alignment=TextAlignmentOptions.Center;
        }
    }
}
