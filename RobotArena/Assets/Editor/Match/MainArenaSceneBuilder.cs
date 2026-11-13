using System;
using System.IO;
using RobotSavaslari.Combat;
using RobotSavaslari.Driving;
using RobotSavaslari.UI;
using RobotSavaslari.Robots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RobotSavaslari.Match.Editor
{
    public static class MainArenaSceneBuilder
    {
        public const string ScenePath = RobotSavaslari.Editor.GameplayStartup.ScenePath;
        private const string WeaponProfilePath = "Assets/Settings/Combat/StandardMelee.asset";

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
            var weaponProfile = AssetDatabase.LoadAssetAtPath<CombatProfile>(WeaponProfilePath);
            if (weaponProfile == null) throw new InvalidOperationException("Missing arena weapon profile: " + WeaponProfilePath);
            foreach (var robot in new[] { match.Player, match.Opponent })
            {
                if (robot == null) throw new InvalidOperationException("Match robot reference is missing.");
                if (robot.GetComponent<RobotMotor>() == null) robot.gameObject.AddComponent<RobotMotor>();
                if (robot.GetComponent<RobotDriveMotor>() == null) robot.gameObject.AddComponent<RobotDriveMotor>();
                using (var serializedRobot = new SerializedObject(robot))
                {
                    serializedRobot.FindProperty("weaponProfile").objectReferenceValue = weaponProfile;
                    serializedRobot.ApplyModifiedPropertiesWithoutUndo();
                }
                robot.enabled = true;
            }
            var driver = match.Player.GetComponent<PlayerKeyboardDriver>();
            if (driver == null) driver = match.Player.gameObject.AddComponent<PlayerKeyboardDriver>();
            driver.Motor = match.Player.GetComponent<RobotMotor>();
            match.ConfigureGameplay();
            match.gameObject.name = "Arena Match";
            if (match.GetComponent<SettingsPanelController>() == null)
                match.gameObject.AddComponent<SettingsPanelController>();
            if (match.GetComponent<MainMenuScreen>() == null)
                match.gameObject.AddComponent<MainMenuScreen>();
            RobotSavaslari.Editor.ArenaSceneBuilder.ExpandGrid(scene);
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
