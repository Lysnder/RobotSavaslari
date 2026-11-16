using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Attaches combat presentation behaviour (camera, last-hit replay, robot inspection, result controls) to the
    /// production arena's existing camera and match object when MainArena loads. Idempotent: reloading the scene or calling Install again never adds a
    /// second component, and other scenes are left untouched.
    /// </summary>
    public static class MainArenaPresentationInstaller
    {
        public const string SceneName = "MainArena";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Install(scene);
        }

        /// <summary>Binds the scene's main camera to its gameplay match. Returns null outside MainArena.</summary>
        public static CombatCameraController Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != SceneName) return null;
            MatchController match = FindGameplayMatch(scene);
            Camera camera = FindMainCamera(scene);
            if (match == null || camera == null) return null;

            var controller = GetOrAdd<CombatCameraController>(camera.gameObject);
            controller.Bind(match);

            var recorder = GetOrAdd<MatchReplayRecorder>(match.gameObject);
            recorder.Bind(match);
            var replay = GetOrAdd<MatchReplayPlayer>(match.gameObject);
            replay.Bind(match, recorder, controller);
            var inspection = GetOrAdd<PostMatchInspection>(match.gameObject);
            inspection.Bind(match, controller, replay);
            GetOrAdd<ResultPresentationScreen>(match.gameObject).Bind(match, replay, inspection);
            return controller;
        }

        private static T GetOrAdd<T>(GameObject host) where T : Component
        {
            var component = host.GetComponent<T>();
            return component != null ? component : host.AddComponent<T>();
        }

        private static MatchController FindGameplayMatch(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MainMenuScreen screen in root.GetComponentsInChildren<MainMenuScreen>(true))
                {
                    var match = screen.GetComponent<MatchController>();
                    if (match != null) return match;
                }
            return null;
        }

        private static Camera FindMainCamera(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(false))
                    if (camera.CompareTag("MainCamera")) return camera;
            return null;
        }
    }
}
