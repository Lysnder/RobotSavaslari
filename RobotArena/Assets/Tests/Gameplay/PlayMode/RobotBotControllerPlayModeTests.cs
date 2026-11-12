using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Decision = RobotSavaslari.RobotBotController.Decision;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>
    /// Bot decisions stepped by hand: tracking, attack intent, wall recovery, combat rhythm and the
    /// bot scenario lab. The bot's own physics tick is switched off so every step is deterministic.
    /// </summary>
    public sealed class RobotBotControllerPlayModeTests : InputTestFixture
    {
        private const float Step = 0.02f;
        private GameObject _botObject;
        private RobotBotController _bot;
        private Rigidbody _body;
        private Transform _target;

        [UnityTearDown]
        public IEnumerator Cleanup() => GameplayTestScene.Unload();

        private IEnumerator CreateBot(bool tactics = false)
        {
            yield return GameplayTestScene.LoadEmptyScene();
            _botObject = new GameObject("Bot");
            _body = _botObject.AddComponent<Rigidbody>();
            _body.isKinematic = true; // no progress: lets the stuck detector be exercised
            _body.useGravity = false;
            _botObject.AddComponent<RobotDriveMotor>();
            _bot = _botObject.AddComponent<RobotBotController>();
            _bot.enableCombatTactics = tactics;
            _bot.enabled = false; // stepped by the test only
            _target = new GameObject("Target").transform;
            _target.position = new Vector3(0f, 0f, 5f);
            _bot.Target = _target;
            _bot.SetMatchRunning(true);
            yield return null;
        }

        private void Tick(int steps)
        {
            for (int i = 0; i < steps; i++) _bot.Tick(Step);
        }

        [UnityTest]
        public IEnumerator FollowsTurnsAndRequestsAttacksWhenCloseAndFacing()
        {
            yield return CreateBot();
            Tick(1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Following));
            Assert.That(_bot.Distance, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(_bot.Command.x, Is.GreaterThan(0f));

            _target.position = new Vector3(0f, 0f, -4f);
            Tick(1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Turning), "target behind: turn in place");
            Assert.That(Mathf.Abs(_bot.TargetAngle), Is.GreaterThan(170f));
            Assert.That(_bot.Command.x, Is.Zero);

            int requests = 0;
            _bot.AttackRequest += (bot, target) => requests++;
            _target.position = new Vector3(0f, 0f, 1.2f);
            Tick(1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Attacking));
            Assert.That(_bot.AttackRequested, Is.True);
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(_bot.Command, Is.EqualTo(Vector2.zero), "stops inside weapon range");
        }

        [UnityTest]
        public IEnumerator StuckBotBacksOutTurnsAndDrivesAgain()
        {
            yield return CreateBot();
            Tick(Mathf.CeilToInt(_bot.stuckSeconds / Step) + 2);
            Assert.That(_bot.State, Is.EqualTo(Decision.BackingOut));
            Assert.That(_bot.RecoveryCount, Is.EqualTo(1));
            Assert.That(_bot.Command.x, Is.LessThan(0f));

            Tick(Mathf.CeilToInt(_bot.reverseSeconds / Step) + 1);
            Assert.That(_bot.State, Is.EqualTo(Decision.EscapeTurn));
            Tick(Mathf.CeilToInt(_bot.escapeTurnSeconds / Step) + 1);
            Assert.That(_bot.State, Is.EqualTo(Decision.EscapeDrive));
            Tick(Mathf.CeilToInt(_bot.escapeDriveSeconds / Step) + 1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Following));
        }

        [UnityTest]
        public IEnumerator StopsWithoutTargetMatchOrLife()
        {
            yield return CreateBot();
            var targetHealth = _target.gameObject.AddComponent<RobotHealth>();
            Tick(1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Following));

            targetHealth.TakeDamage(1000f);
            Tick(1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Idle), "defeated target");
            targetHealth.Restore();

            _bot.SetMatchRunning(false);
            Tick(1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Idle), "match stopped");
            Assert.That(_bot.MatchRunning, Is.False);
            _bot.SetMatchRunning(true);

            _target.gameObject.SetActive(false);
            Tick(1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Idle), "inactive target");
            _target.gameObject.SetActive(true);

            _bot.Target = null;
            Tick(1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Idle));
            Assert.That(_bot.Distance, Is.EqualTo(float.PositiveInfinity));

            var ownHealth = _botObject.AddComponent<RobotHealth>();
            _bot.Target = _target;
            yield return null;
            ownHealth.TakeDamage(1000f);
            Tick(1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Idle), "defeated bot");
        }

        [UnityTest]
        public IEnumerator CombatRhythmBacksOffRepositionsAndComesBack()
        {
            yield return CreateBot(true);
            _target.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            _target.gameObject.AddComponent<RobotHealth>();
            yield return null;
            _target.position = new Vector3(0f, 0f, 1.2f);

            Tick(Mathf.CeilToInt(_bot.attackBurstSeconds / Step) + 3);
            Assert.That(_bot.State, Is.EqualTo(Decision.Disengaging), "attack burst ends with a retreat");
            Assert.That(_bot.RepositionCount, Is.EqualTo(1));

            Tick(Mathf.CeilToInt(_bot.disengageSeconds / Step) + 1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Repositioning));
            Tick(Mathf.CeilToInt(_bot.repositionSeconds / Step) + 1);
            Assert.That(_bot.State, Is.EqualTo(Decision.Waiting));
            Assert.That(_bot.Command, Is.EqualTo(Vector2.zero));
            Tick(Mathf.CeilToInt(_bot.openingSeconds / Step) + 1);
            Assert.That(_bot.State, Is.Not.EqualTo(Decision.Waiting), "back to the fight");
        }

        [UnityTest]
        public IEnumerator SpinningTargetTriggersARepositionNearTheWall()
        {
            yield return CreateBot(true);
            _body.position = new Vector3(4.2f, 0f, 3.2f);
            _botObject.transform.position = _body.position;
            _target.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            _target.position = new Vector3(4.2f, 0f, 4.6f);
            yield return null;
            for (int i = 0; i < 80 && _bot.RepositionCount == 0; i++)
            {
                _target.rotation *= Quaternion.Euler(0f, 9f, 0f);
                _target.GetComponent<Rigidbody>().rotation = _target.rotation;
                _bot.Tick(Step);
            }
            Assert.That(_bot.RepositionCount, Is.EqualTo(1), "a spinning target is not chased forever");
            Tick(10);
            Assert.That(_bot.State, Is.EqualTo(Decision.Disengaging).Or.EqualTo(Decision.Repositioning));
        }

        [UnityTest]
        public IEnumerator FarTargetResetsTheRhythm()
        {
            yield return CreateBot(true);
            _target.position = new Vector3(0f, 0f, 1.2f);
            Tick(5);
            _target.position = new Vector3(0f, 0f, 8f);
            Tick(Mathf.CeilToInt(_bot.circleTimeout / Step) + 5);
            Assert.That(_bot.RepositionCount, Is.Zero, "far away the bot just pursues");
            Assert.That(_bot.State, Is.EqualTo(Decision.Following).Or.EqualTo(Decision.BackingOut).Or.EqualTo(Decision.EscapeTurn).Or.EqualTo(Decision.EscapeDrive));
        }

        [UnityTest]
        public IEnumerator AttackProbeCountsRequests()
        {
            yield return CreateBot();
            var probe = _botObject.AddComponent<BotAttackProbe>();
            _target.position = new Vector3(0f, 0f, 1.2f);
            Tick(3);
            Assert.That(probe.Count, Is.EqualTo(3));
            Assert.That(probe.LastRequestTime, Is.GreaterThanOrEqualTo(0f));
            probe.ResetCount();
            Assert.That(probe.Count, Is.Zero);
            Assert.That(probe.LastRequestTime, Is.EqualTo(-1f));
            probe.enabled = false;
            Tick(2);
            Assert.That(probe.Count, Is.Zero, "disabled probe stops listening");
        }

        [UnityTest]
        public IEnumerator ScenarioLabRunsEveryScenario()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            var botObject = new GameObject("Lab Bot");
            botObject.AddComponent<Rigidbody>().useGravity = false;
            botObject.AddComponent<RobotDriveMotor>();
            var bot = botObject.AddComponent<RobotBotController>();
            botObject.AddComponent<BotAttackProbe>();
            var target = new GameObject("Lab Target").transform;
            var obstacles = new GameObject("Obstacles");
            var runnerObject = new GameObject("Runner");
            runnerObject.SetActive(false);
            var runner = runnerObject.AddComponent<BotScenarioRunner>();
            runner.bot = bot;
            runner.testTarget = target;
            runner.obstacles = obstacles;
            runnerObject.SetActive(true);
            yield return null;

            foreach (BotScenarioRunner.Scenario scenario in System.Enum.GetValues(typeof(BotScenarioRunner.Scenario)))
            {
                runner.Select(scenario);
                Assert.That(runner.Elapsed, Is.Zero);
                Assert.That(bot.Target, Is.EqualTo(target));
                Assert.That(obstacles.activeSelf, Is.EqualTo(scenario == BotScenarioRunner.Scenario.Corner || scenario == BotScenarioRunner.Scenario.AngledWall));
                yield return GameplayTestScene.FixedSteps(5);
                Assert.That(runner.Elapsed, Is.GreaterThan(0f));
                ImmediateGui.Draw(runner);
            }

            // Timed events: target loss and return, and the match end.
            Time.timeScale = 8f;
            runner.Select(BotScenarioRunner.Scenario.TargetLoss);
            yield return new WaitUntil(() => runner.Elapsed > 2.2f);
            Assert.That(bot.Target, Is.Null, "target removed after two seconds");
            yield return new WaitUntil(() => runner.Elapsed > 4.2f);
            Assert.That(bot.Target, Is.EqualTo(target), "target returned after four seconds");
            runner.Select(BotScenarioRunner.Scenario.MatchEnd);
            yield return new WaitUntil(() => runner.Elapsed > 1.7f);
            Assert.That(bot.MatchRunning, Is.False, "match end stops the bot");
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator ScenarioLabTargetCanBeSteeredByKeyboard()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var botObject = new GameObject("Lab Bot");
            botObject.AddComponent<Rigidbody>().useGravity = false;
            botObject.AddComponent<RobotDriveMotor>();
            var bot = botObject.AddComponent<RobotBotController>();
            var target = new GameObject("Lab Target").transform;
            var runnerObject = new GameObject("Runner");
            runnerObject.SetActive(false);
            var runner = runnerObject.AddComponent<BotScenarioRunner>();
            runner.bot = bot;
            runner.testTarget = target;
            runner.obstacles = new GameObject("Obstacles");
            runnerObject.SetActive(true);
            yield return null;

            runner.automaticTarget = false;
            Vector3 start = target.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.RightArrow));
            InputSystem.Update();
            yield return GameplayTestScene.FixedSteps(10);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(target.position.z, Is.GreaterThan(start.z));
            Assert.That(target.position.x, Is.GreaterThan(start.x));
        }
    }
}
