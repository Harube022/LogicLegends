using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicTestControls
    {
        RectTransform adminPanel;
        Button adminToggle,adminComplete,adminReset;
        TMP_Text adminStatus,adminMessage;
        Toggle monsterToolsToggle;
        Transform monsterToolsBody;
        int adminTopic=-1,adminRevision=-1;

        void BuildTopicAdmin(RectTransform dock,Transform body)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var header=dock.Find("Show_Hide_Test_Tools") as RectTransform;
            if(header==null)return;
            monsterToolsToggle=header.GetComponent<Toggle>();monsterToolsBody=body;
            var title=header.Find("Title") as RectTransform;
            if(title!=null)title.offsetMax=new Vector2(-300,title.offsetMax.y);
            adminToggle=AdminButton("Topic_Admin_Toggle",header,"ADMIN",new Vector2(.43f,.06f),new Vector2(.67f,.94f),ToggleTopicAdmin);
            adminPanel=new GameObject("Topic_Admin_Panel",typeof(RectTransform)).GetComponent<RectTransform>();
            adminPanel.SetParent(dock,false);adminPanel.anchorMin=new Vector2(0,1);adminPanel.anchorMax=Vector2.one;
            adminPanel.pivot=new Vector2(.5f,1);adminPanel.anchoredPosition=new Vector2(0,-56);adminPanel.sizeDelta=new Vector2(0,252);
            adminPanel.gameObject.AddComponent<Image>().color=new Color(.025f,.075f,.1f,.98f);
            AdminLabel("Admin_Heading",adminPanel,"ADMIN · TOPIC TESTING",headingFont,23,new Vector2(.035f,.85f),new Vector2(.965f,.97f));
            adminStatus=AdminLabel("Admin_Active_Topic",adminPanel,"",textFont,21,new Vector2(.035f,.66f),new Vector2(.965f,.84f));
            adminComplete=AdminButton("Admin_Complete_Active_Topic",adminPanel,"Complete active topic",new Vector2(.035f,.405f),new Vector2(.49f,.635f),()=>RunTopicAdmin(true));
            adminReset=AdminButton("Admin_Reset_Active_Topic",adminPanel,"Reset active topic",new Vector2(.51f,.405f),new Vector2(.965f,.635f),()=>RunTopicAdmin(false));
            AdminLabel("Admin_Help",adminPanel,"Complete skips its remaining challenges.\nReset restarts this topic at the Book.",textFont,18,new Vector2(.04f,.18f),new Vector2(.96f,.38f));
            adminMessage=AdminLabel("Admin_Last_Action",adminPanel,"Other topics and recorded mistakes are kept.",textFont,17,new Vector2(.04f,.025f),new Vector2(.96f,.175f));
            if(monsterToolsToggle!=null)monsterToolsToggle.onValueChanged.AddListener(shown=>{if(shown&&adminPanel!=null)adminPanel.gameObject.SetActive(false);});
            adminPanel.gameObject.SetActive(false);RefreshTopicAdmin();
#endif
        }
        public void ToggleTopicAdmin()
        {
            if(adminPanel==null||session==null||!session.CanUseTopicAdmin)return;
            bool show=!adminPanel.gameObject.activeSelf;
            if(show)
            {
                if(monsterToolsToggle!=null)monsterToolsToggle.SetIsOnWithoutNotify(false);
                if(monsterToolsBody!=null)monsterToolsBody.gameObject.SetActive(false);
            }
            adminPanel.gameObject.SetActive(show);RefreshTopicAdmin();
        }
        void RefreshTopicAdmin()
        {
            if(adminToggle==null)return;
            bool allowed=session!=null&&session.CanUseTopicAdmin;
            adminToggle.gameObject.SetActive(allowed);
            if(!allowed){adminPanel.gameObject.SetActive(false);return;}
            adminTopic=session.ActiveTopic;adminRevision=adminTopic<0?-1:session.State.stations[adminTopic].revision;
            bool active=adminTopic>=0&&!session.TeamWon;
            adminComplete.interactable=adminReset.interactable=active;
            adminStatus.text=active?"Active: T"+(adminTopic+1)+" · "+ArchiveTopicCatalog.Names[adminTopic]:"No active topic · Activate a Book Statue first";
        }
        void RunTopicAdmin(bool complete)
        {
            if(session==null||!session.RequestTopicAdmin(adminTopic,adminRevision,complete))
            {if(adminMessage!=null)adminMessage.text="Topic changed. Refresh the panel and try again.";RefreshTopicAdmin();return;}
            adminMessage.text="T"+(adminTopic+1)+(complete?" completed":" reset")+" for testing · Other topics kept";
            RefreshTopicAdmin();
        }
        Button AdminButton(string name,Transform parent,string caption,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=rect.gameObject.AddComponent<Image>();image.sprite=buttonArt;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4.5f;
            var button=rect.gameObject.AddComponent<Button>();button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(action);
            var colors=button.colors;colors.disabledColor=new Color(.45f,.5f,.52f,.8f);button.colors=colors;
            AdminLabel("Label",rect,caption,headingFont,21,new Vector2(.04f,.06f),new Vector2(.96f,.94f));return button;
        }
        TMP_Text AdminLabel(string name,Transform parent,string content,TMP_FontAsset font,int size,Vector2 min,Vector2 max)
        {
            var label=new GameObject(name,typeof(RectTransform)).AddComponent<TextMeshProUGUI>();label.transform.SetParent(parent,false);
            label.font=font;label.text=content;label.fontSize=size;label.enableAutoSizing=true;label.fontSizeMin=size-2;label.fontSizeMax=size;
            label.color=new Color(1,.94f,.8f);label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
            label.rectTransform.anchorMin=min;label.rectTransform.anchorMax=max;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;return label;
        }
    }
}
