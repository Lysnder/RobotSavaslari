using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.Driving;
using RobotSavaslari.Presentation;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotSavaslari.Match.Tests
{
    public sealed class ChallengeSessionTests : InputTestFixture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private MatchController _match;
        private ChallengeSessionController _session;
        private ChallengeDefinition _definition;
        private string _directory, _path, _history;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _keyboard.MakeCurrent();
        }

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            if (_directory == null)
            {
                _directory = Path.Combine(Path.GetTempPath(), "LocalCircuit-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_directory);
                _path = Path.Combine(_directory, "challenge-session.json");
                _history = Path.Combine(_directory, "match-history.json");
            }
            yield return SceneManager.LoadSceneAsync("MainArena");
            yield return null;
            _match = UnityEngine.Object.FindAnyObjectByType<MatchController>();
            _match.SetManualMode(true);
            _match.HistoryStore = new MatchHistoryStore(_history);
            Assert.That(_match.GetComponents<ChallengeSessionController>(), Has.Length.EqualTo(1));
            _session = _match.GetComponent<ChallengeSessionController>();
            _session.UseSavePath(_path);
            _definition = Resources.Load<ChallengeDefinition>("Challenges/LocalCircuit");
            Assert.That(_definition, Is.Not.Null);
            Assert.That(_definition.IsValid, Is.True);
        }

        public override void TearDown()
        {
            if (_session != null) _session.Close();
            if (_match != null)
            {
                var settings = _match.GetComponent<SettingsPanelController>();
                if (settings.IsOpen) settings.ToggleSettings();
            }
            Time.timeScale = 1;
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            _directory = null;
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator ThreeEncountersSaveOnceEachAndRestoreNormalRematch()
        {
            var arena = UnityEngine.Object.FindAnyObjectByType<ArenaEnvironment>();
            var originalArena = arena.Layout;
            var opponents = _match.GetComponent<OpponentSelectionPanel>();
            Assert.That(opponents.Select(BotPersonality.Counter, BotDifficulty.Easy), Is.True);
            Assert.That(_match.ConfigureRules(new MatchRules { timeLimitSeconds = 120 }), Is.True);
            Begin();
            var firstId = _session.Session.sessionId;
            yield return BeginPlaying();
            var settings = _match.GetComponent<SettingsPanelController>();
            settings.ToggleSettings();
            float elapsed = _match.ElapsedSeconds;
            yield return null;
            Assert.That(_match.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(_session.Session.nextStageIndex, Is.EqualTo(0));
            Assert.That(_session.Continue(), Is.False);
            settings.ToggleSettings();
            ForceTimeLimit();
            yield return null;
            Assert.That(_match.Outcome, Is.EqualTo(MatchController.MatchOutcome.Draw));
            Assert.That(_session.Session.nextStageIndex, Is.EqualTo(1));
            var saved = File.ReadAllBytes(_path);
            var callback = typeof(ChallengeSessionController).GetMethod("OnResult", Private);
            for (int i = 0; i < 10; i++) callback.Invoke(_session, new object[] { _match.Outcome });
            _session.enabled = false;
            _session.enabled = true;
            CollectionAssert.AreEqual(saved, File.ReadAllBytes(_path));

            var inspection = UnityEngine.Object.FindAnyObjectByType<PostMatchInspection>();
            Assert.That(inspection.Begin(PostMatchInspection.PlayerRobot), Is.True);
            Assert.That(_session.Continue(), Is.False);
            inspection.Stop();
            Assert.That(_session.Continue(), Is.True);
            Assert.That(arena.Layout, Is.SameAs(_definition.stages[1].arena));
            Assert.That(opponents.Personality, Is.EqualTo(BotPersonality.Counter));
            Assert.That(_match.Rules.mode, Is.EqualTo(MatchMode.FirstToTwo));
            yield return BeginPlaying();
            ForceTimeLimit();
            yield return null;
            Assert.That(_match.Outcome, Is.EqualTo(MatchController.MatchOutcome.Draw));
            Assert.That(_session.Session.nextStageIndex, Is.EqualTo(1));
            Assert.That(_session.Continue(), Is.False);
            Assert.That(_match.ContinueSeries(), Is.True);
            yield return BeginPlaying();
            FinishRound(true);
            Assert.That(_session.Session.nextStageIndex, Is.EqualTo(1));
            Assert.That(_match.ContinueSeries(), Is.True);
            yield return BeginPlaying();
            FinishRound(true);
            Assert.That(_session.Session.nextStageIndex, Is.EqualTo(2));
            Assert.That(_session.Session.results[1].encounter.roundsPlayed, Is.EqualTo(3));

            Press(_keyboard.rKey);
            yield return null;
            Assert.That(_match.State, Is.EqualTo(MatchController.MatchState.Result));
            Release(_keyboard.rKey);
            Assert.That(_session.Continue(), Is.True);
            yield return BeginPlaying();
            Assert.That(_match.ContinueSeries(), Is.False);
            FinishRound(false);
            Assert.That(_session.Complete, Is.True);
            Assert.That(_session.Continue(), Is.False);
            Assert.That(_session.Session.results[2].encounter.outcome, Is.EqualTo(MatchController.MatchOutcome.OpponentVictory));
            var history = File.ReadAllBytes(_history);
            Assert.That(_session.Leave(false), Is.True);
            CollectionAssert.AreEqual(history, File.ReadAllBytes(_history));
            Assert.That(_session.HasCheckpoint, Is.False);
            Assert.That(_session.Archived(0).sessionId, Is.EqualTo(firstId));
            Assert.That(new ChallengeSessionStore(_definition, _path).Load().archive[0].nextStageIndex, Is.EqualTo(3));
            Assert.That(_match.Rules.timeLimitSeconds, Is.EqualTo(120));
            Assert.That(arena.Layout, Is.SameAs(originalArena));
            Assert.That(opponents.Difficulty, Is.EqualTo(BotDifficulty.Easy));

            Assert.That(_match.Rematch(_match.HistoryStore.Load()[0]), Is.True);
            yield return BeginPlaying();
            FinishRound(false);
            _match.Prepare();
            Begin();
            Assert.That(_session.Session.sessionId, Is.Not.EqualTo(firstId));
        }

        [UnityTest]
        public IEnumerator ReloadDuringSecondEncounterRestartsThatStageAndKeepOrRestartWorks()
        {
            Begin();
            yield return BeginPlaying();
            FinishRound(true);
            Assert.That(_session.Continue(), Is.True);
            var previous = _session.Session.sessionId;
            var before = File.ReadAllBytes(_path);
            yield return BeginPlaying();
            FinishRound(false);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_path));

            yield return LoadArena();
            Assert.That(_session.HasCheckpoint, Is.True);
            Assert.That(_session.Session.nextStageIndex, Is.EqualTo(1));
            Begin();
            Assert.That(_match.RoundNumber, Is.EqualTo(1));
            Assert.That(_match.OpponentWins, Is.Zero);
            yield return BeginPlaying();
            FinishRound(true);
            Assert.That(_session.Open(), Is.True);
            Assert.That(_session.Leave(false), Is.True);
            Assert.That(_session.Session.sessionId, Is.EqualTo(previous));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_path));
            Assert.That(_session.Open(), Is.True);
            Assert.That(_session.Begin(true), Is.True);
            Assert.That(_session.Session.sessionId, Is.Not.EqualTo(previous));
            Assert.That(_session.Archived(0).sessionId, Is.EqualTo(previous));
            yield return BeginPlaying();
            FinishRound(false);
            Assert.That(_session.Open(), Is.True);
            Assert.That(_session.Leave(true), Is.True);
            Assert.That(_session.HasCheckpoint, Is.False);
            Assert.That(_session.ArchiveCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ModalBlocksKeyboardAndRestoresEnabledStates()
        {
            var camera = UnityEngine.Object.FindAnyObjectByType<CombatCameraController>();
            var settings = _match.GetComponent<SettingsPanelController>();
            var panel = _match.GetComponent<OpponentSelectionPanel>();
            var driving = UnityEngine.Object.FindAnyObjectByType<DrivingFeatureInstaller>();
            if (driving == null)
            {
                var owned = new GameObject("Challenge test driving installer");
                SceneManager.MoveGameObjectToScene(owned, _match.gameObject.scene);
                driving = owned.AddComponent<DrivingFeatureInstaller>();
            }
            Assert.That(driving.Install(), Is.True);
            yield return null;
            var bindings = driving.BindingsScreen;
            Assert.That(driving.TimeTrial.Enter(), Is.True);
            Assert.That(_session.Open(), Is.False);
            driving.TimeTrial.Exit();
            bindings.Open();
            bindings.BeginCapture(DriveAction.Forward);
            Assert.That(_session.Open(), Is.False);
            bindings.Close();
            settings.ToggleSettings();
            Assert.That(_session.Open(), Is.False);
            settings.ToggleSettings();

            panel.enabled = false;
            var mode = camera.Mode;
            Assert.That(_session.Open(), Is.True);
            Press(_keyboard.enterKey);
            Press(_keyboard.cKey);
            Press(_keyboard.spaceKey);
            Press(_keyboard.escapeKey);
            yield return null;
            Assert.That(_match.State, Is.EqualTo(MatchController.MatchState.Ready));
            Assert.That(camera.Mode, Is.EqualTo(mode));
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(camera.enabled, Is.False);
            _session.enabled = false;
            Assert.That(_session.DialogOpen, Is.False);
            Assert.That(camera.enabled, Is.True);
            Assert.That(settings.enabled, Is.True);
            Assert.That(panel.enabled, Is.False);
        }

        [TestCase("{")]
        [TestCase("{\"version\":2,\"checkpoint\":[],\"archive\":[]}")]
        [TestCase("{\"version\":1,\"checkpoint\":[{\"definitionRevision\":99,\"definitionId\":\"local-circuit\"}],\"archive\":[]}")]
        [TestCase("{\"version\":1,\"checkpoint\":[{\"definitionRevision\":1,\"definitionId\":\"local-circuit\",\"sessionId\":\"00000000000000000000000000000042\",\"nextStageIndex\":9,\"results\":[]}],\"archive\":[]}")]
        public void InvalidSaveIsPreservedOnlyOnExplicitRecovery(string content)
        {
            File.WriteAllText(_path, content);
            var store = new ChallengeSessionStore(_definition, _path);
            store.Load();
            Assert.That(store.ReadFailed, Is.True);
            Assert.That(store.Save(new ChallengeSave()), Is.False);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(content));
            Assert.That(store.PreserveUnreadableSave(), Is.True);
            Assert.That(File.Exists(_path), Is.False);
            Assert.That(File.ReadAllText(Directory.GetFiles(_directory, "*.preserved-*")[0]), Is.EqualTo(content));
        }

        [UnityTest]
        public IEnumerator FailedAtomicWriteKeepsCheckpointAndHistoryFailureKeepsNotice()
        {
            Begin();
            var previous = File.ReadAllBytes(_path);
            yield return BeginPlaying();
            _match.HistoryStore = new MatchHistoryStore(Path.Combine(_path, "unwritable.json"));
            using (File.Open(_path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None)) FinishRound(true);
            Assert.That(_session.PendingSave, Is.True);
            Assert.That(_match.HistoryNotice, Is.Not.Null.And.Not.Empty);
            Assert.That(_session.Continue(), Is.False);
            CollectionAssert.AreEqual(previous, File.ReadAllBytes(_path));
            Assert.That(_session.SaveResult(), Is.True);
            Assert.That(_session.SaveResult(), Is.False);
            Assert.That(new ChallengeSessionStore(_definition, _path).Load().Active.nextStageIndex, Is.EqualTo(1));
        }

        [Test]
        public void CompetingProcessCannotOverwriteNewerFile()
        {
            var first = new ChallengeSessionStore(_definition, _path);
            var second = new ChallengeSessionStore(_definition, _path);
            var save = first.Load();
            second.Load();
            Assert.That(first.Save(save), Is.True);
            var original = File.ReadAllBytes(_path);
            Assert.That(second.Save(save), Is.False);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(_path));
        }

        [Test]
        public void MeasureCheckpointWrites()
        {
            const int samples = 50;
            var timings = new double[samples];
            var store = new ChallengeSessionStore(_definition, _path);
            var save = store.Load();
            string content = JsonUtility.ToJson(new ChallengeSession
            {
                sessionId = Guid.NewGuid().ToString("N"),
                definitionId = _definition.definitionId,
                definitionRevision = _definition.revision
            });
            for (int i = -5; i < samples; i++)
            {
                var session = JsonUtility.FromJson<ChallengeSession>(content);
                session.sessionId = Guid.NewGuid().ToString("N");
                save = save.ChangeActive(session, true);
                long allocation = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                bool written = store.Save(save);
                double milliseconds = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
                Assert.That(written, Is.True);
                if (i < 0) continue;
                timings[i] = milliseconds;
                TestContext.Out.WriteLine($"Checkpoint {i}: {milliseconds:0.000} ms, {bytes} B");
            }
            Array.Sort(timings);
            TestContext.Out.WriteLine($"50 samples after 5 warmups, p50={timings[24]:0.000}, p95={timings[47]:0.000}, max={timings[49]:0.000} ms; {save.archive.Count} archives");
        }

        private void Begin()
        {
            Assert.That(_session.Open(), Is.True);
            Assert.That(_session.Begin(false), Is.True, _session.Notice);
            Assert.That(_session.Begin(false), Is.False);
        }

        private IEnumerator BeginPlaying()
        {
            Assert.That(_match.State, Is.EqualTo(MatchController.MatchState.Countdown));
            typeof(MatchController).GetField("countdownEndsAt", Private).SetValue(_match, Time.time);
            yield return null;
            Assert.That(_match.State, Is.EqualTo(MatchController.MatchState.Playing));
        }

        private void ForceTimeLimit() =>
            typeof(MatchController).GetField("elapsedSeconds", Private).SetValue(_match, _match.Rules.timeLimitSeconds);

        private void FinishRound(bool playerWins)
        {
            var defeated = playerWins ? _match.Opponent : _match.Player;
            defeated.GetComponent<RobotHealth>().TakeDamage(1000);
            Assert.That(_match.State, Is.EqualTo(MatchController.MatchState.Result));
        }
    }
}
