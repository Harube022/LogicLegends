using System;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LogicLegends.Tests.Khezu.Editor
{
    public static class KhezuCinematicSetup
    {
        public const string Folder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuCinematic";
        public const string ScenePath = Folder + "/OpenArchive_KhezuCinematic_Test.unity";
        const string Monster = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/MONSTERS/ProofDevourer";
        public const string AudioFolder = "Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Cinematics/Audio";
        public static void AssignDesignedAudio(KhezuCinematicJumpscareController c)
        {
            string[] names = { "Khezu_Capture_Impact", "Khezu_Low_Breathing", "Khezu_Tense_Growl", "Khezu_Maw_Roar_Stinger", "Khezu_Impact_Accent", "Khezu_Barrier_Pulse" };
            var clips = new AudioClip[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string path = AudioFolder + "/" + names[i] + ".wav";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new FileNotFoundException("Render the Khezu audio assets first.", path);
                importer.forceToMono = true; importer.loadInBackground = false;
                var settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData = true;
                settings.compressionFormat = AudioCompressionFormat.PCM; settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                // Preserve the authored headroom rather than normalizing quieter tension cues.
                var serialized = new SerializedObject(importer); var normalize = serialized.FindProperty("m_Normalize");
                if (normalize != null) { normalize.boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo(); }
                importer.SaveAndReimport();
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            c.captureImpact = clips[0]; c.lowBreathing = clips[1]; c.tenseGrowl = clips[2];
            c.monsterRoar = clips[3]; c.impactAccent = clips[4]; c.barrierActivation = clips[5];
            c.masterVolume = .65f; c.captureLevel = .65f; c.breathingLevel = .18f; c.growlLevel = .3f;
            c.roarLevel = .95f; c.accentLevel = .25f; c.barrierLevel = .4f;
            EditorUtility.SetDirty(c);
        }
        [MenuItem("Tools/Logic Legends/Khezu Cinematic/Use Existing Lobby For Next Hosted Room")]
        public static void ChooseLobbyTarget()
        {
            if (!Application.isPlaying || Photon.Pun.PhotonNetwork.InRoom)
                throw new InvalidOperationException("In Main Menu Play Mode, open Multiplayer first, then choose this before hosting.");
            MultiplayerLobbyService.EnsureExists().TargetScene = "OpenArchive_KhezuCinematic_Test";
            Debug.Log("Next hosted lobby uses the Khezu cinematic test. Host and join using the existing lobby.");
        }
        [MenuItem("Tools/Logic Legends/Khezu Cinematic/Create Test Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var current = SceneManager.GetActiveScene();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty && SceneManager.GetSceneAt(i).path != KhezuHuntingSetup.ScenePath)
                    throw new InvalidOperationException("Save current scene edits first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Cinematic scene already exists; open the saved scene.");
            // Save-as-copy preserves any unsaved source edits in the new test without overwriting Phase 2.
            if (current.path == KhezuHuntingSetup.ScenePath && current.isDirty)
            { if (!EditorSceneManager.SaveScene(current, ScenePath, true)) throw new IOException("Could not preserve the current test scene."); }
            else if (!AssetDatabase.CopyAsset(KhezuHuntingSetup.ScenePath, ScenePath)) throw new IOException("Could not copy the existing hunting test.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var h = Object.FindFirstObjectByType<KhezuHuntingController>();
            var c = h.gameObject.AddComponent<KhezuCinematicJumpscareController>();
            c.presentationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Monster + "/Prefabs/ProofDevourer.prefab");
            c.capture = AssetDatabase.LoadAssetAtPath<AnimationClip>(Monster + "/Animations/Capture.anim");
            c.jumpscare = AssetDatabase.LoadAssetAtPath<AnimationClip>(Monster + "/Animations/Jumpscare.anim");
            c.barrierCast = AssetDatabase.LoadAssetAtPath<AnimationClip>(Monster + "/Animations/Barrier_Cast.anim");
            AssignDesignedAudio(c);
            var e = h.gameObject.AddComponent<KhezuCaptureEncounterController>(); e.cinematic = c;
            AddControls(c, e, h);
            EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            AssetDatabase.SaveAssets(); Selection.activeGameObject = h.gameObject;
        }
        static void AddControls(KhezuCinematicJumpscareController c, KhezuCaptureEncounterController e, KhezuHuntingController h)
        {
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(x => x.name == "Canvas 1");
            var parent = canvas.transform.Find("SafeArea"); if (parent == null) parent = canvas.transform;
            var bar = Panel("Cinematic_Test_Controls", parent, new Vector2(.30f, .02f), new Vector2(.66f, .14f), new Color(.025f, .04f, .065f, .96f));
            var controls = bar.gameObject.AddComponent<KhezuCinematicTestControls>(); controls.cinematic = c; controls.encounter = e; controls.hunting = h;
            controls.status = Label("Status", bar, new Vector2(.02f, .56f), new Vector2(.98f, .96f), "CINEMATIC TEST", 19);
            controls.previewButton = Button("Preview_POV", bar, new Vector2(.02f, .08f), new Vector2(.47f, .54f), "PREVIEW POV (OFFLINE)");
            UnityEventTools.AddPersistentListener(controls.previewButton.onClick, controls.Preview);
            var toggleRect = Panel("Reduced_Motion", bar, new Vector2(.52f, .08f), new Vector2(.97f, .54f), new Color(.12f, .18f, .23f, 1));
            var toggle = toggleRect.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = toggleRect.GetComponent<Image>();
            var check = Panel("Check", toggleRect, new Vector2(.02f, .18f), new Vector2(.09f, .82f), new Color(.3f, .85f, .82f));
            toggle.graphic = check.GetComponent<Image>(); toggle.isOn = c.reducedMotion;
            Label("Label", toggleRect, new Vector2(.13f, 0), Vector2.one, "REDUCED MOTION", 18);
            UnityEventTools.AddPersistentListener(toggle.onValueChanged, controls.SetReducedMotion); controls.reducedMotion = toggle;
            var handoff = Panel("Phase3_Handoff_Test_Only", parent, new Vector2(.24f, .32f), new Vector2(.76f, .68f), new Color(.035f, .025f, .055f, .98f));
            controls.handoffPanel = handoff.gameObject;
            Label("Title", handoff, new Vector2(.05f, .72f), new Vector2(.95f, .96f), "BARRIER HANDOFF", 32);
            Label("Explanation", handoff, new Vector2(.08f, .28f), new Vector2(.92f, .74f), "Cinematic complete.\nPhase 3 mathematical challenge is not implemented yet.\nThis panel verifies the handoff only.", 23);
            var reset = Button("Reset_Test_Encounter", handoff, new Vector2(.25f, .06f), new Vector2(.75f, .25f), "RESET TEST ENCOUNTER");
            UnityEventTools.AddPersistentListener(reset.onClick, controls.ResetEncounter);
            UnityEventTools.AddPersistentListener(e.localChallengeRequested, controls.ShowHandoff);
            UnityEventTools.AddPersistentListener(e.localEncounterEnded, controls.HideHandoff);
            handoff.gameObject.SetActive(false);
        }
        public static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color; return r;
        }
        static Text Label(string name, Transform parent, Vector2 min, Vector2 max, string text, int size)
        {
            var r = Panel(name, parent, min, max, Color.clear); Object.DestroyImmediate(r.GetComponent<Image>());
            var t = r.gameObject.AddComponent<Text>(); t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = new Color(.91f, .94f, 1); t.raycastTarget = false;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 14; t.resizeTextMaxSize = size; return t;
        }
        static Button Button(string name, Transform parent, Vector2 min, Vector2 max, string text)
        {
            var r = Panel(name, parent, min, max, new Color(.14f, .27f, .34f)); var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = r.GetComponent<Image>(); Label("Label", r, Vector2.zero, Vector2.one, text, 19); return b;
        }
    }
}
