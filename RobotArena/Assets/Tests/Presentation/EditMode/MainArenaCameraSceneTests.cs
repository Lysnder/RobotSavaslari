using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Presentation.Tests
{
    /// <summary>The saved arena must keep a single camera and listener; presentation is attached at runtime.</summary>
    public sealed class MainArenaCameraSceneTests
    {
        private const string MainScene = "Assets/Scenes/Gameplay/MainArena.unity";

        [Test]
        public void SavedArenaHasOneCameraOneListenerAndNoSerializedCameraController()
        {
            SceneAsset previousStartScene = EditorSceneManager.playModeStartScene;
            Scene scene = SceneManager.GetSceneByPath(MainScene);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedForTest) scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);
                int cameras = 0, listeners = 0, controllers = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    cameras += root.GetComponentsInChildren<Camera>(true).Length;
                    listeners += root.GetComponentsInChildren<AudioListener>(true).Length;
                    controllers += root.GetComponentsInChildren<CombatCameraController>(true).Length;
                }
                Assert.That(cameras, Is.EqualTo(1), "camera count");
                Assert.That(listeners, Is.EqualTo(1), "audio listener count");
                Assert.That(controllers, Is.Zero, "the controller is attached at runtime, not saved in the scene");
            }
            finally
            {
                if (openedForTest && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                EditorSceneManager.playModeStartScene = previousStartScene;
            }
        }
    }
}
