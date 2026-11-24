using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using State = RobotSavaslari.Match.MatchController.MatchState;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>Arena menu, HUD and settings panel: keyboard shortcuts, buttons and every screen state.</summary>
    public sealed class MenuAndSettingsPlayModeTests : InputTestFixture
    {
        [UnityTearDown]
        public IEnumerator Cleanup() => GameplayTestScene.Unload();

        private Keyboard _keyboard;
        private GameplayTestScene.SettingsSnapshot _settings;
        private static MatchController Match => GameplayTestScene.Match;
        private MainMenuScreen Menu => Match.GetComponent<MainMenuScreen>();
        private SettingsPanelController Settings => Match.GetComponent<SettingsPanelController>();

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _settings = new GameplayTestScene.SettingsSnapshot();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            _settings.Restore();
            base.TearDown();
        }

        /// <summary>Queues a key state so the next frame's input update sees a fresh press before Update runs.</summary>
        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        private static Rect Scaled(float x, float y, float width, float height) => new Rect(x, y, width, height);

        [UnityTest]
        public IEnumerator EnterStartsAndRStartsTheNextMatch()
        {
            yield return GameplayTestScene.LoadMainArena();
            yield return Tap(Key.Enter);
            Assert.That(Match.State, Is.EqualTo(State.Countdown), "Enter starts from the menu");
            yield return new WaitUntil(() => Match.State == State.Playing);
            yield return Tap(Key.R);
            Assert.That(Match.State, Is.EqualTo(State.Playing), "R does nothing while playing");
            Match.Defeat(false);
            yield return Tap(Key.R);
            Assert.That(Match.State, Is.EqualTo(State.Countdown), "R starts a new match from the result");
        }

        [UnityTest]
        public IEnumerator EscapeTogglesSettingsAndPausesTheGame()
        {
            yield return GameplayTestScene.LoadMainArena();
            yield return Tap(Key.Escape);
            Assert.That(Settings.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            yield return Tap(Key.Enter);
            Assert.That(Match.State, Is.EqualTo(State.Ready), "menu ignores Enter while settings are open");
            yield return Tap(Key.Escape);
            Assert.That(Settings.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator MenuButtonsStartAndOpenSettings()
        {
            yield return GameplayTestScene.LoadMainArena();
            float scale = MainMenuScreen.ReadyScale(Screen.width, Screen.height);
            ImmediateGui.Click(Menu, new Vector2(120f, Screen.height / scale - 82f) * scale);
            Assert.That(Settings.IsOpen, Is.True, "Ayarlar button");
            ImmediateGui.Draw(Menu);
            Settings.ToggleSettings();

            ImmediateGui.Click(Menu, new Vector2(120f, 218f) * scale);
            Assert.That(Match.State, Is.EqualTo(State.Countdown), "Oyna button");
            ImmediateGui.Draw(Menu);
            float width = ImmediateGui.ScaledWidth;
            ImmediateGui.ClickScaled(Menu, Scaled(width - 174f, 16f, 158f, 36f));
            Assert.That(Settings.IsOpen, Is.True, "settings button during the match");
            Settings.ToggleSettings();

            yield return new WaitUntil(() => Match.State == State.Playing);
            ImmediateGui.Draw(Menu);
            Match.Defeat(true);
            ImmediateGui.Draw(Menu);
            var panel = new Rect((width - 560f) * 0.5f, (ImmediateGui.ScaledHeight - 218f) * 0.5f, 560f, 218f);
            ImmediateGui.ClickScaled(Menu, Scaled(panel.x + 286f, panel.y + 154f, 262f, 44f));
            Assert.That(Match.State, Is.EqualTo(State.Ready), "Ana menu button");

            Match.RequestCountdown();
            yield return new WaitUntil(() => Match.State == State.Playing);
            Match.Defeat(false);
            ImmediateGui.ClickScaled(Menu, Scaled(panel.x + 12f, panel.y + 154f, 262f, 44f));
            Assert.That(Match.State, Is.EqualTo(State.Countdown), "Yeni mac button");

            yield return new WaitUntil(() => Match.State == State.Playing);
            Match.Defeat(false);
            Match.RestartMatch();
            ImmediateGui.Draw(Menu);
            Assert.That(Match.IsRestarting, Is.True, "nothing drawn while restarting changes the state");
        }

        [UnityTest]
        public IEnumerator SettingsPanelControlsChangeAndSaveValues()
        {
            yield return GameplayTestScene.LoadMainArena();
            Settings.ToggleSettings(); // opening reloads the saved values
            GameSettings.ResetCameraToDefaults();
            GameSettings.QualityLevel = Mathf.Min(1, QualitySettings.names.Length - 1);
            ImmediateGui.Draw(Settings);

            void ClickPanel(float x, float y)
            {
                float scale = MainMenuScreen.ReadyScale(Screen.width, Screen.height);
                Vector2 origin = new Vector2(Screen.width / scale - 676f, Screen.height / scale - 538f) * .5f;
                ImmediateGui.Click(Settings, (origin + new Vector2(x, y)) * scale);
            }
            bool fullscreen = GameSettings.Fullscreen;
            ClickPanel(fullscreen ? 535 : 363, 254);
            Assert.That(GameSettings.Fullscreen, Is.Not.EqualTo(fullscreen), "fullscreen toggle");

            int quality = GameSettings.QualityLevel;
            if (quality > 0)
            {
                ClickPanel(303, 309);
                Assert.That(GameSettings.QualityLevel, Is.EqualTo(quality - 1), "lower quality");
            }
            if (GameSettings.QualityLevel < QualitySettings.names.Length - 1)
            {
                int before = GameSettings.QualityLevel;
                ClickPanel(625, 309);
                Assert.That(GameSettings.QualityLevel, Is.EqualTo(before + 1), "raise quality");
            }

            ClickPanel(287, 128); // Kamera tab
            ClickPanel(366, 303);
            Assert.That(GameSettings.CameraInvertY, Is.True, "invert toggle");
            ClickPanel(149, 500);
            Assert.That(GameSettings.CameraInvertY, Is.False, "camera defaults button");

            GameSettings.MasterVolume = 0.2f;
            ClickPanel(122, 128); // Ses ve görüntü tab
            ClickPanel(149, 500);
            Assert.That(GameSettings.MasterVolume, Is.EqualTo(1f), "all defaults button");

            GameSettings.MasterVolume = 0.4f;
            ClickPanel(552, 500);
            Assert.That(Settings.IsOpen, Is.False, "save and close");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(PlayerPrefs.GetFloat("Settings.MasterVolume"), Is.EqualTo(0.4f), "saved on close");

            ImmediateGui.Draw(Settings); // closed: draws nothing
        }

        [UnityTest]
        public IEnumerator DisablingTheOpenPanelClosesItAndRestoresTime()
        {
            yield return GameplayTestScene.LoadMainArena();
            Settings.ToggleSettings();
            Assert.That(Time.timeScale, Is.Zero);
            Settings.enabled = false;
            Assert.That(Settings.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Settings.enabled = true;
        }
    }
}
