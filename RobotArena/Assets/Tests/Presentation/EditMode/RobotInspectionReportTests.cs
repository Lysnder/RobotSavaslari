using NUnit.Framework;
using PartKind = RobotSavaslari.RobotDamageVisuals.PartKind;
using PartState = RobotSavaslari.Presentation.RobotInspectionReport.PartState;

namespace RobotSavaslari.Presentation.Tests
{
    public sealed class RobotInspectionReportTests
    {
        [TestCase("Body", "Gövde")]
        [TestCase("Spinner", "Döner silah")]
        [TestCase("Lifter", "Kaldırıcı silah")]
        [TestCase("Wheel_FL", "Sol ön tekerlek")]
        [TestCase("Wheel_RL", "Sol arka tekerlek")]
        [TestCase("Wheel_FR", "Sağ ön tekerlek")]
        [TestCase("Wheel_RR", "Sağ arka tekerlek")]
        [TestCase("Armor_FL", "Sol ön zırh")]
        [TestCase("Armor_RR", "Sağ arka zırh")]
        [TestCase("Wheel_XY", "Wheel_XY")]
        [TestCase("Wheel_FLX", "Wheel_FLX")]
        [TestCase("Antenna", "Antenna")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void PartNamesAreDescribedForPlayers(string partName, string expected)
        {
            Assert.That(RobotInspectionReport.DescribePart(partName), Is.EqualTo(expected));
        }

        [TestCase(0f, false, "Sağlam")]
        [TestCase(0.004f, false, "Sağlam")]
        [TestCase(0.42f, false, "%42 hasarlı")]
        [TestCase(5f, false, "%100 hasarlı")]
        [TestCase(0.3f, true, "Koptu")]
        public void ConditionIsDescribed(float ratio, bool detached, string expected)
        {
            Assert.That(RobotInspectionReport.DescribeCondition(ratio, detached), Is.EqualTo(expected));
        }

        [Test]
        public void UndamagedRobotIsRecognised()
        {
            var report = new RobotInspectionReport("Mavi Robot", 1f, true, new[]
            {
                new PartState("Body", PartKind.Body, 0f, false),
                new PartState("Wheel_FL", PartKind.Wheel, 0f, false),
            });
            Assert.That(report.IsUndamaged, Is.True);
            Assert.That(report.DetachedCount, Is.Zero);
            Assert.That(report.HasPartData, Is.True);
        }

        [Test]
        public void DetachedPartsAndDisabledWeaponAreCounted()
        {
            var report = new RobotInspectionReport("Kirmizi Robot", 0f, false, new[]
            {
                new PartState("Body", PartKind.Body, 0.6f, false),
                new PartState("Lifter", PartKind.Weapon, 1f, true),
                new PartState("Wheel_RR", PartKind.Wheel, 1f, true),
            });
            Assert.That(report.IsUndamaged, Is.False);
            Assert.That(report.DetachedCount, Is.EqualTo(2));
            Assert.That(report.WeaponOperational, Is.False);
            Assert.That(report.Parts[1].Condition, Is.EqualTo("Koptu"));
            Assert.That(report.Parts[1].DisplayName, Is.EqualTo("Kaldırıcı silah"));
        }

        [Test]
        public void PartDamageAloneMeansDamaged()
        {
            var report = new RobotInspectionReport("Mavi Robot", 1f, true, new[] { new PartState("Armor_FR", PartKind.Armor, 0.2f, false) });
            Assert.That(report.IsUndamaged, Is.False);
        }

        [Test]
        public void RobotWithoutPartDataReportsHealthOnly()
        {
            var report = new RobotInspectionReport(null, 0.25f, true, null);
            Assert.That(report.HasPartData, Is.False);
            Assert.That(report.Parts, Is.Empty);
            Assert.That(report.RobotName, Is.Empty);
            Assert.That(report.HealthRatio, Is.EqualTo(0.25f));
        }

        [Test]
        public void RatiosAreClampedAndNaNIsTreatedAsNoDamage()
        {
            var part = new PartState("Body", PartKind.Body, float.NaN, false);
            Assert.That(part.DamageRatio, Is.Zero);
            Assert.That(new PartState("Body", PartKind.Body, -1f, false).DamageRatio, Is.Zero);
            Assert.That(new PartState("Body", PartKind.Body, 3f, false).DamageRatio, Is.EqualTo(1f));
            Assert.That(new RobotInspectionReport("x", 2f, true, null).HealthRatio, Is.EqualTo(1f));
            Assert.That(new RobotInspectionReport("x", float.NaN, true, null).HealthRatio, Is.Zero);
        }

        [Test]
        public void CaptureOfNullRobotIsEmpty()
        {
            RobotInspectionReport report = RobotInspectionReport.Capture(null);
            Assert.That(report.HasPartData, Is.False);
            Assert.That(report.RobotName, Is.Empty);
        }
    }
}
