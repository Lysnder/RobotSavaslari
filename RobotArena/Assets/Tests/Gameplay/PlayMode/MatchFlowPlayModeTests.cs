using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Match;
using UnityEngine;
using UnityEngine.TestTools;
using State = RobotSavaslari.Match.MatchController.MatchState;
using Outcome = RobotSavaslari.Match.MatchController.MatchOutcome;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>Match state machine on MainArena: countdown, play, single result, restart and reset.</summary>
    public sealed class MatchFlowPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup() => GameplayTestScene.Unload();

        private static MatchController Match => GameplayTestScene.Match;

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator CountdownLocksTheRobotsThenStartsPlaying()
        {
            yield return GameplayTestScene.LoadMainArena();
            Assert.That(Match.State, Is.EqualTo(State.Ready));
            Assert.That(Match.ManualNotificationsOnly, Is.False, "arena runs real gameplay");
            Assert.That(Match.PlayerAllowed || Match.OpponentAllowed, Is.False, "nobody may act before the match");
            Assert.That(Match.CountdownRemaining, Is.Zero);
            Assert.That(Match.Probe(true), Is.False);

            Assert.That(Match.RequestCountdown(), Is.True);
            Assert.That(Match.State, Is.EqualTo(State.Countdown));
            Assert.That(Match.CountdownRemaining, Is.GreaterThan(0f).And.LessThanOrEqualTo(3f));
            Assert.That(Match.RequestCountdown(), Is.False, "countdown already running");
            Assert.That(Match.StartMatch(), Is.False, "direct start only from Ready");
            Assert.That(Match.Probe(false), Is.False);

            yield return new WaitUntil(() => Match.State == State.Playing);
            Assert.That(Match.MatchCount, Is.EqualTo(1));
            Assert.That(Match.ShowStartMessage, Is.True, "start message right after the countdown");
            Assert.That(Match.PlayerAllowed && Match.OpponentAllowed, Is.True);
            Assert.That(Match.Probe(true), Is.True);
            Assert.That(Match.Probe(false), Is.True);
            Assert.That(Match.DemoBotTarget, Is.EqualTo(Match.Player.gameObject));
            Assert.That(RobotPrototypeController.Selected, Is.EqualTo(Match.Player));
            Assert.That(Match.Opponent.IsComputer, Is.True);
            Assert.That(Match.Player.IsComputer, Is.False);
        }

        [UnityTest]
        public IEnumerator OnlyTheFirstValidDefeatProducesAResult()
        {
            yield return GameplayTestScene.LoadMainArena();
            yield return GameplayTestScene.StartPlaying();
            int results = 0;
            Outcome reported = Outcome.None;
            System.Action<Outcome> onResult = outcome => { results++; reported = outcome; };
            Match.ResultDetermined += onResult;
            try
            {
                Assert.That(Match.NotifyDefeat(null, Match.MatchCount), Is.False);
                Assert.That(Match.NotifyDefeat(Match.gameObject, Match.MatchCount), Is.False, "not a robot of this match");
                Assert.That(Match.NotifyDefeat(Match.Player.gameObject, Match.MatchCount - 1), Is.False, "stale match id");
                Match.DemoInvalidNotifications();
                Assert.That(results, Is.Zero);
                Assert.That(Match.State, Is.EqualTo(State.Playing));

                Match.DemoSameEvaluation(true);
                Assert.That(results, Is.EqualTo(1));
                Assert.That(reported, Is.EqualTo(Outcome.OpponentVictory));
                Assert.That(Match.Outcome, Is.EqualTo(Outcome.OpponentVictory));
                Assert.That(Match.ResultText, Is.EqualTo("Rakip kazandi"));
                Assert.That(Match.ResultCount, Is.EqualTo(1));

                Match.DemoRepeatedNotifications();
                Match.DemoSameEvaluation(false);
                Assert.That(results, Is.EqualTo(1), "later notifications are ignored");
                Assert.That(Match.Player.CombatStopped && Match.Opponent.CombatStopped, Is.True);
                Assert.That(Match.PlayerAllowed || Match.OpponentAllowed, Is.False);
            }
            finally { Match.ResultDetermined -= onResult; }
        }

        [UnityTest]
        public IEnumerator LosingAllHealthDecidesTheMatch()
        {
            yield return GameplayTestScene.LoadMainArena();
            yield return GameplayTestScene.StartPlaying();
            GameplayTestScene.SeparateRobots();
            Match.Opponent.GetComponent<RobotHealth>().TakeDamage(1000f);
            Assert.That(Match.State, Is.EqualTo(State.Result));
            Assert.That(Match.Outcome, Is.EqualTo(Outcome.PlayerVictory));
            Assert.That(Match.ResultText, Is.EqualTo("Oyuncu kazandi"));
        }

        [UnityTest]
        public IEnumerator RestartStartsANewMatchAfterOneFrame()
        {
            yield return GameplayTestScene.LoadMainArena();
            Assert.That(Match.RestartMatch(), Is.False, "nothing to restart before a result");
            yield return GameplayTestScene.StartPlaying();
            Match.Player.GetComponent<RobotHealth>().TakeDamage(30f);
            Match.Defeat(true);

            Assert.That(Match.RestartMatch(), Is.True);
            Assert.That(Match.IsRestarting, Is.True);
            Assert.That(Match.RestartMatch(), Is.False, "double click guarded");
            Assert.That(Match.RequestCountdown(), Is.False);
            yield return null;
            yield return null;
            Assert.That(Match.State, Is.EqualTo(State.Playing));
            Assert.That(Match.MatchCount, Is.EqualTo(2));
            Assert.That(Match.ResultCount, Is.Zero);
            Assert.That(Match.Outcome, Is.EqualTo(Outcome.None));
            Assert.That(Match.Player.GetComponent<RobotHealth>().CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator PrepareResetsRobotsOnlyOutsideOfPlay()
        {
            yield return GameplayTestScene.LoadMainArena();
            Vector3 playerSpawn = Match.Player.transform.position;
            Match.DirtyDemoState();
            Assert.That(Match.DemoAttackCooldown, Is.Zero, "demo state only changes while playing");

            yield return GameplayTestScene.StartPlaying();
            GameplayTestScene.SeparateRobots();
            Match.DirtyDemoState();
            Assert.That(Match.DemoAttackCooldown, Is.EqualTo(5f));
            Assert.That(Match.DemoBotTarget, Is.Null);
            Assert.That(Match.Player.GetComponent<RobotHealth>().CurrentHealth, Is.EqualTo(90f));
            Match.Prepare();
            Assert.That(Match.State, Is.EqualTo(State.Playing), "prepare is ignored during play");

            Match.Defeat(false);
            Match.Prepare();
            Assert.That(Match.State, Is.EqualTo(State.Ready));
            Assert.That(Match.Outcome, Is.EqualTo(Outcome.None));
            Assert.That(Match.ResultCount, Is.Zero);
            Assert.That(Match.DemoAttackCooldown, Is.Zero);
            Assert.That(Match.Player.GetComponent<RobotHealth>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(Vector3.Distance(Match.Player.transform.position, playerSpawn), Is.LessThan(0.01f));
            Assert.That(RobotPrototypeController.Selected, Is.Null);
            Assert.That(Match.StartMatch(), Is.True, "direct start from Ready");
            Assert.That(Match.State, Is.EqualTo(State.Playing));
        }

        [UnityTest]
        public IEnumerator ManualModeSwitchesRobotControllersOff()
        {
            yield return GameplayTestScene.LoadMainArena();
            Match.SetManualMode(true);
            Assert.That(Match.ManualNotificationsOnly, Is.True);
            Assert.That(Match.Player.enabled || Match.Opponent.enabled, Is.False);
            Match.ConfigureGameplay();
            Match.SetManualMode(false);
            Assert.That(Match.ManualNotificationsOnly, Is.False);
            Assert.That(Match.Player.enabled && Match.Opponent.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator RegressionPanelDrawsInEveryState()
        {
            yield return GameplayTestScene.LoadMainArena();
            // The panel belongs to the regression harness; the arena hides it behind its own menu.
            GameplayTestScene.SetField(Match, "gameplayInterface", false);
            ImmediateGui.Draw(Match);
            yield return GameplayTestScene.StartPlaying();
            ImmediateGui.Draw(Match);
            Match.Defeat(false);
            ImmediateGui.Draw(Match);
            Match.RestartMatch();
            ImmediateGui.Draw(Match);
            yield return null;
            yield return null;
            GameplayTestScene.SetField(Match, "gameplayInterface", true);
            ImmediateGui.Draw(Match);
            Assert.That(Match.State, Is.EqualTo(State.Playing), "drawing changes nothing");
        }

        [UnityTest]
        public IEnumerator IncompleteSetupDisablesTheMatch()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            var host = new GameObject("Unconfigured Match");
            var match = host.AddComponent<MatchController>();
            LogAssert.Expect(LogType.Error, "Maç için iki farklı robot ve iki doğma noktası atanmalıdır.");
            yield return null;
            Assert.That(match.enabled, Is.False);
            Assert.That(match.IsInitialized, Is.False);
            Assert.That(match.RequestCountdown(), Is.False);
            Assert.That(match.StartMatch(), Is.False);
            Assert.That(match.RestartMatch(), Is.False);
            Assert.That(match.NotifyDefeat(host, 0), Is.False);
            match.Prepare();
            match.SetManualMode(true);
            Assert.That(match.State, Is.EqualTo(State.Ready));
            Object.Destroy(host);
        }
    }
}
