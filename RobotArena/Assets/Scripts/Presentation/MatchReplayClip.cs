using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Fixed-size visual recording of the last seconds of a match: one pose per tracked renderer per
    /// frame, plus impact points. Memory is allocated once; when full, the oldest frame is overwritten.
    /// Pure data, no scene access, so sampling and bounds are testable on their own.
    /// </summary>
    public sealed class MatchReplayClip
    {
        public struct Pose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public bool Visible;

            public Pose(Vector3 position, Quaternion rotation, bool visible)
            {
                Position = position;
                Rotation = rotation;
                Visible = visible;
            }
        }

        public struct Impact
        {
            public float Time;
            public int Robot;
            public Vector3 Point;
            public Vector3 Normal;
            public float Amount;
        }

        private readonly float[] _times;
        private readonly Pose[] _poses;
        private readonly Impact[] _impacts;
        private int _head, _impactHead, _impactCount;

        public MatchReplayClip(int trackCount, int capacity, int impactCapacity = 32)
        {
            if (trackCount < 0) throw new ArgumentOutOfRangeException(nameof(trackCount));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            TrackCount = trackCount;
            Capacity = capacity;
            _times = new float[capacity];
            _poses = new Pose[capacity * trackCount];
            _impacts = new Impact[Mathf.Max(1, impactCapacity)];
        }

        public int TrackCount { get; }
        public int Capacity { get; }
        public int ImpactCapacity => _impacts.Length;
        /// <summary>Recorded frames, at most <see cref="Capacity"/>.</summary>
        public int Count { get; private set; }
        public float StartTime => Count == 0 ? 0f : _times[_head];
        public float EndTime => Count == 0 ? 0f : _times[Slot(Count - 1)];
        public float Duration => EndTime - StartTime;
        /// <summary>Fixed storage size; it does not grow with match length.</summary>
        public int PoseStorageLength => _poses.Length;

        public void Clear()
        {
            _head = Count = 0;
            _impactHead = _impactCount = 0;
        }

        /// <summary>Appends a frame. Frames must move forward in time; repeated or older times are ignored.</summary>
        public bool Record(float time, IReadOnlyList<Pose> poses)
        {
            if (poses == null || poses.Count != TrackCount || float.IsNaN(time)) return false;
            if (Count > 0 && time <= EndTime) return false;
            int slot;
            if (Count < Capacity)
            {
                slot = Slot(Count);
                Count++;
            }
            else
            {
                slot = _head;
                _head = (_head + 1) % Capacity;
            }
            _times[slot] = time;
            int offset = slot * TrackCount;
            for (int i = 0; i < TrackCount; i++) _poses[offset + i] = poses[i];
            return true;
        }

        public void RecordImpact(Impact impact)
        {
            int slot = (_impactHead + _impactCount) % _impacts.Length;
            if (_impactCount < _impacts.Length) _impactCount++;
            else _impactHead = (_impactHead + 1) % _impacts.Length;
            _impacts[slot] = impact;
        }

        /// <summary>Time of a recorded frame; 0 is the oldest.</summary>
        public float GetFrameTime(int frame)
        {
            if ((uint)frame >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(frame));
            return _times[Slot(frame)];
        }

        public Pose GetPose(int frame, int track)
        {
            if ((uint)frame >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(frame));
            if ((uint)track >= (uint)TrackCount) throw new ArgumentOutOfRangeException(nameof(track));
            return _poses[Slot(frame) * TrackCount + track];
        }

        /// <summary>
        /// Writes the interpolated pose of every track at <paramref name="time"/> (clamped to the clip).
        /// Positions are blended linearly and rotations spherically. Visibility steps: a part counts as
        /// visible only while both neighbouring frames show it, so a detached part disappears as soon
        /// as playback leaves the last frame that still showed it.
        /// </summary>
        public bool Sample(float time, Pose[] output)
        {
            if (Count == 0 || output == null || output.Length < TrackCount) return false;
            time = Mathf.Clamp(time, StartTime, EndTime);
            int upper = FindUpperFrame(time);
            int lower = Mathf.Max(0, upper - 1);
            float t0 = GetFrameTime(lower), t1 = GetFrameTime(upper);
            float blend = t1 > t0 ? Mathf.Clamp01((time - t0) / (t1 - t0)) : 1f;
            int a = Slot(lower) * TrackCount, b = Slot(upper) * TrackCount;
            for (int i = 0; i < TrackCount; i++)
            {
                Pose from = _poses[a + i], to = _poses[b + i];
                output[i] = new Pose(
                    Vector3.Lerp(from.Position, to.Position, blend),
                    Quaternion.Slerp(Normalize(from.Rotation), Normalize(to.Rotation), blend),
                    blend >= 1f ? to.Visible : from.Visible && to.Visible);
            }
            return true;
        }

        /// <summary>The latest impact inside the recorded window.</summary>
        public bool TryGetLastImpact(out Impact impact)
        {
            for (int i = _impactCount - 1; i >= 0; i--)
            {
                Impact candidate = _impacts[(_impactHead + i) % _impacts.Length];
                if (Count > 0 && candidate.Time >= StartTime)
                {
                    impact = candidate;
                    return true;
                }
            }
            impact = default;
            return false;
        }

        /// <summary>Impacts inside the recorded window, oldest first.</summary>
        public int CopyImpacts(List<Impact> output)
        {
            output.Clear();
            if (Count == 0) return 0;
            for (int i = 0; i < _impactCount; i++)
            {
                Impact impact = _impacts[(_impactHead + i) % _impacts.Length];
                if (impact.Time >= StartTime) output.Add(impact);
            }
            return output.Count;
        }

        private int Slot(int frame) => (_head + frame) % Capacity;

        // First frame whose time is >= time (binary search over the chronological order).
        private int FindUpperFrame(float time)
        {
            int low = 0, high = Count - 1;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (GetFrameTime(mid) < time) low = mid + 1;
                else high = mid;
            }
            return low;
        }

        private static Quaternion Normalize(Quaternion q)
        {
            float magnitude = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return magnitude > 1e-6f ? new Quaternion(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude) : Quaternion.identity;
        }
    }
}
