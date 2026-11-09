using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicUI
    {
        // Local presentation preference only. Timers and topic state stay in ArchiveTopicSession.
        sealed class ObjectiveCard
        {
            public RectTransform rect;
            public TMP_Text title,timer;
            public RectTransform hideButton;
            public bool available;
        }
        readonly ObjectiveCard[] objectiveCards=new ObjectiveCard[10];
        RectTransform collapsedObjective;
        TMP_Text collapsedTitle,collapsedTimer;
        public bool ObjectiveDetailsHidden {get;private set;}
        public RectTransform CollapsedObjective=>collapsedObjective;

        internal void RegisterObjective(int topic,RectTransform rect,TMP_Text title,TMP_Text timer)
        {
            objectiveCards[topic]=new ObjectiveCard{rect=rect,title=title,timer=timer};
            // Reserve a separate touch target; neither the timer nor the title is covered.
            var max=title.rectTransform.anchorMax;max.x=.62f;title.rectTransform.anchorMax=max;
            var min=timer.rectTransform.anchorMin;min.x=.64f;timer.rectTransform.anchorMin=min;
            max=timer.rectTransform.anchorMax;max.x=.82f;timer.rectTransform.anchorMax=max;
            objectiveCards[topic].hideButton=ObjectiveToggleButton(rect,"Hide",ToggleObjectiveDetails);
            if(collapsedObjective!=null)return;
            collapsedObjective=Rect("Collapsed_Topic_Challenge",transform,Vector2.one,Vector2.one);
            collapsedObjective.pivot=Vector2.one;collapsedObjective.sizeDelta=new Vector2(440,64);
            Surface(collapsedObjective,statusArt);collapsedObjective.GetComponent<Image>().raycastTarget=false;
            collapsedTitle=Text("Current_Topic_And_Challenge",collapsedObjective,"",20,headingFont);
            Stretch(collapsedTitle.rectTransform,new Vector2(.14f,.18f),new Vector2(.62f,.82f));
            collapsedTitle.enableAutoSizing=true;collapsedTitle.fontSizeMin=17;collapsedTitle.fontSizeMax=20;
            collapsedTitle.textWrappingMode=TextWrappingModes.NoWrap;collapsedTitle.overflowMode=TextOverflowModes.Ellipsis;
            collapsedTitle.alignment=TextAlignmentOptions.MidlineLeft;
            collapsedTimer=Text("Current_Shared_Timer",collapsedObjective,"",22,headingFont);
            Stretch(collapsedTimer.rectTransform,new Vector2(.64f,.18f),new Vector2(.82f,.82f));
            collapsedTimer.alignment=TextAlignmentOptions.MidlineRight;
            collapsedTimer.enableAutoSizing=true;collapsedTimer.fontSizeMin=19;collapsedTimer.fontSizeMax=22;
            ObjectiveToggleButton(collapsedObjective,"Show",ToggleObjectiveDetails);
            collapsedObjective.gameObject.SetActive(false);
        }

        RectTransform ObjectiveToggleButton(Transform parent,string label,UnityEngine.Events.UnityAction action)
        {
            var button=Button(parent,label,action);button.name=label+"_Challenge_Details";
            button.anchorMin=button.anchorMax=Vector2.one;
            button.pivot=Vector2.one;button.sizeDelta=new Vector2(64,44);button.anchoredPosition=new Vector2(-8,-8);
            var text=button.GetComponentInChildren<TMP_Text>();text.fontSize=text.fontSizeMax=19;text.fontSizeMin=17;
            text.enableAutoSizing=true;text.textWrappingMode=TextWrappingModes.NoWrap;
            var selectable=button.GetComponent<Button>();selectable.navigation=new Navigation{mode=Navigation.Mode.None};
            return button;
        }

        internal void SetObjectiveVisible(int topic,bool available)
        {
            var card=objectiveCards[topic];if(card==null)return;
            card.available=available;card.rect.gameObject.SetActive(available&&!ObjectiveDetailsHidden);
        }

        public void ToggleObjectiveDetails()
        {
            ObjectiveDetailsHidden=!ObjectiveDetailsHidden;
            RefreshObjectiveToggle();
        }

        void LateUpdate()=>RefreshObjectiveToggle();

        void RefreshObjectiveToggle()
        {
            if(collapsedObjective==null)return;
            bool usable=session!=null&&session.Ready&&Player.LocalInstance!=null&&Time.timeScale>0&&
                session.CanAct(session.LocalActor)&&ReadingStation==null;
            ObjectiveCard visible=null;
            foreach(var card in objectiveCards)
            {
                if(card==null)continue;
                // Body labels reach 82% of card height. Keep a full touch
                // target above them; a short card lifts this corner tab into
                // the existing 12-unit gap below the health display.
                float top=Mathf.Min(8,card.rect.rect.height*.18f-46);
                card.hideButton.anchoredPosition=new Vector2(-8,-top);
                bool show=usable&&card.available;
                card.rect.gameObject.SetActive(show&&!ObjectiveDetailsHidden);
                if(show&&visible==null)visible=card;
            }
            bool minimized=ObjectiveDetailsHidden&&visible!=null;
            collapsedObjective.gameObject.SetActive(minimized);
            if(!minimized)return;
            // Follow the existing safe-area/health/jump layout. Reuse displayed
            // labels rather than keeping a second countdown or challenge counter.
            var source=visible.rect;
            collapsedObjective.anchorMin=source.anchorMin;collapsedObjective.anchorMax=source.anchorMax;
            collapsedObjective.pivot=source.pivot;collapsedObjective.anchoredPosition=source.anchoredPosition;
            collapsedObjective.sizeDelta=new Vector2(source.rect.width,64);
            collapsedTitle.text=visible.title.text;
            bool timerShown=visible.timer.gameObject.activeSelf;
            collapsedTimer.gameObject.SetActive(timerShown);
            collapsedTimer.text=visible.timer.text;collapsedTimer.color=visible.timer.color;
        }
    }
}
