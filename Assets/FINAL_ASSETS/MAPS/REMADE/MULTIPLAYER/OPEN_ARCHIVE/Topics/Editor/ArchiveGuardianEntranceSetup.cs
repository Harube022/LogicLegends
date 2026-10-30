using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveGuardianEntranceSetup
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Environment/Configure Night Guardian Entrance")]
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty||scene.path!="Assets/Scenes/MultiplayerGameplay.unity")throw new InvalidOperationException("Use the saved production scene outside Play Mode.");
            var world=UnityEngine.Object.FindFirstObjectByType<ArchiveWorldProgression>();
            if(world==null||world.secondary==null)throw new InvalidOperationException("The existing second guardian is required.");
            string shaderPath=ArchiveTopicSetup.Folder+"/Environment/ArchiveGuardianEntranceEnergy.shader";
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if(shader==null||ShaderUtil.ShaderHasError(shader)||!shader.isSupported)throw new InvalidOperationException("Entrance energy shader failed to import.");
            string folder=ArchiveTopicSetup.Folder+"/Environment/Resources/";
            ConfigureMaterial(folder+"ArchiveGuardianEntranceEnergy.mat",shader,true);
            ConfigureMaterial(folder+"ArchiveGuardianEntranceDust.mat",shader,false);
            const string audio="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Cinematics/Audio/";
            world.entranceRise=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"Khezu_Tense_Growl.wav");
            world.entranceImpact=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"Khezu_Impact_Accent.wav");
            if(world.entranceRise==null||world.entranceImpact==null)throw new InvalidOperationException("Existing cinematic sound accents are required.");
            if(world.GetComponent<ArchiveGuardianEntranceVfx>()==null)world.gameObject.AddComponent<ArchiveGuardianEntranceVfx>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(world);EditorUtility.SetDirty(world);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        static void ConfigureMaterial(string path,Shader shader,bool energy)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;material.enableInstancing=true;
            material.SetFloat("_SrcBlend",(float)(energy?BlendMode.SrcAlpha:BlendMode.One));
            material.SetFloat("_DstBlend",(float)(energy?BlendMode.One:BlendMode.Zero));
            material.SetFloat("_ZWrite",energy?0:1);material.SetFloat("_Glow",energy?1.7f:1);
            material.SetColor("_Tint",Color.white);material.renderQueue=energy?3000:2001;
            material.SetOverrideTag("RenderType",energy?"Transparent":"Opaque");EditorUtility.SetDirty(material);
        }
    }
}
