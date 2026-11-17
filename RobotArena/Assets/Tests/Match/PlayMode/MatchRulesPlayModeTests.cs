using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RobotSavaslari.Combat;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotSavaslari.Match.Tests
{
    public sealed class MatchRulesPlayModeTests : InputTestFixture
    {
        private MatchController match;
        private RobotHealth player, opponent;
        private string historyPath;
        private Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        }

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            yield return SceneManager.LoadSceneAsync("MainArena");
            yield return null; yield return null;
            match = UnityEngine.Object.FindAnyObjectByType<MatchController>();
            Assert.IsTrue(match.IsInitialized);
            historyPath = Path.Combine(Path.GetTempPath(), "MatchRules-" + Guid.NewGuid().ToString("N") + ".json");
            match.HistoryStore = new MatchHistoryStore(historyPath);
            match.SetManualMode(true);
            player = match.Player.GetComponent<RobotHealth>(); opponent = match.Opponent.GetComponent<RobotHealth>();
        }

        public override void TearDown()
        {
            if (match != null)
            {
                var settings = match.GetComponent<SettingsPanelController>();
                if (settings.IsOpen) settings.ToggleSettings();
            }
            Time.timeScale = 1;
            if (File.Exists(historyPath)) File.Delete(historyPath);
            base.TearDown();
        }

        private void Rules(MatchMode mode, float limit = 0)
        {
            Assert.IsTrue(match.ConfigureRules(new MatchRules { mode = mode, timeLimitSeconds = limit }));
        }

        private void Win(bool forPlayer)
        {
            (forPlayer ? opponent : player).TakeDamage(1000, Vector3.zero, Vector3.up, Vector3.zero, true, forPlayer ? player : opponent);
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Result));
            Assert.That(match.ResultCount, Is.EqualTo(1));
        }

        private IEnumerator NextRound()
        {
            Assert.IsTrue(match.RestartMatch());
            Assert.IsFalse(match.RestartMatch());
            yield return null; yield return null;
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Playing));
        }

        [UnityTest]
        public IEnumerator SeriesEndsTwoZeroAndCannotContinueACompletedSeries()
        {
            Rules(MatchMode.FirstToTwo); Assert.IsTrue(match.StartMatch());
            Win(true); Assert.IsFalse(match.EncounterComplete); Assert.IsEmpty(match.HistoryStore.Load());
            yield return NextRound(); Win(true);
            Assert.That(match.PlayerWins, Is.EqualTo(2)); Assert.That(match.OpponentWins, Is.Zero);
            Assert.IsTrue(match.IsSeriesComplete); Assert.IsFalse(match.ContinueSeries());
            Assert.IsFalse(match.NotifyDefeat(match.Opponent.gameObject, match.MatchCount));
            Assert.That(match.HistoryStore.Load().Count, Is.EqualTo(1));
            Assert.That(match.LastMatch.roundsPlayed, Is.EqualTo(2));
            Assert.That(match.LastMatch.player.dealtDamage, Is.EqualTo(200));
        }

        [UnityTest]
        public IEnumerator SeriesEndsTwoOneAndNewSeriesStartsClean()
        {
            Rules(MatchMode.FirstToTwo); match.StartMatch(); Win(true);
            yield return NextRound(); Win(false);
            Assert.IsFalse(match.EncounterComplete);
            yield return NextRound(); Win(false);
            Assert.That(match.PlayerWins, Is.EqualTo(1)); Assert.That(match.OpponentWins, Is.EqualTo(2));
            Assert.That(match.SeriesOutcome, Is.EqualTo(MatchController.MatchOutcome.OpponentVictory));
            Assert.IsFalse(match.ContinueSeries());
            Assert.IsTrue(match.RequestCountdown());
            Assert.IsFalse(match.RequestCountdown());
            Assert.That(match.PlayerWins + match.OpponentWins, Is.Zero);
            Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth));
        }

        [UnityTest]
        public IEnumerator TimeOverUsesRatiosAndPublishesOneResult()
        {
            typeof(RobotHealth).GetField("maxHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(opponent, 200f);
            opponent.Restore();
            Rules(MatchMode.Single, 1); match.StartMatch();
            opponent.TakeDamage(50);
            yield return new WaitForSeconds(1.15f);
            Assert.That(match.Outcome, Is.EqualTo(MatchController.MatchOutcome.PlayerVictory));
            Assert.That(match.LastRound.reason, Is.EqualTo(RoundFinishReason.TimeOver));
            Assert.That(match.LastRound.durationSeconds, Is.EqualTo(1).Within(.001));
            Assert.That(match.ResultCount, Is.EqualTo(1));
            Assert.IsFalse(match.NotifyDefeat(match.Player.gameObject, match.MatchCount));
        }

        [UnityTest]
        public IEnumerator TiedTimedRoundRepeatsWithoutConsumingAWinningRound()
        {
            Rules(MatchMode.FirstToTwo, 1); match.StartMatch();
            yield return new WaitForSeconds(1.15f);
            Assert.That(match.Outcome, Is.EqualTo(MatchController.MatchOutcome.Draw));
            Assert.That(match.DrawnRounds, Is.EqualTo(1));
            Assert.That(match.PlayerWins + match.OpponentWins, Is.Zero);
            Assert.IsFalse(match.EncounterComplete);
            yield return NextRound(); Assert.That(match.RoundNumber, Is.EqualTo(1)); Win(true);
            yield return NextRound(); Win(false);
            yield return NextRound(); Win(true);
            Assert.That(match.LastMatch.roundsPlayed, Is.EqualTo(4));
            Assert.That(match.LastMatch.drawnRounds, Is.EqualTo(1));
            Assert.IsTrue(match.IsSeriesComplete); Assert.IsFalse(match.ContinueSeries());
        }

        [UnityTest]
        public IEnumerator ActualDamageIncludesFatalHitButNeverAttributesUnknownSource()
        {
            Rules(MatchMode.Single); match.StartMatch();
            opponent.TakeDamage(10); // unknown/environment
            opponent.TakeDamage(500, Vector3.zero, Vector3.up, Vector3.zero, true, player);
            Assert.That(match.LastRound.opponent.receivedDamage, Is.EqualTo(100));
            Assert.That(match.LastRound.player.dealtDamage, Is.EqualTo(90));
            Assert.That(match.LastMatch.player.dealtDamage, Is.EqualTo(90));
            yield return NextRound();
            Assert.IsNull(match.LastRound);
            Win(true); Assert.That(match.LastRound.player.dealtDamage, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator PauseFreezesTimerAndCombatAndRequiresFreshInput()
        {
            Rules(MatchMode.Single, 60); match.SetManualMode(false); match.StartMatch();
            foreach (var robot in new[] {match.Player, match.Opponent}) robot.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            match.Player.SendMessage("OnApplicationFocus", true);
            Press(keyboard.wKey, queueEventOnly: true); yield return null; yield return null;
            var driver = match.Player.GetComponent<PlayerKeyboardDriver>();
            var attack = match.Player.GetComponent<MeleeAttack>();
            Assert.IsTrue(driver.enabled);
            attack.RequestAttack();
            var bot = match.Opponent.GetComponent<RobotBotController>();
            bot.Memory.Observe(true, 0, .02f);
            var settings = match.GetComponent<SettingsPanelController>(); settings.ToggleSettings();
            yield return null;
            float elapsed = match.ElapsedSeconds, cooldown = attack.CooldownRemaining;
            int memory = bot.Memory.Count;
            Vector3 position = match.Player.transform.position;
            Assert.IsFalse(driver.enabled); Assert.IsTrue(match.IsPaused);
            Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.MatchClosed));
            Assert.IsFalse(match.RequestCountdown()); Assert.IsFalse(match.NotifyDefeat(match.Opponent.gameObject, match.MatchCount));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(match.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(attack.CooldownRemaining, Is.EqualTo(cooldown).Within(.01));
            Assert.That(bot.Memory.Count, Is.EqualTo(memory)); Assert.That(match.Player.transform.position, Is.EqualTo(position));
            settings.ToggleSettings(); yield return null;
            Assert.IsFalse(driver.enabled, "Held W must not resume buffered movement.");
            Release(keyboard.wKey, queueEventOnly: true); yield return null; yield return null;
            Assert.IsTrue(driver.enabled); Assert.IsFalse(match.IsPaused);
            Assert.That(match.ResultCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HistoryRematchRestoresRulesButNotOldHealthOrScore()
        {
            Rules(MatchMode.FirstToTwo, 60); match.StartMatch(); Win(true); yield return NextRound(); Win(true);
            Assert.IsTrue(match.IsSeriesComplete);
            Assert.IsTrue(match.LastMatch.IsValid, JsonUtility.ToJson(match.LastMatch));
            Assert.IsNull(match.HistoryNotice);
            Assert.That(match.HistoryStore.FilePath, Is.EqualTo(historyPath));
            var saved = new MatchHistoryStore(historyPath);
            var records = saved.Load();
            Assert.That(records.Count, Is.EqualTo(1), saved.LastError + " " + (File.Exists(historyPath) ? File.ReadAllText(historyPath) : "file missing"));
            var record = records[0];
            match.Prepare(); Rules(MatchMode.Single);
            Assert.IsTrue(match.Rematch(record)); Assert.IsFalse(match.Rematch(record));
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
            Assert.That(match.Rules.mode, Is.EqualTo(MatchMode.FirstToTwo));
            Assert.That(match.Rules.timeLimitSeconds, Is.EqualTo(60));
            Assert.That(match.PlayerWins + match.OpponentWins, Is.Zero);
            Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth));
            Assert.That(match.HistoryStore.Load().Count, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator SharedMeleeAttributesClampedFatalDamageBeforeResult()
        {
            Rules(MatchMode.Single); match.SetManualMode(false); match.StartMatch();
            match.Player.ResetMotion(); match.Opponent.ResetMotion();
            var a = match.Player.GetComponent<Rigidbody>(); var b = match.Opponent.GetComponent<Rigidbody>();
            a.position = new Vector3(0,.82f,0); a.rotation = Quaternion.identity;
            b.position = new Vector3(0,.82f,1.4f); b.rotation = Quaternion.Euler(0,180,0);
            match.Player.transform.SetPositionAndRotation(a.position,a.rotation);
            match.Opponent.transform.SetPositionAndRotation(b.position,b.rotation);
            opponent.TakeDamage(90); Physics.SyncTransforms();
            var attack=match.Player.GetComponent<MeleeAttack>();
            Assert.That(attack.RequestAttack(),Is.EqualTo(AttackOutcome.Started));
            Assert.That(match.State,Is.EqualTo(MatchController.MatchState.Result));
            Assert.That(match.LastRound.player.dealtDamage,Is.EqualTo(10));
            Assert.That(match.LastRound.opponent.receivedDamage,Is.EqualTo(100));
            Assert.That(match.LastMatch.player.dealtDamage,Is.EqualTo(10));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisabledMatchRejectsTransitionsAndDoesNotAdvanceTimer()
        {
            Rules(MatchMode.Single,60); match.StartMatch();
            match.enabled=false;
            float elapsed=match.ElapsedSeconds;
            Assert.IsFalse(match.NotifyDefeat(match.Player.gameObject,match.MatchCount));
            Assert.IsFalse(match.StartMatch()); Assert.IsFalse(match.RequestCountdown());
            yield return new WaitForSeconds(.1f);
            Assert.That(match.ElapsedSeconds,Is.EqualTo(elapsed));
            match.enabled=true; yield return null;
            Assert.That(match.ElapsedSeconds-elapsed,Is.LessThan(.1f));
        }
    }
}
