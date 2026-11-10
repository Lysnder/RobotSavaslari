using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RobotSavaslari.Burak.Editor
{
    public static class BurakB1SceneBuilder
    {
        public const string ScenePath = "Assets/Burak/Scenes/BurakB1.unity";

        [MenuItem("RobotSavaslari/Burak/Build B1 Test Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder("Assets/Burak/Scenes")) AssetDatabase.CreateFolder("Assets/Burak", "Scenes");
            // Copy first: never open or save edits into the shared Arena.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null &&
                !AssetDatabase.CopyAsset("Assets/Scenes/Arena.unity", ScenePath))
                throw new InvalidOperationException("Arena could not be copied.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (UnityEngine.Object.FindFirstObjectByType<BurakMatchController>() != null) return;
            foreach (var match in UnityEngine.Object.FindObjectsByType<ArenaMatchController>())
                UnityEngine.Object.DestroyImmediate(match);
            foreach (var selection in UnityEngine.Object.FindObjectsByType<RobotSelectionController>())
                UnityEngine.Object.DestroyImmediate(selection);
            var player = GameObject.Find("Mavi Robot").GetComponent<RobotPrototypeController>();
            var opponent = GameObject.Find("Kirmizi Robot").GetComponent<RobotPrototypeController>();
            // Shared Arena currently stores spawn poses in ArenaMatchController, not spawn objects.
            var ps = new GameObject("Burak Player Spawn").transform;
            ps.SetPositionAndRotation(new Vector3(-3.15f, .82f, 0), Quaternion.Euler(0, 90, 0));
            var os = new GameObject("Burak Opponent Spawn").transform;
            os.SetPositionAndRotation(new Vector3(3.15f, .82f, 0), Quaternion.Euler(0, -90, 0));
            player.transform.SetPositionAndRotation(ps.position, ps.rotation);
            opponent.transform.SetPositionAndRotation(os.position, os.rotation);
            var harness = new GameObject("Burak B1 Match").AddComponent<BurakMatchController>();
            harness.Configure(player, opponent, ps, os);
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(0, 14, -10);
                Camera.main.transform.LookAt(new Vector3(0, .8f, 0));
                Camera.main.fieldOfView = 50;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("B1 scene saved: " + ScenePath);
        }
    }
}
