using System.Collections.Generic;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Plays a frozen <see cref="MatchReplayClip"/> on the result screen with look-only proxies. The
    /// live robots stay where they are, hidden; nothing is simulated again, so playback cannot change
    /// health, score or the match result. Runs on unscaled time and pauses while settings are open.
    /// </summary>
    /// <remarks>
    /// Detached parts disappear from the robot at the moment they came off. The flight of the debris
    /// is not recorded: the damage system keeps its debris list private.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MatchReplayPlayer : MonoBehaviour
    {
        private const int MarkerSegments = 32;

        [SerializeField, Range(0.1f, 1f)] private float slowMotionSpeed = 0.5f;
        [Tooltip("How long earlier hits flash; the deciding hit stays marked to the end.")]
        [SerializeField, Min(0f)] private float impactFlashSeconds = 0.35f;
        [SerializeField, Min(0.01f)] private float markerRadius = 0.2f;
        [SerializeField] private Color impactColor = new Color(1f, 0.45f, 0.1f);
        [SerializeField] private Color lastImpactColor = new Color(1f, 0.92f, 0.2f);

        private readonly MatchPresentationScope _scope = new MatchPresentationScope();
        private readonly List<MatchReplayClip.Impact> _impacts = new List<MatchReplayClip.Impact>();
        private MatchController _match;
        private SettingsPanelController _settings;
        private MatchReplayRecorder _recorder;
        private CombatCameraController _camera;
        private Transform _root, _playerRoot, _opponentRoot;
        private MeshRenderer[] _proxies;
        private MatchReplayClip.Pose[] _poses;
        private LineRenderer _marker;
        private Material _markerMaterial;

        public bool IsActive { get; private set; }
        public bool IsFinished { get; private set; }
        public bool IsSlowMotion { get; private set; }
        public bool IsPaused => _settings != null && _settings.IsOpen;
        /// <summary>Seconds since the start of the recorded window.</summary>
        public float PlaybackTime { get; private set; }
        public float Duration => _recorder != null && _recorder.Clip != null ? _recorder.Clip.Duration : 0f;
        public Transform PlayerProxy => _playerRoot;
        public Transform OpponentProxy => _opponentRoot;
        public Transform ProxyRoot => _root;
        public bool IsImpactMarkerVisible => _marker != null && _marker.enabled;
        public Vector3 ImpactMarkerPosition => _marker != null ? _marker.transform.position : Vector3.zero;
        /// <summary>The proxy drawn for a recorder track, or null when inactive or the source was gone.</summary>
        public MeshRenderer GetProxy(int track) => _proxies != null && (uint)track < (uint)_proxies.Length ? _proxies[track] : null;
        /// <summary>
        /// Offered on the result screen when a clip exists and no other presentation (inspection) has
        /// taken the camera.
        /// </summary>
        public bool CanBegin => !IsActive && _match != null && _match.State == MatchController.MatchState.Result &&
                                _recorder != null && _recorder.HasReplay &&
                                (_camera == null || !_camera.HasPresentationTargets);

        public void Bind(MatchController match, MatchReplayRecorder recorder, CombatCameraController camera)
        {
            if (IsActive) Stop();
            _match = match;
            _settings = match != null ? match.GetComponent<SettingsPanelController>() : null;
            _recorder = recorder;
            _camera = camera;
        }

        public bool Begin()
        {
            if (!CanBegin) return false;
            MatchReplayClip clip = _recorder.Clip;
            _root = RobotVisualProxyFactory.CreateRoot("Match Replay");
            _playerRoot = RobotVisualProxyFactory.CreateRoot("Replay Player", _root);
            _opponentRoot = RobotVisualProxyFactory.CreateRoot("Replay Opponent", _root);
            _proxies = new MeshRenderer[clip.TrackCount];
            _poses = new MatchReplayClip.Pose[clip.TrackCount];
            for (int i = 0; i < clip.TrackCount; i++)
            {
                Transform parent = _recorder.TrackRobot(i) == MatchReplayRecorder.PlayerRobot ? _playerRoot : _opponentRoot;
                _proxies[i] = RobotVisualProxyFactory.CreateProxy(_recorder.Tracks[i], parent);
            }
            clip.CopyImpacts(_impacts);
            CreateMarker();

            _scope.Enter(_match, _camera, _playerRoot, _opponentRoot);
            IsActive = true;
            IsSlowMotion = false;
            Restart();
            return true;
        }

        public void Restart()
        {
            if (!IsActive) return;
            PlaybackTime = 0f;
            IsFinished = Duration <= 0f;
            ApplyFrame();
        }

        public void Seek(float seconds)
        {
            if (!IsActive) return;
            PlaybackTime = Mathf.Clamp(seconds, 0f, Duration);
            IsFinished = PlaybackTime >= Duration;
            ApplyFrame();
        }

        public void SetSlowMotion(bool slow) => IsSlowMotion = slow;

        public void Stop()
        {
            if (!IsActive) return;
            IsActive = false;
            _scope.Exit();
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
                Destroy(_root.gameObject);
            }
            if (_markerMaterial != null) Destroy(_markerMaterial);
            _root = _playerRoot = _opponentRoot = null;
            _proxies = null;
            _marker = null;
            _markerMaterial = null;
            _impacts.Clear();
        }

        private void Update()
        {
            if (!IsActive) return;
            if (_match == null || _match.State != MatchController.MatchState.Result || _recorder == null || !_recorder.HasReplay)
            {
                Stop();
                return;
            }
            if (!IsPaused && !IsFinished)
            {
                PlaybackTime += Time.unscaledDeltaTime * (IsSlowMotion ? slowMotionSpeed : 1f);
                if (PlaybackTime >= Duration)
                {
                    PlaybackTime = Duration;
                    IsFinished = true;
                }
            }
            ApplyFrame();
        }

        private void OnDisable() => Stop();

        private void ApplyFrame()
        {
            MatchReplayClip clip = _recorder.Clip;
            float time = clip.StartTime + PlaybackTime;
            if (!clip.Sample(time, _poses)) return;
            for (int i = 0; i < _proxies.Length; i++)
            {
                MeshRenderer proxy = _proxies[i];
                if (proxy == null) continue;
                proxy.transform.SetPositionAndRotation(_poses[i].Position, _poses[i].Rotation);
                proxy.enabled = _poses[i].Visible;
            }
            UpdateMarker(time);
        }

        private void UpdateMarker(float time)
        {
            if (_marker == null) return;
            int shown = -1;
            for (int i = _impacts.Count - 1; i >= 0; i--)
            {
                if (_impacts[i].Time > time) continue;
                bool deciding = i == _impacts.Count - 1;
                if (deciding || time - _impacts[i].Time <= impactFlashSeconds) shown = i;
                break;
            }
            _marker.enabled = shown >= 0;
            if (shown < 0) return;
            MatchReplayClip.Impact impact = _impacts[shown];
            Vector3 normal = impact.Normal.sqrMagnitude > 0.0001f ? impact.Normal.normalized : Vector3.up;
            _marker.transform.SetPositionAndRotation(impact.Point + normal * 0.02f, Quaternion.FromToRotation(Vector3.forward, normal));
            _markerMaterial.color = shown == _impacts.Count - 1 ? lastImpactColor : impactColor;
        }

        private void CreateMarker()
        {
            Shader shader = Resources.Load<Shader>("SelectionRing");
            if (shader == null) return;
            var markerObject = new GameObject("Impact Marker");
            markerObject.transform.SetParent(_root, false);
            _markerMaterial = new Material(shader) { name = "Runtime Replay Impact Marker" };
            _marker = markerObject.AddComponent<LineRenderer>();
            _marker.sharedMaterial = _markerMaterial;
            _marker.useWorldSpace = false;
            _marker.loop = true;
            _marker.widthMultiplier = 0.035f;
            _marker.positionCount = MarkerSegments;
            _marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _marker.receiveShadows = false;
            for (int i = 0; i < MarkerSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / MarkerSegments;
                _marker.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * markerRadius);
            }
            _marker.enabled = false;
        }
    }
}
