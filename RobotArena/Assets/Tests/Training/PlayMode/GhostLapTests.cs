using System.Collections;
using System.IO;
using NUnit.Framework;
using RobotSavaslari.Driving;
using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Training.Tests
{
    /// <summary>
    /// A4 ölçütleri: ilk geçerli turun kaydedilmesi, yalnızca daha iyi geçerli
    /// turun rekoru güncellemesi, bozuk/eksik ya da başka parkura ait kaydın
    /// güvenle atlanması ve hayaletin fizik/dövüş bileşeni taşımaması.
    /// </summary>
    public class GhostLapTests
    {
        private GameObject ground;
        private GameObject robot;
        private GameObject host;
        private RobotMotor motor;
        private TimeTrialSession session;
        private GhostRunner runner;

        private static readonly TimeTrialCheckpoint[] Route =
        {
            new TimeTrialCheckpoint { position = new Vector3(0f, 0f, 3f), radius = 1f },
            new TimeTrialCheckpoint { position = new Vector3(3f, 0f, -3f), radius = 1f },
        };

        [SetUp]
        public void SetUp()
        {
            GhostLapStore.Clear();

            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(200f, 1f, 200f);

            robot = new GameObject("TestRobot");
            robot.transform.position = new Vector3(-4f, 0.05f, -3f);

            BoxCollider collider = robot.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.6f, 1.4f);

            Rigidbody body = robot.AddComponent<Rigidbody>();
            body.mass = 25f;

            motor = robot.AddComponent<RobotMotor>();
            PlayerKeyboardDriver driver = robot.AddComponent<PlayerKeyboardDriver>();

            // Hayaletin kopyalayacağı görsel parça.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Govde";
            visual.transform.SetParent(robot.transform, false);

            host = new GameObject("Egitim");
            session = host.AddComponent<TimeTrialSession>();
            session.ConfigureRoute(new Vector3(-4f, 0f, -3f), 0f, Route);
            session.Bind(null, null, null, driver);
            session.CanEnter = true;

            runner = host.AddComponent<GhostRunner>();
            runner.Session = session;
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

            GhostLapStore.Clear();
        }

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

        private IEnumerator CompleteLap(bool valid)
        {
            session.Enter();
            yield return new WaitForSeconds(0.25f);

            if (!valid)
            {
                // Sırayı atla: tur geçersiz olur.
                yield return MoveTo(Route[1].position);
            }

            foreach (TimeTrialCheckpoint checkpoint in Route)
            {
                yield return MoveTo(checkpoint.position);
                yield return new WaitForSeconds(0.08f);
            }
        }

        [Test]
        public void EskiSurumKaydiKullanilmaz()
        {
            GhostLapRecording recording = BuildRecording("ayni-parkur", 10f);
            recording.version = GhostLapRecording.CurrentVersion + 1;

            Assert.IsFalse(recording.IsUsableFor("ayni-parkur"));
        }

        [Test]
        public void BaskaParkurKaydiKullanilmaz()
        {
            GhostLapRecording recording = BuildRecording("eski-parkur", 10f);

            Assert.IsFalse(recording.IsUsableFor("yeni-parkur"),
                "Rota degistigi halde eski rekor ayni parkur gibi kullanildi.");
            Assert.IsTrue(recording.IsUsableFor("eski-parkur"));
        }

        [Test]
        public void EksikKayitKullanilmaz()
        {
            GhostLapRecording recording = BuildRecording("parkur", 10f);
            recording.samples = new[] { recording.samples[0] };

            Assert.IsFalse(recording.IsUsableFor("parkur"), "Tek ornekli kayit kullanilabilir sayildi.");
        }

        [Test]
        public void BozukDosyaGuvenleAtlanir()
        {
            File.WriteAllText(GhostLapStore.FilePath, "{bu gecerli json degil");

            Assert.IsNull(GhostLapStore.Load("parkur"), "Bozuk dosya atlanmadi.");
        }

        [Test]
        public void KayitYazilipOkunur()
        {
            GhostLapRecording recording = BuildRecording("parkur", 12.5f);

            Assert.IsTrue(GhostLapStore.Save(recording));

            GhostLapRecording loaded = GhostLapStore.Load("parkur");
            Assert.IsNotNull(loaded, "Kayit okunamadi.");
            Assert.AreEqual(12.5f, loaded.lapTime, 0.0001f);
            Assert.AreEqual(recording.samples.Length, loaded.samples.Length);
        }

        [Test]
        public void AradegerHesabiSinirlardaTasmaz()
        {
            GhostLapRecording recording = BuildRecording("parkur", 2f);

            recording.Evaluate(-5f, out Vector3 before, out _);
            recording.Evaluate(99f, out Vector3 after, out _);

            Assert.AreEqual(recording.samples[0].position, before);
            Assert.AreEqual(recording.samples[recording.samples.Length - 1].position, after);
        }

        [UnityTest]
        public IEnumerator GecerliTurRekorOlarakKaydedilir()
        {
            Assert.AreEqual(0f, runner.PersonalBestTime, "Baslangicta rekor olmamali.");

            yield return CompleteLap(true);

            Assert.AreEqual(TimeTrialState.Finished, session.State);
            Assert.IsTrue(session.LastLapValid);
            Assert.Greater(runner.PersonalBestTime, 0f, "Ilk gecerli tur kaydedilmedi.");
            Assert.AreEqual(session.LastLapTime, runner.PersonalBestTime, 0.001f);
            Assert.IsNotNull(GhostLapStore.Load(session.CourseId), "Rekor diske yazilmadi.");
        }

        [UnityTest]
        public IEnumerator GecersizTurRekorOlmaz()
        {
            yield return CompleteLap(false);

            Assert.AreEqual(TimeTrialState.Finished, session.State);
            Assert.IsFalse(session.LastLapValid);
            Assert.AreEqual(0f, runner.PersonalBestTime, "Gecersiz tur rekor oldu.");
            Assert.IsNull(GhostLapStore.Load(session.CourseId));
        }

        [UnityTest]
        public IEnumerator DahaYavasTurRekoruEzmez()
        {
            yield return CompleteLap(true);
            float firstRecord = runner.PersonalBestTime;
            Assert.Greater(firstRecord, 0f);

            // İkinci tur bilerek daha yavaş.
            session.Retry();
            yield return new WaitForSeconds(0.6f);
            foreach (TimeTrialCheckpoint checkpoint in Route)
            {
                yield return MoveTo(checkpoint.position);
                yield return new WaitForSeconds(0.1f);
            }

            Assert.Greater(session.LastLapTime, firstRecord, "Ikinci tur daha yavas olmaliydi.");
            Assert.AreEqual(firstRecord, runner.PersonalBestTime, 0.001f,
                "Daha yavas tur rekoru ezdi.");
        }

        [UnityTest]
        public IEnumerator HayaletFizikVeDovusBileseniTasimaz()
        {
            // Önce bir rekor oluştur, sonra yeni denemede hayalet kurulsun.
            yield return CompleteLap(true);
            session.Retry();
            yield return null;

            Assert.IsTrue(runner.HasGhost, "Rekor varken hayalet kurulmadi.");

            GameObject ghost = GameObject.Find("Kisisel Rekor Hayaleti");
            Assert.IsNotNull(ghost, "Hayalet sahnede bulunamadi.");

            Assert.IsEmpty(ghost.GetComponentsInChildren<Rigidbody>(true), "Hayalette Rigidbody var.");
            Assert.IsEmpty(ghost.GetComponentsInChildren<Collider>(true), "Hayalette Collider var.");
            Assert.IsEmpty(ghost.GetComponentsInChildren<RobotMotor>(true), "Hayalette motor var.");
            Assert.IsEmpty(ghost.GetComponentsInChildren<PlayerKeyboardDriver>(true), "Hayalette surucu var.");
            Assert.IsEmpty(ghost.GetComponentsInChildren<TimeTrialSession>(true),
                "Hayalette kontrol noktasi ureticisi var.");
            Assert.IsNotEmpty(ghost.GetComponentsInChildren<MeshRenderer>(true),
                "Hayaletin gorsel parcasi yok.");
        }

        [UnityTest]
        public IEnumerator EgitimdenCikisHayaletiTemizler()
        {
            yield return CompleteLap(true);
            session.Retry();
            yield return null;
            Assert.IsTrue(runner.HasGhost);

            session.Exit();
            yield return null;

            Assert.IsFalse(runner.HasGhost, "Cikista hayalet temizlenmedi.");
            Assert.IsNull(GameObject.Find("Kisisel Rekor Hayaleti"));
        }

        private static GhostLapRecording BuildRecording(string courseId, float lapTime)
        {
            return new GhostLapRecording
            {
                version = GhostLapRecording.CurrentVersion,
                courseId = courseId,
                lapTime = lapTime,
                samples = new[]
                {
                    new GhostLapSample { time = 0f, position = Vector3.zero, yaw = 0f },
                    new GhostLapSample { time = lapTime, position = new Vector3(1f, 0f, 2f), yaw = 90f },
                },
            };
        }
    }
}
