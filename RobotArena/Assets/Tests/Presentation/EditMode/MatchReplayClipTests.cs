using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RobotSavaslari.Presentation.Tests
{
    public sealed class MatchReplayClipTests
    {
        private const float Tolerance = 1e-4f;

        private static MatchReplayClip.Pose[] Frame(float x, bool visible = true, float yaw = 0f)
        {
            return new[]
            {
                new MatchReplayClip.Pose(new Vector3(x, 0.82f, 0f), Quaternion.Euler(0f, yaw, 0f), visible),
                new MatchReplayClip.Pose(new Vector3(-x, 0.82f, 0f), Quaternion.identity, true),
            };
        }

        [Test]
        public void EmptyClipCannotBeSampled()
        {
            var clip = new MatchReplayClip(2, 10);
            Assert.That(clip.Count, Is.Zero);
            Assert.That(clip.Duration, Is.Zero);
            Assert.That(clip.Sample(0f, new MatchReplayClip.Pose[2]), Is.False);
            Assert.That(clip.TryGetLastImpact(out _), Is.False);
        }

        [Test]
        public void SingleFrameClipReturnsThatFrameForAnyTime()
        {
            var clip = new MatchReplayClip(2, 10);
            Assert.That(clip.Record(3f, Frame(1.5f)), Is.True);
            var output = new MatchReplayClip.Pose[2];
            foreach (float time in new[] { -10f, 3f, 99f })
            {
                Assert.That(clip.Sample(time, output), Is.True);
                Assert.That(output[0].Position.x, Is.EqualTo(1.5f));
                Assert.That(output[0].Visible, Is.True);
            }
            Assert.That(clip.Duration, Is.Zero);
        }

        [Test]
        public void RingBufferKeepsOnlyTheNewestFramesInFixedMemory()
        {
            var clip = new MatchReplayClip(2, 151);
            int storage = clip.PoseStorageLength;
            for (int i = 0; i < 1000; i++) clip.Record(i / 30f, Frame(i));

            Assert.That(clip.Count, Is.EqualTo(151));
            Assert.That(clip.PoseStorageLength, Is.EqualTo(storage), "storage does not grow");
            Assert.That(clip.StartTime, Is.EqualTo(849 / 30f).Within(Tolerance));
            Assert.That(clip.EndTime, Is.EqualTo(999 / 30f).Within(Tolerance));
            Assert.That(clip.Duration, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(clip.GetPose(0, 0).Position.x, Is.EqualTo(849f));
            Assert.That(clip.GetPose(150, 0).Position.x, Is.EqualTo(999f));
            for (int i = 1; i < clip.Count; i++)
                Assert.That(clip.GetFrameTime(i), Is.GreaterThan(clip.GetFrameTime(i - 1)), "chronological after wrap");
        }

        [Test]
        public void RepeatedOrOlderTimesAndWrongTrackCountsAreIgnored()
        {
            var clip = new MatchReplayClip(2, 10);
            Assert.That(clip.Record(1f, Frame(1f)), Is.True);
            Assert.That(clip.Record(1f, Frame(2f)), Is.False);
            Assert.That(clip.Record(0.5f, Frame(3f)), Is.False);
            Assert.That(clip.Record(2f, new MatchReplayClip.Pose[1]), Is.False);
            Assert.That(clip.Record(float.NaN, Frame(4f)), Is.False);
            Assert.That(clip.Count, Is.EqualTo(1));
        }

        [Test]
        public void SamplingInterpolatesPositionAndRotationAndClamps()
        {
            var clip = new MatchReplayClip(2, 10);
            clip.Record(0f, Frame(0f, true, 0f));
            clip.Record(1f, Frame(10f, true, 90f));
            var output = new MatchReplayClip.Pose[2];

            clip.Sample(0.25f, output);
            Assert.That(output[0].Position.x, Is.EqualTo(2.5f).Within(Tolerance));
            Assert.That(output[1].Position.x, Is.EqualTo(-2.5f).Within(Tolerance));
            Assert.That(Quaternion.Angle(output[0].Rotation, Quaternion.Euler(0f, 22.5f, 0f)), Is.LessThan(0.01f));

            clip.Sample(-5f, output);
            Assert.That(output[0].Position.x, Is.EqualTo(0f));
            clip.Sample(5f, output);
            Assert.That(output[0].Position.x, Is.EqualTo(10f));
        }

        [Test]
        public void SkippedFramesStillInterpolateAcrossTheGap()
        {
            var clip = new MatchReplayClip(2, 10);
            clip.Record(0f, Frame(0f));
            clip.Record(0.9f, Frame(9f)); // a long hitch between samples
            var output = new MatchReplayClip.Pose[2];
            clip.Sample(0.45f, output);
            Assert.That(output[0].Position.x, Is.EqualTo(4.5f).Within(Tolerance));
        }

        [Test]
        public void DetachedPartDisappearsAfterLastVisibleFrame()
        {
            var clip = new MatchReplayClip(2, 10);
            clip.Record(0f, Frame(0f, true));
            clip.Record(1f, Frame(1f, true));
            clip.Record(2f, Frame(2f, false));
            var output = new MatchReplayClip.Pose[2];

            clip.Sample(1f, output);
            Assert.That(output[0].Visible, Is.True, "still attached at its last frame");
            clip.Sample(1.5f, output);
            Assert.That(output[0].Visible, Is.False, "gone once playback moves towards the detached frame");
            clip.Sample(2f, output);
            Assert.That(output[0].Visible, Is.False);
            Assert.That(output[1].Visible, Is.True, "other tracks unaffected");
        }

        [Test]
        public void ImpactsOutsideTheWindowAreDroppedAndCapacityIsBounded()
        {
            var clip = new MatchReplayClip(2, 3, 4);
            for (int i = 0; i < 6; i++)
            {
                clip.Record(i, Frame(i));
                clip.RecordImpact(new MatchReplayClip.Impact { Time = i, Robot = i % 2, Point = new Vector3(i, 1f, 0f), Normal = Vector3.up, Amount = 5f });
            }
            // Frames 3..5 remain; impacts 2..5 remain in the ring, only 3..5 are inside the window.
            var impacts = new List<MatchReplayClip.Impact>();
            Assert.That(clip.CopyImpacts(impacts), Is.EqualTo(3));
            Assert.That(impacts[0].Time, Is.EqualTo(3f));
            Assert.That(clip.TryGetLastImpact(out MatchReplayClip.Impact last), Is.True);
            Assert.That(last.Point.x, Is.EqualTo(5f));
            Assert.That(last.Robot, Is.EqualTo(1));
        }

        [Test]
        public void ClearEmptiesFramesAndImpacts()
        {
            var clip = new MatchReplayClip(2, 5);
            clip.Record(0f, Frame(0f));
            clip.RecordImpact(new MatchReplayClip.Impact { Time = 0f });
            clip.Clear();
            Assert.That(clip.Count, Is.Zero);
            Assert.That(clip.TryGetLastImpact(out _), Is.False);
            Assert.That(clip.Record(0f, Frame(0f)), Is.True, "time may start over after a clear");
        }
    }
}
