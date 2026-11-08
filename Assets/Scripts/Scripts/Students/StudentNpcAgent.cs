using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LogicLegends.Students
{
    [DefaultExecutionOrder(50)]
    public sealed class StudentNpcAgent : MonoBehaviour
    {
        public string Identity { get; private set; }
        public string DisplayName { get; private set; }
        public int Style { get; private set; }
        public StudentWalkMap Map { get; private set; }
        StudentNpcDirector director;
        StudentNpcCatalog config;
        PlayerAnimator[] animationDrivers;
        Animator[] animators;
        SharedSkinAnimationRetarget[] retargets;
        Renderer[] modelRenderers;
        bool[] rendererDefaults;
        bool visualsShown=true;
        Transform headBone;
        HandThoughtBubble bubble;
        TMP_Text availability;
        readonly List<int> route=new List<int>();
        readonly Queue<int> recent=new Queue<int>();
        readonly Dictionary<Vector2Int,int> visitedAreas=new Dictionary<Vector2Int,int>();
        readonly System.Random random=new System.Random(System.Guid.NewGuid().GetHashCode());
        public int ObstructionReplans { get; private set; }
        public int WalkSegments { get; private set; }
        public int RunSegments { get; private set; }
        public float MovementSpeed { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsTalking=>talking;
        public Vector3 FacePosition=>headBone!=null?headBone.position+Vector3.up*.25f:transform.position+Vector3.up*(config.height*.88f);
        public Vector3 DialogueAnchor=>FacePosition+Vector3.up*.9f;
        int step;
        float rest,stuck,chosenAt,tripDeadline;
        bool talking,longTrip;
        Vector3 lastPosition;
        public void Configure(StudentNpcDirector owner,StudentNpcCatalog.Skin skin,int style,StudentWalkMap map)
        {
            director=owner;config=owner.Config;Identity=skin.id;DisplayName=skin.displayName;Style=style;Map=map;
            // Instantiate inactive so no player, inventory or network script can bind itself.
            var staging=new GameObject("Inactive skin staging");staging.SetActive(false);
            var visual=Instantiate(skin.prefab,staging.transform,false);
            foreach(var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                if(!(behaviour is PlayerAnimator)&&!(behaviour is SharedSkinAnimationRetarget)&&!(behaviour is IntegratedSkinProfile))behaviour.enabled=false;
            foreach(var collider in visual.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var body in visual.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;}
            // Retargeting must receive a freshly evaluated pose even off-screen.
            foreach(var animator in visual.GetComponentsInChildren<Animator>(true)){animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
            animationDrivers=visual.GetComponentsInChildren<PlayerAnimator>(true);
            animators=visual.GetComponentsInChildren<Animator>(true);
            retargets=visual.GetComponentsInChildren<SharedSkinAnimationRetarget>(true);
            modelRenderers=visual.GetComponentsInChildren<Renderer>(true);
            foreach(var bone in visual.GetComponentsInChildren<Transform>(true))if(bone.name=="mixamorig:Head"){headBone=bone;break;}
            rendererDefaults=new bool[modelRenderers.Length];for(int i=0;i<modelRenderers.Length;i++)rendererDefaults[i]=modelRenderers[i].enabled;
            foreach(var driver in animationDrivers)driver.DriveStudentMotion(0);
            visual.transform.SetParent(transform,false);Destroy(staging);visual.SetActive(true);
            var capsule=gameObject.AddComponent<CapsuleCollider>();capsule.isTrigger=true;capsule.radius=config.radius;capsule.height=config.height;capsule.center=Vector3.up*(config.height*.5f);
            bubble=HandThoughtBubble.Create(transform,"Student hand interaction","Student notice",true);bubble.BoundedScreenSize=true;bubble.VisibilityDistance=20;
            // The Book Statue artwork and font at a larger, bounded mobile size.
            bubble.ScreenSizeMultiplier=1.4f;bubble.MaximumScreenWidthFraction=.85f;
            availability=StageUITheme.Text("Hint availability",bubble.Instruction.transform,"",19,true);availability.color=Color.black;availability.raycastTarget=false;
            availability.fontSharedMaterial=bubble.Marker.fontSharedMaterial;
            availability.rectTransform.anchorMin=availability.rectTransform.anchorMax=new Vector2(.5f,.84f);
            availability.rectTransform.anchoredPosition=Vector2.zero;availability.rectTransform.sizeDelta=new Vector2(350,24);
            lastPosition=transform.position;rest=Roll(.4f,2f);
        }
        public void PauseForConversation(Transform player)
        {
            talking=true;route.Clear();rest=1;MovementSpeed=0;IsRunning=false;
            foreach(var animator in animators)animator.updateMode=AnimatorUpdateMode.UnscaledTime;
            foreach(var retarget in retargets)retarget.EvaluatePausedStudentPose=true;
            foreach(var driver in animationDrivers)driver.DriveStudentMotion(0);
            bubble.Show(false,false,transform.position);
        }
        public void Resume()
        {
            talking=false;
            foreach(var animator in animators)animator.updateMode=AnimatorUpdateMode.Normal;
            foreach(var retarget in retargets)retarget.EvaluatePausedStudentPose=false;
        }
        public string NextChat()=>director.NextCasual(Identity);
        float Roll(float min,float max)=>min+(float)random.NextDouble()*(max-min);
        void Update()
        {
            if(director==null||animationDrivers==null)return;
            bool paused=talking||director.Paused||!director.AreaActive(transform.position);
            // Close-up and tutorial cameras must see their content unobstructed.
            SetVisuals(talking||!director.HideModelsForView&&director.AreaActive(transform.position));
            var player=Player.LocalInstance;
            if(talking&&player!=null)
            {
                var face=player.transform.position-transform.position;face.y=0;
                if(face.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(face),180*Time.unscaledDeltaTime);
            }
            bool near=player!=null&&StudentWalkMap.PlanarSquare(player.transform.position,transform.position)<=config.talkRange*config.talkRange;
            bool hasHint=director.HintAvailable(this);
            bubble.Marker.text=hasHint?"!":"…";
            availability.text=near&&hasHint?"Hint available":"";
            availability.color=StudentNpcDialogue.HintColor;
            bubble.Show(!paused&&(hasHint||near),near,transform.position+Vector3.up*(config.height+.7f));
            float dt=Time.deltaTime;
            if(paused){MovementSpeed=0;IsRunning=false;foreach(var driver in animationDrivers)driver.DriveStudentMotion(0);lastPosition=transform.position;return;}
            if(rest>0){rest-=dt;MovementSpeed=0;IsRunning=false;Drive(0);return;}
            if(route.Count==0||step>=route.Count){ChooseDestination();Drive(0);return;}
            // Passing a waypoint must not insert a one-frame Idle transition.
            while(step<route.Count&&StudentWalkMap.PlanarSquare(transform.position,Map.Nodes[route[step]].Point)<.05f)step++;
            if(step>=route.Count){route.Clear();rest=Roll(config.restSeconds.x,config.restSeconds.y);Drive(0);return;}
            var target=Map.Nodes[route[step]].Point;
            Vector3 direction=target-transform.position;direction.y=0;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),180*dt);
            float alignment=Mathf.Clamp01((Vector3.Dot(transform.forward,direction.normalized)-.2f)/.8f);
            float remaining=Vector3.Distance(transform.position,Map.Nodes[route[route.Count-1]].Point);
            bool nearPlayer=player!=null&&StudentWalkMap.PlanarSquare(player.transform.position,transform.position)<(config.talkRange+2)*(config.talkRange+2);
            bool sprint=longTrip&&director.AllowsRunning&&remaining>config.slowNearDestination&&!nearPlayer;
            // Walk on narrow turns, stairs and steeper slopes; run on open long legs.
            sprint=sprint&&alignment>.85f&&Mathf.Abs(target.y-transform.position.y)<.4f;
            float wanted=(sprint?config.runSpeed:config.walkSpeed)*alignment;
            MovementSpeed=Mathf.MoveTowards(MovementSpeed,wanted,config.acceleration*dt);
            if(alignment<.02f){MovementSpeed=0;IsRunning=false;Drive(0);return;}
            var desired=Vector3.MoveTowards(transform.position,target,MovementSpeed*dt);
            Vector3 ground=transform.position;
            bool clear=director.CharacterSpaceFree(this,desired)&&Map.GroundedStep(transform.position,desired,out ground);
            if(clear)
            {
                transform.position=ground;
                stuck=0;
            }
            else
            {
                stuck+=dt;
                MovementSpeed=0;
                if(stuck>.8f){ObstructionReplans++;Map.Block(route[step]);route.Clear();rest=Roll(.5f,1.4f);stuck=0;}
            }
            if(Time.time>tripDeadline){route.Clear();rest=.5f;}
            float actual=dt>0?Mathf.Sqrt(StudentWalkMap.PlanarSquare(lastPosition,transform.position))/dt:0;
            // Hysteresis prevents gait changes at every grid edge or speed sample.
            bool wasRunning=IsRunning;
            IsRunning=longTrip&&director.AllowsRunning&&actual>config.walkSpeed*(wasRunning?1.08f:1.25f);
            if(IsRunning&&!wasRunning)RunSegments++;
            Drive(actual);
        }
        void Drive(float speed){if(speed<=.05f)IsRunning=false;foreach(var driver in animationDrivers)driver.DriveStudentMotion(speed,IsRunning);lastPosition=transform.position;}
        void SetVisuals(bool shown)
        {
            if(visualsShown==shown)return;visualsShown=shown;
            for(int i=0;i<modelRenderers.Length;i++)if(modelRenderers[i]!=null)modelRenderers[i].enabled=shown&&rendererDefaults[i];
        }
        void ChooseDestination()
        {
            int start=Map.Nearest(transform.position),best=-1;float score=-1;
            bool shortTrip=random.NextDouble()<.35;
            // Cover different physical areas, rather than revisiting different
            // grid cells around the same house. Short strolls alternate with trips.
            for(int attempt=0;attempt<64;attempt++)
            {
                int candidate=random.Next(Map.Nodes.Count);
                if(start<0||Map.Nodes[candidate].Links.Count<2||Map.Nodes[candidate].Component!=Map.Nodes[start].Component)continue;
                if(recent.Contains(candidate)||!Map.AwayFromInteractions(Map.Nodes[candidate].Point)||!director.CharacterSpaceFree(this,Map.Nodes[candidate].Point))continue;
                var p=Map.Nodes[candidate].Point;float distance=Mathf.Sqrt(StudentWalkMap.PlanarSquare(transform.position,p));
                if(distance<5||shortTrip&&distance>config.longTripDistance)continue;
                var area=new Vector2Int(Mathf.FloorToInt(p.x/20),Mathf.FloorToInt(p.z/20));
                int visits=visitedAreas.TryGetValue(area,out int value)?value:0;
                float candidateScore=(shortTrip?config.longTripDistance-distance:distance)/(1+visits*.75f)+Roll(0,6);
                if(candidateScore>score){score=candidateScore;best=candidate;}
            }
            if(best>=0&&Map.Path(start,best,route))
            {
                WalkSegments++;step=0;MovementSpeed=0;IsRunning=false;
                recent.Enqueue(best);while(recent.Count>6)recent.Dequeue();chosenAt=Time.time;
                var destination=Map.Nodes[best].Point;var area=new Vector2Int(Mathf.FloorToInt(destination.x/20),Mathf.FloorToInt(destination.z/20));
                visitedAreas[area]=(visitedAreas.TryGetValue(area,out int visits)?visits:0)+1;
                float length=0;var previous=transform.position;
                foreach(int index in route){length+=Vector3.Distance(previous,Map.Nodes[index].Point);previous=Map.Nodes[index].Point;}
                longTrip=director.AllowsRunning&&length>=config.longTripDistance;
                tripDeadline=chosenAt+Mathf.Clamp(length/Mathf.Max(.1f,config.walkSpeed)+15,25,150);
            }
            else {route.Clear();rest=Roll(1,2f);}
        }
        void OnDisable(){if(bubble!=null)bubble.Show(false,false,transform.position);}
    }
}
