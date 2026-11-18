using NUnit.Framework;
using UnityEngine;

namespace RobotSavaslari.Tests
{
    public sealed class BotControllerTests
    {
        private GameObject _owner, _target, _wall;
        private RobotBotController _bot;
        private Rigidbody _targetBody;
        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Decision test robot");
            _owner.transform.position = new Vector3(0, 50, 0);
            _owner.AddComponent<RobotHealth>();
            _bot = _owner.AddComponent<RobotBotController>();
            _bot.enabled = false;
            _owner.GetComponent<RobotDriveMotor>().enabled = false;
            _owner.GetComponent<Rigidbody>().useGravity = false;
            _target = new GameObject("Decision test target");
            _target.transform.SetPositionAndRotation(new Vector3(0, 50, 2.6f), Quaternion.Euler(0, 180, 0));
            _targetBody = _target.AddComponent<Rigidbody>();
            _targetBody.useGravity = false;
            _target.AddComponent<RobotHealth>();
            _bot.Target = _target.transform;
            _bot.enableCombatTactics = true;
            _bot.SetMatchRunning(true);
        }
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1;
            Object.DestroyImmediate(_owner); Object.DestroyImmediate(_target);
            if (_wall != null) Object.DestroyImmediate(_wall);
        }
        [Test]
        public void SameScenarioProducesDifferentPersonalityDecisions()
        {
            _bot.ConfigureDecisions(BotPersonality.Aggressive, BotDifficulty.Hard);
            _bot.Tick(.02f);
            Assert.That(_bot.State, Is.EqualTo(RobotBotController.Decision.Following));
            _bot.ConfigureDecisions(BotPersonality.Counter, BotDifficulty.Hard);
            _bot.Tick(.02f);
            Assert.That(_bot.State, Is.EqualTo(RobotBotController.Decision.Disengaging));
        }
        [TestCase(BotPersonality.Aggressive, BotDifficulty.Easy)]
        [TestCase(BotPersonality.Aggressive, BotDifficulty.Hard)]
        [TestCase(BotPersonality.Counter, BotDifficulty.Easy)]
        [TestCase(BotPersonality.Counter, BotDifficulty.Hard)]
        public void ProfilesDoNotChangePhysicalStats(BotPersonality personality, BotDifficulty difficulty)
        {
            var motor = _owner.GetComponent<RobotDriveMotor>();
            float speed = motor.maxSpeed, acceleration = motor.acceleration, turn = motor.turnSpeed;
            _bot.tacticalHalfExtents = new Vector2(3, 2);
            _bot.ConfigureDecisions(personality, difficulty);
            for (int i = 0; i < 80; i++) _bot.Tick(.02f);
            Assert.That(motor.maxSpeed, Is.EqualTo(speed));
            Assert.That(motor.acceleration, Is.EqualTo(acceleration));
            Assert.That(motor.turnSpeed, Is.EqualTo(turn));
            Assert.That(_owner.GetComponent<RobotHealth>().CurrentHealth, Is.EqualTo(100));
            Assert.That(_owner.GetComponent<RobotHealth>().MaxHealth, Is.EqualTo(100));
            Assert.That(_bot.tacticalHalfExtents, Is.EqualTo(new Vector2(3, 2)));
        }
        [Test]
        public void IncomingChargeEvadesButRetreatDoesNot()
        {
            _targetBody.linearVelocity = Vector3.back * 4;
            _bot.Tick(.02f);
            Assert.That(_bot.State, Is.EqualTo(RobotBotController.Decision.Evading));
            Assert.That(_bot.Memory.Count, Is.EqualTo(1));
            _bot.ResetDecision(); _targetBody.linearVelocity = Vector3.forward * 4;
            _bot.Tick(.02f);
            Assert.That(_bot.EvasionCount, Is.Zero);
        }
        [Test]
        public void EscapeRejectsWallAndChoosesOtherSide()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(-1.5f, 50, 0);
            _wall.transform.localScale = new Vector3(.2f, 2, 3);
            Physics.SyncTransforms();
            _targetBody.linearVelocity = Vector3.back * 4;
            _bot.Tick(.02f);
            Assert.That(_bot.EvasionPoint.x, Is.GreaterThan(0));
            Assert.That(_bot.IsTacticalPathSafe(_bot.EvasionPoint), Is.True);
        }
        [Test]
        public void RepeatedChargesSwitchDefenseAndTargetLossClearsMemory()
        {
            float first = 0;
            for (int charge = 0; charge < 3; charge++)
            {
                _targetBody.linearVelocity = Vector3.back * 4;
                for (int i = 0; i < 25; i++) _bot.Tick(.02f);
                if (charge == 0) first = _bot.DefenseSide;
                _targetBody.linearVelocity = Vector3.zero;
                for (int i = 0; i < 50; i++) _bot.Tick(.02f);
            }
            Assert.That(_bot.Memory.TotalCharges, Is.EqualTo(3));
            Assert.That(_bot.DefenseSide, Is.EqualTo(-first));
            _bot.Target = null; _bot.Tick(.02f);
            Assert.That(_bot.Memory.Count, Is.Zero);
            Assert.That(_bot.Command, Is.EqualTo(Vector2.zero));
        }
        [Test]
        public void PausePreservesStateAndRandomSequenceButNewMatchResets()
        {
            _targetBody.linearVelocity = Vector3.back * 4; _bot.Tick(.02f);
            int samples = _bot.DecisionSamples;
            float error = _bot.AimError;
            var state = _bot.State;
            Time.timeScale = 0;
            for (int i = 0; i < 1000; i++) _bot.Tick(.02f);
            Assert.That(_bot.Memory.Count, Is.EqualTo(1));
            Assert.That(_bot.State, Is.EqualTo(state));
            Assert.That(_bot.DecisionSamples, Is.EqualTo(samples));
            Assert.That(_bot.AimError, Is.EqualTo(error));
            Time.timeScale = 1;
            _bot.SetMatchRunning(false); _bot.SetMatchRunning(true);
            Assert.That(_bot.Memory.Count, Is.Zero);
            Assert.That(_bot.DecisionSamples, Is.Zero);
        }
        [Test]
        public void DifficultyHasMeasuredReactionAndAimDifferenceWithRepeatableSeed()
        {
            _target.transform.position = new Vector3(0, 50, 4);
            _bot.ConfigureDecisions(BotPersonality.Aggressive, BotDifficulty.Easy, 123);
            for (int i = 0; i < 40; i++) _bot.Tick(.02f);
            int easySamples = _bot.DecisionSamples; float error = _bot.AimError;
            _bot.ConfigureDecisions(BotPersonality.Aggressive, BotDifficulty.Easy, 123);
            for (int i = 0; i < 40; i++) _bot.Tick(.02f);
            Assert.That(_bot.AimError, Is.EqualTo(error));
            _bot.ConfigureDecisions(BotPersonality.Aggressive, BotDifficulty.Hard, 123);
            for (int i = 0; i < 40; i++) _bot.Tick(.02f);
            Assert.That(_bot.DecisionSamples, Is.GreaterThan(easySamples));
            Assert.That(Mathf.Abs(_bot.AimError), Is.LessThanOrEqualTo(2));
            Debug.Log($"DECISION_MEASURE easySamples={easySamples} hardSamples={_bot.DecisionSamples} easyError={error:F3} hardError={_bot.AimError:F3}");
        }
        [Test]
        public void ChargeDoesNotRestartCommittedCounterManeuver()
        {
            _bot.ConfigureDecisions(BotPersonality.Counter, BotDifficulty.Hard);
            _bot.Tick(.02f);
            Assert.That(_bot.State, Is.EqualTo(RobotBotController.Decision.Disengaging));
            _targetBody.linearVelocity = Vector3.back * 4;
            bool sawFlank = false, sawOpening = false;
            for (int i = 0; i < 110; i++)
            {
                _bot.Tick(.02f);
                sawFlank |= _bot.State == RobotBotController.Decision.Repositioning;
                sawOpening |= _bot.State == RobotBotController.Decision.Waiting;
            }
            Assert.That(sawFlank && sawOpening, Is.True);
            Assert.That(_bot.EvasionCount, Is.Zero);
        }
        [Test]
        public void CounterOpeningTurnsBackTowardTheOpponent()
        {
            _bot.ConfigureDecisions(BotPersonality.Counter, BotDifficulty.Easy, 740);
            _bot.Tick(.02f);
            Assert.That(_bot.State, Is.EqualTo(RobotBotController.Decision.Disengaging));
            _owner.GetComponent<Rigidbody>().rotation = Quaternion.Euler(0, 90, 0);
            for (int i = 0; i < 110 && _bot.State != RobotBotController.Decision.Waiting; i++) _bot.Tick(.02f);
            Assert.That(_bot.State, Is.EqualTo(RobotBotController.Decision.Waiting), "bounded flank reaches its opening stance");
            Assert.That(_bot.Command.x, Is.Zero, "Counter holds its opening position");
            Assert.That(_bot.Command.y, Is.LessThan(-.1f), "opening must turn back toward the observed opponent after the flank");
        }

        [TestCase(BotDifficulty.Easy, false)]
        [TestCase(BotDifficulty.Hard, false)]
        [TestCase(BotDifficulty.Easy, true)]
        [TestCase(BotDifficulty.Hard, true)]
        public void CounterUsesAReadyStrikeInsteadOfDiscardingItDuringDefence(BotDifficulty difficulty, bool evading)
        {
            var weapon = _owner.AddComponent<RobotSavaslari.Combat.MeleeAttack>();
            _target.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            float now = 100f;
            weapon.Clock = () => now;
            Assert.That(weapon.RequestAttack(), Is.EqualTo(RobotSavaslari.Combat.AttackOutcome.Started));
            now += .2f; weapon.Tick(); // still cooling down, with no target in reach
            _bot.ConfigureDecisions(BotPersonality.Counter, difficulty, difficulty == BotDifficulty.Easy ? 741 : 123);
            _targetBody.linearVelocity = evading ? Vector3.back * 4 : Vector3.zero;
            _bot.Tick(.02f);
            if (!evading)
                for (int i = 0; i < 110 && _bot.State != RobotBotController.Decision.Waiting; i++) _bot.Tick(.02f);
            var defending = evading ? RobotBotController.Decision.Evading : RobotBotController.Decision.Waiting;
            Assert.That(_bot.State, Is.EqualTo(defending));
            _targetBody.linearVelocity = Vector3.zero;
            _targetBody.position = new Vector3(0, 50, 1.6f);
            _target.transform.position = _targetBody.position;
            Physics.SyncTransforms();
            _bot.Tick(.02f);
            Assert.That(_bot.State, Is.EqualTo(defending), "cooldown does not cancel the defence");
            Assert.That(_bot.AttackRequested, Is.False);

            now = 101.1f; weapon.Tick();
            _bot.Tick(.02f);
            Assert.That(_bot.AttackRequested, Is.True, "ready weapon, valid range and angle form a counterattack opening");
            Assert.That(_bot.State, Is.Not.EqualTo(defending));
            int starts = 0;
            weapon.Started += _ => starts++;
            Assert.That(weapon.RequestAttack(), Is.EqualTo(RobotSavaslari.Combat.AttackOutcome.Started));
            Assert.That(_target.GetComponent<RobotHealth>().CurrentHealth, Is.EqualTo(75f), "the opportunity reaches shared melee damage");
            Assert.That(weapon.RequestAttack(), Is.EqualTo(RobotSavaslari.Combat.AttackOutcome.Cooldown));
            Assert.That(starts, Is.EqualTo(1), "rejected retries do not count as actual starts");
        }

        [Test]
        public void ExistingStuckRecoveryStillHasPriority()
        {
            _target.transform.position = new Vector3(0, 50, 4);
            for (int i = 0; i < 50; i++) _bot.Tick(.02f);
            Assert.That(_bot.State, Is.EqualTo(RobotBotController.Decision.BackingOut));
            _targetBody.linearVelocity = Vector3.back * 5;
            _bot.Tick(.02f);
            Assert.That(_bot.State, Is.EqualTo(RobotBotController.Decision.BackingOut));
            Assert.That(_bot.Command.x, Is.LessThan(0));
        }
    }
}
