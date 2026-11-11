using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Robots.Tests
{
    /// <summary>
    /// Kabul ölçütlerinin fizik testleri: ileri/geri gitme, iki yöne dönme,
    /// komut bırakıldığında durma, duvar ve köşeden dışarı çıkmama ve klavye
    /// olmadan aynı motora komut gönderme.
    /// </summary>
    public class RobotMotorPlayModeTests
    {
        private const float RobotHalfDepth = 0.7f;

        private readonly List<GameObject> spawned = new List<GameObject>();

        private RobotMotor motor;
        private Transform robot;

        [SetUp]
        public void SetUp()
        {
            CreateBlock("Zemin", new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));

            GameObject robotObject = new GameObject("TestRobot");
            spawned.Add(robotObject);
            robotObject.transform.position = new Vector3(0f, 0.05f, 0f);

            BoxCollider collider = robotObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.6f, 1.4f);
            collider.center = new Vector3(0f, 0.3f, 0f);

            Rigidbody body = robotObject.AddComponent<Rigidbody>();
            body.mass = 25f;
            body.centerOfMass = new Vector3(0f, 0.2f, 0f);

            motor = robotObject.AddComponent<RobotMotor>();
            robot = robotObject.transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject instance in spawned)
            {
                if (instance != null)
                {
                    Object.Destroy(instance);
                }
            }

            spawned.Clear();
            motor = null;
            robot = null;
        }

        [UnityTest]
        public IEnumerator IleriKomutuRobotuIleriGoturur()
        {
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(1f);

            Assert.Greater(robot.position.z, 1f, "Robot ileri komutuyla ilerlemedi.");
            Assert.Greater(motor.ForwardSpeed, 0f);
        }

        [UnityTest]
        public IEnumerator GeriKomutuRobotuGeriGoturur()
        {
            motor.SetMoveCommand(-1f, 0f);
            yield return Simulate(1f);

            Assert.Less(robot.position.z, -0.5f, "Robot geri komutuyla gerilemedi.");
            Assert.Less(motor.ForwardSpeed, 0f);
        }

        [UnityTest]
        public IEnumerator DonusKomutuIkiYoneCalisir()
        {
            motor.SetMoveCommand(0f, 1f);
            yield return Simulate(0.5f);

            float rightYaw = Mathf.DeltaAngle(0f, robot.eulerAngles.y);
            Assert.Greater(rightYaw, 10f, "Robot saga donmedi.");

            motor.Stop();
            yield return Simulate(0.3f);

            robot.rotation = Quaternion.identity;
            motor.SetMoveCommand(0f, -1f);
            yield return Simulate(0.5f);

            float leftYaw = Mathf.DeltaAngle(0f, robot.eulerAngles.y);
            Assert.Less(leftYaw, -10f, "Robot sola donmedi.");
        }

        [UnityTest]
        public IEnumerator KomutBirakilincaHareketKesilir()
        {
            motor.SetMoveCommand(1f, 1f);
            yield return Simulate(1f);

            motor.Stop();
            yield return Simulate(1f);

            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f, "Komut birakilmadi.");
            Assert.AreEqual(0f, motor.TurnCommand, 0.0001f, "Donus komutu birakilmadi.");
            Assert.AreEqual(0f, motor.ForwardSpeed, 0.05f, "Robot komut birakildiktan sonra kaymaya devam etti.");
            Assert.AreEqual(0f, motor.TurnSpeed, 1f, "Robot komut birakildiktan sonra donmeye devam etti.");

            Vector3 restingPosition = robot.position;
            float restingYaw = robot.eulerAngles.y;
            yield return Simulate(0.5f);

            Assert.AreEqual(restingPosition.x, robot.position.x, 0.05f);
            Assert.AreEqual(restingPosition.z, robot.position.z, 0.05f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(restingYaw, robot.eulerAngles.y), 1f);
        }

        [UnityTest]
        public IEnumerator DuvaraSurulenRobotDisariGecmez()
        {
            const float wallFace = 5f;
            CreateBlock("Duvar", new Vector3(0f, 0.75f, wallFace + 0.5f), new Vector3(20f, 1.5f, 1f));

            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(4f);

            Assert.Less(robot.position.z, wallFace - RobotHalfDepth + 0.05f,
                $"Robot duvardan gecti (z = {robot.position.z:0.###}).");
        }

        [UnityTest]
        public IEnumerator KoseyeSurulenRobotDisariGecmez()
        {
            const float wallFace = 5f;
            CreateBlock("Duvar Kuzey", new Vector3(0f, 0.75f, wallFace + 0.5f), new Vector3(20f, 1.5f, 1f));
            CreateBlock("Duvar Dogu", new Vector3(wallFace + 0.5f, 0.75f, 0f), new Vector3(1f, 1.5f, 20f));

            robot.rotation = Quaternion.Euler(0f, 45f, 0f);
            motor.SetMoveCommand(1f, 0f);
            yield return Simulate(5f);

            Assert.Less(robot.position.z, wallFace - 0.5f,
                $"Robot kose duvarindan gecti (z = {robot.position.z:0.###}).");
            Assert.Less(robot.position.x, wallFace - 0.5f,
                $"Robot kose duvarindan gecti (x = {robot.position.x:0.###}).");
            Assert.Less(robot.position.y, 0.5f, "Robot duvarin uzerine tirmandi.");
        }

        [UnityTest]
        public IEnumerator KlavyeOlmadanKomutDiziIleSurulur()
        {
            Assert.IsNull(motor.GetComponent<KeyboardRobotInput>(), "Test robotunda klavye bileseni olmamali.");

            RobotCommandSequence sequence = motor.gameObject.AddComponent<RobotCommandSequence>();
            sequence.SetCommands(new[]
            {
                new TimedRobotCommand { duration = 1f, forward = 1f, turn = 0f },
                new TimedRobotCommand { duration = 0.5f, forward = 0f, turn = 1f },
            });

            sequence.Play();
            yield return Simulate(1f);

            Assert.IsTrue(sequence.IsPlaying);
            Assert.Greater(robot.position.z, 1f, "Senaryo komutu robotu ilerletmedi.");

            yield return Simulate(0.6f);
            Assert.Greater(Mathf.DeltaAngle(0f, robot.eulerAngles.y), 5f, "Senaryo komutu robotu dondurmedi.");

            sequence.Stop();
            yield return Simulate(0.5f);

            Assert.AreEqual(0f, motor.MoveCommand, 0.0001f);
            Assert.AreEqual(0f, motor.TurnCommand, 0.0001f);
        }

        [UnityTest]
        public IEnumerator KomutlarAraliginDisindaKirpilir()
        {
            motor.SetMoveCommand(5f, -9f);
            yield return null;

            Assert.AreEqual(1f, motor.MoveCommand, 0.0001f);
            Assert.AreEqual(-1f, motor.TurnCommand, 0.0001f);
        }

        private GameObject CreateBlock(string name, Vector3 position, Vector3 scale)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            spawned.Add(block);

            return block;
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
