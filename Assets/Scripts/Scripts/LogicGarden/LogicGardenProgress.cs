using Firebase.Auth;
using UnityEngine;

/// <summary>Saved, per-player garden and library milestones on this device.</summary>
public static class LogicGardenProgress
{
    private const string ConversationField = "wizardConversationCompleted";
    private const string LibraryField = "logicLibraryVisited";
    private const string IntroductionField = "logicLibraryIntroductionSeen";
    private static string userKey;

    public static bool ConversationCompleted { get; private set; }
    public static bool LibraryVisited { get; private set; }
    public static bool IntroductionSeen { get; private set; }

    public static void Load()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance != null ? FirebaseAuth.DefaultInstance.CurrentUser : null;
        string newKey = user != null ? user.UserId : "offline";
        userKey = newKey;
        ConversationCompleted = PlayerPrefs.GetInt(Key(ConversationField), 0) != 0;
        LibraryVisited = PlayerPrefs.GetInt(Key(LibraryField), 0) != 0;
        IntroductionSeen = PlayerPrefs.GetInt(Key(IntroductionField), 0) != 0;
    }

    public static void CompleteConversation() { Load(); ConversationCompleted = true; Save(ConversationField); }
    public static void VisitLibrary() { Load(); LibraryVisited = true; Save(LibraryField); }
    public static void SeeIntroduction() { Load(); IntroductionSeen = true; Save(IntroductionField); }

    /// <summary>Call only when creating a new game/profile, never on a return visit.</summary>
    public static void ResetForNewGame()
    {
        Load();
        PlayerPrefs.DeleteKey(Key(ConversationField));
        PlayerPrefs.DeleteKey(Key(LibraryField));
        PlayerPrefs.DeleteKey(Key(IntroductionField));
        PlayerPrefs.Save();
        ConversationCompleted = LibraryVisited = IntroductionSeen = false;
    }

    private static string Key(string field) => "logicGarden." + (userKey ?? "offline") + "." + field;

    private static void Save(string field)
    {
        PlayerPrefs.SetInt(Key(field), 1);
        PlayerPrefs.Save();
    }
}
