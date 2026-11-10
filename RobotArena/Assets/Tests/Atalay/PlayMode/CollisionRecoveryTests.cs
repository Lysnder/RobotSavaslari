using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace RobotSavaslari.Atalay.Tests
{
    /// <summary>
    /// A3 ölçütleri: duvar ve köşe senaryolarında robotun dışarı geçmemesi,
    /// çarpışmadan sonra geri sürüşle kurtulabilmesi, odak kaybında komutun
    /// temizlenmesi ve kontrol kapatılıp açıldığında eski değerin kalmaması.
    /// </summary>
    public class CollisionRecoveryTests : InputTestFixture
    {
        private const float WallFace = 8f;

        private readonly List<GameObject> spawned = new List<GameObject>();

        private Keyboard keyboard;
        private GameObject robot;
        private RobotMotor motor;
        private PlayerKeyboardDriver driver;
        private CollisionReporter reporter;

        public override void Setup()
        {
            base.Setup();

            keyboard = InputSystem.AddDevice<Keyboard>();

            CreateBlock("Zemin", new Vector3(0f, -0.5f, 0f), new Vector3(200f, 1f, 200f));

            robot = new GameObject("TestRobot");
            spawned.Add(robot);
            robot.transform.position = new Vector3(0f, 0.05f, 0f);

            BoxCollider collider = robot.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.6f, 1.4f);
            collider.center = new Vector3(0f, 0.3f, 0f);

            Rigidbody body = robot.AddComponent<Rigidbody>();
            body.mass = 25f;
            body.centerOfMass = new Vector3(0f, 0.2f, 0f);

            motor = robot.AddComponent<RobotMotor>();
            driver = robot.AddComponent<PlayerKeyboardDriver>();
            reporter = robot.AddComponent<CollisionReporter>();
        }

        public override void TearDown()
        {
            foreach (GameObject instance in spawned)
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }
            }

            spawned.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator OndenCarpismaSonrasiGeriSurusleCikilir()
        {
            CreateWall("Duvar", new Vector3(0f, 0.75f, WallFace + 0.5f), new Vector3(30f, 1.5f, 1f));

            driver.enabled = false;
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(4f);

            float stuckZ = robot.transform.position.z;
            Assert.Less(stuckZ, WallFace, $"Robot duvardan gecti (z = {stuckZ:0.###}).");
            Assert.IsTrue(reporter.HasReport, "Carpisma kaydedilmedi.");
            TestContext.WriteLine(
                $"Onden carpisma: {reporter.ImpactSpeed:0.00} m/s, {reporter.ImpactAngle:0} derece, " +
                $"kalan {reporter.ResidualSpeed:0.00} m/s");

            motor.SetMoveCommand(-1f, 0f);
            yield return Simulate(2f);

            Assert.Less(robot.transform.position.z, stuckZ - 1f,
                "Carpismadan sonra geri surusle uzaklasilamadi.");
        }

        [UnityTest]
        public IEnumerator AciliCarpismaSonrasiYonDuzeltilebilir()
        {
            CreateWall("Duvar", new Vector3(0f, 0.75f, WallFace + 0.5f), new Vector3(30f, 1.5f, 1f), yaw: 30f);

            driver.enabled = false;
            robot.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(4f);

            Assert.Less(robot.transform.position.z, WallFace + 1f,
                $"Robot acili duvardan gecti (z = {robot.transform.position.z:0.###}).");

            // Geri gel ve yönü değiştir: kontrol çarpışmadan sonra da çalışmalı.
            motor.SetMoveCommand(-1f, 0f);
            yield return Simulate(1.5f);

            float yawBefore = robot.transform.eulerAngles.y;
            motor.SetMoveCommand(0f, -1f);
            yield return Simulate(1f);

            float turned = Mathf.Abs(Mathf.DeltaAngle(yawBefore, robot.transform.eulerAngles.y));
            Assert.Greater(turned, 20f, "Carpismadan sonra robot donemedi.");
        }

        [UnityTest]
        public IEnumerator KoseyeSurulenRobotGeriCikabilir()
        {
            CreateWall("Kose dip", new Vector3(0f, 0.75f, WallFace + 0.5f), new Vector3(30f, 1.5f, 1f));
            CreateWall("Kose yan", new Vector3(WallFace + 0.5f, 0.75f, 0f), new Vector3(1f, 1.5f, 30f));

            driver.enabled = false;
            robot.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(5f);

            Assert.Less(robot.transform.position.z, WallFace, "Robot kose duvarindan gecti (z).");
            Assert.Less(robot.transform.position.x, WallFace, "Robot kose duvarindan gecti (x).");
            Assert.Less(robot.transform.position.y, 0.5f, "Robot duvara tirmandi.");

            Vector3 corner = new Vector3(WallFace, 0f, WallFace);
            float distanceBefore = Vector3.Distance(robot.transform.position, corner);

            motor.SetMoveCommand(-1f, 0f);
            yield return Simulate(2f);

            float distanceAfter = Vector3.Distance(robot.transform.position, corner);
            Assert.Greater(distanceAfter, distanceBefore + 1f, "Kosede kalan robot geri cikamadi.");
        }

        [UnityTest]
        public IEnumerator DuvarBoyuncaSurusteKontrolKaybolmaz()
        {
            CreateWall("Duvar", new Vector3(2f, 0.75f, 0f), new Vector3(1f, 1.5f, 60f));

            driver.enabled = false;
            robot.transform.position = new Vector3(0.9f, 0.05f, -10f);
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(4f);

            Assert.Greater(robot.transform.position.z, -2f,
                "Duvara yaslanan robot ilerleyemedi; surtunme kontrolu kilitliyor.");
            Assert.Less(robot.transform.position.x, 2f, "Robot duvarin icine girdi.");
        }

        [UnityTest]
        public IEnumerator OdakKaybindaKomutTemizlenir()
        {
            HoldKeys(Key.W);
            yield return null;
            Assert.AreEqual(1f, motor.MoveCommand, 0.0001f);

            // Pencere arkaya düştüğünde Unity bu geri çağrıyı yollar.
            robot.SendMessage("OnApplicationFocus", false);
            yield return null;

            Assert.IsFalse(driver.HasFocus);
            Assert.AreEqual(0f, driver.ForwardInput, 0.0001f, "Odak disinda girdi okunmaya devam etti.");
            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f, "Odak kaybinda komut temizlenmedi.");
        }

        [UnityTest]
        public IEnumerator OdakGeriGelinceTusBasilmadanKomutGelmez()
        {
            HoldKeys(Key.W);
            yield return null;

            robot.SendMessage("OnApplicationFocus", false);
            yield return null;

            ReleaseAllKeys();
            robot.SendMessage("OnApplicationFocus", true);
            yield return null;

            Assert.IsTrue(driver.HasFocus);
            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f, "Odak geri gelince eski komut uygulandi.");
        }

        [UnityTest]
        public IEnumerator HareketsizVeHizliRobotAyriAyriDurdurulur()
        {
            driver.enabled = false;

            // Hareketsiz robot: komut sıfırlanınca hiç hareket etmemeli.
            motor.Stop();
            yield return Simulate(0.5f);
            Vector3 restingStart = robot.transform.position;
            yield return Simulate(0.5f);
            Assert.AreEqual(restingStart.z, robot.transform.position.z, 0.02f);

            // Hızlı giden robot: komut kesilince kalan hareket ölçülür.
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2.5f);

            Vector3 releasePosition = robot.transform.position;
            motor.Stop();
            yield return null;

            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f, "Komut kesilmedi.");

            yield return Simulate(1.5f);
            float coast = Vector3.Distance(releasePosition, robot.transform.position);
            TestContext.WriteLine($"Komut kesildikten sonra kalan hareket: {coast:0.00} m");

            Assert.Less(coast, 3f, "Komut kesildikten sonra robot beklenenden uzaga kaydi.");
            Assert.AreEqual(0f, motor.ForwardSpeed, 0.05f, "Robot durmadi.");
        }

        private void HoldKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        private void ReleaseAllKeys() => HoldKeys();

        private GameObject CreateBlock(string name, Vector3 position, Vector3 scale, float yaw = 0f)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            block.transform.localScale = scale;
            spawned.Add(block);

            return block;
        }

        private GameObject CreateWall(string name, Vector3 position, Vector3 scale, float yaw = 0f)
        {
            return CreateBlock(name, position, scale, yaw);
        }

        private static IEnumerator Simulate(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
        }
    }
}
