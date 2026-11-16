using NUnit.Framework;
using UnityEngine;

namespace RobotSavaslari.Presentation.Tests
{
    public sealed class CombatCameraFramingTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void OrbitDirectionPointsBehindAndAbovePivot()
        {
            Vector3 level = CombatCameraFraming.OrbitDirection(0f, 0f);
            Assert.That(Vector3.Distance(level, Vector3.back), Is.LessThan(Tolerance));

            Vector3 raised = CombatCameraFraming.OrbitDirection(0f, 45f);
            Assert.That(raised.y, Is.GreaterThan(0f));
            Assert.That(raised.z, Is.LessThan(0f));
            Assert.That(raised.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void OrbitRotationLooksBackAtPivot()
        {
            for (float yaw = -180f; yaw < 180f; yaw += 45f)
            {
                Vector3 toPivot = -CombatCameraFraming.OrbitDirection(yaw, 30f);
                Vector3 forward = CombatCameraFraming.OrbitRotation(yaw, 30f) * Vector3.forward;
                Assert.That(Vector3.Distance(toPivot, forward), Is.LessThan(Tolerance), "yaw " + yaw);
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(190f, -170f)]
        [TestCase(-190f, 170f)]
        [TestCase(720f + 30f, 30f)]
        [TestCase(180f, -180f)]
        public void WrapYawStaysInHalfOpenRange(float input, float expected)
        {
            Assert.That(CombatCameraFraming.WrapYaw(input), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void SmoothingWeightIsFrameRateIndependent()
        {
            Assert.That(CombatCameraFraming.SmoothingWeight(10f, 0f), Is.EqualTo(0f));
            Assert.That(CombatCameraFraming.SmoothingWeight(0f, 0.016f), Is.EqualTo(1f));
            Assert.That(CombatCameraFraming.SmoothingWeight(10f, 100f), Is.EqualTo(1f).Within(Tolerance));

            // Two half steps must cover the same remaining distance as one full step.
            float full = 1f - CombatCameraFraming.SmoothingWeight(12f, 0.04f);
            float half = 1f - CombatCameraFraming.SmoothingWeight(12f, 0.02f);
            Assert.That(half * half, Is.EqualTo(full).Within(Tolerance));
        }

        [Test]
        public void ZoomUsesDirectionOnlyAndClamps()
        {
            float closer = CombatCameraFraming.ApplyZoom(8f, 1f, 0.1f, 2f, 16f);
            float closerNotch = CombatCameraFraming.ApplyZoom(8f, 120f, 0.1f, 2f, 16f);
            Assert.That(closer, Is.LessThan(8f));
            Assert.That(closerNotch, Is.EqualTo(closer).Within(Tolerance));

            float farther = CombatCameraFraming.ApplyZoom(closer, -1f, 0.1f, 2f, 16f);
            Assert.That(farther, Is.EqualTo(8f).Within(Tolerance), "one notch in and out returns to the start");

            Assert.That(CombatCameraFraming.ApplyZoom(2.05f, 1f, 0.5f, 2f, 16f), Is.EqualTo(2f));
            Assert.That(CombatCameraFraming.ApplyZoom(15f, -1f, 0.5f, 2f, 16f), Is.EqualTo(16f));
            Assert.That(CombatCameraFraming.ApplyZoom(40f, 0f, 0.1f, 2f, 16f), Is.EqualTo(16f));
        }

        [Test]
        public void NearPlaneRadiusCoversNearPlaneCorners()
        {
            float radius = CombatCameraFraming.NearPlaneRadius(0.3f, 50f, 16f / 9f);
            float halfHeight = 0.3f * Mathf.Tan(25f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * 16f / 9f;
            var corner = new Vector3(halfWidth, halfHeight, 0.3f);
            Assert.That(radius, Is.EqualTo(corner.magnitude).Within(Tolerance));
            Assert.That(radius, Is.GreaterThan(0.3f));
        }

        [Test]
        public void FitDistanceUsesNarrowerFieldOfView()
        {
            // Wide screen: vertical 60 degrees is the narrower angle, so d = r / sin(30).
            Assert.That(CombatCameraFraming.FitDistance(1f, 60f, 2f), Is.EqualTo(2f).Within(Tolerance));

            // Portrait: the horizontal angle is narrower and needs more distance.
            float halfHorizontal = Mathf.Atan(Mathf.Tan(30f * Mathf.Deg2Rad) * 0.5f);
            Assert.That(CombatCameraFraming.FitDistance(1f, 60f, 0.5f), Is.EqualTo(1f / Mathf.Sin(halfHorizontal)).Within(Tolerance));
            Assert.That(CombatCameraFraming.FitDistance(1f, 60f, 0.5f), Is.GreaterThan(2f));
            Assert.That(CombatCameraFraming.FitDistance(-1f, 60f, 1f), Is.Zero);
        }

        [Test]
        public void EnclosePairContainsEveryCornerOfBothRobots()
        {
            var first = new Bounds(new Vector3(-4f, 1.2f, -3f), new Vector3(1.3f, 0.8f, 1.7f));
            var second = new Bounds(new Vector3(4.5f, 1.1f, 3.5f), new Vector3(1.2f, 0.6f, 1.6f));
            CombatCameraFraming.EnclosePair(first, second, out Vector3 center, out float radius);
            Assert.That(Vector3.Distance(center, (first.center + second.center) * 0.5f), Is.LessThan(Tolerance));
            foreach (Bounds bounds in new[] { first, second })
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                            Assert.That(Vector3.Distance(center, corner), Is.LessThanOrEqualTo(radius + Tolerance));
                        }
        }

        [Test]
        public void PairAxisYawKeepsPreviousWhenRobotsOverlap()
        {
            Assert.That(CombatCameraFraming.PairAxisYaw(Vector3.zero, new Vector3(2f, 0f, 0f), 0f, 0.5f), Is.EqualTo(90f).Within(Tolerance));
            Assert.That(CombatCameraFraming.PairAxisYaw(Vector3.zero, new Vector3(0f, 0f, 2f), 45f, 0.5f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(CombatCameraFraming.PairAxisYaw(Vector3.zero, new Vector3(0f, 0f, -2f), 0f, 0.5f), Is.EqualTo(-180f).Within(Tolerance));

            // Height difference alone (one robot lifted) is not a direction.
            Assert.That(CombatCameraFraming.PairAxisYaw(Vector3.zero, new Vector3(0.1f, 3f, 0.1f), 37f, 0.5f), Is.EqualTo(37f));
            Assert.That(CombatCameraFraming.PairAxisYaw(Vector3.one, Vector3.one, -12f, 0.5f), Is.EqualTo(-12f));
        }

        [Test]
        public void RateLimitedYawNeverExceedsMaximumTurnRate()
        {
            const float maxSpeed = 120f;
            const float dt = 1f / 60f;
            float yaw = 0f;
            float previous = yaw;
            int frames = 0;
            // Worst case: the goal flips by 180 degrees and smoothing would jump all the way.
            while (Mathf.Abs(Mathf.DeltaAngle(yaw, 180f)) > 0.01f && frames < 1000)
            {
                yaw = CombatCameraFraming.RateLimitedYaw(yaw, 180f, 1f, maxSpeed, dt);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(previous, yaw)), Is.LessThanOrEqualTo(maxSpeed * dt + Tolerance));
                previous = yaw;
                frames++;
            }
            Assert.That(frames, Is.InRange(85, 95), "180 degrees at 120 deg/s takes about 1.5 s");

            // Crossing the wrap point takes the short way round.
            float wrapped = CombatCameraFraming.RateLimitedYaw(170f, -170f, 1f, maxSpeed, 1f);
            Assert.That(wrapped, Is.EqualTo(-170f).Within(Tolerance));
        }

        [Test]
        public void KeepAboveFloorUsesRealFloorHeight()
        {
            // MainArena's walkable surface is y = 0.82, not the world origin.
            Vector3 below = CombatCameraFraming.KeepAboveFloor(new Vector3(1f, 0.5f, 2f), 0.82f, 0.4f);
            Assert.That(Vector3.Distance(below, new Vector3(1f, 1.22f, 2f)), Is.LessThan(Tolerance));

            Vector3 above = CombatCameraFraming.KeepAboveFloor(new Vector3(1f, 5f, 2f), 0.82f, 0.4f);
            Assert.That(above.y, Is.EqualTo(5f));
        }
    }
}
