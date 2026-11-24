using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Combat;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace RobotSavaslari.Gameplay.Tests
{
    public sealed class DuelContractFlowTests
    {
        private MatchController match;
        private MainMenuScreen menu;
        private PlayerRobotSelection selection;
        private string temporary, preference;
        private bool hadPreference;
        private int notifications;

        [UnitySetUp]
        public IEnumerator Load()
        {
            hadPreference = PlayerPrefs.HasKey(DuelSetup.PreferenceKey);
            preference = PlayerPrefs.GetString(DuelSetup.PreferenceKey);
            temporary = Path.Combine(Application.temporaryCachePath, "DuelContract-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            yield return GameplayTestScene.LoadMainArena();
            match = GameplayTestScene.Match;
            menu = match.GetComponent<MainMenuScreen>();
            selection = match.GetComponent<PlayerRobotSelection>();
            match.HistoryStore = new MatchHistoryStore(Path.Combine(temporary, "history.json"));
            MainArenaPresentationInstaller.Install(match.gameObject.scene);
            Assert.That(MainArenaPresentationInstaller.UseArchiveStore(match,
                new MatchReplayArchiveStore(Path.Combine(temporary, "replays"))), Is.True);
            match.ResultDetermined += CountResult;
        }

        private void CountResult(MatchController.MatchOutcome _) => notifications++;

        [UnityTearDown]
        public IEnumerator Clean()
        {
            if (match) match.ResultDetermined -= CountResult;
            notifications = 0;
            yield return GameplayTestScene.Unload();
            if (hadPreference) PlayerPrefs.SetString(DuelSetup.PreferenceKey, preference);
            else PlayerPrefs.DeleteKey(DuelSetup.PreferenceKey);
            PlayerPrefs.Save();
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }

        [UnityTest]
        [Timeout(150000)]
        public IEnumerator SpinnerPlayerForkBotTimedFinishAndMenuRematch()
        {
            Assert.That(selection.SelectFork(false), Is.True);
            Assert.That(menu.SelectPreset(DuelSetup.Quick()), Is.True);
            menu.StartLobbyMatch();
            var first = match.LockedSetup;
            AssertRoles(false);
            Assert.That(first.Seconds, Is.EqualTo(60));
            Assert.That(first.Mode, Is.EqualTo(MatchMode.Single));
            yield return Playing();
            Assert.That(match.Opponent.GetComponent<RobotBotController>().Target, Is.SameAs(match.Player.transform));
            yield return Capture("01-spinner-playing");
            ProbeBothWeapons(25f, 18f);

            // Keep a known health lead for the full timed finish. Only the test clock rate
            // is accelerated. The production countdown, rules and expiry logic still execute.
            var bot = match.Opponent.GetComponent<RobotBotController>();
            bot.enabled = false;
            GameplayTestScene.Place(match.Player, new Vector3(-4f, .8f, -3f), false);
            GameplayTestScene.Place(match.Opponent, new Vector3(4f, .8f, 3f), false);
            Time.timeScale = 4f;
            yield return new WaitUntil(() => match.State == MatchController.MatchState.Result);
            Time.timeScale = 1f;
            var round = match.LastRound;
            Assert.That(round.reason, Is.EqualTo(RoundFinishReason.TimeOver));
            Assert.That(round.outcome, Is.EqualTo(MatchController.MatchOutcome.PlayerVictory));
            Assert.That(round.durationSeconds, Is.InRange(60f, 60.5f));
            Assert.That(match.ResultCount, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(match.HistoryStore.Load().Count, Is.EqualTo(1));
            TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "DUEL_TIMED {0}/{1} seconds={2:F2} hp={3:F1}/{4:F1} outcome={5} reason={6} results={7}",
                first.PlayerRobotId, first.OpponentRobotId, round.durationSeconds, round.player.remainingHealth,
                round.opponent.remainingHealth, round.outcome, round.reason, match.ResultCount));
            yield return Capture("02-spinner-time-result");
            menu.ContinueResult();
            AssertRoles(false);
            Assert.That(match.LockedSetup.EncounterId, Is.Not.EqualTo(first.EncounterId));
            Assert.That(match.LockedSetup.Seconds, Is.EqualTo(first.Seconds));
            yield return Playing();
            Assert.That(match.MatchCount, Is.EqualTo(2));
            Assert.That(bot.enabled, Is.True);
            Assert.That(match.Player.GetComponent<MeleeAttack>().CooldownRemaining, Is.Zero);
            var health = match.Player.GetComponent<RobotHealth>();
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
            WinWithPlayerWeapon();
            Assert.That(notifications, Is.EqualTo(2));
            Assert.That(match.HistoryStore.Load().Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ForkPlayerSpinnerBotWinsFirstToTwoWithLockedSlots()
        {
            Assert.That(selection.SelectFork(true), Is.True);
            Assert.That(menu.SelectPreset(DuelSetup.Series()), Is.True);
            menu.StartLobbyMatch();
            AssertRoles(true);
            var series = match.LockedSetup;
            Assert.That(series.Mode, Is.EqualTo(MatchMode.FirstToTwo));
            var player = match.Player;
            var opponent = match.Opponent;
            yield return Playing();
            yield return Capture("03-fork-playing");
            ProbeBothWeapons(18f, 25f);
            yield return new WaitForSeconds(match.Player.GetComponent<MeleeAttack>().Cooldown + .05f);
            WinWithPlayerWeapon();
            Assert.That(match.EncounterComplete, Is.False);
            Assert.That(match.HistoryStore.Load().Count, Is.Zero);
            menu.ContinueResult();
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
            Assert.That(match.LockedSetup, Is.SameAs(series));
            Assert.That(selection.SelectFork(false), Is.False);
            yield return Playing();
            Assert.That(match.Player, Is.SameAs(player));
            Assert.That(match.Opponent, Is.SameAs(opponent));
            AssertRoles(true);
            WinWithPlayerWeapon();
            Assert.That(match.EncounterComplete, Is.True);
            Assert.That(match.HistoryStore.Load().Count, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(2));
            Assert.That(match.ResultCount, Is.EqualTo(1));
            Assert.That(match.PlayerWins, Is.EqualTo(2));
            TestContext.WriteLine($"DUEL_SERIES {series.PlayerRobotId}/{series.OpponentRobotId} {match.PlayerWins}-{match.OpponentWins} rounds={match.LastMatch.roundsPlayed} results={notifications} history={match.HistoryStore.Load().Count}");
            yield return Capture("04-fork-series-result");
        }

        [UnityTest]
        public IEnumerator MalformedStartsRejectedAndAcceptedInputIsFrozen()
        {
            var setup = DuelSetup.Quick();
            Assert.That(selection.SelectFork(false), Is.True);
            Assert.That(menu.SelectPreset(setup), Is.True);
            var invalid = new Action<DuelSetup>[] {
                request => request.playerRobotId = "unknown", request => request.opponentRobotId = "unknown",
                request => request.playerRole = "bot", request => request.catalogRevision++,
                request => request.version++, request => request.arenaId = "unknown",
                request => request.arenaId = "Tactical", request => request.encounterId = Guid.NewGuid().ToString("N")
            };
            foreach (var mutate in invalid)
            {
                var request = match.CreateDuelStart(setup);
                mutate(request);
                Assert.That(match.RequestCountdown(request), Is.False);
                Assert.That(match.LockedSetup, Is.Null);
                Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Ready));
            }
            Assert.That(match.MatchCount, Is.Zero);
            Assert.That(notifications, Is.Zero);
            Assert.That(match.HistoryStore.Load(), Is.Empty);

            var accepted = match.CreateDuelStart(setup);
            Assert.That(match.RequestCountdown(accepted), Is.True);
            Assert.That(match.RequestCountdown(accepted), Is.False);
            menu.StartLobbyMatch();
            string id = accepted.encounterId;
            accepted.playerRobotId = "mutated";
            accepted.encounterId = Guid.NewGuid().ToString("N");
            accepted.seconds = 1f;
            Assert.That(match.LockedSetup.EncounterId, Is.EqualTo(id));
            Assert.That(match.LockedSetup.Seconds, Is.EqualTo(60f));
            AssertRoles(false);
            yield return Playing();
            WinWithPlayerWeapon();
            Assert.That(match.MatchCount, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(match.HistoryStore.Load().Count, Is.EqualTo(1));
            TestContext.WriteLine("DUEL_GUARD invalid=8 duplicateStart=blocked frozen=true matches=1 results=1");
        }

        [UnityTest]
        public IEnumerator LegacyHistoryRematchResolvesLiveSlots()
        {
            // Existing v1 history files contain the rules, score and statistics only.
            string json = "{\"id\":\"legacy-01\",\"endedAtUtc\":\"2026-11-01T12:00:00Z\"," +
                "\"rules\":{\"mode\":0,\"timeLimitSeconds\":60},\"outcome\":1,\"playerWins\":1,\"roundsPlayed\":1," +
                "\"durationSeconds\":10,\"player\":{\"maximumHealth\":100,\"remainingHealth\":70}," +
                "\"opponent\":{\"maximumHealth\":100,\"remainingHealth\":0}}";
            File.WriteAllText(match.HistoryStore.FilePath, "{\"version\":1,\"records\":[" + json + "]}");
            var previous = match.HistoryStore.Load().Single();
            Assert.That(previous.IsValid, Is.True);
            Assert.That(selection.SelectFork(true), Is.True);
            Assert.That(menu.SelectPreset(DuelSetup.Quick()), Is.True);
            Assert.That(match.Rematch(previous), Is.True);
            AssertRoles(true);
            Assert.That(match.Rules.timeLimitSeconds, Is.EqualTo(previous.rules.timeLimitSeconds));
            yield return Playing();
            WinWithPlayerWeapon();
            var old = match.HistoryStore.Load().Single(record => record.id == previous.id);
            Assert.That(old.durationSeconds, Is.EqualTo(10));
            var stored = File.ReadAllText(match.HistoryStore.FilePath);
            Assert.That(stored, Does.Not.Contain("playerRobotId"));
            TestContext.WriteLine("DUEL_LEGACY v1 oldRecord=preserved rematch=fork/spinner results=1");
        }

        private IEnumerator Playing()
        {
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
            Assert.That(match.PlayerAllowed, Is.False);
            yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
            yield return null;
            Assert.That(match.Opponent.GetComponent<RobotBotController>().Target, Is.SameAs(match.Player.transform));
        }

        private void AssertRoles(bool forkPlayer)
        {
            var locked = match.LockedSetup;
            Assert.That(locked, Is.Not.Null);
            Assert.That(locked.PlayerRole, Is.EqualTo("player"));
            Assert.That(locked.OpponentRole, Is.EqualTo("bot"));
            Assert.That(locked.PlayerRobotId, Is.EqualTo(forkPlayer ? DuelSetup.Fork : DuelSetup.Spinner));
            Assert.That(locked.OpponentRobotId, Is.EqualTo(forkPlayer ? DuelSetup.Spinner : DuelSetup.Fork));
            Assert.That(locked.ArenaId, Is.EqualTo("Open"));
            Assert.That(locked.EncounterId, Is.EqualTo(match.EncounterId));
            Assert.That(locked.CatalogRevision, Is.EqualTo(DuelSetup.CatalogRevision));
            Assert.That(match.Player.GetComponent<MeleeAttack>().Profile.damage, Is.EqualTo(forkPlayer ? 18f : 25f));
            Assert.That(match.Opponent.GetComponent<MeleeAttack>().Profile.damage, Is.EqualTo(forkPlayer ? 25f : 18f));
            var botWeapon = match.Opponent.GetComponent<RobotDamageVisuals>().Parts.Single(part => part.Kind == RobotDamageVisuals.PartKind.Weapon);
            Assert.That(botWeapon.Name, Is.EqualTo(forkPlayer ? "Spinner" : "Lifter"));
        }

        private void ProbeBothWeapons(float outgoing, float incoming)
        {
            FaceEachOther();
            var hp = match.Player.GetComponent<RobotHealth>();
            var enemy = match.Opponent.GetComponent<RobotHealth>();
            float before = enemy.CurrentHealth;
            Assert.That(match.Player.GetComponent<MeleeAttack>().RequestAttack(), Is.EqualTo(AttackOutcome.Started));
            Assert.That(before - enemy.CurrentHealth, Is.EqualTo(outgoing).Within(.01f));
            before = hp.CurrentHealth;
            Assert.That(match.Opponent.GetComponent<MeleeAttack>().RequestAttack(), Is.EqualTo(AttackOutcome.Started));
            Assert.That(before - hp.CurrentHealth, Is.EqualTo(incoming).Within(.01f));
        }

        private void FaceEachOther()
        {
            GameplayTestScene.Place(match.Player, new Vector3(0f, .8f, 0f), false, Quaternion.identity);
            GameplayTestScene.Place(match.Opponent, new Vector3(0f, .8f, 1.8f), false, Quaternion.Euler(0f, 180f, 0f));
        }

        private void WinWithPlayerWeapon()
        {
            FaceEachOther();
            var attack = match.Player.GetComponent<MeleeAttack>();
            float clock = Time.time + attack.Cooldown + .05f;
            attack.Clock = () => clock;
            try
            {
                attack.Tick();
                for (int hit = 0; hit < 12 && match.State == MatchController.MatchState.Playing; hit++)
                {
                    Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.Started));
                    clock += attack.Cooldown + .05f;
                    attack.Tick();
                }
            }
            finally { attack.Clock = null; }
            Assert.That(match.Outcome, Is.EqualTo(MatchController.MatchOutcome.PlayerVictory));
        }

        private static IEnumerator Capture(string phase)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer) yield break;
            string folder = Environment.GetEnvironmentVariable("DUEL_CONTRACT_FRAMES");
            if (string.IsNullOrEmpty(folder)) yield break;
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(folder);
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(folder, phase + ".png"), frame.EncodeToPNG());
                TestContext.WriteLine($"DUEL_FRAME {phase} {frame.width}x{frame.height} {SystemInfo.graphicsDeviceType}");
            }
            finally { UnityEngine.Object.Destroy(frame); }
        }
    }
}
