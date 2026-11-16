using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Combat;
using RobotSavaslari.Combat.Testing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>
    /// The melee acceptance scenarios (health, single hit per attack, cooldown, match gate, reach)
    /// run on real colliders in edit mode, plus the rig they are built on.
    /// </summary>
    public sealed class CombatScenarioSuiteTests
    {
        [Test]
        public void EveryMeleeScenarioPasses()
        {
            List<CombatScenarios.Result> results = CombatScenarios.RunAll();
            Assert.That(results, Is.Not.Empty);
            string[] failures = results.Where(r => !r.Passed).Select(r => r.ToString()).ToArray();
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
            Assert.That(results.All(r => r.ToString().StartsWith("PASS [")), Is.True);
        }

        [Test]
        public void ResultTextShowsVerdictCardAndDetail()
        {
            Assert.That(new CombatScenarios.Result("E9", "ad", false, "neden").ToString(), Is.EqualTo("FAIL [E9] ad - neden"));
            Assert.That(new CombatScenarios.Result("E9", "ad", true, "").ToString(), Is.EqualTo("PASS [E9] ad"));
        }

        [Test]
        public void RigBuildsTwoOpposingRobotsAndCleansUp()
        {
            var log = new List<string>();
            CombatTestRig rig = CombatTestRig.Create(new Vector3(500f, 0f, 500f), true, 80f, 120f);
            GameObject a = rig.A.Root, b = rig.B.Root;
            try
            {
                rig.Log += log.Add;
                Assert.That(rig.Other(rig.A), Is.SameAs(rig.B));
                Assert.That(rig.Other(rig.B), Is.SameAs(rig.A));
                Assert.That(rig.A.Colliders.Length, Is.EqualTo(3), "body and two wheels");
                Assert.That(rig.A.Health.CurrentHealth, Is.EqualTo(80f));
                Assert.That(rig.B.Health.CurrentHealth, Is.EqualTo(120f));
                Assert.That(rig.Gate.IsOpen, Is.True);
                Assert.That(rig.Center, Is.EqualTo(new Vector3(500f, 0f, 500f)));

                rig.Place(rig.B, rig.A, 1.2f, 30f, 15f);
                Vector3 offset = rig.B.Root.transform.position - rig.A.Root.transform.position;
                Assert.That(offset.magnitude, Is.EqualTo(1.2f).Within(1e-3f));
                Assert.That(Vector3.SignedAngle(rig.A.Root.transform.forward, offset, Vector3.up), Is.EqualTo(30f).Within(0.1f));

                float start = rig.Now;
                rig.Advance(0.25f);
                Assert.That(rig.Now, Is.EqualTo(start + 0.25f).Within(1e-4f));

                rig.Place(rig.B, rig.A, CombatTestRig.DefaultSpacing, 0f);
                Assert.That(rig.Swing(rig.A), Is.EqualTo(AttackOutcome.Started));
                Assert.That(log.Any(line => line.Contains("hasar")), Is.True, "hit is logged");

                rig.B.Health.TakeDamage(1000f);
                Assert.That(rig.B.DefeatNotifications, Is.EqualTo(1));
                Assert.That(log.Any(line => line.Contains("YENILDI")), Is.True);

                rig.NewMatch();
                Assert.That(rig.B.DefeatNotifications, Is.Zero);
                Assert.That(rig.B.Health.CurrentHealth, Is.EqualTo(120f));
                Assert.That(log.Last(), Does.StartWith("Yeni mac"));
            }
            finally { rig.Dispose(); }
            Assert.That(a == null && b == null, Is.True, "robots destroyed");
        }

        [Test]
        public void RealTimeRigCannotBeAdvancedByHand()
        {
            CombatTestRig rig = CombatTestRig.Create(new Vector3(-500f, 0f, -500f), false);
            try { Assert.Throws<InvalidOperationException>(() => rig.Advance(1f)); }
            finally { rig.Dispose(); }
        }

        [Test]
        public void ProfileIsAppliedToBothRobots()
        {
            CombatProfile profile = CombatProfile.CreateRuntime("Genis", 10f, 3f, 2f, 0f, 360f);
            CombatTestRig rig = CombatTestRig.Create(new Vector3(0f, 0f, 800f), true);
            try
            {
                rig.ApplyProfile(profile);
                foreach (CombatTestRobot robot in new[] { rig.A, rig.B })
                {
                    Assert.That(robot.Attack.Profile, Is.SameAs(profile));
                    Assert.That(robot.Attack.Damage, Is.EqualTo(10f));
                    Assert.That(robot.Attack.Range, Is.EqualTo(3f));
                    Assert.That(robot.Attack.Cooldown, Is.EqualTo(2f));
                    Assert.That(robot.Attack.ActiveDuration, Is.Zero);
                    Assert.That(robot.Attack.ArcDegrees, Is.EqualTo(360f));
                }
                // Full arc: a target directly behind is still in reach.
                rig.Place(rig.B, rig.A, 1.5f, 180f);
                Assert.That(rig.B.Colliders.Any(c => rig.A.Attack.IsInReach(c, out _)), Is.True);
                Assert.That(rig.Swing(rig.A), Is.EqualTo(AttackOutcome.Started));
                Assert.That(rig.A.Attack.LastReport.Outcome, Is.EqualTo(AttackOutcome.Hit), "zero-length window samples once");
                Assert.That(rig.Swing(rig.A), Is.EqualTo(AttackOutcome.Cooldown));
            }
            finally
            {
                rig.Dispose();
                Object.DestroyImmediate(profile);
            }
        }
    }
}
