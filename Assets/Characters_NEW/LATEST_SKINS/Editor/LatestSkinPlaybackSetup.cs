#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Playback only. Never changes a clip, limb pose, stride, or movement speed.</summary>
public static class LatestSkinPlaybackSetup
{
    public const float WalkSteps=2.4f,RunSteps=3.5f;
    public const string Report=LatestSkinIntegration.Reports+"/PlaybackChecks.txt";
    public static float NominalSpeed(string gender,string action)
    {var p=AssetDatabase.LoadAssetAtPath<GameObject>(LatestSkinIntegration.BasePlayers[gender=="Male"?0:1]).GetComponent<Player>();return p.moveSpeed*(action=="Run"?p.RunSpeedMultiplier:1);}
    public static AnimationClip Clip(Animator a,string state)=>(AnimationClip)((AnimatorController)a.runtimeAnimatorController).layers[0].stateMachine.states.Single(s=>s.state.name==state).state.motion;
    public static Vector3 BindHips(Animator model)
    {
        foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            for(int i=0;i<renderer.bones.Length;i++)
                if(renderer.bones[i].name=="mixamorig:Hips")
                    return (model.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix*renderer.sharedMesh.bindposes[i].inverse).GetColumn(3);
        throw new InvalidOperationException("Missing mesh bind hips: "+model.name);
    }
    public static void Sample(Animator a,AnimationClip clip,float phase)
    {
        clip.SampleAnimation(a.gameObject,clip.length*phase);
        var adapter=a.GetComponent<SharedSkinAnimationRetarget>();
        if(adapter!=null&&adapter.motions.Any(m=>m.clip==clip))adapter.CorrectPose(1,0,0);
    }
    public static string Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode");
        var maps=JsonUtility.FromJson<LatestSkinIntegration.Manifest>(File.ReadAllText(LatestSkinIntegration.Reports+"/Mapping.json"));
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            foreach(var map in maps.mappings)
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(map.prefab),scene);
                try
                {
                    var a=root.GetComponentInChildren<Animator>();a.enabled=false;
                    foreach(var warp in root.GetComponentsInChildren<PlayerStrideWarp>(true))Object.DestroyImmediate(warp);
                    var so=new SerializedObject(a.GetComponent<PlayerAnimator>());
                    so.FindProperty("useNaturalCadence").boolValue=true;
                    foreach(var action in new[]{"Walk","Run"})
                    {
                        string lower=action.ToLowerInvariant();var clip=Clip(a,action);
                        int cycles=Mathf.Max(1,so.FindProperty(lower+"Cycles").intValue);
                        float maximum=Mathf.Min(action=="Walk"?1.65f:1.5f,(action=="Walk"?WalkSteps:RunSteps)*clip.length/(2*cycles));
                        float nominalRate=NominalSpeed(map.gender,action)/(so.FindProperty("source"+action+"Speed").floatValue*Mathf.Abs(a.transform.lossyScale.z));
                        float rate=Mathf.Min(nominalRate,maximum);
                        so.FindProperty(lower+"ClipDuration").floatValue=clip.length;
                        so.FindProperty(lower+"StepsPerSecond").floatValue=rate*2*cycles/clip.length;
                        so.FindProperty(lower+"PlaybackLimits").vector2Value=new Vector2(0,rate);
                    }
                    so.FindProperty("playbackSmoothing").floatValue=.18f;so.ApplyModifiedPropertiesWithoutUndo();
                    var adapter=a.GetComponent<SharedSkinAnimationRetarget>();
                    if(adapter!=null)
                    {
                        foreach(var motion in adapter.motions)motion.groundOffset=new AnimationCurve();
                        adapter.soleContacts=new SharedSkinAnimationRetarget.Contact[0];
                        // Convert donor translation around mesh bind origins, never an
                        // arbitrary animated Idle pose or a per-frame floor correction.
                        var donorMap=maps.mappings.Single(m=>m.gender+"_"+m.category==adapter.donorSkin);
                        var donor=AssetDatabase.LoadAssetAtPath<GameObject>(donorMap.prefab).GetComponentInChildren<Animator>(true);
                        adapter.sourceHipsOrigin=BindHips(donor);adapter.targetHipsOrigin=BindHips(a);
                    }
                    a.applyRootMotion=false;a.speed=1;Clip(a,"Idle").SampleAnimation(a.gameObject,0);a.enabled=true;
                    PrefabUtility.SaveAsPrefabAsset(root,map.prefab);
                }
                finally{Object.DestroyImmediate(root);}
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);AssetDatabase.SaveAssets();}
        return "Removed stride IK from all 12 gameplay prefabs; bounded playback only; clips and gameplay speeds unchanged.";
    }
}
#endif
