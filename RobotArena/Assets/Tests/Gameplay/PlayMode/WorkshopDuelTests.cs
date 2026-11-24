using System;
using System.Collections;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.Combat;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Gameplay.Tests
{
    public sealed partial class WorkshopDuelTests
    {
        private MatchController match;
        private MainMenuScreen menu;
        private ArenaEnvironment arena;
        private string temporary, savedDuel;
        private bool hadDuel;
        private (float speed, float acceleration, float brake, float yaw) measured;

        [UnitySetUp]
        public IEnumerator Load()
        {
            hadDuel = PlayerPrefs.HasKey(DuelSetup.PreferenceKey);
            savedDuel = PlayerPrefs.GetString(DuelSetup.PreferenceKey);
            temporary = Path.Combine(Application.temporaryCachePath, "WorkshopDuel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            yield return GameplayTestScene.LoadMainArena(temporary);
            match = GameplayTestScene.Match;
            menu = match.GetComponent<MainMenuScreen>();
            arena = UnityEngine.Object.FindAnyObjectByType<ArenaEnvironment>();
            Assert.That(Selection.UseStore(new WorkshopStore(Path.Combine(temporary, "workshop.json"))), Is.True);
        }

        [UnityTearDown]
        public IEnumerator Clean()
        {
            yield return GameplayTestScene.Unload();
            if (hadDuel) PlayerPrefs.SetString(DuelSetup.PreferenceKey, savedDuel);
            else PlayerPrefs.DeleteKey(DuelSetup.PreferenceKey);
            PlayerPrefs.Save();
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }

        [UnityTest]
        public IEnumerator StockQuickDuelMeasurements()
        {
            var stock = match.GetComponent<PlayerRobotSelection>();
            foreach (bool fork in new[] { false, true })
            {
                Assert.That(stock.SelectFork(fork), Is.True);
                yield return StartDuel(DuelSetup.Quick());
                yield return MeasureOpen(fork ? "stock-fork" : "stock-spinner");
                match.Prepare();
                yield return null;
            }
        }

        private IEnumerator StartDuel(DuelSetup setup)
        {
            Assert.That(menu.SelectPreset(setup), Is.True);
            menu.StartLobbyMatch();
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
            yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
            yield return null;
            Assert.That(arena.Layout.name, Is.EqualTo(setup.arenaId));
            Assert.That(match.Opponent.GetComponent<RobotBotController>().Target, Is.SameAs(match.Player.transform));
        }

        // A reproducible instrumentation pass in the production match. Inputs go through the
        // normal physics command router. The opponent stays clear only during the straight run.
        private IEnumerator MeasureOpen(string label)
        {
            var keyboard = match.Player.GetComponent<PlayerKeyboardDriver>();
            var bot = match.Opponent.GetComponent<RobotBotController>();
            var motor = match.Player.GetComponent<RobotDriveMotor>();
            var body = motor.Body;
            var attack = match.Player.GetComponent<MeleeAttack>();
            var target = match.Opponent.GetComponent<RobotHealth>();
            var enemyBody = target.GetComponent<Rigidbody>();
            Assert.That(bot.enabled, Is.True);
            keyboard.enabled = false;
            bot.enabled = false;
            GameplayTestScene.Place(match.Opponent, new Vector3(4f, GameplayTestScene.Floor, 3f), false);
            GameplayTestScene.Place(match.Player, new Vector3(0f, GameplayTestScene.Floor, -3f), false, Quaternion.identity);
            yield return GameplayTestScene.FixedSteps(25);
            motor.Clear(true);
            var tick = new WaitForFixedUpdate();
            float earlySpeed = 0f;
            for (int i = 1; i <= 32; i++)
            {
                motor.Submit(1f, 0f);
                yield return tick;
                if (i == 6) earlySpeed = body.linearVelocity.z;
            }
            float speed = body.linearVelocity.z;
            float brakeStart = body.position.z;
            for (int i = 0; i < 35 && body.linearVelocity.z > .05f; i++)
            {
                motor.Submit(0f, 0f);
                yield return tick;
            }
            float braking = body.position.z - brakeStart;
            Assert.That(Mathf.Abs(body.linearVelocity.z), Is.LessThan(.08f), "release reached rest");
            float startAngle = body.rotation.eulerAngles.y;
            for (int i = 0; i < 20; i++)
            {
                motor.Submit(0f, 1f);
                yield return tick;
            }
            float yaw = Mathf.Abs(Mathf.DeltaAngle(startAngle, body.rotation.eulerAngles.y));
            measured = (speed, earlySpeed / (6f * Time.fixedDeltaTime), braking, yaw);
            Assert.That(earlySpeed, Is.GreaterThan(.05f), label);
            Assert.That(speed, Is.GreaterThan(earlySpeed), label);
            Assert.That(braking, Is.GreaterThan(0f), label);
            Assert.That(yaw, Is.GreaterThan(5f), label);

            GameplayTestScene.Place(match.Player, Vector3.up * .8f, false, Quaternion.identity);
            GameplayTestScene.Place(match.Opponent, new Vector3(0f, .8f, 1.80f), false, Quaternion.identity);
            bot.enabled = true;
            keyboard.enabled = true;
            float originalHealth = target.CurrentHealth;
            float clock = Time.time;
            attack.Clock = () => clock;
            int hits = 0;
            float damage = 0f, push = 0f, cooldown = 0f;
            while (match.State == MatchController.MatchState.Playing && hits < 30)
            {
                Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.Started), label);
                hits++;
                if (hits == 1)
                {
                    damage = originalHealth - target.CurrentHealth;
                    push = enemyBody.linearVelocity.z;
                    cooldown = attack.CooldownRemaining;
                    Assert.That(damage, Is.GreaterThan(0f), label);
                    Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.Cooldown), label);
                }
                clock += attack.Cooldown + .02f;
                attack.Tick();
            }
            attack.Clock = null;
            Assert.That(match.Outcome, Is.EqualTo(MatchController.MatchOutcome.PlayerVictory), label);
            TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "WORKSHOP_MEASURE {0} mass={1:F2} acceleration={2:F3} speed={3:F3} brake={4:F4} yaw={5:F2} damage={6:F2} push={7:F2} cooldown={8:F2} hits={9} outcome={10} dt={11:F3}",
                label, body.mass, earlySpeed / (6f * Time.fixedDeltaTime), speed, braking, yaw, damage, push, cooldown, hits,
                match.Outcome, Time.fixedDeltaTime));
            yield return null;
        }
    }
}
