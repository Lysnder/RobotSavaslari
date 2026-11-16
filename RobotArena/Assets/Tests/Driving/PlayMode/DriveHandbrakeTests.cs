using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace RobotSavaslari.Driving.Tests
{
    /// <summary>
    /// A1 kabul ölçütleri: aynı başlangıç hızında aynı sürede normal dönüşten
    /// daha büyük yön değişimi ve daha düşük çıkış hızı; dönüş hızının sınırlı
    /// olması; sıfır hızda spin, bedava 180° ve hız üretimi olmaması; normal
    /// frenden farklı davranması; boost ile birleşiminde önceliğin açık olması.
    /// </summary>
    public class DriveHandbrakeTests : InputTestFixture
    {
        private Keyboard keyboard;
        private GameObject ground;
        private GameObject robot;
        private RobotMotor motor;
        private DriveHandbrake handbrake;

        public override void Setup()
        {
            base.Setup();

            keyboard = InputSystem.AddDevice<Keyboard>();

            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Zemin";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(400f, 1f, 400f);

            robot = new GameObject("TestRobot");
            robot.transform.position = new Vector3(0f, 0.05f, 0f);

            BoxCollider collider = robot.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.6f, 1.4f);
            collider.center = new Vector3(0f, 0.3f, 0f);

            Rigidbody body = robot.AddComponent<Rigidbody>();
            body.mass = 25f;
            body.centerOfMass = new Vector3(0f, 0.2f, 0f);

            motor = robot.AddComponent<RobotMotor>();
            handbrake = robot.AddComponent<DriveHandbrake>();
        }

        public override void TearDown()
        {
            if (robot != null)
            {
                Object.DestroyImmediate(robot);
            }

            if (ground != null)
            {
                Object.DestroyImmediate(ground);
            }

            base.TearDown();
        }

        [UnityTest]
        public IEnumerator ElFreniAyniHizdaDahaKeskinDondururVeHizKaybettirir()
        {
            float normalYaw = 0f;
            float normalExit = 0f;
            float brakedYaw = 0f;
            float brakedExit = 0f;

            yield return MeasureTurn(false, (yaw, exit) => { normalYaw = yaw; normalExit = exit; });
            yield return MeasureTurn(true, (yaw, exit) => { brakedYaw = yaw; brakedExit = exit; });

            TestContext.WriteLine($"Normal donus: {normalYaw:0.0} derece, cikis hizi {normalExit:0.00} m/s");
            TestContext.WriteLine($"El freni    : {brakedYaw:0.0} derece, cikis hizi {brakedExit:0.00} m/s");

            Assert.Greater(brakedYaw, normalYaw + 20f,
                "El freni ayni surede daha buyuk yon degisimi vermedi.");
            Assert.Less(brakedExit, normalExit - 1f,
                "El freninin hiz bedeli olculemedi.");
        }

        [UnityTest]
        public IEnumerator ElFreniDonusHiziniSinirlarVeBedavaDonusVermez()
        {
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2f);

            handbrake.SetHandbrakeRequested(true);
            motor.SetMoveCommand(1f, 1f);

            float limit = motor.MaxTurnSpeed * handbrake.TurnScale;
            float previousYaw = robot.transform.eulerAngles.y;
            float biggestStep = 0f;
            float elapsed = 0f;

            while (elapsed < 1.5f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                float yaw = robot.transform.eulerAngles.y;
                biggestStep = Mathf.Max(biggestStep, Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw)));
                previousYaw = yaw;

                Assert.LessOrEqual(Mathf.Abs(motor.TurnSpeed), limit + 1f,
                    "Donus hizi el freni sinirini asti.");
            }

            float stepLimit = limit * Time.fixedDeltaTime;
            TestContext.WriteLine($"Donus sinirı {limit:0} derece/s, en buyuk tek adim {biggestStep:0.00} derece");

            Assert.LessOrEqual(biggestStep, stepLimit + 1f,
                "Tek karede sinirsiz donus uretildi.");
            Assert.Less(biggestStep, 180f, "Tek karede 180 derece donus uretildi.");
        }

        [UnityTest]
        public IEnumerator SifirHizdaElFreniDevreyeGirmez()
        {
            handbrake.SetHandbrakeRequested(true);
            motor.SetMoveCommand(0f, 1f);
            yield return Simulate(1f);

            Assert.IsFalse(handbrake.IsEngaged, "Yerinde dururken el freni devreye girdi.");
            Assert.AreEqual(1f, motor.TurnModifier, 0.0001f, "Yerinde spin icin donus carpani uygulandi.");
            Assert.AreEqual(0f, motor.ForwardSpeed, 0.1f, "El freni yerinde hiz uretti.");
        }

        [UnityTest]
        public IEnumerator ElFreniHizUretmez()
        {
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2f);

            handbrake.SetHandbrakeRequested(true);
            motor.SetMoveCommand(1f, 1f);

            float cap = motor.MaxForwardSpeed * handbrake.SpeedScale;
            float elapsed = 0f;
            float peak = 0f;

            while (elapsed < 1.5f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                peak = Mathf.Max(peak, motor.ForwardSpeed);
            }

            TestContext.WriteLine($"El freni hiz tavani {cap:0.00}, olculen son hiz {motor.ForwardSpeed:0.00} m/s");

            Assert.LessOrEqual(motor.ForwardSpeed, cap + 0.2f,
                "El freni sirasinda hiz dusurulmus tavanin ustunde kaldi.");
            Assert.LessOrEqual(peak, motor.MaxForwardSpeed + 0.2f, "El freni hiz uretti.");
        }

        [UnityTest]
        public IEnumerator NormalFrenIleElFreniFarkliDavranir()
        {
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2f);

            // Normal fren: gaz bırakılır, dönüşe dokunulmaz.
            motor.Stop();
            yield return Simulate(1f);

            Assert.AreEqual(1f, motor.TurnModifier, 0.0001f, "Normal fren donusu degistirdi.");
            Assert.AreEqual(0f, motor.ForwardSpeed, 0.1f, "Normal fren robotu durdurmadi.");

            // El freni: hızın bir kısmı korunur, dönüş keskinleşir.
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2f);
            handbrake.SetHandbrakeRequested(true);
            motor.SetMoveCommand(1f, 1f);
            yield return Simulate(1f);

            Assert.IsTrue(handbrake.IsEngaged);
            Assert.AreEqual(handbrake.TurnScale, motor.TurnModifier, 0.0001f);
            Assert.Greater(motor.ForwardSpeed, 1f, "El freni robotu tamamen durdurdu; normal frenden farki kalmadi.");
        }

        [UnityTest]
        public IEnumerator BoostVeElFreniBirlikteIstendigindeElFreniOnceliklidir()
        {
            DriveBoost boost = robot.AddComponent<DriveBoost>();

            motor.SetMoveCommand(1f, 0f);
            boost.SetBoostRequested(true);
            yield return Simulate(1.5f);
            Assert.IsTrue(boost.IsBoosting, "Once boost devrede olmali.");

            float energyBefore = boost.Energy;
            handbrake.SetHandbrakeRequested(true);
            yield return Simulate(1f);

            Assert.IsTrue(handbrake.IsEngaged, "El freni devreye girmedi.");
            Assert.IsFalse(boost.IsBoosting, "El freni varken boost uygulanmaya devam etti.");
            Assert.AreEqual(handbrake.SpeedScale, motor.SpeedModifier, 0.0001f,
                "Birlesik istekte el freninin hiz carpani uygulanmadi.");
            Assert.AreEqual(handbrake.TurnScale, motor.TurnModifier, 0.0001f);
            Assert.GreaterOrEqual(boost.Energy, energyBefore,
                "El freni onceliklendirildigi halde boost enerjisi harcandi.");

            // El freni bırakılınca boost isteği hâlâ duruyorsa geri döner.
            handbrake.SetHandbrakeRequested(false);
            yield return Simulate(0.5f);
            Assert.IsTrue(boost.IsBoosting, "El freni birakilinca boost geri gelmedi.");
        }

        [UnityTest]
        public IEnumerator ElFreniBirakilincaGeciciDurumTemizlenir()
        {
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2f);

            handbrake.SetHandbrakeRequested(true);
            yield return Simulate(0.5f);
            Assert.IsTrue(handbrake.IsEngaged);

            handbrake.SetHandbrakeRequested(false);
            yield return Simulate(0.5f);

            Assert.IsFalse(handbrake.IsEngaged);
            Assert.AreEqual(1f, motor.TurnModifier, 0.0001f, "Donus carpani birakilmadi.");
            Assert.AreEqual(1f, motor.SpeedModifier, 0.0001f, "Hiz carpani birakilmadi.");
        }

        [UnityTest]
        public IEnumerator SifirlamaVeKapatmaGeciciDurumuTemizler()
        {
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2f);

            handbrake.SetHandbrakeRequested(true);
            yield return Simulate(0.5f);
            Assert.IsTrue(handbrake.IsEngaged);

            handbrake.ResetState();
            yield return null;

            Assert.IsFalse(handbrake.IsRequested, "Sifirlama istegi temizlemedi.");
            Assert.IsFalse(handbrake.IsEngaged);
            Assert.AreEqual(1f, motor.TurnModifier, 0.0001f);

            // Kapatma da aynı şekilde bırakmalı.
            handbrake.SetHandbrakeRequested(true);
            yield return Simulate(0.3f);
            handbrake.enabled = false;
            yield return null;

            Assert.IsFalse(handbrake.IsRequested);
            Assert.AreEqual(1f, motor.TurnModifier, 0.0001f);
        }

        [UnityTest]
        public IEnumerator ElFreniTusuSurucuUzerindenIstekUretir()
        {
            PlayerKeyboardDriver driver = robot.AddComponent<PlayerKeyboardDriver>();
            driver.Handbrake = handbrake;

            HoldKeys(Key.W, Key.LeftCtrl);
            yield return null;

            Assert.IsTrue(driver.HandbrakeInput, "El freni tusu okunmadi.");
            Assert.IsTrue(handbrake.IsRequested, "Istek el freni bilesenine ulasmadi.");
            Assert.AreEqual(1f, motor.MoveCommand, 0.0001f, "El freni ileri komutunu bozdu.");

            robot.SendMessage("OnApplicationFocus", false);
            yield return null;

            Assert.IsFalse(driver.HandbrakeInput);
            Assert.IsFalse(handbrake.IsRequested, "Odak kaybinda el freni istegi temizlenmedi.");
        }

        /// <summary>
        /// Aynı başlangıç hızından aynı süre dönüş yapar; dönülen açıyı ve çıkış
        /// hızını bildirir. İki ölçüm arasında robot aynı poza ve duruşa döner.
        /// </summary>
        private IEnumerator MeasureTurn(bool withHandbrake, System.Action<float, float> report)
        {
            robot.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            motor.ResetMotion();
            handbrake.SetHandbrakeRequested(false);
            yield return Simulate(0.3f);

            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2f);

            handbrake.SetHandbrakeRequested(withHandbrake);
            motor.SetMoveCommand(1f, 1f);

            float turned = 0f;
            float previousYaw = robot.transform.eulerAngles.y;
            float elapsed = 0f;

            while (elapsed < 1f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                float yaw = robot.transform.eulerAngles.y;
                turned += Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw));
                previousYaw = yaw;
            }

            report(turned, motor.ForwardSpeed);
        }

        private void HoldKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
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
