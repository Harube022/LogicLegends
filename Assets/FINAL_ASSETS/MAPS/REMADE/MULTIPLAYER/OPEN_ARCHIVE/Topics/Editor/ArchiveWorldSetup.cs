using System;
using System.Linq;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.Tests.Monsters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveWorldSetup
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Install Day To Night Progression")]
        public static void Install()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty)throw new InvalidOperationException("Use a saved scene outside Play Mode.");
            var session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();if(session==null)throw new InvalidOperationException("Open MultiplayerGameplay.");
            var world=session.GetComponent<ArchiveWorldProgression>();if(world==null)world=session.gameObject.AddComponent<ArchiveWorldProgression>();
            world.session=session;world.selector=UnityEngine.Object.FindFirstObjectByType<DevelopmentMonsterSelection>();
            if(world.selector==null||!world.selector.HasSharedEncounter)throw new InvalidOperationException("Both existing rig profiles are required.");
            world.sun=RenderSettings.sun!=null?RenderSettings.sun:UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
            if(world.secondarySpawn==null)
            {
                var spawn=new GameObject("Night_MonsterSpawnPoint").transform;spawn.SetParent(session.hunting.monsterSpawnPoint.parent,false);
                spawn.SetPositionAndRotation(session.hunting.monsterSpawnPoint.position+session.hunting.monsterSpawnPoint.right*28,session.hunting.monsterSpawnPoint.rotation);
                world.secondarySpawn=spawn;
            }
            if(world.secondary==null)
            {
                var clone=UnityEngine.Object.Instantiate(session.hunting.gameObject,session.hunting.transform.parent);clone.name="Night_Second_Monster";clone.SetActive(false);
                world.secondary=clone.GetComponent<KhezuHuntingController>();world.secondary.encounterId="night-secondary";world.secondary.monsterSpawnPoint=world.secondarySpawn;world.secondary.health=session.hunting.health;
            }
            if(world.moon==null)
            {
                var go=new GameObject("Archive_Moonlight");go.transform.SetParent(session.transform,false);go.transform.rotation=Quaternion.Euler(48,-115,0);
                world.moon=go.AddComponent<Light>();world.moon.type=LightType.Directional;world.moon.color=new Color(.55f,.68f,1);world.moon.intensity=0;world.moon.shadows=LightShadows.None;
            }
            const string atmosphere="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/Atmosphere/Audio/";
            world.nightWind=AssetDatabase.LoadAssetAtPath<AudioClip>(atmosphere+"Archive_Night_Ominous_Wind_Loop.wav");
            world.nightCracks=AssetDatabase.LoadAssetAtPath<AudioClip>(atmosphere+"Archive_Night_Distant_Creaks_Loop.wav");
            world.nightFogColor=new Color(.10f,.13f,.18f);world.nightFogDensity=.016f;
            world.nightWindVolume=.22f;world.nightDetailVolume=.24f;world.warningAmbienceMultiplier=.18f;
            world.entranceRoar=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Cinematics/Audio/Khezu_Maw_Roar_Stinger.wav");
            world.dayAmbience=UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(a=>a.clip!=null&&(a.clip.name.IndexOf("ForestBirds",StringComparison.OrdinalIgnoreCase)>=0||a.clip.name=="FOREST2")).ToArray();
            session.worldProgression=world;EditorUtility.SetDirty(session);EditorUtility.SetDirty(world);EditorUtility.SetDirty(world.secondary);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
    }
}
