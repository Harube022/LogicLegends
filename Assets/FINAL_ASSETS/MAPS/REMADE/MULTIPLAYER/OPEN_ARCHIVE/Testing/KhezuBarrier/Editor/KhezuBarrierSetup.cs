using System;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LogicLegends.Tests.Khezu.Editor
{
    public static class KhezuBarrierSetup
    {
        public const string Folder="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuBarrier";
        static Font font;
        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size){
            var go=new GameObject(name,typeof(RectTransform));go.layer=5;var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
        }
        static Text Label(string name,Transform parent,string text,Vector2 position,Vector2 size,int fontSize){
            var rect=Rect(name,parent,position,size);var t=rect.gameObject.AddComponent<Text>();t.font=font;t.text=text;t.fontSize=fontSize;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;return t;
        }
        static Button Button(string name,Transform parent,string text,Vector2 position,Color color,UnityAction action){
            var rect=Rect(name,parent,position,new Vector2(290,88));var image=rect.gameObject.AddComponent<Image>();image.color=color;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.None};
            UnityEventTools.AddPersistentListener(button.onClick,action);Label("Label",rect,text,Vector2.zero,new Vector2(280,80),30);return button;
        }
        [MenuItem("Tools/Logic Legends/Khezu Barrier/Connect Movement Lock And Development Outcomes")]
        public static void Configure(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            var encounter=Object.FindFirstObjectByType<KhezuCaptureEncounterController>();if(encounter==null)throw new InvalidOperationException("Open the existing cinematic encounter scene.");
            var hunting=encounter.GetComponent<KhezuHuntingController>();font=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            var canvas=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(c=>c.isRootCanvas);
            if(canvas.GetComponent<GraphicRaycaster>()==null)canvas.gameObject.AddComponent<GraphicRaycaster>();
            var events=Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if(events.Length!=1 || events[0].GetComponents<BaseInputModule>().Length==0)throw new InvalidOperationException("Expected the existing single EventSystem and UI input module.");
            foreach(var module in events[0].GetComponents<BaseInputModule>())module.enabled=true;events[0].enabled=true;
            var safe=canvas.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="SafeArea");
            var old=canvas.GetComponentInChildren<KhezuBarrierDevelopmentUI>(true);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var root=Rect("Khezu_Barrier_Development_UI",safe,Vector2.zero,Vector2.zero);var ui=root.gameObject.AddComponent<KhezuBarrierDevelopmentUI>();ui.encounter=encounter;
            var panel=Rect("Mathematical_Barrier_Panel",root,new Vector2(0,-40),new Vector2(800,360));var background=panel.gameObject.AddComponent<Image>();background.color=new Color(.045f,.012f,.02f,.94f);ui.panel=panel.gameObject;
            Label("Title",panel,"MATHEMATICAL BARRIER",new Vector2(0,124),new Vector2(760,54),32);
            Label("Description",panel,"Khezu has trapped you inside a mathematical seal!",new Vector2(0,60),new Vector2(750,55),25);
            ui.status=Label("Test_Message",panel,"Choose an outcome to test the encounter.",new Vector2(0,6),new Vector2(740,48),23);
            ui.successButton=Button("Barrier_SUCCESS",panel,"SUCCESS",new Vector2(-164,-90),new Color(.06f,.40f,.18f,1),ui.Success);
            ui.failedButton=Button("Barrier_FAILED",panel,"FAILED",new Vector2(164,-90),new Color(.66f,.035f,.075f,1),ui.Failed);
            panel.gameObject.SetActive(false);
            var debug=canvas.GetComponentsInChildren<KhezuHuntTestControls>(true).Select(c=>c.gameObject).Concat(canvas.GetComponentsInChildren<KhezuCinematicTestControls>(true).Select(c=>c.gameObject)).Concat(canvas.GetComponentsInChildren<RectTransform>(true).Where(t=>t.name=="Khezu_Awakening_Buttons").Select(t=>t.gameObject)).ToArray();
            ui.otherTestControls=debug.Select(go=>go.GetComponent<CanvasGroup>()??go.AddComponent<CanvasGroup>()).ToArray();
            var cinematicControls=canvas.GetComponentsInChildren<KhezuCinematicTestControls>(true).FirstOrDefault();
            if(cinematicControls!=null){
                if(cinematicControls.handoffPanel!=null)cinematicControls.handoffPanel.SetActive(false);
                for(int i=encounter.localChallengeRequested.GetPersistentEventCount()-1;i>=0;i--)if(encounter.localChallengeRequested.GetPersistentTarget(i)==cinematicControls)UnityEventTools.RemovePersistentListener(encounter.localChallengeRequested,i);
            }
            var health=Object.FindFirstObjectByType<HealthManager>();
            if(health==null){
                var hud=Rect("Khezu_Existing_Health_System",safe,new Vector2(-190,-175),new Vector2(280,70));hud.anchorMin=hud.anchorMax=Vector2.one;
                health=hud.gameObject.AddComponent<HealthManager>();var hearts=new GameObject[3];var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Assets/Interface_Sprites/gameplay_interface/heart.png");
                for(int i=0;i<3;i++){
                    var heart=Rect("Heart_"+(i+1),hud,new Vector2((i-1)*74,0),new Vector2(64,64));
                    if(sprite!=null){var image=heart.gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;}
                    else Label("Heart",heart,"♥",Vector2.zero,new Vector2(64,64),48).color=new Color(1,.12f,.2f);
                    hearts[i]=heart.gameObject;
                }
                var dead=Rect("Out_Of_Hearts",hud,new Vector2(0,-54),new Vector2(300,40));Label("Text",dead,"Out of hearts",Vector2.zero,new Vector2(300,40),24);dead.gameObject.SetActive(false);
                health.ConfigureBarrierHealth(hearts,dead.gameObject);
            }
            hunting.health=health;hunting.monsterSpawnPoint=GameObject.Find("OpenArchive").transform.Find("UniversalMonsterNest/MonsterSpawnPoint");
            if(!health.HasAuthoritativeActorHealth || hunting.monsterSpawnPoint==null)throw new InvalidOperationException("The existing health service or MonsterSpawnPoint is missing.");
            encounter.developmentTestOutcomes=true;
            EditorUtility.SetDirty(hunting);EditorUtility.SetDirty(encounter);EditorUtility.SetDirty(health);EditorSceneManager.MarkSceneDirty(encounter.gameObject.scene);EditorSceneManager.SaveScene(encounter.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
    }
    // The panel is absent from non-development player scenes, rather than merely hidden.
    public sealed class KhezuBarrierReleaseUiStripper : IProcessSceneWithReport
    {
        public int callbackOrder=>100;
        public void OnProcessScene(Scene scene,BuildReport report){
            if(report==null)return;
            Strip(scene,(report.summary.options&BuildOptions.Development)!=0);
        }
        public static void Strip(Scene scene,bool development){
            if(development)return;
            foreach(var root in scene.GetRootGameObjects())foreach(var ui in root.GetComponentsInChildren<KhezuBarrierDevelopmentUI>(true))Object.DestroyImmediate(ui.gameObject);
        }
    }
}
