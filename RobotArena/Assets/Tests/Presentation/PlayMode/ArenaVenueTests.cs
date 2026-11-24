using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Presentation.Tests
{
    public sealed class ArenaVenueTests
    {
        [UnityTearDown] public IEnumerator Cleanup() => MainArenaTestScene.Unload();

        [UnityTest]
        public IEnumerator AllLayoutsKeepTheirGeometryAndUseTheSameTwoRobots()
        {
            yield return MainArenaTestScene.Load();
            var match = MainArenaTestScene.Match;
            var arena = Object.FindFirstObjectByType<ArenaEnvironment>();
            var menu = match.GetComponent<MainMenuScreen>();
            var venue = match.GetComponent<ArenaVenue>();
            var originalPlayer = match.Player;
            var originalOpponent = match.Opponent;
            Assert.That(arena.Layouts.Count, Is.EqualTo(4));
            foreach (var layout in arena.Layouts.ToArray())
            {
                var setup = DuelSetup.Quick();
                setup.arenaId = layout.name;
                Assert.That(menu.SelectPreset(setup), Is.True, layout.name);
                menu.StartLobbyMatch();
                yield return null;
                yield return null;
                Assert.That(venue.IsPresenting, Is.True, layout.name);
                Assert.That(arena.Layout, Is.SameAs(layout));
                var sceneModel = Object.FindFirstObjectByType<ArenaVenueModel>();
                Assert.That(sceneModel.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(sceneModel.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(sceneModel.GetComponentsInChildren<RobotHealth>(true), Is.Empty);
                Assert.That(sceneModel.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(1));
                var colliders = arena.Contents.GetComponentsInChildren<Collider>(true);
                venue.enabled = false;
                Assert.That(venue.IsPresenting, Is.False);
                venue.enabled = true;
                yield return null;
                yield return null;
                CollectionAssert.AreEquivalent(colliders, arena.Contents.GetComponentsInChildren<Collider>(true));
                Assert.That(match.Player, Is.SameAs(originalPlayer));
                Assert.That(match.Opponent, Is.SameAs(originalOpponent));
                Assert.That(Object.FindObjectsByType<ArenaVenueModel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
                match.GetComponent<SettingsPanelController>().ToggleSettings();
                yield return null;
                yield return null;
                Assert.That(venue.IsPresenting, Is.False);
                match.GetComponent<SettingsPanelController>().ToggleSettings();
                match.Prepare();
                yield return null;
                yield return null;
                Assert.That(venue.IsPresenting, Is.False);
            }
        }
    }
}
