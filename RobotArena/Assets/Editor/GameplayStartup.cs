using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Editor
{
    [InitializeOnLoad]
    public static class GameplayStartup
    {
        public const string ScenePath = "Assets/Scenes/Gameplay/MainArena.unity";
        private const string InitialSceneChecked = "RobotSavaslari.InitialPlaySceneChecked";

        static GameplayStartup()
        {
            EditorApplication.update += SetInitialPlayScene;
            EditorSceneManager.sceneOpened += ClearPlayOverride;
            EditorSceneManager.newSceneCreated += OnNewScene;
            EditorSceneManager.sceneDirtied += OnSceneDirtied;
        }

        private static void SetInitialPlayScene()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
                SessionState.GetBool(InitialSceneChecked, false))
            {
                EditorApplication.update -= SetInitialPlayScene;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var mainScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (mainScene == null) return;

            // The first graphical launch can have no scene at all until the user presses Play.
            Scene scene = SceneManager.GetActiveScene();
            bool freshScene = SceneManager.sceneCount == 0 ||
                (SceneManager.sceneCount == 1 && scene.IsValid() && string.IsNullOrEmpty(scene.path) && !scene.isDirty);
            if (EditorSceneManager.playModeStartScene == null && freshScene)
                EditorSceneManager.playModeStartScene = mainScene;

            // A later script recompile must not redirect a deliberately created unnamed scene.
            SessionState.SetBool(InitialSceneChecked, true);
            EditorApplication.update -= SetInitialPlayScene;
        }

        private static void ClearPlayOverride(Scene scene, OpenSceneMode mode)
        {
            if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == ScenePath)
                EditorSceneManager.playModeStartScene = null;
        }

        private static void OnNewScene(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == ScenePath)
                EditorSceneManager.playModeStartScene = null;
        }

        private static void OnSceneDirtied(Scene scene)
        {
            if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == ScenePath)
                EditorSceneManager.playModeStartScene = null;
        }
    }
}
