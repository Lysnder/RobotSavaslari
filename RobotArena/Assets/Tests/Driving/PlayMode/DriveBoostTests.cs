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
    /// A0 kabul ölçütleri: boost aynı koşulda normalden ölçülebilir hızlı,
    /// enerji harcıyor, bırakılınca normal sınır geri geliyor ve enerji
    /// yenileniyor. Enerjinin negatif ya da kapasite üstü olmaması, boş
    /// enerjide basılı tuşun boost üretmemesi ve botun motor davranışının
    /// değişmemesi de burada ölçülür.
    /// </summary>
    public class DriveBoostTests : InputTestFixture
    {
        private Keyboard keyboard;
        private GameObject ground;
        private GameObject robot;
        private RobotMotor motor;
        private DriveBoost boost;

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

        private DriveBoost AttachBoost()
        {
            boost = robot.AddComponent<DriveBoost>();
            return boost;
        }

        [UnityTest]
        public IEnumerator BoostAyniKosuldaNormaldenHizlidir()
        {
            AttachBoost();

            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2f);
            float normalSpeed = motor.ForwardSpeed;

            boost.SetBoostRequested(true);
            yield return Simulate(2f);
            float boostedSpeed = motor.ForwardSpeed;

            TestContext.WriteLine($"Normal hiz: {normalSpeed:0.00} m/s, boost: {boostedSpeed:0.00} m/s");

            Assert.Greater(boostedSpeed, normalSpeed + 0.5f,
                "Boost ayni kosulda olculebilir sekilde hizlandirmadi.");
            Assert.IsTrue(boost.IsBoosting);
        }

        [UnityTest]
        public IEnumerator BoostBirakilincaNormalSinirGeriGelir()
        {
            AttachBoost();

            motor.SetMoveCommand(1f, 0f);
            boost.SetBoostRequested(true);
            yield return Simulate(2f);

            float boostedSpeed = motor.ForwardSpeed;
            Assert.Greater(boostedSpeed, motor.MaxForwardSpeed + 0.5f);

            boost.SetBoostRequested(false);
            yield return Simulate(2f);

            Assert.IsFalse(boost.IsBoosting);
            Assert.AreEqual(1f, motor.SpeedModifier, 0.0001f, "Gecici carpan birakilmadi.");
            Assert.AreEqual(motor.MaxForwardSpeed, motor.ForwardSpeed, 0.2f,
                "Boost birakildiktan sonra normal ust hiza donulmedi.");
        }

        [UnityTest]
        public IEnumerator BoostEnerjiHarcarVeSonraYenilenir()
        {
            AttachBoost();
            float startingEnergy = boost.Energy;

            motor.SetMoveCommand(1f, 0f);
            boost.SetBoostRequested(true);
            yield return Simulate(1f);

            float drained = boost.Energy;
            Assert.Less(drained, startingEnergy - 10f, "Boost enerji harcamadi.");

            boost.SetBoostRequested(false);
            yield return Simulate(2.5f);

            TestContext.WriteLine($"Baslangic {startingEnergy:0.0} -> boost sonrasi {drained:0.0} -> dolum {boost.Energy:0.0}");
            Assert.Greater(boost.Energy, drained + 5f, "Enerji yenilenmedi.");
        }

        [UnityTest]
        public IEnumerator EnerjiBitinceBasiliBoostDurur()
        {
            AttachBoost();

            motor.SetMoveCommand(1f, 0f);
            boost.SetBoostRequested(true);

            // Kapasite 100, tüketim 40/s: üç saniyeden önce biter.
            yield return Simulate(4f);

            Assert.AreEqual(0f, boost.Energy, 0.0001f, "Enerji negatife dustu ya da bitmedi.");
            Assert.IsFalse(boost.IsBoosting, "Enerji bittigi halde boost suruyor.");
            Assert.IsTrue(boost.IsRequested, "Tus hala basili sayilmali.");
            Assert.AreEqual(1f, motor.SpeedModifier, 0.0001f, "Bos enerjide carpan birakilmadi.");

            // Basılı tuşla titremesin: eşik dolana kadar yeniden başlamaz.
            yield return Simulate(0.1f);
            Assert.IsFalse(boost.IsBoosting);
        }

        [UnityTest]
        public IEnumerator EnerjiKapasiteyiAsmaz()
        {
            AttachBoost();

            // Önce harca, sonra dolum için fazlasıyla bekle: üst sınır tutmalı.
            motor.SetMoveCommand(1f, 0f);
            boost.SetBoostRequested(true);
            yield return Simulate(1f);
            Assert.Less(boost.Energy, boost.Capacity);

            boost.SetBoostRequested(false);
            yield return Simulate(6f);

            Assert.AreEqual(boost.Capacity, boost.Energy, 0.0001f,
                "Enerji kapasitenin ustune cikti ya da dolmadi.");
        }

        [UnityTest]
        public IEnumerator GeriSurusteBoostUygulanmaz()
        {
            AttachBoost();
            float startingEnergy = boost.Energy;

            motor.SetMoveCommand(-1f, 0f);
            boost.SetBoostRequested(true);
            yield return Simulate(2f);

            Assert.IsFalse(boost.IsBoosting, "Geri viteste boost devreye girdi.");
            Assert.AreEqual(startingEnergy, boost.Energy, 0.0001f, "Geri viteste enerji harcandi.");
            Assert.AreEqual(1f, motor.SpeedModifier, 0.0001f);
            Assert.AreEqual(-motor.MaxReverseSpeed, motor.ForwardSpeed, 0.2f,
                "Geri hiz tasarlanan sinirin disina cikti.");
        }

        [UnityTest]
        public IEnumerator SifirlamaEnerjiyiVeCarpaniTemizler()
        {
            AttachBoost();

            motor.SetMoveCommand(1f, 0f);
            boost.SetBoostRequested(true);
            yield return Simulate(1.5f);
            Assert.Less(boost.Energy, boost.Capacity);

            boost.ResetState();
            yield return null;

            Assert.AreEqual(boost.Capacity, boost.Energy, 0.0001f, "Enerji baslangica donmedi.");
            Assert.IsFalse(boost.IsBoosting);
            Assert.IsFalse(boost.IsRequested, "Aktif komut temizlenmedi.");
            Assert.AreEqual(1f, motor.SpeedModifier, 0.0001f);
        }

        [UnityTest]
        public IEnumerator HasarKosuluBoostlaBirlikteGecerliKalir()
        {
            AttachBoost();

            // Hasarlı robot: hareket kabiliyeti yarıya düşmüş.
            motor.SetDriveCondition(0.5f, 1f, 0f, 1f);
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2f);
            float damagedSpeed = motor.ForwardSpeed;

            // Enerji penceresi içinde kal: 40/s tüketimle 1.5 saniye güvenli.
            boost.SetBoostRequested(true);
            yield return Simulate(1.5f);
            float boostedSpeed = motor.ForwardSpeed;

            TestContext.WriteLine($"Hasarli {damagedSpeed:0.00} m/s -> hasarli+boost {boostedSpeed:0.00} m/s");
            TestContext.WriteLine($"Hasarli ust hiz {motor.MaxForwardSpeed * 0.5f:0.00} -> boostlu {motor.EffectiveMaxForwardSpeed:0.00} m/s");

            Assert.IsTrue(boost.IsBoosting, "Boost enerji penceresi icinde devrede olmali.");
            Assert.AreEqual(1.5f, motor.SpeedModifier, 0.0001f);

            // Boost hasar oranını ezmez, üstüne biner: 6 * 0.5 * 1.5 = 4.5
            Assert.AreEqual(4.5f, motor.EffectiveMaxForwardSpeed, 0.0001f);
            Assert.Less(motor.EffectiveMaxForwardSpeed, motor.MaxForwardSpeed * motor.SpeedModifier,
                "Hasar kosulu boostun altinda kaybolmus.");

            // Ulaşılan hız üzerinden ölçmüyoruz: hasarlı ivme ortak motordaki
            // sürtünme tabanının altında kaldığı için fiziksel hız üst sınıra
            // çıkamıyor. Bulgu teslim notunda Emin'e bildirildi.
            Assert.Greater(boostedSpeed, damagedSpeed, "Boost hasarli robotu hizlandirmadi.");
        }

        [UnityTest]
        public IEnumerator BoostsuzMotorEskiDavranisiKorur()
        {
            // Varsayılan bot: üzerinde DriveBoost yok.
            Assert.IsNull(robot.GetComponent<DriveBoost>());

            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(2.5f);

            Assert.AreEqual(1f, motor.SpeedModifier, 0.0001f);
            Assert.AreEqual(1f, motor.AccelerationModifier, 0.0001f);
            Assert.AreEqual(motor.MaxForwardSpeed, motor.ForwardSpeed, 0.2f,
                "Boostsuz motorun ust hizi degisti.");
        }

        [UnityTest]
        public IEnumerator BoostTusuSurucuUzerindenIstekUretir()
        {
            AttachBoost();
            PlayerKeyboardDriver driver = robot.AddComponent<PlayerKeyboardDriver>();
            driver.Boost = boost;

            HoldKeys(Key.W, Key.LeftShift);
            yield return null;

            Assert.IsTrue(driver.BoostInput, "Boost tusu okunmadi.");
            Assert.IsTrue(boost.IsRequested, "Istek boost bilesenine ulasmadi.");
            Assert.AreEqual(1f, motor.MoveCommand, 0.0001f, "W+boost ileri komutunu bozdu.");

            // Odak kaybında basılı boost sürmemeli.
            robot.SendMessage("OnApplicationFocus", false);
            yield return null;

            Assert.IsFalse(driver.BoostInput);
            Assert.IsFalse(boost.IsRequested, "Odak kaybinda boost istegi temizlenmedi.");
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
