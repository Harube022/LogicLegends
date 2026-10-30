using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveRewardView
    {
        const float TeamItemPanelHeight=146;
        readonly List<int> nearbyTeamItemActors=new List<int>();
        readonly Dictionary<int,Player> teamItemPlayers=new Dictionary<int,Player>();
        RectTransform teamItemPanel;
        UnityEngine.UI.Button teamTargetButton,giveItemButton,useOnTeammateButton;
        TMP_Text teamTargetLabel,giveItemLabel,useOnTeammateLabel,teamItemHint;
        int teamTargetActor;
        bool teamTargetInitialized;
        string teamItemMatch="";
        string teamItemFeedback="";
        float teamItemFeedbackUntil;
        public int TargetActor=>teamTargetActor;
        public int NearbyTeamItemCount=>nearbyTeamItemActors.Count;
        public RectTransform TeamItemPanel=>teamItemPanel;
        public UnityEngine.UI.Button TeamItemTargetButton=>teamTargetButton;
        public UnityEngine.UI.Button GiveItemButton=>giveItemButton;
        public UnityEngine.UI.Button UseOnTeammateButton=>useOnTeammateButton;
        public string TeamItemExplanation=>teamItemHint!=null?teamItemHint.text:"";

        void BuildTeamItemActions()
        {
            teamItemPanel=Rect("Nearby_Teammate_Item_Actions",root,Vector2.one*.5f,Vector2.one*.5f);
            teamItemPanel.sizeDelta=new Vector2(600,TeamItemPanelHeight);
            var background=teamItemPanel.gameObject.AddComponent<UnityEngine.UI.Image>();
            StageUITheme.CompactSurface(background);background.raycastTarget=false;
            teamTargetButton=TeamItemButton("Select_Teammate",new Vector2(.025f,.73f),new Vector2(.975f,.97f),out teamTargetLabel);
            teamTargetButton.onClick.AddListener(CycleTeamItemTarget);
            giveItemButton=TeamItemButton("Give_One_Item",new Vector2(.025f,.32f),new Vector2(.49f,.71f),out giveItemLabel);
            giveItemButton.onClick.AddListener(()=>GiveSelectedItemToTeammate());
            useOnTeammateButton=TeamItemButton("Use_Item_On_Teammate",new Vector2(.51f,.32f),new Vector2(.975f,.71f),out useOnTeammateLabel);
            useOnTeammateButton.onClick.AddListener(()=>UseSelectedItemOnTeammate());
            teamItemHint=Text("Teammate_Item_Explanation",teamItemPanel,16);
            teamItemHint.rectTransform.anchorMin=new Vector2(.035f,.045f);teamItemHint.rectTransform.anchorMax=new Vector2(.965f,.29f);
            teamItemHint.alignment=TextAlignmentOptions.Center;teamItemHint.enableAutoSizing=true;
            teamItemHint.fontSizeMin=13;teamItemHint.fontSizeMax=16;
            teamItemPanel.gameObject.SetActive(false);
        }
        UnityEngine.UI.Button TeamItemButton(string name,Vector2 min,Vector2 max,out TMP_Text label)
        {
            var rect=Rect(name,teamItemPanel,min,max);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
            button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            label=Text(name+"_Label",rect,20);label.alignment=TextAlignmentOptions.Center;label.margin=new Vector4(8,1,8,1);
            StageUITheme.SkinButton(button);label.fontSizeMin=16;label.fontSizeMax=21;return button;
        }
        void PlaceTeamItemActions(float scale)
        {
            if(teamItemPanel==null)return;
            teamItemPanel.sizeDelta=new Vector2(pack.sizeDelta.x,TeamItemPanelHeight);
            teamItemPanel.anchoredPosition=pack.anchoredPosition+Vector2.up*(HotbarHeight*.5f+TeamItemPanelHeight*.5f+8);
        }
        bool TeamItemActorNearby(int actor)
        {
            if(actor==0||actor==session.LocalActor||session.HasEscapedActor(actor))return false;
            if(PhotonNetwork.InRoom)
            {
                var peer=PhotonNetwork.CurrentRoom.GetPlayer(actor);
                if(peer==null||peer.IsInactive)return false;
            }
            else if(actor!=-2)return false;
            if(!teamItemPlayers.TryGetValue(actor,out var teammate)||teammate==null)
            {
                teammate=session.ActorPlayer(actor);if(teammate==null)return false;
                teamItemPlayers[actor]=teammate;
            }
            if(!teammate.gameObject.activeInHierarchy)return false;
            var local=Player.LocalInstance;if(local==null)return false;
            var fallen=session.Downed(actor);
            if(fallen!=null&&fallen.phase!=2)return false;
            if(fallen==null&&session.hunting!=null&&session.hunting.health!=null&&
                !session.hunting.health.IsActorAlive(actor))return false;
            var point=fallen!=null?fallen.anchor:teammate.transform.position;
            return (point-local.transform.position).sqrMagnitude<=ArchiveTopicSession.SharingDistance*ArchiveTopicSession.SharingDistance;
        }
        void AddNearbyTeamItemActor(int actor)
        {if(!nearbyTeamItemActors.Contains(actor)&&TeamItemActorNearby(actor))nearbyTeamItemActors.Add(actor);}
        void RefreshTeamItemCandidates()
        {
            nearbyTeamItemActors.Clear();
            if(session.State.actors!=null)foreach(var actor in session.State.actors)if(actor!=null)AddNearbyTeamItemActor(actor.actor);
            if(session.State.survival?.downed!=null)foreach(var fallen in session.State.survival.downed)if(fallen!=null)AddNearbyTeamItemActor(fallen.actor);
            // Newly joined peers may not have an academic contribution record yet.
            if(PhotonNetwork.InRoom)
                foreach(var peer in PhotonNetwork.CurrentRoom.Players.Values)AddNearbyTeamItemActor(peer.ActorNumber);
            else AddNearbyTeamItemActor(-2);
            nearbyTeamItemActors.Sort();
            if(teamTargetActor!=0&&!nearbyTeamItemActors.Contains(teamTargetActor))
            {
                teamTargetActor=0;teamItemFeedback="Teammate unavailable. Select a nearby teammate before sharing.";
                teamItemFeedbackUntil=Time.unscaledTime+3;
            }
            if(!teamTargetInitialized&&nearbyTeamItemActors.Count>0)
            {teamTargetActor=nearbyTeamItemActors[0];teamTargetInitialized=true;}
        }
        void RefreshTeamItemActions(bool controlsAllowed)
        {
            if(teamItemPanel==null)return;
            if(teamItemMatch!=session.State.matchId)
            {
                teamItemMatch=session.State.matchId;teamTargetActor=0;teamTargetInitialized=false;
                teamItemPlayers.Clear();teamItemFeedbackUntil=0;
            }
            int selected=SelectedHotbarSlot;
            if(selected<0||selected>=6||session.CountItem(session.LocalActor,(ArchiveSurvivalItem)selected)<=0)
            {
                // Keep the chosen teammate when a last-copy gift empties this slot.
                // Selecting another item must not silently switch its recipient.
                teamItemPanel.gameObject.SetActive(false);
                nearbyTeamItemActors.Clear();return;
            }
            RefreshTeamItemCandidates();
            bool visible=controlsAllowed&&nearbyTeamItemActors.Count>0&&focusedTopic<0;
            teamItemPanel.gameObject.SetActive(visible);
            bool targetValid=teamTargetActor!=0&&nearbyTeamItemActors.Contains(teamTargetActor);
            string name=targetValid?session.ActorName(teamTargetActor):"Select teammate";
            teamTargetLabel.text=targetValid?"TO: "+name+(nearbyTeamItemActors.Count>1?"  > NEXT":""):
                "SELECT NEARBY TEAMMATE  >";
            teamTargetButton.interactable=controlsAllowed&&nearbyTeamItemActors.Count>0&&(!targetValid||nearbyTeamItemActors.Count>1);
            giveItemLabel.text="GIVE 1";
            useOnTeammateLabel.text=targetValid?"USE ON "+name:"USE ON TEAMMATE";
            bool support=selected==(int)ArchiveSurvivalItem.Medkit||selected==(int)ArchiveSurvivalItem.Battery||selected==(int)ArchiveSurvivalItem.RevivalKit;
            string giveReason="Select a nearby teammate.",useReason=giveReason;
            bool giveReady=targetValid&&session.CanGiveItem(session.LocalActor,teamTargetActor,(ArchiveSurvivalItem)selected,out giveReason);
            bool useReady=targetValid&&support&&session.CanUseItemOnTeammate(session.LocalActor,teamTargetActor,(ArchiveSurvivalItem)selected,out useReason);
            giveItemButton.interactable=controlsAllowed&&giveReady;
            useOnTeammateButton.gameObject.SetActive(support);useOnTeammateButton.interactable=controlsAllowed&&useReady;
            var giveRect=(RectTransform)giveItemButton.transform;
            giveRect.anchorMax=new Vector2(support ? .49f : .975f,.71f);
            if(Time.unscaledTime<teamItemFeedbackUntil)teamItemHint.text=teamItemFeedback;
            else if(!targetValid)teamItemHint.text="Select a nearby teammate. Items are never used automatically.";
            else if(support&&!useReady)teamItemHint.text=useReason;
            else if(!giveReady)teamItemHint.text=giveReason;
            else teamItemHint.text=support?"GIVE transfers one item. USE helps "+name+" directly.":
                "Give one "+ItemName((ArchiveSurvivalItem)selected)+" to "+name+".";
        }
        public bool SelectTeamItemTarget(int actor)
        {
            if(!HotbarControlsAllowed()||teamItemPanel==null||!teamItemPanel.gameObject.activeInHierarchy||!TeamItemActorNearby(actor))return false;
            teamTargetActor=actor;teamTargetInitialized=true;teamItemFeedbackUntil=0;RefreshTeamItemActions(true);return true;
        }
        public void CycleTeamItemTarget()
        {
            if(!HotbarControlsAllowed()||teamItemPanel==null||!teamItemPanel.gameObject.activeInHierarchy)return;
            RefreshTeamItemCandidates();if(nearbyTeamItemActors.Count==0)return;
            int index=nearbyTeamItemActors.IndexOf(teamTargetActor);
            SelectTeamItemTarget(nearbyTeamItemActors[(index+1)%nearbyTeamItemActors.Count]);
        }
        bool TeamItemClickAllowed(out int target,out ArchiveSurvivalItem item)
        {
            target=teamTargetActor;item=(ArchiveSurvivalItem)SelectedHotbarSlot;
            return HotbarControlsAllowed()&&teamItemPanel!=null&&teamItemPanel.gameObject.activeInHierarchy&&
                SelectedHotbarSlot>=0&&SelectedHotbarSlot<6&&target!=0&&TeamItemActorNearby(target)&&
                session.CountItem(session.LocalActor,item)>0;
        }
        public bool GiveSelectedItemToTeammate()
        {
            if(!TeamItemClickAllowed(out int target,out var item))return false;
            if(!session.CanGiveItem(session.LocalActor,target,item,out var reason))
            {ShowTeamItemFeedback(reason);return false;}
            bool sent=session.RequestGiveEquippedItem(target);
            if(sent)ShowTeamItemFeedback("Give 1 requested for "+session.ActorName(target)+".");return sent;
        }
        public bool UseSelectedItemOnTeammate()
        {
            if(!TeamItemClickAllowed(out int target,out var item))return false;
            if(!session.CanUseItemOnTeammate(session.LocalActor,target,item,out var reason))
            {ShowTeamItemFeedback(reason);return false;}
            bool sent=session.RequestUseEquippedItem(target);
            if(sent)ShowTeamItemFeedback("Support requested for "+session.ActorName(target)+".");return sent;
        }
        void ShowTeamItemFeedback(string message)
        {teamItemFeedback=message;teamItemFeedbackUntil=Time.unscaledTime+2.5f;RefreshTeamItemActions(HotbarControlsAllowed());}
    }
}
