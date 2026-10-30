using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Clearly labelled offline practice body. Never registers a network user or owns local input.</summary>
    public sealed class ArchivePracticeAvatar : MonoBehaviour
    {
        public ArchiveTopicSession session;
        TMP_Text label;
        void Start()
        {
            label=new GameObject("Test_Teammate_Health").AddComponent<TextMeshPro>();
            label.transform.SetParent(transform,false);label.font=StageUITheme.ReadingFont;label.fontSize=2.8f;
            label.color=new Color(.45f,1,1);label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(8,2);
        }
        void LateUpdate()
        {
            if(label==null||session==null)return;
            label.transform.position=transform.position+Vector3.up*3;
            if(Camera.main!=null)label.transform.rotation=Camera.main.transform.rotation;
            int hearts=session.hunting.health.ActorHearts(-2);var fallen=session.Downed(-2);
            label.text="TEST TEAMMATE\n"+hearts+" / "+session.hunting.health.MaximumHealth+" hearts"+(fallen!=null?" · "+(fallen.phase==2?"REVIVE AT NEST":"DOWNED"):" · Practice only");
        }
    }
    public sealed partial class ArchiveTopicSession
    {
        ArchivePracticeAvatar practiceAvatar;
        public int[] DebugPracticeActors=>practiceAvatar!=null?new[]{-1,-2}:new[]{-1};
        public int[] AdminTestActors=>PhotonNetwork.InRoom?PhotonNetwork.PlayerList.Select(p=>p.ActorNumber).ToArray():DebugPracticeActors;
        public Player DebugActorPlayer(int actor)=>actor==-2&&practiceAvatar!=null?practiceAvatar.GetComponent<Player>():null;
        public bool SpawnAdminTeammate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!CanUseTopicAdmin||PhotonNetwork.InRoom||Player.LocalInstance==null)return false;
            if(practiceAvatar!=null)return true;
            var original=Player.LocalInstance;var root=new GameObject("TEST_TEAMMATE_Offline_Practice");root.SetActive(false);
            practiceAvatar=root.AddComponent<ArchivePracticeAvatar>();practiceAvatar.session=this;
            root.layer=original.gameObject.layer; // Match real players so camera obstacle tests ignore the practice capsule.
            root.transform.localScale=original.transform.localScale;
            var source=original.GetComponent<CharacterController>();var capsule=root.AddComponent<CharacterController>();
            capsule.center=source.center;capsule.radius=source.radius;capsule.height=source.height;capsule.stepOffset=source.stepOffset;capsule.skinWidth=source.skinWidth;
            var body=root.AddComponent<Player>();body.moveSpeed=original.moveSpeed;
            var skin=original.GetComponentsInChildren<Animator>(true).FirstOrDefault();
            if(skin!=null)
            {
                var visual=Instantiate(skin.gameObject,root.transform,false);visual.name="Existing_Player_Rig_Practice";
                foreach(var script in visual.GetComponentsInChildren<MonoBehaviour>(true))DestroyImmediate(script);
                var animator=visual.GetComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                visual.AddComponent<PlayerAnimator>();
                foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=false;
            }
            Vector3 target=original.transform.position+original.transform.right*5;
            if(Physics.Raycast(target+Vector3.up*15,Vector3.down,out var floor,40,~0,QueryTriggerInteraction.Ignore))
            {float offset=original.transform.position.y-LogicLegends.OpenArchive.Water.OpenArchiveWater.Feet(source).y;target.y=floor.point.y+offset+.1f;}
            root.transform.SetPositionAndRotation(target,original.transform.rotation);root.SetActive(true);
            hunting.health.RegisterActors(DebugPracticeActors);hunting.PublishRescueHealth();return true;
#else
            return false;
#endif
        }
        public void RemoveAdminTeammate()
        {
            if(practiceAvatar==null)return;
            foreach(var monster in Hunting.KhezuHuntingController.ActiveMonsters){monster.EndRescueCarry(-2);monster.SetPlayerEligibility(-2,false);}
            if(Ready&&State.survival!=null){State.survival.downed.RemoveAll(d=>d.actor==-2);State.survival.players.RemoveAll(p=>p.actor==-2);}
            var root=practiceAvatar.gameObject;practiceAvatar=null;Destroy(root);
        }
    }
}
