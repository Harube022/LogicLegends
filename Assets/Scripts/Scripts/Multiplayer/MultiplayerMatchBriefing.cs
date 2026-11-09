using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Topics;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Local, optional lobby orientation. Never owns readiness, progress or a match clock.</summary>
[DisallowMultipleComponent]
public sealed class MultiplayerMatchBriefing : MonoBehaviour
{
    public const string SeenPreference = "OpenArchive.MatchBriefing.v1.Seen";
    public const int PageCount = 4;
    MultiplayerLobbyUI lobby;
    GameObject lobbyOverlay, root;
    RectTransform card, entryRect, safeArea;
    TMP_Text title, pageLabel, body, nextLabel;
    Button entry, previous, next, skip;
    ScrollRect scroll;
    Scrollbar scrollbar;
    MultiplayerBriefingVideo demonstration;
    int page;
    bool autoConsidered;
    Vector2 screen;
    Rect lastSafe;

    public bool Visible => root != null && root.activeInHierarchy;
    public bool HasSeen => PlayerPrefs.GetInt(SeenPreference, 0) != 0;
    public int CurrentPage => page;
    public Button EntryButton => entry;
    public ScrollRect ReadingScroll => scroll;
    public Scrollbar VisibleScrollbar => scrollbar;
    public RectTransform Card => card;
    public MultiplayerBriefingVideo Demonstration => demonstration;
    public string CurrentBody => body != null ? body.text : "";
    bool Available => lobby != null && lobby.IsOpen && SceneManager.GetActiveScene().name == "Main Menu" &&
        MultiplayerLobbyService.Instance != null && !MultiplayerLobbyService.Instance.Starting && !MultiplayerLobbyService.Instance.Busy;

    public void Initialize(MultiplayerLobbyUI owner, GameObject overlay)
    {
        lobby = owner; lobbyOverlay = overlay;
        if (root != null || overlay == null) return;
        var inside = overlay.transform.Find("SafeArea/WoodFrame/Interior");
        if (inside == null) return;
        entry = MultiplayerUIArt.Action("HowToPlay_Multiplayer", inside, "HOW TO PLAY", .78f, .075f, .78f, .075f);
        entryRect = (RectTransform)entry.transform; entryRect.sizeDelta = new Vector2(180, 64);
        entry.GetComponent<Image>().pixelsPerUnitMultiplier = 4.5f;
        var entryText = entry.GetComponentInChildren<TMP_Text>(); entryText.fontSizeMin = 18; entryText.fontSizeMax = 23;
        entry.onClick.AddListener(() => Review());
        Build();
    }

    void Build()
    {
        root = new GameObject("Multiplayer_Match_Briefing", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 420;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var dim = root.AddComponent<Image>(); dim.color = new Color(.012f, .026f, .035f, .82f);
        safeArea = MultiplayerUIArt.Rect("SafeArea", root.transform); MultiplayerSafeArea.Apply(safeArea); var safe = safeArea;
        card = MultiplayerUIArt.Rect("Briefing_Wood_Frame", safe, .035f, .055f, .965f, .945f);
        var wood = card.gameObject.AddComponent<Image>(); wood.sprite = StageUITheme.Panel; wood.type = Image.Type.Sliced; wood.pixelsPerUnitMultiplier = 2.3f;
        var paper = MultiplayerUIArt.Paper("Briefing_Paper", card, .045f, .065f, .955f, .935f);
        pageLabel = MultiplayerUIArt.Label("Page_Progress", paper, "", .045f, .90f, .78f, .97f, 22, false, new Color(.04f, .32f, .33f));
        pageLabel.alignment = TextAlignmentOptions.MidlineLeft;
        title = MultiplayerUIArt.Label("Briefing_Title", paper, "", .045f, .79f, .86f, .90f, 36, false, StageUITheme.ReadingInk);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        var close = MultiplayerUIArt.Action("Skip_Briefing_X", paper, "X", .89f, .83f, .97f, .95f);
        close.onClick.AddListener(Dismiss);
        var area = MultiplayerUIArt.Rect("Briefing_Scroll", paper, .045f, .235f, .49f, .77f);
        scroll = area.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 38;
        var viewport = MultiplayerUIArt.Rect("Viewport", area, 0, 0, .965f, 1);
        viewport.gameObject.AddComponent<RectMask2D>(); viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .001f);
        var content = MultiplayerUIArt.Rect("Reading_Content", viewport, 0, 1, 1, 1); content.pivot = new Vector2(.5f, 1);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(4, 12, 8, 12);
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        body = StageUITheme.Text("Briefing_Text", content, "", 28, true); body.color = StageUITheme.ReadingInk;
        body.alignment = TextAlignmentOptions.TopLeft; body.richText = true; body.enableAutoSizing = false;
        body.textWrappingMode = TextWrappingModes.Normal; body.raycastTarget = false;
        scroll.viewport = viewport; scroll.content = content;
        var rail = MultiplayerUIArt.Rect("Visible_Scrollbar", area, .977f, .02f, 1, .98f);
        rail.gameObject.AddComponent<Image>().color = new Color(.23f, .20f, .12f, .22f);
        var slide = MultiplayerUIArt.Rect("Sliding_Area", rail); slide.offsetMin = new Vector2(2, 2); slide.offsetMax = new Vector2(-2, -2);
        var handle = MultiplayerUIArt.Rect("Handle", slide); handle.gameObject.AddComponent<Image>().color = new Color(.055f, .36f, .35f);
        scrollbar = rail.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handle.GetComponent<Image>(); scrollbar.handleRect = handle;
        scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        var media = MultiplayerUIArt.Rect("Watch_Gameplay", paper, .52f, .235f, .955f, .77f);
        demonstration = media.gameObject.AddComponent<MultiplayerBriefingVideo>(); demonstration.Build(media);
        var footer = MultiplayerUIArt.Label("Review_Reminder", paper, "Watch the demonstration, then practise together. Reopen HOW TO PLAY anytime.", .05f, .18f, .95f, .23f, 19, true, new Color(.12f, .31f, .29f));
        footer.alignment = TextAlignmentOptions.Center;
        previous = MultiplayerUIArt.Action("Previous_Briefing_Page", paper, "BACK", .045f, .045f, .275f, .16f);
        previous.onClick.AddListener(PreviousPage);
        skip = MultiplayerUIArt.Action("Skip_Briefing", paper, "SKIP", .315f, .045f, .545f, .16f);
        skip.onClick.AddListener(Dismiss);
        next = MultiplayerUIArt.Action("Next_Briefing_Page", paper, "NEXT", .585f, .045f, .955f, .16f);
        nextLabel = next.GetComponentInChildren<TMP_Text>(); next.onClick.AddListener(NextPage);
        root.SetActive(false); RefreshPage();
    }

    void Update()
    {
        if (root == null) return;
        MultiplayerSafeArea.Apply(safeArea);
        bool available = Available;
        entry.gameObject.SetActive(available);
        bool inRoom = MultiplayerLobbyService.Instance != null && MultiplayerLobbyService.Instance.InLobby;
        entryRect.anchorMin = entryRect.anchorMax = new Vector2(inRoom ? .67f : .78f, inRoom ? .082f : .075f);
        entryRect.anchoredPosition = Vector2.zero;
        if (!available)
        {
            Hide(false);
            if (lobby == null || !lobby.IsOpen) autoConsidered = false;
            return;
        }
        if (!autoConsidered)
        {
            autoConsidered = true;
            if (!HasSeen) Review();
        }
        if (Visible && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Dismiss();
        if (screen != new Vector2(Screen.width, Screen.height) || lastSafe != Screen.safeArea)
        { screen = new Vector2(Screen.width, Screen.height); lastSafe = Screen.safeArea; Canvas.ForceUpdateCanvases(); }
    }

    public bool Review()
    {
        if (root == null || !Available) return false;
        autoConsidered = true; page = 0; root.SetActive(true); root.transform.SetAsLastSibling(); RefreshPage(); return true;
    }
    public void NextPage() { if (!Visible) return; if (page == PageCount - 1) Dismiss(); else { page++; RefreshPage(); } }
    public void PreviousPage() { if (!Visible) return; page = Mathf.Max(0, page - 1); RefreshPage(); }
    public void Dismiss() => Hide(true);
    void Hide(bool remember)
    {
        if (root == null) return;
        if (remember && Visible) { PlayerPrefs.SetInt(SeenPreference, 1); PlayerPrefs.Save(); }
        root.SetActive(false);
        demonstration.Hide();
    }
    void RefreshPage()
    {
        page = Mathf.Clamp(page, 0, PageCount - 1);
        pageLabel.text = "THE OPEN ARCHIVE  |  " + (page + 1) + " / " + PageCount;
        title.text = PageTitle(page); body.text = PageBody(page); previous.interactable = page > 0;
        if (Visible) demonstration.Show(page);
        skip.gameObject.SetActive(page < PageCount - 1); nextLabel.text = page == PageCount - 1 ? "GOT IT" : "NEXT";
        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
    }
    public static string PageTitle(int index)
    {
        switch (index)
        {
            case 0: return "RESTORE THE OPEN ARCHIVE";
            case 1: return "COOPERATE AND PREPARE";
            case 2: return "SURVIVE THE NIGHT";
            default: return "RESCUE YOUR TEAM AND ESCAPE";
        }
    }
    public static string PageBody(int index)
    {
        switch (index)
        {
            case 0:
                return "<b>ONE TEAM, TEN TOPICS</b>\nComplete all 10 Discrete Mathematics topics. Only one topic can run at a time. Read its Book to begin or continue; your team's solved challenges are shared.\n\n" +
                    "<b>ONE SHARED MATCH CLOCK</b>\nYour team starts with " + (ArchiveTopicSession.MatchBaseSeconds / 60).ToString("0") + " minutes. Completing a whole topic earns +" + (ArchiveTopicSession.MatchTopicBonusSeconds / 60).ToString("0") + " minutes, once per topic.\n\n" +
                    "The match clock keeps running while you read, use menus or face a monster challenge. Topic challenge clocks are separate. Keep moving and help one another.";
            case 1:
                return "<b>RESTORE EVERY CRYSTAL</b>\nAccepted topic work rebuilds the crystal. Meaningful contributions from every eligible teammate light the remaining team runes. Share the solving work; pressing Confirm repeatedly is not cooperation.\n\n" +
                    "<b>TEAM SIGNALS</b>\nOpen TEAM to check teammates' hearts and status. Use PING for Help Here, Need Medkit, Revive Teammate or Go to Portal. Follow the direction arrow and ground marker; signals last 12 seconds.\n\n" +
                    "<b>SHARE YOUR SUPPLIES</b>\nChoose an inventory item, then use its action button. You can give items to a nearby teammate or use supported items on them.\n\n" +
                    "A medkit restores one missing heart. A revival kit rescues a downed teammate. Flashlights help at night; spare batteries refill them. Tranquilizer darts can put a guardian to sleep briefly.\n\n"+
                    "<b>TEAM COIN REWARDS</b>\nEarn up to 1,000 coins: 600 for team victory, 25 per fully restored crystal (up to 250), and up to 150 for participation. Three distinct confirmed puzzle contributions or useful teammate-support actions unlock your full share; fewer contributions earn a smaller share. Repeated button presses do not count.\n\n"+
                    "Being downed does not erase your work. Defeat awards up to 150 consolation coins for completed topics. Admin-assisted and offline practice matches earn no permanent currency. Gems are reserved for the first full-participation victory and first all-crystal victory, once per account. Rewards are earned only after the account service confirms them.";
            case 2:
                return "<b>MISTAKES WAKE A GUARDIAN</b>\nFour mistakes fully awaken the first guardian. Incorrect answers and challenge timeouts have consequences, so discuss the reason for your answer before confirming.\n\n" +
                    "<b>THE WORLD CHANGES WITH YOUR PROGRESS</b>\nAt seven completed topics, night arrives and a second guardian enters. After the entrance warning, guardians can hunt even if your team has fewer than four mistakes.\n\n" +
                    "Prepare your flashlight and supplies before dark. Stay aware of your teammates, help anyone being chased, and keep working toward the portal.\n\n"+
                    "<b>BREAK THE HUNT</b>\nSprint has no stamina limit. After 30 seconds of active pursuit, your guardian becomes enraged and accelerates. At 40 seconds, it captures its eligible target through the usual jumpscare and barrier challenge. Use a tranquilizer or teammate beacon to delay pursuit. Sedation, active distractions, cinematics and another ongoing capture pause this chase clock.\n\n"+
                    "<b>READ THE ROAD AND THE GUARDIAN</b>\nAfter every two completed topics, a short rune disturbance may form on a road margin. Its golden outline warns for three seconds. The marked area slows walking briefly; use the clear paved lane beside it. It does not remove a heart or prevent jumping.\n\n"+
                    "The Primal Axiom's orange tell warns of a short rush. The Proof Devourer's crimson tell marks the route it is reading. Change direction rather than running straight through the warning.\n\n"+
                    "<b>RESONANCE BEACONS</b>\nTap the hand button beside a ready teal beacon to draw one nearby hunting guardian toward it after a rising chime. Use those few seconds to help a solver or rescue a teammate. Sleeping guardians ignore beacons. Every beacon shares a long recharge, so plan the distraction together.";
            default:
                return "<b>CAPTURED DOES NOT MEAN DEFEATED</b>\nA captured player has " + (KhezuHuntingController.BarrierChallengeSeconds / 60).ToString("0") + " minutes to solve a Discrete Mathematics barrier challenge. A wrong answer or timeout costs one heart and briefly stuns the player. The other teammates can keep moving.\n\n" +
                    "<b>REVIVE AT THE NEST</b>\nA player with zero hearts is carried to the monster nest and spectates until revived. Bring a revival kit to rescue them. If every teammate is down, the team loses.\n\n" +
                    "<b>EVERY SURVIVOR MUST ESCAPE</b>\nAfter all 10 topics are complete, the northern portal opens and a separate " + (ArchiveTopicSession.MatchEscapeSeconds / 60).ToString("0") + "-minute escape countdown begins. Every surviving teammate must enter before it ends. Running out of match or escape time means team defeat.";
        }
    }
    void OnDisable() { Hide(false); autoConsidered = false; }
    void OnDestroy() { if (entry != null) Destroy(entry.gameObject); if (root != null) Destroy(root); }
}
