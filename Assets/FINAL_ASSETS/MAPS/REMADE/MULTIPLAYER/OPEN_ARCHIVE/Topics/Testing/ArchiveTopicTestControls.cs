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
        Transform legacyDock;
        IEnumerator Start()
        {
            while(!session.Ready)yield return null;
            var body=FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Test_Tools_Content");
            if(body==null)yield break;
            legacyDock=body.parent;
            legacyDock.gameObject.SetActive(LogicLegends.Tests.Khezu.KhezuLegacyTestPermission.CanUse);
            BuildTopicAdmin((RectTransform)legacyDock,body);
        }
        void Update()
        {
            // This service remains active while the dock is hidden. A development
            // host handoff can restore the same dock without rebuilding its buttons.
            if(legacyDock!=null)legacyDock.gameObject.SetActive(LogicLegends.Tests.Khezu.KhezuLegacyTestPermission.CanUse);
            RefreshTopicAdmin();
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
