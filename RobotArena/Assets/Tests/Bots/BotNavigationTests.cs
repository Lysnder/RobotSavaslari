using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotSavaslari.Tests
{
    public sealed class BotNavigationTests
    {
        private const float Tick = .02f;
        private GameObject owner, opponent, obstacle;
        private RobotBotController bot;
        private Rigidbody body;
        private Scene arenaScene;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Navigation robot");
            owner.transform.position = new Vector3(0, 50, -3);
            body = owner.AddComponent<Rigidbody>();
            body.useGravity = false;
            owner.AddComponent<BoxCollider>().size = new Vector3(.64f, .6f, .64f);
            owner.AddComponent<RobotHealth>();
            bot = owner.AddComponent<RobotBotController>();
            bot.enabled = false;
            owner.GetComponent<RobotDriveMotor>().enabled = false;
            opponent = new GameObject("Navigation target");
            opponent.transform.position = new Vector3(0, 50, 3);
            bot.Target = opponent.transform;
            bot.enableCombatTactics = true;
            bot.tacticalHalfExtents = new Vector2(4, 4);
            obstacle = new GameObject("Route obstruction");
            obstacle.transform.position = new Vector3(0, 50.5f, 0);
            obstacle.AddComponent<BoxCollider>().size = new Vector3(1.6f, 2, .3f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(opponent);
            Object.DestroyImmediate(obstacle);
        }

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            if (arenaScene.IsValid() && arenaScene.isLoaded) yield return SceneManager.UnloadSceneAsync(arenaScene);
        }

        [Test]
        public void OpenPursuitSkipsSearchAndObstructionProducesLateralSteering()
        {
            obstacle.SetActive(false);
            bot.Tick(Tick);
            Assert.That(bot.Command.x, Is.GreaterThan(0));
            Assert.That(bot.NavigationPlans, Is.Zero);

            obstacle.SetActive(true);
            Physics.SyncTransforms();
            bot.Tick(Tick);
            Assert.That(bot.Command, Is.EqualTo(Vector2.zero));
            int probes = 0;
            for (; probes < 24 && Mathf.Abs(bot.Command.y) < .12f; probes++) StepWithinBudget();
            Assert.That(probes, Is.LessThan(24), "A detour must become actionable within the retry interval.");
            Assert.That(Mathf.Abs(bot.NavigationWaypoint.x), Is.GreaterThan(.1f));
            Assert.That(bot.AttackRequested, Is.False);
        }

        [Test]
        public void OccludedOpponentInsideAttackRangeIsNotAnAttackOpportunity()
        {
            body.position = new Vector3(0, 50, -.7f);
            opponent.transform.position = new Vector3(0, 50, .7f);
            Physics.SyncTransforms();
            int attempts = 0;
            bot.AttackRequest += (_, __) => attempts++;
            bot.Tick(Tick);
            Assert.Zero(attempts);
            Assert.IsFalse(bot.AttackRequested);
            obstacle.SetActive(false);
            Physics.SyncTransforms();
            bot.Tick(Tick);
            Assert.AreEqual(1, attempts);
        }

        [Test]
        public void ClosedTargetRetriesAreBoundedAndResetDiscardsTheRoute()
        {
            obstacle.GetComponent<BoxCollider>().size = new Vector3(10, 2, .3f);
            Physics.SyncTransforms();
            for (int i = 0; i < 120; i++)
            {
                StepWithinBudget();
                Assert.That(bot.Command, Is.EqualTo(Vector2.zero));
                Assert.That(bot.AttackRequested, Is.False);
            }
            Assert.That(bot.NavigationPlans, Is.InRange(3, 5));
            Assert.That(bot.NavigationReason, Is.EqualTo(BotRouteReason.Unreachable));
            owner.GetComponent<RobotHealth>().Restore();
            Assert.That(bot.NavigationPlans, Is.Zero);
            Assert.That(bot.NavigationReason, Is.EqualTo(BotRouteReason.None));
        }

        [Test]
        public void PlannerFindsAClearanceSizedPassageBetweenTwoWalls()
        {
            body.position = new Vector3(.8f, 50, -3);
            opponent.transform.position = new Vector3(.8f, 50, 3);
            var left = obstacle.GetComponent<BoxCollider>();
            left.center = new Vector3(-2.3f, 0, 0);
            left.size = new Vector3(3.4f, 2, .3f);
            var right = obstacle.AddComponent<BoxCollider>();
            right.center = new Vector3(2.3f, 0, 0);
            right.size = left.size;
            Physics.SyncTransforms();
            for (int i = 0; i < 24 && Mathf.Abs(bot.Command.y) < .12f; i++) StepWithinBudget();
            Assert.That(bot.NavigationPlans, Is.EqualTo(1));
            Assert.That(bot.Command.y, Is.LessThan(-.12f), "The only opening is to the left of the blocked direct path.");
            Assert.That(bot.NavigationWaypoint.x, Is.LessThan(.6f));
        }

        [Test]
        public void MovingTargetDoesNotDiscardAnUnobstructedDetourSegment()
        {
            for (int i = 0; i < 24 && Mathf.Abs(bot.Command.y) < .12f; i++) StepWithinBudget();
            Assert.That(Mathf.Abs(bot.Command.y), Is.GreaterThan(.12f));
            Assert.That(bot.NavigationPlans, Is.EqualTo(1));
            opponent.transform.position += Vector3.right * 1.3f;
            Physics.SyncTransforms();
            for (int i = 0; i < 30; i++) StepWithinBudget();
            Assert.That(bot.NavigationPlans, Is.EqualTo(1), "Keep the safe opening instead of stopping on every target movement.");
            Assert.That(Mathf.Abs(bot.Command.y), Is.GreaterThan(.12f));
        }

        [Test]
        public void SaturatedQueriesStopTheBotWithoutStartingASearch()
        {
            for (int i = 0; i < 64; i++)
            {
                var collider = obstacle.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, 0, 3);
            }
            Physics.SyncTransforms();
            StepWithinBudget();
            Assert.That(bot.Command, Is.EqualTo(Vector2.zero));
            Assert.That(bot.NavigationPlans, Is.Zero);
            Assert.That(bot.NavigationReason, Is.EqualTo(BotRouteReason.QuerySaturated));
        }

        [Test]
        public void NewObstructionStopsExistingCommandAndOpeningRestoresDirectPursuit()
        {
            obstacle.SetActive(false);
            var root = new GameObject("Navigation warning barrier");
            root.transform.position = new Vector3(0, 50, 0);
            var barrier = root.AddComponent<WarningBarrier>();
            try
            {
                barrier.Configure(null, null);
                barrier.Advance(1.3f);
                Assert.That(barrier.Phase, Is.EqualTo(BarrierPhase.Warning));
                Physics.SyncTransforms();
                bot.Tick(Tick);
                Assert.That(bot.Command.x, Is.GreaterThan(0));
                barrier.Advance(1.5f);
                barrier.Advance(.9f);
                Assert.That(barrier.Phase, Is.EqualTo(BarrierPhase.Active));
                Physics.SyncTransforms();
                bot.Tick(Tick);
                Assert.That(bot.Command, Is.EqualTo(Vector2.zero));
                Assert.That(bot.NavigationPlans, Is.EqualTo(1));
                barrier.ResetCycle();
                Physics.SyncTransforms();
                bot.Tick(Tick);
                Assert.That(bot.Command.x, Is.GreaterThan(0));
                Assert.That(bot.NavigationReason, Is.EqualTo(BotRouteReason.None));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SharedMotorDrivesAroundWallAndReachesRealAttackRange()
        {
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var motor = owner.GetComponent<RobotDriveMotor>();
            Transform attacked = null;
            bot.AttackRequest += (_, target) => attacked = target;
            float widestDetour = 0f;
            SimulationMode simulation = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                for (int i = 0; i < 400 && attacked == null; i++)
                {
                    bot.Tick(Tick);
                    motor.Step(Tick);
                    Physics.Simulate(Tick);
                    widestDetour = Mathf.Max(widestDetour, Mathf.Abs(body.position.x));
                    Assert.That(bot.NavigationQueries, Is.LessThanOrEqualTo(64));
                }
            }
            finally { Physics.simulationMode = simulation; }
            Assert.That(widestDetour, Is.GreaterThan(1f), "A physical detour must go around the collider.");
            Assert.That(attacked, Is.SameAs(opponent.transform), "The waypoint must never become an attack target.");
            Assert.That(bot.RecoveryCount, Is.Zero, "A traversable obstacle should not require the wall recovery cycle.");
        }

        [Test]
        public void TwoBotsStayWithinTheUpdateBudgetAfterWarmup()
        {
            var rivalBody = opponent.AddComponent<Rigidbody>();
            rivalBody.useGravity = false;
            opponent.AddComponent<BoxCollider>().size = new Vector3(.64f, .6f, .64f);
            opponent.AddComponent<RobotHealth>();
            opponent.AddComponent<RobotDamageVisuals>();
            owner.AddComponent<RobotDamageVisuals>();
            var rival = opponent.AddComponent<RobotBotController>();
            rival.enabled = false;
            opponent.GetComponent<RobotDriveMotor>().enabled = false;
            rival.enableCombatTactics = true;
            rival.Target = owner.transform;
            Physics.SyncTransforms();

            const int warmup = 120, count = 500;
            var milliseconds = new double[count];
            for (int i = 0; i < warmup; i++) { bot.Tick(Tick); rival.Tick(Tick); }
            long maxBytes = 0;
            for (int i = 0; i < count; i++)
            {
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                bot.Tick(Tick);
                rival.Tick(Tick);
                long end = System.Diagnostics.Stopwatch.GetTimestamp();
                maxBytes = System.Math.Max(maxBytes, System.GC.GetAllocatedBytesForCurrentThread() - before);
                milliseconds[i] = (end - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            }
            System.Array.Sort(milliseconds);
            double p95 = milliseconds[count * 95 / 100 - 1];
            Debug.Log($"Two bot Tick benchmark: {Application.platform}, Unity {Application.unityVersion}, " +
                $"{SystemInfo.processorType}, {SystemInfo.systemMemorySize} MB, {Screen.width}x{Screen.height}; " +
                $"warmup={warmup}, samples={count}, p50={milliseconds[count / 2]:F4} ms, p95={p95:F4} ms, " +
                $"max={milliseconds[count - 1]:F4} ms, maxGC={maxBytes} B/tick");
            Assert.That(bot.NavigationPlans + rival.NavigationPlans, Is.GreaterThan(0));
            Assert.That(p95, Is.LessThanOrEqualTo(2d));
            Assert.That(maxBytes, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MainArenaBotMakesProgressWithAgainstAndAcrossTheConveyor()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Gameplay/MainArena.unity", LoadSceneMode.Additive);
            arenaScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            yield return null;
            ArenaEnvironment arena = null;
            foreach (GameObject root in arenaScene.GetRootGameObjects())
            {
                arena = root.GetComponentInChildren<ArenaEnvironment>();
                if (arena != null) break;
            }
            Assert.That(arena, Is.Not.Null);
            int open = -1;
            for (int i = 0; i < arena.Layouts.Count; i++)
                if (arena.Layouts[i].displayName == "Open") { open = i; break; }
            Assert.That(open, Is.GreaterThanOrEqualTo(0));
            Assert.That(arena.Select(open), Is.True);
            var match = arena.Match;
            Assert.That(match.StartMatch(), Is.True);
            var arenaBot = match.Opponent.GetComponent<RobotBotController>();
            var arenaBody = match.Opponent.GetComponent<Rigidbody>();
            var targetBody = match.Player.GetComponent<Rigidbody>();
            targetBody.isKinematic = true;
            var belts = arena.Contents.GetComponentsInChildren<ConveyorSurface>();
            Assert.That(belts.Length, Is.GreaterThan(0));
            Vector3 beltCenter = belts[0].transform.position;
            Vector3 along = belts[0].SurfaceVelocity.normalized;
            foreach (Vector3 direction in new[] { along, -along, Vector3.Cross(along, Vector3.up) })
            {
                ConveyorSurface.RemovePreviousMotion();
                Place(arenaBody, beltCenter, Quaternion.LookRotation(direction));
                Place(targetBody, beltCenter + direction * 3f, Quaternion.LookRotation(direction));
                arenaBot.ResetDecision();
                Physics.SyncTransforms();
                bool contacted = false;
                for (int i = 0; i < 20; i++)
                {
                    yield return new WaitForFixedUpdate();
                    float surfaceSpeed = ConveyorSurface.AppliedVelocity(arenaBody).magnitude;
                    contacted |= surfaceSpeed > .01f;
                    Assert.That(surfaceSpeed, Is.LessThanOrEqualTo(ConveyorSurface.SurfaceSpeed + .001f));
                }
                Assert.That(contacted, Is.True, direction.ToString());
                Assert.That(Vector3.Dot(arenaBody.position - beltCenter, direction), Is.GreaterThan(.16f), direction.ToString());
            }
        }

        private static void Place(Rigidbody robot, Vector3 position, Quaternion rotation)
        {
            robot.position = position;
            robot.rotation = rotation;
            robot.transform.SetPositionAndRotation(position, rotation);
        }

        private void StepWithinBudget()
        {
            bot.Tick(Tick);
            Assert.That(bot.NavigationQueries, Is.LessThanOrEqualTo(64));
            Assert.That(bot.NavigationExpansions, Is.LessThanOrEqualTo(128));
        }
    }
}
