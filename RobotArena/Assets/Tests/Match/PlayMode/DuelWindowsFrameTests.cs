using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.Presentation;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotSavaslari.Match.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsPlayer)]
    public sealed class DuelWindowsFrameTests
    {
        private string savedPreference, temporaryStore, outputRoot;
        private bool hadPreference;
        private int savedWidth, savedHeight;
        private FullScreenMode savedMode;

        [SetUp]
        public void SetUp()
        {
            savedMode = Screen.fullScreenMode;
            savedWidth = Screen.width;
            savedHeight = Screen.height;
            hadPreference = PlayerPrefs.HasKey(DuelSetup.PreferenceKey);
            savedPreference = PlayerPrefs.GetString(DuelSetup.PreferenceKey);
            temporaryStore = Path.Combine(Application.temporaryCachePath, "DuelMatch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryStore);
            outputRoot = Environment.GetEnvironmentVariable("DUEL_WINDOWS_FRAMES");
            if (string.IsNullOrWhiteSpace(outputRoot))
                outputRoot = Path.Combine(Application.temporaryCachePath, "DuelWindowsFrames");
        }

        [TearDown]
        public void TearDown()
        {
            if (hadPreference) PlayerPrefs.SetString(DuelSetup.PreferenceKey, savedPreference);
            else PlayerPrefs.DeleteKey(DuelSetup.PreferenceKey);
            PlayerPrefs.Save();
            Screen.SetResolution(savedWidth, savedHeight, savedMode);
            if (Directory.Exists(temporaryStore)) Directory.Delete(temporaryStore, true);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TacticalReadyResultAndRematchAt720And1080()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            foreach (int height in new[] { 720, 1080 })
            {
                int width = height * 16 / 9;
                yield return SceneManager.LoadSceneAsync("MainArena", LoadSceneMode.Single);
                var match = UnityEngine.Object.FindAnyObjectByType<MatchController>();
                yield return new WaitUntil(() => match != null && match.IsInitialized);
                string store = Path.Combine(temporaryStore, height.ToString());
                Directory.CreateDirectory(store);
                match.HistoryStore = new MatchHistoryStore(Path.Combine(store, "match-history.json"));
                Assert.That(MainArenaPresentationInstaller.UseArchiveStore(match,
                    new MatchReplayArchiveStore(Path.Combine(store, "replays"))), Is.True);

                Screen.SetResolution(width, height, FullScreenMode.Windowed);
                float deadline = Time.realtimeSinceStartup + 8f;
                while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(Screen.width, Is.EqualTo(width));
                Assert.That(Screen.height, Is.EqualTo(height));
                var menu = match.GetComponent<MainMenuScreen>();
                var arena = UnityEngine.Object.FindAnyObjectByType<ArenaEnvironment>();
                var opponent = match.GetComponent<OpponentSelectionPanel>();
                var setup = DuelSetup.Tactical();
                Assert.That(menu.SelectPreset(setup), Is.True);
                Assert.That(menu.IsShowingReadyMenu, Is.True);
                string folder = Path.Combine(outputRoot, height.ToString());
                Directory.CreateDirectory(folder);
                yield return Capture(folder, "01-ready", width, height);

                menu.StartLobbyMatch();
                Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
                yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
                Assert.That(arena.Layout.name, Is.EqualTo(setup.arenaId));
                Assert.That(opponent.Personality, Is.EqualTo(setup.personality));
                string encounter = match.EncounterId;
                int matchNumber = match.MatchCount;
                var target = match.Opponent.GetComponent<RobotHealth>();
                var player = match.Player.GetComponent<RobotHealth>();
                target.TakeDamage(target.CurrentHealth, target.transform.position + Vector3.up * .45f,
                    Vector3.up, Vector3.zero, true, player);
                Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Result));
                Assert.That(match.Outcome, Is.EqualTo(MatchController.MatchOutcome.PlayerVictory));
                Assert.That(match.EncounterComplete, Is.True);
                yield return Capture(folder, "02-result", width, height);

                menu.ContinueResult();
                Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
                Assert.That(match.EncounterId, Is.Not.EqualTo(encounter));
                Assert.That(match.PlayerWins, Is.Zero);
                yield return Capture(folder, "03-rematch-countdown", width, height);
                yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
                Assert.That(match.MatchCount, Is.EqualTo(matchNumber + 1));
                Assert.That(match.Rules.timeLimitSeconds, Is.EqualTo(setup.seconds));
                Assert.That(arena.Layout.name, Is.EqualTo(setup.arenaId));
                Assert.That(opponent.Difficulty, Is.EqualTo(setup.difficulty));
                yield return Capture(folder, "04-rematch-playing", width, height);
                // Close recording in our temporary store before unloading its match.
                match.Opponent.GetComponent<RobotHealth>().TakeDamage(target.MaxHealth);
                yield return null;
            }
        }

        private static IEnumerator Capture(string folder, string phase, int width, int height)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.That(frame, Is.Not.Null);
            try
            {
                Assert.That(frame.width, Is.EqualTo(width));
                Assert.That(frame.height, Is.EqualTo(height));
                byte[] bytes = frame.EncodeToPNG();
                string path = Path.Combine(folder, phase + ".png");
                File.WriteAllBytes(path, bytes);
                Debug.Log($"DUEL_FRAME {phase} {width}x{height} {SystemInfo.graphicsDeviceType} {bytes.Length} bytes");
            }
            finally { UnityEngine.Object.Destroy(frame); }
        }
    }
}
