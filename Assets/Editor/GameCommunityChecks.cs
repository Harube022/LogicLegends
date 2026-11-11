using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Local editor checks that do not sign in, write data or change the open scene.</summary>
public static class GameCommunityChecks
{
    public static string Run()
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
        var backend = typeof(TrialBackendClient);
        var transport = backend.GetField("ValidationTransport", statics);
        var uid = backend.GetField("ValidationUid", statics);
        object oldTransport = transport.GetValue(null), oldUid = uid.GetValue(null);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            transport.SetValue(null, (Func<string, string, Task<string>>)((p, j) => Task.FromResult("{}")));
            uid.SetValue(null, "feedback-check");
            var canvasObject = new GameObject("Community test canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, preview);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var canvas = (RectTransform)canvasObject.transform;
            canvas.sizeDelta = new Vector2(1920, 1080);
            var overlay = MultiplayerUIArt.Rect("Community test overlay", canvas);
            var component = overlay.gameObject.AddComponent<GameCommunityUI>();
            var type = typeof(GameCommunityUI);
            type.GetField("root", instance).SetValue(component, overlay);
            void Invoke(string name, params object[] args) => type.GetMethod(name, instance).Invoke(component, args);
            T Field<T>(string name) => (T)type.GetField(name, instance).GetValue(component);
            void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }

            type.GetMethod("Report", statics).Invoke(null, new object[] { overlay, "feedback-check", "Self" });
            Check(Field<GameObject>("panel") == null, "Self-report must not open.");
            type.GetMethod("Report", statics).Invoke(null, new object[] { overlay, "target-check", "Teammate" });
            Check(Field<TMP_Text>("heading").text == "Report Player: Teammate", "Report must name the selected player.");
            Check(Field<GameObject>("categoryList").GetComponentsInChildren<UnityEngine.UI.Button>().Length == 7, "Missing report reasons.");
            foreach (var size in new[] { new Vector2(1440, 1080), new Vector2(1920, 1080), new Vector2(2400, 1080) })
            {
                canvas.sizeDelta = size;
                Canvas.ForceUpdateCanvases();
                var buttons = Field<GameObject>("categoryList").GetComponentsInChildren<UnityEngine.UI.Button>();
                Check(buttons.All(b => ((RectTransform)b.transform).rect.width > 140 && ((RectTransform)b.transform).rect.height > 55),
                    "Report reasons do not fit the landscape layout " + size);
            }
            Invoke("Select", 6);
            Invoke("Send");
            Check(Field<TMP_Text>("status").text.Contains("describe the issue"), "Others must require a description.");
            Invoke("Close");
            Invoke("Open", null, null);
            Check(Field<GameObject>("categoryList").GetComponentsInChildren<UnityEngine.UI.Button>().Length == 4, "Missing feedback types.");
            Invoke("Select", 1);
            var draft = Field<TMP_InputField>("description");
            draft.text = "A draft with reproduction details.";
            Field<TMP_InputField>("subject").text = "Test issue";
            // Firebase is deliberately unavailable in Edit Mode. A submission
            // must leave the draft intact and keep Submit available.
            Check(!FirebaseManager.IsReady, "Run this check outside Play Mode.");
            Invoke("Send");
            Check(draft.text == "A draft with reproduction details." && Field<UnityEngine.UI.Button>("submit").interactable &&
                !Field<bool>("submitted"), "Failed submission lost the draft or displayed success.");
            return "PASS: Social target and self-report, 7 reasons, 4 categories, Others validation, failed draft retention, 4:3 / 16:9 / 20:9 landscape button geometry.";
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            transport.SetValue(null, oldTransport);
            uid.SetValue(null, oldUid);
        }
    }
}
