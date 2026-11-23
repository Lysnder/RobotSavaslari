using System;
using UnityEngine;

namespace RobotSavaslari.Match
{
    [Serializable]
    public sealed class DuelSetup
    {
        public const string PreferenceKey = "RobotSavaslari.DuelSetup";
        public int version;
        public string arenaId;
        public BotPersonality personality = (BotPersonality)(-1);
        public BotDifficulty difficulty = (BotDifficulty)(-1);
        public MatchMode mode = (MatchMode)(-1);
        public float seconds = -1f;

        public MatchRules Rules => new MatchRules { mode = mode, timeLimitSeconds = seconds };
        public bool Valid => version == 1 && !string.IsNullOrWhiteSpace(arenaId) &&
            Enum.IsDefined(typeof(BotPersonality), personality) && Enum.IsDefined(typeof(BotDifficulty), difficulty) &&
            Rules.IsValid;

        public static DuelSetup Create(string arena, BotPersonality bot, BotDifficulty level, MatchRules rules) =>
            new DuelSetup { version = 1, arenaId = arena, personality = bot, difficulty = level,
                mode = rules.mode, seconds = rules.timeLimitSeconds };

        public static DuelSetup Quick() => Create("Open", BotPersonality.Aggressive, BotDifficulty.Easy,
            new MatchRules { mode = MatchMode.Single, timeLimitSeconds = 60 });
        public static DuelSetup Tactical() => Create("Tactical", BotPersonality.Counter, BotDifficulty.Hard,
            new MatchRules { mode = MatchMode.Single, timeLimitSeconds = 120 });
        public static DuelSetup Series() => Create("Open", BotPersonality.Aggressive, BotDifficulty.Hard,
            new MatchRules { mode = MatchMode.FirstToTwo, timeLimitSeconds = 60 });

        public bool SameAs(DuelSetup other) => other != null && arenaId == other.arenaId &&
            personality == other.personality && difficulty == other.difficulty && mode == other.mode && seconds == other.seconds;

        public static DuelSetup Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 4096) return null;
            var value = new DuelSetup();
            try { JsonUtility.FromJsonOverwrite(json, value); }
            catch (ArgumentException) { return null; }
            return value.Valid ? value : null;
        }
    }
}
