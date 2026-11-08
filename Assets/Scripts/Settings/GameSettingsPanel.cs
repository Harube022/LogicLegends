using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One functional settings surface for Main Menu and solo pause menus.</summary>
public sealed class GameSettingsPanel : MonoBehaviour
{
    [SerializeField] ProfileAccountInformationUI accountInformationPrefab;
    static readonly Color Blue = new Color32(5, 121, 177, 255), Ink = new Color32(55, 32, 15, 255), Cream = new Color32(255, 242, 212, 255);
    SoloSettingsData draft;
    readonly List<Action> refreshers = new List<Action>();
    bool wired, refreshing;
    LevelMenu trialMenu;
    GameObject popup;
    TMP_Text feedback;
    Transform frame;
    public bool HasPendingChanges => draft != null && JsonUtility.ToJson(draft) != JsonUtility.ToJson(SoloGameSettings.Current);
    public SoloSettingsData Draft => draft;
    void Awake() { Initialize(); }
    void OnEnable()
    {
        Initialize(); draft = SoloGameSettings.Current.Copy(); RefreshControls();
        ClearFeedback();
    }
    void OnDisable() { ClosePopup(); draft = null; }
    public void ConfigureTrial(LevelMenu owner)
    {
        trialMenu = owner; Initialize();
        var logout = frame.Find("Footer/LogOut").GetComponent<Button>();
        logout.onClick = new Button.ButtonClickedEvent(); logout.onClick.AddListener(RequestMainMenu);
        logout.GetComponentInChildren<TMP_Text>(true).text = "MAIN MENU";
        var quit = frame.Find("Footer/QuitGame").GetComponent<Button>();
        quit.onClick = new Button.ButtonClickedEvent(); quit.onClick.AddListener(RequestQuit);
        var close = transform.Find("CloseSettingsAction")?.GetComponent<Button>();
        if (close != null) { close.onClick = new Button.ButtonClickedEvent(); close.onClick.AddListener(Close); }
    }
    void Initialize()
    {
        if (wired) return;
        frame = transform.Find("SettingsFrame"); if (frame == null) return;
        wired = true;
        Segments("GRAPHICSPage/Display/GRAPHICSQUALITY/Control", new[] { "LOW", "MEDIUM", "HIGH" }, () => draft.graphicsQuality, value => draft.graphicsQuality = value);
        Segments("GRAPHICSPage/Display/FRAMERATE/Control", new[] { "30 FPS", "60 FPS" }, () => draft.frameRate == 60 ? 1 : 0, value => draft.frameRate = value == 1 ? 60 : 30);
        Choice("GRAPHICSPage/Display/SHADOWQUALITY/Control", new[] { "OFF", "LOW", "MEDIUM", "HIGH" }, () => draft.shadowQuality, value => draft.shadowQuality = value);
        Choice("GRAPHICSPage/Effects/LIGHTINGQUALITY/Control", new[] { "LOW", "MEDIUM", "HIGH" }, () => draft.lightingQuality, value => draft.lightingQuality = value);
        Switch("GRAPHICSPage/Effects/FOG/Control", () => draft.fog, value => draft.fog = value);
        Switch("GRAPHICSPage/Effects/MOTIONBLUR/Control", () => draft.motionBlur, value => draft.motionBlur = value);
        Slider("CONTROLSPage/Camera/HORIZONTALSENSITIVITY/Control", .25f, 2, () => draft.horizontalSensitivity, value => draft.horizontalSensitivity = value, value => value.ToString("0.0") + "X");
        Slider("CONTROLSPage/Camera/VERTICALSENSITIVITY/Control", .25f, 2, () => draft.verticalSensitivity, value => draft.verticalSensitivity = value, value => value.ToString("0.0") + "X");
        Slider("CONTROLSPage/Camera/PINCHZOOMSENSITIVITY/Control", .25f, 2, () => draft.zoomSensitivity, value => draft.zoomSensitivity = value, value => value.ToString("0.0") + "X");
        Slider("CONTROLSPage/Touch/BUTTONSIZE/Control", .8f, 1.2f, () => draft.buttonSize, value => draft.buttonSize = value, Percent);
        Slider("CONTROLSPage/Touch/BUTTONOPACITY/Control", .35f, 1, () => draft.buttonOpacity, value => draft.buttonOpacity = value, Percent);
        Switch("CONTROLSPage/Touch/LEFT-HANDEDLAYOUT/Control", () => draft.leftHanded, value => draft.leftHanded = value);
        Slider("AUDIOPage/Volume/MUSIC/Control", 0, 1, () => draft.music, value => draft.music = value, Percent);
        Slider("AUDIOPage/Volume/SOUNDEFFECTS/Control", 0, 1, () => draft.soundFX, value => draft.soundFX = value, Percent);
        Switch("AUDIOPage/Volume/MUTEALLAUDIO/Control", () => draft.mute, value => draft.mute = value);
        Slider("ACCESSIBILITYPage/Readability/TEXTSIZE/Control", .8f, 1.25f, () => draft.textSize, value => draft.textSize = value, Percent);
        Switch("ACCESSIBILITYPage/Readability/HIGH-CONTRASTLABELS/Control", () => draft.highContrast, value => draft.highContrast = value);
        Switch("ACCESSIBILITYPage/Readability/COLOR-INDEPENDENTCUES/Control", () => draft.colorIndependentCues, value => draft.colorIndependentCues = value);
        Action("Footer/Apply", Apply); Action("Footer/Reset", RequestReset);
        Action("GENERALPage/Account/ACCOUNTINFORMATION/Control/VIEWACCOUNT", ShowAccount);
        Action("GENERALPage/Account/TUTORIAL/Control/REPLAYTUTORIAL", ShowGuidePicker);
        Action("GENERALPage/Help/NEEDHELP?/Control/SUPPORT", () => ShowInfo("HELP & SUPPORT", "Open HELP inside a trial for its controls, examples and gameplay guide.\n\nFor account or payment issues, contact your game administrator with the trial name or payment reference. Never share your password."));
        Action("GENERALPage/Help/YOURPRIVACY/Control/PRIVACYPOLICY", () => ShowInfo("DATA & PRIVACY", "Settings are saved on this device. Reset changes settings only.\n\nAccount progress, Coins, Gems and owned outfits are stored with your signed-in account. Returning to Main Menu keeps you signed in; Log Out in Main Menu ends your session.\n\nFor account data requests, contact your game administrator."));
        var language = frame.Find("GENERALPage/Account/LANGUAGE/Control");
        if (language != null) { var button = language.GetComponent<Button>(); if (button != null) button.interactable = false; var arrow = language.Find("Arrow"); if (arrow != null) arrow.gameObject.SetActive(false); }
        var version = frame.Find("GENERALPage/Account/GAMEVERSION/Version")?.GetComponent<TMP_Text>();
        if (version != null) version.text = Application.version;
        var close = transform.Find("CloseSettingsAction")?.GetComponent<Button>();
        if (close != null) { Enable(close); close.onClick.AddListener(Close); }
        feedback = StageUITheme.Text("SettingsFeedback", frame, "", 28);
        feedback.font = StageUITheme.HeadingFont; feedback.color = new Color32(20, 112, 45, 255);
        StageUITheme.Stretch(feedback.rectTransform, new Vector2(.15f, .19f), new Vector2(.85f, .225f));
        feedback.enableAutoSizing = true; feedback.fontSizeMin = 24; feedback.fontSizeMax = 28;
    }
    static string Percent(float value) => Mathf.RoundToInt(value * 100) + "%";
    static void Enable(Selectable selectable)
    {
        selectable.enabled = true; selectable.interactable = true; selectable.navigation = new Navigation { mode = Navigation.Mode.None };
        if (selectable.targetGraphic != null) selectable.targetGraphic.raycastTarget = true;
    }
    void Action(string path, UnityEngine.Events.UnityAction callback)
    {
        var button = frame.Find(path)?.GetComponent<Button>(); if (button == null) return;
        Enable(button); button.onClick.AddListener(callback);
    }
    void Segments(string path, string[] choices, Func<int> get, Action<int> set)
    {
        var root = frame.Find(path); if (root == null) return;
        var buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            int value = i; Enable(buttons[i]); buttons[i].onClick.AddListener(() => { if (draft == null) return; set(value); ClearFeedback(); RefreshControls(); });
        }
        refreshers.Add(() => {
            for (int i = 0; i < buttons.Length; i++)
            { buttons[i].targetGraphic.color = get() == i ? Blue : new Color32(232, 209, 166, 255); buttons[i].GetComponentInChildren<TMP_Text>(true).color = get() == i ? Cream : Ink; }
        });
    }
    void Choice(string path, string[] choices, Func<int> get, Action<int> set)
    {
        var root = frame.Find(path); if (root == null) return;
        var dropdown = root.GetComponent<TMP_Dropdown>();
        if (dropdown == null) { Debug.LogError("The settings dropdown needs its updated prefab: " + path); return; }
        Enable(dropdown); dropdown.ClearOptions(); dropdown.AddOptions(new List<string>(choices));
        dropdown.onValueChanged.AddListener(value => { if (refreshing || draft == null) return; set(value); ClearFeedback(); RefreshControls(); });
        refreshers.Add(() => { dropdown.SetValueWithoutNotify(get()); dropdown.RefreshShownValue(); });
    }
#if UNITY_EDITOR
    /// <summary>Authors a native dropdown list below the existing painted control.</summary>
    public static void AuthorDropdown(Transform root, string[] choices)
    {
        var oldButton = root.GetComponent<Button>(); if (oldButton != null) DestroyImmediate(oldButton);
        var dropdown = root.GetComponent<SettingsDropdown>();
        if (dropdown == null)
        {
            var legacy = root.GetComponent<TMP_Dropdown>(); if (legacy != null) DestroyImmediate(legacy);
            var oldTemplate = root.Find("DropdownTemplate"); if (oldTemplate != null) DestroyImmediate(oldTemplate.gameObject);
            dropdown = root.gameObject.AddComponent<SettingsDropdown>();
        }
        dropdown.targetGraphic = root.GetComponent<Image>(); dropdown.captionText = root.Find("Value").GetComponent<TMP_Text>();
        dropdown.transition = Selectable.Transition.None; dropdown.alphaFadeSpeed = .08f;
        if (dropdown.template == null)
        {
            var template = StageUITheme.Rect("DropdownTemplate", root);
            template.anchorMin = new Vector2(0, 0); template.anchorMax = new Vector2(1, 0); template.pivot = new Vector2(.5f, 1);
            template.anchoredPosition = new Vector2(0, -4); template.sizeDelta = new Vector2(0, choices.Length * 64 + 16);
            var background = template.gameObject.AddComponent<Image>(); background.sprite = StagePresentation.Art("GraphStatusFrame"); background.type = Image.Type.Sliced; background.pixelsPerUnitMultiplier = 5;
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = StageUITheme.Rect("Viewport", template); StageUITheme.Stretch(viewport, Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(8, 8); viewport.offsetMax = new Vector2(-8, -8); viewport.gameObject.AddComponent<RectMask2D>();
            var content = StageUITheme.Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, 64);
            var item = StageUITheme.Rect("Item", content); item.anchorMin = Vector2.zero; item.anchorMax = new Vector2(1, 0); item.pivot = new Vector2(.5f, .5f); item.anchoredPosition = new Vector2(0, 32); item.sizeDelta = new Vector2(0, 64);
            var face = item.gameObject.AddComponent<Image>(); face.color = Cream;
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = face; toggle.transition = Selectable.Transition.ColorTint;
            var colors = toggle.colors; colors.highlightedColor = new Color(.8f, .92f, 1); colors.pressedColor = new Color(.65f, .85f, .95f); colors.selectedColor = colors.highlightedColor; toggle.colors = colors;
            var selected = StageUITheme.Rect("Selected", item); StageUITheme.Stretch(selected, new Vector2(.01f, .15f), new Vector2(.035f, .85f));
            var check = selected.gameObject.AddComponent<Image>(); check.color = Blue; check.raycastTarget = false; toggle.graphic = check;
            var label = StageUITheme.Text("Option Label", item, "OPTION", 28); label.color = Ink; label.alignment = TextAlignmentOptions.MidlineLeft;
            StageUITheme.Stretch(label.rectTransform, new Vector2(.08f, .04f), new Vector2(.95f, .96f)); label.textWrappingMode = TextWrappingModes.NoWrap;
            dropdown.itemText = label; dropdown.template = template; scroll.viewport = viewport; scroll.content = content;
            template.gameObject.SetActive(false);
        }
        dropdown.ClearOptions(); dropdown.AddOptions(new List<string>(choices)); dropdown.SetValueWithoutNotify(0); dropdown.RefreshShownValue();
    }
#endif
    void Slider(string path, float min, float max, Func<float> get, Action<float> set, Func<float, string> format)
    {
        var root = frame.Find(path); if (root == null) return;
        var slider = root.GetComponentInChildren<Slider>(true); var body = slider.transform;
        slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = false;
        slider.fillRect = body.Find("Track/Fill Area/Fill") as RectTransform;
        slider.handleRect = body.Find("Handle Slide Area/Handle") as RectTransform;
        if (slider.handleRect != null)
        {
            var area = (RectTransform)slider.handleRect.parent;
            area.anchorMin = new Vector2(0, .5f); area.anchorMax = new Vector2(1, .5f);
            area.offsetMin = new Vector2(5.5f, -29); area.offsetMax = new Vector2(-5.5f, 29);
            slider.handleRect.anchorMin = new Vector2(.5f, 0); slider.handleRect.anchorMax = new Vector2(.5f, 1);
            slider.handleRect.sizeDelta = new Vector2(38, 0); slider.handleRect.anchoredPosition = Vector2.zero;
        }
        var hit = body.GetComponent<Image>(); if (hit == null) hit = body.gameObject.AddComponent<Image>();
        hit.color = Color.clear; hit.raycastTarget = true; hit.raycastPadding = new Vector4(0, -10, 0, -10);
        if (slider.handleRect != null) slider.targetGraphic = slider.handleRect.GetComponent<Image>();
        Enable(slider);
        var label = root.Find("Value")?.GetComponent<TMP_Text>();
        slider.onValueChanged.AddListener(value => { if (refreshing || draft == null) return; set(value); ClearFeedback(); if (label != null) label.text = format(value); });
        refreshers.Add(() => { slider.SetValueWithoutNotify(get()); if (label != null) label.text = format(get()); });
    }
    void Switch(string path, Func<bool> get, Action<bool> set)
    {
        var root = frame.Find(path); if (root == null) return;
        var toggle = root.GetComponentInChildren<Toggle>(true); toggle.targetGraphic = toggle.GetComponent<Graphic>(); Enable(toggle);
        toggle.onValueChanged.AddListener(value => { if (refreshing || draft == null) return; set(value); ClearFeedback(); RefreshControls(); });
        refreshers.Add(() => {
            bool on = get(); toggle.SetIsOnWithoutNotify(on);
            toggle.transform.Find("Face").GetComponent<Graphic>().color = on ? Blue : new Color32(112, 65, 31, 255);
            var knob = (RectTransform)toggle.transform.Find("Handle"); knob.anchorMin = knob.anchorMax = new Vector2(on ? .855f : .145f, .5f);
            var label = toggle.transform.Find("State").GetComponent<TMP_Text>(); label.text = on ? "ON" : "OFF";
            StageUITheme.Stretch(label.rectTransform, new Vector2(on ? .05f : .31f, .1f), new Vector2(on ? .69f : .95f, .9f));
        });
    }
    void RefreshControls()
    {
        if (draft == null) return;
        refreshing = true; foreach (var refresh in refreshers) refresh(); refreshing = false;
    }
    public void Apply()
    {
        if (draft == null) return;
        SoloGameSettings.Apply(draft); draft = SoloGameSettings.Current.Copy(); RefreshControls();
        if (feedback != null) feedback.text = "Changes Saved";
    }
    void ClearFeedback() { if (feedback != null) feedback.text = ""; }
    public void ResetDraft()
    {
        draft = new SoloSettingsData(); RefreshControls(); ClearFeedback();
    }
    void RequestReset() => ShowConfirmation("RESET SETTINGS?", "Reset all settings to their defaults?\nTap Apply afterward to save them.", "YES", ResetDraft, null, true, true);
    public void Close()
    {
        if (HasPendingChanges)
            ShowConfirmation("UNSAVED CHANGES", "Apply your changes before closing Settings?\nCancel closes Settings without saving.", "APPLY", () => { Apply(); CloseSettings(); }, CloseSettings, true, true);
        else CloseSettings();
    }
    void CloseSettings()
    {
        ClosePopup();
        if (trialMenu != null) trialMenu.ClosePause();
        else
        {
            var menu = FindFirstObjectByType<MainMenuManager>();
            if (menu != null) menu.ShowMainMenu(); else gameObject.SetActive(false);
        }
    }
    void RequestMainMenu() => ShowConfirmation("RETURN TO MAIN MENU?", "Leave this trial and return to Main Menu?\nUnfinished trial rewards will not be awarded.", "MAIN MENU", () => trialMenu.ReturnToMainMenu());
    void RequestQuit() => ShowConfirmation("QUIT GAME?", "Are you sure you want to quit the game?", "QUIT", () => trialMenu.QuitGame());
    void ShowAccount()
    {
        if (accountInformationPrefab == null) return;
        ClosePopup();
        // Keep Settings enabled so its selected tab and unsaved changes survive closing the account view.
        var account = Instantiate(accountInformationPrefab, DialogHost(), false);
        popup = account.gameObject;
        var canvas = popup.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 31000;
        popup.AddComponent<GraphicRaycaster>();
        account.Show();
    }
    void ShowGuidePicker()
    {
        var titles = new string[TrialCatalog.Count]; for (int i = 0; i < titles.Length; i++) titles[i] = TrialCatalog.Get(i + 1).Title;
        ShowChoices(titles, -1, i => ShowInfo(titles[i], StageHelpContent.ForScene(TrialCatalog.Get(i + 1).Scene)));
    }
    void ShowChoices(string[] choices, int selected, Action<int> choose)
    {
        var content = BeginPopup("CHOOSE AN OPTION", out var card);
        var list = ScrollContent(content);
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childControlWidth = true; layout.childForceExpandWidth = true;
        list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        for (int i = 0; i < choices.Length; i++)
        {
            int choice = i; var button = StageUITheme.Button("Option" + i, list, choices[i], () => { ClosePopup(); choose(choice); });
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 68;
            if (i == selected) button.image.color = new Color(.7f, .9f, .85f);
        }
        var close = PopupButton(card, "CANCEL", -1, ClosePopup); close.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
    }
    void ShowConfirmation(string title, string body, string confirm, Action action, Action cancelAction = null, bool confirmGreen = false, bool closeButton = false)
    {
        ClosePopup(); var overlay = StageUITheme.Rect("Settings Dialog", DialogHost());
        StageUITheme.Stretch(overlay, Vector2.zero, Vector2.one); popup = overlay.gameObject;
        var shade = popup.AddComponent<Image>(); shade.color = new Color(.015f, .025f, .04f, .80f); shade.raycastTarget = true;
        var canvas = popup.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 31000; popup.AddComponent<GraphicRaycaster>();
        var card = StageUITheme.Rect("ConfirmationCard", overlay); card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f; card.sizeDelta = new Vector2(960, 520);
        var surface = card.gameObject.AddComponent<Image>(); surface.sprite = StagePresentation.Art("GraphMapFrame"); surface.type = Image.Type.Sliced; surface.pixelsPerUnitMultiplier = 6.5f;
        float fit = Mathf.Min(1, (overlay.rect.width - 80) / 960, (overlay.rect.height - 80) / 520); card.localScale = Vector3.one * Mathf.Max(.1f, fit);
        var plaque = StageUITheme.Rect("HeadingPlaque", card); StageUITheme.Stretch(plaque, new Vector2(.2f, .72f), new Vector2(.8f, .92f));
        var banner = plaque.gameObject.AddComponent<Image>(); banner.sprite = StagePresentation.Art("GraphStatusFrame"); banner.type = Image.Type.Sliced; banner.pixelsPerUnitMultiplier = 5; banner.raycastTarget = false;
        var heading = StageUITheme.Text("Heading", plaque, title, 44); heading.color = StageUITheme.ReadingInk; heading.enableAutoSizing = true; heading.fontSizeMin = 28; heading.fontSizeMax = 44;
        StageUITheme.Stretch(heading.rectTransform, Vector2.one * .08f, Vector2.one * .92f);
        var message = StageUITheme.Text("Message", card, body, 30, true); message.color = StageUITheme.ReadingInk; message.lineSpacing = 14;
        StageUITheme.Stretch(message.rectTransform, new Vector2(.08f, .36f), new Vector2(.92f, .68f)); message.enableAutoSizing = true; message.fontSizeMin = 24; message.fontSizeMax = 30;
        var dialog = popup;
        ConfirmationButton(card, "CANCEL", -160, !confirmGreen, () => { if (popup != dialog || !dialog.activeInHierarchy) return; ClosePopup(); cancelAction?.Invoke(); });
        ConfirmationButton(card, confirm, 160, confirmGreen, () => { if (popup != dialog || !dialog.activeInHierarchy) return; ClosePopup(); action(); });
        if (closeButton)
        {
            var close = MultiplayerUIArt.CloseAction("CloseConfirmation", card, 0, 1);
            close.onClick.AddListener(ClosePopup);
        }
    }
    static Button ConfirmationButton(RectTransform card, string text, float x, bool green, UnityEngine.Events.UnityAction action)
    {
        var button = StageUITheme.Button(text, card, text, action); var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = Vector2.one * .5f; rect.sizeDelta = new Vector2(280, 88); rect.anchoredPosition = new Vector2(x, -155);
        var sprites = Resources.LoadAll<Sprite>(green ? "StageUI/WoodButtonGreen" : "StageUI/WoodButtonRed");
        if (sprites.Length > 0) button.image.sprite = sprites[0]; button.image.pixelsPerUnitMultiplier = 4.5f;
        return button;
    }
    void ShowInfo(string title, string body) => ShowDialog(title, body, "", null);
    void ShowDialog(string title, string body, string confirm, Action action)
    {
        var content = BeginPopup(title, out var card);
        var list = ScrollContent(content);
        var label = StageUITheme.Text("Message", list, body, 28, true); label.color = StageUITheme.ReadingInk;
        label.alignment = TextAlignmentOptions.TopLeft; label.enableAutoSizing = false;
        label.rectTransform.anchorMin = new Vector2(0, 1); label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.pivot = new Vector2(.5f, 1); label.rectTransform.sizeDelta = Vector2.zero;
        label.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        list.gameObject.AddComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
        list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        PopupButton(card, action == null ? "CLOSE" : "CANCEL", action == null ? 0 : -1, ClosePopup);
        if (action != null) PopupButton(card, confirm, 1, () => { ClosePopup(); action(); });
    }
    Transform BeginPopup(string title, out RectTransform card)
    {
        ClosePopup(); var overlay = StageUITheme.Rect("Settings Dialog", DialogHost());
        StageUITheme.Stretch(overlay, Vector2.zero, Vector2.one); popup = overlay.gameObject;
        var shade = popup.AddComponent<Image>(); shade.color = new Color(.01f, .02f, .03f, .85f); shade.raycastTarget = true;
        var canvas = popup.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 31000;
        popup.AddComponent<GraphicRaycaster>();
        card = StageUITheme.Rect("Dialog Card", overlay); StageUITheme.Stretch(card, new Vector2(.13f, .12f), new Vector2(.87f, .88f));
        StagePresentation.Frame(card.gameObject.AddComponent<Image>(), true);
        var heading = StageUITheme.Text("Heading", card, title, 38); heading.color = StageUITheme.ReadingInk;
        heading.enableAutoSizing = true; heading.fontSizeMin = 24; heading.fontSizeMax = 38;
        StageUITheme.Stretch(heading.rectTransform, new Vector2(.06f, .82f), new Vector2(.94f, .94f));
        var content = StageUITheme.Rect("Content", card); StageUITheme.Stretch(content, new Vector2(.08f, .25f), new Vector2(.92f, .79f));
        return content;
    }
    static RectTransform ScrollContent(Transform host)
    {
        var scroll = host.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = StageUITheme.Rect("Viewport", host); StageUITheme.Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .001f); viewport.gameObject.AddComponent<RectMask2D>();
        var list = StageUITheme.Rect("List", viewport); list.anchorMin = new Vector2(0, 1); list.anchorMax = Vector2.one; list.pivot = new Vector2(.5f, 1); list.sizeDelta = Vector2.zero;
        scroll.viewport = viewport; scroll.content = list; scroll.scrollSensitivity = 40; return list;
    }
    static Button PopupButton(RectTransform card, string caption, int side, UnityEngine.Events.UnityAction action)
    {
        var button = StageUITheme.Button(caption, card, caption, action); var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .12f); rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = new Vector2(side * 170, 0); rect.sizeDelta = new Vector2(300, 80); return button;
    }
    Transform DialogHost()
    {
        // The settings surface is fitted to a safe area. Modal backgrounds
        // belong to the full root canvas, including the space outside that fit.
        var canvas = GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.rootCanvas.transform : transform;
    }
    void ClosePopup()
    {
        if (popup != null)
        {
            popup.SetActive(false);
            if (Application.isPlaying) Destroy(popup); else DestroyImmediate(popup);
        }
        popup = null;
    }
}
