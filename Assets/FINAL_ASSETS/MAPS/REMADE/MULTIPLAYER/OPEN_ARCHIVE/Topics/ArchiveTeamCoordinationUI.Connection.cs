using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    public enum ArchiveConnectionQuality { Local,Measuring,Good,Variable,Poor,Reconnecting }
    public sealed partial class ArchiveTeamCoordinationUI
    {
        RectTransform connectionBadge;
        TMP_Text connectionText;
        ArchiveSignalIconGraphic connectionBars;
        float nextConnectionRead;
        public ArchiveConnectionQuality ConnectionQuality {get;private set;}
        public string ConnectionText=>connectionText!=null?connectionText.text:"";
        public RectTransform ConnectionBadge=>connectionBadge;

        void BuildConnectionBadge()
        {
            connectionBadge=Fixed("Local_Connection_Quality",dock,new Vector2(0,1),new Vector2(0,1),new Vector2(370,30));
            connectionBadge.anchoredPosition=new Vector2(0,-90);
            var face=connectionBadge.gameObject.AddComponent<Image>();face.color=new Color(.025f,.065f,.075f,.88f);face.raycastTarget=false;
            var bars=Rect("Connection_Bars",connectionBadge,new Vector2(.025f,.12f),new Vector2(.105f,.91f));
            connectionBars=bars.gameObject.AddComponent<ArchiveSignalIconGraphic>();connectionBars.icon=ArchiveSignalIcon.Bars;connectionBars.raycastTarget=false;
            connectionText=Text("Server_Latency",connectionBadge,17,false,Cream);Stretch(connectionText.rectTransform,new Vector2(.13f,.08f),new Vector2(.98f,.92f));
            connectionText.fontSizeMin=15;
        }
        // Presentation thresholds only. Photon already smooths RTT; variance
        // also degrades the color so a low but erratic RTT is not called stable.
        public static ArchiveConnectionQuality RateConnection(int rtt,int variation)
        {
            if(rtt<=0)return ArchiveConnectionQuality.Measuring;
            if(rtt>=200||variation>=80)return ArchiveConnectionQuality.Poor;
            if(rtt>=100||variation>=35)return ArchiveConnectionQuality.Variable;
            return ArchiveConnectionQuality.Good;
        }
        void UpdateConnectionBadge()
        {
            if(connectionBadge==null||Time.unscaledTime<nextConnectionRead)return;
            nextConnectionRead=Time.unscaledTime+.5f;
            connectionBadge.sizeDelta=new Vector2(dock.rect.width,30);
            if(MultiplayerLobbyService.IsGameplayRecovering||MultiplayerLobbyService.Instance!=null&&MultiplayerLobbyService.Instance.IsRecovering)
            {PresentConnection(ArchiveConnectionQuality.Reconnecting,0,0);return;}
            if(PhotonNetwork.OfflineMode||session.LocalActor<0)
            {PresentConnection(ArchiveConnectionQuality.Local,0,0);return;}
            if(!PhotonNetwork.InRoom||!PhotonNetwork.IsConnectedAndReady)
            {PresentConnection(ArchiveConnectionQuality.Measuring,0,0);return;}
            var peer=PhotonNetwork.NetworkingClient.LoadBalancingPeer;
            PresentConnection(RateConnection(peer.RoundTripTime,peer.RoundTripTimeVariance),peer.RoundTripTime,peer.RoundTripTimeVariance);
        }
        void PresentConnection(ArchiveConnectionQuality quality,int rtt,int variation)
        {
            ConnectionQuality=quality;
            Color tint;int bars;
            switch(quality)
            {
                case ArchiveConnectionQuality.Good:tint=new Color(.40f,.91f,.58f);bars=3;break;
                case ArchiveConnectionQuality.Variable:tint=new Color(1,.76f,.27f);bars=2;break;
                case ArchiveConnectionQuality.Poor:case ArchiveConnectionQuality.Reconnecting:tint=new Color(1,.37f,.32f);bars=1;break;
                default:tint=new Color(.67f,.74f,.72f);bars=0;break;
            }
            connectionBars.bars=bars;connectionBars.color=tint;connectionBars.SetVerticesDirty();connectionText.color=tint;
            connectionText.text=quality==ArchiveConnectionQuality.Local?"LOCAL TEST  ·  no live latency":
                quality==ArchiveConnectionQuality.Reconnecting?"RECONNECTING…":quality==ArchiveConnectionQuality.Measuring?"MEASURING CONNECTION…":
                (quality==ArchiveConnectionQuality.Good?"GOOD":quality==ArchiveConnectionQuality.Variable?"VARIABLE":"HIGH DELAY")+"  "+rtt+" ms  ·  ±"+Mathf.Max(0,variation)+" ms";
        }
    }
}
