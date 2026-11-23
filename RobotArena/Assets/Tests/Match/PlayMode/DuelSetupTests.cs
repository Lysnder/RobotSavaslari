using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotSavaslari.Match.Tests
{
    public sealed class DuelSetupTests
    {
        private string previous, folder;
        private bool hadPreference;

        [SetUp]
        public void SetUp()
        {
            hadPreference = PlayerPrefs.HasKey(DuelSetup.PreferenceKey);
            previous = PlayerPrefs.GetString(DuelSetup.PreferenceKey);
            PlayerPrefs.DeleteKey(DuelSetup.PreferenceKey);
            folder = Path.Combine(Path.GetTempPath(), "DuelSetup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadPreference) PlayerPrefs.SetString(DuelSetup.PreferenceKey, previous);
            else PlayerPrefs.DeleteKey(DuelSetup.PreferenceKey);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            Directory.Delete(folder, true);
        }

        [TestCase("{broken")]
        [TestCase("{}")]
        [TestCase("{\"version\":99,\"arenaId\":\"Open\"}")]
        [TestCase("{\"version\":1,\"arenaId\":\"Open\"}")]
        public void InvalidPreferencesAreRejected(string json) => Assert.That(DuelSetup.Read(json), Is.Null);

        [UnityTest]
        public IEnumerator PresetsUseExistingCatalogAndSnapshotSurvivesRematchAndReload()
        {
            yield return SceneManager.LoadSceneAsync("MainArena");
            yield return null;
            var match = UnityEngine.Object.FindAnyObjectByType<MatchController>();
            match.HistoryStore = new MatchHistoryStore(Path.Combine(folder, "history.json"));
            match.SetManualMode(true);
            var menu = match.GetComponent<MainMenuScreen>();
            var panel = match.GetComponent<OpponentSelectionPanel>();
            var environment = UnityEngine.Object.FindAnyObjectByType<ArenaEnvironment>();
            foreach (var preset in new[] { DuelSetup.Quick(), DuelSetup.Series(), DuelSetup.Tactical() })
            {
                Assert.That(menu.SelectPreset(preset), Is.True);
                Assert.That(panel.Personality, Is.EqualTo(preset.personality));
                Assert.That(panel.Difficulty, Is.EqualTo(preset.difficulty));
                Assert.That(match.Rules.mode, Is.EqualTo(preset.mode));
                Assert.That(match.Rules.timeLimitSeconds, Is.EqualTo(preset.seconds));
                Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Ready));
            }
            var absent = DuelSetup.Quick();
            absent.arenaId = "missing";
            Assert.That(menu.SelectPreset(absent), Is.False);
            Assert.That(panel.Difficulty, Is.EqualTo(BotDifficulty.Hard));
            menu.StartLobbyMatch();
            string first = match.EncounterId;
            menu.StartLobbyMatch();
            Assert.That(match.EncounterId, Is.EqualTo(first));
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
            Assert.That(environment.Layout.name, Is.EqualTo("Tactical"));
            Assert.That(DuelSetup.Tactical().SameAs(DuelSetup.Read(PlayerPrefs.GetString(DuelSetup.PreferenceKey))), Is.True);
            Time.timeScale = 4f;
            yield return new WaitForSeconds(3.15f);
            match.Opponent.GetComponent<RobotHealth>().TakeDamage(1000);
            Assert.That(match.EncounterComplete, Is.True);
            menu.ContinueResult();
            Assert.That(match.EncounterId, Is.Not.EqualTo(first));
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
            Assert.That(environment.Layout.name, Is.EqualTo("Tactical"));
            Assert.That(panel.Personality, Is.EqualTo(BotPersonality.Counter));
            yield return SceneManager.LoadSceneAsync("MainArena");
            yield return null;
            menu = UnityEngine.Object.FindAnyObjectByType<MainMenuScreen>();
            menu.StartLobbyMatch();
            match = menu.GetComponent<MatchController>();
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
            Assert.That(match.Rules.timeLimitSeconds, Is.EqualTo(120));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<ArenaEnvironment>().Layout.name, Is.EqualTo("Tactical"));
        }
    }
}
