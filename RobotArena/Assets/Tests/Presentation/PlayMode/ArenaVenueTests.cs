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
        private string temporaryStore;
        private bool hadPreference;
        private string preferences;

        [SetUp]
        public void PreservePreferences()
        {
            hadPreference = PlayerPrefs.HasKey(DuelSetup.PreferenceKey);
            preferences = PlayerPrefs.GetString(DuelSetup.PreferenceKey);
            temporaryStore = Path.Combine(Application.temporaryCachePath, "VenueLayout-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryStore);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return MainArenaTestScene.Unload();
            if (hadPreference) PlayerPrefs.SetString(DuelSetup.PreferenceKey, preferences);
            else PlayerPrefs.DeleteKey(DuelSetup.PreferenceKey);
            PlayerPrefs.Save();
            if (Directory.Exists(temporaryStore)) Directory.Delete(temporaryStore, true);
        }

        [UnityTest]
        public IEnumerator AllLayoutsKeepTheirGeometryAndUseTheSameTwoRobots()
        {
            yield return MainArenaTestScene.Load();
            var match = MainArenaTestScene.Match;
            match.HistoryStore = new MatchHistoryStore(Path.Combine(temporaryStore, "history.json"));
            MainArenaPresentationInstaller.UseArchiveStore(match, new MatchReplayArchiveStore(Path.Combine(temporaryStore, "replays")));
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
                var panels = sceneModel.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Clear safety panels").ToArray();
                Assert.That(panels.Length, Is.EqualTo(4), layout.name);
                Assert.That(panels.All(r => r.sharedMaterial.renderQueue == 3000), Is.True);
                Assert.That(sceneModel.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.name == "Team launch markings"), Is.EqualTo(1));
                var colliders = arena.Contents.GetComponentsInChildren<Collider>(true);
                var safety = GameObject.Find("North Safety Barrier").GetComponent<Collider>();
                var oldWire = GameObject.Find("North Mesh Wire").GetComponent<MeshRenderer>();
                var floor = arena.transform.Find("Floor 0-0").GetComponentInChildren<MeshRenderer>();
                var finish = floor.sharedMaterial;
                Assert.That(finish.name, Is.EqualTo("Work-worn ring steel"));
                Assert.That(oldWire.forceRenderingOff, Is.True);
                Assert.That(safety.enabled, Is.True);
                venue.enabled = false;
                Assert.That(venue.IsPresenting, Is.False);
                Assert.That(oldWire.forceRenderingOff, Is.False);
                Assert.That(safety.enabled, Is.True);
                Assert.That(floor.sharedMaterial, Is.Not.SameAs(finish));
                venue.enabled = true;
                yield return null;
                yield return null;
                CollectionAssert.AreEquivalent(colliders, arena.Contents.GetComponentsInChildren<Collider>(true));
                Assert.That(floor.sharedMaterial, Is.SameAs(finish));
                Assert.That(match.Player, Is.SameAs(originalPlayer));
                Assert.That(match.Opponent, Is.SameAs(originalOpponent));
                Assert.That(Object.FindObjectsByType<ArenaVenueModel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
                match.GetComponent<SettingsPanelController>().ToggleSettings();
                yield return null;
                yield return null;
                Assert.That(venue.IsPresenting, Is.False);
                match.GetComponent<SettingsPanelController>().ToggleSettings();
                yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
                var health = match.Opponent.GetComponent<RobotHealth>();
                health.TakeDamage(health.MaxHealth);
                Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Result));
                var inspection = match.GetComponent<PostMatchInspection>();
                Assert.That(inspection.Begin(0), Is.True);
                yield return null;
                yield return null;
                Assert.That(venue.IsPresenting, Is.False);
                inspection.Stop();
                match.Prepare();
                yield return null;
                yield return null;
                Assert.That(venue.IsPresenting, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ChallengeDialogPauseAndReplaySilenceOnlyTheirOwnPhases()
        {
            yield return MainArenaTestScene.Load();
            var match = MainArenaTestScene.Match;
            match.HistoryStore = new MatchHistoryStore(Path.Combine(temporaryStore, "history.json"));
            MainArenaPresentationInstaller.UseArchiveStore(match, new MatchReplayArchiveStore(Path.Combine(temporaryStore, "replays")));
            var challenge = match.GetComponent<ChallengeSessionController>();
            challenge.UseSavePath(Path.Combine(temporaryStore, "challenge.json"));
            var venue = match.GetComponent<ArenaVenue>();
            Assert.That(challenge.Open(), Is.True);
            yield return null;
            Assert.That(venue.IsPresenting, Is.False);
            Assert.That(challenge.Begin(false), Is.True);
            yield return null;
            yield return null;
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
            Assert.That(challenge.InChallenge && !challenge.DialogOpen && venue.IsPresenting, Is.True);
            yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
            yield return new WaitForSeconds(.32f);
            Assert.That(venue.IsPresenting, Is.True);
            var settings = match.GetComponent<SettingsPanelController>();
            settings.ToggleSettings();
            yield return null;
            yield return null;
            Assert.That(venue.IsPresenting, Is.False);
            settings.ToggleSettings();
            yield return null;
            yield return null;
            Assert.That(venue.IsPresenting, Is.True);
            var bot = match.Opponent.GetComponent<RobotHealth>();
            bot.TakeDamage(bot.MaxHealth);
            yield return null;
            yield return null;
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Result));
            Assert.That(venue.IsPresenting && venue.BlueSignal > venue.OrangeSignal, Is.True);
            Assert.That(challenge.Open(), Is.True);
            yield return null;
            yield return null;
            Assert.That(venue.IsPresenting, Is.False);
            challenge.Close();
            yield return null;
            yield return null;
            Assert.That(venue.IsPresenting, Is.True);
            var replay = match.GetComponent<MatchReplayPlayer>();
            Assert.That(replay.Begin(), Is.True);
            yield return null;
            yield return null;
            Assert.That(venue.IsPresenting, Is.False);
            replay.Stop();
            yield return null;
            yield return null;
            Assert.That(venue.IsPresenting, Is.True);
        }

        [UnityTest]
        public IEnumerator FloorPaintHasNoDepthAndNearbyProtectiveFramesClearALowCamera()
        {
            yield return MainArenaTestScene.Load();
            var match = MainArenaTestScene.Match;
            match.GetComponent<ArenaVenue>().enabled = false;
            var model = Object.FindFirstObjectByType<ArenaVenueModel>(FindObjectsInactive.Include);
            model.gameObject.SetActive(true);
            var arena = Object.FindFirstObjectByType<ArenaEnvironment>();
            var bounds = arena.GetComponent<ArenaBounds>();
            var camera = Camera.main;
            var center = new Vector3(bounds.Center.x, bounds.SurfaceHeight, bounds.Center.y);
            camera.transform.position = center + Vector3.up * 12;
            int normalGroups = 0;
            foreach (bool low in new[] { false, true })
            {
                model.Build(arena.Layout, low);
                model.Present(1f, 1f, 1f, 1f, 1f);
                var renderers = model.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers, Is.Not.Empty);
                foreach (var renderer in renderers)
                {
                    var box = renderer.bounds;
                    bool outside = box.max.x < center.x - bounds.HalfExtents.x || box.min.x > center.x + bounds.HalfExtents.x ||
                        box.max.z < center.z - bounds.HalfExtents.y || box.min.z > center.z + bounds.HalfExtents.y;
                    if (!outside && renderer.transform.parent.name != "Protective frame")
                    {
                        Assert.That(box.min.y, Is.InRange(center.y, center.y + .025f), renderer.name);
                        Assert.That(box.size.y, Is.LessThan(.001f), renderer.name);
                    }
                    Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
                }
                if (low) Assert.That(renderers.Length, Is.LessThan(normalGroups));
                else normalGroups = renderers.Length;
                var first = renderers[0].GetComponent<MeshFilter>().sharedMesh;
                model.Build(arena.Layout, low);
                Assert.That(renderers[0].GetComponent<MeshFilter>().sharedMesh, Is.SameAs(first));
                TestContext.WriteLine($"VENUE_GROUPS low={low} renderers={renderers.Length} triangles={renderers.Sum(r => (long)r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3)}");
                camera.transform.position = center + new Vector3(0, .5f, bounds.HalfExtents.y + 2f);
                var frames = renderers.Where(r => r.name == "Clear safety panels").ToArray();
                var nearest = frames.OrderBy(r => Vector3.Distance(camera.transform.position, r.bounds.center)).First();
                model.Present(2f, 0, 0, 0, 0);
                Assert.That(model.GetComponentsInChildren<MeshRenderer>().Length, Is.LessThan(renderers.Length));
                Assert.That(nearest.gameObject.activeInHierarchy, Is.False);
                Assert.That(frames.Count(r => r.gameObject.activeInHierarchy), Is.EqualTo(3));
                camera.transform.position = center + Vector3.up * 12;
                model.Present(3f, 0, 0, 0, 0);
                Assert.That(model.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(renderers.Length));
            }
        }
    }
}
