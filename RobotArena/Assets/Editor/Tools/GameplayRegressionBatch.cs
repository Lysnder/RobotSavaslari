using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Editor
{
    [InitializeOnLoad]
    public static class GameplayRegressionBatch
    {
        private const string RunKey = "RobotSavaslari.GameplayRegressionBatch";
        private static double readyAt;
        private static bool hadErrors;

        static GameplayRegressionBatch()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(RunKey, "")))
                Register();
        }

        public static void RunBotScenarios()
        {
            Begin("scenarios", "Assets/Scenes/Tests/AI/BotScenarios.unity");
        }

        public static void RunCombatRegressions()
        {
            Begin("combat", "Assets/Scenes/Tests/CombatArena.unity");
        }

        public static void RunDefaultStartupSmoke()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-arena-smoke") < 0)
                throw new InvalidOperationException("Run startup verification in a graphical Editor with -arena-smoke.");
            readyAt = EditorApplication.timeSinceStartup + 2.0;
            EditorApplication.update += StartDefaultSceneWhenReady;
        }

        private static void StartDefaultSceneWhenReady()
        {
            if (EditorApplication.timeSinceStartup < readyAt || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            EditorApplication.update -= StartDefaultSceneWhenReady;
            Scene scene = SceneManager.GetActiveScene();
            string playScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene);
            Debug.Log($"DEFAULT_STARTUP sceneCount={SceneManager.sceneCount} valid={scene.IsValid()} active={scene.path} name={scene.name} dirty={scene.isDirty} playOverride={playScene}");
            bool startsMain = (scene.path == GameplayStartup.ScenePath &&
                (string.IsNullOrEmpty(playScene) || playScene == GameplayStartup.ScenePath)) ||
                ((SceneManager.sceneCount == 0 ||
                  (SceneManager.sceneCount == 1 && scene.IsValid() && string.IsNullOrEmpty(scene.path) && !scene.isDirty)) &&
                 playScene == GameplayStartup.ScenePath);
            if (!startsMain)
            {
                Debug.LogError("DEFAULT_STARTUP FAIL: the untouched Editor Play action would not start MainArena.");
                EditorApplication.Exit(1);
                return;
            }
            // Observe the ordinary startup configuration without setting or opening a scene here.
            EditorApplication.isPlaying = true;
        }

        private static void Begin(string mode, string scene)
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run this entry point from an Editor batch process in Edit mode.");
            SessionState.SetString(RunKey, mode);
            Register();
            EditorSceneManager.OpenScene(scene);
            EditorApplication.isPlaying = true;
        }

        private static void Register()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            // Let scene Start methods and initial physics ticks finish before the existing checks.
            readyAt = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.update += RunWhenReady;
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                hadErrors = true;
        }

        private static void RunWhenReady()
        {
            if (EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= RunWhenReady;
            string mode = SessionState.GetString(RunKey, "");
            if (mode == "scenarios")
                RunCheck("BOT_SCENARIOS", BotScenarioChecks.RunInPlayMode);
            else if (mode == "combat")
            {
                RunCheck("BOT_TACTICS", BotTacticsChecks.RunInPlayMode);
                RunCheck("BOT_COMBAT", BotTacticsChecks.RunCombatInPlayMode);
                RunCheck("REGIONAL_DAMAGE", RobotDamageChecks.RunInPlayMode);
            }
            else
                hadErrors = true;

            SessionState.EraseString(RunKey);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Application.logMessageReceived -= OnLog;
            if (hadErrors) Debug.LogError("GAMEPLAY_REGRESSION FAIL " + mode);
            else Debug.Log("GAMEPLAY_REGRESSION PASS " + mode);
            EditorApplication.Exit(hadErrors ? 1 : 0);
        }

        private static void RunCheck(string name, Func<string> check)
        {
            try
            {
                Debug.Log(name + " PASS " + check());
            }
            catch (Exception error)
            {
                hadErrors = true;
                Debug.LogError(name + " FAIL " + error);
            }
        }
    }
}
