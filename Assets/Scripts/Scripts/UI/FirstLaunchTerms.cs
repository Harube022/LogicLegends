using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>Local agreement gate for the game's startup scene.</summary>
public sealed class FirstLaunchTerms : MonoBehaviour
{
    internal const string PreferenceKey = "LogicLegends.AcceptedTermsVersion";
    internal const int CurrentVersion = 1;
    const string StartupScene = "Assets/Scenes/Main Menu.unity";
    static readonly Color Teal = new Color(.06f, .29f, .29f);

    const string Terms =
        "<b><color=#155555>Use your own account</color></b>\nKeep your sign-in details private. Use a display name that respects other players.\n\n" +
        "<b><color=#155555>Play fairly</color></b>\nSolve challenges through normal play. Do not cheat, use bugs to gain an advantage, or manipulate rewards and rankings.\n\n" +
        "<b><color=#155555>Respect your teammates</color></b>\nKeep chat and multiplayer interactions respectful. Do not harass, threaten, impersonate, or share another person's private information.\n\n" +
        "<b><color=#155555>Progress and leaderboard</color></b>\nSaved progress and match results are tied to your account. Your display name and leaderboard points can be seen by other signed-in players.\n\n" +
        "<b><color=#155555>Use Logic Legends to learn</color></b>\nThe game gives you practice in Discrete Mathematics 1. Follow your instructor's rules when using it for class.";

    RectTransform safeArea;
    Rect lastSafeArea;
    Vector2Int lastSize;
    Action continueStartup;

    internal static bool Blocks(Scene scene) => scene.path == StartupScene &&
        PlayerPrefs.GetInt(PreferenceKey, 0) < CurrentVersion;

    internal static bool ShowIfNeeded(Component owner, Action onAgree)
    {
        var scene = owner.gameObject.scene;
        if (!Blocks(scene)) return false;

        var root = new GameObject("First Launch Terms", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(root, scene);
        var modal = root.AddComponent<FirstLaunchTerms>();
        modal.continueStartup = onAgree;
        modal.Build();
        return true;
    }

    void Build()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32766;
        var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.022f, .062f, .084f);
        safeArea = Rect("Terms Safe Area", transform, 0, 0, 1, 1);
        UpdateSafeArea();

        var frame = Rect("Terms Frame", safeArea, .14f, .08f, .86f, .92f);
        var wood = frame.gameObject.AddComponent<UnityEngine.UI.Image>();
        wood.sprite = StageUITheme.Panel;
        wood.type = UnityEngine.UI.Image.Type.Sliced;
        wood.pixelsPerUnitMultiplier = 2.4f;

        var paper = Rect("Terms Paper", frame, .027f, .04f, .973f, .96f);
        paper.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.95f, .885f, .74f);
        var title = StageUITheme.Text("Terms Title", paper, "Terms of Use", 52);
        StageUITheme.Stretch(title.rectTransform, new Vector2(.058f, .835f), new Vector2(.94f, .952f));
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.color = StageUITheme.ReadingInk;
        var note = StageUITheme.Text("Terms Prompt", paper, "Review these rules before playing Logic Legends.", 28, true);
        StageUITheme.Stretch(note.rectTransform, new Vector2(.06f, .76f), new Vector2(.94f, .838f));
        note.alignment = TextAlignmentOptions.MidlineLeft;
        note.color = Teal;

        BuildReadingArea(paper);
        var exit = StageUITheme.Button("Exit Terms", paper, "EXIT GAME", MainMenuLoadingScreen.CancelApplication);
        var agree = StageUITheme.Button("Agree to Terms", paper, "I AGREE", Agree);
        StageUITheme.Stretch((RectTransform)exit.transform, new Vector2(.06f, .055f), new Vector2(.32f, .155f));
        StageUITheme.Stretch((RectTransform)agree.transform, new Vector2(.62f, .055f), new Vector2(.94f, .155f));
        exit.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnRight = agree };
        agree.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnLeft = exit };
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    static void BuildReadingArea(RectTransform paper)
    {
        var area = Rect("Terms Scroll View", paper, .058f, .195f, .943f, .733f);
        var scroll = area.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var viewport = Rect("Viewport", area, 0, 0, .97f, 1);
        viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.91f, .83f, .65f);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var content = Rect("Content", viewport, 0, 1, 1, 1);
        content.pivot = new Vector2(.5f, 1);
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 18, 20);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit =
            UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var copy = StageUITheme.Text("Terms Text", content, Terms, 30, true);
        copy.alignment = TextAlignmentOptions.TopLeft;
        copy.color = StageUITheme.ReadingInk;

        var rail = Rect("Terms Scrollbar", area, .978f, 0, 1, 1);
        rail.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.76f, .68f, .5f);
        var slide = Rect("Sliding Area", rail, 0, 0, 1, 1);
        slide.offsetMin = new Vector2(3, 3);
        slide.offsetMax = new Vector2(-3, -3);
        var handle = Rect("Handle", slide, 0, 0, 1, 1);
        var grip = handle.gameObject.AddComponent<UnityEngine.UI.Image>();
        grip.color = Teal;
        var bar = rail.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
        bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle;
        bar.targetGraphic = grip;
        bar.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalNormalizedPosition = 1;
    }

    static RectTransform Rect(string name, Transform parent, float left, float bottom, float right, float top)
    {
        var rect = StageUITheme.Rect(name, parent);
        StageUITheme.Stretch(rect, new Vector2(left, bottom), new Vector2(right, top));
        return rect;
    }

    void LateUpdate()
    {
        if (lastSize.x != Screen.width || lastSize.y != Screen.height || lastSafeArea != Screen.safeArea)
            UpdateSafeArea();
    }

    void UpdateSafeArea()
    {
        lastSize = new Vector2Int(Screen.width, Screen.height);
        lastSafeArea = Screen.safeArea;
        MultiplayerSafeArea.Apply(safeArea);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
    }

    void Agree()
    {
        var resume = continueStartup;
        if (resume == null) return;

        PlayerPrefs.SetInt(PreferenceKey, CurrentVersion);
        PlayerPrefs.Save();
        continueStartup = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
        resume();
    }
}
