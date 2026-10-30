using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveWorldProgression
    {
        ArchiveGuardianEntranceVfx entranceEffects;
        AudioSource entranceCharge,entranceHit;
        Light entranceKey,entranceRim;
        bool entranceImpactPlayed,filmPlanReady,subjectReady;
        Renderer[] filmVisuals=System.Array.Empty<Renderer>();
        Bounds filmSubjectBounds;
        float filmYaw,filmTilt;
        readonly RaycastHit[] filmHits=new RaycastHit[32];
        readonly List<CameraRenderState> suspendedCameraRendering=new List<CameraRenderState>();
        struct CameraRenderState {public Camera camera;public int mask;public CameraClearFlags clear;}
        public Camera FilmCamera=>filmCamera;
        public ArchiveGuardianEntranceVfx EntranceEffects=>entranceEffects;
        Color EntranceTint=>SecondaryName=="Rajang"?new Color(1,.35f,.035f):new Color(1,.055f,.14f);

        void BuildEntrancePresentation()
        {
            entranceEffects=GetComponent<ArchiveGuardianEntranceVfx>();
            if(entranceEffects==null)entranceEffects=gameObject.AddComponent<ArchiveGuardianEntranceVfx>();
            entranceEffects.Initialize();
            entranceCharge=Voice("Guardian_Entrance_Charge",entranceRise,false,.29f);
            entranceHit=Voice("Guardian_Entrance_Impact",entranceImpact,false,.52f);
            entranceCharge.priority=100;entranceHit.priority=80;
            entranceKey=EntranceLight("Guardian_Reveal_Key",new Color(.68f,.80f,1));
            entranceRim=EntranceLight("Guardian_Reveal_Rim",Color.white);
        }
        Light EntranceLight(string name,Color tint)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var light=go.AddComponent<Light>();
            light.type=LightType.Spot;light.color=tint;light.shadows=LightShadows.None;
            light.range=65;light.spotAngle=95;light.innerSpotAngle=65;light.renderMode=LightRenderMode.Auto;
            light.enabled=false;return light;
        }
        void IlluminateEntrance(Bounds bounds,float t)
        {
            if(entranceKey==null||entranceRim==null)return;
            // Two bounded, shadowless lights only during the covered film. These
            // reveal the real rig without changing the world's night exposure.
            float pulse=ReducedEntranceMotion?0:Mathf.Max(0,1-Mathf.Abs(t-.49f)/.10f);
            var focus=bounds.center;
            entranceKey.transform.SetPositionAndRotation(focus+secondary.transform.forward*14+Vector3.up*11,
                Quaternion.LookRotation(-secondary.transform.forward*14-Vector3.up*11));
            entranceKey.range=Mathf.Max(40,bounds.size.magnitude*3);entranceKey.intensity=520+pulse*100;entranceKey.enabled=true;
            var rimOffset=-secondary.transform.forward*9+secondary.transform.right*9+Vector3.up*8;
            entranceRim.transform.SetPositionAndRotation(focus+rimOffset,Quaternion.LookRotation(-rimOffset));
            entranceRim.color=Color.Lerp(EntranceTint,Color.white,.25f);entranceRim.intensity=680+pulse*130;
            entranceRim.range=entranceKey.range;entranceRim.enabled=true;
        }
        void UpdateEntranceSound(float elapsed)
        {
            float t=elapsed/Mathf.Max(.1f,entranceSeconds);
            if(!heardEntrance)
            {
                heardEntrance=true;
                // Late joiners resume the shared shot; they do not hear a stale
                // opening swell or delayed shock after the reveal already happened.
                if(t>=0&&t<.20f&&entranceCharge!=null)
                {entranceCharge.pitch=SecondaryName=="Rajang"?.78f:1.05f;entranceCharge.Play();}
            }
            if(!entranceImpactPlayed&&t>=.46f)
            {
                entranceImpactPlayed=true;
                if(t<.72f)
                {
                    if(entranceHit!=null)entranceHit.Play();
                    if(roar!=null){roar.pitch=SecondaryName=="Rajang"?.82f:1;roar.Play();}
                }
            }
        }
        void PresentEntranceEffects(bool visible,float elapsed)
        {
            if(!visible||secondary==null||!secondary.Initialized)
            {entranceEffects?.Clear();return;}
            entranceEffects?.Present(secondary.transform,ReadFilmBounds(),EntranceTint,
                Mathf.Clamp01(elapsed/Mathf.Max(.1f,entranceSeconds)),ReducedEntranceMotion);
        }
        void ClearEntrancePresentation()
        {
            entranceEffects?.Clear();RestoreGameplayCameraRendering();
            if(filmCamera!=null)filmCamera.enabled=false;
            if(entranceKey!=null)entranceKey.enabled=false;if(entranceRim!=null)entranceRim.enabled=false;
            if(entranceCharge!=null)entranceCharge.Stop();if(entranceHit!=null)entranceHit.Stop();if(roar!=null)roar.Stop();
        }
        void ResetFilmSubject()
        {
            subjectReady=filmPlanReady=false;
            filmVisuals=secondary==null?System.Array.Empty<Renderer>():secondary.GetComponentsInChildren<Renderer>(true)
                .Where(r=>(r is MeshRenderer||r is SkinnedMeshRenderer)&&r.enabled&&r.gameObject.activeInHierarchy).ToArray();
        }
        Bounds ReadFilmBounds()
        {
            var fallback=secondary.GetComponent<BoxCollider>().bounds;
            Bounds current=default;bool found=false;
            foreach(var renderer in filmVisuals)
            {
                if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                var b=renderer.bounds;
                if(b.size.sqrMagnitude<.001f||b.size.magnitude>100||float.IsNaN(b.center.x))continue;
                if(!found){current=b;found=true;}else current.Encapsulate(b);
            }
            if(!found)current=fallback;
            // Imported skinned bounds include the mane, horns, mouth and claws.
            // Accumulate the wake's extents to avoid a crop or breathing zoom as
            // the existing Full_Awaken animation changes pose.
            if(!subjectReady){filmSubjectBounds=current;subjectReady=true;}
            else filmSubjectBounds.Encapsulate(current);
            return filmSubjectBounds;
        }
        static Vector3[] Corners(Bounds b)
        {
            var points=new Vector3[8];int index=0;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                points[index++]=b.center+Vector3.Scale(b.extents,new Vector3(x,y,z));
            return points;
        }
        float FitFilmDistance(Bounds bounds,Vector3 outward,Vector3 focus)
        {
            var right=Vector3.Cross(Vector3.up,-outward).normalized;
            var up=Vector3.Cross(-outward,right).normalized;
            float vertical=Mathf.Tan(46*Mathf.Deg2Rad*.5f)*.65f;
            float horizontal=Mathf.Tan(46*Mathf.Deg2Rad*.5f)*Mathf.Max(.5f,filmCamera.aspect)*.84f;
            float distance=4;
            foreach(var point in Corners(bounds))
            {
                var p=point-focus;float depth=Vector3.Dot(outward,p);
                distance=Mathf.Max(distance,depth+Mathf.Abs(Vector3.Dot(p,right))/horizontal,depth+Mathf.Abs(Vector3.Dot(p,up))/vertical);
            }
            return distance+.7f;
        }
        int FilmObstructions(Vector3 cameraPosition,Bounds bounds)
        {
            int blocked=0;
            var targets=new[]{bounds.center,bounds.center+Vector3.up*bounds.extents.y*.65f,
                bounds.center+secondary.transform.right*bounds.extents.x*.65f,bounds.center-secondary.transform.right*bounds.extents.x*.65f};
            foreach(var target in targets)
            {
                var delta=cameraPosition-target;float distance=delta.magnitude;
                int count=Physics.RaycastNonAlloc(target,delta.normalized,filmHits,distance,~0,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)
                {
                    var collider=filmHits[i].collider;
                    if(collider==null||collider.transform.IsChildOf(secondary.transform)||filmHits[i].distance>=distance-.3f)continue;
                    blocked++;break;
                }
            }
            return blocked;
        }
        void PlanFilmCamera(Bounds bounds)
        {
            float best=float.PositiveInfinity;
            foreach(float yaw in new[]{-55f,-30f,0f,30f,55f})foreach(float tilt in new[]{.25f,.43f})
            {
                var direction=(Quaternion.Euler(0,yaw,0)*secondary.transform.forward+Vector3.up*tilt).normalized;
                float distance=FitFilmDistance(bounds,direction,bounds.center)*1.15f;
                float score=FilmObstructions(bounds.center+direction*distance,bounds)*100+Mathf.Abs(yaw)*.025f+tilt;
                if(score>=best)continue;best=score;filmYaw=yaw;filmTilt=tilt;
            }
            filmPlanReady=true;
        }
        void UpdateFilmCamera(float t)
        {
            if(secondary==null||filmCamera==null)return;
            if(filmVisuals.Length==0)ResetFilmSubject();
            var bounds=ReadFilmBounds();if(!filmPlanReady)PlanFilmCamera(bounds);
            bool reduced=ReducedEntranceMotion;
            float push=reduced?.5f:Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.85f,t));
            float yaw=filmYaw+(reduced?0:Mathf.Lerp(-3.5f,3.5f,push));
            var outward=(Quaternion.Euler(0,yaw,0)*secondary.transform.forward+Vector3.up*filmTilt).normalized;
            filmCamera.fieldOfView=reduced?50:Mathf.Lerp(54,46,push);
            float distance=FitFilmDistance(bounds,outward,bounds.center)*(reduced?1.08f:Mathf.Lerp(1.16f,1.02f,push));
            var focus=bounds.center;var position=focus+outward*distance;
            if(!reduced)
            {
                // A single short impact beat, not continuous high-frequency shake.
                float hit=Mathf.Max(0,1-Mathf.Abs(t-.49f)/.055f);
                position+=secondary.transform.right*(Mathf.Sin(t*140)*.12f*hit);
            }
            filmCamera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(focus-position));
            IlluminateEntrance(bounds,t);
        }
        void SuppressGameplayCameraRendering()
        {
            // Retain enabled MainCamera/brain references for player input and UI.
            // Only its covered world rendering is suppressed during the opaque film.
            foreach(var camera in Camera.allCameras)
            {
                if(camera==filmCamera||!camera.enabled||camera.cameraType!=CameraType.Game||camera.targetTexture!=null||camera.cullingMask==0||
                    suspendedCameraRendering.Any(s=>s.camera==camera))continue;
                suspendedCameraRendering.Add(new CameraRenderState{camera=camera,mask=camera.cullingMask,clear=camera.clearFlags});
                camera.cullingMask=0;camera.clearFlags=CameraClearFlags.Nothing;
            }
        }
        void RestoreGameplayCameraRendering()
        {
            foreach(var state in suspendedCameraRendering)if(state.camera!=null){state.camera.cullingMask=state.mask;state.camera.clearFlags=state.clear;}
            suspendedCameraRendering.Clear();
        }
    }
}
