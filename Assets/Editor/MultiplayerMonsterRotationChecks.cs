using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LogicLegends.Tests.Monsters;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Local rotation regression checks. No room, scene or encounter is created or modified.</summary>
public static class MultiplayerMonsterRotationChecks
{
    [Serializable] public sealed class Report
    {
        public bool passed, hostPreferenceRestored;
        public string protocol, scope = "Persistent host-device rotation and room metadata only. Does not claim live Photon replication or Android verification.";
        public List<string> sequence = new List<string>(), checks = new List<string>();
    }

    [MenuItem("Logic Legends/Multiplayer/Verify Hosted Monster Rotation")]
    public static void Verify() => Run();

    public static Report Run()
    {
        if (Application.isPlaying || PhotonNetwork.InRoom)
            throw new InvalidOperationException("Run stopped outside a Photon room; the real host preference is restored afterward.");
        var type = typeof(MultiplayerMonsterRotation);
        var preference = type.GetField("NextMonsterKey", BindingFlags.NonPublic | BindingFlags.Static);
        var record = type.GetMethod("RecordStartedMatch", BindingFlags.NonPublic | BindingFlags.Static);
        if (preference == null || record == null) throw new InvalidOperationException("The existing rotation owner changed; update these checks.");
        string key = (string)preference.GetRawConstantValue();
        bool existed = PlayerPrefs.HasKey(key); int original = PlayerPrefs.GetInt(key);
        var report = new Report { protocol = LobbyRules.Protocol };
        void Check(bool condition, string label)
        {
            report.checks.Add((condition ? "PASS " : "FAIL ") + label);
            if (!condition) throw new InvalidOperationException(label);
        }
        void Started(DevelopmentMonster monster) => record.Invoke(null, new object[] { monster });
        try
        {
            PlayerPrefs.DeleteKey(key);
            Check(MultiplayerMonsterRotation.NextHostedMonster == DevelopmentMonster.Rajang, "A new host device starts with Rajang");
            var reservedRoom = new Hashtable { [MultiplayerMonsterRotation.RoomKey] = (int)MultiplayerMonsterRotation.NextHostedMonster };
            Check(MultiplayerMonsterRotation.TryRead(reservedRoom, out _) && MultiplayerMonsterRotation.NextHostedMonster == DevelopmentMonster.Rajang,
                "Reserving and reading an unstarted room does not consume the next monster");
            for (int i = 0; i < 6; i++)
            {
                var expected = i % 2 == 0 ? DevelopmentMonster.Rajang : DevelopmentMonster.Khezu;
                var next = MultiplayerMonsterRotation.NextHostedMonster;
                Check(next == expected, "Confirmed hosted match " + (i + 1) + " selects " + expected);
                var properties = new Hashtable { [MultiplayerMonsterRotation.RoomKey] = (int)next };
                Check(MultiplayerMonsterRotation.TryRead(properties, out var guest) && guest == next,
                    "Guests and rejoining clients read the fixed room choice for match " + (i + 1));
                report.sequence.Add(next.ToString());
                Started(next);
                var after = MultiplayerMonsterRotation.NextHostedMonster;
                Started(next);
                Check(MultiplayerMonsterRotation.NextHostedMonster == after, "Repeated confirmation cannot double-advance match " + (i + 1));
                Check(MultiplayerMonsterRotation.TryRead(properties, out guest) && guest == next,
                    "Advancing the host preference cannot alter an existing room's initial guardian metadata");
            }
            Check(MultiplayerMonsterRotation.TryRead(reservedRoom, out var reserved) && reserved == DevelopmentMonster.Rajang,
                "A room keeps its reserved guardian independently of later hosted matches");
            Check(!MultiplayerMonsterRotation.TryRead(null, out _) && !MultiplayerMonsterRotation.TryRead(new Hashtable(), out _),
                "Missing room metadata is rejected");
            foreach (object invalid in new object[] { -1, 2, 99, "Rajang", "0", (byte)0, false })
                Check(!MultiplayerMonsterRotation.TryRead(new Hashtable { [MultiplayerMonsterRotation.RoomKey] = invalid }, out _),
                    "Invalid room metadata is rejected: " + invalid + " (" + invalid.GetType().Name + ")");
            PlayerPrefs.SetInt(key, 99);
            Check(MultiplayerMonsterRotation.NextHostedMonster == DevelopmentMonster.Rajang, "A damaged local preference safely returns to Rajang");
            report.passed = true;
        }
        finally
        {
            if (existed) PlayerPrefs.SetInt(key, original); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            report.hostPreferenceRestored = PlayerPrefs.HasKey(key) == existed && (!existed || PlayerPrefs.GetInt(key) == original);
            report.passed &= report.hostPreferenceRestored;
            Directory.CreateDirectory("Temp/MultiplayerQA");
            File.WriteAllText("Temp/MultiplayerQA/hosted-monster-rotation.json", JsonUtility.ToJson(report, true));
        }
        Debug.Log("Hosted monster rotation: " + string.Join(" → ", report.sequence) + "; " + report.checks.Count + " checks; original preference restored.");
        return report;
    }
}
