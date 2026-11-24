using System;
using UnityEngine;

namespace RobotSavaslari.Match
{
    [Serializable]
    public sealed class DuelSetup
    {
        public const string PreferenceKey = "RobotSavaslari.DuelSetup";
        public const int CatalogRevision = 1;
        public const string Spinner = "spinner", Fork = "fork";
        public int version;
        public string arenaId;
        public BotPersonality personality = (BotPersonality)(-1);
        public BotDifficulty difficulty = (BotDifficulty)(-1);
        public MatchMode mode = (MatchMode)(-1);
        public float seconds = -1f;
        public int catalogRevision;
        public string playerRobotId, opponentRobotId, playerRole, opponentRole, encounterId;

        public MatchRules Rules => new MatchRules { mode = mode, timeLimitSeconds = seconds };
        public bool Valid => (version == 1 || version == 2) && !string.IsNullOrWhiteSpace(arenaId) &&
            Enum.IsDefined(typeof(BotPersonality), personality) && Enum.IsDefined(typeof(BotDifficulty), difficulty) &&
            Rules.IsValid && (version == 1 ? catalogRevision == 0 && string.IsNullOrEmpty(playerRobotId) && string.IsNullOrEmpty(opponentRobotId) &&
                string.IsNullOrEmpty(playerRole) && string.IsNullOrEmpty(opponentRole) && string.IsNullOrEmpty(encounterId) :
                catalogRevision == CatalogRevision && playerRole == "player" && opponentRole == "bot" &&
                Guid.TryParseExact(encounterId, "N", out _) && KnownPlayerId(playerRobotId) &&
                opponentRobotId == OtherRobot(playerRobotId));

        public static string OtherRobot(string id) =>
            Combat.WorkshopWeapons.Pair(id?.Substring(id.LastIndexOf('/') + 1));

        private static bool KnownPlayerId(string id)
        {
            if (id == Spinner || id == Fork) return true;
            if (id == null || id.Length > 64) return false;
            var parts = id.Split('/');
            return parts.Length == 5 && parts[0] == "workshop" && parts[1] == "standard" &&
                (parts[2] == "soft" || parts[2] == "hard") && (parts[3] == "light" || parts[3] == "heavy") &&
                Combat.WorkshopWeapons.Index(parts[4]) >= 0;
        }

        public DuelSetup ForEncounter(string id, string player, string opponent)
        {
            var start = (DuelSetup)MemberwiseClone();
            start.version = 2;
            start.catalogRevision = CatalogRevision;
            start.playerRobotId = player;
            start.opponentRobotId = opponent;
            start.playerRole = "player";
            start.opponentRole = "bot";
            start.encounterId = id;
            return start;
        }

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
            return value.version == 1 && value.Valid ? value : null;
        }
    }

    public sealed class DuelStart
    {
        public readonly string ArenaId, PlayerRobotId, OpponentRobotId, PlayerRole, OpponentRole, EncounterId;
        public readonly int CatalogRevision;
        public readonly MatchMode Mode;
        public readonly float Seconds;
        public readonly BotPersonality Personality;
        public readonly BotDifficulty Difficulty;

        internal DuelStart(DuelSetup setup)
        {
            ArenaId = setup.arenaId;
            PlayerRobotId = setup.playerRobotId;
            OpponentRobotId = setup.opponentRobotId;
            PlayerRole = setup.playerRole;
            OpponentRole = setup.opponentRole;
            EncounterId = setup.encounterId;
            CatalogRevision = setup.catalogRevision;
            Mode = setup.mode;
            Seconds = setup.seconds;
            Personality = setup.personality;
            Difficulty = setup.difficulty;
        }

        internal DuelSetup ToSetup() => DuelSetup.Create(ArenaId, Personality, Difficulty,
            new MatchRules { mode = Mode, timeLimitSeconds = Seconds }).ForEncounter(EncounterId, PlayerRobotId, OpponentRobotId);
    }
}
