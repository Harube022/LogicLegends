using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogicLegends.Tests.Khezu.Editor
{
    /// <summary>Arranges the existing test controls; their callbacks and state owners are retained.</summary>
    public static class KhezuTestUILayout
    {
        const string DockName = "Khezu_Test_Tools_Dock";
        const float Width = 520;

        [MenuItem("Tools/Logic Legends/Khezu Tests/Organize Existing Test Controls")]
        public static void ApplyCurrent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void Apply(Scene scene)
        {
            var transforms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var hunt = transforms.Select(t => t.GetComponent<KhezuHuntTestControls>()).FirstOrDefault(c => c != null);
            var cinematic = transforms.Select(t => t.GetComponent<KhezuCinematicTestControls>()).FirstOrDefault(c => c != null);
            if (hunt == null) throw new InvalidOperationException("This scene has no existing Khezu hunting test panel.");
            var safe = transforms.Single(t => t.name == "SafeArea");
            var dock = Rect(DockName, safe);
            dock.anchorMin = dock.anchorMax = new Vector2(0, 1);
            dock.pivot = new Vector2(0, 1);
            dock.anchoredPosition = new Vector2(28, -154);
            dock.sizeDelta = new Vector2(Width, cinematic != null ? 406 : 276);

            var header = Rect("Show_Hide_Test_Tools", dock);
            Top(header, 0, 48);
            var headerImage = Background(header, new Color(.045f, .115f, .16f, .98f), true);
            var toggle = header.GetComponent<Toggle>() ?? header.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = headerImage;
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            Text("Title", header, "KHEZU TEST TOOLS", 20, TextAnchor.MiddleLeft, 18, 164);
            Text("Hint", header, "Show / hide", 17, TextAnchor.MiddleRight, 350, 50);
            var box = Rect("Indicator", header);
            box.anchorMin = box.anchorMax = new Vector2(1, .5f);
            box.pivot = new Vector2(.5f, .5f); box.anchoredPosition = new Vector2(-27, 0); box.sizeDelta = new Vector2(24, 24);
            Background(box, new Color(.13f, .25f, .31f, 1), false);
            var mark = Rect("Visible", box);
            Stretch(mark, 5, 5);
            toggle.graphic = Background(mark, new Color(.28f, .86f, .78f), false);

            var body = Rect("Test_Tools_Content", dock);
            Top(body, 56, cinematic != null ? 350 : 220);
            // A standard UI Toggle controls presentation only; no additional runtime system is needed.
            for (int i = toggle.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(toggle.onValueChanged, i);
            UnityEventTools.AddPersistentListener(toggle.onValueChanged, body.gameObject.SetActive);
            toggle.SetIsOnWithoutNotify(true);
            body.gameObject.SetActive(true);

            var huntRect = (RectTransform)hunt.transform;
            huntRect.SetParent(body, false); Top(huntRect, 0, 220);
            Background(huntRect, new Color(.025f, .065f, .095f, .93f), false);
            Top(hunt.status.rectTransform, 12, 70, 28);
            StyleText(hunt.status, 18, TextAnchor.UpperLeft);
            var primary = Row("Hunt_Actions", huntRect, 94, 56);
            var secondary = Row("Hunt_Reset_Actions", huntRect, 158, 48);
            MoveButton(transforms, "Hunt_ForceFour", primary, "Awaken Khezu", false);
            MoveButton(transforms, "Hunt_Start", primary, "Start / Resume", false);
            MoveButton(transforms, "Hunt_Sleep", primary, "Return to sleep", false);
            MoveButton(transforms, "Hunt_Reset", secondary, "Reset hunt", true);
            MoveButton(transforms, "Hunt_ClearRotation", secondary, "Clear rotation", true);

            if (cinematic != null)
            {
                var cinematicRect = (RectTransform)cinematic.transform;
                cinematicRect.SetParent(body, false); Top(cinematicRect, 228, 122);
                Background(cinematicRect, new Color(.025f, .065f, .095f, .93f), false);
                Top(cinematic.status.rectTransform, 8, 46, 28);
                StyleText(cinematic.status, 18, TextAnchor.MiddleLeft);
                var row = Row("Cinematic_Actions", cinematicRect, 62, 48);
                MoveButton(transforms, "Preview_POV", row, "Preview POV (offline)", false);
                var motion = cinematic.reducedMotion;
                motion.transform.SetParent(row, false);
                LayoutSize((RectTransform)motion.transform);
                var label = motion.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single();
                label.text = "Reduced motion";
                Stretch(label.rectTransform, 42, 10); StyleText(label, 19, TextAnchor.MiddleLeft);
                var check = (RectTransform)motion.graphic.transform;
                check.anchorMin = check.anchorMax = new Vector2(0, .5f);
                check.pivot = new Vector2(.5f, .5f); check.anchoredPosition = new Vector2(22, 0); check.sizeDelta = new Vector2(18, 18);
                Background((RectTransform)motion.transform, new Color(.12f, .20f, .26f), true);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(dock);
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var found = parent.Find(name) as RectTransform;
            if (found != null) return found;
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        static void Top(RectTransform rect, float top, float height, float inset = 0)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1); rect.localScale = Vector3.one;
            rect.anchoredPosition = new Vector2(0, -top); rect.sizeDelta = new Vector2(-inset, height);
        }
        static void Stretch(RectTransform rect, float left, float right)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, 0); rect.offsetMax = new Vector2(-right, 0);
        }
        static Image Background(RectTransform rect, Color color, bool raycast)
        {
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = raycast; return image;
        }
        static void StyleText(UnityEngine.UI.Text text, int size, TextAnchor alignment)
        {
            text.fontSize = size; text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16; text.resizeTextMaxSize = size;
            text.alignment = alignment; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        }
        static void Text(string name, Transform parent, string value, int size, TextAnchor alignment, float left, float right)
        {
            var rect = Rect(name, parent); Stretch(rect, left, right);
            var text = rect.GetComponent<UnityEngine.UI.Text>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.color = Color.white;
            text.text = value; StyleText(text, size, alignment);
        }
        static RectTransform Row(string name, Transform parent, float top, float height)
        {
            var rect = Rect(name, parent); Top(rect, top, height, 28);
            var layout = rect.GetComponent<HorizontalLayoutGroup>() ?? rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleCenter; return rect;
        }
        static void LayoutSize(RectTransform rect)
        {
            var layout = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 0; layout.preferredWidth = 0; layout.flexibleWidth = 1;
        }
        static void MoveButton(Transform[] transforms, string name, Transform parent, string title, bool secondary)
        {
            var rect = (RectTransform)transforms.Single(t => t.name == name);
            rect.SetParent(parent, false); LayoutSize(rect);
            Background(rect, secondary ? new Color(.12f, .20f, .26f) : new Color(.055f, .29f, .36f), true);
            var label = rect.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single();
            Stretch(label.rectTransform, 6, 6); StyleText(label, 19, TextAnchor.MiddleCenter); label.text = title;
        }
    }
}
