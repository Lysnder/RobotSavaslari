using System;
using UnityEngine;

namespace RobotSavaslari.Match
{
    public enum MatchMode { Single, FirstToTwo }
    public enum RoundFinishReason { Defeat, TimeOver }

    [Serializable]
    public sealed class MatchRules
    {
        public MatchMode mode;
        public float timeLimitSeconds;

        public bool IsValid => Enum.IsDefined(typeof(MatchMode), mode) &&
            !float.IsNaN(timeLimitSeconds) && !float.IsInfinity(timeLimitSeconds) &&
            (timeLimitSeconds == 0f || timeLimitSeconds >= 1f && timeLimitSeconds <= 3600f);
        public MatchRules Copy() => new MatchRules { mode = mode, timeLimitSeconds = timeLimitSeconds };

        public static MatchController.MatchOutcome CompareHealth(float playerHealth, float playerMaximum,
            float opponentHealth, float opponentMaximum)
        {
            double playerRatio = (double)playerHealth * opponentMaximum;
            double opponentRatio = (double)opponentHealth * playerMaximum;
            return playerRatio > opponentRatio ? MatchController.MatchOutcome.PlayerVictory
                : playerRatio < opponentRatio ? MatchController.MatchOutcome.OpponentVictory
                : MatchController.MatchOutcome.Draw;
        }
    }

    [Serializable]
    public sealed class RobotMatchStatistics
    {
        public float remainingHealth, maximumHealth, dealtDamage, receivedDamage;
        public int detachedParts;
    }

    [Serializable]
    public sealed class RoundResult
    {
        public int matchId, roundNumber;
        public float durationSeconds;
        public MatchController.MatchOutcome outcome;
        public RoundFinishReason reason;
        public RobotMatchStatistics player, opponent;
    }

    [Serializable]
    public sealed class MatchRecord
    {
        public string id, endedAtUtc;
        public MatchRules rules;
        public MatchController.MatchOutcome outcome;
        public int playerWins, opponentWins, drawnRounds, roundsPlayed;
        public float durationSeconds;
        public RobotMatchStatistics player, opponent;

        public bool IsValid => !string.IsNullOrEmpty(id) && id.Length <= 64 &&
            DateTimeOffset.TryParse(endedAtUtc, out _) && rules != null && rules.IsValid &&
            (outcome == MatchController.MatchOutcome.PlayerVictory || outcome == MatchController.MatchOutcome.OpponentVictory || outcome == MatchController.MatchOutcome.Draw) &&
            playerWins >= 0 && opponentWins >= 0 && drawnRounds >= 0 && roundsPlayed > 0 &&
            (long)playerWins + opponentWins + drawnRounds == roundsPlayed && FiniteNonnegative(durationSeconds) &&
            ValidStatistics(player) && ValidStatistics(opponent) && ValidScore;

        private bool ValidScore => rules.mode == MatchMode.Single
            ? roundsPlayed == 1 && playerWins == (outcome == MatchController.MatchOutcome.PlayerVictory ? 1 : 0) &&
                opponentWins == (outcome == MatchController.MatchOutcome.OpponentVictory ? 1 : 0) &&
                drawnRounds == (outcome == MatchController.MatchOutcome.Draw ? 1 : 0)
            : outcome == MatchController.MatchOutcome.PlayerVictory ? playerWins == 2 && opponentWins < 2
            : outcome == MatchController.MatchOutcome.OpponentVictory && opponentWins == 2 && playerWins < 2;

        private static bool FiniteNonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        private static bool ValidStatistics(RobotMatchStatistics value) => value != null &&
            FiniteNonnegative(value.remainingHealth) && FiniteNonnegative(value.maximumHealth) && value.maximumHealth > 0f &&
            value.remainingHealth <= value.maximumHealth && FiniteNonnegative(value.dealtDamage) &&
            FiniteNonnegative(value.receivedDamage) && value.detachedParts >= 0;
    }
}
