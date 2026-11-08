using System;
using UnityEngine;

[CreateAssetMenu(menuName="Logic Legends/Wizard Voice Catalog")]
public sealed class WizardVoiceCatalog : ScriptableObject
{
    [Serializable] public sealed class Entry { public string id; [TextArea] public string subtitle; public AudioClip clip; }
    public Entry[] lines=new Entry[0];
    public AudioClip Find(string subtitle)
    {foreach(var line in lines)if(string.Equals(line.subtitle,subtitle,StringComparison.Ordinal))return line.clip;return null;}
}
