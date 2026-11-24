using System.Collections;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Gameplay.Tests
{
    public sealed partial class WorkshopDuelTests
    {
        private static void BreakWheel(RobotPrototypeController robot, bool rear, bool left)
        {
            var damage = robot.GetComponent<RobotDamageVisuals>();
            var wheel = damage.Parts.Single(part =>
            {
                if (part.Kind != RobotDamageVisuals.PartKind.Wheel) return false;
                Vector3 local = robot.transform.InverseTransformPoint(part.Transform.position);
                return (local.z < 0f) == rear && (local.x < 0f) == left;
            });
            robot.GetComponent<RobotHealth>().TakeDamage(wheel.Threshold + .1f, wheel.Transform.position, Vector3.up, Vector3.zero, true);
            Assert.That(wheel.Detached, Is.True, wheel.Name);
        }

        private static void DisableDrivers(MatchController controller)
        {
            controller.Player.GetComponent<PlayerKeyboardDriver>().enabled = false;
            controller.Opponent.GetComponent<RobotBotController>().enabled = false;
            GameplayTestScene.Place(controller.Opponent, new Vector3(5, GameplayTestScene.Floor, 4), false);
        }

        [UnityTest]
        public IEnumerator FourCornerDamageChangesAttitudeAndDrive()
        {
            yield return StartDuel(DuelSetup.Quick());
            DisableDrivers(match);
            var robot = match.Player;
            var router = robot.GetComponent<RobotDriveMotor>();
            var body = router.Body;
            var hull = robot.GetComponent<BoxCollider>();
            Vector3 fullSize = hull.size;
            var acceleration = new float[4];
            var drift = new float[4];
            var brakeFromTwo = new float[4];
            var roll = new float[4];
            var pitch = new float[4];
            string[] labels = { "intact", "rear-left", "rear-right", "both-rear" };
            var tick = new WaitForFixedUpdate();
            for (int loss = 0; loss < labels.Length; loss++)
            {
                robot.GetComponent<RobotHealth>().Restore();
                router.Clear(true);
                GameplayTestScene.Place(robot, new Vector3(0, GameplayTestScene.Floor, -3), false, Quaternion.identity);
                if (loss == 1 || loss == 3) BreakWheel(robot, true, true);
                if (loss == 2 || loss == 3) BreakWheel(robot, true, false);
                float low = float.PositiveInfinity, high = float.NegativeInfinity, maxVertical = 0f;
                for (int i = 0; i < 100; i++)
                {
                    yield return tick;
                    if (i < 75) continue;
                    low = Mathf.Min(low, body.position.y);
                    high = Mathf.Max(high, body.position.y);
                    maxVertical = Mathf.Max(maxVertical, Mathf.Abs(body.linearVelocity.y));
                }
                Vector3 side = body.rotation * Vector3.right, nose = body.rotation * Vector3.forward;
                roll[loss] = Mathf.Asin(side.y) * Mathf.Rad2Deg;
                pitch[loss] = Mathf.Asin(nose.y) * Mathf.Rad2Deg;
                Assert.That(hull.bounds.min.y, Is.GreaterThan(GameplayTestScene.Floor - .035f), "no floor penetration");
                TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "WHEEL_SETTLE {0} y={1:F4}..{2:F4} roll={3:F2} pitch={4:F2} max_vy={5:F3}",
                    labels[loss], low, high, roll[loss], pitch[loss], maxVertical));
                Assert.That(high - low, Is.LessThan(.012f), "stationary vertical travel");
                var start = body.position;
                for (int i = 1; i <= 34; i++)
                {
                    router.Submit(1, 0);
                    yield return tick;
                    if (i == 6) acceleration[loss] = body.linearVelocity.z / (6 * Time.fixedDeltaTime);
                }
                drift[loss] = body.position.x - start.x;
                float speed = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
                Vector3 brakeStart = body.position;
                yield return GameplayTestScene.FixedSteps(38);
                float stopDistance = Vector3.ProjectOnPlane(body.position - brakeStart, Vector3.up).magnitude;
                Assert.That(body.linearVelocity.magnitude, Is.LessThan(.15f), "stop");
                // The maximum speeds differ; a second coast-down compares braking at the same entry speed.
                body.linearVelocity = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized * 2f;
                brakeStart = body.position;
                yield return GameplayTestScene.FixedSteps(30);
                brakeFromTwo[loss] = Vector3.ProjectOnPlane(body.position - brakeStart, Vector3.up).magnitude;
                float yaw = body.rotation.eulerAngles.y;
                for (int i = 0; i < 15; i++) { router.Submit(0, 1); yield return tick; }
                float turn = Mathf.DeltaAngle(yaw, body.rotation.eulerAngles.y);
                TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "WHEEL_MEASURE {0} roll_deg={1:F2} pitch_deg={2:F2} drift_m={3:F3} accel_mps2={4:F2} speed_mps={5:F2} brake_m={6:F3} turn_deg={7:F2} brake_from_2mps_m={8:F3}",
                    labels[loss], roll[loss], pitch[loss], drift[loss], acceleration[loss], speed, stopDistance, turn, brakeFromTwo[loss]));
                Assert.That(turn, Is.GreaterThan(3f), labels[loss]);
                Assert.That(speed, Is.GreaterThan(.5f), labels[loss]);
            }
            Assert.That(Mathf.Abs(roll[0]) + Mathf.Abs(pitch[0]), Is.LessThan(.5f));
            Assert.That(roll[1], Is.GreaterThan(1f), "left side lower");
            Assert.That(roll[2], Is.LessThan(-1f), "right side lower");
            Assert.That(pitch[1], Is.GreaterThan(1f), "rear lower");
            Assert.That(pitch[2], Is.GreaterThan(1f), "rear lower");
            Assert.That(drift[1], Is.LessThan(drift[0] - .02f));
            Assert.That(drift[2], Is.GreaterThan(drift[0] + .02f));
            Assert.That(acceleration[1], Is.InRange(.5f, acceleration[0] - .25f));
            Assert.That(acceleration[2], Is.InRange(.5f, acceleration[0] - .25f));
            Assert.That(acceleration[3], Is.LessThan(Mathf.Min(acceleration[1], acceleration[2])));
            Assert.That(brakeFromTwo[1], Is.GreaterThan(brakeFromTwo[0] + .003f));
            Assert.That(brakeFromTwo[2], Is.GreaterThan(brakeFromTwo[0] + .003f));
            Assert.That(brakeFromTwo[3], Is.GreaterThan(Mathf.Max(brakeFromTwo[1], brakeFromTwo[2])));
            match.Defeat(false);
            match.Prepare();
            Assert.That(hull.size, Is.EqualTo(fullSize));
        }

        [UnityTest]
        public IEnumerator DamagedChassisHandlesBeltImpactAndRematch()
        {
            var series = DuelSetup.Series();
            series.arenaId = "Tactical";
            yield return StartDuel(series);
            DisableDrivers(match);
            var robot = match.Player;
            var motor = robot.GetComponent<RobotDriveMotor>();
            var body = motor.Body;
            var hull = robot.GetComponent<BoxCollider>();
            Vector3 size = hull.size;
            var belt = arena.Contents.GetComponentInChildren<ConveyorSurface>();
            GameplayTestScene.Place(robot, belt.transform.position, false, Quaternion.identity);
            BreakWheel(robot, true, true);
            Vector3 start = body.position;
            yield return GameplayTestScene.FixedSteps(35);
            Assert.That(Vector3.Dot(body.position - start, belt.SurfaceVelocity.normalized), Is.GreaterThan(.12f));
            var obstacle = arena.Contents.GetComponentsInChildren<BoxCollider>().First(c => c.name.StartsWith("Arena Obstacle"));
            Vector3 approach = obstacle.bounds.center - Vector3.forward * (obstacle.bounds.extents.z + 1.6f);
            approach.y = GameplayTestScene.Floor;
            GameplayTestScene.Place(robot, approach, false, Quaternion.identity);
            body.AddForceAtPosition((Vector3.forward * 4 + Vector3.up) * body.mass, body.worldCenterOfMass + robot.transform.right * .2f, ForceMode.Impulse);
            bool reachedObstacle = false;
            for (int i = 0; i < 55; i++)
            {
                motor.Submit(1, 0);
                yield return new WaitForFixedUpdate();
                reachedObstacle |= hull.bounds.SqrDistance(obstacle.ClosestPoint(body.worldCenterOfMass)) < .0025f;
            }
            Assert.That(reachedObstacle, Is.True, "damaged hull reaches the arena obstacle");
            Assert.That(body.position.z, Is.LessThan(obstacle.bounds.min.z));
            yield return GameplayTestScene.FixedSteps(60);
            Assert.That(hull.bounds.min.y, Is.GreaterThan(GameplayTestScene.Floor - .035f));
            Assert.That(Mathf.Abs(body.linearVelocity.y), Is.LessThan(.12f));
            Assert.That(Vector3.Dot(body.rotation * Vector3.up, Vector3.up), Is.GreaterThan(.85f));
            for (int i = 0; i < 2; i++)
            {
                if (i == 1) BreakWheel(robot, true, false);
                match.Defeat(false);
                Assert.That(match.EncounterComplete, Is.EqualTo(i == 1));
                Assert.That(match.RequestCountdown(), Is.True);
                Assert.That(robot.GetComponent<RobotDamageVisuals>().LostWheels, Is.Zero);
                Assert.That(hull.size, Is.EqualTo(size));
                Assert.That(Vector3.Angle(body.rotation * Vector3.up, Vector3.up), Is.LessThan(.1f));
                Assert.That(body.linearVelocity.sqrMagnitude + body.angularVelocity.sqrMagnitude, Is.LessThan(.001f));
                yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
            }
            DisableDrivers(match);
            var opponent = match.Opponent;
            GameplayTestScene.Place(opponent, new Vector3(4, GameplayTestScene.Floor, 3), false, Quaternion.identity);
            BreakWheel(opponent, true, false);
            yield return GameplayTestScene.FixedSteps(85);
            var attitude = opponent.GetComponent<Rigidbody>().rotation;
            Assert.That((attitude * Vector3.right).y, Is.LessThan(-.015f), "bot lost right support");
            Assert.That((attitude * Vector3.forward).y, Is.GreaterThan(.015f), "bot lost rear support");

            GameplayTestScene.Place(opponent, new Vector3(20f, GameplayTestScene.Floor, 0), true);
            int invalidWrites = 0;
            void WatchVelocity(string message, string stack, LogType type)
            {
                if (message.Contains("velocity") && message.Contains("kinematic body")) invalidWrites++;
            }
            Application.logMessageReceived += WatchVelocity;
            try
            {
                opponent.ConstrainToArena();
                opponent.ResetMotion();
                yield return GameplayTestScene.FixedSteps(2);
                Assert.That(opponent.GetComponent<Rigidbody>().position.x, Is.LessThan(8f), "kinematic boundary is still enforced");
                Assert.That(invalidWrites, Is.Zero, "velocity setters require a dynamic body");
            }
            finally { Application.logMessageReceived -= WatchVelocity; }
        }

    }
}
