using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Editor
{
    public static class BotScenarioSceneBuilder
    {
        [MenuItem("Robot Savaslari/Bot/Create H1-H4 Scenario Scene")]
        public static void Build()
        {
            const string path = "Assets/Scenes/BotScenarios.unity";
            if (System.IO.File.Exists(path)) throw new System.InvalidOperationException("BotScenarios already exists. Open the scene instead.");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var floor = Box("Floor", new Vector3(0, -0.15f, 0), new Vector3(18, 0.3f, 18), new Color(0.15f, 0.19f, 0.24f));
            Box("North wall", new Vector3(0, 0.5f, 8), new Vector3(16, 1, 0.25f), Color.gray);
            Box("South wall", new Vector3(0, 0.5f, -8), new Vector3(16, 1, 0.25f), Color.gray);
            Box("East wall", new Vector3(8, 0.5f, 0), new Vector3(0.25f, 1, 16), Color.gray);
            Box("West wall", new Vector3(-8, 0.5f, 0), new Vector3(0.25f, 1, 16), Color.gray);
            var obstacles = new GameObject("L corner obstacles");
            Box("Corner X", new Vector3(2, 0.6f, 1), new Vector3(0.2f, 1.2f, 2.2f), Color.gray).transform.SetParent(obstacles.transform);
            Box("Corner Z", new Vector3(1, 0.6f, 2), new Vector3(2.2f, 1.2f, 0.2f), Color.gray).transform.SetParent(obstacles.transform);
            var robot = Box("Bot - shared motor only", new Vector3(-3, 0.5f, -3), new Vector3(0.8f, 0.6f, 0.95f), new Color(0.9f, 0.2f, 0.15f));
            robot.transform.localScale = Vector3.one;
            robot.GetComponent<BoxCollider>().size = new Vector3(0.8f, 0.6f, 0.95f);
            // Keep mesh dimensions without scaling physics root.
            robot.GetComponent<MeshFilter>().sharedMesh = null;
            var shell = Box("Body visual", robot.transform.position, new Vector3(0.8f, 0.6f, 0.95f), new Color(0.9f, 0.2f, 0.15f));
            Object.DestroyImmediate(shell.GetComponent<Collider>()); shell.transform.SetParent(robot.transform, true);
            var nose = Box("Forward marker", robot.transform.position + Vector3.forward * 0.55f, new Vector3(0.22f, 0.2f, 0.25f), Color.yellow);
            Object.DestroyImmediate(nose.GetComponent<Collider>()); nose.transform.SetParent(robot.transform, true);
            var rb = robot.AddComponent<Rigidbody>(); rb.mass = 20; rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            robot.AddComponent<RobotDriveMotor>();
            var bot = robot.AddComponent<RobotBotController>(); robot.AddComponent<BotAttackProbe>();
            var target = Box("Target - move me", new Vector3(-3, 0.5f, 2), new Vector3(0.6f, 0.6f, 0.6f), Color.cyan);
            Object.DestroyImmediate(target.GetComponent<Collider>()); bot.Target = target.transform;
            var runner = new GameObject("Scenario selector").AddComponent<BotScenarioRunner>();
            runner.bot = bot; runner.testTarget = target.transform; runner.obstacles = obstacles; obstacles.SetActive(false);
            var camera = new GameObject("Bot Lab Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 20, -13); camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true; camera.orthographicSize = 10.5f; camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Directional Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
        private static GameObject Box(string name, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.position = position; go.transform.localScale = scale;
            // Shared built-in material plus serialized renderer tint: no transient material references in scene.
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color;
            string materialPath = "Assets/Materials/BotLab" + (color == Color.gray ? "Wall" : color == Color.cyan ? "Target" : color == Color.yellow ? "Nose" : color.r > 0.5f ? "Robot" : "Floor") + ".mat";
            var saved = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (saved == null) { AssetDatabase.CreateAsset(material, materialPath); saved = material; }
            else Object.DestroyImmediate(material);
            go.GetComponent<Renderer>().sharedMaterial = saved; return go;
        }
    }
}
