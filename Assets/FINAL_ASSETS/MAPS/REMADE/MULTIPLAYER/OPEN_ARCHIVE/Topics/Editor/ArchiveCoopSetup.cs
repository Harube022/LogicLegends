using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveCoopSetup
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Install T1 Cooperative Relay")]
        public static void Install()
        {
            var session=Object.FindFirstObjectByType<ArchiveTopicSession>();if(session==null||Application.isPlaying)throw new System.InvalidOperationException("Open the saved topic scene outside Play Mode.");
            var station=session.stations[0];var trial=station.GetComponent<ArchiveT1CoopTrial>();if(trial!=null)return;
            trial=station.gameObject.AddComponent<ArchiveT1CoopTrial>();trial.station=station;
            Build(station,trial);Save(station);
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Install Optional Relays T1–T10")]
        public static void InstallAll()
        {
            var session=Object.FindFirstObjectByType<ArchiveTopicSession>();
            if(session==null||Application.isPlaying)throw new System.InvalidOperationException("Open MultiplayerGameplay outside Play Mode.");
            foreach(var station in session.stations)
            {
                var trial=station.GetComponent<ArchiveT1CoopTrial>();
                if(trial==null){trial=station.gameObject.AddComponent<ArchiveT1CoopTrial>();trial.station=station;Build(station,trial);}
                if(trial.sign!=null)trial.sign.text="T"+(station.topic+1)+" · OPTIONAL TEAM RELAY\nActivate the main Book first.\nAvailable while this topic runs.\nCloses when the topic completes.";
                if(trial.sign!=null&&trial.backSign==null)
                {
                    var back=Object.Instantiate(trial.sign.gameObject,trial.sign.transform.parent);back.name="Coop_Relay_Back_Instructions";
                    back.transform.localPosition=trial.sign.transform.localPosition+Vector3.forward*.35f;back.transform.localRotation=Quaternion.Euler(0,180,0);
                    trial.backSign=back.GetComponent<TMP_Text>();
                }
                if(trial.gate!=null){var collider=trial.gate.GetComponent<Collider>();if(collider!=null)collider.enabled=false;}
                EditorUtility.SetDirty(trial);
            }
            Save(session.stations[0]);
        }
        public static void RebuildPilot()
        {
            var session=Object.FindFirstObjectByType<ArchiveTopicSession>();if(session==null||Application.isPlaying)throw new System.InvalidOperationException("Stop Play Mode.");
            var station=session.stations[0];var trial=station.GetComponent<ArchiveT1CoopTrial>();if(trial==null){Install();return;}
            foreach(var child in station.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Coop_")).ToArray())Object.DestroyImmediate(child.gameObject);
            Build(station,trial);Save(station);
        }
        static void Build(ArchiveTopicStation station,ArchiveT1CoopTrial trial)
        {
            string folder="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art";
            Material Mat(string name,Color color){string path=folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;AssetDatabase.CreateAsset(m,path);}return m;}
            var stone=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Materials/Archive_Topic_Stone.mat");
            var brass=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Materials/Archive_Topic_BrassInlay.mat");
            var ink=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Materials/Archive_Topic_TealInlay.mat");
            var teal=Mat("Coop_Teal",new Color(.06f,.56f,.52f));var amber=Mat("Coop_Amber",new Color(1,.57f,.08f));var scarlet=Mat("Coop_Pulse",new Color(.9f,.17f,.1f));
            var root=new GameObject("Coop_Relay_Root").transform;root.SetParent(station.transform,false);root.localPosition=new Vector3(2.9f,0,0);root.localRotation=ArchiveT1CoopTrial.LaneRotation;
            Transform Cube(string name,Vector3 p,Vector3 size,Material material,bool collide=true){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;if(!collide)Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;}
            // A low, solid relay deck sits on existing land; no holes, timed disappearing floors or parkour falls.
            Cube("Coop_Relay_Deck",new Vector3(0,.08f,12.9f),new Vector3(17,.16f,3.4f),stone);
            Cube("Coop_Left_Curb",new Vector3(0,.18f,11.15f),new Vector3(17,.20f,.18f),stone);
            Cube("Coop_Right_Curb",new Vector3(0,.18f,14.65f),new Vector3(17,.20f,.18f),stone);
            for(int i=0;i<2;i++){
                var p=new Vector3(i==0?-7:7,.13f,9.8f);Cube("Coop_"+(i==0?"P":"Q")+"_Rune",p,new Vector3(2.2f,.24f,2),stone);
                Cube("Coop_Rune_Gold_"+i,p+Vector3.up*.13f,new Vector3(1.95f,.025f,1.75f),brass,false);
                var rune=new GameObject("Coop_Input_"+(i==0?"P":"Q"),typeof(TextMeshPro));rune.transform.SetParent(root,false);rune.transform.localPosition=p+new Vector3(0,.16f,-.3f);rune.transform.localRotation=Quaternion.Euler(90,0,0);rune.GetComponent<TMP_Text>().text=i==0?"P":"Q";rune.GetComponent<TMP_Text>().font=station.font;rune.GetComponent<TMP_Text>().fontSize=7;rune.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Center;rune.GetComponent<TMP_Text>().rectTransform.sizeDelta=new Vector2(1.8f,1.4f);
            }
            foreach(float z in new[]{11.08f,14.72f}){Cube("Coop_Gate_Pillar",new Vector3(0,2.4f,z),new Vector3(.65f,4.8f,.65f),stone);Cube("Coop_Gate_Capital",new Vector3(0,4.75f,z),new Vector3(.85f,.18f,.85f),brass);}
            Cube("Coop_Gate_Lintel",new Vector3(0,4.95f,12.9f),new Vector3(.55f,.35f,4.2f),stone);
            trial.gate=Cube("Coop_Logic_Gate",new Vector3(0,.8f,12.9f),new Vector3(.16f,1.5f,3.3f),ink);
            trial.pulseRunes=Enumerable.Range(0,3).Select(i=>Cube("Coop_Pulse_Rune_"+i,new Vector3((i-1)*3,.17f,12.9f),new Vector3(.6f,.025f,3),teal,false).GetComponent<Renderer>()).ToArray();
            trial.calmMaterial=teal;trial.warningMaterial=amber;trial.pulseMaterial=scarlet;
            Cube("Coop_Relay_Plaque_Frame",new Vector3(-6,3.4f,15.1f),new Vector3(4.9f,2.4f,.15f),brass,false);
            Cube("Coop_Relay_Plaque_Face",new Vector3(-6,3.4f,14.99f),new Vector3(4.65f,2.15f,.04f),ink,false);
            var label=new GameObject("Coop_Relay_Instructions",typeof(TextMeshPro));label.transform.SetParent(root,false);label.transform.localPosition=new Vector3(-6,3.4f,14.92f);label.transform.localRotation=Quaternion.identity;
            trial.sign=label.GetComponent<TMP_Text>();trial.sign.font=station.font;trial.sign.fontSize=2.8f;trial.sign.enableAutoSizing=true;trial.sign.fontSizeMin=1.5f;trial.sign.fontSizeMax=2.8f;trial.sign.alignment=TextAlignmentOptions.Center;trial.sign.rectTransform.sizeDelta=new Vector2(4.5f,2.05f);trial.sign.text="T1 · TEAM RELAY\nActivate the main Book first.\nSolve P and Q while T1 runs.\nOptional while T1 is active.";
            EditorUtility.SetDirty(station);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
        }
        static void Save(ArchiveTopicStation station){UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(station.gameObject.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(station.gameObject.scene);AssetDatabase.SaveAssets();}
    }
}
