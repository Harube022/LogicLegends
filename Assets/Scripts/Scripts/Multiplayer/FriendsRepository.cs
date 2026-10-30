using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Firebase.Auth;
using Firebase.Database;

/// <summary>Account-owned social data. Photon room chat remains separate from these persistent direct messages.</summary>
public static class FriendsRepository
{
    public sealed class Profile
    {
        public string Id;
        public string Name;
    }

    public sealed class Message
    {
        public string Id;
        public string Sender;
        public string Text;
        public string InviteCode;
        public long SentAt;
    }

    public sealed class Notifications
    {
        public int Requests;
        public readonly Dictionary<string, int> UnreadByFriend = new Dictionary<string, int>();
        public int Chats => UnreadByFriend.Values.Sum();
    }

    public const int MessageLimit = 200;
    public const int MaxContacts = 100;
    private const string Social = "social";

    public static string MyId => FirebaseManager.IsReady ? FirebaseAuth.DefaultInstance.CurrentUser?.UserId : null;
    private static DatabaseReference Root => FirebaseDatabase.DefaultInstance.RootReference;
    private static string Mine()
    {
        string id = MyId;
        if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("Sign in to use Friends.");
        return id;
    }

    public static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128 &&
        id.All(c => !char.IsControl(c) && ".#$[]/".IndexOf(c) < 0);

    public static async Task<Profile> FindProfileAsync(string id)
    {
        Mine();
        id = (id ?? string.Empty).Trim();
        if (!ValidId(id)) return null;
        // Existing accounts already store their chosen name here. Reading this one
        // field by exact ID also makes accounts findable before their next sign-in.
        DataSnapshot snapshot = await Root.Child("users").Child(id).Child("username").GetValueAsync();
        return ReadProfile(id, snapshot);
    }

    private static Profile ReadProfile(string id, DataSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.Exists) return null;
        string name = snapshot.Value?.ToString();
        return new Profile { Id = id, Name = PlayerUsername.TryNormalize(name, out string valid) ? valid : "Adventurer" };
    }

    private static async Task<List<Profile>> LoadPeopleAsync(string node)
    {
        string id = Mine();
        DataSnapshot index = await Root.Child(Social).Child(node).Child(id).GetValueAsync();
        var people = new List<Profile>();
        foreach (DataSnapshot entry in index.Children)
        {
            if (people.Count >= MaxContacts) break;
            if (entry.Value == null || !ValidId(entry.Key)) continue;
            Profile profile = await FindProfileAsync(entry.Key);
            if (profile != null) people.Add(profile);
        }
        return people.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static Task<List<Profile>> LoadFriendsAsync() => LoadPeopleAsync("friends");
    public static Task<List<Profile>> LoadRequestsAsync() => LoadPeopleAsync("requests");

    /// <summary>Counts pending requests and incoming messages since each conversation was last opened.</summary>
    public static async Task<Notifications> LoadNotificationsAsync(Func<string, string> lastReadId)
    {
        string me = Mine();
        var result = new Notifications();
        DataSnapshot requests = await Root.Child(Social).Child("requests").Child(me).GetValueAsync();
        result.Requests = requests.Children.Count(entry => entry.Value != null);
        DataSnapshot friends = await Root.Child(Social).Child("friends").Child(me).GetValueAsync();
        foreach (DataSnapshot friend in friends.Children)
        {
            if (!ValidId(friend.Key) || friend.Value is not bool accepted || !accepted) continue;
            string seen = lastReadId(friend.Key);
            DataSnapshot messages = await Conversation(me, friend.Key).OrderByKey().LimitToLast(60).GetValueAsync();
            int unread = 0;
            foreach (DataSnapshot message in messages.Children)
                if (string.CompareOrdinal(message.Key, seen) > 0 &&
                    message.Child("sender").Value?.ToString() == friend.Key) unread++;
            if (unread > 0) result.UnreadByFriend[friend.Key] = unread;
        }
        return result;
    }

    public static IDisposable WatchNotifications(Func<string, string> lastReadId,
        Action<Notifications> changed, Action<Exception> failed)
        => new NotificationSubscription(Mine(), lastReadId, changed, failed);

    internal static int CountUnread(IEnumerable<Message> messages, string friend, string seen)
        => messages.Count(message => message.Sender == friend && string.CompareOrdinal(message.Id, seen) > 0);

    private sealed class NotificationSubscription : IDisposable
    {
        private readonly string me;
        private readonly Func<string, string> lastReadId;
        private readonly Action<Notifications> changed;
        private readonly Action<Exception> failed;
        private readonly Dictionary<string, IDisposable> conversations = new Dictionary<string, IDisposable>();
        private readonly Dictionary<string, List<Message>> histories = new Dictionary<string, List<Message>>();
        private IDisposable requests, friends;
        private int requestCount;
        private bool disposed;

        public NotificationSubscription(string me, Func<string, string> lastReadId,
            Action<Notifications> changed, Action<Exception> failed)
        {
            this.me = me; this.lastReadId = lastReadId; this.changed = changed; this.failed = failed;
            try
            {
                requests = new SnapshotSubscription(Root.Child(Social).Child("requests").Child(me), snapshot =>
                {
                    if (!Active) return;
                    requestCount = snapshot.Children.Count(entry => entry.Value is bool pending && pending);
                    Publish();
                }, ReportError);
                friends = new SnapshotSubscription(Root.Child(Social).Child("friends").Child(me), SyncFriends, ReportError);
            }
            catch { Dispose(); throw; }
        }

        private bool Active => !disposed && MyId == me;
        private void ReportError(Exception error) { if (Active) failed(error); }

        private void SyncFriends(DataSnapshot snapshot)
        {
            if (!Active) return;
            var accepted = new HashSet<string>(snapshot.Children
                .Where(entry => entry.Value is bool value && value && ValidId(entry.Key) && entry.Key != me)
                .Take(MaxContacts).Select(entry => entry.Key), StringComparer.Ordinal);
            foreach (string other in conversations.Keys.Where(id => !accepted.Contains(id)).ToArray())
            {
                conversations[other]?.Dispose();
                conversations.Remove(other);
                histories.Remove(other);
            }
            foreach (string other in accepted)
            {
                if (!Active) break;
                if (conversations.ContainsKey(other)) continue;
                conversations[other] = null;
                var stream = new SnapshotSubscription(Conversation(me, other).OrderByKey().LimitToLast(60), messages =>
                {
                    if (!Active || !conversations.ContainsKey(other)) return;
                    histories[other] = ReadMessages(messages, me, other);
                    Publish();
                }, ReportError);
                if (Active && conversations.ContainsKey(other)) conversations[other] = stream;
                else stream.Dispose();
            }
            Publish();
        }

        private void Publish()
        {
            if (!Active) return;
            var result = new Notifications { Requests = requestCount };
            foreach (var history in histories)
            {
                int unread = CountUnread(history.Value, history.Key, lastReadId(history.Key));
                if (unread > 0) result.UnreadByFriend[history.Key] = unread;
            }
            changed(result);
        }

        public void Dispose()
        {
            disposed = true;
            requests?.Dispose(); friends?.Dispose();
            foreach (var stream in conversations.Values) stream?.Dispose();
            conversations.Clear(); histories.Clear();
        }
    }

    public static async Task<bool> IsFriendAsync(string other)
    {
        string me = Mine();
        if (!ValidId(other)) return false;
        DataSnapshot value = await Root.Child(Social).Child("friends").Child(me).Child(other).GetValueAsync();
        return value.Exists && value.Value is bool accepted && accepted;
    }

    public static async Task<bool> RequestPendingAsync(string other)
    {
        string me = Mine();
        if (!ValidId(other)) return false;
        DataSnapshot value = await Root.Child(Social).Child("requests").Child(other).Child(me).GetValueAsync();
        return value.Exists;
    }

    public static async Task SendRequestAsync(string other)
    {
        string me = Mine();
        if (!ValidId(other) || other == me) throw new InvalidOperationException("Enter another player's ID.");
        if (await FindProfileAsync(other) == null) throw new InvalidOperationException("Player not found.");
        if (await IsFriendAsync(other)) throw new InvalidOperationException("This player is already your friend.");
        await Root.Child(Social).Child("requests").Child(other).Child(me).SetValueAsync(true);
    }

    public static async Task AcceptRequestAsync(string other)
    {
        string me = Mine();
        if (!ValidId(other)) throw new InvalidOperationException("Invalid player ID.");
        var update = new Dictionary<string, object>
        {
            [$"{Social}/friends/{me}/{other}"] = true,
            [$"{Social}/friends/{other}/{me}"] = true,
            [$"{Social}/requests/{me}/{other}"] = null
        };
        await Root.UpdateChildrenAsync(update);
    }

    public static Task DeclineRequestAsync(string other)
    {
        string me = Mine();
        if (!ValidId(other)) throw new InvalidOperationException("Invalid player ID.");
        return Root.Child(Social).Child("requests").Child(me).Child(other).SetValueAsync(null);
    }

    public static Task RemoveFriendAsync(string other)
    {
        string me = Mine();
        if (!ValidId(other)) throw new InvalidOperationException("Invalid player ID.");
        var update = new Dictionary<string, object>
        {
            [$"{Social}/friends/{me}/{other}"] = null,
            [$"{Social}/friends/{other}/{me}"] = null
        };
        return Root.UpdateChildrenAsync(update);
    }

    private static DatabaseReference Conversation(string me, string other)
    {
        if (!ValidId(other) || me == other) throw new InvalidOperationException("Choose a friend to chat with.");
        return string.CompareOrdinal(me, other) < 0
            ? Root.Child(Social).Child("messages").Child(me).Child(other)
            : Root.Child(Social).Child("messages").Child(other).Child(me);
    }

    public static async Task<List<Message>> LoadMessagesAsync(string other)
    {
        string me = Mine();
        DataSnapshot snapshot = await Conversation(me, other).OrderByKey().LimitToLast(60).GetValueAsync();
        return ReadMessages(snapshot, me, other);
    }

    public static IDisposable WatchMessages(string other, Action<List<Message>> changed, Action<Exception> failed)
    {
        string me = Mine();
        return new SnapshotSubscription(Conversation(me, other).OrderByKey().LimitToLast(60),
            snapshot => changed(ReadMessages(snapshot, me, other)), failed);
    }

    private sealed class SnapshotSubscription : IDisposable
    {
        private readonly Query query;
        private readonly EventHandler<ValueChangedEventArgs> handler;
        private bool disposed;
        public SnapshotSubscription(Query query, Action<DataSnapshot> changed, Action<Exception> failed)
        {
            this.query = query;
            var context = SynchronizationContext.Current;
            handler = (_, args) =>
            {
                void Deliver()
                {
                    if (disposed) return;
                    if (args.DatabaseError != null) failed(new InvalidOperationException(args.DatabaseError.Message));
                    else
                    {
                        try { changed(args.Snapshot); }
                        catch (Exception error) { failed(error); }
                    }
                }
                if (context == null || context == SynchronizationContext.Current) Deliver();
                else context.Post(_ => Deliver(), null);
            };
            query.ValueChanged += handler;
        }
        public void Dispose() { disposed = true; query.ValueChanged -= handler; }
    }

    private static List<Message> ReadMessages(DataSnapshot snapshot, string me, string other)
    {
        var result = new List<Message>();
        foreach (DataSnapshot entry in snapshot.Children)
        {
            string sender = entry.Child("sender").Value?.ToString();
            string body = entry.Child("text").Value?.ToString();
            string code = entry.Child("inviteCode").Value?.ToString();
            if (sender != me && sender != other || string.IsNullOrEmpty(body)) continue;
            long.TryParse(entry.Child("sentAt").Value?.ToString(), out long timestamp);
            result.Add(new Message
            {
                Id = entry.Key, Sender = sender, Text = InappropriateTextFilter.Censor(LobbyRules.CleanText(body, MessageLimit)),
                InviteCode = LobbyRules.IsValidCode(code) ? code : null, SentAt = timestamp
            });
        }
        return result;
    }

    public static async Task<Message> SendMessageAsync(string other, string text, string inviteCode = null)
    {
        string me = Mine();
        string body = LobbyRules.CleanText(text, MessageLimit);
        if (string.IsNullOrWhiteSpace(body)) throw new InvalidOperationException("Write a message first.");
        if (!string.IsNullOrEmpty(inviteCode) && !LobbyRules.IsValidCode(inviteCode))
            throw new InvalidOperationException("Invalid invite code.");
        body = InappropriateTextFilter.Censor(body);
        var reference = Conversation(me, other).Push();
        long sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var message = new Dictionary<string, object>
        {
            ["sender"] = me, ["text"] = body,
            ["inviteCode"] = inviteCode ?? string.Empty,
            ["sentAt"] = sentAt
        };
        await reference.SetValueAsync(message);
        return new Message { Id = reference.Key, Sender = me, Text = body, InviteCode = inviteCode, SentAt = sentAt };
    }
}
