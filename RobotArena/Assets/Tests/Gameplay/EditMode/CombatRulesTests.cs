using System;
using NUnit.Framework;
using RobotSavaslari.Combat;
using RobotSavaslari.Robots;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>Pure combat rules: damage, knockback, cooldown, hit bookkeeping, reports and profiles.</summary>
    public sealed class CombatRulesTests
    {
        [TestCase(4f, 20f, 10f, 8f)]
        [TestCase(3f, 10f, 10f, 3f)]
        public void RamDamageThroughTheAttackEntryPoint(float speed, float attacker, float defender, float expected)
        {
            Assert.That(DamageCalculator.Calculate(AttackData.Ram(speed, attacker, defender)), Is.EqualTo(expected).Within(1e-5f));
        }

        [TestCase(float.NaN, 10f, 10f)]
        [TestCase(float.PositiveInfinity, 10f, 10f)]
        [TestCase(4f, float.NaN, 10f)]
        [TestCase(4f, -1f, 10f)]
        [TestCase(4f, 10f, float.NegativeInfinity)]
        public void RamDamageRejectsInvalidInput(float speed, float attacker, float defender)
        {
            Assert.That(DamageCalculator.CalculateRamDamage(speed, attacker, defender), Is.Zero);
        }

        [Test]
        public void UnknownAttackTypeIsRejected()
        {
            var attack = new AttackData((AttackType)99, 1f, 1f, 1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => DamageCalculator.Calculate(attack));
        }

        [Test]
        public void AttackDataKeepsItsInputs()
        {
            AttackData data = AttackData.Ram(2f, 3f, 4f);
            Assert.That(data.Type, Is.EqualTo(AttackType.Ram));
            Assert.That(data.ImpactSpeed, Is.EqualTo(2f));
            Assert.That(data.AttackerMass, Is.EqualTo(3f));
            Assert.That(data.DefenderMass, Is.EqualTo(4f));
        }

        [TestCase(0f, 1f, 1f, 1f, 1f)]
        [TestCase(1f, 0f, 1f, 1f, 1f)]
        [TestCase(1f, 1f, float.NaN, 1f, 1f)]
        [TestCase(1f, 1f, 1f, -1f, 1f)]
        [TestCase(1f, 1f, 1f, 1f, float.PositiveInfinity)]
        public void KnockbackRejectsInvalidInput(float speed, float own, float other, float ratio, float max)
        {
            Assert.That(KnockbackCalculator.Calculate(speed, own, other, ratio, max), Is.Zero);
        }

        [Test]
        public void LighterRobotIsPushedFurtherUpToTheCap()
        {
            Assert.That(KnockbackCalculator.Calculate(2f, 10f, 20f, 0.5f, 10f), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(KnockbackCalculator.Calculate(2f, 20f, 10f, 0.5f, 10f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(KnockbackCalculator.Calculate(100f, 1f, 50f, 1f, 2.5f), Is.EqualTo(2.5f));
        }

        [Test]
        public void CooldownStartsWhenUsedAndCanBeReset()
        {
            var cooldown = new AttackCooldown();
            Assert.That(cooldown.IsReady(0f), Is.True, "ready before first use");
            Assert.That(cooldown.Remaining(0f), Is.Zero);
            cooldown.Begin(10f, 1.5f);
            Assert.That(cooldown.IsReady(10.5f), Is.False);
            Assert.That(cooldown.Remaining(10.5f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(cooldown.IsReady(11.5f), Is.True);
            cooldown.Begin(20f, -3f);
            Assert.That(cooldown.IsReady(20f), Is.True, "negative duration counts as zero");
            cooldown.Begin(30f, 5f);
            cooldown.Reset();
            Assert.That(cooldown.IsReady(30f), Is.True);
        }

        [Test]
        public void HitLogCountsEachTargetOnce()
        {
            var log = new AttackHitLog();
            var target = new object();
            Assert.That(log.TryRegister(null), Is.False);
            Assert.That(log.Contains(null), Is.False);
            Assert.That(log.TryRegister(target), Is.True);
            Assert.That(log.TryRegister(target), Is.False);
            Assert.That(log.Contains(target), Is.True);
            Assert.That(log.Count, Is.EqualTo(1));
            log.Clear();
            Assert.That(log.Count, Is.Zero);
            Assert.That(log.Contains(target), Is.False);
        }

        [TestCase(AttackOutcome.None, 0, "-")]
        [TestCase(AttackOutcome.Started, 0, "SALDIRI SURUYOR")]
        [TestCase(AttackOutcome.Hit, 2, "ISABET (2 hedef)")]
        [TestCase(AttackOutcome.Miss, 0, "ISKA")]
        [TestCase(AttackOutcome.Cooldown, 0, "BEKLEME - istek reddedildi")]
        [TestCase(AttackOutcome.MatchClosed, 0, "MAC KAPALI")]
        [TestCase(AttackOutcome.MatchClosed, 1, "MAC KAPANDI - saldiri kesildi")]
        [TestCase(AttackOutcome.AttackerDefeated, 0, "SALDIRAN YENIK")]
        public void ReportsDescribeEveryOutcome(AttackOutcome outcome, int targets, string expected)
        {
            var report = new AttackReport(outcome, targets, 3f);
            Assert.That(report.Describe(), Is.EqualTo(expected));
            Assert.That(report.Outcome, Is.EqualTo(outcome));
            Assert.That(report.TargetsHit, Is.EqualTo(targets));
            Assert.That(report.Time, Is.EqualTo(3f));
        }

        [Test]
        public void ProfileCountsHitsToDefeat()
        {
            CombatProfile profile = CombatProfile.CreateRuntime("Deneme", 30f, 2f, 0.5f, 0.2f, 90f);
            try
            {
                Assert.That(profile.name, Is.EqualTo("Deneme"));
                Assert.That(profile.range, Is.EqualTo(2f));
                Assert.That(profile.cooldown, Is.EqualTo(0.5f));
                Assert.That(profile.activeDuration, Is.EqualTo(0.2f));
                Assert.That(profile.arcDegrees, Is.EqualTo(90f));
                Assert.That(profile.HitsToDefeat(100f), Is.EqualTo(4));
                profile.damage = 0f;
                Assert.That(profile.HitsToDefeat(100f), Is.EqualTo(int.MaxValue));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void RobotHitAndMoveCommandCarryTheirValues()
        {
            var hit = new RobotHit(5f, Vector3.one, Vector3.up, Vector3.right, true);
            Assert.That(hit.Amount, Is.EqualTo(5f));
            Assert.That(hit.Point, Is.EqualTo(Vector3.one));
            Assert.That(hit.Normal, Is.EqualTo(Vector3.up));
            Assert.That(hit.Impulse, Is.EqualTo(Vector3.right));
            Assert.That(hit.Weapon, Is.True);

            // Whole numbers format the same in every culture.
            var command = new RobotMoveCommand(1f, -1f);
            Assert.That(command.Forward, Is.EqualTo(1f));
            Assert.That(command.Turn, Is.EqualTo(-1f));
            Assert.That(command.ToString(), Does.Contain("forward: 1").And.Contain("turn: -1"));
            Assert.That(RobotMoveCommand.Idle.Forward, Is.Zero);
            Assert.That(RobotMoveCommand.Idle.Turn, Is.Zero);
        }
    }
}
