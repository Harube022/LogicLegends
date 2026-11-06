using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local waiting and team result views; no currency or solo-stage completion side effects.</summary>
    [DefaultExecutionOrder(3201)]
    public sealed class ArchiveExitUI : MonoBehaviour
    {
        public ArchiveTopicSession session;
        Canvas canvas;
        RectTransform banner;
        TMP_Text notice;
        StageResultCard result;
        bool leaving;
        public static ArchiveExitUI Current{get;private set;}
        public Button ReviewButton=>result?.Primary;
        public Button ReturnButton=>result?.Menu;
        public bool ResultVisible=>result!=null&&result.Root.gameObject.activeInHierarchy;
        public bool PortalNoticeVisible=>banner!=null&&banner.gameObject.activeInHierarchy;
        void Awake()
        {
            Current=this;ArchiveLearningRecapUI.Watch(session!=null?session:ArchiveTopicSession.Current);
            canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=180;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            banner=StageUITheme.Rect("PortalOpenNotice",transform);
            banner.anchorMin=new Vector2(.27f,.80f);banner.anchorMax=new Vector2(.73f,.88f);banner.offsetMin=banner.offsetMax=Vector2.zero;
            var image=banner.gameObject.AddComponent<Image>();image.color=new Color(.015f,.13f,.15f,.93f);image.raycastTarget=false;
            notice=StageUITheme.Text("ExitProgress",banner,"",26,true);
            StageUITheme.Stretch(notice.rectTransform,new Vector2(.035f,.06f),new Vector2(.965f,.94f));
            notice.color=new Color(1,.88f,.57f);notice.alignment=TextAlignmentOptions.Center;notice.raycastTarget=false;
            result=StageResultCard.Build(transform,()=>StartCoroutine(Leave()));
            var menuLabel=result.Menu.GetComponentInChildren<TMP_Text>();if(menuLabel!=null)menuLabel.text="RETURN TO PARTY";
            result.Primary.onClick.AddListener(ArchiveLearningRecapUI.OpenLastReview);
            result.Primary.GetComponentInChildren<TMP_Text>().text="REVIEW LEARNING";
            result.Primary.gameObject.SetActive(false);result.Extra.gameObject.SetActive(false);
            StageUITheme.Stretch((RectTransform)result.Menu.transform,new Vector2(.32f,.14f),new Vector2(.68f,.27f));
            banner.gameObject.SetActive(false);
        }
        void Update()
        {
            if (session==null)session=ArchiveTopicSession.Current;
            bool opened=session!=null&&session.PortalOpened;
            bool defeated=session!=null&&session.TeamDefeated;
            bool escaped=opened&&session.HasEscapedActor(session.LocalActor),won=opened&&session.TeamWon;
            // Victory and reward receipts are already authoritative. Delay only
            // this result card while the bounded shared victory film is covered.
            if(won&&ArchiveMatchCinematic.CoversVictoryResults(session))
            {banner.gameObject.SetActive(false);result.Root.gameObject.SetActive(false);ArchiveLearningRecapUI.Observe(session);return;}
            banner.gameObject.SetActive(opened&&!escaped&&!won&&!defeated);
            bool show=escaped||won||defeated;
            result.Root.gameObject.SetActive(show);
            ArchiveLearningRecapUI.Observe(session);
            result.Primary.gameObject.SetActive((won||defeated)&&ArchiveLearningRecapUI.HasReview);
            result.Menu.gameObject.SetActive(won||defeated);
            if(defeated)
            {
                if(ArchiveTopicUI.Current!=null&&ArchiveTopicUI.Current.ReadingStation!=null)ArchiveTopicUI.Current.Close();
                bool expired=session.State.survival.defeatReason==ArchiveTopicSession.MatchTimeoutReason;
                result.Stage.text="THE OPEN ARCHIVE";result.Heading.text=expired?"TIME IS UP":"TEAM DEFEAT";
                result.Summary.text=(expired?"The Archive's shared match time has ended.":"No teammate remains to rescue the team.")+"\nReturning to the party in "+Mathf.CeilToInt(session.DefeatRemaining)+"...\n"+session.MatchRewardSummary;
                Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return;
            }
            if(!opened)return;
            int escapedCount=session.EscapedSurvivorCount,total=session.SurvivorCount;
            if(!show)
            {
                bool ready=session.exitPortal!=null&&session.exitPortal.Formed;
                notice.text=(ready?"EXIT PORTAL OPEN · NORTHERN ARCH":"ALL 10 TOPICS COMPLETE · PORTAL FORMING")+
                    "\n"+escapedCount+" / "+total+" survivors escaped · Everyone must enter";
                return;
            }
            if(ArchiveTopicUI.Current!=null&&ArchiveTopicUI.Current.ReadingStation!=null)ArchiveTopicUI.Current.Close();
            result.Stage.text="THE OPEN ARCHIVE · 10 / 10 TOPICS COMPLETE";
            result.Heading.text=won?"VICTORY":"YOU ESCAPED";
            result.Summary.text=won?"Every surviving teammate reached the portal.\n"+session.MatchRewardSummary:
                "You are safe beyond the portal.\nWaiting for teammates · "+escapedCount+" / "+total+" survivors escaped";
            // Leaving early would abandon the remaining team. The menu appears at the shared victory.
            result.Menu.gameObject.SetActive(won);
            if(won&&Cursor.lockState!=CursorLockMode.None){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        void LateUpdate()
        {
            if(!ResultVisible||session==null||!session.TeamWon&&!session.TeamDefeated)return;
            // StageResultLayout is shared with solo. Apply only this multiplayer
            // card's denser receipt text and party action after its common layout.
            StageUITheme.Stretch(result.Summary.rectTransform,new Vector2(.09f,.32f),new Vector2(.91f,.60f));
            result.Summary.fontSizeMin=19;result.Summary.fontSizeMax=27;
            var label=result.Menu.GetComponentInChildren<TMP_Text>();if(label!=null)label.text="RETURN TO PARTY";
            var review=result.Primary.GetComponentInChildren<TMP_Text>();if(review!=null)review.text="REVIEW LEARNING";
        }
        IEnumerator Leave()
        {
            if(leaving)yield break;leaving=true;
            if(MultiplayerLobbyService.Instance!=null){MultiplayerLobbyService.Instance.ReturnToLobbyAfterMatch();yield break;}
            if(PhotonNetwork.InRoom){PhotonNetwork.LeaveRoom(false);while(PhotonNetwork.InRoom)yield return null;}
            SceneManager.LoadScene("Main Menu");
        }
        void OnDestroy(){if(Current==this)Current=null;}
    }
}
