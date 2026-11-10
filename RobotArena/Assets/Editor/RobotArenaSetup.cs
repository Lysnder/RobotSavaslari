using System.Collections.Generic;
using System.IO;
using RobotSavaslari.Robots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Editor
{
    /// <summary>
    /// Robot prefab'ını, malzemeleri ve test arenası sahnesini üretir.
    /// Elle kurulum yerine tek menü komutu: yeniden çalıştırıldığında
    /// aynı dosyaların üzerine yazar.
    /// </summary>
    public static class RobotArenaSetup
    {
        public const string RobotPrefabPath = "Assets/Prefabs/Robot.prefab";
        public const string ArenaScenePath = "Assets/Scenes/Tests/RobotMotor.unity";

        private const string PrefabFolder = "Assets/Prefabs";
        private const string MaterialFolder = "Assets/Materials";

        private const float ArenaHalfSize = 10f;
        private const float WallThickness = 0.5f;
        private const float WallHeight = 1.5f;

        [MenuItem("Robot Savaslari/Kurulum/Robot Prefab + Arena Sahnesi Olustur")]
        public static void Generate()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            Material bodyMaterial = CreateMaterial("RobotBody", new Color(0.21f, 0.35f, 0.58f));
            Material noseMaterial = CreateMaterial("RobotNose", new Color(0.93f, 0.52f, 0.14f));
            Material floorMaterial = CreateMaterial("ArenaFloor", new Color(0.26f, 0.27f, 0.29f));
            Material wallMaterial = CreateMaterial("ArenaWall", new Color(0.62f, 0.60f, 0.56f));

            GameObject prefab = CreateRobotPrefab(bodyMaterial, noseMaterial);
            CreateArenaScene(prefab, floorMaterial, wallMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Robot prefab: {RobotPrefabPath}\nArena sahnesi: {ArenaScenePath}");
        }

        private static GameObject CreateRobotPrefab(Material bodyMaterial, Material noseMaterial)
        {
            // Pivot yerde durur: prefab y = 0'a bırakıldığında paletler zemine oturur.
            GameObject root = new GameObject("Robot");

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.6f, 1.4f);
            collider.center = new Vector3(0f, 0.3f, 0f);

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 25f;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.automaticCenterOfMass = false;
            body.centerOfMass = new Vector3(0f, 0.2f, 0f);

            root.AddComponent<RobotMotor>();
            root.AddComponent<KeyboardRobotInput>();

            RobotCommandSequence sequence = root.AddComponent<RobotCommandSequence>();
            SetBool(sequence, "playOnStart", false);

            CreateVisual(root.transform, "Gövde", new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.6f, 1.4f), bodyMaterial);
            CreateVisual(root.transform, "Burun", new Vector3(0f, 0.35f, 0.8f), new Vector3(0.3f, 0.25f, 0.3f), noseMaterial);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RobotPrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }

        private static void CreateArenaScene(GameObject robotPrefab, Material floorMaterial, Material wallMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0f, 16f, -17f), Quaternion.Euler(40f, 0f, 0f));
            }

            Light light = Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            GameObject arena = new GameObject("Arena");

            float outerSize = (ArenaHalfSize + WallThickness) * 2f;
            CreateBlock(arena.transform, "Zemin", new Vector3(0f, -0.25f, 0f),
                new Vector3(outerSize, 0.5f, outerSize), floorMaterial);

            float wallOffset = ArenaHalfSize + (WallThickness * 0.5f);
            float wallY = WallHeight * 0.5f;

            CreateBlock(arena.transform, "Duvar Kuzey", new Vector3(0f, wallY, wallOffset),
                new Vector3(outerSize, WallHeight, WallThickness), wallMaterial);
            CreateBlock(arena.transform, "Duvar Güney", new Vector3(0f, wallY, -wallOffset),
                new Vector3(outerSize, WallHeight, WallThickness), wallMaterial);
            CreateBlock(arena.transform, "Duvar Doğu", new Vector3(wallOffset, wallY, 0f),
                new Vector3(WallThickness, WallHeight, outerSize), wallMaterial);
            CreateBlock(arena.transform, "Duvar Batı", new Vector3(-wallOffset, wallY, 0f),
                new Vector3(WallThickness, WallHeight, outerSize), wallMaterial);

            // Klavyeyle sürülen robot.
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(robotPrefab);
            player.name = "Robot (Klavye)";
            player.transform.position = new Vector3(-3f, 0f, -2f);

            // Aynı motora klavye olmadan komut gönderen robot.
            GameObject scripted = (GameObject)PrefabUtility.InstantiatePrefab(robotPrefab);
            scripted.name = "Robot (Senaryo)";
            scripted.transform.position = new Vector3(3f, 0f, -2f);
            scripted.GetComponent<KeyboardRobotInput>().enabled = false;
            SetBool(scripted.GetComponent<RobotCommandSequence>(), "playOnStart", true);

            EditorSceneManager.SaveScene(scene, ArenaScenePath);
            AddSceneToBuildSettings(ArenaScenePath);
        }

        private static void CreateVisual(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;

            // Çarpışma hacmi tek yerde (kökteki BoxCollider) dursun.
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            block.isStatic = true;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = GetDefaultShader();

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            EditorUtility.SetDirty(material);

            return material;
        }

        private static Shader GetDefaultShader()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultShader != null)
            {
                return pipeline.defaultShader;
            }

            return Shader.Find("Standard");
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(scene => scene.path == scenePath))
            {
                return;
            }

            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static void SetBool(Component component, string fieldName, bool value)
        {
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"{component.GetType().Name}.{fieldName} alani bulunamadi.");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
