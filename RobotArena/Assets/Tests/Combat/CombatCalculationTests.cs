using NUnit.Framework;
using RobotSavaslari.Combat;

namespace RobotSavaslari.Tests
{
    public sealed class CombatCalculationTests
    {
        [TestCase(4f, 20f, 10f, 8f)]
        [TestCase(4f, 10f, 20f, 2f)]
        [TestCase(0f, 10f, 10f, 0f)]
        [TestCase(-4f, 10f, 10f, 0f)]
        [TestCase(4f, 10f, 0f, 0f)]
        public void RamUsesOutgoingSpeedAndMassRatio(float speed, float attacker, float defender, float expected)
        {
            Assert.That(DamageCalculator.Calculate(AttackData.Ram(speed, attacker, defender)), Is.EqualTo(expected));
        }

        [Test]
        public void InvalidSpeedDoesNotDamage()
        {
            Assert.That(DamageCalculator.CalculateRamDamage(float.NaN, 10f, 10f), Is.Zero);
            Assert.That(DamageCalculator.CalculateRamDamage(float.PositiveInfinity, 10f, 10f), Is.Zero);
        }

        [Test]
        public void RecoilIsMassScaledAndCapped()
        {
            Assert.That(KnockbackCalculator.Calculate(4f, 10f, 20f, 0.5f, 2.5f), Is.EqualTo(2.5f));
            Assert.That(KnockbackCalculator.Calculate(4f, 20f, 10f, 0.5f, 2.5f), Is.EqualTo(1f));
            Assert.That(KnockbackCalculator.Calculate(4f, 0f, 10f, 0.5f, 2.5f), Is.Zero);
        }
    }
}
