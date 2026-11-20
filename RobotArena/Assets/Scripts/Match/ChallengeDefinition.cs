using System;
using System.Collections.Generic;
using RobotSavaslari.Arena;
using UnityEngine;

namespace RobotSavaslari.Match
{
    [Serializable]
    public sealed class ChallengeStage
    {
        public string stageId, arenaId, profileId;
        public ArenaLayout arena;
        public MatchRules rules;

        public BotPersonality Personality => profileId.StartsWith("counter-") ? BotPersonality.Counter : BotPersonality.Aggressive;
        public BotDifficulty Difficulty => profileId.EndsWith("-easy") ? BotDifficulty.Easy : BotDifficulty.Hard;

        public bool IsValid => !string.IsNullOrWhiteSpace(stageId) && stageId.Length <= 32 && arena != null &&
            (arenaId == "open" || arenaId == "tactical") &&
            (profileId == "aggressive-easy" || profileId == "aggressive-hard" ||
             profileId == "counter-easy" || profileId == "counter-hard") && rules != null && rules.IsValid;
    }

    public sealed class ChallengeDefinition : ScriptableObject
    {
        public const int MaximumStages = 20;
        public string definitionId;
        public int revision;
        public string title;
        public ChallengeStage[] stages;

        public bool IsValid
        {
            get
            {
                if (definitionId != "local-circuit" || revision <= 0 || string.IsNullOrWhiteSpace(title) ||
                    stages == null || stages.Length == 0 || stages.Length > MaximumStages) return false;
                var ids = new HashSet<string>();
                foreach (var stage in stages)
                    if (stage == null || !stage.IsValid || !ids.Add(stage.stageId)) return false;
                return true;
            }
        }
    }
}
