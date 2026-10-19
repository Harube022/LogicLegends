using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LogicLegends.Tests.Khezu.Editor
{
    public static class KhezuAwakeningPanelSetup
    {
        [MenuItem("Tools/Logic Legends/Khezu Phase 1/Add On-Screen Test Buttons")]
        public static void Add()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != KhezuAwakeningTestSetup.ScenePath) throw new InvalidOperationException("Open the Khezu test scene first.");
            if (Object.FindFirstObjectByType<KhezuAwakeningTestPanel>() != null) return;
            var session = Object.FindFirstObjectByType<LogicLegends.OpenArchiveTesting.OpenArchiveExplorationSession>();
            var canvas = session.GetComponentsInChildren<Canvas>(true).First(c => c.isRootCanvas);
            var safeArea = canvas.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "SafeArea");
            var panel = Rect("Khezu_Awakening_Buttons", safeArea, new Vector2(0, -156), new Vector2(720, 126));
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1);
            panel.gameObject.AddComponent<Image>().color = new Color(.025f, .07f, .1f, .92f);
            var label = Label(panel, "KHEZU TEST   |   Mistakes 0 / 4   |   Sleep", new Vector2(0, 35), new Vector2(700, 42), 25);
            var controller = Object.FindFirstObjectByType<KhezuAwakeningTestController>();
            var ui = panel.gameObject.AddComponent<KhezuAwakeningTestPanel>();
            var increase = Button(panel, "Khezu_AddMistake", "+1 Mistake", -230, new Color(.07f, .36f, .44f));
            var awaken = Button(panel, "Khezu_AwakenAll", "Awaken All", 0, new Color(.49f, .19f, .16f));
            var reset = Button(panel, "Khezu_ResetSleep", "Reset to Sleep", 230, new Color(.14f, .24f, .32f));
            ui.Configure(controller, label, increase, awaken, reset);
            UnityEventTools.AddPersistentListener(increase.onClick, ui.IncreaseMistakes);
            UnityEventTools.AddPersistentListener(awaken.onClick, ui.AwakenAll);
            UnityEventTools.AddPersistentListener(reset.onClick, ui.ResetToSleep);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        static Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var rect = Rect("Label", parent, position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white; label.raycastTarget = false; label.text = text;
            return label;
        }
        static Button Button(Transform parent, string name, string title, float x, Color color)
        {
            var rect = Rect(name, parent, new Vector2(x, -23), new Vector2(216, 56));
            var image = rect.gameObject.AddComponent<Image>(); image.color = color;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors; colors.disabledColor = new Color(.48f, .48f, .48f, .75f); button.colors = colors;
            Label(rect, title, Vector2.zero, new Vector2(208, 52), 25);
            return button;
        }
    }
}
