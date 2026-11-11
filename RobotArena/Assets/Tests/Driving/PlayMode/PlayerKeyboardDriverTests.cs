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
    /// A1 girdi kurallarının otomatik doğrulaması. Sanal klavye
    /// <see cref="InputTestFixture"/> ile sürülür, yani gerçek tuşa basılmadan
    /// bileşenin motora ne gönderdiği ölçülür. Fizik rotaları ayrıca Unity'de
    /// elle oynanır.
    /// </summary>
    public class PlayerKeyboardDriverTests : InputTestFixture
    {
        private Keyboard keyboard;
        private GameObject robot;
        private RobotMotor motor;
        private PlayerKeyboardDriver driver;

        public override void Setup()
        {
            base.Setup();

            keyboard = InputSystem.AddDevice<Keyboard>();

            robot = new GameObject("TestRobot");
            motor = robot.AddComponent<RobotMotor>();
            driver = robot.AddComponent<PlayerKeyboardDriver>();
        }

        public override void TearDown()
        {
            if (robot != null)
            {
                Object.DestroyImmediate(robot);
            }

            base.TearDown();
        }

        /// <summary>
        /// Klavyenin tüm durumunu tek seferde yazar: verilen tuşlar basılı,
        /// diğerleri bırakılmış olur. Karşıt tuş kuralı ancak iki tuş gerçekten
        /// aynı anda basılıyken sınanabildiği için tuşlar tek tek değil tek
        /// durum olayıyla gönderilir.
        /// </summary>
        private void HoldKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        /// <summary>Bütün tuşları bırakır.</summary>
        private void ReleaseAllKeys() => HoldKeys();

        [UnityTest]
        public IEnumerator OkTuslariIkiEkseniAyniAndaSurer()
        {
            HoldKeys(Key.UpArrow, Key.LeftArrow);
            yield return null;
            Assert.AreEqual(1f, motor.MoveCommand);
            Assert.AreEqual(-1f, motor.TurnCommand);

            HoldKeys(Key.DownArrow, Key.RightArrow);
            yield return null;
            Assert.AreEqual(-1f, motor.MoveCommand);
            Assert.AreEqual(1f, motor.TurnCommand);
        }

        [UnityTest]
        public IEnumerator IkiTusTakimiAyniYonuBirKezSayarVeKarsitYonuSifirlar()
        {
            HoldKeys(Key.W, Key.UpArrow, Key.D, Key.RightArrow);
            yield return null;
            Assert.AreEqual(1f, driver.ForwardInput);
            Assert.AreEqual(1f, driver.TurnInput);

            // W and Up are the same direction: Down cancels both together.
            HoldKeys(Key.W, Key.UpArrow, Key.DownArrow, Key.RightArrow);
            yield return null;
            Assert.AreEqual(0f, motor.MoveCommand);
            Assert.AreEqual(1f, motor.TurnCommand);

            HoldKeys(Key.UpArrow, Key.A, Key.RightArrow);
            yield return null;
            Assert.AreEqual(1f, motor.MoveCommand);
            Assert.AreEqual(0f, motor.TurnCommand);

            ReleaseAllKeys();
            yield return null;
            Assert.AreEqual(0f, motor.MoveCommand);
            Assert.AreEqual(0f, motor.TurnCommand);
        }

        [UnityTest]
        public IEnumerator KlavyeCikarilincaOkKomutlariTemizlenir()
        {
            HoldKeys(Key.UpArrow, Key.RightArrow);
            yield return null;
            Assert.AreEqual(1f, motor.MoveCommand);
            Assert.AreEqual(1f, motor.TurnCommand);

            InputSystem.RemoveDevice(keyboard);
            yield return null;
            Assert.IsFalse(driver.HasKeyboard);
            Assert.AreEqual(0f, driver.ForwardInput);
            Assert.AreEqual(0f, driver.TurnInput);
            Assert.AreEqual(0f, motor.MoveCommand);
            Assert.AreEqual(0f, motor.TurnCommand);
        }

        [UnityTest]
        public IEnumerator OrtakRobotGirdisiKarisikKarsitTuslariSifirlar()
        {
            driver.enabled = false;
            robot.AddComponent<KeyboardRobotInput>();

            HoldKeys(Key.W, Key.DownArrow, Key.D, Key.LeftArrow);
            yield return null;
            Assert.AreEqual(0f, motor.MoveCommand);
            Assert.AreEqual(0f, motor.TurnCommand);

            HoldKeys(Key.DownArrow, Key.D);
            yield return null;
            Assert.AreEqual(-1f, motor.MoveCommand);
            Assert.AreEqual(1f, motor.TurnCommand);
        }

        [UnityTest]
        public IEnumerator IleriTusuIleriKomutuGonderir()
        {
            HoldKeys(Key.W);
            yield return null;

            Assert.AreEqual(1f, driver.ForwardInput, 0.0001f);
            Assert.AreEqual(1f, motor.MoveCommand, 0.0001f);
            Assert.AreEqual(0f, motor.TurnCommand, 0.0001f);
        }

        [UnityTest]
        public IEnumerator GeriTusuGeriKomutuGonderir()
        {
            HoldKeys(Key.S);
            yield return null;

            Assert.AreEqual(-1f, driver.ForwardInput, 0.0001f);
            Assert.AreEqual(-1f, motor.MoveCommand, 0.0001f);
        }

        [UnityTest]
        public IEnumerator DonusTuslariIkiYoneCalisir()
        {
            HoldKeys(Key.D);
            yield return null;
            Assert.AreEqual(1f, motor.TurnCommand, 0.0001f, "D saga donus gondermedi.");

            HoldKeys(Key.A);
            yield return null;
            Assert.AreEqual(-1f, motor.TurnCommand, 0.0001f, "A sola donus gondermedi.");
        }

        [UnityTest]
        public IEnumerator IleriyleBirlikteDonusYapilabilir()
        {
            HoldKeys(Key.W, Key.D);
            yield return null;

            Assert.AreEqual(1f, motor.MoveCommand, 0.0001f);
            Assert.AreEqual(1f, motor.TurnCommand, 0.0001f);
        }

        [UnityTest]
        public IEnumerator KarsitTuslarIleriGeriEkseniniSifirlar()
        {
            HoldKeys(Key.W, Key.S);
            yield return null;

            Assert.AreEqual(0f, driver.ForwardInput, 0.0001f, "W+S ileri/geri eksenini sifirlamadi.");
            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f);
        }

        [UnityTest]
        public IEnumerator KarsitTuslarDonusEkseniniSifirlar()
        {
            HoldKeys(Key.A, Key.D);
            yield return null;

            Assert.AreEqual(0f, driver.TurnInput, 0.0001f, "A+D donus eksenini sifirlamadi.");
            Assert.AreEqual(0f, motor.TurnCommand, 0.0001f);
        }

        [UnityTest]
        public IEnumerator KarsitTuslarDigerEkseniEtkilemez()
        {
            HoldKeys(Key.W, Key.A, Key.D);
            yield return null;

            Assert.AreEqual(1f, motor.MoveCommand, 0.0001f, "Donus sifirlamasi ileri komutunu da kesti.");
            Assert.AreEqual(0f, motor.TurnCommand, 0.0001f);
        }

        [UnityTest]
        public IEnumerator TusBirakilincaKomutSifirlanir()
        {
            HoldKeys(Key.W, Key.D);
            yield return null;
            Assert.AreEqual(1f, motor.MoveCommand, 0.0001f);

            ReleaseAllKeys();
            yield return null;

            Assert.AreEqual(0f, driver.ForwardInput, 0.0001f);
            Assert.AreEqual(0f, driver.TurnInput, 0.0001f);
            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f, "Tus birakildiginda komut sifirlanmadi.");
            Assert.AreEqual(0f, motor.TurnCommand, 0.0001f);
        }

        [UnityTest]
        public IEnumerator KontrolKapaliykenGirdiMotoraUlasmaz()
        {
            HoldKeys(Key.W);
            yield return null;
            Assert.AreEqual(1f, motor.MoveCommand, 0.0001f);

            driver.enabled = false;
            yield return null;

            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f, "Kontrol kapatilinca komut birakilmadi.");

            HoldKeys(Key.S);
            yield return null;

            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f, "Kapali kontrol hala komut gonderiyor.");
        }

        [UnityTest]
        public IEnumerator KontrolTekrarAcilincaEskiDegerKullanilmaz()
        {
            HoldKeys(Key.W);
            yield return null;

            driver.enabled = false;
            yield return null;

            ReleaseAllKeys();
            driver.enabled = true;
            yield return null;

            Assert.AreEqual(0f, driver.ForwardInput, 0.0001f, "Eski girdi degeri korunmus.");
            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f, "Eski komut tekrar gonderilmis.");
        }

        [UnityTest]
        public IEnumerator DenemeKontroluKlavyesizSurer()
        {
            DriveProbe probe = robot.AddComponent<DriveProbe>();

            HoldKeys(Key.W);
            yield return null;

            probe.StartDriving();
            yield return null;

            Assert.IsTrue(probe.IsDriving);
            Assert.IsFalse(driver.enabled, "Deneme kontrolu klavye bilesenini kapatmadi.");
            Assert.AreEqual(probe.ProbeForward, motor.MoveCommand, 0.0001f,
                "Deneme kontrolu motoru suremedi.");
            Assert.AreEqual(probe.ProbeTurn, motor.TurnCommand, 0.0001f);

            probe.StopDriving();
            yield return null;

            Assert.IsTrue(driver.enabled, "Deneme kontrolu bitince klavye geri acilmadi.");
        }
    }
}
