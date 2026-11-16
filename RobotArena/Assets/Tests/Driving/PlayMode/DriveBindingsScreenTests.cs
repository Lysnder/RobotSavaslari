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
    /// A2 ekran davranışı: yakalama sırasında oyuna komut gitmemesi, yakalanan
    /// tuşun sürücüye uygulanması, çakışmanın reddedilmesi, iptal, maç
    /// başlayınca kapanma ve varsayılana dönüş.
    /// </summary>
    public class DriveBindingsScreenTests : InputTestFixture
    {
        private static readonly DriveAction[] AllActions =
        {
            DriveAction.Forward,
            DriveAction.Backward,
            DriveAction.TurnLeft,
            DriveAction.TurnRight,
            DriveAction.Boost,
            DriveAction.Handbrake,
        };

        private Keyboard keyboard;
        private GameObject ground;
        private GameObject robot;
        private RobotMotor motor;
        private PlayerKeyboardDriver driver;
        private DriveBindingsScreen screen;

        public override void Setup()
        {
            base.Setup();

            ClearStoredBindings();
            DriveBindings.ResetToDefaults();

            keyboard = InputSystem.AddDevice<Keyboard>();

            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Zemin";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);

            robot = new GameObject("TestRobot");
            robot.transform.position = new Vector3(0f, 0.05f, 0f);

            BoxCollider collider = robot.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.6f, 1.4f);
            collider.center = new Vector3(0f, 0.3f, 0f);

            Rigidbody body = robot.AddComponent<Rigidbody>();
            body.mass = 25f;

            motor = robot.AddComponent<RobotMotor>();
            driver = robot.AddComponent<PlayerKeyboardDriver>();
            screen = robot.AddComponent<DriveBindingsScreen>();
            screen.Driver = driver;
            screen.AccessAllowed = true;

            DriveBindings.Apply(driver);
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

            ClearStoredBindings();
            DriveBindings.ResetToDefaults();
            base.TearDown();
        }

        private static void ClearStoredBindings()
        {
            foreach (DriveAction action in AllActions)
            {
                PlayerPrefs.DeleteKey("Settings.Driving." + action);
            }

            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator EkranAcilincaKontrolKapanirKapanincaDoner()
        {
            Assert.IsTrue(driver.enabled);

            screen.Open();
            yield return null;

            Assert.IsTrue(screen.IsOpen);
            Assert.IsFalse(driver.enabled, "Ekran acikken surus kontrolu kapanmadi.");

            screen.Close();
            yield return null;

            Assert.IsFalse(screen.IsOpen);
            Assert.IsTrue(driver.enabled, "Ekran kapaninca kontrol geri gelmedi.");
        }

        [UnityTest]
        public IEnumerator YakalamaSirasindaOyunaKomutGitmez()
        {
            screen.Open();
            screen.BeginCapture(DriveAction.Boost);
            yield return null;

            HoldKeys(Key.W);
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f,
                "Yakalama sirasinda basilan tus oyuna komut olarak gitti.");
        }

        [UnityTest]
        public IEnumerator YakalananTusEslemeyiDegistirirVeSurucuyeUygulanir()
        {
            screen.Open();
            screen.BeginCapture(DriveAction.Forward);
            yield return null;

            Assert.IsTrue(screen.TryCapture(Key.I));

            Assert.AreEqual(Key.I, DriveBindings.Forward);
            Assert.AreEqual(Key.I, driver.GetBinding(DriveAction.Forward),
                "Yeni esleme surucuye uygulanmadi.");

            // Ekran kapanınca yeni tuş gerçekten sürüyor mu?
            screen.Close();
            yield return null;

            HoldKeys(Key.I);
            yield return null;

            Assert.AreEqual(1f, motor.MoveCommand, 0.0001f, "Yeni esleme ile ileri gidilemedi.");
        }

        [UnityTest]
        public IEnumerator CakisanTusReddedilirVeMesajGosterilir()
        {
            screen.Open();
            screen.BeginCapture(DriveAction.Forward);
            yield return null;

            Assert.IsFalse(screen.TryCapture(Key.D), "Cakisan tus kabul edildi.");
            Assert.AreEqual(Key.W, driver.GetBinding(DriveAction.Forward));
            StringAssert.Contains("saga donus", screen.Message);
        }

        [UnityTest]
        public IEnumerator EscYakalamayiIptalEder()
        {
            screen.Open();
            screen.BeginCapture(DriveAction.Forward);
            yield return null;

            Assert.IsFalse(screen.TryCapture(Key.Escape));
            Assert.IsNull(screen.Capturing, "Esc yakalamayi iptal etmedi.");
            Assert.AreEqual(Key.W, driver.GetBinding(DriveAction.Forward));
        }

        [UnityTest]
        public IEnumerator ErisimKapanincaEkranKapanir()
        {
            screen.Open();
            yield return null;
            Assert.IsTrue(screen.IsOpen);

            // Maç başladı ya da ayarlar açıldı.
            screen.AccessAllowed = false;
            yield return null;

            Assert.IsFalse(screen.IsOpen, "Erisim kapandiginda ekran kapanmadi.");
            Assert.IsTrue(driver.enabled, "Kontrol geri gelmedi.");
        }

        [UnityTest]
        public IEnumerator VarsayilanaDonusEslemeleriGeriGetirir()
        {
            screen.Open();
            screen.BeginCapture(DriveAction.Forward);
            yield return null;
            Assert.IsTrue(screen.TryCapture(Key.I));

            screen.ResetToDefaults();
            yield return null;

            Assert.AreEqual(Key.W, DriveBindings.Forward);
            Assert.AreEqual(Key.W, driver.GetBinding(DriveAction.Forward));
        }

        [UnityTest]
        public IEnumerator OkTuslariEslemeDegissseDeCalisir()
        {
            screen.Open();
            screen.BeginCapture(DriveAction.Forward);
            yield return null;
            Assert.IsTrue(screen.TryCapture(Key.I));
            screen.Close();
            yield return null;

            // Birincil tuş değişti ama ok takımı korunuyor.
            HoldKeys(Key.UpArrow);
            yield return null;

            Assert.AreEqual(1f, motor.MoveCommand, 0.0001f,
                "Esleme degisince ok tusu calismayi birakti.");
        }

        private void HoldKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }
    }
}
