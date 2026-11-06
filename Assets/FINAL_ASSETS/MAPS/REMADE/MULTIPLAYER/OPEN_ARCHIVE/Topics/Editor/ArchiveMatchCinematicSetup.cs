using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Installs local match presentation on the existing shared session.</summary>
    public static class ArchiveMatchCinematicSetup
    {
        const string ScenePath="Assets/Scenes/MultiplayerGameplay.unity";
        const string Folder=ArchiveTopicSetup.Folder+"/Art/MatchCinematics";
        const string HeroShaderPath=ArchiveTopicSetup.Folder+"/Art/Shaders/ArchiveHeroSummon.shader";
        const string EnergySourcePath=ArchiveTopicSetup.Folder+"/Environment/Resources/ArchiveGuardianEntranceEnergy.mat";
        const string Magic="Assets/Assets/Sounds Effects/Small Sound Kit/Magic/";
        static readonly string[] AudioPaths={Magic+"Mgc_Glacier_Cast_01.wav",Magic+"Mgc_Ice_Arrow_Cast_01.wav",
            "Assets/Assets/Sounds Effects/WORLDBGM/SOFTWIN.mp3",Magic+"Mgc_Ice_Arrow_Cast_01.wav"};

        [Serializable] sealed class Report
        {
            public bool passed,physicsPreserved,navigationPreserved,sessionStatePreserved;
            public string scene,energyMaterial,energyShader,heroMaterial,heroShader;
            public string[] audio;
            public string scope="Local start/victory presentation only. Existing session clocks, outcomes, actors, physics and navigation are unchanged.";
        }
        static Component[] Components(Scene scene)=>scene.GetRootGameObjects()
            .SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
        static string Physics(Scene scene)=>string.Join("\n",Components(scene).OfType<Collider>().OrderBy(c=>c.GetInstanceID())
            .Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)+":"+c.transform.localToWorldMatrix));
        static string Navigation(Scene scene)=>string.Join("\n",Components(scene)
            .Where(c=>c is UnityEngine.AI.NavMeshAgent||c is UnityEngine.AI.NavMeshObstacle||c is Hunting.KhezuNavigationData)
            .OrderBy(c=>c.GetInstanceID()).Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)));

        [MenuItem("Tools/Logic Legends/Open Archive/Cinematics/Configure Match Intro And Victory")]
        public static void Install()=>Apply();

        public static ArchiveMatchCinematic Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty||scene.path!=ScenePath)
                throw new InvalidOperationException("Use the saved production MultiplayerGameplay scene outside Play Mode.");
            var sessions=UnityEngine.Object.FindObjectsByType<ArchiveTopicSession>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(s=>s.gameObject.scene==scene).ToArray();
            if(sessions.Length!=1)throw new InvalidOperationException("Exactly one existing ArchiveTopicSession is required.");
            var session=sessions[0];
            if(UnityEngine.Object.FindObjectsByType<ArchiveMatchCinematic>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Any(c=>c.gameObject.scene==scene&&c.gameObject!=session.gameObject))
                throw new InvalidOperationException("Match presentation must live on the existing session, without a duplicate owner.");

            // Validate every dependency before adding a component or changing the scene.
            AssetDatabase.ImportAsset(HeroShaderPath,ImportAssetOptions.ForceUpdate);
            var heroShader=AssetDatabase.LoadAssetAtPath<Shader>(HeroShaderPath);
            var energySource=AssetDatabase.LoadAssetAtPath<Material>(EnergySourcePath);
            var energyShader=energySource==null?null:energySource.shader;
            if(heroShader==null||heroShader.name!="LogicLegends/OpenArchive/HeroSummon"||ShaderUtil.ShaderHasError(heroShader)||!heroShader.isSupported)
                throw new InvalidOperationException("Import the owned HeroSummon shader before configuring match presentation.");
            if(energySource==null||energyShader==null||ShaderUtil.ShaderHasError(energyShader)||!energyShader.isSupported)
                throw new InvalidOperationException("The existing URP guardian energy material is required.");
            var audio=AudioPaths.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
            if(audio.Any(a=>a==null))throw new InvalidOperationException("An existing summoning/victory sound clip is missing.");

            string physics=Physics(scene),navigation=Navigation(scene),state=EditorJsonUtility.ToJson(session);
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var energy=EnergyMaterial(energySource,energyShader);
            var hero=HeroMaterial(heroShader);
            var cinematic=session.GetComponent<ArchiveMatchCinematic>();
            bool created=cinematic==null;
            if(created)cinematic=Undo.AddComponent<ArchiveMatchCinematic>(session.gameObject);
            Undo.RecordObject(cinematic,"Configure Archive match cinematics");
            cinematic.session=session;cinematic.energyMaterial=energy;cinematic.heroSummonMaterial=hero;
            cinematic.summoningRise=audio[0];cinematic.summoningArrival=audio[1];
            cinematic.victoryRise=audio[2];cinematic.victoryImpact=audio[3];
            if(created){cinematic.playIntro=true;cinematic.playVictory=true;cinematic.reducedMotion=false;cinematic.victorySeconds=6;}
            PrefabUtility.RecordPrefabInstancePropertyModifications(cinematic);EditorUtility.SetDirty(cinematic);

            var report=new Report
            {
                scene=scene.path,physicsPreserved=physics==Physics(scene),navigationPreserved=navigation==Navigation(scene),
                sessionStatePreserved=state==EditorJsonUtility.ToJson(session),energyMaterial=AssetDatabase.GetAssetPath(energy),
                energyShader=energy.shader.name,heroMaterial=AssetDatabase.GetAssetPath(hero),heroShader=hero.shader.name,audio=AudioPaths
            };
            report.passed=report.physicsPreserved&&report.navigationPreserved&&report.sessionStatePreserved&&
                cinematic.session==session&&(energy.HasProperty("_BaseColor")||energy.HasProperty("_Tint"))&&hero.HasProperty("_RevealY");
            Directory.CreateDirectory("Temp/ArchiveMatchCinematics");
            File.WriteAllText("Temp/ArchiveMatchCinematics/Setup.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Match presentation changed existing gameplay state, physics or navigation.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return cinematic;
        }
        static Material EnergyMaterial(Material source,Shader shader)
        {
            string path=Folder+"/Archive_Match_Energy.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(source){name="Archive_Match_Energy"};AssetDatabase.CreateAsset(material,path);}
            // Keep the existing lightweight, vertex-colour guardian pass. The
            // runtime tint helper supports this shader's _Tint property.
            material.shader=shader;material.shaderKeywords=Array.Empty<string>();material.enableInstancing=true;
            material.SetColor("_Tint",Color.white);material.SetFloat("_Glow",1.5f);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.One);
            material.SetFloat("_ZWrite",0);material.renderQueue=3000;
            material.SetOverrideTag("RenderType","Transparent");EditorUtility.SetDirty(material);return material;
        }
        static Material HeroMaterial(Shader shader)
        {
            string path=Folder+"/Archive_Hero_Summon.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader){name="Archive_Hero_Summon"};AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;material.enableInstancing=true;material.renderQueue=2000;
            material.SetTexture("_BaseMap",Texture2D.whiteTexture);material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_RevealY",10000);material.SetColor("_GlowColor",new Color(.3f,1.1f,1.25f,1));
            material.SetFloat("_EdgeWidth",.14f);material.SetFloat("_NoiseScale",4);
            EditorUtility.SetDirty(material);return material;
        }
    }
}
