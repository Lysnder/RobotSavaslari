using System.Collections.Generic;
using System.Reflection;
using RobotSavaslari.Arena;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
using RobotSavaslari.Training;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// The archive-open path: plays a saved encounter on the Ready screen of the live arena with
    /// look-only proxies built from MainArena's own robot meshes. It owns "archive mode" while its panel
    /// is open: the main menu, training entry, key bindings and driving input are switched off and given
    /// back exactly as they were. Playback reads one chunk at a time, runs on unscaled time, pauses while
    /// settings are open and never touches health, match state or physics; the live robots and arena are
    /// only hidden. The result-screen <see cref="MatchReplayPlayer"/> is a separate, unchanged path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchReplayArchivePlayer : MonoBehaviour
    {
        public enum View { Player, Opponent, Duel }

        public static readonly float[] Speeds = { 1f, 0.5f, 0.25f };
        private const int MarkerSegments = 32;
        private static readonly int WearId = Shader.PropertyToID("_Wear");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int[] HitIds =
        {
            Shader.PropertyToID("_Hit0"), Shader.PropertyToID("_Hit1"), Shader.PropertyToID("_Hit2"), Shader.PropertyToID("_Hit3"),
        };
        // MainMenuScreen.IsShowingReadyMenu is newer than this package's base; read it when it exists.
        private static readonly PropertyInfo ReadyMenuProperty = typeof(MainMenuScreen).GetProperty("IsShowingReadyMenu", BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo HistoryField = typeof(MainMenuScreen).GetField("_showHistory", BindingFlags.NonPublic | BindingFlags.Instance);
        private static Mesh _cubeMesh;

        [SerializeField, Min(0f)] private float impactFlashSeconds = 0.35f;
        [SerializeField, Min(0.01f)] private float markerRadius = 0.2f;
        [SerializeField] private Color impactColor = new Color(1f, 0.45f, 0.1f);
        [SerializeField, Range(5f, 80f)] private float inspectionPitch = 25f;
        [SerializeField, Min(0.5f)] private float inspectionDistance = 4.5f;

        private readonly MatchPresentationScope _scope = new MatchPresentationScope();
        private readonly BehaviourEnableScope _inputScope = new BehaviourEnableScope();
        private readonly List<Renderer> _liveLayout = new List<Renderer>();
        private readonly List<ReplayEvent> _impacts = new List<ReplayEvent>();
        private MatchController _match;
        private SettingsPanelController _settings;
        private CombatCameraController _camera;
        private MatchReplayPlayer _liveReplay;
        private PostMatchInspection _inspection;
        private MatchReplayArchiveReader _reader;
        private Transform _root, _playerRoot, _opponentRoot, _debrisRoot, _arenaRoot;
        private MeshRenderer[] _proxies;
        private MeshRenderer[] _templates;
        private MatchReplayClip.Pose[] _poses;
        private byte[] _wear, _appliedWear;
        private Vector4[] _marks, _appliedMarks;
        private MaterialPropertyBlock _block;
        private LineRenderer _marker;
        private Material _markerMaterial;
        private CombatCameraController.CameraMode _savedMode;
        private float _savedYaw, _savedPitch, _savedDistance;
        private bool _hasSavedCamera;

        public MatchReplayArchiveStore Store { get; private set; }
        /// <summary>The archive panel owns the Ready screen (menu, training entry and driving input are off).</summary>
        public bool IsArchiveModeOpen { get; private set; }
        /// <summary>An archive is being played.</summary>
        public bool IsActive { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsFinished { get; private set; }
        public bool IsSettingsOpen => _settings != null && _settings.IsOpen;
        public float Speed { get; private set; } = 1f;
        public View CurrentView { get; private set; } = View.Duel;
        /// <summary>The robot being inspected while paused, or -1.</summary>
        public int InspectedRobot { get; private set; } = -1;
        public float PlaybackTime { get; private set; }
        public float Duration => _reader != null ? _reader.EndTime - _reader.StartTime : 0f;
        public ReplayArchiveInfo Info => _reader?.Info;
        public MatchReplayArchiveReader Reader => _reader;
        /// <summary>Why the last open or playback failed or stopped, for the panel.</summary>
        public string LastError { get; private set; }
        public Transform ProxyRoot => _root;
        public Transform PlayerProxy => _playerRoot;
        public Transform OpponentProxy => _opponentRoot;
        public Transform DebrisProxyRoot => _debrisRoot;
        public Transform ArenaProxyRoot => _arenaRoot;
        public int ProxyCount { get; private set; }
        public bool IsImpactMarkerVisible => _marker != null && _marker.enabled;
        public MeshRenderer GetProxy(int track) => _proxies != null && (uint)track < (uint)_proxies.Length ? _proxies[track] : null;
        /// <summary>Behaviours archive mode turned off and will give back.</summary>
        public int OwnedBehaviourCount => _inputScope.Count;

        public void Bind(MatchController match, CombatCameraController camera, MatchReplayArchiveStore store,
            MatchReplayPlayer liveReplay, PostMatchInspection inspection)
        {
            if (IsArchiveModeOpen) CloseArchiveMode();
            _match = match;
            _settings = match != null ? match.GetComponent<SettingsPanelController>() : null;
            _camera = camera;
            Store = store;
            _liveReplay = liveReplay;
            _inspection = inspection;
            _installer = null;
            _trial = null;
        }

        private void Awake() => _block = new MaterialPropertyBlock();

        private void OnDisable() => CloseArchiveMode();

        private void OnDestroy() => CloseArchiveMode();

        // ---- Permission ------------------------------------------------------------------------------

        /// <summary>
        /// The archive can be opened only from the normal Ready menu: the match is set up, enabled and
        /// unpaused, the menu is the visible screen, and no settings, key capture, training or other
        /// presentation is open. Ready alone is not enough.
        /// </summary>
        public bool CanOpenArchiveMode(out string reason)
        {
            if (IsArchiveModeOpen) return Refuse("Tekrar arşivi zaten açık.", out reason);
            if (!AllowsArchive(out reason)) return false;
            MainMenuScreen menu = Menu;
            if (menu == null || !menu.isActiveAndEnabled || !MenuShowsReady(menu)) return Refuse("Ana menü görünür değil.", out reason);
            return true;
        }

        private bool AllowsArchive(out string reason)
        {
            reason = null;
            if (!isActiveAndEnabled) return Refuse("Tekrar arşivi kapalı.", out reason);
            if (_match == null || !_match.isActiveAndEnabled || !_match.IsInitialized) return Refuse("Maç hazır değil.", out reason);
            if (_match.State != MatchController.MatchState.Ready) return Refuse("Tekrar arşivi yalnız hazır ekranında açılır.", out reason);
            if (_match.IsPaused || IsSettingsOpen) return Refuse("Ayarlar açıkken arşiv açılmaz.", out reason);
            DriveBindingsScreen bindings = _match.Player != null ? _match.Player.GetComponent<DriveBindingsScreen>() : null;
            if (bindings != null && bindings.IsOpen) return Refuse("Tuş atama ekranı açık.", out reason);
            TimeTrialSession trial = TrialSession;
            if (trial != null && trial.State != TimeTrialState.Inactive) return Refuse("Sürüş eğitimi sürüyor.", out reason);
            if ((_liveReplay != null && _liveReplay.IsActive) || (_inspection != null && _inspection.IsActive) ||
                (!IsActive && _camera != null && _camera.HasPresentationTargets))
                return Refuse("Başka bir sunum açık.", out reason);
            if (Store == null) return Refuse("Tekrar arşivi bağlı değil.", out reason);
            return true;
        }

        private static bool MenuShowsReady(MainMenuScreen menu)
        {
            if (ReadyMenuProperty != null && ReadyMenuProperty.PropertyType == typeof(bool)) return (bool)ReadyMenuProperty.GetValue(menu);
            return HistoryField == null || HistoryField.FieldType != typeof(bool) || !(bool)HistoryField.GetValue(menu);
        }

        private static bool Refuse(string message, out string reason)
        {
            reason = message;
            return false;
        }

        private MainMenuScreen Menu => _match != null ? _match.GetComponent<MainMenuScreen>() : null;

        private DrivingFeatureInstaller _installer;
        private TimeTrialSession _trial;

        private DrivingFeatureInstaller Installer
        {
            get
            {
                if (_installer != null || _match == null) return _installer;
                foreach (DrivingFeatureInstaller installer in FindObjectsByType<DrivingFeatureInstaller>())
                    if (installer.gameObject.scene == _match.gameObject.scene) return _installer = installer;
                return null;
            }
        }

        private TimeTrialSession TrialSession
        {
            get
            {
                if (_trial != null || _match == null) return _trial;
                DrivingFeatureInstaller installer = Installer;
                if (installer != null && installer.TimeTrial != null) return _trial = installer.TimeTrial;
                foreach (TimeTrialSession session in FindObjectsByType<TimeTrialSession>())
                    if (session.gameObject.scene == _match.gameObject.scene) return _trial = session;
                return null;
            }
        }

        // ---- Archive mode ----------------------------------------------------------------------------

        /// <summary>Takes over the Ready screen for the archive panel. False (with <see cref="LastError"/>) when not allowed.</summary>
        public bool OpenArchiveMode()
        {
            if (!CanOpenArchiveMode(out string reason))
            {
                LastError = reason;
                return false;
            }
            LastError = null;
            _inputScope.Disable(Menu);
            DrivingFeatureInstaller installer = Installer;
            _inputScope.Disable(installer);
            if (installer != null) _inputScope.Disable(installer.GetComponent<TimeTrialHud>());
            if (_match.Player != null)
            {
                _inputScope.Disable(_match.Player.GetComponent<DriveBindingsScreen>());
                _inputScope.Disable(_match.Player.GetComponent<PlayerKeyboardDriver>());
                // The live boost gauge would sit under the playback bar showing the idle robot's energy.
                _inputScope.Disable(_match.Player.GetComponent<DriveBoostGauge>());
            }
            IsArchiveModeOpen = true;
            Store.CleanupTemporaryFiles();
            return true;
        }

        /// <summary>Stops playback and gives the Ready screen back exactly as it was.</summary>
        public void CloseArchiveMode()
        {
            Stop();
            if (!IsArchiveModeOpen && !_inputScope.IsActive) return;
            IsArchiveModeOpen = false;
            _inputScope.Restore();
            GUIUtility.keyboardControl = 0;
            GUIUtility.hotControl = 0;
        }

        // ---- Compatibility ---------------------------------------------------------------------------

        /// <summary>
        /// Null when this build and scene can draw the archive; otherwise why not. Every robot track must
        /// resolve to a mesh at the same path on a live robot of the same name with the same vertex count,
        /// and every arena track to a known layout object. Nothing is guessed or carried over.
        /// </summary>
        public string CheckCompatibility(ReplayArchiveInfo info) => ResolveTemplates(info, null, out _);

        private string ResolveTemplates(ReplayArchiveInfo info, MeshRenderer[] templates, out Material layoutMaterial)
        {
            layoutMaterial = null;
            if (info == null) return "Kayıt okunamadı.";
            if (info.Schema != MatchReplayArchive.SchemaVersion || info.ContentRevision != MatchReplayArchive.ContentRevision)
                return $"Uyumsuz: içerik revizyonu {info.ContentRevision}, bu sürüm {MatchReplayArchive.ContentRevision}.";
            foreach (ReplayCell cell in info.Cells)
                if (!MatchReplayArchive.IsSupportedCellContent(cell.Content)) return "Uyumsuz: desteklenmeyen arena içeriği (" + cell.Content + ").";
            if (_match == null) return "Uyumsuz: maç bulunamadı.";
            bool needsLayout = false;
            for (int t = 0; t < info.Tracks.Count; t++)
            {
                ReplayTrack track = info.Tracks[t];
                if (track.Kind == ReplayTrackKind.LayoutStatic || track.Kind == ReplayTrackKind.LayoutDynamic)
                {
                    needsLayout = true;
                    if (track.MeshSignature != CubeMesh.vertexCount) return "Uyumsuz: arena nesnesi görünümü değişti.";
                    continue;
                }
                string robotName = MatchReplayArchive.KeyRobotName(track.Key);
                string path = MatchReplayArchive.KeyRobotPath(track.Key);
                Transform robot = FindRobot(robotName);
                if (robot == null) return "Uyumsuz: robot bulunamadı (" + robotName + ").";
                string templatePath = StripDuplicateSuffix(path);
                Transform node = robot.Find(templatePath);
                var renderer = node != null ? node.GetComponent<MeshRenderer>() : null;
                var filter = node != null ? node.GetComponent<MeshFilter>() : null;
                if (renderer == null || filter == null || filter.sharedMesh == null) return "Uyumsuz: görsel parça bulunamadı (" + track.Key + ").";
                if (filter.sharedMesh.vertexCount != track.MeshSignature) return "Uyumsuz: parça görünümü değişti (" + track.Key + ").";
                if (templates != null) templates[t] = renderer;
            }
            if (needsLayout)
            {
                layoutMaterial = FindLayoutMaterial(info.LayoutMaterial);
                if (layoutMaterial == null) return "Uyumsuz: arena malzemesi bulunamadı (" + info.LayoutMaterial + ").";
            }
            return null;
        }

        private static string StripDuplicateSuffix(string path)
        {
            int hash = path.LastIndexOf('#');
            return hash > 0 && path.IndexOf('/', hash) < 0 ? path.Substring(0, hash) : path;
        }

        private Transform FindRobot(string name)
        {
            if (string.IsNullOrEmpty(name) || _match == null) return null;
            if (_match.Player != null && _match.Player.name == name) return _match.Player.transform;
            if (_match.Opponent != null && _match.Opponent.name == name) return _match.Opponent.transform;
            return null;
        }

        private Material FindLayoutMaterial(string name)
        {
            CollectLiveLayout();
            foreach (Renderer renderer in _liveLayout)
                if (renderer != null && renderer.sharedMaterial != null && (name == null || renderer.sharedMaterial.name == name))
                    return renderer.sharedMaterial;
            if (string.IsNullOrEmpty(name)) return null;
            foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
                if (material != null && material.name == name) return material;
            return null;
        }

        private void CollectLiveLayout()
        {
            _liveLayout.Clear();
            if (_match == null) return;
            foreach (ArenaEnvironment environment in FindObjectsByType<ArenaEnvironment>())
            {
                if (environment.gameObject.scene != _match.gameObject.scene) continue;
                Transform contents = environment.Contents;
                if (contents != null) _liveLayout.AddRange(contents.GetComponentsInChildren<Renderer>(true));
            }
        }

        private static Mesh CubeMesh
        {
            get
            {
                if (_cubeMesh != null) return _cubeMesh;
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                cube.SetActive(false);
                DestroyImmediate(cube);
                return _cubeMesh;
            }
        }

        // ---- Playback --------------------------------------------------------------------------------

        /// <summary>
        /// Opens an archive file for playback inside archive mode. The file must pass its header, footer
        /// and every chunk checksum, and <see cref="CheckCompatibility"/>. Returns false with
        /// <see cref="LastError"/> otherwise; nothing in the scene changes then.
        /// </summary>
        public bool Open(string path)
        {
            if (!IsArchiveModeOpen || IsActive)
            {
                LastError = IsActive ? "Bir kayıt zaten oynatılıyor." : "Tekrar arşivi açık değil.";
                return false;
            }
            if (!AllowsArchive(out string reason))
            {
                LastError = reason;
                return false;
            }
            MatchReplayArchiveReader reader = MatchReplayArchiveReader.Open(path, out string error);
            if (reader == null)
            {
                LastError = "Bozuk kayıt: " + error;
                return false;
            }
            var templates = new MeshRenderer[reader.Info.Tracks.Count];
            string incompatible = ResolveTemplates(reader.Info, templates, out Material layoutMaterial);
            if (incompatible == null && reader.Info.Chunks.Count == 0) incompatible = "Kayıtta kare yok.";
            if (incompatible == null && !reader.VerifyAllChunks(out error)) incompatible = "Bozuk kayıt: " + error;
            if (incompatible != null)
            {
                reader.Dispose();
                LastError = incompatible;
                return false;
            }

            _reader = reader;
            _templates = templates;
            ReplayArchiveInfo info = reader.Info;
            BuildProxies(info, layoutMaterial);
            _poses = new MatchReplayClip.Pose[info.DynamicTrackCount];
            _wear = new byte[info.WearTrackCount];
            _appliedWear = new byte[info.WearTrackCount];
            for (int i = 0; i < _appliedWear.Length; i++) _appliedWear[i] = 255;
            _marks = new Vector4[info.WearTrackCount * MatchReplayArchive.MarksPerWearTrack];
            _appliedMarks = new Vector4[_marks.Length];
            _impacts.Clear();
            foreach (ReplayEvent item in info.Events)
                if (item.Type == ReplayEventType.Impact) _impacts.Add(item);
            CreateMarker();

            if (_camera != null)
            {
                _savedMode = _camera.Mode;
                _savedYaw = _camera.Yaw;
                _savedPitch = _camera.Pitch;
                _savedDistance = _camera.Distance;
                _hasSavedCamera = true;
            }
            CollectLiveLayout();
            _scope.Enter(_match, _camera, _playerRoot, _opponentRoot, _liveLayout);
            IsActive = true;
            IsPaused = false;
            IsFinished = false;
            InspectedRobot = -1;
            Speed = 1f;
            LastError = null;
            SetView(View.Duel);
            PlaybackTime = 0f;
            ApplyFrame();
            return IsActive;
        }

        public void SetPaused(bool paused)
        {
            if (!IsActive) return;
            IsPaused = paused;
            if (!paused)
            {
                InspectedRobot = -1;
                if (IsFinished) Seek(0f);
            }
        }

        public void SetSpeed(float speed)
        {
            if (speed > 0f && speed <= 1f && MatchReplayArchive.IsFinite(speed)) Speed = speed;
        }

        /// <summary>Jumps to a time; only the chunk around it is read from disk.</summary>
        public void Seek(float seconds)
        {
            if (!IsActive) return;
            PlaybackTime = Mathf.Clamp(MatchReplayArchive.IsFinite(seconds) ? seconds : 0f, 0f, Duration);
            IsFinished = PlaybackTime >= Duration;
            ApplyFrame();
        }

        /// <summary>Seeks to the next (<paramref name="direction"/> &gt; 0) or previous event. False when there is none.</summary>
        public bool StepEvent(int direction)
        {
            if (!IsActive) return false;
            float now = _reader.StartTime + PlaybackTime;
            List<ReplayEvent> events = _reader.Info.Events;
            if (direction > 0)
            {
                foreach (ReplayEvent item in events)
                    if (item.Time > now + 0.01f)
                    {
                        Seek(item.Time - _reader.StartTime);
                        return true;
                    }
                return false;
            }
            for (int i = events.Count - 1; i >= 0; i--)
                if (events[i].Time < now - 0.01f)
                {
                    Seek(events[i].Time - _reader.StartTime);
                    return true;
                }
            return false;
        }

        public void SetView(View view)
        {
            if (!IsActive) return;
            CurrentView = view;
            if (_camera == null) return;
            Transform primary = view == View.Opponent ? _opponentRoot : _playerRoot;
            Transform secondary = view == View.Duel ? _opponentRoot : null;
            _camera.SetPresentationTargets(primary, secondary);
            _camera.SetMode(view == View.Duel ? CombatCameraController.CameraMode.Duel : CombatCameraController.CameraMode.Follow);
        }

        /// <summary>Pauses at the current moment and orbits one robot closely; the part list shows its state then.</summary>
        public bool Inspect(int robot)
        {
            if (!IsActive || (robot != MatchReplayRecorder.PlayerRobot && robot != MatchReplayRecorder.OpponentRobot)) return false;
            IsPaused = true;
            InspectedRobot = robot;
            SetView(robot == MatchReplayRecorder.PlayerRobot ? View.Player : View.Opponent);
            if (_camera != null) _camera.SetOrbit(_camera.Yaw, inspectionPitch, inspectionDistance);
            return true;
        }

        /// <summary>
        /// Damage parts of a robot at the current moment: name and whether it is still on the robot. The
        /// part list is the live robot's <see cref="RobotDamageVisuals"/> partition, matched by path.
        /// </summary>
        public void GetPartStates(int robot, List<KeyValuePair<string, bool>> output)
        {
            output.Clear();
            if (!IsActive) return;
            ReplayArchiveInfo info = _reader.Info;
            string robotName = robot == MatchReplayRecorder.PlayerRobot ? info.PlayerName : info.OpponentName;
            Transform live = FindRobot(robotName);
            var visuals = live != null ? live.GetComponent<RobotDamageVisuals>() : null;
            if (visuals == null) return;
            foreach (RobotDamageVisuals.Part part in visuals.Parts)
            {
                if (part.Transform == null) continue;
                string key = MatchReplayArchive.RobotPrefix + robotName + "/" + PathUnder(live, part.Transform);
                ReplayTrack track = info.Tracks.Find(t => t.Key == key);
                if (track == null || track.Column < 0) continue;
                output.Add(new KeyValuePair<string, bool>(part.Name, _poses[track.Column].Visible));
            }
        }

        private static string PathUnder(Transform root, Transform node)
        {
            var names = new List<string>();
            for (Transform current = node; current != null && current != root; current = current.parent) names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }

        public void Stop()
        {
            if (!IsActive && _reader == null) return;
            IsActive = false;
            _scope.Exit();
            if (_camera != null && _hasSavedCamera)
            {
                _camera.SetMode(_savedMode);
                _camera.SetOrbit(_savedYaw, _savedPitch, _savedDistance);
            }
            _hasSavedCamera = false;
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
                Destroy(_root.gameObject);
            }
            if (_markerMaterial != null) Destroy(_markerMaterial);
            _root = _playerRoot = _opponentRoot = _debrisRoot = _arenaRoot = null;
            _proxies = null;
            _templates = null;
            _marker = null;
            _markerMaterial = null;
            _impacts.Clear();
            _liveLayout.Clear();
            _reader?.Dispose();
            _reader = null;
            ProxyCount = 0;
            InspectedRobot = -1;
            IsPaused = false;
        }

        private void Update()
        {
            if (!IsArchiveModeOpen) return;
            if (_match == null || !_match.isActiveAndEnabled || _match.State != MatchController.MatchState.Ready)
            {
                LastError = "Maç hazır ekranından çıktı; tekrar arşivi kapatıldı.";
                CloseArchiveMode();
                return;
            }
            if (!IsActive) return;
            if (!IsSettingsOpen && !IsPaused && !IsFinished)
            {
                PlaybackTime += Time.unscaledDeltaTime * Speed;
                if (PlaybackTime >= Duration)
                {
                    PlaybackTime = Duration;
                    IsFinished = true;
                }
            }
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (!IsActive || _reader == null) return;
            float time = _reader.StartTime + PlaybackTime;
            if (!_reader.Sample(time, _poses, _wear, _marks, out string error))
            {
                Stop();
                LastError = "Bozuk kayıt: " + (error ?? "kare okunamadı.");
                return;
            }
            ReplayArchiveInfo info = _reader.Info;
            for (int t = 0; t < info.Tracks.Count; t++)
            {
                ReplayTrack track = info.Tracks[t];
                MeshRenderer proxy = _proxies[t];
                if (proxy == null || track.Column < 0) continue;
                MatchReplayClip.Pose pose = _poses[track.Column];
                proxy.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                proxy.enabled = pose.Visible;
                if (track.WearColumn >= 0) ApplyWear(proxy, track.WearColumn);
            }
            UpdateMarker(time);
        }

        private void ApplyWear(MeshRenderer proxy, int column)
        {
            int at = column * MatchReplayArchive.MarksPerWearTrack;
            bool changed = _appliedWear[column] != _wear[column];
            for (int k = 0; k < MatchReplayArchive.MarksPerWearTrack && !changed; k++) changed = _appliedMarks[at + k] != _marks[at + k];
            if (!changed) return;
            _appliedWear[column] = _wear[column];
            _block.Clear();
            _block.SetFloat(WearId, _wear[column] / 255f);
            for (int k = 0; k < MatchReplayArchive.MarksPerWearTrack; k++)
            {
                _appliedMarks[at + k] = _marks[at + k];
                _block.SetVector(HitIds[k], _marks[at + k]);
            }
            proxy.SetPropertyBlock(_block);
        }

        private void BuildProxies(ReplayArchiveInfo info, Material layoutMaterial)
        {
            _root = RobotVisualProxyFactory.CreateRoot("Match Replay Archive");
            _playerRoot = RobotVisualProxyFactory.CreateRoot("Archive Player", _root);
            _opponentRoot = RobotVisualProxyFactory.CreateRoot("Archive Opponent", _root);
            _debrisRoot = RobotVisualProxyFactory.CreateRoot("Archive Debris", _root);
            _arenaRoot = RobotVisualProxyFactory.CreateRoot("Archive Arena", _root);
            _proxies = new MeshRenderer[info.Tracks.Count];
            ProxyCount = 0;
            for (int t = 0; t < info.Tracks.Count; t++)
            {
                ReplayTrack track = info.Tracks[t];
                MeshRenderer proxy;
                if (track.Kind == ReplayTrackKind.LayoutStatic || track.Kind == ReplayTrackKind.LayoutDynamic)
                    proxy = CreateLayoutProxy(track, layoutMaterial);
                else
                {
                    Transform parent = track.Kind == ReplayTrackKind.RobotDebris ? _debrisRoot
                        : track.Robot == MatchReplayRecorder.PlayerRobot ? _playerRoot : _opponentRoot;
                    proxy = RobotVisualProxyFactory.CreateProxy(_templates[t], parent);
                    if (proxy != null)
                    {
                        proxy.name = MatchReplayArchive.LastSegment(track.Key);
                        proxy.transform.localScale = track.Scale;
                        // The live robot's current wear is not the recorded one: start clean.
                        proxy.SetPropertyBlock(null);
                        proxy.enabled = false;
                    }
                }
                _proxies[t] = proxy;
                if (proxy != null) ProxyCount++;
            }
        }

        private MeshRenderer CreateLayoutProxy(ReplayTrack track, Material material)
        {
            var proxy = new GameObject(MatchReplayArchive.LastSegment(track.Key));
            proxy.transform.SetParent(_arenaRoot, false);
            proxy.transform.SetPositionAndRotation(track.StaticPosition, track.StaticRotation);
            proxy.transform.localScale = track.Scale;
            proxy.AddComponent<MeshFilter>().sharedMesh = CubeMesh;
            var renderer = proxy.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            if (track.HasColor)
            {
                _block.Clear();
                _block.SetColor(BaseColorId, track.Color);
                renderer.SetPropertyBlock(_block);
            }
            renderer.enabled = track.Kind == ReplayTrackKind.LayoutDynamic || track.StaticVisible;
            return renderer;
        }

        private void UpdateMarker(float time)
        {
            if (_marker == null) return;
            int shown = -1;
            for (int i = _impacts.Count - 1; i >= 0; i--)
            {
                if (_impacts[i].Time > time) continue;
                if (time - _impacts[i].Time <= impactFlashSeconds) shown = i;
                break;
            }
            _marker.enabled = shown >= 0;
            if (shown < 0) return;
            ReplayEvent impact = _impacts[shown];
            Vector3 normal = impact.Normal.sqrMagnitude > 0.0001f ? impact.Normal.normalized : Vector3.up;
            _marker.transform.SetPositionAndRotation(impact.Point + normal * 0.02f, Quaternion.FromToRotation(Vector3.forward, normal));
        }

        private void CreateMarker()
        {
            Shader shader = Resources.Load<Shader>("SelectionRing");
            if (shader == null) return;
            var markerObject = new GameObject("Archive Impact Marker");
            markerObject.transform.SetParent(_root, false);
            _markerMaterial = new Material(shader) { name = "Runtime Archive Impact Marker", color = impactColor };
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
