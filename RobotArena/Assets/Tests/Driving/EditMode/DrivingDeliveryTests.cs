using System.IO;
using NUnit.Framework;
using RobotSavaslari.Robots;
using UnityEditor;
using UnityEngine;

namespace RobotSavaslari.Driving.Tests
{
    /// <summary>
    /// Teslim sözleşmesini korur: ayar setleri ortak prefab'ın varyantlarında
    /// durur, deneme sahneleri yerindedir ve hiçbiri derleme listesine
    /// eklenmemiştir (proje ayarları kartların teslim alanı dışında).
    /// </summary>
    public class DrivingDeliveryTests
    {
        private const string CommonRobotPrefabPath = "Assets/Prefabs/Robot.prefab";
        private const string StableVariantPath = "Assets/Prefabs/Driving/Robot_AyarA_Sabit.prefab";
        private const string AgileVariantPath = "Assets/Prefabs/Driving/Robot_AyarB_Cevik.prefab";

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Tests/Driving/KeyboardDriving.unity",
            "Assets/Scenes/Tests/Driving/DrivingFeel.unity",
            "Assets/Scenes/Tests/Driving/CollisionRecovery.unity",
            "Assets/Scenes/Tests/Driving/DrivingAcceptance.unity",
        };

        [TestCase(StableVariantPath)]
        [TestCase(AgileVariantPath)]
        public void AyarSetiOrtakPrefabinVaryantidir(string variantPath)
        {
            GameObject variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Assert.IsNotNull(variant, $"Varyant bulunamadi: {variantPath}");

            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(variant),
                "Ayarlar ortak prefab'in varyantinda tutulmali.");

            GameObject root = PrefabUtility.GetCorrespondingObjectFromSource(variant);
            Assert.IsNotNull(root, "Varyantin kaynak prefab'i okunamadi.");
            Assert.AreEqual(CommonRobotPrefabPath, AssetDatabase.GetAssetPath(root),
                "Varyant ortak robot prefab'indan turemeli.");
        }

        [TestCase(StableVariantPath)]
        [TestCase(AgileVariantPath)]
        public void AyarSetiSurusBilesenleriniTasir(string variantPath)
        {
            GameObject variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Assert.IsNotNull(variant, $"Varyant bulunamadi: {variantPath}");

            Assert.IsNotNull(variant.GetComponent<PlayerKeyboardDriver>(), "PlayerKeyboardDriver yok.");
            Assert.IsNotNull(variant.GetComponent<DriveProbe>(), "DriveProbe yok.");
            Assert.IsNotNull(variant.GetComponent<CollisionReporter>(), "CollisionReporter yok.");

            Assert.IsFalse(variant.GetComponent<KeyboardRobotInput>().enabled,
                "Ortak klavye bileseni varyantta kapali olmali.");
            Assert.IsFalse(variant.GetComponent<RobotCommandSequence>().enabled,
                "Ortak komut dizisi varyantta kapali olmali.");
        }

        [Test]
        public void IkiAyarSetiFarkliDegerlerTasir()
        {
            RobotMotor stable = LoadMotor(StableVariantPath);
            RobotMotor agile = LoadMotor(AgileVariantPath);

            float stableSpeed = ReadFloat(stable, "maxForwardSpeed");
            float agileSpeed = ReadFloat(agile, "maxForwardSpeed");
            float stableTurn = ReadFloat(stable, "maxTurnSpeed");
            float agileTurn = ReadFloat(agile, "maxTurnSpeed");
            float stableBrake = ReadFloat(stable, "brakeDeceleration");
            float agileBrake = ReadFloat(agile, "brakeDeceleration");

            Assert.Less(stableSpeed, agileSpeed, "Cevik set daha hizli olmali.");
            Assert.Less(stableTurn, agileTurn, "Cevik set daha hizli donmeli.");
            Assert.Greater(stableBrake, agileBrake, "Sabit set daha sert frenlemeli.");

            // Kayıtlı değerler: başka bir geliştirici varyantı açtığında aynısını bulur.
            Assert.AreEqual(5f, stableSpeed, 0.001f);
            Assert.AreEqual(7.5f, agileSpeed, 0.001f);
            Assert.AreEqual(140f, stableTurn, 0.001f);
            Assert.AreEqual(230f, agileTurn, 0.001f);
        }

        [Test]
        public void DenemeSahneleriYerinde()
        {
            foreach (string path in ScenePaths)
            {
                Assert.IsTrue(File.Exists(path), $"Sahne bulunamadi: {path}");
            }
        }

        [Test]
        public void DenemeSahneleriDerlemeListesindeDegil()
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                Assert.IsFalse(scene.path.StartsWith("Assets/Scenes/Tests/Driving/"),
                    $"Deneme sahnesi derleme listesine eklenmis: {scene.path}. " +
                    "Proje ayarlari bu kartlarin teslim alani disinda.");
            }
        }

        private static RobotMotor LoadMotor(string variantPath)
        {
            GameObject variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Assert.IsNotNull(variant, $"Varyant bulunamadi: {variantPath}");

            RobotMotor motor = variant.GetComponent<RobotMotor>();
            Assert.IsNotNull(motor, $"Varyantta RobotMotor yok: {variantPath}");

            return motor;
        }

        private static float ReadFloat(Object target, string fieldName)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(fieldName);
            Assert.IsNotNull(property, $"{target.GetType().Name}.{fieldName} alani bulunamadi.");

            return property.floatValue;
        }
    }
}
