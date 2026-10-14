using Firebase.Auth;
using UnityEngine;

/// <summary>Saved, per-player garden and library milestones on this device.</summary>
public static class LogicGardenProgress
{
    private const string ConversationField = "wizardConversationCompleted";
    private const string LibraryField = "logicLibraryVisited";
    private const string IntroductionField = "logicLibraryIntroductionSeen";
    private const string ControlsField = "controlsTutorialCompleted";
    private const string TourField = "logicLibraryTourCompleted";
    private static string userKey;

    public static bool ConversationCompleted { get; private set; }
    public static bool LibraryVisited { get; private set; }
    public static bool IntroductionSeen { get; private set; }
    public static bool ControlsTutorialCompleted { get; private set; }
    public static bool LibraryTourCompleted { get; private set; }
#if UNITY_EDITOR
    // Play Mode checks use an isolated profile; never overwrite the signed-in save.
    public static string ValidationProfile { get; set; }
#endif

    public static void Load()
    {
        FirebaseUser user = FirebaseManager.IsReady ? FirebaseAuth.DefaultInstance.CurrentUser : null;
        string newKey = user != null ? user.UserId : "offline";
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(ValidationProfile)) newKey = ValidationProfile;
#endif
        userKey = newKey;
        ConversationCompleted = PlayerPrefs.GetInt(Key(ConversationField), 0) != 0;
        LibraryVisited = PlayerPrefs.GetInt(Key(LibraryField), 0) != 0;
        IntroductionSeen = PlayerPrefs.GetInt(Key(IntroductionField), 0) != 0;
        ControlsTutorialCompleted = PlayerPrefs.GetInt(Key(ControlsField), 0) != 0;
        LibraryTourCompleted = PlayerPrefs.GetInt(Key(TourField), 0) != 0;
    }

    public static void CompleteConversation() { Load(); ConversationCompleted = true; Save(ConversationField); }
    public static void VisitLibrary() { Load(); LibraryVisited = true; Save(LibraryField); }
    public static void SeeIntroduction() { Load(); IntroductionSeen = true; Save(IntroductionField); }
    public static void CompleteControlsTutorial() { Load(); ControlsTutorialCompleted = true; Save(ControlsField); }
    public static void CompleteLibraryTour() { Load(); LibraryTourCompleted = true; Save(TourField); }

    /// <summary>Call only when creating a new game/profile, never on a return visit.</summary>
    public static void ResetForNewGame()
    {
        Load();
        PlayerPrefs.DeleteKey(Key(ConversationField));
        PlayerPrefs.DeleteKey(Key(LibraryField));
        PlayerPrefs.DeleteKey(Key(IntroductionField));
        PlayerPrefs.DeleteKey(Key(ControlsField));
        PlayerPrefs.DeleteKey(Key(TourField));
        PlayerPrefs.Save();
        ConversationCompleted = LibraryVisited = IntroductionSeen = ControlsTutorialCompleted = LibraryTourCompleted = false;
    }

    private static string Key(string field) => "logicGarden." + (userKey ?? "offline") + "." + field;

    private static void Save(string field)
    {
        PlayerPrefs.SetInt(Key(field), 1);
        PlayerPrefs.Save();
    }
}
