using System.Collections.Generic;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Result-screen inspection of one robot's final damaged state. Selecting a robot freezes a
    /// look-only snapshot of its visible meshes and wear; the live robot is hidden, not changed. The
    /// snapshot does not follow the live robot and owns no debris, so it stays intact after the
    /// damage system destroys detached parts. Places where parts came off are outlined.
    /// Nothing here advances time or calls health, result or collision APIs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PostMatchInspection : MonoBehaviour
    {
        public const int PlayerRobot = MatchReplayRecorder.PlayerRobot;
        public const int OpponentRobot = MatchReplayRecorder.OpponentRobot;

        // A missing-part outline drawn as one line: bottom loop, top loop, then the remaining uprights.
        private static readonly int[] OutlinePath = { 0, 1, 2, 3, 0, 4, 5, 6, 7, 4, 5, 1, 2, 6, 7, 3 };

        [SerializeField, Range(5f, 80f)] private float inspectionPitch = 25f;
        [SerializeField, Min(0.5f)] private float inspectionDistance = 4.5f;
        [SerializeField] private Color missingPartColor = new Color(1f, 0.25f, 0.2f);

        private readonly MatchPresentationScope _scope = new MatchPresentationScope();
        private readonly List<MeshRenderer> _sources = new List<MeshRenderer>();
        private MatchController _match;
        private SettingsPanelController _settings;
        private CombatCameraController _camera;
        private MatchReplayPlayer _replay;
        private Transform _root;
        private Material _outlineMaterial;
        private float _savedYaw, _savedPitch, _savedDistance;

        public bool IsActive { get; private set; }
        /// <summary><see cref="PlayerRobot"/>, <see cref="OpponentRobot"/>, or -1 when inactive.</summary>
        public int SelectedRobot { get; private set; } = -1;
        public bool IsInterfaceHidden { get; private set; }
        public RobotInspectionReport Report { get; private set; }
        /// <summary>Root of the frozen snapshot the camera orbits.</summary>
        public Transform SnapshotRoot => _root;
        public int MissingPartOutlines { get; private set; }
        public bool CanBegin => !IsActive && _match != null && _match.State == MatchController.MatchState.Result;

        public void Bind(MatchController match, CombatCameraController camera, MatchReplayPlayer replay)
        {
            if (IsActive) Stop();
            _match = match;
            _settings = match != null ? match.GetComponent<SettingsPanelController>() : null;
            _camera = camera;
            _replay = replay;
        }

        /// <summary>Starts inspecting a robot; a running replay is closed first. False for an unknown robot.</summary>
        public bool Begin(int robot)
        {
            if (IsActive) return Select(robot);
            if (_match == null || _match.State != MatchController.MatchState.Result || RobotAt(robot) == null) return false;
            if (_replay != null && _replay.IsActive) _replay.Stop();
            if (_camera != null && _camera.HasPresentationTargets) return false;

            if (_camera != null)
            {
                _savedYaw = _camera.Yaw;
                _savedPitch = _camera.Pitch;
                _savedDistance = _camera.Distance;
            }
            IsActive = true;
            BuildSnapshot(robot);
            if (_camera != null) _camera.SetOrbit(_camera.Yaw, inspectionPitch, inspectionDistance);
            return true;
        }

        /// <summary>Switches the inspected robot; the orbit angle and zoom are kept.</summary>
        public bool Select(int robot)
        {
            if (!IsActive) return Begin(robot);
            if (RobotAt(robot) == null) return false;
            if (robot == SelectedRobot) return true;
            _scope.Exit();
            DestroySnapshot();
            BuildSnapshot(robot);
            return true;
        }

        public void SetInterfaceHidden(bool hidden)
        {
            if (!IsActive) hidden = false;
            IsInterfaceHidden = hidden;
            if (_camera != null) _camera.ControlsVisible = !hidden;
        }

        public void Stop()
        {
            if (!IsActive) return;
            IsActive = false;
            SetInterfaceHidden(false);
            _scope.Exit();
            DestroySnapshot();
            if (_camera != null) _camera.SetOrbit(_savedYaw, _savedPitch, _savedDistance);
        }

        private void Update()
        {
            if (!IsActive) return;
            if (_match == null || _match.State != MatchController.MatchState.Result)
            {
                Stop();
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame) ProcessToggleKey();
        }

        /// <summary>The H shortcut: hides or shows the interface unless settings are open.</summary>
        public bool ProcessToggleKey()
        {
            if (!IsActive || (_settings != null && _settings.IsOpen)) return false;
            SetInterfaceHidden(!IsInterfaceHidden);
            return true;
        }

        private void OnDisable() => Stop();

        private RobotPrototypeController RobotAt(int robot)
        {
            if (_match == null) return null;
            return robot == PlayerRobot ? _match.Player : robot == OpponentRobot ? _match.Opponent : null;
        }

        private void BuildSnapshot(int robot)
        {
            RobotPrototypeController source = RobotAt(robot);
            SelectedRobot = robot;
            Report = RobotInspectionReport.Capture(source);
            _root = RobotVisualProxyFactory.CreateRoot("Robot Inspection " + source.name);

            RobotVisualProxyFactory.CollectMeshRenderers(source.transform, _sources);
            foreach (MeshRenderer renderer in _sources)
                if (RobotVisualProxyFactory.IsVisible(renderer))
                    RobotVisualProxyFactory.CreateProxy(renderer, _root);
            _sources.Clear();

            MissingPartOutlines = 0;
            var visuals = source.GetComponent<RobotDamageVisuals>();
            if (visuals != null)
                foreach (RobotDamageVisuals.Part part in visuals.Parts)
                    if (part.Detached && part.Transform != null && part.Mesh != null)
                        CreateMissingPartOutline(part);

            _scope.Enter(_match, _camera, _root, null);
        }

        private void DestroySnapshot()
        {
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
                Destroy(_root.gameObject);
            }
            if (_outlineMaterial != null) Destroy(_outlineMaterial);
            _root = null;
            _outlineMaterial = null;
            Report = null;
            SelectedRobot = -1;
            MissingPartOutlines = 0;
        }

        /// <summary>Outlines the bounds a detached part occupied, at its place on the robot.</summary>
        private void CreateMissingPartOutline(RobotDamageVisuals.Part part)
        {
            if (_outlineMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("SelectionRing");
                if (shader == null) return;
                _outlineMaterial = new Material(shader) { name = "Runtime Inspection Missing Part", color = missingPartColor };
            }
            var outline = new GameObject("Missing " + part.Name);
            outline.transform.SetParent(_root, false);
            outline.transform.SetPositionAndRotation(part.Transform.position, part.Transform.rotation);
            outline.transform.localScale = part.Transform.lossyScale;
            var line = outline.AddComponent<LineRenderer>();
            line.sharedMaterial = _outlineMaterial;
            line.useWorldSpace = false;
            line.widthMultiplier = 0.02f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            Bounds bounds = part.Mesh.bounds;
            line.positionCount = OutlinePath.Length;
            for (int i = 0; i < OutlinePath.Length; i++)
                line.SetPosition(i, Corner(bounds, OutlinePath[i]));
            MissingPartOutlines++;
        }

        // Corners 0-3 form the bottom loop, 4-7 the matching top loop.
        private static Vector3 Corner(Bounds bounds, int index)
        {
            int loop = index & 3;
            float x = loop == 1 || loop == 2 ? 1f : -1f;
            float z = loop >= 2 ? 1f : -1f;
            float y = index >= 4 ? 1f : -1f;
            return bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
        }
    }
}
