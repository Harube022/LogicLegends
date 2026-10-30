using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Presentation only: flat nest bodies and a local spectator camera. Health and rescue remain session-owned.</summary>
    [DefaultExecutionOrder(10045), DisallowMultipleComponent]
    public sealed class ArchiveDownedPresentation : MonoBehaviour
    {
        public ArchiveTopicSession session;
        [Min(.02f)] public float floorClearance = .10f;
        public bool IsSpectating { get; private set; }
        public int SpectatedActor { get; private set; }
        public int LyingActorCount => bodies.Count;
        public bool IsActorLying(int actor) => bodies.ContainsKey(actor);
        public Bounds LyingBounds(int actor) => bodies.TryGetValue(actor, out var pose) ? pose.bounds : new Bounds();

        sealed class RigPose
        {
            public Transform root, parent, pivot;
            public int sibling;
            public Vector3 position, scale, lyingPosition;
            public Quaternion rotation, lyingRotation;
            public Animator animator;
            public bool animatorEnabled;
            public PlayerAnimator[] drivers;
            public bool[] driverEnabled;
            public SkinnedMeshRenderer[] skins;
            public Bounds[] skinBounds;
            public bool[] skinUpdates;
        }
        sealed class BodyPose
        {
            public Player player;
            public readonly List<RigPose> rigs = new List<RigPose>();
            public Bounds bounds;
        }
        readonly Dictionary<int, BodyPose> bodies = new Dictionary<int, BodyPose>();
        CinemachineCamera spectatorCamera;
        Unity.Cinemachine.CameraTarget originalTarget;
        Transform proxy;
        Canvas overlay;
        RectTransform spectatorPanel;
        TMP_Text status;
        UnityEngine.UI.Button previous, next;
        Vector2 lastScreen;
        Rect lastSafe;
        float nextCameraSearch;
        int[] eligible = Array.Empty<int>();
        bool changedCamera;

        void Start()
        {
            if(session == null) session = GetComponent<ArchiveTopicSession>();
            BuildUI();
        }
        void LateUpdate()
        {
            if(session == null || !session.Ready)
            { RestoreAllBodies(); EndSpectator(); return; }
            foreach(int actor in bodies.Keys.ToArray())
            {
                var d = session.Downed(actor);
                if(d == null || d.phase != 2 || session.ActorPlayer(actor) != bodies[actor].player) RestoreBody(actor);
            }
            foreach(var d in session.State.survival.downed)
            {
                if(d == null || d.phase != 2) continue;
                var player = session.ActorPlayer(d.actor);
                if(player == null) continue;
                if(!bodies.TryGetValue(d.actor, out var pose))
                {
                    // Rescue owns a one-second descent from the carrier. Cache the local visual
                    // offset only after that root movement settles, or the body would sink with it.
                    if(ArchiveTopicSession.Clock-d.arrivedAt<1.05) continue;
                    pose = MakeLyingPose(player);
                    if(pose.rigs.Count == 0) continue;
                    bodies[d.actor] = pose;
                }
                foreach(var rig in pose.rigs)
                {
                    if(rig.pivot == null) continue;
                    rig.pivot.localPosition = rig.lyingPosition;
                    rig.pivot.localRotation = rig.lyingRotation;
                }
            }
            bool downed = Player.LocalInstance != null && session.hunting?.health != null &&
                !session.hunting.health.IsActorAlive(session.LocalActor) && !session.TeamWon;
            if(downed && !MultiplayerLobbyService.IsGameplayRecovering) UpdateSpectator();
            else EndSpectator();
        }
        BodyPose MakeLyingPose(Player player)
        {
            var pose = new BodyPose { player = player };
            foreach(var animator in player.GetComponentsInChildren<Animator>(true))
            {
                if(animator.transform == player.transform || !animator.gameObject.activeInHierarchy) continue;
                if(pose.rigs.Any(r => animator.transform.IsChildOf(r.root))) continue;
                var root = animator.transform;
                var rig = new RigPose
                {
                    root=root, parent=root.parent, sibling=root.GetSiblingIndex(),
                    position=root.localPosition, rotation=root.localRotation, scale=root.localScale,
                    animator=animator, animatorEnabled=animator.enabled,
                    drivers=root.GetComponentsInChildren<PlayerAnimator>(true),
                    skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                };
                rig.driverEnabled=rig.drivers.Select(d=>d.enabled).ToArray();
                rig.skinBounds=rig.skins.Select(r=>r.localBounds).ToArray();
                rig.skinUpdates=rig.skins.Select(r=>r.updateWhenOffscreen).ToArray();
                foreach(var driver in rig.drivers) driver.enabled=false;
                // An existing neutral clip provides a relaxed body, instead of freezing the standing dizzy animation.
                var clips=animator.runtimeAnimatorController!=null?animator.runtimeAnimatorController.animationClips:Array.Empty<AnimationClip>();
                var idle=clips.FirstOrDefault(c=>c!=null&&c.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0);
                if(idle!=null) idle.SampleAnimation(root.gameObject,0);
                animator.enabled=false;
                root.localScale=rig.scale;
                root.localRotation=rig.rotation;
                root.localPosition=rig.position;
                // Animate the imported rig in its original local space. Changing the Animator
                // root itself can become a cached default and be written back after revival.
                rig.pivot=new GameObject("Downed_Visual_Pivot_"+root.GetInstanceID()).transform;
                rig.pivot.SetParent(rig.parent,false);
                rig.pivot.SetSiblingIndex(rig.sibling);
                root.SetParent(rig.pivot,false);
                root.localPosition=rig.position;root.localRotation=rig.rotation;root.localScale=rig.scale;
                rig.pivot.rotation=Quaternion.AngleAxis(-90,player.transform.right)*rig.pivot.rotation;
                var bounds=WorldMeshBounds(root,rig.skins);
                float floor=GroundBelow(player);
                rig.pivot.position += new Vector3(player.transform.position.x-bounds.center.x,
                    floor+floorClearance-bounds.min.y,player.transform.position.z-bounds.center.z);
                rig.lyingPosition=rig.pivot.localPosition;rig.lyingRotation=rig.pivot.localRotation;
                pose.rigs.Add(rig);
            }
            bool any=false;Bounds all=default;
            foreach(var rig in pose.rigs)
            {
                var b=WorldMeshBounds(rig.root,rig.skins);
                if(!any){all=b;any=true;}else all.Encapsulate(b);
            }
            pose.bounds=all;
            return pose;
        }
        static Bounds WorldMeshBounds(Transform root,SkinnedMeshRenderer[] skins)
        {
            bool any=false;Bounds result=default;
            foreach(var skin in skins)
            {
                if(skin==null||!skin.enabled||!skin.gameObject.activeInHierarchy)continue;
                // Imported explorer renderers retain a 0.01 transform scale. Baking with scale
                // supplies renderer-local vertices; TransformPoint below applies that scale exactly once.
                var baked=new Mesh();skin.BakeMesh(baked,true);var local=baked.bounds;
                skin.localBounds=local;skin.updateWhenOffscreen=true;
                for(int i=0;i<8;i++)
                {
                    var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var point=skin.transform.TransformPoint(corner);
                    if(!any){result=new Bounds(point,Vector3.zero);any=true;}else result.Encapsulate(point);
                }
                Destroy(baked);
            }
            if(!any)
            {
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {if(!any){result=renderer.bounds;any=true;}else result.Encapsulate(renderer.bounds);}
            }
            return any?result:new Bounds(root.position,Vector3.one);
        }
        static float GroundBelow(Player player)
        {
            // Search below the delivered feet. Starting high above the body can hit an
            // overhead bridge/pillar cap and place the lying mesh on that roof instead.
            var hits=Physics.RaycastAll(player.transform.position+Vector3.up*.75f,Vector3.down,35,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.6f&&h.collider.GetComponentInParent<Player>()==null).OrderBy(h=>h.distance);
            foreach(var hit in hits)return hit.point.y;
            return player.transform.position.y;
        }
        void RestoreBody(int actor)
        {
            if(!bodies.TryGetValue(actor,out var pose))return;
            foreach(var rig in pose.rigs)
            {
                if(rig.root!=null)
                {
                    rig.root.SetParent(rig.parent,false);
                    rig.root.SetSiblingIndex(rig.sibling);
                    rig.root.localPosition=rig.position;rig.root.localRotation=rig.rotation;rig.root.localScale=rig.scale;
                }
                if(rig.pivot!=null)Destroy(rig.pivot.gameObject);
                for(int i=0;i<rig.skins.Length;i++)if(rig.skins[i]!=null){rig.skins[i].localBounds=rig.skinBounds[i];rig.skins[i].updateWhenOffscreen=rig.skinUpdates[i];}
                if(rig.animator!=null)
                {
                    rig.animator.enabled=rig.animatorEnabled;
                    if(rig.animatorEnabled){rig.animator.Rebind();rig.animator.Update(0);}
                }
                if(rig.root!=null){rig.root.localPosition=rig.position;rig.root.localRotation=rig.rotation;rig.root.localScale=rig.scale;}
                for(int i=0;i<rig.drivers.Length;i++)if(rig.drivers[i]!=null)rig.drivers[i].enabled=rig.driverEnabled[i];
            }
            bodies.Remove(actor);
        }
        void RestoreAllBodies(){foreach(int actor in bodies.Keys.ToArray())RestoreBody(actor);}

        void UpdateSpectator()
        {
            if(!IsSpectating)
            {
                IsSpectating=true;SpectatedActor=0;changedCamera=false;
                if(GameInput.Instance!=null)GameInput.Instance.SetMobileMovement(Vector2.zero);
                MobileInputUI.FindJoystick()?.ResetJoystick();MobileLookInput.ResetDelta();
            }
            eligible=session.AdminTestActors.Where(a=>(!PhotonNetwork.InRoom||PhotonNetwork.CurrentRoom.GetPlayer(a)!=null&&!PhotonNetwork.CurrentRoom.GetPlayer(a).IsInactive)&&a!=session.LocalActor&&session.ActorPlayer(a)!=null&&
                session.ActorPlayer(a).gameObject.activeInHierarchy&&session.hunting.health.IsActorAlive(a)&&!session.HasEscapedActor(a)).OrderBy(a=>a).ToArray();
            if(!eligible.Contains(SpectatedActor))SpectatedActor=eligible.FirstOrDefault();
            if(spectatorCamera==null && Time.unscaledTime>=nextCameraSearch)
            {
                // A scene/camera reload invalidates the previous camera's saved bindings too.
                changedCamera=false;originalTarget=default;
                nextCameraSearch=Time.unscaledTime+.2f;
                var local=Player.LocalInstance;
                spectatorCamera=FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None).FirstOrDefault(c=>c.Follow!=null&&local!=null&&(c.Follow==local.transform||c.Follow.IsChildOf(local.transform)));
                if(spectatorCamera==null)spectatorCamera=FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None).FirstOrDefault(c=>c.name=="CM_ThirdPersonCam");
            }
            if(spectatorCamera!=null)
            {
                if(proxy==null){proxy=new GameObject("Downed_Spectator_Target").transform;proxy.SetParent(transform,false);}
                if(!changedCamera)
                {
                    originalTarget=spectatorCamera.Target;
                    // Preserve Cinemachine's inherited LookAt mode. Its LookAt setter enables
                    // CustomLookAtTarget, which would survive a later TrackingTarget rebind.
                    bool redirectLookAt=originalTarget.CustomLookAtTarget&&spectatorCamera.LookAt!=null;
                    spectatorCamera.Target.TrackingTarget=proxy;
                    if(redirectLookAt)spectatorCamera.Target.LookAtTarget=proxy;
                    spectatorCamera.PreviousStateIsValid=false;changedCamera=true;
                }
                var target=session.ActorPlayer(SpectatedActor);
                var source=Player.LocalInstance!=null?Player.LocalInstance.transform.Find("CameraTarget"):null;
                if(target!=null)
                {
                    var cameraTarget=target.transform.Find("CameraTarget");
                    proxy.position=cameraTarget!=null?cameraTarget.position:target.transform.position+Vector3.up*3.5f;
                    proxy.rotation=source!=null?source.rotation:Quaternion.Euler(18,target.transform.eulerAngles.y,0);
                }
                else
                {
                    proxy.position=session.hunting.monsterSpawnPoint!=null?session.hunting.monsterSpawnPoint.position+Vector3.up*4:Player.LocalInstance.transform.position+Vector3.up*4;
                    proxy.rotation=Quaternion.Euler(35,0,0);
                }
            }
            if(spectatorPanel!=null)
            {
                if(lastScreen!=new Vector2(Screen.width,Screen.height)||lastSafe!=Screen.safeArea)PlaceUI();
                spectatorPanel.gameObject.SetActive(true);
                status.text=SpectatedActor!=0?"DOWNED · SPECTATING "+session.ActorName(SpectatedActor)+"\nA teammate must bring a Revival Kit to your body at the nest.":
                    "DOWNED · NO LIVING TEAMMATE AVAILABLE\nWaiting for the team's outcome.";
                previous.interactable=next.interactable=eligible.Length>1;
            }
        }
        public void CycleSpectator(int direction)
        {
            if(!IsSpectating||eligible.Length<2)return;
            int at=Array.IndexOf(eligible,SpectatedActor);SpectatedActor=eligible[(Mathf.Max(0,at)+(direction<0?eligible.Length-1:1))%eligible.Length];
            if(spectatorCamera!=null)spectatorCamera.PreviousStateIsValid=false;
        }
        void EndSpectator()
        {
            if(changedCamera&&spectatorCamera!=null)
            {
                // Do not overwrite a newer camera owner (scene transitions or a new confirmed capture).
                if(spectatorCamera.Target.TrackingTarget==proxy)spectatorCamera.Target.TrackingTarget=originalTarget.TrackingTarget;
                if(spectatorCamera.Target.LookAtTarget==proxy)
                {
                    spectatorCamera.Target.LookAtTarget=originalTarget.LookAtTarget;
                    spectatorCamera.Target.CustomLookAtTarget=originalTarget.CustomLookAtTarget;
                }
                spectatorCamera.PreviousStateIsValid=false;
            }
            changedCamera=false;IsSpectating=false;SpectatedActor=0;spectatorCamera=null;eligible=Array.Empty<int>();
            if(spectatorPanel!=null)spectatorPanel.gameObject.SetActive(false);
        }
        void BuildUI()
        {
            var go=new GameObject("Archive_Downed_Spectator",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));go.transform.SetParent(transform,false);
            overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=119;
            var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            spectatorPanel=Rect("Spectator_Control_Panel",go.transform,new Vector2(.5f,0),new Vector2(.5f,0));spectatorPanel.pivot=new Vector2(.5f,0);
            var shade=spectatorPanel.gameObject.AddComponent<UnityEngine.UI.Image>();shade.color=new Color(.035f,.065f,.08f,.96f);shade.raycastTarget=false;
            status=Rect("Spectating_And_Revival_Instructions",spectatorPanel,new Vector2(.13f,.12f),new Vector2(.87f,.88f)).gameObject.AddComponent<TextMeshProUGUI>();
            status.font=StageUITheme.ReadingFont;status.fontSize=22;status.color=new Color(1,.90f,.72f);status.alignment=TextAlignmentOptions.Center;status.raycastTarget=false;
            previous=Button("Previous_Living_Teammate","<",new Vector2(.015f,.15f),new Vector2(.11f,.85f),()=>CycleSpectator(-1));
            next=Button("Next_Living_Teammate",">",new Vector2(.89f,.15f),new Vector2(.985f,.85f),()=>CycleSpectator(1));
            PlaceUI();spectatorPanel.gameObject.SetActive(false);
        }
        UnityEngine.UI.Button Button(string name,string text,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var r=Rect(name,spectatorPanel,min,max);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.12f,.32f,.34f);
            var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(action);button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var label=Rect("Label",r,Vector2.zero,Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();label.font=StageUITheme.ReadingFont;label.fontSize=28;label.text=text;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;return button;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        void PlaceUI()
        {
            if(spectatorPanel==null)return;lastScreen=new Vector2(Screen.width,Screen.height);lastSafe=Screen.safeArea;float scale=Mathf.Max(.01f,overlay.scaleFactor);
            spectatorPanel.sizeDelta=new Vector2(Mathf.Min(1000,lastSafe.width/scale*.88f),116);
            var root=overlay.GetComponent<RectTransform>();var point=new Vector2(lastSafe.center.x,lastSafe.yMin+lastSafe.height*.04f);
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(root,point,null,out var local))spectatorPanel.anchoredPosition=local-new Vector2(0,root.rect.yMin);
        }
        void OnDisable(){RestoreAllBodies();EndSpectator();}
        void OnDestroy(){RestoreAllBodies();EndSpectator();if(proxy!=null)Destroy(proxy.gameObject);if(overlay!=null)Destroy(overlay.gameObject);}
    }
}
