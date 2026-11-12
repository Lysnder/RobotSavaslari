using System;
using System.Collections.Generic;
using RobotSavaslari.Match;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Samples the two match robots' visible parts into a bounded <see cref="MatchReplayClip"/> while a
    /// match is playing. The clip is frozen when the result is decided and cleared when the next match
    /// starts. It only reads transforms and listens to events; it never calls health, match or
    /// collision APIs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchReplayRecorder : MonoBehaviour
    {
        public const int PlayerRobot = 0;
        public const int OpponentRobot = 1;

        [Tooltip("Seconds of play kept for the replay.")]
        [SerializeField, Range(1f, 15f)] private float bufferSeconds = 5f;
        [SerializeField, Range(10, 60)] private int samplesPerSecond = 30;

        private readonly List<MeshRenderer> _tracks = new List<MeshRenderer>();
        private readonly List<int> _trackRobots = new List<int>();
        private readonly List<MeshRenderer> _scratchRenderers = new List<MeshRenderer>();
        private MatchController _match;
        private RobotHealth _playerHealth, _opponentHealth;
        private Action<RobotHit> _onPlayerDamaged, _onOpponentDamaged;
        private MatchReplayClip.Pose[] _poses = Array.Empty<MatchReplayClip.Pose>();
        private int _recordedMatch = -1;
        private float _nextSampleTime;

        public MatchReplayClip Clip { get; private set; }
        public bool IsRecording { get; private set; }
        /// <summary>True once the result is decided: the clip no longer changes until the next match.</summary>
        public bool IsFrozen { get; private set; }
        public int RecordedMatch => _recordedMatch;
        public float BufferSeconds => bufferSeconds;
        public int SamplesPerSecond => samplesPerSecond;
        public int FrameCapacity => Mathf.CeilToInt(bufferSeconds * samplesPerSecond) + 1;
        public IReadOnlyList<MeshRenderer> Tracks => _tracks;
        public bool HasReplay => IsFrozen && Clip != null && Clip.Count > 0;

        public int TrackRobot(int track) => _trackRobots[track];

        public void Bind(MatchController match)
        {
            if (_match == match) return;
            Unsubscribe();
            _match = match;
            ClearRecording();
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_match == null || _playerHealth != null || _opponentHealth != null) return;
            _match.ResultDetermined += OnResultDetermined;
            _playerHealth = _match.Player != null ? _match.Player.GetComponent<RobotHealth>() : null;
            _opponentHealth = _match.Opponent != null ? _match.Opponent.GetComponent<RobotHealth>() : null;
            _onPlayerDamaged = hit => OnDamaged(PlayerRobot, hit);
            _onOpponentDamaged = hit => OnDamaged(OpponentRobot, hit);
            if (_playerHealth != null) _playerHealth.Damaged += _onPlayerDamaged;
            if (_opponentHealth != null) _opponentHealth.Damaged += _onOpponentDamaged;
        }

        private void Unsubscribe()
        {
            if (_match != null) _match.ResultDetermined -= OnResultDetermined;
            if (_playerHealth != null) _playerHealth.Damaged -= _onPlayerDamaged;
            if (_opponentHealth != null) _opponentHealth.Damaged -= _onOpponentDamaged;
            _playerHealth = _opponentHealth = null;
        }

        private void LateUpdate()
        {
            if (_match == null || !_match.IsInitialized) return;
            if (_match.State == MatchController.MatchState.Playing)
            {
                if (_recordedMatch != _match.MatchCount) BeginRecording();
                if (IsRecording && Time.time >= _nextSampleTime) Capture(Time.time);
            }
            else if (_match.State != MatchController.MatchState.Result && (IsRecording || IsFrozen || (Clip != null && Clip.Count > 0)))
            {
                // Ready, countdown or restart: the previous match's replay is no longer offered.
                ClearRecording();
            }
        }

        private void BeginRecording()
        {
            _tracks.Clear();
            _trackRobots.Clear();
            AddTracks(_match.Player != null ? _match.Player.transform : null, PlayerRobot);
            AddTracks(_match.Opponent != null ? _match.Opponent.transform : null, OpponentRobot);
            int capacity = FrameCapacity;
            if (Clip == null || Clip.TrackCount != _tracks.Count || Clip.Capacity != capacity)
            {
                Clip = new MatchReplayClip(_tracks.Count, capacity);
                _poses = new MatchReplayClip.Pose[_tracks.Count];
            }
            Clip.Clear();
            _recordedMatch = _match.MatchCount;
            IsRecording = true;
            IsFrozen = false;
            _nextSampleTime = 0f;
        }

        private void AddTracks(Transform robot, int robotIndex)
        {
            RobotVisualProxyFactory.CollectMeshRenderers(robot, _scratchRenderers);
            foreach (MeshRenderer renderer in _scratchRenderers)
            {
                _tracks.Add(renderer);
                _trackRobots.Add(robotIndex);
            }
        }

        private void Capture(float time)
        {
            for (int i = 0; i < _tracks.Count; i++)
            {
                MeshRenderer renderer = _tracks[i];
                _poses[i] = renderer != null
                    ? new MatchReplayClip.Pose(renderer.transform.position, renderer.transform.rotation, RobotVisualProxyFactory.IsVisible(renderer))
                    : new MatchReplayClip.Pose(Vector3.zero, Quaternion.identity, false);
            }
            if (Clip.Record(time, _poses)) _nextSampleTime = time + 1f / samplesPerSecond;
        }

        private void OnDamaged(int robot, RobotHit hit)
        {
            if (!IsRecording || Clip == null) return;
            Clip.RecordImpact(new MatchReplayClip.Impact
            {
                Time = Time.time, Robot = robot, Point = hit.Point, Normal = hit.Normal, Amount = hit.Amount
            });
        }

        private void OnResultDetermined(MatchController.MatchOutcome outcome)
        {
            if (!IsRecording)
            {
                // A match decided before the first sample still gets a one-frame clip of its final look.
                if (_match == null || _recordedMatch == _match.MatchCount) return;
                BeginRecording();
            }
            // The deciding hit has already detached parts; keep that final look as the last frame.
            // A same-time frame is ignored by the clip, so nudge it forward by a tick.
            float time = Clip.Count > 0 && Time.time <= Clip.EndTime ? Clip.EndTime + 1e-4f : Time.time;
            Capture(time);
            IsRecording = false;
            IsFrozen = true;
        }

        private void ClearRecording()
        {
            Clip?.Clear();
            IsRecording = false;
            IsFrozen = false;
            _recordedMatch = -1;
        }
    }
}
