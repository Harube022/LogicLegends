using UnityEngine;

/// <summary>Legacy serialized compatibility. Wizard speech has been removed.</summary>
[DisallowMultipleComponent]
public sealed class WizardDialogueVoice : MonoBehaviour
{
    public WizardVoiceCatalog catalog;
    [Range(0,1)] public float volume=.7f;
    public string CurrentSubtitle { get; private set; }
    public AudioSource Source => null;
    // Legacy compatibility only. Lefyahj is text-only; no playback is created.
    void Awake(){Stop();}
    public void Speak(string subtitle)
    {
        Stop();
    }
    public void Stop(){CurrentSubtitle=null;}
    void OnDisable(){Stop();}
    void OnDestroy(){Stop();}
}
