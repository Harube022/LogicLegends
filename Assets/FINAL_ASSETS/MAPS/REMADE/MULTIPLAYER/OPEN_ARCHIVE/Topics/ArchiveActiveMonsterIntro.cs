using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>A local, visual-only preview of the already selected primary guardian.</summary>
    public sealed class ArchiveActiveMonsterIntro : IDisposable
    {
        readonly Transform owner;
        readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
        KhezuHuntingController source;
        RuntimeAnimatorController controller;
        GameObject prefab,stage,model;
        Renderer[] visuals=Array.Empty<Renderer>();
        AnimationClip clip;
        PlayableGraph graph;
        AnimationClipPlayable motion;
        ArchiveGuardianEntranceVfx effects;
        Light key,rim;
        Bounds framing;
        bool framingReady,unsupported,disposed;

        public bool Visible {get;private set;}
        public string SpeciesName {get;private set;}="Guardian";
        public string SpeciesTitle {get;private set;}="THE ARCHIVE GUARDIAN";
        public Color Tint {get;private set;}=new Color(1,.055f,.14f,1);
        public string AnimationName=>clip!=null?clip.name:"";
        public AudioClip Roar {get;private set;}
        public int RendererCount=>Visible?visuals.Length+(effects!=null?effects.ActiveRendererCount:0):0;

        public ArchiveActiveMonsterIntro(Transform owner)
        {this.owner=owner!=null?owner:throw new ArgumentNullException(nameof(owner));}

        public bool Present(KhezuHuntingController current,float progress,bool reducedMotion,out Bounds bounds)
        {
            bounds=default;
            if(disposed||current==null||!current.isActiveAndEnabled||!current.Initialized||progress<0||progress>=1)
            {Hide();return false;}
            var actorAnimator=current.GetComponent<Animator>();
            var cinematic=current.GetComponent<KhezuCinematicJumpscareController>();
            var nextPrefab=cinematic!=null?cinematic.presentationPrefab:null;
            var nextController=actorAnimator!=null?actorAnimator.runtimeAnimatorController:null;
            // Counter-attack settings are independent of the selected visual rig.
            var rigName=(nextPrefab!=null?nextPrefab.name:"")+" "+(nextController!=null?nextController.name:"");
            bool rajang=rigName.IndexOf("primal",StringComparison.OrdinalIgnoreCase)>=0||rigName.IndexOf("rajang",StringComparison.OrdinalIgnoreCase)>=0;
            if(source!=current||prefab!=nextPrefab||controller!=nextController||SpeciesName!=(rajang?"Rajang":"Khezu"))
            {
                ReleaseStage();source=current;prefab=nextPrefab;controller=nextController;
                SpeciesName=rajang?"Rajang":"Khezu";
                SpeciesTitle=rajang?"THE PRIMAL AXIOM":"THE PROOF DEVOURER";
                Tint=rajang?new Color(1,.35f,.035f,1):new Color(1,.055f,.14f,1);
                Roar=cinematic!=null?cinematic.monsterRoar:null;
                unsupported=!Build(actorAnimator);
            }
            if(unsupported||stage==null||!graph.IsValid()){Hide();return false;}
            stage.SetActive(true);
            if(!graph.IsPlaying())graph.Play();
            stage.transform.SetPositionAndRotation(current.transform.position,current.transform.rotation);
            HideSourceVisuals(current);
            // Only the detached rig is sampled. The live Animator, root, mistake
            // counters, navigation and encounter state are never changed.
            motion.SetTime(Mathf.Clamp01(progress)*Mathf.Max(.001f,clip.length));
            graph.Evaluate(0);
            model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
            model.transform.localScale=Vector3.one*Mathf.Max(.001f,Mathf.Abs(current.transform.lossyScale.x));
            var currentBounds=ReadBounds();
            if(!framingReady){framing=currentBounds;framingReady=true;}else framing.Encapsulate(currentBounds);
            bounds=framing;
            effects.Present(stage.transform,bounds,Tint,Mathf.Clamp(progress,.001f,.999f),reducedMotion);
            Illuminate(bounds);
            Visible=true;return true;
        }
        static string Key(string value)=>new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        bool Build(Animator original)
        {
            if(prefab==null||controller==null||original==null||!SafePrefab(prefab))return false;
            var clips=controller.animationClips;
            clip=new[]{"fullawaken","wake03","idle"}.Select(name=>clips.FirstOrDefault(c=>c!=null&&Key(c.name)==name)).FirstOrDefault(c=>c!=null);
            if(clip==null)return false;
            stage=new GameObject("Active_Guardian_Local_Intro"){hideFlags=HideFlags.DontSave};
            stage.transform.SetParent(owner,false);
            var scale=owner.lossyScale;
            stage.transform.localScale=new Vector3(Inverse(scale.x),Inverse(scale.y),Inverse(scale.z));
            stage.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
            model=UnityEngine.Object.Instantiate(prefab,stage.transform);
            model.name="Existing_Selected_Guardian_Visual_Only";
            model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
            model.transform.localScale=Vector3.one*Mathf.Max(.001f,Mathf.Abs(source.transform.lossyScale.x));
            var animator=model.GetComponent<Animator>();
            animator.runtimeAnimatorController=null;
            if(original.avatar!=null)animator.avatar=original.avatar;
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            visuals=model.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            foreach(var renderer in visuals)
            {renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;}
            graph=PlayableGraph.Create("Archive_Selected_Guardian_Intro");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            motion=AnimationClipPlayable.Create(graph,clip);motion.SetSpeed(0);motion.SetApplyFootIK(false);motion.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph,"Existing Selected Guardian Rig",animator).SetSourcePlayable(motion);
            graph.Play();motion.SetTime(0);graph.Evaluate(0);
            effects=stage.AddComponent<ArchiveGuardianEntranceVfx>();effects.radiusScale=.85f;effects.glowIntensity=1.35f;effects.Initialize();
            key=Spot("Guardian_Intro_Key",new Color(.82f,.90f,1),440);
            rim=Spot("Guardian_Intro_Rim",Color.Lerp(Tint,Color.white,.20f),560);
            stage.SetActive(false);return true;
        }
        static bool SafePrefab(GameObject candidate)
        {
            if(candidate.GetComponent<Animator>()==null)return false;
            // Validate before Instantiate, so no gameplay Awake/OnEnable can run.
            foreach(var component in candidate.GetComponentsInChildren<Component>(true))
                if(component==null||!(component is Transform||component is Animator||component is Renderer||component is MeshFilter||component is LODGroup))return false;
            return true;
        }
        static float Inverse(float value)=>Mathf.Abs(value)>.0001f?1/value:1;
        void HideSourceVisuals(KhezuHuntingController current)
        {
            foreach(var renderer in current.GetComponentsInChildren<Renderer>(true))
            {
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                if(!hidden.ContainsKey(renderer))hidden[renderer]=renderer.forceRenderingOff;
                renderer.forceRenderingOff=true;
            }
        }
        Bounds ReadBounds()
        {
            bool first=true;Bounds result=default;
            foreach(var renderer in visuals)
            {
                if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                var b=renderer.bounds;
                if(float.IsNaN(b.center.x)||float.IsInfinity(b.center.x)||b.size.sqrMagnitude<.001f||b.size.magnitude>150)continue;
                if(first){result=b;first=false;}else result.Encapsulate(b);
            }
            if(first)result=new Bounds(source.transform.position+Vector3.up*5,new Vector3(12,10,12));
            return result;
        }
        Light Spot(string name,Color tint,float intensity)
        {
            var go=new GameObject(name);go.transform.SetParent(stage.transform,false);
            var light=go.AddComponent<Light>();light.type=LightType.Spot;light.color=tint;light.intensity=intensity;
            light.shadows=LightShadows.None;light.spotAngle=95;light.innerSpotAngle=65;light.renderMode=LightRenderMode.Auto;light.enabled=false;return light;
        }
        void Illuminate(Bounds bounds)
        {
            float size=Mathf.Max(7,bounds.size.magnitude);
            var front=source.transform.forward;var right=source.transform.right;
            var a=bounds.center+front*size*.7f+Vector3.up*size*.6f;
            var b=bounds.center-front*size*.45f+right*size*.5f+Vector3.up*size*.45f;
            key.transform.SetPositionAndRotation(a,Quaternion.LookRotation(bounds.center-a));rim.transform.SetPositionAndRotation(b,Quaternion.LookRotation(bounds.center-b));
            key.range=rim.range=Mathf.Max(40,size*3);key.enabled=rim.enabled=true;
        }
        public void Hide()
        {
            foreach(var entry in hidden)if(entry.Key!=null)entry.Key.forceRenderingOff=entry.Value;
            hidden.Clear();Visible=false;framingReady=false;
            if(graph.IsValid())graph.Stop();
            if(effects!=null)effects.Clear();if(key!=null)key.enabled=false;if(rim!=null)rim.enabled=false;
            if(stage!=null)stage.SetActive(false);
        }
        void ReleaseStage()
        {
            Hide();if(graph.IsValid())graph.Destroy();if(effects!=null)effects.TearDown();
            if(stage!=null)Destroy(stage);
            stage=model=null;effects=null;key=rim=null;visuals=Array.Empty<Renderer>();clip=null;framingReady=false;unsupported=false;
        }
        public void Dispose(){if(disposed)return;ReleaseStage();disposed=true;source=null;prefab=null;controller=null;}
        static void Destroy(UnityEngine.Object value)
        {if(value!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}}
    }
}
