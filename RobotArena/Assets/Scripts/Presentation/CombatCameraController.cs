using System.Collections.Generic;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Drives the arena's existing camera. <see cref="CameraMode.Follow"/> orbits the match's player
    /// robot; <see cref="CameraMode.Duel"/> frames the player and the opponent together. Both modes
    /// keep the near plane out of static arena geometry. The controller never creates cameras or
    /// listeners and never touches physics bodies.
    /// </summary>
    /// <remarks>
    /// The chosen mode is a session preference: it survives new matches and is only changed by
    /// the player (button or C key) or <see cref="SetMode"/>. Sensitivity, invert Y, smoothing and
    /// the default distance are the player's persistent <see cref="GameSettings"/> values and are
    /// read every frame, so changes in the settings panel apply immediately.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CombatCameraController : MonoBehaviour
    {
        public enum CameraMode { Follow, Duel }

        private const float ReferenceHeight = 720f;
        private const float FloorProbeHeight = 0.5f;
        private const float FloorProbeDepth = 10f;
        private const float MaxFrameStep = 0.1f;

        [Header("Orbit")]
        [SerializeField, Range(0f, 89f)] private float defaultPitch = 42f;
        [SerializeField, Range(0f, 89f)] private float minPitch = 8f;
        [SerializeField, Range(1f, 89f)] private float maxPitch = 80f;
        [SerializeField, Min(0.5f)] private float minDistance = GameSettings.CameraDistanceMin;
        [SerializeField, Min(1f)] private float maxDistance = GameSettings.CameraDistanceMax;
        [Tooltip("Degrees of yaw per pixel at sensitivity 1; the player's setting multiplies it.")]
        [SerializeField, Min(0f)] private float horizontalDegreesPerPixel = 0.2f;
        [Tooltip("Degrees of pitch per pixel at sensitivity 1; the player's setting multiplies it.")]
        [SerializeField, Min(0f)] private float verticalDegreesPerPixel = 0.15f;
        [SerializeField, Range(0.02f, 0.5f)] private float zoomStep = 0.12f;

        [Header("Duel")]
        [Tooltip("Upper bound for how fast the duel view may turn, in degrees per second.")]
        [SerializeField, Min(1f)] private float maxDuelYawSpeed = 120f;
        [Tooltip("Below this ground distance the robots overlap and the previous duel direction is kept.")]
        [SerializeField, Min(0.05f)] private float minDuelSeparation = 0.6f;
        [SerializeField, Range(0f, 89f)] private float minDuelPitch = 30f;
        [SerializeField, Min(1f)] private float maxDuelDistance = 24f;
        [Tooltip("Extra room around both robots, as a ratio of the enclosing radius.")]
        [SerializeField, Range(0f, 1f)] private float duelFramingMargin = 0.15f;
        [Tooltip("The wheel may pull the duel view back up to this multiple of the fitted distance.")]
        [SerializeField, Range(1f, 3f)] private float maxDuelZoomOut = 1.75f;

        [Header("Smoothing")]
        [Tooltip("Follow sharpness at the softest smoothing setting.")]
        [SerializeField, Min(0.1f)] private float softestSharpness = 3f;
        [Tooltip("Follow sharpness just above zero smoothing.")]
        [SerializeField, Min(0.1f)] private float firmestSharpness = 30f;
        [SerializeField, Min(0f)] private float duelFramingSharpness = 4f;
        [Tooltip("A per-frame pivot jump larger than this is a respawn: the camera cuts instead of sweeping.")]
        [SerializeField, Min(0.5f)] private float teleportDistance = 2.5f;

        [Header("Collision")]
        [SerializeField, Min(0f)] private float floorClearance = 0.2f;
        [SerializeField, Min(0f)] private float obstructionMargin = 0.05f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private Camera _camera;
        private MatchController _match;
        private SettingsPanelController _settings;
        private Transform _target, _opponent;
        private Transform _matchPlayer;
        private Transform _presentationPrimary, _presentationSecondary;
        private bool _hasPresentationTargets;
        private CameraMode _mode = CameraMode.Follow;
        private float _defaultYaw;
        private float _yaw, _pitch, _distance;
        private float _duelYawOffset, _duelZoom = 1f, _duelAxisYaw;
        private float _currentYaw, _currentPitch, _currentDistance;
        private Vector3 _pivot, _lastTargetPivot, _lastOpponentPivot;
        private float _appliedDefaultDistance = -1f;
        private bool _hasPose;
        private GUIStyle _hintStyle;

        /// <summary>Draws the bottom-right camera buttons. Inspection turns this off while its UI is hidden.</summary>
        public bool ControlsVisible { get; set; } = true;

        public CameraMode Mode => _mode;
        public MatchController Match => _match;
        /// <summary>The followed robot (the match's player).</summary>
        public Transform Target => _target;
        /// <summary>The second robot framed in duel mode (the match's opponent).</summary>
        public Transform DuelOpponent => _opponent;
        /// <summary>True while replay or inspection has replaced the live robots as camera targets.</summary>
        public bool HasPresentationTargets => _hasPresentationTargets;
        /// <summary>True when duel mode is selected and both robots are available.</summary>
        public bool IsFramingDuel => _mode == CameraMode.Duel && _target != null && _opponent != null;
        public float Yaw => _yaw;
        public float Pitch => _pitch;
        public float Distance => _distance;
        public float DuelYawOffset => _duelYawOffset;
        public float DuelZoom => _duelZoom;
        /// <summary>The yaw the camera is actually showing this frame.</summary>
        public float ViewYaw => _currentYaw;
        public float ViewDistance => _currentDistance;
        public float DefaultYaw => _defaultYaw;
        public float DefaultPitch => defaultPitch;
        /// <summary>The player's saved follow distance, clamped to the zoom range.</summary>
        public float DefaultDistance => Mathf.Clamp(GameSettings.CameraDefaultDistance, minDistance, maxDistance);
        /// <summary>Sharpness used for follow and orbit easing, derived from the smoothing setting.</summary>
        public float FollowSharpness => CombatCameraFraming.SharpnessFromSmoothing(GameSettings.CameraSmoothing, softestSharpness, firmestSharpness);
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public float MaxDuelYawSpeed => maxDuelYawSpeed;
        public float MaxDuelDistance => maxDuelDistance;
        public float MaxFrameDelta => MaxFrameStep;
        public Vector3 Pivot => _pivot;
        public float FloorHeight { get; private set; }
        public float NearPlaneRadius => CombatCameraFraming.NearPlaneRadius(_camera.nearClipPlane, _camera.fieldOfView, _camera.aspect);
        public bool AcceptsPointerInput => isActiveAndEnabled && _match != null && (_settings == null || !_settings.IsOpen);

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _pitch = _currentPitch = defaultPitch;
            _distance = _currentDistance = DefaultDistance;
            _appliedDefaultDistance = DefaultDistance;
        }

        private void OnEnable()
        {
            _hasPose = false;
        }

        /// <summary>Connects the camera to the arena's match. Safe to call repeatedly.</summary>
        public void Bind(MatchController match)
        {
            if (_match == match) return;
            _match = match;
            _settings = match != null ? match.GetComponent<SettingsPanelController>() : null;
            _target = _opponent = _matchPlayer = null;
            _hasPose = false;
        }

        /// <summary>
        /// Temporarily frames presentation objects (replay or inspection proxies) instead of the live
        /// robots. The current mode and orbit are kept; the view cuts to the new targets. With no
        /// secondary target duel mode falls back to orbiting the primary.
        /// </summary>
        public void SetPresentationTargets(Transform primary, Transform secondary)
        {
            _presentationPrimary = primary;
            _presentationSecondary = secondary;
            _hasPresentationTargets = primary != null;
            _hasPose = false;
        }

        /// <summary>Returns to the live match robots; the view cuts back to them.</summary>
        public void ClearPresentationTargets()
        {
            if (!_hasPresentationTargets) return;
            _presentationPrimary = _presentationSecondary = null;
            _hasPresentationTargets = false;
            _hasPose = false;
        }

        /// <summary>Selects the camera mode. The view turns into the new mode at the limited rate.</summary>
        public void SetMode(CameraMode mode)
        {
            if (mode == _mode) return;
            // Leaving duel keeps the current heading so follow mode starts without turning.
            if (_mode == CameraMode.Duel && _hasPose) _yaw = CombatCameraFraming.WrapYaw(_currentYaw);
            _mode = mode;
        }

        public void ToggleMode()
        {
            SetMode(_mode == CameraMode.Follow ? CameraMode.Duel : CameraMode.Follow);
        }

        /// <summary>Returns the current mode's angles and distance to their defaults; the camera eases back.</summary>
        public void ResetView()
        {
            _pitch = Mathf.Clamp(defaultPitch, minPitch, maxPitch);
            if (IsFramingDuel)
            {
                _duelYawOffset = 0f;
                _duelZoom = 1f;
                return;
            }
            _yaw = _defaultYaw;
            _distance = DefaultDistance;
        }

        /// <summary>Sets the follow orbit goal directly; values are clamped to the configured limits.</summary>
        public void SetOrbit(float yaw, float pitch, float distance)
        {
            _yaw = CombatCameraFraming.WrapYaw(yaw);
            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            _distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        /// <summary>Applies a mouse movement in pixels: offsets the duel view while framing two robots, orbits otherwise.</summary>
        public void ApplyOrbitInput(Vector2 pixels)
        {
            float vertical = GameSettings.CameraInvertY ? pixels.y : -pixels.y;
            float yawDelta = pixels.x * horizontalDegreesPerPixel * GameSettings.CameraHorizontalSensitivity;
            if (IsFramingDuel) _duelYawOffset = CombatCameraFraming.WrapYaw(_duelYawOffset + yawDelta);
            else _yaw = CombatCameraFraming.WrapYaw(_yaw + yawDelta);
            _pitch = Mathf.Clamp(_pitch + vertical * verticalDegreesPerPixel * GameSettings.CameraVerticalSensitivity, minPitch, maxPitch);
        }

        /// <summary>Applies one frame of wheel input; positive values move closer.</summary>
        public void ApplyZoomInput(float wheel)
        {
            if (IsFramingDuel)
                // Duel zoom never cuts a robot out of frame: it only adds room beyond the fitted distance.
                _duelZoom = CombatCameraFraming.ApplyZoom(_duelZoom, wheel, zoomStep, 1f, maxDuelZoomOut);
            else
                _distance = CombatCameraFraming.ApplyZoom(_distance, wheel, zoomStep, minDistance, maxDistance);
        }

        /// <summary>Reads the mouse for this frame. Returns true when the orbit goal changed.</summary>
        public bool ProcessPointerInput(Mouse mouse)
        {
            if (mouse == null || !AcceptsPointerInput) return false;
            bool changed = false;
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0f)
                {
                    ApplyOrbitInput(delta);
                    changed = true;
                }
            }
            float wheel = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(wheel, 0f))
            {
                ApplyZoomInput(wheel);
                changed = true;
            }
            return changed;
        }

        /// <summary>Reads the camera's keyboard shortcut (C toggles the mode). Returns true when the mode changed.</summary>
        public bool ProcessKeyboardInput(Keyboard keyboard)
        {
            if (keyboard == null || !AcceptsPointerInput || !keyboard.cKey.wasPressedThisFrame) return false;
            ToggleMode();
            return true;
        }

        private void Update()
        {
            ProcessKeyboardInput(Keyboard.current);
        }

        private void LateUpdate()
        {
            ApplyDefaultDistanceSetting();
            ProcessPointerInput(Mouse.current);
            RefreshTargets();
            if (_target == null) return;

            Vector3 targetPivot = MeasureTargetPivot(_target);
            Vector3 opponentPivot = _opponent != null ? MeasureTargetPivot(_opponent) : targetPivot;
            bool respawned = _hasPose &&
                (Jumped(targetPivot, _lastTargetPivot) || (_opponent != null && Jumped(opponentPivot, _lastOpponentPivot)));
            bool snap = !_hasPose || respawned;

            ComputeGoal(snap, out Vector3 goalPivot, out float goalYaw, out float goalPitch, out float goalDistance);
            if (snap)
            {
                _pivot = goalPivot;
                _currentYaw = goalYaw;
                _currentPitch = goalPitch;
                _currentDistance = goalDistance;
                _hasPose = true;
            }
            else
            {
                // Unscaled time keeps the camera responsive while gameplay is paused; the cap stops a
                // long hitch from turning into a single large jump.
                float dt = Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);
                float sharpness = FollowSharpness;
                float follow = CombatCameraFraming.SmoothingWeight(sharpness, dt);
                float orbit = CombatCameraFraming.SmoothingWeight(sharpness * 1.5f, dt);
                _pivot = Vector3.Lerp(_pivot, goalPivot, follow);
                _currentPitch = Mathf.Lerp(_currentPitch, goalPitch, orbit);
                if (IsFramingDuel)
                {
                    _currentYaw = CombatCameraFraming.RateLimitedYaw(_currentYaw, goalYaw, orbit, maxDuelYawSpeed, dt);
                    _currentDistance = Mathf.Lerp(_currentDistance, goalDistance,
                        CombatCameraFraming.SmoothingWeight(duelFramingSharpness, dt));
                }
                else
                {
                    _currentYaw = Mathf.LerpAngle(_currentYaw, goalYaw, orbit);
                    _currentDistance = Mathf.Lerp(_currentDistance, goalDistance, orbit);
                }
            }
            _lastTargetPivot = targetPivot;
            _lastOpponentPivot = opponentPivot;
            ApplyPose();
        }

        /// <summary>
        /// A changed default distance in the settings panel moves the follow camera to it at once, so the
        /// player sees the effect (also while paused: the camera runs on unscaled time).
        /// </summary>
        private void ApplyDefaultDistanceSetting()
        {
            float current = DefaultDistance;
            if (Mathf.Approximately(current, _appliedDefaultDistance)) return;
            _appliedDefaultDistance = current;
            _distance = current;
        }

        private bool Jumped(Vector3 now, Vector3 before)
        {
            return (now - before).sqrMagnitude > teleportDistance * teleportDistance;
        }

        private void ComputeGoal(bool snap, out Vector3 pivot, out float yaw, out float pitch, out float distance)
        {
            if (!IsFramingDuel || !TryGetRenderBounds(_target, out Bounds player) || !TryGetRenderBounds(_opponent, out Bounds opponent))
            {
                pivot = MeasureTargetPivot(_target);
                yaw = _yaw;
                pitch = _pitch;
                distance = _distance;
                return;
            }

            // Look past the player towards the opponent. The axis is only refreshed while the robots
            // are clearly apart; after a respawn it is taken fresh so no old heading carries over.
            float previousAxis = snap ? _defaultYaw : _duelAxisYaw;
            _duelAxisYaw = CombatCameraFraming.PairAxisYaw(player.center, opponent.center, previousAxis, minDuelSeparation);
            CombatCameraFraming.EnclosePair(player, opponent, out pivot, out float radius);
            float fitted = CombatCameraFraming.FitDistance(radius * (1f + duelFramingMargin), _camera.fieldOfView, _camera.aspect);
            yaw = CombatCameraFraming.WrapYaw(_duelAxisYaw + _duelYawOffset);
            pitch = Mathf.Max(_pitch, minDuelPitch);
            distance = Mathf.Clamp(fitted * _duelZoom, minDistance, maxDuelDistance);
        }

        private void RefreshTargets()
        {
            Transform matchPlayer = _match != null && _match.Player != null ? _match.Player.transform : null;
            Transform matchOpponent = _match != null && _match.Opponent != null ? _match.Opponent.transform : null;
            if (matchPlayer != _matchPlayer)
            {
                _matchPlayer = matchPlayer;
                if (matchPlayer != null)
                {
                    // The default view looks over the player's spawn heading towards the arena.
                    _defaultYaw = _duelAxisYaw = CombatCameraFraming.WrapYaw(matchPlayer.eulerAngles.y);
                    _yaw = _defaultYaw;
                    _distance = DefaultDistance;
                    _pitch = Mathf.Clamp(defaultPitch, minPitch, maxPitch);
                }
            }

            // A destroyed presentation target falls back to the live robots.
            bool presenting = _hasPresentationTargets && _presentationPrimary != null;
            Transform desired = presenting ? _presentationPrimary : matchPlayer;
            Transform desiredOpponent = presenting ? _presentationSecondary : matchOpponent;
            if (desired == _target && desiredOpponent == _opponent) return;
            _target = desired;
            _opponent = desiredOpponent;
            _hasPose = false;
        }

        private void ApplyPose()
        {
            float radius = NearPlaneRadius;
            Vector3 direction = CombatCameraFraming.OrbitDirection(_currentYaw, _currentPitch);
            float distance = ResolveObstruction(_pivot, direction, _currentDistance, radius);
            Vector3 position = _pivot + direction * distance;
            FloorHeight = MeasureFloorHeight(_pivot);
            position = CombatCameraFraming.KeepAboveFloor(position, FloorHeight, radius + floorClearance);
            Vector3 look = _pivot - position;
            Quaternion rotation = look.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(look, Vector3.up)
                : CombatCameraFraming.OrbitRotation(_currentYaw, _currentPitch);
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Shortens the orbit so the near plane stops in front of static geometry.</summary>
        private float ResolveObstruction(Vector3 pivot, Vector3 direction, float distance, float radius)
        {
            int count = Physics.SphereCastNonAlloc(pivot, radius, direction, _hits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float allowed = distance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                // Robots and debris move; only static arena colliders block the view.
                if (!IsStaticGeometry(hit.collider) || hit.distance <= 0f) continue;
                allowed = Mathf.Min(allowed, hit.distance - obstructionMargin);
            }
            return Mathf.Max(0.1f, allowed);
        }

        private float MeasureFloorHeight(Vector3 pivot)
        {
            Vector3 origin = pivot + Vector3.up * FloorProbeHeight;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, FloorProbeDepth,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (IsStaticGeometry(hit.collider) && hit.point.y > best) best = hit.point.y;
            }
            if (!float.IsNegativeInfinity(best)) return best;
            // No collider below: fall back to the robot's own footprint.
            return TryGetRenderBounds(_target, out Bounds bounds) ? bounds.min.y : pivot.y - 0.5f;
        }

        private Vector3 MeasureTargetPivot(Transform target)
        {
            return TryGetRenderBounds(target, out Bounds bounds) ? bounds.center : target.position + Vector3.up * 0.5f;
        }

        /// <summary>
        /// Visible mesh bounds of a robot, ignoring particles and selection lines. Detached debris is
        /// spawned outside the robot hierarchy, so it is never framed.
        /// </summary>
        private bool TryGetRenderBounds(Transform target, out Bounds bounds)
        {
            bounds = default;
            if (target == null) return false;
            target.GetComponentsInChildren(false, _renderers);
            bool found = false;
            foreach (Renderer renderer in _renderers)
            {
                if (!renderer.enabled || renderer.forceRenderingOff ||
                    !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            return found;
        }

        private static bool IsStaticGeometry(Collider collider)
        {
            return collider != null && !collider.isTrigger && collider.attachedRigidbody == null;
        }

        private void OnGUI()
        {
            if (_match == null || !_match.IsInitialized || _match.State == MatchController.MatchState.Ready ||
                !ControlsVisible || (_settings != null && _settings.IsOpen)) return;
            if (_hintStyle == null)
                _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleRight };
            Matrix4x4 previous = GUI.matrix;
            try
            {
                float scale = Mathf.Max(0.01f, Mathf.Min(Screen.height / ReferenceHeight, Screen.width / 600f));
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                GUI.Label(new Rect(width - 356f, height - 84f, 340f, 22f), "Sağ tık + sürükle: döndür   Tekerlek: yakınlaştır", _hintStyle);
                string modeLabel = _mode == CameraMode.Follow ? "Düello kamerası (C)" : "Takip kamerası (C)";
                if (GUI.Button(new Rect(width - 352f, height - 56f, 168f, 40f), modeLabel))
                    ToggleMode();
                if (GUI.Button(new Rect(width - 176f, height - 56f, 160f, 40f), "Kamerayı sıfırla"))
                    ResetView();
            }
            finally { GUI.matrix = previous; }
        }
    }
}
