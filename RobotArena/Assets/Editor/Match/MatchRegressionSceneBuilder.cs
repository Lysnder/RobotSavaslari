using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RobotSavaslari.Match.Editor
{
    public static class MatchRegressionSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tests/MatchRegression.unity";

        [MenuItem("RobotSavaslari/Match/Build Match Regression Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Scenes/Tests");
            AssetDatabase.Refresh();
            // Copy first: never open or save edits into the shared Arena.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null &&
                !AssetDatabase.CopyAsset("Assets/Scenes/Tests/CombatArena.unity", ScenePath))
                throw new InvalidOperationException("Arena could not be copied.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (UnityEngine.Object.FindFirstObjectByType<MatchController>() != null) return;
            foreach (var match in UnityEngine.Object.FindObjectsByType<ArenaMatchController>())
                UnityEngine.Object.DestroyImmediate(match);
            foreach (var selection in UnityEngine.Object.FindObjectsByType<RobotSelectionController>())
                UnityEngine.Object.DestroyImmediate(selection);
            var player = GameObject.Find("Mavi Robot").GetComponent<RobotPrototypeController>();
            var opponent = GameObject.Find("Kirmizi Robot").GetComponent<RobotPrototypeController>();
            // Shared Arena currently stores spawn poses in ArenaMatchController, not spawn objects.
            var ps = new GameObject("Player Spawn").transform;
            ps.SetPositionAndRotation(new Vector3(-3.15f, .82f, 0), Quaternion.Euler(0, 90, 0));
            var os = new GameObject("Opponent Spawn").transform;
            os.SetPositionAndRotation(new Vector3(3.15f, .82f, 0), Quaternion.Euler(0, -90, 0));
            player.transform.SetPositionAndRotation(ps.position, ps.rotation);
            opponent.transform.SetPositionAndRotation(os.position, os.rotation);
            var harness = new GameObject("Match Regression").AddComponent<MatchController>();
            harness.Configure(player, opponent, ps, os);
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(0, 14, -10);
                Camera.main.transform.LookAt(new Vector3(0, .8f, 0));
                Camera.main.fieldOfView = 50;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Match regression scene saved: " + ScenePath);
        }
    }
}
