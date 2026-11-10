using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RobotSavaslari.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Tests
{
    public sealed class ArenaAssetReferenceTests
    {
        private const string MainScene = "Assets/Scenes/Gameplay/MainArena.unity";

        public static IEnumerable<string> ScenePaths()
        {
            return AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path);
        }

        [TestCaseSource(nameof(ScenePaths))]
        public void MovedScenesKeepScriptsAndAssetReferences(string path)
        {
            Scene previousActive = SceneManager.GetActiveScene();
            SceneAsset previousStartScene = EditorSceneManager.playModeStartScene;
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedForTest) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),
                            Is.Zero, path + " has a missing script on " + transform.name);
                    }
                }
                foreach (System.Text.RegularExpressions.Match reference in Regex.Matches(File.ReadAllText(path), @"guid: ([a-fA-F0-9]{32})"))
                {
                    string guid = reference.Groups[1].Value;
                    if (guid.StartsWith("0000000000000000")) continue; // Unity's built-in resources.
                    Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Not.Empty, path + " has an unresolved GUID " + guid);
                }
                if (path == MainScene) AssertGameplayConnections(scene);
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.playModeStartScene = previousStartScene;
            }
        }

        [Test]
        public void AssetMovesPreserveUniqueGuids()
        {
            var pathsByGuid = new Dictionary<string, string>();
            foreach (string path in Directory.GetFiles("Assets", "*.meta", SearchOption.AllDirectories))
            {
                var guidLine = File.ReadLines(path).FirstOrDefault(line => line.StartsWith("guid: "));
                Assert.That(guidLine, Is.Not.Null, "Metadata has no GUID " + path);
                string guid = guidLine.Substring(6).Trim();
                Assert.That(pathsByGuid.TryGetValue(guid, out var original), Is.False,
                    "Duplicate GUID in " + path + " and " + original);
                pathsByGuid.Add(guid, path);
            }
        }

        [Test]
        public void MainArenaIsTheOnlyEnabledBuildScene()
        {
            Assert.That(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path),
                Is.EqualTo(new[] { MainScene }));
        }

        private static T[] Components<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static void AssertGameplayConnections(Scene scene)
        {
            var matches = Components<MatchController>(scene);
            Assert.That(matches, Has.Length.EqualTo(1));
            Assert.That(Components<ArenaMatchController>(scene), Is.Empty);
            Assert.That(Components<RobotPrototypeController>(scene), Has.Length.EqualTo(2));
            Assert.That(Components<Camera>(scene), Has.Length.EqualTo(1));
            Assert.That(Components<AudioListener>(scene), Has.Length.EqualTo(1));
            var match = matches[0];
            Assert.That(match.Player, Is.Not.Null);
            Assert.That(match.Opponent, Is.Not.Null.And.Not.EqualTo(match.Player));
            Assert.That(match.Player.enabled && match.Opponent.enabled, Is.True);
            Assert.That(match.ManualNotificationsOnly, Is.False);
            using (var serialized = new SerializedObject(match))
            {
                Assert.That(serialized.FindProperty("playerSpawn").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("opponentSpawn").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("gameplayInterface").boolValue, Is.True);
            }
        }
    }
}
