using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Local waiting and team result views; no currency or solo-stage completion side effects.</summary>
    public sealed class ArchiveExitUI : MonoBehaviour
    {
        public ArchiveTopicSession session;
        Canvas canvas;
        RectTransform banner;
        TMP_Text notice;
        StageResultCard result;
        bool leaving;
        void Awake()
        {
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
            result.Primary.gameObject.SetActive(false);result.Extra.gameObject.SetActive(false);
            StageUITheme.Stretch((RectTransform)result.Menu.transform,new Vector2(.32f,.14f),new Vector2(.68f,.27f));
            banner.gameObject.SetActive(false);
        }
        void Update()
        {
            if (session==null)session=ArchiveTopicSession.Current;
            bool opened=session!=null&&session.PortalOpened;
            bool escaped=opened&&session.HasEscapedActor(session.LocalActor),won=opened&&session.TeamWon;
            banner.gameObject.SetActive(opened&&!escaped&&!won);
            bool show=escaped||won;
            result.Root.gameObject.SetActive(show);
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
            result.Summary.text=won?"Every surviving teammate reached the portal.\nYour team has escaped the Open Archive!":
                "You are safe beyond the portal.\nWaiting for teammates · "+escapedCount+" / "+total+" survivors escaped";
            // Leaving early would abandon the remaining team. The menu appears at the shared victory.
            result.Menu.gameObject.SetActive(won);
            if(won&&Cursor.lockState!=CursorLockMode.None){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        IEnumerator Leave()
        {
            if(leaving)yield break;leaving=true;
            if(MultiplayerLobbyService.Instance!=null){MultiplayerLobbyService.Instance.LeaveLobby();yield break;}
            if(PhotonNetwork.InRoom){PhotonNetwork.LeaveRoom(false);while(PhotonNetwork.InRoom)yield return null;}
            SceneManager.LoadScene("Main Menu");
        }
    }
}
