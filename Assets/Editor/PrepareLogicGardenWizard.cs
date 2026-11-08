using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

/// <summary>Idempotent, scene-local preparation; leaves player assets and account data alone.</summary>
public static class PrepareLogicGardenWizard
{
    const string Root="Assets/Characters_NEW/LATEST_SKINS/Wizard";
    public static readonly string[] Sources={"IDLE","Agree","Angry Point","Capoeira","Cheering","Fist Pump","Float","Hands Forward Gesture","Hard Head Nod","Idle 1","Idle 2","Nod","Wave","Walking","Walking1"};
    public static string ClipName(string source){return "Wizard_"+(source=="IDLE"?"Idle":source.Replace(" ",""));}
    public static string Run()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Prepare in Edit Mode.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/LogicGarden.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved LogicGarden scene first.");
        Directory.CreateDirectory("Library/WizardQA");
        if(!File.Exists("Library/WizardQA/LogicGarden-before-wizard.unity"))File.Copy(scene.path,"Library/WizardQA/LogicGarden-before-wizard.unity");
        foreach(var folder in new[]{"Animations","Prefabs","Voiceover"})Directory.CreateDirectory(Root+"/"+folder);
        AssetDatabase.Refresh();
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/IDLE.fbx")!=null)
        {var error=AssetDatabase.MoveAsset(Root+"/IDLE.fbx",Root+"/Wizard.fbx");if(!string.IsNullOrEmpty(error))throw new Exception(error);}
        foreach(string source in Sources)
        {
            string path=Root+"/"+(source=="IDLE"?"Wizard":source)+".fbx";
            var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
            string output=Root+"/Animations/"+ClipName(source)+".anim";
            var extracted=AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
            if(extracted==null){extracted=UnityEngine.Object.Instantiate(clip);AssetDatabase.CreateAsset(extracted,output);}
            else EditorUtility.CopySerialized(clip,extracted);
            extracted.name=ClipName(source);
            var settings=AnimationUtility.GetAnimationClipSettings(extracted);settings.loopTime=source=="IDLE";AnimationUtility.SetAnimationClipSettings(extracted,settings);EditorUtility.SetDirty(extracted);
        }
        string controllerPath=Root+"/Animations/Wizard.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine=controller.layers[0].stateMachine;
        foreach(var item in machine.states)machine.RemoveState(item.state);
        AnimatorState idle=null;
        foreach(string source in Sources)
        {var state=machine.AddState(ClipName(source));state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+ClipName(source)+".anim");state.speed=1;state.iKOnFeet=false;if(source=="IDLE")idle=state;}
        machine.defaultState=idle;
        foreach(var item in machine.states)if(item.state!=idle)
        {var transition=item.state.AddTransition(idle);transition.hasExitTime=true;transition.exitTime=1;transition.hasFixedDuration=true;transition.duration=.22f;transition.canTransitionToSelf=false;}
        EditorUtility.SetDirty(controller);
        var reference=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters_NEW/LATEST_SKINS/Male_Skins/Common/Prefabs/Male_Common_Gameplay.prefab");
        Bounds playerBounds;if(!IntegratedSkinProfile.TryVisualBounds(reference.transform,out playerBounds))throw new Exception("Player height reference missing.");
        float height=playerBounds.size.y;
        var root=new GameObject("Wizard");var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Wizard.fbx"));
        model.transform.SetParent(root.transform,false);model.name="Wizard Visual";
        var idleClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/Wizard_Idle.anim");idleClip.SampleAnimation(model,0);
        Bounds raw=VisualBounds(model);float scale=height/raw.size.y;
        model.transform.localScale=Vector3.one*scale;model.transform.localPosition=Vector3.up*(-raw.min.y*scale);
        var animator=model.GetComponent<Animator>();if(animator==null)animator=model.AddComponent<Animator>();
        animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.updateMode=AnimatorUpdateMode.UnscaledTime;
        foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())renderer.updateWhenOffscreen=true;
        var actor=root.AddComponent<WizardActor>();actor.animator=animator;actor.standingHeight=height;
        var capsule=root.AddComponent<CapsuleCollider>();capsule.height=height;capsule.radius=.65f;capsule.center=Vector3.up*height*.5f;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Wizard.prefab");UnityEngine.Object.DestroyImmediate(root);
        var owner=UnityEngine.Object.FindFirstObjectByType<LogicGardenIntroduction>();if(owner==null)throw new Exception("Garden controller missing.");
        var anchor=owner.Wizard;
        var oldAnimator=anchor.GetComponent<Animator>();if(oldAnimator!=null)UnityEngine.Object.DestroyImmediate(oldAnimator);
        foreach(var child in anchor.Cast<Transform>().Where(t=>t.name=="Armature.001"||t.name=="Cube.002"||t.name=="Cube.023"||t.GetComponent<WizardActor>()!=null).ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
        var garden=(GameObject)PrefabUtility.InstantiatePrefab(prefab);garden.name="Lefyahj Wizard Visual";garden.transform.SetParent(anchor,false);var gardenActor=garden.GetComponent<WizardActor>();gardenActor.greetOnSpawn=true;
        var name=anchor.Find("FloatingName");if(name!=null)name.localPosition=new Vector3(0,height+.6f,0);
        var marker=anchor.Find("Introduction Attention Marker");if(marker!=null)marker.localPosition=new Vector3(0,height+1.6f,0);
        var library=UnityEngine.Object.FindFirstObjectByType<StudyLibraryController>();
        var oldLibrary=library.transform.Find("Lefyahj Library Wizard");if(oldLibrary!=null)UnityEngine.Object.DestroyImmediate(oldLibrary.gameObject);
        var librarian=(GameObject)PrefabUtility.InstantiatePrefab(prefab);librarian.name="Lefyahj Library Wizard";librarian.transform.SetParent(library.transform,false);
        librarian.transform.position=new Vector3(-133,21.09451f,675);
        var direction=new Vector3(-126.81f,21.09451f,670.88f)-librarian.transform.position;direction.y=0;librarian.transform.rotation=Quaternion.LookRotation(direction);
        var libraryActor=librarian.GetComponent<WizardActor>();
        if(name!=null){var label=UnityEngine.Object.Instantiate(name.gameObject,librarian.transform);label.name="FloatingName";label.transform.localPosition=new Vector3(0,height+.6f,0);}
        var serialized=new SerializedObject(owner);serialized.FindProperty("gardenWizardActor").objectReferenceValue=gardenActor;serialized.FindProperty("libraryWizardActor").objectReferenceValue=libraryActor;serialized.ApplyModifiedPropertiesWithoutUndo();
        WizardRoamingSetup.Configure(owner);
        var button=serialized.FindProperty("returnToGardenButton").objectReferenceValue as UnityEngine.UI.Button;if(button!=null){button.gameObject.SetActive(false);button.interactable=false;}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Prepared 15 matching clips and two text-only Wizard instances. Player height: "+height+"; uniform model scale: "+scale;
    }
    public static Bounds VisualBounds(GameObject root)
    {
        var bounds=new Bounds();bool found=false;
        foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {var mesh=new Mesh();renderer.BakeMesh(mesh,true);foreach(var point in mesh.vertices){var world=renderer.transform.TransformPoint(point);if(!found){bounds=new Bounds(world,Vector3.zero);found=true;}else bounds.Encapsulate(world);}UnityEngine.Object.DestroyImmediate(mesh);}
        if(!found)throw new Exception("Wizard mesh missing.");return bounds;
    }
    [Serializable]class VoiceManifest {public VoiceLine[] lines;}
    [Serializable]class VoiceLine {public string id,text;}
}
