using System.IO;
using RobotSavaslari.Robots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Atalay.Editor
{
    /// <summary>
    /// A1 deneme sahnesini üretir: düz bir alan, ölçü ızgarası ve üzerinde
    /// Atalay'ın girdi bileşenleri bulunan ortak robot.
    ///
    /// Ortak Arena sahnesine, ortak prefab'a ve proje ayarlarına dokunmaz;
    /// sahne derleme listesine (Build Settings) de eklenmez.
    /// </summary>
    public static class AtalayDriveSceneSetup
    {
        public const string DriveScenePath = "Assets/Scenes/Atalay/A1_KlavyeSurus.unity";

        private const string RobotPrefabPath = "Assets/Prefabs/Robot.prefab";
        private const string MaterialFolder = "Assets/Materials/Atalay";

        private const float GroundSize = 80f;
        private const float GridExtent = 20f;
        private const float GridStep = 5f;

        [MenuItem("Robot Savaslari/Atalay/A1 Deneme Sahnesini Olustur")]
        public static void Generate()
        {
            GameObject robotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabPath);
            if (robotPrefab == null)
            {
                Debug.LogError($"Ortak robot prefab'i bulunamadi: {RobotPrefabPath}");
                return;
            }

            EnsureFolder(MaterialFolder);
            Material groundMaterial = CreateMaterial("A1_Zemin", new Color(0.24f, 0.25f, 0.27f));
            Material gridMaterial = CreateMaterial("A1_Izgara", new Color(0.55f, 0.57f, 0.60f));
            Material startMaterial = CreateMaterial("A1_Baslangic", new Color(0.90f, 0.75f, 0.20f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Light light = Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            GameObject field = new GameObject("Duz Alan");
            CreateBlock(field.transform, "Zemin", new Vector3(0f, -0.5f, 0f),
                new Vector3(GroundSize, 1f, GroundSize), groundMaterial);
            CreateGrid(field.transform, gridMaterial);

            // Başlangıç noktası: her rota buradan, aynı yönle başlar.
            CreateBlock(field.transform, "Baslangic Isareti", new Vector3(0f, 0.015f, -5f),
                new Vector3(1.6f, 0.03f, 1.6f), startMaterial);

            GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(robotPrefab);
            robot.name = "Robot (Atalay)";
            robot.transform.SetPositionAndRotation(new Vector3(0f, 0f, -5f), Quaternion.identity);

            // Ortak girdi bileşenleri kapalı: bu sahnede robotu yalnızca
            // Atalay'ın kendi bileşeni sürer.
            robot.GetComponent<KeyboardRobotInput>().enabled = false;
            robot.GetComponent<RobotCommandSequence>().enabled = false;

            robot.AddComponent<PlayerKeyboardDriver>();
            robot.AddComponent<DriveProbe>();
            robot.AddComponent<DriveTelemetryHud>();

            Camera camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0f, 14f, -19f), Quaternion.Euler(35f, 0f, 0f));
                TestCameraFollow follow = camera.gameObject.AddComponent<TestCameraFollow>();
                follow.Target = robot.transform;

                SerializedObject serialized = new SerializedObject(follow);
                serialized.FindProperty("target").objectReferenceValue = robot.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EnsureFolder(Path.GetDirectoryName(DriveScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, DriveScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"A1 deneme sahnesi: {DriveScenePath}");
        }

        private static void CreateGrid(Transform parent, Material material)
        {
            GameObject grid = new GameObject("Olcu Izgarasi");
            grid.transform.SetParent(parent, false);

            float span = GridExtent * 2f;
            for (float offset = -GridExtent; offset <= GridExtent + 0.001f; offset += GridStep)
            {
                CreateBlock(grid.transform, $"X {offset:0}", new Vector3(0f, 0.01f, offset),
                    new Vector3(span, 0.02f, 0.08f), material);
                CreateBlock(grid.transform, $"Z {offset:0}", new Vector3(offset, 0.01f, 0f),
                    new Vector3(0.08f, 0.02f, span), material);
            }
        }

        private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            block.isStatic = true;

            // Izgara çizgileri yalnızca görseldir; robotu takılmasın diye
            // zemin dışındaki ince bloklarda çarpışma hacmi kaldırılır.
            if (scale.y < 0.1f)
            {
                Object.DestroyImmediate(block.GetComponent<Collider>());
            }

            return block;
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

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
