using NUnit.Framework;
using UnityEngine;

namespace RobotSavaslari.Tests
{
    public sealed class BotDecisionTests
    {
        [TestCase(0, -4, true)]
        [TestCase(0, 4, false)]
        [TestCase(4, 0, false)]
        [TestCase(0, 0, false)]
        public void ThreatUsesRelativeMotion(float x, float z, bool expected)
        {
            var observation = new BotThreatObservation(Vector3.forward * 3, Vector3.zero,
                new Vector3(x, 0, z), Vector3.back);
            Assert.That(observation.Incoming, Is.EqualTo(expected));
        }
        [Test]
        public void PursuingStationaryTargetIsNotACharge()
        {
            Assert.That(new BotThreatObservation(Vector3.forward * 3, Vector3.forward * 4,
                Vector3.zero, Vector3.back).Incoming, Is.False);
        }
        [Test]
        public void CloseContactAndPassingOffsetAreNotCharges()
        {
            Assert.That(new BotThreatObservation(Vector3.forward * 0.6f, Vector3.zero, Vector3.back * 4, Vector3.back).Incoming, Is.False);
            Assert.That(new BotThreatObservation(new Vector3(2, 0, 3), Vector3.zero, Vector3.back * 4, Vector3.back).Incoming, Is.False);
        }
        [Test]
        public void OneContinuousChargeIsOneSample()
        {
            var memory = new BotAttackMemory();
            for (int i = 0; i < 100; i++) memory.Observe(true, 0, 0.02f);
            Assert.That(memory.Count, Is.EqualTo(1));
            Assert.That(memory.AdaptDefense, Is.False);
            memory.Observe(false, 0, 0.1f);
            memory.Observe(true, 0, 0.02f);
            Assert.That(memory.TotalCharges, Is.EqualTo(1));
        }
        [Test]
        public void RepetitionAdaptsThenDifferentAnglesDiluteIt()
        {
            var memory = new BotAttackMemory();
            for (int i = 0; i < 3; i++) Charge(memory, 0);
            Assert.That(memory.AdaptDefense, Is.True);
            Charge(memory, 90);
            Charge(memory, -90);
            memory.Observe(false, 0, 0.02f);
            Assert.That(memory.AdaptDefense, Is.False);
            Assert.That(memory.Repetition, Is.EqualTo(0.6f).Within(0.001f));
        }
        [Test]
        public void MemoryIsBoundedExpiresAndResets()
        {
            var memory = new BotAttackMemory();
            for (int i = 0; i < 20; i++) Charge(memory, i % 2 == 0 ? 0 : 90);
            Assert.That(memory.Count, Is.EqualTo(6));
            memory.Observe(false, 0, 12.1f);
            Assert.That(memory.Count, Is.Zero);
            Charge(memory, 0);
            memory.Clear();
            Assert.That(memory.TotalCharges, Is.Zero);
            Assert.That(memory.Count, Is.Zero);
        }
        [Test]
        public void ZeroDeltaPreservesMemory()
        {
            var memory = new BotAttackMemory();
            Charge(memory, 0);
            for (int i = 0; i < 1000; i++) memory.Observe(false, 90, 0);
            Assert.That(memory.Count, Is.EqualTo(1));
        }
        [Test]
        public void DifficultyChangesDecisionParametersOnly()
        {
            var easy = new BotDecisionProfile(BotDifficulty.Easy);
            var hard = new BotDecisionProfile(BotDifficulty.Hard);
            Assert.That(easy.ReactionMultiplier, Is.GreaterThan(hard.ReactionMultiplier));
            Assert.That(easy.AimErrorDegrees, Is.GreaterThan(hard.AimErrorDegrees));
            Assert.That(easy.PositionError, Is.GreaterThan(hard.PositionError));
        }
        private static void Charge(BotAttackMemory memory, float angle)
        {
            memory.Observe(false, angle, 0.31f);
            memory.Observe(true, angle, 0.02f);
        }
    }
}
