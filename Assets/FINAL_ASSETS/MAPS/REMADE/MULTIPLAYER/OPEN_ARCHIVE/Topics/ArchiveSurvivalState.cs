using System;
using System.Collections.Generic;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    public enum ArchiveSurvivalItem { Flashlight, Battery, Medkit, RevivalKit, Tranquilizer, TranquilizerAmmo }
    [Serializable] public sealed class ArchiveTopicChest
    {
        public int topic, resetSerial, owner;
        public ArchiveSurvivalItem item;
        public bool claimed;
        public bool orphaned;
        public double ownerAbsentSince=-1;
        public double completedAt, claimedAt;
    }
    [Serializable] public sealed class ArchiveSurvivalInventory
    {
        public int actor;
        public int[] items = new int[6];
        public int equippedItem=-1;
        public bool flashlightOn;
        public float batterySeconds;
        public double batteryUpdatedAt, nextUseAt;
    }
    [Serializable] public sealed class ArchiveMonsterSedation
    {
        public string encounterId;
        public double until, immuneUntil;
    }
    [Serializable] public sealed class ArchiveSurvivalState
    {
        public List<ArchiveTopicChest> chests = new List<ArchiveTopicChest>();
        public List<ArchiveSurvivalInventory> players = new List<ArchiveSurvivalInventory>();
        public List<ArchiveDownedActor> downed = new List<ArchiveDownedActor>();
        public List<ArchiveMonsterSedation> sedations = new List<ArchiveMonsterSedation>();
        public int shotSerial, shotActor;
        public double shotAt;
        public Vector3 shotOrigin, shotEnd;
        public bool shotHit;
        public bool defeated;
        public double defeatedAt;
        public string defeatReason;
    }
}
