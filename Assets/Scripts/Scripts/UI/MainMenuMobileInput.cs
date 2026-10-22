using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>Keeps Main Menu forms above Android's IME; gameplay canvases are untouched.</summary>
[DefaultExecutionOrder(32000)]
public sealed class MainMenuMobileInput : MonoBehaviour
{
    private readonly List<TMP_InputField> inputs = new List<TMP_InputField>();
    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform shifted;
    private Vector2 originalPosition;

    public static void EnsureForScene(Scene scene)
    {
        if (scene.name != "Main Menu") return;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<MainMenuMobileInput>() != null) return;
        var owner = new GameObject("Main Menu Mobile Input");
        SceneManager.MoveGameObjectToScene(owner, scene);
        owner.AddComponent<MainMenuMobileInput>();
    }

    private void Start()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                inputs.Add(input);
                input.onSelect.AddListener(OnSelect);
                input.onFocusSelectAll = false;
                input.resetOnDeActivation = false;
            }
    }

    private void OnSelect(string unused)
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var input = selected != null ? selected.GetComponent<TMP_InputField>() : null;
        if (input != null && inputs.Contains(input)) StartCoroutine(PlaceCaretAtEnd(input));
    }

    private IEnumerator PlaceCaretAtEnd(TMP_InputField input)
    {
        // TMP positions the caret during the original pointer event and keyboard activation.
        yield return null;
        yield return null;
        if (input != null && input.isFocused) input.MoveTextEnd(false);
    }

    private void LateUpdate()
    {
        TMP_InputField focused = null;
        foreach (var input in inputs)
            if (input != null && input.gameObject.activeInHierarchy && input.isFocused)
            { focused = input; break; }
        ApplyKeyboardHeight(focused, focused != null ? KeyboardHeight() : 0f);
    }

    private void ApplyKeyboardHeight(TMP_InputField focused, float height)
    {
        if (focused == null || height <= 0f)
        { Restore(); return; }
        var container = Container(focused);
        if (shifted != container)
        {
            Restore();
            shifted = container;
            originalPosition = shifted.anchoredPosition;
        }
        // Measure from the authored position every frame, so repeated focus/IME
        // changes never accumulate offsets or move a form permanently.
        shifted.anchoredPosition = originalPosition;
        var canvas = focused.GetComponentInParent<Canvas>().rootCanvas;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        focused.GetComponent<RectTransform>().GetWorldCorners(corners);
        float bottom = RectTransformUtility.WorldToScreenPoint(camera, corners[0]).y;
        float padding = Mathf.Max(16f, Screen.height * .025f);
        float offset = Mathf.Max(0f, height + padding - bottom);
        var parent = shifted.parent as RectTransform;
        if (parent != null && offset > 0f)
        {
            Vector2 a, b;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Vector2.zero, camera, out a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Vector2.up * offset, camera, out b);
            shifted.anchoredPosition = originalPosition + b - a;
        }
    }

    private static RectTransform Container(TMP_InputField input)
    {
        foreach (var t in input.GetComponentsInParent<RectTransform>(true))
            switch (t.name)
            {
                case "Login_Menu": case "SignUp_Menu": case "UsernameCard":
                case "EditorCard": case "InviteCard": case "ChatInterior": return t;
            }
        return (RectTransform)input.transform;
    }

    private static float KeyboardHeight()
    {
        float height = TouchScreenKeyboard.visible ? TouchScreenKeyboard.area.height : 0f;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var window = activity.Call<AndroidJavaObject>("getWindow"))
            using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
            using (var visible = new AndroidJavaObject("android.graphics.Rect"))
            {
                decor.Call("getWindowVisibleDisplayFrame", visible);
                float windowHeight = decor.Call<int>("getHeight");
                float covered = Mathf.Max(0, windowHeight - visible.Get<int>("bottom"));
                float visibleHeight = visible.Get<int>("bottom") - visible.Get<int>("top");
                float visibleWidth = visible.Get<int>("right") - visible.Get<int>("left");
                if (covered > windowHeight * .15f && Mathf.Abs(Screen.height - visibleHeight) < 8f &&
                    Mathf.Abs(Screen.width - visibleWidth) < 8f) return 0f;
                // Navigation/status bars are not an on-screen keyboard. When
                // Android resizes Unity above the IME there is no overlap to pan.
                if (windowHeight > 0f && covered > windowHeight * .15f)
                    height = Mathf.Max(height, covered * Screen.height / windowHeight);
                else if (height <= 0f && TouchScreenKeyboard.visible)
                {
                    using (var insets = decor.Call<AndroidJavaObject>("getRootWindowInsets"))
                    using (var types = new AndroidJavaClass("android.view.WindowInsets$Type"))
                    {
                        int ime = types.CallStatic<int>("ime");
                        if (insets != null && insets.Call<bool>("isVisible", ime))
                            using (var area = insets.Call<AndroidJavaObject>("getInsets", ime))
                                height = area.Get<int>("bottom") * Screen.height / Mathf.Max(1f, windowHeight);
                    }
                }
            }
        }
        catch (System.Exception)
        {
            // Older Android versions may expose only TouchScreenKeyboard.area.
        }
#endif
        if (height <= 0f && TouchScreenKeyboard.visible) height = Screen.height * .6f;
        return Mathf.Clamp(height, 0f, Screen.height * .85f);
    }

    private void Restore()
    {
        if (shifted != null) shifted.anchoredPosition = originalPosition;
        shifted = null;
    }
    private void OnDisable() => Restore();
    private void OnDestroy()
    {
        Restore();
        foreach (var input in inputs) if (input != null) input.onSelect.RemoveListener(OnSelect);
    }
}
