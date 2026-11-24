using System;
using System.Collections.Generic;
using System.Diagnostics;
using RobotSavaslari.Arena;
using RobotSavaslari.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Records whole encounters for the replay archive: a single match from start to result, or every
    /// round of a first-to-two series until it is decided, with round boundaries, hits, detached parts
    /// and results as events. Frames are streamed to disk at <see cref="MatchReplayArchive.SampleRate"/>
    /// Hz through a fixed buffer. Like <see cref="MatchReplayRecorder"/> it only reads transforms and
    /// renderer state and listens to events; it never calls health, match or physics APIs, and a full
    /// disk or a limit only marks the archive incomplete while the match carries on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchReplayArchiveRecorder : MonoBehaviour
    {
        private const string LayoutContentsName = "Layout Contents";
        private const int TimingSamples = 512;
        private static readonly int WearId = Shader.PropertyToID("_Wear");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int[] HitIds =
        {
            Shader.PropertyToID("_Hit0"), Shader.PropertyToID("_Hit1"), Shader.PropertyToID("_Hit2"), Shader.PropertyToID("_Hit3"),
        };

        private sealed class DebrisSlot
        {
            public RobotDamageVisuals Visuals;
            public int PartIndex;
            public string ObjectName;
            public int BodyTrack = -1, WearTrack = -1;
            public Transform Debris;
            public MeshRenderer BodyRenderer, WearRenderer;
        }

        private readonly List<MeshRenderer> _scratchRenderers = new List<MeshRenderer>();
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<DebrisSlot> _debris = new List<DebrisSlot>();
        private readonly float[] _timings = new float[TimingSamples];
        private MatchController _match;
        private RobotHealth _playerHealth, _opponentHealth;
        private Action<RobotHit> _onPlayerDamaged, _onOpponentDamaged;
        private MaterialPropertyBlock _block;
        private MatchReplayArchiveWriter _writer;
        private ArenaHazard[] _hazards = Array.Empty<ArenaHazard>();
        private ReplayHazardSample[] _hazardSamples = Array.Empty<ReplayHazardSample>();
        private HazardPhase[] _lastPhases = Array.Empty<HazardPhase>();
        private int[] _hazardTracks = Array.Empty<int>();
        private Renderer[] _columnRenderers = Array.Empty<Renderer>();
        private DebrisSlot[] _columnDebris = Array.Empty<DebrisSlot>();
        private bool[] _columnDebrisWear = Array.Empty<bool>();
        private int[] _columnTrack = Array.Empty<int>();
        private MatchReplayClip.Pose[] _poses = Array.Empty<MatchReplayClip.Pose>();
        private byte[] _wear = Array.Empty<byte>();
        private Renderer[] _wearRenderers = Array.Empty<Renderer>();
        private RobotDamageVisuals[] _visuals = new RobotDamageVisuals[2];
        private bool[][] _detached = new bool[2][];
        private int[][] _partTracks = new int[2][];
        private int _roundMatchCount = -1;
        private int _roundIndex = -1;
        private int _roundsCompleted;
        private bool _roundActive, _breakChunk, _scanDebris;
        private float _roundOffset, _timelineEnd;
        // Clocks in double: a float schedule drifts after many minutes of play (or a long Editor session).
        private double _roundStartClock, _nextSampleClock;
        private int _timingCount, _timingHead;

        public MatchReplayArchiveStore Store { get; set; }
        /// <summary>An encounter is being written to a temporary file.</summary>
        public bool IsRecording => _writer != null && _writer.IsOpen;
        /// <summary>The archive being written, or null.</summary>
        public ReplayArchiveInfo Current => _writer?.Info;
        /// <summary>What happened to the last encounter's recording, for the result screen.</summary>
        public string LastStatus { get; private set; }
        /// <summary>The encounter whose recording <see cref="LastStatus"/> describes (its last MatchCount).</summary>
        public int LastStatusMatch { get; private set; } = -1;
        public MatchReplayArchiveStore.CommitResult? LastCommit { get; private set; }
        public string LastArchiveId { get; private set; }
        public int TrackCount => _writer != null ? _writer.Info.Tracks.Count : 0;
        public long BytesWritten => _writer != null ? _writer.BytesWritten : 0;
        public int FramesWritten => _writer != null ? _writer.FramesWritten : 0;
        /// <summary>Managed memory held for the recording in progress (writer buffers and lists plus sampling arrays).</summary>
        public long HeldBytes => _writer == null ? 0 : _writer.HeldBytes + _poses.Length * 32L + _columnRenderers.Length * 24L + _wear.Length + _hazards.Length * 32L;

        public void Bind(MatchController match, MatchReplayArchiveStore store)
        {
            Store = store;
            if (_match == match) return;
            if (IsRecording) FinishSession(ReplayCompleteness.Interrupted, "Kayıt yarıda kesildi.");
            Unsubscribe();
            _roundMatchCount = -1;
            _match = match;
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake() => _block = new MaterialPropertyBlock();

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (IsRecording) FinishSession(ReplayCompleteness.Interrupted, "Kayıt yarıda kesildi (oyun kapandı veya kayıt durdu).");
            // Remember the interrupted match so re-enabling cannot produce a second archive for it.
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_match == null || _playerHealth != null || _opponentHealth != null) return;
            _match.ResultDetermined += OnResultDetermined;
            _playerHealth = _match.Player != null ? _match.Player.GetComponent<RobotHealth>() : null;
            _opponentHealth = _match.Opponent != null ? _match.Opponent.GetComponent<RobotHealth>() : null;
            _onPlayerDamaged = hit => OnDamaged(MatchReplayRecorder.PlayerRobot, hit);
            _onOpponentDamaged = hit => OnDamaged(MatchReplayRecorder.OpponentRobot, hit);
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
            MatchController.MatchState state = _match.State;
            if (state == MatchController.MatchState.Playing)
            {
                if (_roundMatchCount != _match.MatchCount) BeginRound();
                // A fixed schedule keeps the rate at SampleRate whatever the frame rate; after a hitch it
                // catches up one sample per frame instead of drifting. The tolerance absorbs float rounding.
                if (_roundActive && Time.timeAsDouble + 1e-6 >= _nextSampleClock && Capture(TimelineNow()) && !double.IsPositiveInfinity(_nextSampleClock))
                    _nextSampleClock = Math.Max(_nextSampleClock + SampleInterval, Time.timeAsDouble);
            }
            else if (state == MatchController.MatchState.Ready && IsRecording)
            {
                // Back at the menu before the series was decided.
                FinishSession(ReplayCompleteness.Abandoned, "Seri tamamlanmadan ana menüye dönüldü.");
            }
        }

        /// <summary>
        /// Takes one sample now (used by measurements). Returns false when no round is being recorded.
        /// Steady-state sampling does not allocate managed memory.
        /// </summary>
        public bool CaptureNow()
        {
            if (!_roundActive || _writer == null) return false;
            float now = TimelineNow();
            if (now <= _writer.LastTime) now = _writer.LastTime + 1e-4f;
            return Capture(now);
        }

        /// <summary>Duration statistics of recent samples in milliseconds (p50, p95, max) and how many were measured.</summary>
        public int GetSampleTimings(out float p50, out float p95, out float max)
        {
            int count = _timingCount;
            p50 = p95 = max = 0f;
            if (count == 0) return 0;
            var sorted = new float[count];
            Array.Copy(_timings, sorted, count);
            Array.Sort(sorted);
            p50 = sorted[(count - 1) / 2];
            p95 = sorted[Mathf.Min(count - 1, Mathf.CeilToInt(count * 0.95f) - 1)];
            max = sorted[count - 1];
            return count;
        }

        public void ResetSampleTimings() => _timingCount = _timingHead = 0;

        private float TimelineNow() => _roundOffset + (float)Math.Max(0.0, Time.timeAsDouble - _roundStartClock);

        // ---- Rounds and sessions -------------------------------------------------------------------

        private void BeginRound()
        {
            _roundMatchCount = _match.MatchCount;
            bool continuation = IsRecording && _match.Rules.mode == MatchMode.FirstToTwo &&
                                _match.PlayerWins + _match.OpponentWins + _match.DrawnRounds == _roundsCompleted;
            if (IsRecording && !continuation)
                FinishSession(ReplayCompleteness.Abandoned, "Seri tamamlanmadan yeni karşılaşma başladı.");
            if (!IsRecording)
            {
                // A recording only starts with an encounter's first round: a series whose recording failed
                // earlier is not saved from the middle as if it were whole.
                if (_match.PlayerWins + _match.OpponentWins + _match.DrawnRounds > 0)
                {
                    if (LastStatus == null) SetStatus("Tam kayıt yok: bu serinin kaydı önceki raundda kesildi.");
                    return;
                }
                if (!StartSession()) return;
                if (_match.ElapsedSeconds > 0.25f)
                    _writer.MarkIncomplete(ReplayCompleteness.Interrupted, "Kayıt raundun ortasında başladı; başı eksik.");
            }
            if (_writer.Info.Rounds.Count >= MatchReplayArchive.MaxRounds)
            {
                _writer.MarkIncomplete(ReplayCompleteness.FileLimit, "Raund sınırına ulaşıldı; sonraki raundlar kaydedilmedi.");
                _roundActive = false;
                return;
            }

            _roundOffset = _writer.Info.Rounds.Count == 0 ? 0f : _timelineEnd + 1f / MatchReplayArchive.SampleRate;
            _roundStartClock = Time.timeAsDouble;
            _roundIndex = _writer.Info.Rounds.Count;
            _writer.Info.Rounds.Add(new ReplayRound
            {
                MatchId = _match.MatchCount, RoundNumber = _match.RoundNumber, StartTime = _roundOffset, EndTime = _roundOffset,
            });
            _writer.AddEvent(new ReplayEvent { Time = _roundOffset, Type = ReplayEventType.RoundStart, Robot = -1, Round = _roundIndex, Track = -1 });
            foreach (DebrisSlot slot in _debris)
            {
                slot.Debris = null;
                slot.BodyRenderer = slot.WearRenderer = null;
            }
            for (int robot = 0; robot < 2; robot++)
                if (_detached[robot] != null) Array.Clear(_detached[robot], 0, _detached[robot].Length);
            Array.Fill(_lastPhases, (HazardPhase)(-1));
            _roundActive = true;
            _breakChunk = true;
            Capture(_roundOffset);
            _nextSampleClock = Time.timeAsDouble + SampleInterval;
        }

        private static double SampleInterval => 1.0 / MatchReplayArchive.SampleRate;

        private bool StartSession()
        {
            _roundsCompleted = 0;
            _timelineEnd = 0f;
            if (Store == null)
            {
                SetStatus("Tam kayıt yok: tekrar arşivi bağlı değil.");
                return false;
            }
            var info = new ReplayArchiveInfo
            {
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                PlayerName = _match.Player != null ? _match.Player.name : null,
                OpponentName = _match.Opponent != null ? _match.Opponent.name : null,
                RulesMode = (int)_match.Rules.mode,
                TimeLimit = _match.Rules.timeLimitSeconds,
            };
            if (!CollectTracks(info, out string unsupported))
            {
                SetStatus("Tam kayıt yok: " + unsupported);
                return false;
            }
            Store.CleanupTemporaryFiles();
            if (!Store.HasRoomFor(0) && Store.Pending() != null)
            {
                SetStatus("Tam kayıt yok: tekrar arşivi dolu ve bekleyen bir kayıt var. Tekrar arşivinden kayıt silin.");
                return false;
            }
            try
            {
                _writer = Store.CreateWriter(info);
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                _writer = null;
                SetStatus("Tam kayıt yok: kayıt dosyası açılamadı (" + exception.Message + ").");
                return false;
            }
            PrepareColumns(info);
            LastStatus = null;
            return true;
        }

        private void OnResultDetermined(MatchController.MatchOutcome outcome)
        {
            if (_writer == null) return;
            if (!_roundActive)
            {
                // Rounds past the limit were not recorded; the decided encounter is still closed properly.
                if (_match.EncounterComplete) FinishSession(ReplayCompleteness.Complete, null);
                return;
            }
            // The deciding hit has already detached parts; keep that final look as the round's last frame.
            float now = TimelineNow();
            if (now <= _writer.LastTime) now = _writer.LastTime + 1e-4f;
            Capture(now);
            if (_writer == null) return; // A final-frame I/O failure already ended this session.
            _roundActive = false;
            _timelineEnd = Mathf.Max(now, _writer.LastTime);
            _roundsCompleted++;
            RoundResult result = _match.LastRound;
            ReplayRound round = _writer.Info.Rounds[_roundIndex];
            round.EndTime = _timelineEnd;
            round.Outcome = (int)outcome;
            round.Reason = result != null ? (int)result.reason : 0;
            if (result != null) round.RoundNumber = result.roundNumber;
            _writer.Info.Rounds[_roundIndex] = round;
            _writer.AddEvent(new ReplayEvent { Time = _timelineEnd, Type = ReplayEventType.RoundEnd, Robot = -1, Round = _roundIndex, Track = -1, Outcome = (int)outcome });
            if (_match.EncounterComplete)
            {
                _writer.AddEvent(new ReplayEvent { Time = _timelineEnd, Type = ReplayEventType.EncounterEnd, Robot = -1, Round = _roundIndex, Track = -1, Outcome = (int)outcome });
                FinishSession(ReplayCompleteness.Complete, null);
            }
        }

        private void FinishSession(ReplayCompleteness reason, string warning)
        {
            if (_writer == null) return;
            MatchReplayArchiveWriter writer = _writer;
            _writer = null;
            if (_roundActive)
            {
                // Stopped mid-round: close the round at the last recorded frame.
                _timelineEnd = Mathf.Max(_timelineEnd, writer.LastTime);
                ReplayRound round = writer.Info.Rounds[_roundIndex];
                round.EndTime = _timelineEnd;
                writer.Info.Rounds[_roundIndex] = round;
            }
            _roundActive = false;
            if (reason != ReplayCompleteness.Complete) writer.MarkIncomplete(reason, warning);
            ReplayArchiveInfo info = writer.Info;
            info.Duration = Mathf.Max(_timelineEnd, writer.LastTime < 0f ? 0f : writer.LastTime);
            MatchRecord record = _match != null && _match.EncounterComplete ? _match.LastMatch : null;
            info.EncounterId = reason == ReplayCompleteness.Complete && record != null ? record.id : null;
            info.Outcome = record != null && reason == ReplayCompleteness.Complete ? (int)record.outcome : 0;
            info.PlayerWins = _match != null ? _match.PlayerWins : 0;
            info.OpponentWins = _match != null ? _match.OpponentWins : 0;
            info.DrawnRounds = _match != null ? _match.DrawnRounds : 0;
            info.Title = BuildTitle(info, reason == ReplayCompleteness.Complete);

            if (writer.FramesWritten == 0)
            {
                writer.Abort();
                SetStatus("Tam kayıt yok: kare kaydedilmedi.");
                return;
            }
            string message = null;
            LastCommit = Store != null ? Store.Commit(writer, out message) : MatchReplayArchiveStore.CommitResult.Failed;
            if (Store == null) writer.Abort();
            LastArchiveId = LastCommit == MatchReplayArchiveStore.CommitResult.Failed ? null : info.Id;
            SetStatus(message ?? "Tam kayıt yok.");
        }

        private void SetStatus(string message)
        {
            LastStatus = message;
            LastStatusMatch = _match != null ? _match.MatchCount : -1;
        }

        private static string BuildTitle(ReplayArchiveInfo info, bool finished)
        {
            bool series = info.RulesMode == (int)MatchMode.FirstToTwo;
            string outcome = info.Outcome == 1 ? "Galibiyet" : info.Outcome == 2 ? "Mağlubiyet" : info.Outcome == 3 ? "Berabere" : "Sonuçsuz";
            if (!finished) return series ? $"Yarım kalan seri {info.PlayerWins}-{info.OpponentWins}" : "Yarım kalan maç";
            return series ? $"Seri {info.PlayerWins}-{info.OpponentWins} · {outcome}" : $"Tek maç · {outcome}";
        }

        // ---- Tracks ----------------------------------------------------------------------------------

        private bool CollectTracks(ReplayArchiveInfo info, out string unsupported)
        {
            unsupported = null;
            var keys = new HashSet<string>();
            var robots = new List<ReplayTrack>();
            var sources = new List<Renderer>();
            var debrisTracks = new List<ReplayTrack>();
            _debris.Clear();
            var robotTransforms = new[] { _match.Player != null ? _match.Player.transform : null, _match.Opponent != null ? _match.Opponent.transform : null };
            for (int robot = 0; robot < 2; robot++)
            {
                Transform root = robotTransforms[robot];
                _visuals[robot] = null;
                _detached[robot] = null;
                _partTracks[robot] = null;
                if (root == null) continue;
                RobotVisualProxyFactory.CollectMeshRenderers(root, _scratchRenderers);
                foreach (MeshRenderer renderer in _scratchRenderers)
                {
                    string key = UniqueKey(MatchReplayArchive.RobotPrefix + root.name + "/" + PathUnder(root, renderer.transform), keys);
                    if (key == null) continue;
                    robots.Add(new ReplayTrack
                    {
                        Key = key, Kind = ReplayTrackKind.RobotPart, Robot = robot, Wear = renderer.name == "Damage Wear",
                        MeshSignature = renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount, Scale = renderer.transform.lossyScale,
                    });
                    sources.Add(renderer);
                }

                var visuals = root.GetComponent<RobotDamageVisuals>();
                if (visuals == null || visuals.Parts.Count == 0) continue;
                _visuals[robot] = visuals;
                _detached[robot] = new bool[visuals.Parts.Count];
                _partTracks[robot] = new int[visuals.Parts.Count];
                for (int p = 0; p < visuals.Parts.Count; p++)
                {
                    RobotDamageVisuals.Part part = visuals.Parts[p];
                    _partTracks[robot][p] = -1;
                    if (part.Transform == null || part.Mesh == null) continue;
                    string partPath = PathUnder(root, part.Transform);
                    _partTracks[robot][p] = robots.FindIndex(t => t.Key == MatchReplayArchive.RobotPrefix + root.name + "/" + partPath);
                    var slot = new DebrisSlot { Visuals = visuals, PartIndex = p, ObjectName = root.name + " Debris " + part.Name };
                    string bodyKey = UniqueKey(MatchReplayArchive.DebrisPrefix + root.name + "/" + partPath, keys);
                    string wearKey = part.Wear != null ? UniqueKey(MatchReplayArchive.DebrisPrefix + root.name + "/" + partPath + "/Damage Wear", keys) : null;
                    if (bodyKey == null) continue;
                    slot.BodyTrack = debrisTracks.Count;
                    debrisTracks.Add(new ReplayTrack
                    {
                        Key = bodyKey, Kind = ReplayTrackKind.RobotDebris, Robot = robot, MeshSignature = part.Mesh.vertexCount, Scale = part.Transform.lossyScale,
                    });
                    if (wearKey != null)
                    {
                        slot.WearTrack = debrisTracks.Count;
                        debrisTracks.Add(new ReplayTrack
                        {
                            Key = wearKey, Kind = ReplayTrackKind.RobotDebris, Robot = robot, Wear = true, MeshSignature = part.Mesh.vertexCount, Scale = part.Transform.lossyScale,
                        });
                    }
                    _debris.Add(slot);
                }
            }

            var layoutTracks = new List<ReplayTrack>();
            var layoutSources = new List<Renderer>();
            if (!CollectLayout(info, keys, layoutTracks, layoutSources, out unsupported)) return false;

            // Robots first, then the arena, then debris: when a limit cuts tracks, the robots stay whole.
            var ordered = new List<ReplayTrack>();
            var orderedSources = new List<Renderer>();
            ordered.AddRange(robots); orderedSources.AddRange(sources);
            ordered.AddRange(layoutTracks); orderedSources.AddRange(layoutSources);
            int debrisStart = ordered.Count;
            ordered.AddRange(debrisTracks);
            for (int i = 0; i < debrisTracks.Count; i++) orderedSources.Add(null);
            int dropped = Mathf.Max(0, ordered.Count - MatchReplayArchive.MaxTracks);
            if (dropped > 0)
            {
                ordered.RemoveRange(MatchReplayArchive.MaxTracks, dropped);
                orderedSources.RemoveRange(MatchReplayArchive.MaxTracks, dropped);
                info.Completeness |= ReplayCompleteness.TrackLimit;
                info.Warning = $"{dropped} görsel iz {MatchReplayArchive.MaxTracks} iz sınırını aştığı için kaydedilmedi.";
            }
            foreach (DebrisSlot slot in _debris)
            {
                slot.BodyTrack = slot.BodyTrack >= 0 && debrisStart + slot.BodyTrack < ordered.Count ? debrisStart + slot.BodyTrack : -1;
                slot.WearTrack = slot.WearTrack >= 0 && debrisStart + slot.WearTrack < ordered.Count ? debrisStart + slot.WearTrack : -1;
            }
            info.Tracks.AddRange(ordered);
            _trackSources = orderedSources.ToArray();
            return true;
        }

        private Renderer[] _trackSources = Array.Empty<Renderer>();

        private bool CollectLayout(ReplayArchiveInfo info, HashSet<string> keys, List<ReplayTrack> tracks, List<Renderer> sources, out string unsupported)
        {
            unsupported = null;
            ArenaEnvironment environment = FindEnvironment();
            if (environment == null) return true;
            ArenaLayout layout = environment.Layout;
            info.ArenaName = layout != null ? layout.displayName : null;
            var unknown = new List<string>();
            if (layout != null && layout.cells != null)
                foreach (ArenaCellData cell in layout.cells)
                {
                    if (cell == null || info.Cells.Count >= MatchReplayArchive.MaxCells) continue;
                    string content = cell.content.ToString();
                    if (!MatchReplayArchive.IsSupportedCellContent(content) && !unknown.Contains(content)) unknown.Add(content);
                    info.Cells.Add(new ReplayCell
                    {
                        X = cell.coordinate.x, Y = cell.coordinate.y, Content = content,
                        Spawn = (int)cell.spawn, Wall = (int)cell.wall, Direction = cell.direction,
                    });
                }
            if (unknown.Count > 0)
            {
                unsupported = "arena içeriği desteklenmiyor (" + string.Join(", ", unknown) + ").";
                return false;
            }

            Transform contents = environment.Contents;
            if (contents == null) return true;
            int index = 0;
            foreach (Renderer renderer in contents.GetComponentsInChildren<Renderer>(true))
            {
                string path = PathUnder(contents, renderer.transform);
                var filter = renderer.GetComponent<MeshFilter>();
                bool known = renderer is MeshRenderer && filter != null && filter.sharedMesh != null;
                foreach (string segment in path.Split('/'))
                    known &= MatchReplayArchive.IsSupportedLayoutName(segment);
                if (!known)
                {
                    unsupported = "arena içeriği desteklenmiyor (" + renderer.name + ").";
                    return false;
                }
                bool dynamic = false;
                foreach (string segment in path.Split('/'))
                    dynamic |= MatchReplayArchive.IsDynamicLayoutName(segment);
                string key = UniqueKey(MatchReplayArchive.LayoutPrefix + index++ + "/" + path, keys);
                if (key == null)
                {
                    unsupported = "arena nesnesi adı desteklenmiyor (" + renderer.name + ").";
                    return false;
                }
                bool marker = MatchReplayArchive.IsHazardKey(key);
                if (marker && renderer.GetComponentInParent<ArenaHazard>() == null)
                {
                    unsupported = "arena tehlikesi bulunamadı (" + path + ").";
                    return false;
                }
                var track = new ReplayTrack
                {
                    Key = key, Kind = marker ? ReplayTrackKind.HazardMarker : dynamic ? ReplayTrackKind.LayoutDynamic : ReplayTrackKind.LayoutStatic, Robot = -1,
                    MeshSignature = filter.sharedMesh.vertexCount, Scale = renderer.transform.lossyScale,
                    StaticPosition = renderer.transform.position, StaticRotation = renderer.transform.rotation,
                    StaticVisible = RobotVisualProxyFactory.IsVisible(renderer),
                };
                if (info.LayoutMaterial == null && renderer.sharedMaterial != null) info.LayoutMaterial = renderer.sharedMaterial.name;
                if (renderer.HasPropertyBlock())
                {
                    renderer.GetPropertyBlock(_block);
                    Color color = _block.GetColor(BaseColorId);
                    if (color != default)
                    {
                        track.HasColor = true;
                        track.Color = color;
                    }
                }
                tracks.Add(track);
                sources.Add(renderer);
            }
            return true;
        }

        private ArenaEnvironment FindEnvironment()
        {
            Scene scene = _match.gameObject.scene;
            foreach (ArenaEnvironment environment in FindObjectsByType<ArenaEnvironment>())
                if (environment.gameObject.scene == scene) return environment;
            return null;
        }

        private void PrepareColumns(ReplayArchiveInfo info)
        {
            int columns = info.DynamicTrackCount;
            _hazards = new ArenaHazard[info.HazardTrackCount];
            _hazardSamples = new ReplayHazardSample[info.HazardTrackCount];
            _lastPhases = new HazardPhase[info.HazardTrackCount];
            _hazardTracks = new int[info.HazardTrackCount];
            _columnRenderers = new Renderer[columns];
            _columnDebris = new DebrisSlot[columns];
            _columnDebrisWear = new bool[columns];
            _columnTrack = new int[columns];
            _poses = new MatchReplayClip.Pose[columns];
            _wear = new byte[info.WearTrackCount];
            _wearRenderers = new Renderer[info.WearTrackCount];
            for (int t = 0; t < info.Tracks.Count; t++)
            {
                ReplayTrack track = info.Tracks[t];
                if (track.Column < 0) continue;
                if (track.HazardColumn >= 0)
                {
                    _hazards[track.HazardColumn] = _trackSources[t].GetComponentInParent<ArenaHazard>();
                    _hazardTracks[track.HazardColumn] = t;
                }
                _columnTrack[track.Column] = t;
                _columnRenderers[track.Column] = t < _trackSources.Length ? _trackSources[t] : null;
            }
            foreach (DebrisSlot slot in _debris)
            {
                if (slot.BodyTrack >= 0) _columnDebris[info.Tracks[slot.BodyTrack].Column] = slot;
                if (slot.WearTrack >= 0)
                {
                    int column = info.Tracks[slot.WearTrack].Column;
                    _columnDebris[column] = slot;
                    _columnDebrisWear[column] = true;
                }
            }
            info.Chunks.Capacity = 2048;
            info.Events.Capacity = 256;
        }

        private static string UniqueKey(string key, HashSet<string> keys)
        {
            string candidate = key;
            for (int n = 2; !keys.Add(candidate); n++) candidate = key + "#" + n;
            return MatchReplayArchive.IsValidKey(candidate) ? candidate : null;
        }

        private static string PathUnder(Transform root, Transform node)
        {
            var names = new List<string>();
            for (Transform current = node; current != null && current != root; current = current.parent) names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }

        // ---- Sampling --------------------------------------------------------------------------------

        private bool Capture(float time)
        {
            if (_writer == null) return false;
            long start = Stopwatch.GetTimestamp();
            if (_scanDebris) ScanDebris();
            DetectDetachedParts(time);
            if (_breakChunk)
            {
                _writer.BreakChunk();
                _breakChunk = false;
            }
            bool needsMarks = _writer.NeedsMarks;
            for (int column = 0; column < _poses.Length; column++)
            {
                ReplayTrack track = _writer.Info.Tracks[_columnTrack[column]];
                Renderer renderer = _columnRenderers[column];
                DebrisSlot slot = _columnDebris[column];
                if (slot != null) renderer = _columnDebrisWear[column] ? slot.WearRenderer : slot.BodyRenderer;
                if (renderer != null)
                {
                    Transform node = renderer.transform;
                    if (track.HazardColumn >= 0)
                        _hazardSamples[track.HazardColumn] = new ReplayHazardSample { Scale = node.lossyScale, Phase = _hazards[track.HazardColumn].Phase };
                    _poses[column] = new MatchReplayClip.Pose(node.position, node.rotation, RobotVisualProxyFactory.IsVisible(renderer));
                }
                else
                {
                    _poses[column] = new MatchReplayClip.Pose(Vector3.zero, Quaternion.identity, false);
                }
                if (track.WearColumn >= 0)
                {
                    _wearRenderers[track.WearColumn] = renderer;
                    _wear[track.WearColumn] = ReadWear(renderer, track.WearColumn, needsMarks);
                }
            }
            bool written = _writer.WriteFrame(time, _roundIndex, _poses, _wear, _hazardSamples);
            if (written)
                for (int h = 0; h < _hazardSamples.Length; h++)
                {
                    HazardPhase phase = _hazardSamples[h].Phase;
                    if (phase == _lastPhases[h]) continue;
                    _lastPhases[h] = phase;
                    _writer.AddEvent(new ReplayEvent { Type = ReplayEventType.HazardPhaseChanged, Time = time, Round = _roundIndex,
                        Robot = -1, Track = _hazardTracks[h], Phase = phase });
                }
            if (!written && _writer.Error != null)
            {
                string error = _writer.Error;
                _writer = null;
                _roundActive = false;
                SetStatus("Tam kayıt yok: " + error);
            }
            else if (!written && _writer.IsFull) _nextSampleClock = double.PositiveInfinity;
            RecordTiming(start);
            return written;
        }

        private byte ReadWear(Renderer renderer, int wearColumn, bool marks)
        {
            int at = wearColumn * MatchReplayArchive.MarksPerWearTrack;
            if (renderer == null || !renderer.HasPropertyBlock())
            {
                if (marks)
                    for (int k = 0; k < MatchReplayArchive.MarksPerWearTrack; k++) _writer.Marks[at + k] = Vector4.zero;
                return 0;
            }
            renderer.GetPropertyBlock(_block);
            if (marks)
                for (int k = 0; k < MatchReplayArchive.MarksPerWearTrack; k++) _writer.Marks[at + k] = _block.GetVector(HitIds[k]);
            return MatchReplayArchive.QuantizeWear(_block.GetFloat(WearId));
        }

        private void DetectDetachedParts(float time)
        {
            for (int robot = 0; robot < 2; robot++)
            {
                RobotDamageVisuals visuals = _visuals[robot];
                bool[] detached = _detached[robot];
                if (visuals == null || detached == null) continue;
                IReadOnlyList<RobotDamageVisuals.Part> parts = visuals.Parts;
                for (int p = 0; p < detached.Length && p < parts.Count; p++)
                {
                    bool now = parts[p].Detached;
                    if (now && !detached[p])
                    {
                        _writer.AddEvent(new ReplayEvent
                        {
                            Time = time, Type = ReplayEventType.PartDetached, Robot = robot, Round = Mathf.Max(0, _roundIndex),
                            Track = _partTracks[robot][p], Point = parts[p].Transform != null ? parts[p].Transform.position : Vector3.zero,
                        });
                        _scanDebris = true;
                    }
                    detached[p] = now;
                }
            }
            if (_scanDebris) ScanDebris();
        }

        private void ScanDebris()
        {
            _scanDebris = false;
            if (_debris.Count == 0 || _match == null) return;
            _match.gameObject.scene.GetRootGameObjects(_roots);
            foreach (DebrisSlot slot in _debris)
            {
                if (slot.Debris != null || slot.Visuals == null || !slot.Visuals.Parts[slot.PartIndex].Detached) continue;
                foreach (GameObject root in _roots)
                {
                    if (root == null || root.name != slot.ObjectName || root.GetComponent<Rigidbody>() == null) continue;
                    slot.Debris = root.transform;
                    slot.BodyRenderer = root.GetComponent<MeshRenderer>();
                    Transform wear = root.transform.Find("Damage Wear");
                    slot.WearRenderer = wear != null ? wear.GetComponent<MeshRenderer>() : null;
                    break;
                }
            }
            _roots.Clear();
        }

        private void OnDamaged(int robot, RobotHit hit)
        {
            if (!_roundActive || _writer == null) return;
            _writer.AddEvent(new ReplayEvent
            {
                Time = Mathf.Max(TimelineNow(), Mathf.Max(0f, _writer.LastTime)), Type = ReplayEventType.Impact, Robot = robot,
                Source = hit.Weapon ? ReplayImpactSource.Weapon : hit.Source != null ? ReplayImpactSource.RobotContact : ReplayImpactSource.Unknown,
                Round = Mathf.Max(0, _roundIndex), Track = -1, Point = hit.Point, Normal = hit.Normal, Amount = hit.Amount,
            });
            // The damage system updates wear and marks in this same event; store fresh marks with the next frame.
            _breakChunk = true;
            _scanDebris = true;
        }

        private void RecordTiming(long start)
        {
            float milliseconds = (float)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            _timings[_timingHead] = milliseconds;
            _timingHead = (_timingHead + 1) % _timings.Length;
            if (_timingCount < _timings.Length) _timingCount++;
        }
    }
}
