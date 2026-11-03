using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RobotSavaslari.Robots.Tests
{
    /// <summary>
    /// Prefab'ın sözleşmesini korur: fizik gövdesi, çarpışma hacmi, hareket
    /// bileşeni ve klavye bileşeni yerinde mi?
    /// </summary>
    public class RobotPrefabTests
    {
        private const string RobotPrefabPath = "Assets/Prefabs/Robot.prefab";
        private const string ArenaScenePath = "Assets/Scenes/RobotArena.unity";

        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabPath);
            Assert.IsNotNull(prefab, $"Robot prefab'i bulunamadi: {RobotPrefabPath}");
        }

        [Test]
        public void PrefabFizikGovdesineSahip()
        {
            Rigidbody body = prefab.GetComponent<Rigidbody>();

            Assert.IsNotNull(body, "Prefab'da Rigidbody yok.");
            Assert.IsFalse(body.isKinematic, "Rigidbody kinematik olmamali.");
            Assert.IsTrue(body.useGravity, "Rigidbody yer cekimini kullanmali.");
            Assert.AreEqual(CollisionDetectionMode.ContinuousDynamic, body.collisionDetectionMode,
                "Duvardan gecmemek icin surekli carpisma algilama gerekli.");
            Assert.AreEqual(RigidbodyInterpolation.Interpolate, body.interpolation);
            Assert.IsTrue(body.constraints.HasFlag(RigidbodyConstraints.FreezeRotationX));
            Assert.IsTrue(body.constraints.HasFlag(RigidbodyConstraints.FreezeRotationZ));
        }

        [Test]
        public void PrefabCarpismaHacmineSahip()
        {
            BoxCollider collider = prefab.GetComponent<BoxCollider>();

            Assert.IsNotNull(collider, "Prefab'da BoxCollider yok.");
            Assert.IsFalse(collider.isTrigger, "Carpisma hacmi tetikleyici olmamali.");
            Assert.Greater(collider.size.x, 0f);
            Assert.Greater(collider.size.y, 0f);
            Assert.Greater(collider.size.z, 0f);
            Assert.AreEqual(1, prefab.GetComponentsInChildren<Collider>(true).Length,
                "Carpisma hacmi tek yerde (kokte) olmali.");
        }

        [Test]
        public void PrefabHareketVeKlavyeBilesenlerineSahip()
        {
            RobotMotor motor = prefab.GetComponent<RobotMotor>();

            Assert.IsNotNull(motor, "Prefab'da RobotMotor yok.");
            Assert.Greater(motor.MaxForwardSpeed, 0f, "Ileri hiz tanimli olmali.");
            Assert.Greater(motor.MaxReverseSpeed, 0f, "Geri hiz tanimli olmali.");
            Assert.Greater(motor.MaxTurnSpeed, 0f, "Donus hizi tanimli olmali.");

            Assert.IsNotNull(prefab.GetComponent<KeyboardRobotInput>(),
                "Prefab'da KeyboardRobotInput yok.");
            Assert.IsNotNull(prefab.GetComponent<RobotCommandSequence>(),
                "Klavyesiz komut icin RobotCommandSequence yok.");
        }

        [Test]
        public void ArenaSahnesiDerlemeListesinde()
        {
            Assert.IsTrue(System.IO.File.Exists(ArenaScenePath), $"Arena sahnesi bulunamadi: {ArenaScenePath}");

            bool inBuildSettings = false;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == ArenaScenePath && scene.enabled)
                {
                    inBuildSettings = true;
                    break;
                }
            }

            Assert.IsTrue(inBuildSettings, "Arena sahnesi derleme listesinde (Build Settings) degil.");
        }
    }
}
