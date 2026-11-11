using System.Collections.Generic;
using RobotSavaslari.Robots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Driving.Editor
{
    /// <summary>
    /// A2, A3 ve A4 sahnelerini ve iki ayar setinin prefab varyantlarını üretir.
    ///
    /// * A2 — sürüş hissi: düz hızlanma, kısa zikzak, dar dönüş, geri çıkış.
    /// * A3 — çarpışma: önden, açılı, köşe, duvar boyunca.
    /// * A4 — hepsi tek sahnede sırayla.
    ///
    /// Ortak prefab, ortak hareket kodu, ana Arena ve proje ayarları
    /// değiştirilmez; ayar farkları yalnızca Atalay'ın prefab varyantlarında
    /// durur ve hiçbir sahne derleme listesine eklenmez.
    /// </summary>
    public static class DrivingCourseSceneSetup
    {
        public const string FeelScenePath = "Assets/Scenes/Tests/Driving/DrivingFeel.unity";
        public const string CollisionScenePath = "Assets/Scenes/Tests/Driving/CollisionRecovery.unity";
        public const string AcceptanceScenePath = "Assets/Scenes/Tests/Driving/DrivingAcceptance.unity";

        public const string VariantFolder = "Assets/Prefabs/Driving";
        public const string StableVariantPath = VariantFolder + "/Robot_AyarA_Sabit.prefab";
        public const string AgileVariantPath = VariantFolder + "/Robot_AyarB_Cevik.prefab";

        private const string RobotPrefabPath = "Assets/Prefabs/Robot.prefab";
        private const float GroundSize = 150f;

        /// <summary>Bir ayar setinin motor değerleri.</summary>
        private struct MotorSettings
        {
            public float MaxForwardSpeed;
            public float MaxReverseSpeed;
            public float Acceleration;
            public float BrakeDeceleration;
            public float MaxTurnSpeed;
            public float TurnAcceleration;
            public float TurnBrakeDeceleration;
        }

        // A - Sabit: ölçülü hızlanır, sert frenler, dar dönüşte taşmaz.
        // İvme 20'nin altına inmiyor: ortak motor her fizik adımında gövdenin
        // gerçek hızını geri okuduğu için zemin sürtünmesi (~11.8 m/s²)
        // ivmeden düşülüyor ve altındaki ayarlar robotu kaldıramıyor.
        private static readonly MotorSettings StableSettings = new MotorSettings
        {
            MaxForwardSpeed = 5f,
            MaxReverseSpeed = 2.5f,
            Acceleration = 20f,
            BrakeDeceleration = 30f,
            MaxTurnSpeed = 140f,
            TurnAcceleration = 520f,
            TurnBrakeDeceleration = 900f,
        };

        // B - Çevik: hızlı tepki verir, geç durur, dönüşte savrulma hissi verir.
        private static readonly MotorSettings AgileSettings = new MotorSettings
        {
            MaxForwardSpeed = 7.5f,
            MaxReverseSpeed = 4f,
            Acceleration = 26f,
            BrakeDeceleration = 20f,
            MaxTurnSpeed = 230f,
            TurnAcceleration = 900f,
            TurnBrakeDeceleration = 1200f,
        };

        [MenuItem("RobotSavaslari/Driving/Create Driving Courses")]
        public static void Generate()
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError($"Ortak robot prefab'i bulunamadi: {RobotPrefabPath}");
                return;
            }

            DrivingSceneBuildUtility.EnsureFolder(VariantFolder);
            GameObject stable = CreateVariant(basePrefab, StableVariantPath, StableSettings);
            GameObject agile = CreateVariant(basePrefab, AgileVariantPath, AgileSettings);

            BuildScene(FeelScenePath, stable, agile, includeFeelRoutes: true, includeCollisionRoutes: false);
            BuildScene(CollisionScenePath, stable, agile, includeFeelRoutes: false, includeCollisionRoutes: true);
            BuildScene(AcceptanceScenePath, stable, agile, includeFeelRoutes: true, includeCollisionRoutes: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Surus: {FeelScenePath}\nCarpisma: {CollisionScenePath}\nKabul: {AcceptanceScenePath}");
        }

        /// <summary>
        /// Ortak prefab'ın varyantını oluşturur: ortak girdi bileşenleri kapalı,
        /// Atalay'ın bileşenleri ekli, motor değerleri bu ayar setine göre.
        /// </summary>
        private static GameObject CreateVariant(GameObject basePrefab, string path, MotorSettings settings)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);

            instance.GetComponent<KeyboardRobotInput>().enabled = false;
            instance.GetComponent<RobotCommandSequence>().enabled = false;

            if (instance.GetComponent<PlayerKeyboardDriver>() == null)
            {
                instance.AddComponent<PlayerKeyboardDriver>();
            }

            if (instance.GetComponent<DriveProbe>() == null)
            {
                instance.AddComponent<DriveProbe>();
            }

            if (instance.GetComponent<CollisionReporter>() == null)
            {
                instance.AddComponent<CollisionReporter>();
            }

            RobotMotor motor = instance.GetComponent<RobotMotor>();
            DrivingSceneBuildUtility.SetFloat(motor, "maxForwardSpeed", settings.MaxForwardSpeed);
            DrivingSceneBuildUtility.SetFloat(motor, "maxReverseSpeed", settings.MaxReverseSpeed);
            DrivingSceneBuildUtility.SetFloat(motor, "acceleration", settings.Acceleration);
            DrivingSceneBuildUtility.SetFloat(motor, "brakeDeceleration", settings.BrakeDeceleration);
            DrivingSceneBuildUtility.SetFloat(motor, "maxTurnSpeed", settings.MaxTurnSpeed);
            DrivingSceneBuildUtility.SetFloat(motor, "turnAcceleration", settings.TurnAcceleration);
            DrivingSceneBuildUtility.SetFloat(motor, "turnBrakeDeceleration", settings.TurnBrakeDeceleration);

            GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            return variant;
        }

        private static void BuildScene(string scenePath, GameObject stable, GameObject agile,
            bool includeFeelRoutes, bool includeCollisionRoutes)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Light light = Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            Material groundMaterial = DrivingSceneBuildUtility.CreateMaterial("DrivingGround", new Color(0.24f, 0.25f, 0.27f));
            Material markerMaterial = DrivingSceneBuildUtility.CreateMaterial("DrivingStart", new Color(0.90f, 0.75f, 0.20f));
            Material gateMaterial = DrivingSceneBuildUtility.CreateMaterial("DrivingGate", new Color(0.30f, 0.72f, 0.88f));
            Material wallMaterial = DrivingSceneBuildUtility.CreateMaterial("DrivingWall", new Color(0.62f, 0.60f, 0.56f));

            GameObject field = new GameObject("Alan");
            DrivingSceneBuildUtility.CreateBlock(field.transform, "Zemin", new Vector3(0f, -0.5f, 0f),
                new Vector3(GroundSize, 1f, GroundSize), groundMaterial);

            GameObject routeRoot = new GameObject("Rotalar");
            List<RouteCourse> routes = new List<RouteCourse>();

            if (includeFeelRoutes)
            {
                BuildFeelRoutes(routeRoot.transform, field.transform, routes, markerMaterial, gateMaterial, wallMaterial);
            }

            if (includeCollisionRoutes)
            {
                BuildCollisionRoutes(routeRoot.transform, field.transform, routes, markerMaterial, gateMaterial, wallMaterial);
            }

            GameObject hudObject = new GameObject("Arayuz");
            DriveTelemetryHud hud = hudObject.AddComponent<DriveTelemetryHud>();

            Camera camera = Object.FindAnyObjectByType<Camera>();
            TestCameraFollow follow = null;
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0f, 16f, -24f), Quaternion.Euler(35f, 0f, 0f));
                follow = camera.gameObject.AddComponent<TestCameraFollow>();
            }

            GameObject rigObject = new GameObject("Deneme Duzenegi");
            DriveRig rig = rigObject.AddComponent<DriveRig>();
            ConfigureRig(rig, stable, agile, routes, follow, hud);

            DrivingSceneBuildUtility.EnsureFolder("Assets/Scenes/Tests/Driving");
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void ConfigureRig(DriveRig rig, GameObject stable, GameObject agile,
            List<RouteCourse> routes, TestCameraFollow follow, DriveTelemetryHud hud)
        {
            SerializedObject serialized = new SerializedObject(rig);

            SerializedProperty sets = serialized.FindProperty("settingSets");
            sets.arraySize = 2;

            SerializedProperty first = sets.GetArrayElementAtIndex(0);
            first.FindPropertyRelative("setName").stringValue = "A - Sabit";
            first.FindPropertyRelative("note").stringValue =
                "Yavas hizlanir, sert frenler. Dar donusu temiz alir, duz hatta yavas.";
            first.FindPropertyRelative("robotPrefab").objectReferenceValue = stable;

            SerializedProperty second = sets.GetArrayElementAtIndex(1);
            second.FindPropertyRelative("setName").stringValue = "B - Cevik";
            second.FindPropertyRelative("note").stringValue =
                "Hizli tepki verir, gec durur. Zikzakta kolay, duvar dibinde affetmez.";
            second.FindPropertyRelative("robotPrefab").objectReferenceValue = agile;

            SerializedProperty routeArray = serialized.FindProperty("routes");
            routeArray.arraySize = routes.Count;
            for (int index = 0; index < routes.Count; index++)
            {
                routeArray.GetArrayElementAtIndex(index).objectReferenceValue = routes[index];
            }

            serialized.FindProperty("cameraFollow").objectReferenceValue = follow;
            serialized.FindProperty("hud").objectReferenceValue = hud;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildFeelRoutes(Transform routeRoot, Transform field, List<RouteCourse> routes,
            Material markerMaterial, Material gateMaterial, Material wallMaterial)
        {
            routes.Add(BuildRoute(routeRoot, "1 - Duz hizlanma",
                "Tam gazla duz git, son kapida dur. Hizlanma ve fren mesafesi olculur.",
                new Vector3(-50f, 0f, -45f), 0f,
                new[]
                {
                    new Vector3(-50f, 0f, -30f),
                    new Vector3(-50f, 0f, -15f),
                    new Vector3(-50f, 0f, 0f),
                    new Vector3(-50f, 0f, 15f),
                },
                markerMaterial, gateMaterial));

            routes.Add(BuildRoute(routeRoot, "2 - Kisa zikzak",
                "Kapilari sirayla gec. Yon degistirirken donusun gecikmesi olculur.",
                new Vector3(-32f, 0f, -45f), 0f,
                new[]
                {
                    new Vector3(-28f, 0f, -35f),
                    new Vector3(-36f, 0f, -25f),
                    new Vector3(-28f, 0f, -15f),
                    new Vector3(-36f, 0f, -5f),
                },
                markerMaterial, gateMaterial));

            routes.Add(BuildRoute(routeRoot, "3 - Dar donus",
                "Kapilarin etrafindan donerek basa don. Donus yaricapi olculur.",
                new Vector3(-14f, 0f, -45f), 0f,
                new[]
                {
                    new Vector3(-14f, 0f, -33f),
                    new Vector3(-8f, 0f, -27f),
                    new Vector3(-14f, 0f, -21f),
                    new Vector3(-20f, 0f, -27f),
                },
                markerMaterial, gateMaterial));

            RouteCourse reverse = BuildRoute(routeRoot, "4 - Geri cikis",
                "Cikmaza gir, sonra geri ve donusle cik. Geri surus kontrolu olculur.",
                new Vector3(-50f, 0f, 22f), 0f,
                new[]
                {
                    new Vector3(-50f, 0f, 14f),
                    new Vector3(-50f, 0f, 4f),
                },
                markerMaterial, gateMaterial);
            routes.Add(reverse);

            // Çıkmaz: üç duvarlı cep, robot ancak geri sürerek çıkabilir.
            GameObject pocket = new GameObject("Cikmaz");
            pocket.transform.SetParent(field, false);
            DrivingSceneBuildUtility.CreateBlock(pocket.transform, "Dip", new Vector3(-50f, 0.75f, 32f),
                new Vector3(9f, 1.5f, 1f), wallMaterial);
            DrivingSceneBuildUtility.CreateBlock(pocket.transform, "Sol", new Vector3(-54.5f, 0.75f, 28f),
                new Vector3(1f, 1.5f, 9f), wallMaterial);
            DrivingSceneBuildUtility.CreateBlock(pocket.transform, "Sag", new Vector3(-45.5f, 0.75f, 28f),
                new Vector3(1f, 1.5f, 9f), wallMaterial);
        }

        private static void BuildCollisionRoutes(Transform routeRoot, Transform field, List<RouteCourse> routes,
            Material markerMaterial, Material gateMaterial, Material wallMaterial)
        {
            GameObject obstacles = new GameObject("Engeller");
            obstacles.transform.SetParent(field, false);

            routes.Add(BuildRoute(routeRoot, "5 - Onden carpisma",
                "Tam gazla duvara gir, sonra geri surup kurtul.",
                new Vector3(14f, 0f, -45f), 0f,
                new[] { new Vector3(14f, 0f, -34f) },
                markerMaterial, gateMaterial));
            DrivingSceneBuildUtility.CreateBlock(obstacles.transform, "Duvar - onden",
                new Vector3(14f, 0.75f, -25f), new Vector3(12f, 1.5f, 1f), wallMaterial);

            routes.Add(BuildRoute(routeRoot, "6 - Acili carpisma",
                "Duvara aciyla gir. Carpisma sonrasi yon duzeltmesi denenir.",
                new Vector3(34f, 0f, -45f), 0f,
                new[] { new Vector3(34f, 0f, -34f) },
                markerMaterial, gateMaterial));
            DrivingSceneBuildUtility.CreateBlock(obstacles.transform, "Duvar - acili",
                new Vector3(34f, 0.75f, -25f), new Vector3(14f, 1.5f, 1f), wallMaterial, yaw: 30f);

            routes.Add(BuildRoute(routeRoot, "7 - Koseye surme",
                "Koseye dayan, sonra geri cikip yonunu degistir.",
                new Vector3(52f, 0f, -14f), 0f,
                new[] { new Vector3(52f, 0f, -6f) },
                markerMaterial, gateMaterial));
            DrivingSceneBuildUtility.CreateBlock(obstacles.transform, "Kose - dip",
                new Vector3(52f, 0.75f, 4f), new Vector3(16f, 1.5f, 1f), wallMaterial);
            DrivingSceneBuildUtility.CreateBlock(obstacles.transform, "Kose - yan",
                new Vector3(59.5f, 0.75f, -3f), new Vector3(1f, 1.5f, 15f), wallMaterial);

            routes.Add(BuildRoute(routeRoot, "8 - Duvar boyunca",
                "Duvara yaslanarak boyunca git. Surtunme kontrolu bozuyor mu?",
                new Vector3(14f, 0f, 2f), 0f,
                new[]
                {
                    new Vector3(14f, 0f, 14f),
                    new Vector3(14f, 0f, 28f),
                    new Vector3(14f, 0f, 40f),
                },
                markerMaterial, gateMaterial));
            DrivingSceneBuildUtility.CreateBlock(obstacles.transform, "Duvar - boyunca",
                new Vector3(18f, 0.75f, 24f), new Vector3(1f, 1.5f, 42f), wallMaterial);
        }

        /// <summary>
        /// Tek rota kurar: başlangıç noktası (konum + yön) ve sırayla geçilecek
        /// kapı işaretleri. İşaretler yalnızca görseldir, çarpışma hacimleri yoktur.
        /// </summary>
        private static RouteCourse BuildRoute(Transform parent, string routeName, string instructions,
            Vector3 startPosition, float startYaw, Vector3[] gatePositions,
            Material markerMaterial, Material gateMaterial)
        {
            GameObject routeObject = new GameObject(routeName);
            routeObject.transform.SetParent(parent, false);
            routeObject.transform.position = startPosition;

            GameObject startObject = new GameObject("Baslangic");
            startObject.transform.SetParent(routeObject.transform, false);
            startObject.transform.SetPositionAndRotation(startPosition, Quaternion.Euler(0f, startYaw, 0f));

            DrivingSceneBuildUtility.CreateBlock(routeObject.transform, "Baslangic Isareti",
                startPosition + new Vector3(0f, 0.015f, 0f), new Vector3(1.8f, 0.03f, 1.8f),
                markerMaterial, startYaw);

            Transform[] gates = new Transform[gatePositions.Length];
            for (int index = 0; index < gatePositions.Length; index++)
            {
                GameObject gate = DrivingSceneBuildUtility.CreateBlock(routeObject.transform, $"Kapi {index + 1}",
                    gatePositions[index] + new Vector3(0f, 0.015f, 0f), new Vector3(2.4f, 0.03f, 0.5f),
                    gateMaterial, startYaw);
                gates[index] = gate.transform;
            }

            RouteCourse route = routeObject.AddComponent<RouteCourse>();

            SerializedObject serialized = new SerializedObject(route);
            serialized.FindProperty("routeName").stringValue = routeName;
            serialized.FindProperty("instructions").stringValue = instructions;
            serialized.FindProperty("startPoint").objectReferenceValue = startObject.transform;

            SerializedProperty gateArray = serialized.FindProperty("gates");
            gateArray.arraySize = gates.Length;
            for (int index = 0; index < gates.Length; index++)
            {
                gateArray.GetArrayElementAtIndex(index).objectReferenceValue = gates[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            return route;
        }
    }
}
