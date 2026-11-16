using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Driving;
using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Training.Tests
{
    /// <summary>
    /// A3 ölçütleri: kontrol noktalarının yalnızca doğru sırada ilerlemesi,
    /// yanlış sıralı turun geçersiz kalması, bitişin yalnızca bir kez
    /// raporlanması, sürenin işlemesi, yeniden denemenin her şeyi sıfırlaması
    /// ve çıkışta saklanan durumun geri yüklenmesi.
    ///
    /// Oturum maç bileşenleri olmadan da çalışır (hepsi null kontrollüdür), bu
    /// yüzden testler gerçek robotu ve gerçek fizik dünyasını kullanıp maç
    /// kurulumunu taklit etmez.
    /// </summary>
    public class TimeTrialSessionTests
    {
        private GameObject ground;
        private GameObject robot;
        private GameObject host;
        private RobotMotor motor;
        private PlayerKeyboardDriver driver;
        private TimeTrialSession session;

        private static readonly TimeTrialCheckpoint[] Route =
        {
            new TimeTrialCheckpoint { position = new Vector3(0f, 0f, 3f), radius = 1f },
            new TimeTrialCheckpoint { position = new Vector3(3f, 0f, 3f), radius = 1f },
            new TimeTrialCheckpoint { position = new Vector3(3f, 0f, -3f), radius = 1f },
        };

        [SetUp]
        public void SetUp()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Zemin";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);

            robot = new GameObject("TestRobot");
            robot.transform.position = new Vector3(-4f, 0.05f, -3f);

            BoxCollider collider = robot.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.6f, 1.4f);
            collider.center = new Vector3(0f, 0.3f, 0f);

            Rigidbody body = robot.AddComponent<Rigidbody>();
            body.mass = 25f;

            motor = robot.AddComponent<RobotMotor>();
            driver = robot.AddComponent<PlayerKeyboardDriver>();

            host = new GameObject("Egitim");
            session = host.AddComponent<TimeTrialSession>();
            session.ConfigureRoute(new Vector3(-4f, 0f, -3f), 0f, Route);
            session.Bind(null, null, null, driver);
            session.CanEnter = true;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject instance in new[] { host, robot, ground })
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        /// <summary>Robotu bir kontrol noktasının içine taşır.</summary>
        private IEnumerator MoveTo(Vector3 position)
        {
            motor.ResetMotion();
            Rigidbody body = motor.Body;
            Vector3 target = new Vector3(position.x, body.position.y, position.z);
            body.position = target;
            robot.transform.position = target;

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EgitimeGirisKontroluAcarVeBaslangicaKoyar()
        {
            driver.enabled = false;

            Assert.IsTrue(session.Enter(), "Egitime girilemedi.");
            yield return null;

            Assert.AreEqual(TimeTrialState.Running, session.State);
            Assert.IsTrue(driver.enabled, "Egitimde surus kontrolu acilmadi.");
            Assert.AreEqual(0, session.NextCheckpointIndex);
            Assert.AreEqual(session.StartPosition.x, robot.transform.position.x, 0.1f);
            Assert.AreEqual(session.StartPosition.z, robot.transform.position.z, 0.1f);
        }

        [UnityTest]
        public IEnumerator GirisIzniYokkenGirilmez()
        {
            session.CanEnter = false;

            Assert.IsFalse(session.Enter(), "Izin yokken egitime girildi.");
            Assert.AreEqual(TimeTrialState.Inactive, session.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator KontrolNoktalariYalnizSirayilaIlerler()
        {
            session.Enter();
            yield return null;

            // İkinci noktaya önce gitmek ilerletmemeli.
            yield return MoveTo(Route[1].position);
            Assert.AreEqual(0, session.NextCheckpointIndex, "Sira atlanarak ilerlendi.");
            Assert.IsFalse(session.LapValid, "Yanlis sira turu gecersiz kilmadi.");
            StringAssert.Contains("Yanlis sira", session.Notice);

            // Doğru nokta ilerletir.
            yield return MoveTo(Route[0].position);
            Assert.AreEqual(1, session.NextCheckpointIndex, "Dogru nokta ilerletmedi.");
        }

        [UnityTest]
        public IEnumerator SirayilaGecilenTurTamamlanirVeBitisBirKezRaporlanir()
        {
            session.Enter();
            yield return null;

            foreach (TimeTrialCheckpoint checkpoint in Route)
            {
                yield return MoveTo(checkpoint.position);
            }

            Assert.AreEqual(TimeTrialState.Finished, session.State);
            Assert.IsTrue(session.LastLapValid, "Sirayla gecilen tur gecersiz sayildi.");
            Assert.Greater(session.LastLapTime, 0f, "Tur suresi olculmedi.");

            float recorded = session.LastLapTime;
            yield return MoveTo(Route[Route.Length - 1].position);
            yield return null;

            Assert.AreEqual(TimeTrialState.Finished, session.State);
            Assert.AreEqual(recorded, session.LastLapTime, 0.0001f,
                "Bitis ikinci kez raporlandi.");
        }

        [UnityTest]
        public IEnumerator YanlisSiraliTurGecersizKalir()
        {
            session.Enter();
            yield return null;

            yield return MoveTo(Route[1].position);
            foreach (TimeTrialCheckpoint checkpoint in Route)
            {
                yield return MoveTo(checkpoint.position);
            }

            Assert.AreEqual(TimeTrialState.Finished, session.State);
            Assert.IsFalse(session.LastLapValid, "Yanlis sirali tur gecerli sayildi.");
        }

        [UnityTest]
        public IEnumerator SureIlerler()
        {
            session.Enter();
            yield return null;

            float first = session.ElapsedTime;
            yield return new WaitForSeconds(0.3f);

            Assert.Greater(session.ElapsedTime, first, "Sure ilerlemedi.");
        }

        [UnityTest]
        public IEnumerator YenidenDenemeHerSeyiSifirlar()
        {
            session.Enter();
            yield return null;

            yield return MoveTo(Route[0].position);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(1, session.NextCheckpointIndex);

            session.Retry();
            yield return null;

            Assert.AreEqual(TimeTrialState.Running, session.State);
            Assert.AreEqual(0, session.NextCheckpointIndex, "Kontrol noktalari sifirlanmadi.");
            Assert.Less(session.ElapsedTime, 0.2f, "Sure sifirlanmadi.");
            Assert.IsTrue(session.LapValid);
            Assert.AreEqual(session.StartPosition.x, robot.transform.position.x, 0.1f);
            Assert.AreEqual(0f, motor.ForwardSpeed, 0.05f, "Hareket sifirlanmadi.");
        }

        [UnityTest]
        public IEnumerator CikisSaklananKontrolDurumunuGeriYukler()
        {
            // Giriş öncesi kontrol kapalıydı: çıkışta yine kapalı olmalı.
            driver.enabled = false;

            session.Enter();
            yield return null;
            Assert.IsTrue(driver.enabled);

            session.Exit();
            yield return null;

            Assert.AreEqual(TimeTrialState.Inactive, session.State);
            Assert.IsFalse(driver.enabled, "Cikista saklanan kontrol durumu geri yuklenmedi.");
            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f, "Cikista komut birakilmadi.");
        }

        [UnityTest]
        public IEnumerator BitisSonrasiSureIlerlemez()
        {
            session.Enter();
            yield return null;

            foreach (TimeTrialCheckpoint checkpoint in Route)
            {
                yield return MoveTo(checkpoint.position);
            }

            float finished = session.ElapsedTime;
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(finished, session.ElapsedTime, 0.0001f, "Bitisten sonra sure islemeye devam etti.");
        }
    }
}
