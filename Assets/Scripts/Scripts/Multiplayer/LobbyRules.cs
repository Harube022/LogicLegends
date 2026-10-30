using System;
using System.Linq;
using Photon.Realtime;
using NetPlayer = Photon.Realtime.Player;

public static class LobbyRules
{
    public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    // Updated peers share the encounter authority, reconnect behavior and DM1 bank.
    public const string Protocol = "LL-private-lobby-20";
    public const int RecoverySeatMilliseconds = 45000;
    public const float RecoverySeconds = 30f;
    public const int RecoveryAttempts = 4;
    public const string ReadyKey = "ll.ready", VersionKey = "ll.version", StateKey = "ll.state", SceneKey = "ll.scene";
    public const string Waiting = "waiting", Locking = "locking", Loading = "loading";
    public const byte ChatEvent = 74;
    public const int Capacity = 4, ChatLimit = 200, HistoryLimit = 60;
    public const float ChatCooldown = 1f;
    public static string NormalizeCode(string value) => (value ?? "").Trim().ToUpperInvariant();
    public static bool IsValidCode(string value) => value != null && value.Length == 6 && value.All(c => CodeAlphabet.IndexOf(c) >= 0);
    public static string NewCode()
    {
        var bytes = new byte[6];
        using (var random = System.Security.Cryptography.RandomNumberGenerator.Create()) random.GetBytes(bytes);
        // 32 symbols make masking unbiased.
        return new string(bytes.Select(b => CodeAlphabet[b & 31]).ToArray());
    }
    public static string CleanText(string text, int limit)
    {
        string value = new string((text ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (value.Length <= limit) return value;
        if (char.IsHighSurrogate(value[limit - 1])) limit--;
        return value.Substring(0, limit);
    }
    public static string DisplayName(NetPlayer player) => string.IsNullOrWhiteSpace(player.NickName)
        ? "Adventurer " + player.ActorNumber : CleanText(player.NickName, 32);
    public static bool IsReady(NetPlayer player) => !player.IsInactive && player.CustomProperties.TryGetValue(ReadyKey, out var value) && value is bool ready && ready;
    public static bool CanStart(NetPlayer[] players) => players.Length >= 2 && players.Length <= Capacity && players.All(IsReady);
    public static string JoinError(short code)
    {
        switch (code)
        {
            case ErrorCode.GameDoesNotExist: return "Lobby not found. Check the code, game version and region.";
            case ErrorCode.GameFull: return "This lobby is full (4/4). Ask the host to make space.";
            case ErrorCode.GameClosed: return "This lobby is closed or the game has already started.";
            case ErrorCode.MaxCcuReached: return "The multiplayer service is full. Please try again later.";
            default: return "Couldn't join the lobby. Check your connection and try again.";
        }
    }
}
