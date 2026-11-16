using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Presentation.Tests
{
    /// <summary>
    /// Last-hit replay on MainArena: bounded recording, frozen at the result, cleared for the next
    /// match, and playback with look-only proxies that leaves health, counters and events untouched.
    /// Tests cause damage through RobotHealth themselves; the replay code never does.
    /// </summary>
    public sealed class MatchReplayPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator UnloadArena() => MainArenaTestScene.Unload();

        private static MatchController Match => MainArenaTestScene.Match;
        private static CombatCameraController CameraRig => MainArenaTestScene.Camera;
        private MatchReplayRecorder _recorder;
        private MatchReplayPlayer _replay;

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        private IEnumerator Load()
        {
            yield return MainArenaTestScene.Load();
            _recorder = Match.GetComponent<MatchReplayRecorder>();
            _replay = Match.GetComponent<MatchReplayPlayer>();
            Assert.That(_recorder, Is.Not.Null, "recorder installed");
            Assert.That(_replay, Is.Not.Null, "player installed");
            Assert.That(Match.GetComponents<MatchReplayRecorder>().Length, Is.EqualTo(1));
            Assert.That(Match.GetComponents<ResultPresentationScreen>().Length, Is.EqualTo(1));
        }

        /// <summary>Kinematic robots in opposite corners: the bot cannot reach the player during long waits.</summary>
        private static void SeparateRobots()
        {
            MainArenaTestScene.PlaceRobot(Match.Player, new Vector3(-4.5f, MainArenaTestScene.Floor, -3.5f), true);
            MainArenaTestScene.PlaceRobot(Match.Opponent, new Vector3(4.5f, MainArenaTestScene.Floor, 3.5f), true);
        }

        private struct GameplayCounters
        {
            public float PlayerHealth, OpponentHealth;
            public int MatchCount, ResultCount;
            public MatchController.MatchState State;
            public MatchController.MatchOutcome Outcome;
            public Vector3 PlayerPosition, OpponentPosition;

            public static GameplayCounters Read() => new GameplayCounters
            {
                PlayerHealth = Match.Player.GetComponent<RobotHealth>().CurrentHealth,
                OpponentHealth = Match.Opponent.GetComponent<RobotHealth>().CurrentHealth,
                MatchCount = Match.MatchCount,
                ResultCount = Match.ResultCount,
                State = Match.State,
                Outcome = Match.Outcome,
                PlayerPosition = Match.Player.transform.position,
                OpponentPosition = Match.Opponent.transform.position,
            };
        }

        [UnityTest]
        public IEnumerator RecordsOnlyWhilePlayingAndFreezesAtTheResult()
        {
            yield return Load();
            Assert.That(Match.RequestCountdown(), Is.True);
            yield return null;
            Assert.That(_recorder.IsRecording, Is.False, "no recording during the countdown");
            yield return new WaitUntil(() => Match.State == MatchController.MatchState.Playing);
            SeparateRobots();
            yield return new WaitForSeconds(1f);
            Assert.That(_recorder.IsRecording, Is.True);
            Assert.That(_recorder.Clip.Count, Is.GreaterThan(10));
            Assert.That(_replay.CanBegin, Is.False, "replay is offered only on the result screen");

            Match.Defeat(false);
            Assert.That(_recorder.IsFrozen, Is.True);
            Assert.That(_recorder.HasReplay, Is.True);
            int frames = _recorder.Clip.Count;
            float end = _recorder.Clip.EndTime;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(_recorder.Clip.Count, Is.EqualTo(frames), "frozen clip does not change");
            Assert.That(_recorder.Clip.EndTime, Is.EqualTo(end));
        }

        [UnityTest]
        public IEnumerator LongMatchUsesBoundedBuffer()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            yield return new WaitForSeconds(_recorder.BufferSeconds + 1.5f);
            Match.Defeat(true);

            MatchReplayClip clip = _recorder.Clip;
            Assert.That(clip.Count, Is.LessThanOrEqualTo(_recorder.FrameCapacity));
            Assert.That(clip.Capacity, Is.EqualTo(_recorder.FrameCapacity));
            Assert.That(clip.PoseStorageLength, Is.EqualTo(_recorder.FrameCapacity * clip.TrackCount), "fixed memory");
            Assert.That(clip.Duration, Is.LessThanOrEqualTo(_recorder.BufferSeconds + 2f / _recorder.SamplesPerSecond));
            Assert.That(clip.Duration, Is.GreaterThan(_recorder.BufferSeconds - 0.5f), "the buffer was filled");
        }

        [UnityTest]
        public IEnumerator ReplayHasNoGameplaySideEffects()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            // Kinematic robots: any position change afterwards could only come from the replay.
            SeparateRobots();
            var opponentHealth = Match.Opponent.GetComponent<RobotHealth>();
            Vector3 firstHit = opponentHealth.transform.position + new Vector3(0.4f, 0.5f, 0f);
            opponentHealth.TakeDamage(15f, firstHit, Vector3.right, Vector3.zero, true);
            yield return new WaitForSeconds(0.4f);
            Vector3 decidingHit = opponentHealth.transform.position + new Vector3(-0.3f, 0.45f, 0.2f);
            opponentHealth.TakeDamage(500f, decidingHit, Vector3.left, Vector3.zero, true);
            Assert.That(Match.State, Is.EqualTo(MatchController.MatchState.Result));
            Assert.That(_recorder.Clip.TryGetLastImpact(out MatchReplayClip.Impact last), Is.True);
            Assert.That(Vector3.Distance(last.Point, decidingHit), Is.LessThan(1e-4f), "deciding hit point recorded");

            GameplayCounters before = GameplayCounters.Read();
            var menu = Match.GetComponent<MainMenuScreen>();
            Assert.That(menu.enabled, Is.True);
            var liveRenderers = Match.Player.GetComponentsInChildren<Renderer>(true)
                .Concat(Match.Opponent.GetComponentsInChildren<Renderer>(true)).ToArray();
            bool[] liveForceOff = liveRenderers.Select(r => r.forceRenderingOff).ToArray();

            int events = 0;
            System.Action<RobotHealth> onHealth = _ => events++;
            System.Action<RobotHit> onDamage = _ => events++;
            System.Action<MatchController.MatchOutcome> onResult = _ => events++;
            foreach (var health in new[] { Match.Player.GetComponent<RobotHealth>(), opponentHealth })
            {
                health.HealthChanged += onHealth;
                health.Damaged += onDamage;
            }
            Match.ResultDetermined += onResult;
            try
            {
                for (int run = 0; run < 2; run++)
                {
                    Assert.That(_replay.Begin(), Is.True, "replay " + run);
                    yield return null;
                    Assert.That(menu.enabled, Is.False, "menu hidden during replay");
                    Assert.That(liveRenderers.All(r => r.forceRenderingOff), Is.True, "live robots hidden");
                    Assert.That(CameraRig.Target, Is.EqualTo(_replay.PlayerProxy), "camera frames the replay");
                    MainArenaTestScene.AssertSingleCameraAndListener();
                    AssertLookOnly(_replay.ProxyRoot);

                    yield return new WaitForSecondsRealtime(_replay.Duration + 0.3f);
                    Assert.That(_replay.IsFinished, Is.True);
                    Assert.That(_replay.IsImpactMarkerVisible, Is.True, "deciding hit stays marked");
                    Assert.That(Vector3.Distance(_replay.ImpactMarkerPosition, decidingHit), Is.LessThan(0.05f));

                    _replay.Stop();
                    yield return null;
                    Assert.That(GameObject.Find("Match Replay"), Is.Null, "proxies cleaned up");
                    Assert.That(menu.enabled, Is.True, "menu restored");
                    Assert.That(liveRenderers.Select(r => r.forceRenderingOff).ToArray(), Is.EqualTo(liveForceOff), "live renderers restored");
                    Assert.That(CameraRig.Target, Is.EqualTo(Match.Player.transform), "camera back on the live robot");
                }
            }
            finally
            {
                foreach (var health in new[] { Match.Player.GetComponent<RobotHealth>(), opponentHealth })
                {
                    health.HealthChanged -= onHealth;
                    health.Damaged -= onDamage;
                }
                Match.ResultDetermined -= onResult;
            }

            GameplayCounters after = GameplayCounters.Read();
            Assert.That(events, Is.Zero, "no health, damage or result events during replay");
            Assert.That(after.PlayerHealth, Is.EqualTo(before.PlayerHealth));
            Assert.That(after.OpponentHealth, Is.EqualTo(before.OpponentHealth));
            Assert.That(after.MatchCount, Is.EqualTo(before.MatchCount));
            Assert.That(after.ResultCount, Is.EqualTo(before.ResultCount));
            Assert.That(after.State, Is.EqualTo(before.State));
            Assert.That(after.Outcome, Is.EqualTo(before.Outcome));
            Assert.That(after.PlayerPosition, Is.EqualTo(before.PlayerPosition));
            Assert.That(after.OpponentPosition, Is.EqualTo(before.OpponentPosition));
        }

        private static void AssertLookOnly(Transform root)
        {
            Assert.That(root, Is.Not.Null);
            var allowed = new HashSet<System.Type> { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer) };
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                Assert.That(allowed.Contains(component.GetType()), Is.True, component.GetType().Name + " on proxy " + component.name);
        }

        [UnityTest]
        public IEnumerator DetachedPartVanishesAtTheMomentItCameOff()
        {
            yield return Load();
            var visuals = Match.Opponent.GetComponent<RobotDamageVisuals>();
            Assume.That(visuals, Is.Not.Null);
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            yield return null; // recording begins in the first playing frame
            RobotDamageVisuals.Part wheel = visuals.Parts.FirstOrDefault(p => p.Kind == RobotDamageVisuals.PartKind.Wheel);
            Assume.That(wheel, Is.Not.Null, "opponent mesh is partitioned into parts");
            int track = _recorder.Tracks.ToList().IndexOf(wheel.Transform.GetComponent<MeshRenderer>());
            Assert.That(track, Is.GreaterThanOrEqualTo(0), "wheel is a recorded track");
            yield return new WaitForSeconds(0.5f);

            Match.Opponent.GetComponent<RobotHealth>().TakeDamage(40f, wheel.Transform.position, Vector3.up, Vector3.zero, true);
            Assert.That(wheel.Detached, Is.True);
            yield return new WaitForSeconds(0.5f);
            Match.Defeat(false);

            MatchReplayClip clip = _recorder.Clip;
            Assert.That(clip.GetPose(0, track).Visible, Is.True, "attached at the start of the window");
            Assert.That(clip.GetPose(clip.Count - 1, track).Visible, Is.False, "gone in the final frame");

            Assert.That(_replay.Begin(), Is.True);
            _replay.Seek(0f);
            Assert.That(_replay.GetProxy(track).enabled, Is.True);
            _replay.Seek(_replay.Duration);
            Assert.That(_replay.GetProxy(track).enabled, Is.False);
            _replay.Stop();
            Assert.That(wheel.Detached, Is.True, "the live robot is not repaired");
        }

        [UnityTest]
        public IEnumerator VeryShortMatchStillOffersASafeReplay()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            Match.Defeat(false); // Decided before the recorder's first sample.
            Assert.That(_recorder.HasReplay, Is.True);
            Assert.That(_recorder.Clip.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(_replay.Begin(), Is.True);
            yield return null;
            yield return new WaitForSecondsRealtime(_replay.Duration + 0.1f);
            Assert.That(_replay.IsFinished, Is.True);
            _replay.Stop();
        }

        [UnityTest]
        public IEnumerator NewMatchClearsTheReplayAndStopsPlayback()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            yield return new WaitForSeconds(0.5f);
            Match.Defeat(false);
            Assert.That(_replay.Begin(), Is.True);
            var menu = Match.GetComponent<MainMenuScreen>();

            // Something else leaves the result screen while the replay is open.
            Assert.That(Match.RequestCountdown(), Is.True);
            yield return null;
            yield return null;
            Assert.That(_replay.IsActive, Is.False, "replay stops with the result screen");
            Assert.That(menu.enabled, Is.True);
            Assert.That(_recorder.HasReplay, Is.False, "old replay discarded");
            Assert.That(_recorder.Clip.Count, Is.Zero);

            yield return new WaitUntil(() => Match.State == MatchController.MatchState.Playing);
            yield return new WaitForSeconds(0.5f);
            Match.Defeat(true);
            Assert.That(_recorder.RecordedMatch, Is.EqualTo(2));
            Assert.That(_recorder.HasReplay, Is.True);
        }

        [UnityTest]
        public IEnumerator OpenSettingsPausePlayback()
        {
            yield return Load();
            yield return MainArenaTestScene.StartPlaying();
            SeparateRobots();
            yield return new WaitForSeconds(2f);
            Match.Defeat(false);
            Assert.That(_replay.Begin(), Is.True);
            yield return new WaitForSecondsRealtime(0.3f);

            var settings = Match.GetComponent<SettingsPanelController>();
            settings.ToggleSettings();
            float paused = _replay.PlaybackTime;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(_replay.PlaybackTime, Is.EqualTo(paused), "playback holds while settings are open");
            settings.ToggleSettings();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(_replay.PlaybackTime, Is.GreaterThan(paused));
            _replay.Stop();
        }
    }
}
