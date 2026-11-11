using System;
using TMPro;
using UnityEngine;

/// <summary>Shared Feedback and Report Player form. The website owns submission and review.</summary>
public sealed class GameCommunityUI : MonoBehaviour
{
    static readonly string[] categories = { "general", "bug", "suggestion", "other" };
    static readonly string[] categoryLabels = { "General Feedback", "Bug Report", "Gameplay Suggestion", "Other Concerns" };
    static readonly string[] reasons = { "cheating", "harassment", "language", "username", "spam", "unsportsmanlike", "other" };
    static readonly string[] reasonLabels =
    {
        "Cheating or Exploiting", "Harassment or Bullying", "Inappropriate Language",
        "Offensive or Inappropriate Username", "Spam or Disruptive Behavior", "Unsportsmanlike Conduct", "Others"
    };

    GameObject home, adminButton, panel, categoryList, fields;
    RectTransform root;
    TMP_Text heading, selection, status;
    TMP_InputField subject, description;
    UnityEngine.UI.Button submit, cancel;
    readonly UnityEngine.UI.Button[] options = new UnityEngine.UI.Button[7];
    string account, target, targetName;
    int selected = -1;
    bool sending, submitted;

    internal static void Install(GameObject home)
    {
        var canvas = home != null ? home.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null || canvas.GetComponentInChildren<GameCommunityUI>(true) != null) return;
        var root = MultiplayerUIArt.Rect("CommunityOverlay", canvas.transform);
        var ui = root.gameObject.AddComponent<GameCommunityUI>();
        ui.root = root;
        ui.home = home;
        var safe = MultiplayerUIArt.Rect("CommunitySafeArea", canvas.transform);
        safe.gameObject.AddComponent<Crystal.SafeArea>();
        var entries = MultiplayerUIArt.Rect("CommunityEntries", safe);
        entries.gameObject.SetActive(false);
        ui.StartCoroutine(ui.BindHomeEntries(entries));
    }

    System.Collections.IEnumerator BindHomeEntries(RectTransform entries)
    {
        // The existing home panel is initially hidden until Firebase verification.
        var feedback = HomeButton(entries, "Feedback", "Feedback", new Vector2(28, 24));
        feedback.onClick.AddListener(() => Open(null, null));
        var text = feedback.GetComponentInChildren<TMP_Text>();
        text.rectTransform.offsetMin += new Vector2(44, 0);
        var icon = MultiplayerUIArt.Rect("MessageIcon", feedback.transform, 0, .5f, 0, .5f);
        icon.sizeDelta = new Vector2(30, 21);
        icon.anchoredPosition = new Vector2(41, 2);
        var bubble = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
        bubble.color = StageUITheme.Ink;
        bubble.raycastTarget = false;
        var tail = MultiplayerUIArt.Rect("MessageTail", icon, .15f, 0, .42f, 0);
        tail.sizeDelta = new Vector2(0, 9);
        tail.anchoredPosition = new Vector2(0, -3);
        var point = tail.gameObject.AddComponent<UnityEngine.UI.Image>();
        point.color = bubble.color;
        point.raycastTarget = false;
        var admin = HomeButton(entries, "Admin", "Admin", new Vector2(302, 24));
        adminButton = admin.gameObject;
        adminButton.SetActive(false);
        admin.onClick.AddListener(OpenAdmin);
        while (entries != null)
        {
            bool show = home != null && home.activeInHierarchy;
            entries.gameObject.SetActive(show);
            adminButton.SetActive(show && AdminAuthorization.IsAuthorized);
            yield return null;
        }
    }

    static UnityEngine.UI.Button HomeButton(Transform parent, string name, string label, Vector2 position)
    {
        var button = MultiplayerUIArt.Action(name + "Button", parent, label, 0, 0, 0, 0);
        MultiplayerUIArt.StandardAction(button, 254);
        var rect = (RectTransform)button.transform;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        return button;
    }

    async void OpenAdmin()
    {
        if (!AdminAuthorization.IsAuthorized) return;
        adminButton.GetComponent<UnityEngine.UI.Button>().interactable = false;
        try
        {
            var access = await TrialSessionServices.RequireAccess();
            if (this != null && access.admin && home.activeInHierarchy)
                Application.OpenURL(CommunityBackendClient.Site + "/Profile/profile.html");
        }
        catch (Exception) { /* Denied sessions are routed to login by the session service. */ }
        finally { if (this != null) adminButton.GetComponent<UnityEngine.UI.Button>().interactable = true; }
    }

    internal static void Report(Transform context, string uid, string name)
    {
        var canvas = context.GetComponentInParent<Canvas>();
        var ui = canvas != null ? canvas.GetComponentInChildren<GameCommunityUI>(true) : null;
        if (ui != null && !string.IsNullOrEmpty(uid) && uid != TrialBackendClient.UserId) ui.Open(uid, name);
    }

    void Build()
    {
        panel = MultiplayerUIArt.Rect("CommunityForm", root).gameObject;
        panel.AddComponent<UnityEngine.UI.Image>().color = new Color(.02f, .025f, .04f, .82f);
        var safe = MultiplayerUIArt.Rect("SafeArea", panel.transform);
        safe.gameObject.AddComponent<Crystal.SafeArea>();
        var card = MultiplayerUIArt.Paper("CommunityCard", safe, .12f, .05f, .88f, .95f);
        var paper = card.GetComponent<UnityEngine.UI.Image>();
        if (paper.sprite == null) paper.color = new Color(.96f, .88f, .72f);
        heading = Label("Heading", card, "", .05f, .83f, .95f, .97f, 40);
        status = Label("Status", card, "", .06f, .14f, .94f, .28f, 27);
        categoryList = MultiplayerUIArt.Rect("Categories", card, .07f, .29f, .93f, .82f).gameObject;
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            float left = i % 2 == 0 ? 0f : .515f;
            float top = 1f - (i / 2) * .25f;
            options[i] = Button("Option" + i, categoryList.transform, "", left, top - .225f, left + .485f, top, () => Select(index));
        }
        fields = MultiplayerUIArt.Rect("Fields", card).gameObject;
        selection = Label("SelectedCategory", fields.transform, "", .075f, .68f, .72f, .81f, 30);
        selection.alignment = TextAlignmentOptions.MidlineLeft;
        Button("ChangeCategory", fields.transform, "Change", .73f, .69f, .925f, .81f, ChooseCategory);
        subject = Input("Subject", fields.transform, "Subject", .075f, .54f, .925f, .665f, 120, false);
        description = Input("Description", fields.transform, "Description", .075f, .29f, .925f, .515f, 2000, true);
        submit = Button("Submit", card, "Submit", .55f, .03f, .88f, .14f, Send);
        cancel = Button("Close", card, "Cancel", .12f, .03f, .45f, .14f, Close);
        var mobileInput = FindFirstObjectByType<MainMenuMobileInput>();
        mobileInput?.Register(subject);
        mobileInput?.Register(description);
    }

    static TMP_Text Label(string name, Transform parent, string value, float x0, float y0, float x1, float y1, float size)
    {
        var label = MultiplayerUIArt.Label(name, parent, value, x0, y0, x1, y1, size, true, StageUITheme.ReadingInk);
        label.fontSizeMin = size * .66f;
        label.raycastTarget = false;
        return label;
    }

    static UnityEngine.UI.Button Button(string name, Transform parent, string caption, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction action)
    {
        var button = MultiplayerUIArt.Action(name, parent, caption, x0, y0, x1, y1);
        button.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier = 4.5f;
        var label = button.GetComponentInChildren<TMP_Text>();
        label.rectTransform.offsetMin = new Vector2(16, 6);
        label.rectTransform.offsetMax = new Vector2(-16, -6);
        label.fontSizeMin = 17;
        button.onClick.AddListener(action);
        return button;
    }

    static TMP_InputField Input(string name, Transform parent, string hint, float x0, float y0, float x1, float y1, int limit, bool multiline)
    {
        var rect = MultiplayerUIArt.Rect(name, parent, x0, y0, x1, y1);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(1f, .97f, .88f);
        var viewport = MultiplayerUIArt.Rect("TextArea", rect);
        viewport.offsetMin = new Vector2(16, 8);
        viewport.offsetMax = new Vector2(-16, -8);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var text = Label("Value", viewport, "", 0, 0, 1, 1, 27);
        var placeholder = Label("Placeholder", viewport, hint, 0, 0, 1, 1, 27);
        text.alignment = placeholder.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = false;
        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = limit;
        input.richText = false;
        input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        return input;
    }

    void Open(string uid, string name)
    {
        if (sending || panel != null && panel.activeSelf || string.IsNullOrEmpty(TrialBackendClient.UserId)) return;
        if (panel == null) Build();
        account = TrialBackendClient.UserId;
        target = uid;
        targetName = name;
        selected = -1;
        sending = submitted = false;
        submit.interactable = cancel.interactable = true;
        cancel.GetComponentInChildren<TMP_Text>().text = "Cancel";
        subject.text = description.text = "";
        root.SetAsLastSibling();
        panel.SetActive(true);
        heading.text = target == null ? "Feedback" : "Report Player: " + targetName;
        var labels = target == null ? categoryLabels : reasonLabels;
        for (int i = 0; i < options.Length; i++)
        {
            options[i].gameObject.SetActive(i < labels.Length);
            if (i < labels.Length) options[i].GetComponentInChildren<TMP_Text>().text = labels[i];
        }
        ChooseCategory();
    }

    void ChooseCategory()
    {
        if (sending || submitted) return;
        fields.SetActive(false);
        categoryList.SetActive(true);
        submit.gameObject.SetActive(false);
        status.text = target == null ? "Choose a feedback type." : "Choose a report reason.";
    }

    void Select(int index)
    {
        selected = index;
        categoryList.SetActive(false);
        fields.SetActive(true);
        submit.gameObject.SetActive(true);
        subject.gameObject.SetActive(target == null);
        selection.text = (target == null ? categoryLabels : reasonLabels)[index];
        ((TMP_Text)description.placeholder).text = target == null ? "Detailed description" :
            reasons[index] == "other" ? "Please describe the issue you experienced." : "Additional details (optional)";
        status.text = "";
    }

    void Update()
    {
        if (panel != null && panel.activeSelf && account != TrialBackendClient.UserId) panel.SetActive(false);
    }

    void Close() { if (!sending) panel.SetActive(false); }

    async void Send()
    {
        if (sending || submitted || selected < 0 || account != TrialBackendClient.UserId) return;
        bool report = target != null;
        string details = description.text.Trim();
        if (report && reasons[selected] == "other" && details.Length == 0)
        { status.text = "Please describe the issue you experienced."; return; }
        if (!report && (string.IsNullOrWhiteSpace(subject.text) || details.Length < 5))
        { status.text = "Enter a subject and at least 5 characters of detail."; return; }
        var data = new CommunityBackendClient.Submission
        {
            kind = report ? "player-report" : "feedback",
            targetUid = target, reason = report ? reasons[selected] : null,
            category = report ? null : categories[selected], subject = report ? null : subject.text.Trim(),
            description = details
        };
        sending = true;
        submit.interactable = cancel.interactable = subject.interactable = description.interactable = false;
        status.text = "Submitting...";
        try
        {
            await CommunityBackendClient.Submit(data);
            if (this == null || !panel.activeSelf || account != TrialBackendClient.UserId) return;
            submitted = true;
            status.text = report ? "Your report was submitted for review." : "Thank you! Your feedback has been submitted successfully.";
            fields.SetActive(false);
            submit.gameObject.SetActive(false);
            cancel.GetComponentInChildren<TMP_Text>().text = "Close";
        }
        catch (Exception error)
        {
            if (this != null && panel.activeSelf && account == TrialBackendClient.UserId) status.text = error.Message;
        }
        finally
        {
            sending = false;
            if (this != null) submit.interactable = cancel.interactable = subject.interactable = description.interactable = true;
        }
    }
}
