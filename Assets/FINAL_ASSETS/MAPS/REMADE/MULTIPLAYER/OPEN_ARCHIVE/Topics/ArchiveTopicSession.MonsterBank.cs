using System;
using LogicLegends.OpenArchive.Hunting;

namespace LogicLegends.OpenArchive.Topics
{
    public sealed partial class ArchiveTopicSession
    {
        public const int MonsterChallengeBankSize=50;

        /// <summary>Pure match-seeded shuffled deck; each 50 draws visits every question once.</summary>
        public static int MonsterChallengeIndex(int matchSeed,int drawIndex)
        {
            if(drawIndex<0)throw new ArgumentOutOfRangeException(nameof(drawIndex));
            int cycle=drawIndex/MonsterChallengeBankSize;
            var deck=ArchiveTopicQuestions.Permutation(MonsterChallengeBankSize,MonsterDeckSeed(matchSeed,cycle));
            if(cycle>0)
            {
                var previous=ArchiveTopicQuestions.Permutation(MonsterChallengeBankSize,MonsterDeckSeed(matchSeed,cycle-1));
                // Only the first two entries change, so the previous cycle's last entry
                // is stable without storing a deck or recursively rebuilding old cycles.
                if(deck[0]==previous[MonsterChallengeBankSize-1])
                {int first=deck[0];deck[0]=deck[1];deck[1]=first;}
            }
            return deck[drawIndex%MonsterChallengeBankSize];
        }

        static int MonsterDeckSeed(int matchSeed,int cycle) => unchecked(matchSeed*397 ^ cycle*1664525 ^ 0x4D4F4E53);

        static int StableMonsterEncounterHash(string encounterId)
        {
            unchecked
            {
                int hash=17;
                if(encounterId!=null)foreach(char c in encounterId)hash=hash*31+c;
                return hash;
            }
        }

        static bool BarrierEncounterMatches(ArchiveBarrierChallengeState barrier,KhezuHuntingController monster) =>
            barrier!=null&&monster!=null&&string.Equals(barrier.encounterId??"",monster.encounterId??"",StringComparison.Ordinal);
    }
}
