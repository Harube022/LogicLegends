using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public static class WizardRoamingSetup
{
    const string Root="Assets/Characters_NEW/LATEST_SKINS/Wizard";
    public static string Apply()
    {
        if(Application.isPlaying)throw new Exception("Use Edit Mode.");
        var owner=UnityEngine.Object.FindFirstObjectByType<LogicGardenIntroduction>();
        if(owner==null||owner.gameObject.scene.path!="Assets/Scenes/LogicGarden.unity")throw new Exception("Open LogicGarden.");
        Configure(owner);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);EditorSceneManager.SaveScene(owner.gameObject.scene);
        return "Text-only wizards, sustained greeting, two extracted walks, library-only navigation configured.";
    }
    public static void Configure(LogicGardenIntroduction owner)
    {
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/Animations/Wizard.controller");
        var machine=controller.layers[0].stateMachine;
        if(!controller.parameters.Any(p=>p.name=="WalkPlayback"))controller.AddParameter("WalkPlayback",AnimatorControllerParameterType.Float);
        var parameters=controller.parameters;foreach(var p in parameters)if(p.name=="WalkPlayback")p.defaultFloat=1;controller.parameters=parameters;
        foreach(string name in new[]{"Walking","Walking1"})
        {
            var source=AssetDatabase.LoadAllAssetsAtPath(Root+"/"+name+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
            string path=Root+"/Animations/Wizard_"+name+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(clip==null){clip=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(clip,path);}else EditorUtility.CopySerialized(source,clip);
            clip.name="Wizard_"+name;var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
            var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==clip.name)??machine.AddState(clip.name);
            state.motion=clip;state.speed=1;state.speedParameter="WalkPlayback";state.speedParameterActive=true;state.iKOnFeet=false;
            foreach(var transition in state.transitions)state.RemoveTransition(transition);
        }
        var wave=machine.states.Single(s=>s.state.name=="Wizard_Wave").state;
        foreach(var transition in wave.transitions)wave.RemoveTransition(transition);
        // Blend the authored greeting back into itself; never cut at a loop seam.
        var repeat=wave.AddTransition(wave);repeat.hasExitTime=true;repeat.exitTime=.95f;repeat.hasFixedDuration=true;repeat.duration=.22f;repeat.canTransitionToSelf=true;
        EditorUtility.SetDirty(controller);
        var prefab=PrefabUtility.LoadPrefabContents(Root+"/Prefabs/Wizard.prefab");
        try
        {
            var prefabActor=prefab.GetComponent<WizardActor>();
            prefabActor.walkingNaturalSpeed=NaturalSpeed(prefabActor,"Walking");prefabActor.walking1NaturalSpeed=NaturalSpeed(prefabActor,"Walking1");
            PrefabUtility.SaveAsPrefabAsset(prefab,Root+"/Prefabs/Wizard.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        var legacy=owner.GetComponent<WizardDialogueVoice>();if(legacy!=null)UnityEngine.Object.DestroyImmediate(legacy);
        foreach(var wizard in UnityEngine.Object.FindObjectsByType<WizardInteraction>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(w=>w.gameObject.scene==owner.gameObject.scene))
        {
            var data=new SerializedObject(wizard);data.FindProperty("allowVoiceOver").boolValue=false;
            data.FindProperty("wizardVoiceSource").objectReferenceValue=null;
            foreach(var field in new[]{"dialogueEntries","finalDialogueEntries","successEntries","failEntries"})
            {var array=data.FindProperty(field);for(int i=0;i<array.arraySize;i++)array.GetArrayElementAtIndex(i).FindPropertyRelative("voiceClip").objectReferenceValue=null;}
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var actor=owner.LibraryWizard;
        var agent=actor.GetComponent<NavMeshAgent>();if(agent==null)agent=actor.gameObject.AddComponent<NavMeshAgent>();
        int type=-1;for(int i=0;i<NavMesh.GetSettingsCount();i++){var setting=NavMesh.GetSettingsByIndex(i);if(NavMesh.GetSettingsNameFromID(setting.agentTypeID)=="Library Wizard")type=setting.agentTypeID;}
        if(type==-1){var setting=NavMesh.CreateSettings();type=setting.agentTypeID;}
        // Store dedicated dimensions without changing player/multiplayer agent types.
        var navSettings=NavMesh.GetSettingsByID(type);navSettings.agentRadius=1.25f;navSettings.agentHeight=actor.standingHeight;navSettings.agentClimb=.85f;navSettings.agentSlope=35;
        var navigationSettings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
        var agents=navigationSettings.FindProperty("m_Settings");
        var names=navigationSettings.FindProperty("m_SettingNames");
        for(int i=0;i<agents.arraySize;i++)
        {var item=agents.GetArrayElementAtIndex(i);if(item.FindPropertyRelative("agentTypeID").intValue==type){names.GetArrayElementAtIndex(i).stringValue="Library Wizard";item.FindPropertyRelative("agentRadius").floatValue=1.25f;item.FindPropertyRelative("agentHeight").floatValue=actor.standingHeight;item.FindPropertyRelative("agentClimb").floatValue=.85f;item.FindPropertyRelative("agentSlope").floatValue=35;}}
        navigationSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(navigationSettings.targetObject);
        agent.agentTypeID=type;agent.radius=1.25f;agent.height=actor.standingHeight;agent.baseOffset=0;agent.speed=2.4f;agent.acceleration=6;agent.angularSpeed=180;agent.stoppingDistance=.35f;
        agent.updatePosition=false;agent.updateRotation=false;agent.autoBraking=true;agent.autoTraverseOffMeshLink=false;agent.obstacleAvoidanceType=ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        var roaming=actor.GetComponent<WizardLibraryRoaming>();if(roaming==null)roaming=actor.gameObject.AddComponent<WizardLibraryRoaming>();roaming.agent=agent;roaming.actor=actor;
        var library=UnityEngine.Object.FindFirstObjectByType<StudyLibraryController>();
        var navigation=library.transform.Find("Wizard Library Navigation");if(navigation==null){navigation=new GameObject("Wizard Library Navigation").transform;navigation.SetParent(library.transform,false);}
        var surface=navigation.GetComponent<NavMeshSurface>();if(surface==null)surface=navigation.gameObject.AddComponent<NavMeshSurface>();
        surface.agentTypeID=type;surface.collectObjects=CollectObjects.Volume;surface.center=new Vector3(-126,24.5f,670.5f);surface.size=new Vector3(40,12,38);
        surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.ignoreNavMeshAgent=true;surface.overrideVoxelSize=true;surface.voxelSize=.15f;surface.minRegionArea=2;
        surface.BuildNavMesh();if(surface.navMeshData==null)throw new Exception("Library navigation bake failed.");
        Directory.CreateDirectory(Root+"/Navigation");AssetDatabase.Refresh();string navPath=Root+"/Navigation/StudyLibraryWizard.asset";
        var oldData=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);var newData=surface.navMeshData;
        if(oldData==null)AssetDatabase.CreateAsset(newData,navPath);
        else{surface.RemoveData();EditorUtility.CopySerialized(newData,oldData);surface.navMeshData=oldData;surface.AddData();UnityEngine.Object.DestroyImmediate(newData);EditorUtility.SetDirty(oldData);}
        NavMeshHit ground;
        if(NavMesh.SamplePosition(actor.transform.position,out ground,2,new NavMeshQueryFilter{agentTypeID=type,areaMask=agent.areaMask}))
            actor.transform.position=ground.position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(actor);EditorUtility.SetDirty(roaming);EditorUtility.SetDirty(surface);
    }
    static float NaturalSpeed(WizardActor actor,string source)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/Wizard_"+source+".anim");
        var model=actor.animator.gameObject;
        var binding=AnimationUtility.GetCurveBindings(clip).Single(b=>b.path.EndsWith("mixamorig:Hips")&&b.propertyName=="m_LocalPosition.z");
        var curve=AnimationUtility.GetEditorCurve(clip,binding);
        var hips=model.transform.Find(binding.path);
        float natural=Mathf.Abs(curve.Evaluate(clip.length)-curve.Evaluate(0))*hips.parent.lossyScale.z/clip.length;
        if(source=="Walking"){actor.walkingRootZ=new AnimationCurve(curve.keys);actor.walkingDuration=clip.length;}
        else{actor.walking1RootZ=new AnimationCurve(curve.keys);actor.walking1Duration=clip.length;}
        if(natural<.4f||natural>8)throw new Exception("Unexpected authored walk speed "+natural);
        return natural;
    }
}
