using ExitGames.Client.Photon;
using LogicLegends.Tests.Monsters;
using UnityEngine;

/// <summary>Host-device rotation; the room's fixed choice is shared by all participants.</summary>
public static class MultiplayerMonsterRotation
{
    public const string RoomKey = "ll.monster";
    const string NextMonsterKey = "ll.multiplayer.next-monster.v1";

    public static DevelopmentMonster NextHostedMonster =>
        PlayerPrefs.GetInt(NextMonsterKey, (int)DevelopmentMonster.Rajang) == (int)DevelopmentMonster.Khezu
            ? DevelopmentMonster.Khezu : DevelopmentMonster.Rajang;

    public static bool TryRead(Hashtable properties, out DevelopmentMonster monster)
    {
        monster = DevelopmentMonster.Rajang;
        if (properties == null || !properties.TryGetValue(RoomKey, out var value) || !(value is int id) ||
            (id != (int)DevelopmentMonster.Rajang && id != (int)DevelopmentMonster.Khezu)) return false;
        monster = (DevelopmentMonster)id;
        return true;
    }

    // Called once by the host's confirmed Start flow, never by joins or reconnects.
    internal static void RecordStartedMatch(DevelopmentMonster monster)
    {
        PlayerPrefs.SetInt(NextMonsterKey, (int)(monster == DevelopmentMonster.Rajang
            ? DevelopmentMonster.Khezu : DevelopmentMonster.Rajang));
        PlayerPrefs.Save();
    }
}
