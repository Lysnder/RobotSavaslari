using System;
using System.IO;
using RobotSavaslari.Driving;
using RobotSavaslari.Robots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RobotSavaslari.Match.Editor
{
    public static class MainArenaSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Gameplay/MainArena.unity";

        [MenuItem("RobotSavaslari/Gameplay/Rebuild Main Arena")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Scenes/Gameplay");
            AssetDatabase.Refresh();
            if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset(MatchRegressionSceneBuilder.ScenePath, ScenePath))
                throw new InvalidOperationException("Match regression arena could not be copied.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var match = UnityEngine.Object.FindFirstObjectByType<MatchController>();
            if (match == null) throw new InvalidOperationException("Arena has no match controller.");
            foreach (var oldMatch in UnityEngine.Object.FindObjectsByType<ArenaMatchController>())
                UnityEngine.Object.DestroyImmediate(oldMatch);
            foreach (var selection in UnityEngine.Object.FindObjectsByType<RobotSelectionController>())
                UnityEngine.Object.DestroyImmediate(selection);
            foreach (var robot in new[] { match.Player, match.Opponent })
            {
                if (robot == null) throw new InvalidOperationException("Match robot reference is missing.");
                if (robot.GetComponent<RobotMotor>() == null) robot.gameObject.AddComponent<RobotMotor>();
                if (robot.GetComponent<RobotDriveMotor>() == null) robot.gameObject.AddComponent<RobotDriveMotor>();
                robot.enabled = true;
            }
            var driver = match.Player.GetComponent<PlayerKeyboardDriver>();
            if (driver == null) driver = match.Player.gameObject.AddComponent<PlayerKeyboardDriver>();
            driver.Motor = match.Player.GetComponent<RobotMotor>();
            match.ConfigureGameplay();
            match.gameObject.name = "Arena Match";
            if (match.GetComponent<RobotSavaslari.UI.MainMenuController>() == null)
                match.gameObject.AddComponent<RobotSavaslari.UI.MainMenuController>();
            var playerSpawn = GameObject.Find("Player Spawn");
            var opponentSpawn = GameObject.Find("Opponent Spawn");
            if (playerSpawn != null) playerSpawn.name = "Player Spawn";
            if (opponentSpawn != null) opponentSpawn.name = "Opponent Spawn";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Main arena saved at " + ScenePath);
        }

        // Explicit command-line verification entry point; normal editing keeps its current scene.
        public static void RunGameplaySmoke()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
