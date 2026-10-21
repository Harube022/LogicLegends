using System.Collections;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Offline review utilities, separate from the authoritative topic system.</summary>
    public sealed partial class ArchiveTopicTestControls:MonoBehaviour
    {
        public ArchiveTopicSession session;
        public TMP_FontAsset headingFont,textFont;
        public Sprite buttonArt,frameArt;
        [Range(0,9)]public int selectedTopic;
        TMP_Text label;
        Button visit;
        IEnumerator Start()
        {
            while(!session.Ready)yield return null;
            var body=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Test_Tools_Content");
            if(body==null)yield break;
            var dock=(RectTransform)body.parent;dock.sizeDelta=new Vector2(dock.sizeDelta.x,540);
            BuildTopicAdmin(dock,body);
            var bodyRect=(RectTransform)body;bodyRect.sizeDelta=new Vector2(bodyRect.sizeDelta.x,480);
            var row=new GameObject("Topic_Review_Actions",typeof(RectTransform)).GetComponent<RectTransform>();row.SetParent(body,false);
            row.anchorMin=new Vector2(0,1);row.anchorMax=new Vector2(1,1);row.pivot=new Vector2(.5f,1);row.offsetMin=new Vector2(20,-470);row.offsetMax=new Vector2(-20,-350);
            label=new GameObject("Topic_Name",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();label.transform.SetParent(row,false);label.font=textFont;label.fontSize=22;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;
            var lr=label.rectTransform;lr.anchorMin=new Vector2(0,.6f);lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
            for(int i=0;i<3;i++)
            {
                int index=i;var rect=new GameObject("Topic_Review_"+i,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(row,false);
                rect.anchorMin=new Vector2(i/3f,0);rect.anchorMax=new Vector2((i+1)/3f,.55f);rect.offsetMin=new Vector2(5,3);rect.offsetMax=new Vector2(-5,-3);
                var image=rect.gameObject.AddComponent<Image>();image.sprite=buttonArt;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=4.5f;
                var button=rect.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>{if(index==0)selectedTopic=(selectedTopic+9)%10;else if(index==2)selectedTopic=(selectedTopic+1)%10;else VisitSelected();});
                if(i==1)visit=button;
                var text=new GameObject("Label",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();text.transform.SetParent(rect,false);text.font=headingFont;text.text=new[]{"Previous Topic","Visit Topic","Next Topic"}[i];text.fontSize=22;text.color=new Color(1,.94f,.8f);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
                text.rectTransform.anchorMin=new Vector2(.05f,.08f);text.rectTransform.anchorMax=new Vector2(.95f,.92f);text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
            }
        }
        void Update()
        {
            RefreshTopicAdmin();
            if(label==null)return;
            label.text="T"+(selectedTopic+1)+" — "+ArchiveTopicCatalog.Names[selectedTopic]+"  |  Offline review";
            visit.interactable=CanVisit;
        }
        public bool CanVisit=>!PhotonNetwork.InRoom&&session.Ready&&Player.LocalInstance!=null&&!Player.LocalInstance.IsMovementLocked&&
            (session.hunting==null||session.hunting.State==LogicLegends.OpenArchive.Hunting.KhezuHuntState.Sleeping);
        [ContextMenu("Topics/Visit selected topic (offline, sleeping monster)")]
        public void VisitSelected()
        {
            if(!CanVisit)return;
            var player=Player.LocalInstance;var cc=player.GetComponent<CharacterController>();
            var station=session.stations[selectedTopic];var position=station.transform.TransformPoint(ArchiveTopicStation.BookPosition+Vector3.back*2);
            position.y+=player.transform.position.y-LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(cc).y+.1f;
            if(GameInput.Instance!=null)GameInput.Instance.SetMobileMovement(Vector2.zero);
            var joystick=MobileInputUI.FindJoystick();if(joystick!=null)joystick.ResetJoystick();MobileLookInput.ResetDelta();
            Vector3 delta=position-player.transform.position;cc.enabled=false;player.transform.SetPositionAndRotation(position,station.transform.rotation);cc.enabled=true;player.ResetMotionAfterTeleport();
            foreach(var camera in FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None))
                if(camera.Follow!=null)camera.OnTargetObjectWarped(camera.Follow,delta);
        }
    }
}
