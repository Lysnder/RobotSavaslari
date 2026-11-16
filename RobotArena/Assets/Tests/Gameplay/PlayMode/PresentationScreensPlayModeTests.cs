using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using State = RobotSavaslari.Match.MatchController.MatchState;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>
    /// The on-screen controls of the combat presentation: camera buttons, the result actions, the
    /// replay bar and the inspection panel, driven through their buttons.
    /// </summary>
    public sealed class PresentationScreensPlayModeTests
    {
        private static MatchController Match => GameplayTestScene.Match;
        private CombatCameraController _camera;
        private ResultPresentationScreen _screen;
        private MatchReplayPlayer _replay;
        private PostMatchInspection _inspection;

        [UnityTearDown]
        public IEnumerator Cleanup() => GameplayTestScene.Unload();

        private IEnumerator Load()
        {
            yield return GameplayTestScene.LoadMainArena();
            _camera = Object.FindAnyObjectByType<CombatCameraController>();
            _screen = Match.GetComponent<ResultPresentationScreen>();
            _replay = Match.GetComponent<MatchReplayPlayer>();
            _inspection = Match.GetComponent<PostMatchInspection>();
            Assert.That(_camera != null && _screen != null && _replay != null && _inspection != null, Is.True);
        }

        private static float Width => ImmediateGui.ScaledWidth;
        private static float Height => ImmediateGui.ScaledHeight;

        private IEnumerator FinishAMatch()
        {
            yield return GameplayTestScene.StartPlaying();
            GameplayTestScene.SeparateRobots();
            yield return new WaitForSeconds(0.5f);
            Match.Defeat(false);
            Assert.That(Match.State, Is.EqualTo(State.Result));
        }

        [UnityTest]
        public IEnumerator CameraButtonsToggleModeAndResetTheView()
        {
            yield return Load();
            ImmediateGui.Draw(_camera); // hidden on the menu
            yield return GameplayTestScene.StartPlaying();
            ImmediateGui.ClickScaled(_camera, new Rect(Width - 352f, Height - 56f, 168f, 40f));
            Assert.That(_camera.Mode, Is.EqualTo(CombatCameraController.CameraMode.Duel), "mode button");
            ImmediateGui.ClickScaled(_camera, new Rect(Width - 352f, Height - 56f, 168f, 40f));
            Assert.That(_camera.Mode, Is.EqualTo(CombatCameraController.CameraMode.Follow));
            _camera.ApplyOrbitInput(new Vector2(300f, 0f));
            ImmediateGui.ClickScaled(_camera, new Rect(Width - 176f, Height - 56f, 160f, 40f));
            Assert.That(_camera.Yaw, Is.EqualTo(_camera.DefaultYaw), "reset button");
            _camera.ControlsVisible = false;
            ImmediateGui.Draw(_camera);
            _camera.ControlsVisible = true;
        }

        [UnityTest]
        public IEnumerator ResultButtonsOpenReplayAndInspection()
        {
            yield return Load();
            ImmediateGui.Draw(_screen); // nothing outside the result
            yield return FinishAMatch();
            float y = (Height + 218f) * 0.5f + 12f;
            float x = (Width - 512f) * 0.5f;

            ImmediateGui.ClickScaled(_screen, new Rect(x, y, 250f, 44f));
            Assert.That(_replay.IsActive, Is.True, "Son vurusu izle");
            ImmediateGui.Draw(_screen);

            var bar = new Rect(16f, Height - 160f, 560f, 144f);
            ImmediateGui.ClickScaled(_screen, new Rect(bar.x + 195f, bar.y + 62f, 170f, 36f));
            Assert.That(_replay.IsSlowMotion, Is.True, "slow motion button");
            _replay.Seek(_replay.Duration);
            ImmediateGui.ClickScaled(_screen, new Rect(bar.x + 12f, bar.y + 62f, 170f, 36f));
            Assert.That(_replay.PlaybackTime, Is.Zero, "replay button");
            ImmediateGui.ClickScaled(_screen, new Rect(bar.x + 12f + (bar.width - 24f) * 0.75f - 1f, bar.y + 40f, 2f, 18f));
            Assert.That(_replay.PlaybackTime, Is.GreaterThan(0f), "seek slider");
            ImmediateGui.ClickScaled(_screen, new Rect(bar.x + 378f, bar.y + 62f, 170f, 36f));
            Assert.That(_replay.IsActive, Is.False, "exit button");

            ImmediateGui.ClickScaled(_screen, new Rect(x + 262f, y, 250f, 44f));
            Assert.That(_inspection.IsActive, Is.True, "Robotlari incele");
            Assert.That(_inspection.SelectedRobot, Is.EqualTo(PostMatchInspection.PlayerRobot));
            ImmediateGui.Draw(_screen);

            ImmediateGui.ClickScaled(_screen, new Rect(16f + 12f + 174f, 16f + 38f, 162f, 30f));
            Assert.That(_inspection.SelectedRobot, Is.EqualTo(PostMatchInspection.OpponentRobot), "Rakip button");
            ImmediateGui.ClickScaled(_screen, new Rect(16f + 12f, 16f + 38f, 162f, 30f));
            Assert.That(_inspection.SelectedRobot, Is.EqualTo(PostMatchInspection.PlayerRobot), "Oyuncu button");

            _inspection.SetInterfaceHidden(true);
            ImmediateGui.ClickScaled(_screen, new Rect(16f, 16f, 170f, 30f));
            Assert.That(_inspection.IsInterfaceHidden, Is.False, "show interface button");
            ImmediateGui.Draw(_screen);
            _inspection.Stop();
        }

        [UnityTest]
        public IEnumerator InspectionPanelButtonsHideTheInterfaceAndExit()
        {
            yield return Load();
            yield return FinishAMatch();
            Assert.That(_inspection.Begin(PostMatchInspection.OpponentRobot), Is.True);
            RobotInspectionReport report = _inspection.Report;
            int rows = report.HasPartData ? report.Parts.Count : 1;
            float panelHeight = Mathf.Min(Height - 32f, 214f + rows * 22f);
            // Buttons sit below the part list and the counters; locate them from the panel content.
            float buttonsY = 16f + 8f + 30f + 38f + 22f + 26f + (report.HasPartData ? rows * 22f : 22f) + 6f + 28f;
            Assume.That(buttonsY + 32f, Is.LessThanOrEqualTo(16f + panelHeight + 1f));
            ImmediateGui.ClickScaled(_screen, new Rect(16f + 12f, buttonsY, 162f, 32f));
            Assert.That(_inspection.IsInterfaceHidden, Is.True, "hide interface button");
            _inspection.SetInterfaceHidden(false);
            ImmediateGui.ClickScaled(_screen, new Rect(16f + 12f + 174f, buttonsY, 162f, 32f));
            Assert.That(_inspection.IsActive, Is.False, "exit button");
        }

        [UnityTest]
        public IEnumerator ResultScreenStaysHiddenWhileSettingsAreOpen()
        {
            yield return Load();
            yield return FinishAMatch();
            var settings = Match.GetComponent<UI.SettingsPanelController>();
            settings.ToggleSettings();
            ImmediateGui.Draw(_screen);
            settings.ToggleSettings();
            Assert.That(_replay.IsActive || _inspection.IsActive, Is.False, "drawing with settings open does nothing");
        }

        [UnityTest]
        public IEnumerator InstallerIgnoresOtherScenesAndRepeatedCalls()
        {
            yield return Load();
            Scene arena = SceneManager.GetSceneByName(GameplayTestScene.MainArena);
            CombatCameraController first = MainArenaPresentationInstaller.Install(arena);
            CombatCameraController second = MainArenaPresentationInstaller.Install(arena);
            Assert.That(second, Is.SameAs(first));
            Assert.That(Object.FindObjectsByType<CombatCameraController>().Length, Is.EqualTo(1));
            Assert.That(Match.GetComponents<MatchReplayRecorder>().Length, Is.EqualTo(1));
            Assert.That(Match.GetComponents<PostMatchInspection>().Length, Is.EqualTo(1));

            Scene other = SceneManager.CreateScene("Not The Arena");
            Assert.That(MainArenaPresentationInstaller.Install(other), Is.Null);
            Assert.That(MainArenaPresentationInstaller.Install(default), Is.Null);
            yield return SceneManager.UnloadSceneAsync(other);
        }
    }
}
