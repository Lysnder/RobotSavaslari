using System.IO;
using RobotSavaslari.Robots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Driving.Editor
{
    /// <summary>
    /// A1 deneme sahnesini üretir: düz bir alan, ölçü ızgarası ve üzerinde
    /// Atalay'ın girdi bileşenleri bulunan ortak robot.
    ///
    /// Ortak Arena sahnesine, ortak prefab'a ve proje ayarlarına dokunmaz;
    /// sahne derleme listesine (Build Settings) de eklenmez.
    /// </summary>
    public static class DrivingSceneSetup
    {
        public const string DriveScenePath = "Assets/Scenes/Tests/Driving/KeyboardDriving.unity";

        private const string RobotPrefabPath = "Assets/Prefabs/Robot.prefab";

        private const float GroundSize = 80f;
        private const float GridExtent = 20f;
        private const float GridStep = 5f;

        [MenuItem("RobotSavaslari/Driving/Create Keyboard Course")]
        public static void Generate()
        {
            GameObject robotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabPath);
            if (robotPrefab == null)
            {
                Debug.LogError($"Ortak robot prefab'i bulunamadi: {RobotPrefabPath}");
                return;
            }

            Material groundMaterial = DrivingSceneBuildUtility.CreateMaterial("DrivingGround", new Color(0.24f, 0.25f, 0.27f));
            Material gridMaterial = DrivingSceneBuildUtility.CreateMaterial("DrivingGrid", new Color(0.55f, 0.57f, 0.60f));
            Material startMaterial = DrivingSceneBuildUtility.CreateMaterial("DrivingStart", new Color(0.90f, 0.75f, 0.20f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Light light = Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            GameObject field = new GameObject("Duz Alan");
            DrivingSceneBuildUtility.CreateBlock(field.transform, "Zemin", new Vector3(0f, -0.5f, 0f),
                new Vector3(GroundSize, 1f, GroundSize), groundMaterial);
            CreateGrid(field.transform, gridMaterial);

            // Başlangıç noktası: her rota buradan, aynı yönle başlar.
            DrivingSceneBuildUtility.CreateBlock(field.transform, "Baslangic Isareti",
                new Vector3(0f, 0.015f, -5f), new Vector3(1.6f, 0.03f, 1.6f), startMaterial);

            GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(robotPrefab);
            robot.name = "Robot (Driving)";
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
                DrivingSceneBuildUtility.SetReference(follow, "target", robot.transform);
            }

            DrivingSceneBuildUtility.EnsureFolder(Path.GetDirectoryName(DriveScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, DriveScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Klavye surus sahnesi: {DriveScenePath}");
        }

        private static void CreateGrid(Transform parent, Material material)
        {
            GameObject grid = new GameObject("Olcu Izgarasi");
            grid.transform.SetParent(parent, false);

            float span = GridExtent * 2f;
            for (float offset = -GridExtent; offset <= GridExtent + 0.001f; offset += GridStep)
            {
                DrivingSceneBuildUtility.CreateBlock(grid.transform, $"X {offset:0}",
                    new Vector3(0f, 0.01f, offset), new Vector3(span, 0.02f, 0.08f), material);
                DrivingSceneBuildUtility.CreateBlock(grid.transform, $"Z {offset:0}",
                    new Vector3(offset, 0.01f, 0f), new Vector3(0.08f, 0.02f, span), material);
            }
        }
    }
}
