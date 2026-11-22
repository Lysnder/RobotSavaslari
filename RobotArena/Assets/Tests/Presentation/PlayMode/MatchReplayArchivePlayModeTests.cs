using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
using RobotSavaslari.Training;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace RobotSavaslari.Presentation.Tests
{
    /// <summary>
    /// Full-encounter archive on MainArena: recording single matches and series, the Ready-menu archive
    /// mode and what it gives back, look-only playback, refusal of broken or incompatible files, the
    /// existing barrier and conveyor visuals, and sampling cost. Each test uses its own temporary
    /// archive and history folder; the tests cause damage and results themselves.
    /// </summary>
    public sealed class MatchReplayArchivePlayModeTests
    {
        private string _directory;
        private MatchReplayArchiveStore _store;
        private MatchReplayArchiveRecorder _recorder;
        private MatchReplayArchivePlayer _player;
        private MatchReplayArchiveScreen _screen;

        private static MatchController Match => MainArenaTestScene.Match;
        private static CombatCameraController CameraRig => MainArenaTestScene.Camera;

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            if (_player != null) _player.CloseArchiveMode();
            yield return MainArenaTestScene.Unload();
            try { if (_directory != null && Directory.Exists(_directory)) Directory.Delete(_directory, true); }
            catch (IOException) { }
        }

        private IEnumerator Load()
        {
            yield return MainArenaTestScene.Load();
            _directory = Path.Combine(Path.GetTempPath(), "RobotSavaslariArchivePlayMode", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _store = new MatchReplayArchiveStore(Path.Combine(_directory, "replays"));
            Match.HistoryStore = new MatchHistoryStore(Path.Combine(_directory, "match-history.json"));
            Assert.That(MainArenaPresentationInstaller.UseArchiveStore(Match, _store), Is.True);
            _recorder = Match.GetComponent<MatchReplayArchiveRecorder>();
            _player = Match.GetComponent<MatchReplayArchivePlayer>();
            _screen = Match.GetComponent<MatchReplayArchiveScreen>();
            Assert.That(Match.GetComponents<MatchReplayArchiveRecorder>().Length, Is.EqualTo(1));
            Assert.That(Match.GetComponents<MatchReplayArchivePlayer>().Length, Is.EqualTo(1));
            Assert.That(Match.GetComponents<MatchReplayArchiveScreen>().Length, Is.EqualTo(1));
            Assert.That(MainArenaPresentationInstaller.Install(Match.gameObject.scene), Is.Not.Null, "installing again is idempotent");
            Assert.That(Match.GetComponents<MatchReplayArchiveRecorder>().Length, Is.EqualTo(1));
            Assert.That(_recorder.Store, Is.SameAs(_store), "reinstalling keeps the bound store");
        }

        /// <summary>
        /// Adds the driving features the way the game does when MainArena is the startup scene
        /// (DrivingFeatureInstaller.Launch); tests load the scene later, so it is not there on its own.
        /// </summary>
        private static IEnumerator InstallDrivingFeatures()
        {
            var installer = Object.FindAnyObjectByType<DrivingFeatureInstaller>();
            if (installer == null) installer = new GameObject("Driving Features").AddComponent<DrivingFeatureInstaller>();
            yield return null;
            yield return null;
            Assert.That(installer.Installed, Is.True, "driving features installed");
        }

        private static void SeparateRobots()
        {
            MainArenaTestScene.PlaceRobot(Match.Player, new Vector3(-4.5f, MainArenaTestScene.Floor, -3.5f), true);
            MainArenaTestScene.PlaceRobot(Match.Opponent, new Vector3(4.5f, MainArenaTestScene.Floor, 3.5f), true);
        }

        /// <summary>Plays one round: a hit that tears a wheel off the opponent, then the opponent is defeated.</summary>
        private IEnumerator PlayRoundWithDetachedWheel(float seconds = 1f)
        {
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            yield return new WaitForSeconds(seconds * 0.5f);
            var visuals = Match.Opponent.GetComponent<RobotDamageVisuals>();
            RobotDamageVisuals.Part wheel = visuals.Parts.First(p => p.Kind == RobotDamageVisuals.PartKind.Wheel);
            Match.Opponent.GetComponent<RobotHealth>().TakeDamage(40f, wheel.Transform.position, Vector3.up, Vector3.zero, true);
            Assert.That(wheel.Detached, Is.True);
            yield return new WaitForSeconds(seconds * 0.5f);
            Match.Defeat(false);
            yield return null;
        }

        private MatchReplayArchiveStore.Entry SingleEntry()
        {
            List<MatchReplayArchiveStore.Entry> entries = _store.List();
            Assert.That(entries.Count, Is.EqualTo(1), "archives: " + string.Join(", ", entries.Select(e => e.Info?.Title ?? e.Error)));
            return entries[0];
        }

        private IEnumerator BackToReady()
        {
            Match.Prepare();
            yield return null;
            Assert.That(Match.State, Is.EqualTo(MatchController.MatchState.Ready));
        }

        private static void AssertLookOnly(Transform root)
        {
            Assert.That(root, Is.Not.Null);
            var allowed = new HashSet<Type> { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer) };
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                Assert.That(allowed.Contains(component.GetType()), Is.True, component.GetType().Name + " on proxy " + component.name);
        }

        // ---- Recording -----------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SingleMatchIsArchivedWithEventsAndPlaysWithLookOnlyProxies()
        {
            yield return Load();
            yield return PlayRoundWithDetachedWheel();
            Assert.That(_recorder.LastCommit, Is.EqualTo(MatchReplayArchiveStore.CommitResult.Archived), _recorder.LastStatus);
            StringAssert.Contains("Tam tekrar arşive kaydedildi", _recorder.LastStatus);

            MatchReplayArchiveStore.Entry entry = SingleEntry();
            ReplayArchiveInfo info = entry.Info;
            Assert.That(info.IsComplete, Is.True, MatchReplayArchive.DescribeCompleteness(info));
            Assert.That(info.EncounterId, Is.EqualTo(Match.LastMatch.id), "linked to the match history record");
            Assert.That(info.Rounds.Count, Is.EqualTo(1));
            Assert.That(info.Duration, Is.GreaterThan(0.8f));
            List<ReplayEventType> types = info.Events.Select(e => e.Type).ToList();
            Assert.That(types.First(), Is.EqualTo(ReplayEventType.RoundStart));
            Assert.That(types, Does.Contain(ReplayEventType.Impact));
            Assert.That(types, Does.Contain(ReplayEventType.PartDetached));
            Assert.That(types.Skip(types.Count - 2), Is.EqualTo(new[] { ReplayEventType.RoundEnd, ReplayEventType.EncounterEnd }));
            ReplayEvent detached = info.Events.First(e => e.Type == ReplayEventType.PartDetached);
            Assert.That(detached.Robot, Is.EqualTo(MatchReplayRecorder.OpponentRobot));
            StringAssert.StartsWith("robot/" + Match.Opponent.name + "/", info.Tracks[detached.Track].Key);
            Assert.That(info.Tracks.Any(t => t.Kind == ReplayTrackKind.RobotDebris), Is.True);
            Assert.That(info.Tracks.Count, Is.LessThanOrEqualTo(MatchReplayArchive.MaxTracks));

            yield return BackToReady();
            float playerHealth = Match.Player.GetComponent<RobotHealth>().CurrentHealth;
            float opponentHealth = Match.Opponent.GetComponent<RobotHealth>().CurrentHealth;
            int matchCount = Match.MatchCount;
            Vector3 playerPosition = Match.Player.transform.position;
            int healthEvents = 0;
            Action<RobotHealth> onHealth = _ => healthEvents++;
            Match.Player.GetComponent<RobotHealth>().HealthChanged += onHealth;
            Match.Opponent.GetComponent<RobotHealth>().HealthChanged += onHealth;

            Assert.That(_screen.OpenArchive(), Is.True, _screen.Notice);
            Assert.That(_screen.Entries.Count, Is.EqualTo(1));
            Assert.That(_screen.Play(_screen.Entries[0]), Is.True, _screen.Notice);
            yield return null;
            Assert.That(_player.IsActive, Is.True);
            AssertLookOnly(_player.ProxyRoot);
            Assert.That(_player.ProxyRoot.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(_player.ProxyRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(_player.ProxyRoot.GetComponentsInChildren<RobotHealth>(true), Is.Empty);
            Assert.That(_player.ProxyCount, Is.EqualTo(info.Tracks.Count), "every track has a proxy");
            Assert.That(CameraRig.Target, Is.EqualTo(_player.PlayerProxy), "camera frames the archive");
            MainArenaTestScene.AssertSingleCameraAndListener();

            int partTrack = detached.Track;
            int debrisTrack = info.Tracks.FindIndex(t => t.Kind == ReplayTrackKind.RobotDebris && !t.Wear &&
                MatchReplayArchive.KeyRobotPath(t.Key) == MatchReplayArchive.KeyRobotPath(info.Tracks[partTrack].Key) &&
                MatchReplayArchive.KeyRobotName(t.Key) == Match.Opponent.name);
            Assert.That(debrisTrack, Is.GreaterThanOrEqualTo(0));
            _player.Seek(0f);
            Assert.That(_player.GetProxy(partTrack).enabled, Is.True, "wheel on the robot at the start");
            Assert.That(_player.GetProxy(debrisTrack).enabled, Is.False, "no debris at the start");
            _player.Seek(detached.Time + 0.2f);
            Assert.That(_player.GetProxy(partTrack).enabled, Is.False, "wheel gone after it came off");
            Assert.That(_player.GetProxy(debrisTrack).enabled, Is.True, "the detached wheel flies as debris");

            float before = _player.PlaybackTime;
            _player.Seek(0f);
            _player.SetSpeed(0.5f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(_player.PlaybackTime, Is.InRange(0.1f, 0.4f), "half speed");
            Assert.That(_player.StepEvent(1), Is.True);
            Assert.That(info.Events.Any(e => Mathf.Abs(e.Time - _player.PlaybackTime) < 1e-3f), Is.True, "stepping lands on an event");
            _player.SetView(MatchReplayArchivePlayer.View.Opponent);
            Assert.That(CameraRig.Target, Is.Not.Null);
            yield return null;
            Assert.That(CameraRig.Target, Is.EqualTo(_player.OpponentProxy));
            Assert.That(_player.Inspect(MatchReplayRecorder.OpponentRobot), Is.True);
            _player.Seek(_player.Duration);
            var parts = new List<KeyValuePair<string, bool>>();
            _player.GetPartStates(MatchReplayRecorder.OpponentRobot, parts);
            Assert.That(parts.Count, Is.EqualTo(Match.Opponent.GetComponent<RobotDamageVisuals>().Parts.Count), "every damage part is listed");
            Assert.That(parts.Where(p => !p.Value).Select(p => p.Key), Is.EquivalentTo(new[] { info.Tracks[partTrack].Key.Split('/').Last() }),
                "only the torn-off wheel is shown as lost at the end");

            _screen.CloseArchive();
            yield return null;
            Match.Player.GetComponent<RobotHealth>().HealthChanged -= onHealth;
            Match.Opponent.GetComponent<RobotHealth>().HealthChanged -= onHealth;
            Assert.That(healthEvents, Is.Zero, "playback never changes health");
            Assert.That(Match.Player.GetComponent<RobotHealth>().CurrentHealth, Is.EqualTo(playerHealth));
            Assert.That(Match.Opponent.GetComponent<RobotHealth>().CurrentHealth, Is.EqualTo(opponentHealth));
            Assert.That(Match.MatchCount, Is.EqualTo(matchCount));
            Assert.That(Match.State, Is.EqualTo(MatchController.MatchState.Ready));
            Assert.That(Match.Player.transform.position, Is.EqualTo(playerPosition));
            Assert.That(GameObject.Find("Match Replay Archive"), Is.Null, "proxies removed");
            Assert.That(before, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator SeriesRecordsEveryRoundWithOrderedBoundaries()
        {
            yield return Load();
            Assert.That(Match.ConfigureRules(new MatchRules { mode = MatchMode.FirstToTwo }), Is.True);
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            yield return new WaitForSeconds(0.6f);
            Match.Defeat(false);
            Assert.That(Match.EncounterComplete, Is.False);
            Assert.That(_recorder.IsRecording, Is.True, "the series keeps recording between rounds");
            Assert.That(_store.List(), Is.Empty, "nothing archived before the series is decided");

            Assert.That(Match.ContinueSeries(), Is.True);
            yield return new WaitUntil(() => Match.State == MatchController.MatchState.Playing);
            SeparateRobots();
            yield return new WaitForSeconds(0.6f);
            Match.Opponent.GetComponent<RobotHealth>().TakeDamage(5f, Match.Opponent.transform.position + Vector3.up * 0.4f, Vector3.up, Vector3.zero, true);
            yield return new WaitForSeconds(0.2f);
            Match.Defeat(false);
            Assert.That(Match.EncounterComplete, Is.True);
            yield return null;

            ReplayArchiveInfo info = SingleEntry().Info;
            Assert.That(info.IsComplete, Is.True);
            Assert.That(info.RulesMode, Is.EqualTo((int)MatchMode.FirstToTwo));
            Assert.That(info.Rounds.Count, Is.EqualTo(2));
            Assert.That(info.Rounds[0].RoundNumber, Is.EqualTo(1));
            Assert.That(info.Rounds[1].RoundNumber, Is.EqualTo(2));
            Assert.That(info.Rounds[1].StartTime, Is.GreaterThan(info.Rounds[0].EndTime));
            Assert.That(info.Rounds.All(r => r.Outcome == (int)MatchController.MatchOutcome.PlayerVictory), Is.True);
            Assert.That(info.PlayerWins, Is.EqualTo(2));
            Assert.That(info.EncounterId, Is.EqualTo(Match.LastMatch.id));
            Assert.That(info.Events.Count(e => e.Type == ReplayEventType.RoundStart), Is.EqualTo(2));
            Assert.That(info.Events.Count(e => e.Type == ReplayEventType.RoundEnd), Is.EqualTo(2));
            Assert.That(info.Events.Count(e => e.Type == ReplayEventType.EncounterEnd), Is.EqualTo(1));
            for (int i = 1; i < info.Events.Count; i++)
                Assert.That(MatchReplayArchive.CompareEvents(info.Events[i - 1], info.Events[i]), Is.LessThan(0), "stable event order");
            Assert.That(info.Chunks.Count(c => c.Round == 0), Is.GreaterThan(0));
            Assert.That(info.Chunks.Count(c => c.Round == 1), Is.GreaterThan(0));
            StringAssert.StartsWith("Seri 2-0", info.Title);

            // A second encounter gets its own archive.
            yield return BackToReady();
            Assert.That(Match.ConfigureRules(new MatchRules { mode = MatchMode.Single }), Is.True);
            yield return PlayRoundWithDetachedWheel(0.6f);
            List<MatchReplayArchiveStore.Entry> entries = _store.List();
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries.Select(e => e.Info.EncounterId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(entries.Count(e => e.Info.RulesMode == (int)MatchMode.Single && e.Info.Rounds.Count == 1), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AbandonedSeriesIsArchivedAsIncompleteAndHistoryStaysUntouched()
        {
            yield return Load();
            Assert.That(Match.ConfigureRules(new MatchRules { mode = MatchMode.FirstToTwo }), Is.True);
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            yield return new WaitForSeconds(0.5f);
            Match.Defeat(true);
            yield return BackToReady();
            yield return null;

            ReplayArchiveInfo info = SingleEntry().Info;
            Assert.That(info.Completeness & ReplayCompleteness.Abandoned, Is.EqualTo(ReplayCompleteness.Abandoned));
            Assert.That(info.EncounterId, Is.Null, "an undecided series has no history record");
            StringAssert.Contains("Yarım kalan seri", info.Title);
            Assert.That(Match.HistoryStore.Load(), Is.Empty);

            // A decided single match afterwards is linked; deleting the history does not stop it from opening.
            Assert.That(Match.ConfigureRules(new MatchRules { mode = MatchMode.Single }), Is.True);
            yield return PlayRoundWithDetachedWheel(0.6f);
            Assert.That(_store.List().Count, Is.EqualTo(2));
            File.Delete(Match.HistoryStore.FilePath);
            yield return BackToReady();
            Assert.That(_screen.OpenArchive(), Is.True, _screen.Notice);
            Assert.That(_screen.Play(_screen.Entries.First(e => e.Info.IsComplete)), Is.True, _screen.Notice);
            _screen.CloseArchive();
        }

        // ---- Mode ownership ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ArchiveOpensOnlyFromTheNormalReadyMenu()
        {
            yield return Load();
            yield return InstallDrivingFeatures();
            var settings = Match.GetComponent<SettingsPanelController>();
            var bindings = Match.Player.GetComponent<DriveBindingsScreen>();
            Assert.That(bindings, Is.Not.Null, "driving features installed");
            yield return null;
            Assert.That(_player.CanOpenArchiveMode(out string reason), Is.True, reason);
            Assert.That(_screen.IsEntryVisible, Is.True);

            settings.ToggleSettings();
            Assert.That(_player.CanOpenArchiveMode(out reason), Is.False);
            Assert.That(_screen.OpenArchive(), Is.False);
            Assert.That(_screen.Notice, Is.Not.Null.And.Not.Empty, "the refusal is shown");
            settings.ToggleSettings();
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            bindings.Open();
            Assert.That(_player.CanOpenArchiveMode(out reason), Is.False);
            StringAssert.Contains("Tuş", reason);
            bindings.Close();

            FieldInfo history = typeof(MainMenuScreen).GetField("_showHistory", BindingFlags.NonPublic | BindingFlags.Instance);
            Assume.That(history, Is.Not.Null);
            history.SetValue(Match.GetComponent<MainMenuScreen>(), true);
            Assert.That(_player.CanOpenArchiveMode(out reason), Is.False, "not over the match history panel");
            history.SetValue(Match.GetComponent<MainMenuScreen>(), false);

            var installer = Object.FindAnyObjectByType<DrivingFeatureInstaller>();
            yield return null; // the installer opens the training gate for this Ready frame
            Assert.That(installer.TimeTrial.Enter(), Is.True, "training can start from Ready");
            Assert.That(_player.CanOpenArchiveMode(out reason), Is.False);
            installer.TimeTrial.Exit();
            yield return null;

            CameraRig.SetPresentationTargets(Match.Player.transform, null);
            Assert.That(_player.CanOpenArchiveMode(out reason), Is.False, "another presentation owns the camera");
            CameraRig.ClearPresentationTargets();

            yield return MainArenaTestScene.StartPlaying();
            Assert.That(_player.CanOpenArchiveMode(out reason), Is.False, "not while playing");
            Match.Defeat(false);
            Assert.That(_player.CanOpenArchiveMode(out reason), Is.False, "not on the result screen");
            yield return BackToReady();
            Assert.That(_player.CanOpenArchiveMode(out reason), Is.True, reason);

            Match.enabled = false;
            Assert.That(_player.CanOpenArchiveMode(out reason), Is.False, "not with a disabled match");
            Match.enabled = true;
        }

        [UnityTest]
        public IEnumerator TenOpenCloseCyclesGiveEverythingBack()
        {
            yield return Load();
            yield return InstallDrivingFeatures();
            yield return PlayRoundWithDetachedWheel(0.6f);
            yield return BackToReady();
            yield return null;

            var menu = Match.GetComponent<MainMenuScreen>();
            var installer = Object.FindAnyObjectByType<DrivingFeatureInstaller>();
            var trialHud = installer.GetComponent<TimeTrialHud>();
            var bindings = Match.Player.GetComponent<DriveBindingsScreen>();
            var driver = Match.Player.GetComponent<PlayerKeyboardDriver>();
            // A behaviour that was already off must stay off afterwards.
            driver.enabled = false;
            var gauge = Match.Player.GetComponent<DriveBoostGauge>();
            Behaviour[] owned = { menu, installer, trialHud, bindings, driver, gauge };
            Assert.That(owned, Has.None.Null, "every owned component exists on MainArena with driving features");
            bool[] enabled = owned.Select(b => b.enabled).ToArray();
            Renderer[] live = Match.Player.GetComponentsInChildren<Renderer>(true)
                .Concat(Match.Opponent.GetComponentsInChildren<Renderer>(true))
                .Concat(Object.FindAnyObjectByType<RobotSavaslari.Arena.ArenaEnvironment>().Contents.GetComponentsInChildren<Renderer>(true)).ToArray();
            bool[] forceOff = live.Select(r => r.forceRenderingOff).ToArray();
            CombatCameraController.CameraMode mode = CameraRig.Mode;
            float yaw = CameraRig.Yaw, pitch = CameraRig.Pitch, distance = CameraRig.Distance;
            int roots = Match.gameObject.scene.rootCount;

            for (int cycle = 0; cycle < 10; cycle++)
            {
                Assert.That(_screen.OpenArchive(), Is.True, "cycle " + cycle + ": " + _screen.Notice);
                Assert.That(owned.All(b => !b.enabled), Is.True, "menu, training entry, bindings and driving are off");
                Assert.That(_screen.Play(_screen.Entries[0]), Is.True, _screen.Notice);
                yield return null;
                Assert.That(live.All(r => r.forceRenderingOff), Is.True, "live robots and arena hidden");
                _player.Seek(cycle * 0.1f);
                _player.SetView((MatchReplayArchivePlayer.View)(cycle % 3));
                if (cycle % 2 == 1) _player.Inspect(cycle % 4 == 1 ? 0 : 1);
                if (cycle % 3 == 2)
                {
                    _player.Stop();
                    Assert.That(owned.All(b => !b.enabled), Is.True, "back to the list keeps archive mode");
                    Assert.That(CameraRig.HasPresentationTargets, Is.False);
                }
                _screen.CloseArchive();
                yield return null;
                Assert.That(owned.Select(b => b.enabled).ToArray(), Is.EqualTo(enabled), "cycle " + cycle + ": enabled states restored");
                Assert.That(live.Select(r => r.forceRenderingOff).ToArray(), Is.EqualTo(forceOff), "cycle " + cycle + ": renderers restored");
                Assert.That(CameraRig.HasPresentationTargets, Is.False);
                Assert.That(CameraRig.Mode, Is.EqualTo(mode));
                Assert.That(CameraRig.Yaw, Is.EqualTo(yaw).Within(1e-3f));
                Assert.That(CameraRig.Pitch, Is.EqualTo(pitch).Within(1e-3f));
                Assert.That(CameraRig.Distance, Is.EqualTo(distance).Within(1e-3f));
                Assert.That(Match.gameObject.scene.rootCount, Is.EqualTo(roots), "no leftover objects");
                Assert.That(GUIUtility.keyboardControl, Is.Zero);
            }
            Assert.That(CameraRig.Target, Is.EqualTo(Match.Player.transform), "camera back on the live robot");
            Assert.That(Match.RequestCountdown(), Is.True, "a normal match starts afterwards");
        }

        [UnityTest]
        public IEnumerator PauseDisableAndLeavingReadyCloseSafely()
        {
            yield return Load();
            yield return PlayRoundWithDetachedWheel(1.2f);
            yield return BackToReady();
            var menu = Match.GetComponent<MainMenuScreen>();
            var settings = Match.GetComponent<SettingsPanelController>();

            Assert.That(_screen.OpenArchive(), Is.True);
            Assert.That(_screen.Play(_screen.Entries[0]), Is.True);
            yield return new WaitForSecondsRealtime(0.1f);
            settings.ToggleSettings();
            float paused = _player.PlaybackTime;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(_player.PlaybackTime, Is.EqualTo(paused), "settings pause the archive");
            settings.ToggleSettings();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(_player.PlaybackTime, Is.GreaterThan(paused), "and it resumes");

            _player.enabled = false;
            Assert.That(_player.IsArchiveModeOpen, Is.False, "disabling closes archive mode");
            Assert.That(menu.enabled, Is.True);
            Assert.That(CameraRig.HasPresentationTargets, Is.False);
            _player.enabled = true;

            Assert.That(_screen.OpenArchive(), Is.True);
            Assert.That(_screen.Play(_screen.Entries[0]), Is.True);
            // Something else starts a countdown: archive mode lets go at once.
            menu.enabled = true;
            Assert.That(Match.RequestCountdown(), Is.True);
            yield return null;
            Assert.That(_player.IsArchiveModeOpen, Is.False);
            Assert.That(_player.IsActive, Is.False);
            Assert.That(menu.enabled, Is.True);
            StringAssert.Contains("hazır ekranından çıktı", _player.LastError);
        }

        // ---- Broken and incompatible archives -------------------------------------------------------

        [UnityTest]
        public IEnumerator IncompatibleAndCorruptArchivesAreShownButNotPlayed()
        {
            yield return Load();
            yield return PlayRoundWithDetachedWheel(0.6f);
            yield return BackToReady();
            MatchReplayArchiveStore.Entry good = SingleEntry();

            // Same archive, but its robot is named differently (a changed robot asset).
            string renamed = CopyWithChangedFooter(good, info =>
            {
                foreach (ReplayTrack track in info.Tracks)
                    track.Key = track.Key.Replace("/" + Match.Opponent.name + "/", "/Eski Robot/");
            });
            // Same archive from another content revision.
            string revision = CopyWithChangedFooter(good, info => info.ContentRevision = MatchReplayArchive.ContentRevision + 1);
            // Same archive with a damaged frame.
            string corrupt = Path.Combine(_store.DirectoryPath, Guid.NewGuid().ToString("N") + ".rpl");
            byte[] bytes = File.ReadAllBytes(good.Path);
            bytes[MatchReplayArchive.HeaderBytes + 64] ^= 0x7F;
            File.WriteAllBytes(corrupt, bytes);
            RewriteId(corrupt);

            var menu = Match.GetComponent<MainMenuScreen>();
            Assert.That(_screen.OpenArchive(), Is.True);
            IReadOnlyList<MatchReplayArchiveStore.Entry> entries = _screen.Entries;
            Assert.That(entries.Count, Is.EqualTo(4));
            string Compatibility(string path) => _player.CheckCompatibility(entries.First(e => e.Path == path).Info);
            Assert.That(Compatibility(good.Path), Is.Null);
            StringAssert.Contains("robot bulunamadı", Compatibility(renamed));
            StringAssert.Contains("içerik revizyonu", Compatibility(revision));

            Assert.That(_screen.Play(entries.First(e => e.Path == renamed)), Is.False);
            StringAssert.Contains("Uyumsuz", _screen.Notice);
            Assert.That(_screen.Play(entries.First(e => e.Path == corrupt)), Is.False);
            StringAssert.Contains("Bozuk", _screen.Notice);
            Assert.That(_player.IsActive, Is.False);
            Assert.That(GameObject.Find("Match Replay Archive"), Is.Null, "nothing was built for a refused archive");
            Assert.That(CameraRig.HasPresentationTargets, Is.False);

            Assert.That(_screen.ConfirmDelete(Path.GetFileNameWithoutExtension(corrupt)), Is.True, "a broken archive can be deleted");
            Assert.That(File.Exists(corrupt), Is.False);
            Assert.That(File.Exists(good.Path), Is.True, "only that archive was deleted");
            _screen.CloseArchive();
            Assert.That(menu.enabled, Is.True);
            yield return MainArenaTestScene.StartPlaying();
            Assert.That(Match.State, Is.EqualTo(MatchController.MatchState.Playing), "the normal match still works");
        }

        private string CopyWithChangedFooter(MatchReplayArchiveStore.Entry source, Action<ReplayArchiveInfo> change)
        {
            ReplayArchiveInfo info = MatchReplayArchive.ReadInfo(source.Path, out string error);
            Assert.That(info, Is.Not.Null, error);
            info.Id = Guid.NewGuid().ToString("N");
            change(info);
            string path = Path.Combine(_store.DirectoryPath, info.Id + ".rpl");
            WriteWithFooter(source.Path, path, info);
            return path;
        }

        private static void RewriteId(string path)
        {
            ReplayArchiveInfo info = MatchReplayArchive.ReadInfo(path, out string error);
            Assert.That(info, Is.Not.Null, error);
            info.Id = Path.GetFileNameWithoutExtension(path);
            WriteWithFooter(path, path, info);
        }

        private static void WriteWithFooter(string source, string target, ReplayArchiveInfo info)
        {
            byte[] bytes = File.ReadAllBytes(source);
            int offset = 8;
            long footerOffset = (uint)MatchReplayArchive.ReadInt32(bytes, ref offset);
            byte[] footer = MatchReplayArchive.EncodeFooter(info);
            using (var stream = new FileStream(target, FileMode.Create))
            {
                MatchReplayArchive.WriteHeader(stream, footerOffset, footer.Length, MatchReplayArchive.Crc32(footer, 0, footer.Length));
                stream.Write(bytes, MatchReplayArchive.HeaderBytes, (int)footerOffset - MatchReplayArchive.HeaderBytes);
                stream.Write(footer, 0, footer.Length);
            }
        }

        // ---- Arena and cost -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ExistingBarrierAndConveyorsAreRecordedAsWhitelistedTracks()
        {
            yield return Load();
            var environment = Object.FindAnyObjectByType<RobotSavaslari.Arena.ArenaEnvironment>();
            Assert.That(environment.Layout.cells.Any(c => c.content == RobotSavaslari.Arena.ArenaContent.Hazard), Is.True, "MainArena's default layout has a warning barrier");
            yield return MainArenaTestScene.StartPlaying();
            // Well away from the barrier cell, so it is never held down as occupied.
            MainArenaTestScene.PlaceRobot(Match.Player, new Vector3(-4.5f, MainArenaTestScene.Floor, -3.5f), true);
            MainArenaTestScene.PlaceRobot(Match.Opponent, new Vector3(2.5f, MainArenaTestScene.Floor, -2.5f), true);
            // Idle, warning and travel: the barrier rises during these seconds.
            yield return new WaitForSeconds(4.5f);
            Match.Defeat(false);
            yield return null;

            ReplayArchiveInfo info = SingleEntry().Info;
            ReplayTrack barrier = info.Tracks.Single(t => t.Kind == ReplayTrackKind.LayoutDynamic && t.Key.EndsWith("/Moving Barrier"));
            Assert.That(info.Tracks.Any(t => t.Kind == ReplayTrackKind.LayoutStatic && t.Key.EndsWith("/Conveyor Deck") && t.HasColor), Is.True);
            Assert.That(info.Cells.Count, Is.EqualTo(64));
            Assert.That(info.ArenaName, Is.EqualTo(environment.Layout.displayName));
            using (MatchReplayArchiveReader reader = MatchReplayArchiveReader.Open(SingleEntry().Path, out string error))
            {
                var poses = new MatchReplayClip.Pose[reader.Info.DynamicTrackCount];
                float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
                for (float t = 0f; t <= reader.EndTime; t += 0.1f)
                {
                    Assert.That(reader.Sample(t, poses, null, null, out error), Is.True, error);
                    lowest = Mathf.Min(lowest, poses[barrier.Column].Position.y);
                    highest = Mathf.Max(highest, poses[barrier.Column].Position.y);
                }
                Assert.That(highest - lowest, Is.GreaterThan(0.3f), "the barrier's movement is in the archive");
            }

            yield return BackToReady();
            Assert.That(_screen.OpenArchive(), Is.True);
            Assert.That(_screen.Play(_screen.Entries[0]), Is.True, _screen.Notice);
            Assert.That(_player.ArenaProxyRoot.childCount, Is.EqualTo(info.Tracks.Count(t => t.Robot == -1)));
            Assert.That(environment.Contents.GetComponentsInChildren<Renderer>().All(r => r.forceRenderingOff), Is.True, "the live arena is hidden");
            _screen.CloseArchive();
            Assert.That(environment.Contents.GetComponentsInChildren<Renderer>().Any(r => r.forceRenderingOff), Is.False);
        }

        [UnityTest]
        [Category("Performance")]
        public IEnumerator RealSceneFitsTheTrackBudgetAndSamplingDoesNotAllocate()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            yield return new WaitForSeconds(1f);
            Assert.That(_recorder.IsRecording, Is.True);
            ReplayArchiveInfo current = _recorder.Current;
            int robotTracks = current.Tracks.Count(t => t.Robot >= 0);
            Debug.Log($"[ReplayArchive] MainArena tracks: {current.Tracks.Count} of {MatchReplayArchive.MaxTracks} " +
                      $"({robotTracks} robot/debris, {current.Tracks.Count - robotTracks} arena; {current.DynamicTrackCount} per frame, {current.WearTrackCount} wear)");
            Assert.That(current.Tracks.Count, Is.LessThanOrEqualTo(MatchReplayArchive.MaxTracks));
            Assert.That(current.Completeness & ReplayCompleteness.TrackLimit, Is.EqualTo(ReplayCompleteness.Complete), current.Warning);

            for (int i = 0; i < 40; i++) _recorder.CaptureNow(); // warm-up
            Assert.That(() => { _recorder.CaptureNow(); }, Is.Not.AllocatingGCMemory(), "a steady-state sample allocates nothing");
            _recorder.ResetSampleTimings();
            for (int i = 0; i < 300; i++)
            {
                yield return null;
                _recorder.CaptureNow();
            }
            int samples = _recorder.GetSampleTimings(out float p50, out float p95, out float max);
            Debug.Log($"[ReplayArchive] Editor sampling {samples} samples: p50 {p50:0.000} ms, p95 {p95:0.000} ms, max {max:0.000} ms");
            Assert.That(p95, Is.LessThanOrEqualTo(2f), "sampling p95");
            Match.Defeat(false);
        }

        /// <summary>
        /// The Editor's managed heap grows during play whatever the archive does, so the recorder is measured
        /// against itself: the same number of frames with the recorder off, on, and off again.
        /// </summary>
        [UnityTest]
        [Category("LongRun")]
        [Timeout(600000)]
        public IEnumerator RecordingAddsNoManagedHeapGrowth()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            Time.captureDeltaTime = 1f / MatchReplayArchive.SampleRate;
            var growth = new long[3];
            for (int phase = 0; phase < 3; phase++)
            {
                _recorder.enabled = phase == 1;
                if (phase == 1)
                {
                    // Re-enabling starts a fresh archive on the next playing frame (the previous one was closed as interrupted).
                    yield return null;
                    Assert.That(_recorder.IsRecording, Is.True, _recorder.LastStatus);
                }
                long before = GC.GetTotalMemory(true);
                for (int i = 0; i < 2000; i++) yield return null;
                growth[phase] = GC.GetTotalMemory(true) - before;
            }
            Time.captureDeltaTime = 0f;
            long baseline = (growth[0] + growth[2]) / 2;
            Debug.Log($"[ReplayArchive] managed heap over 2000 frames (100 s of play): recorder off {growth[0] / 1048576f:0.00} MiB, " +
                      $"on {growth[1] / 1048576f:0.00} MiB, off {growth[2] / 1048576f:0.00} MiB");
            Assert.That(growth[1] - baseline, Is.LessThan(4L * 1024 * 1024), "the recorder adds no heap growth beyond measurement noise");
        }

        /// <summary>
        /// Five and fifteen minutes of game time through the real recorder on MainArena. Each frame is one
        /// archive sample (Time.captureDeltaTime), so the run takes far less wall time than it records.
        /// </summary>
        [UnityTest]
        [Category("LongRun")]
        [Timeout(900000)]
        public IEnumerator FiveAndFifteenMinuteRecordingsStayWithinBudget([Values(5, 15)] int minutes)
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            Time.captureDeltaTime = 1f / MatchReplayArchive.SampleRate;
            float end = Time.time + minutes * 60f;
            float started = Time.realtimeSinceStartup;
            int frames = 0;
            while (Time.time < end)
            {
                frames++;
                if (frames % 2000 == 0)
                {
                    Assert.That(Match.State, Is.EqualTo(MatchController.MatchState.Playing), "the match keeps running");
                    Assert.That(_recorder.IsRecording, Is.True, _recorder.LastStatus);
                }
                yield return null;
            }
            float wall = Time.realtimeSinceStartup - started;
            long held = _recorder.HeldBytes;
            int samples = _recorder.GetSampleTimings(out float p50, out float p95, out float max);
            Time.captureDeltaTime = 0f;
            Match.Defeat(false);
            yield return null;

            MatchReplayArchiveStore.Entry entry = SingleEntry();
            Assert.That(entry.Info.IsComplete, Is.True, MatchReplayArchive.DescribeCompleteness(entry.Info));
            Assert.That(entry.Info.Duration, Is.EqualTo(minutes * 60f).Within(1f));
            int recorded = entry.Info.Chunks.Sum(c => c.FrameCount);
            Assert.That(recorded, Is.InRange(minutes * 60 * MatchReplayArchive.SampleRate - 2, minutes * 60 * MatchReplayArchive.SampleRate + 2),
                "one archive frame per 1/20 s of play");
            using (MatchReplayArchiveReader reader = MatchReplayArchiveReader.Open(entry.Path, out string error))
            {
                var verify = System.Diagnostics.Stopwatch.StartNew();
                Assert.That(reader.VerifyAllChunks(out error), Is.True, error);
                verify.Stop();
                var poses = new MatchReplayClip.Pose[reader.Info.DynamicTrackCount];
                var wear = new byte[reader.Info.WearTrackCount];
                var marks = new Vector4[reader.Info.WearTrackCount * 4];
                var random = new System.Random(minutes);
                var seeks = new List<double>();
                for (int i = 0; i < 200; i++)
                {
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    Assert.That(reader.Sample((float)random.NextDouble() * reader.EndTime, poses, wear, marks, out error), Is.True, error);
                    seeks.Add((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                }
                seeks.Sort();
                Debug.Log($"[ReplayArchive] {minutes} min real recorder: {frames} frames in {wall:0.0} s wall, file {entry.Bytes / 1048576f:0.0} MiB, " +
                          $"{entry.Info.Tracks.Count} tracks, {entry.Info.Chunks.Count} chunks, {recorded} frames recorded; sampling p50 {p50:0.000} p95 {p95:0.000} max {max:0.000} ms ({samples} last samples); " +
                          $"verify {verify.Elapsed.TotalMilliseconds:0} ms; seek p50 {seeks[99]:0.000} p95 {seeks[189]:0.000} max {seeks[199]:0.000} ms; reader memory {reader.DecodedBytes / 1024f:0} KiB; " +
                          $"recorder holds {held / 1024f:0} KiB");
                Assert.That(held + reader.DecodedBytes, Is.LessThan(64L * 1024 * 1024), "recording and playback memory budget");
                Assert.That(seeks[189], Is.LessThanOrEqualTo(200.0));
                Assert.That(p95, Is.LessThanOrEqualTo(2f));
            }
            Assert.That(entry.Bytes, Is.LessThan(MatchReplayArchive.MaxFileBytes));
        }
    }
}
