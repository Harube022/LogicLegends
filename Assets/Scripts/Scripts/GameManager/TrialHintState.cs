/// <summary>A read-only description of the next action. Never contains an answer.</summary>
public readonly struct TrialHintAction
{
    public readonly string Key, Message;
    public readonly bool Paused;
    public bool Available => !string.IsNullOrEmpty(Key) && !string.IsNullOrEmpty(Message);
    public TrialHintAction(string key, string message, bool paused = false)
    { Key = key; Message = message; Paused = paused; }
    public TrialHintAction Suspend(bool paused) => new TrialHintAction(Key, Message, Paused || paused);
}

public interface ITrialHintSource
{
    TrialHintAction RequiredHintAction();
}

/// <summary>One delay per unfinished action; modals suspend rather than restart it.</summary>
public sealed class TrialHintState
{
    public const float DelaySeconds = 30f;
    public string Key { get; private set; }
    public string Message { get; private set; }
    public float WaitingSeconds { get; private set; }
    public bool Visible { get; private set; }
    public void Advance(TrialHintAction action, float seconds, bool paused = false)
    {
        if (!action.Available) { Clear(); return; }
        if (Key != action.Key)
        {
            Key = action.Key; WaitingSeconds = 0; Visible = false;
            Message = action.Message;
            // The elapsed sample can straddle two actions. Start at zero.
            return;
        }
        Message = action.Message;
        if (paused || action.Paused) { Visible = false; return; }
        WaitingSeconds += UnityEngine.Mathf.Max(0, seconds);
        Visible = WaitingSeconds >= DelaySeconds;
    }
    public void Clear()
    { Key = Message = null; WaitingSeconds = 0; Visible = false; }
    public void ShowNow(TrialHintAction action)
    {
        if (!action.Available || action.Paused) return;
        Key = action.Key; Message = action.Message;
        WaitingSeconds = DelaySeconds; Visible = true;
    }
}
