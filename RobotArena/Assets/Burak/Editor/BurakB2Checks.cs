using System;
using UnityEngine;

namespace RobotSavaslari.Burak.Editor
{
    public static class BurakB2Checks
    {
        public static string Run()
        {
            Require(Application.isPlaying, "Play mode required");
            var match = UnityEngine.Object.FindFirstObjectByType<BurakMatchController>();
            Require(match != null && match.State == BurakMatchController.MatchState.Ready, "Start in Ready");
            var player = GameObject.Find("Mavi Robot");
            var opponent = GameObject.Find("Kirmizi Robot");
            int events = 0;
            Action<BurakMatchController.MatchOutcome> listener = _ =>
            {
                events++;
                Require(!match.PlayerAllowed && !match.OpponentAllowed, "Permissions closed before result event");
                Require(!match.NotifyDefeat(player, match.MatchCount), "Reentrant notification rejected");
            };
            match.ResultDetermined += listener;
            try
            {
                match.Defeat(true);
                match.Defeat(false);
                Require(events == 0 && match.ResultCount == 0 && match.Outcome == BurakMatchController.MatchOutcome.None, "Pre-match ignored");
                Require(match.StartMatch(), "First match starts");
                int oldMatch = match.MatchCount;
                Require(!match.NotifyDefeat(null, oldMatch) && !match.NotifyDefeat(match.gameObject, oldMatch), "Unknown identity ignored");
                match.Defeat(false);
                Require(match.Outcome == BurakMatchController.MatchOutcome.PlayerVictory, "Opponent defeat means player victory");
                for (int i = 0; i < 25; i++) { match.Defeat(false); match.Defeat(true); }
                Require(events == 1 && match.ResultCount == 1 && match.Outcome == BurakMatchController.MatchOutcome.PlayerVictory, "Repeated and later notices cannot change first result");
                match.Prepare();
                Require(match.Outcome == BurakMatchController.MatchOutcome.None && match.ResultCount == 0, "Reset clears result");
                Require(match.StartMatch(), "Second match starts");
                Require(!match.NotifyDefeat(opponent, oldMatch), "Stale match ignored");
                Require(match.State == BurakMatchController.MatchState.Playing, "Stale notice leaves match playing");
                match.Defeat(true);
                match.Defeat(false);
                Require(events == 2 && match.ResultCount == 1 && match.Outcome == BurakMatchController.MatchOutcome.OpponentVictory, "Player defeat means opponent victory; later notice ignored");
                Require(!match.PlayerAllowed && !match.OpponentAllowed && !match.Probe(true) && !match.Probe(false), "Result locks both commands");
                match.Prepare();
                Require(match.StartMatch(), "Health integration match starts");
                opponent.GetComponent<RobotHealth>().TakeDamage(opponent.GetComponent<RobotHealth>().MaxHealth);
                Require(events == 3 && match.Outcome == BurakMatchController.MatchOutcome.PlayerVictory, "Existing health event uses same entry point");
                match.Prepare();
                return "PASS: two winners, pre-match/duplicate/post-result/reentrant/stale/unknown notices, single result event, both permissions locked, health adapter. PENDING: simultaneous defeat rule (Emin).";
            }
            finally { match.ResultDetermined -= listener; }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("B2 FAIL: " + message);
        }
    }
}
