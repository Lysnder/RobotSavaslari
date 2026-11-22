using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Streams one encounter into a temporary file chunk by chunk. Only the chunk being filled is held in
    /// memory (a fixed buffer, allocated once), so recording length does not change memory use. The
    /// header points at the footer only after <see cref="Finish"/> wrote it, so an interrupted file is
    /// never mistaken for a finished one.
    /// </summary>
    public sealed class MatchReplayArchiveWriter : IDisposable
    {
        private static readonly HashSet<string> OpenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly byte[] _chunk;
        private readonly Vector4[] _marks;
        private FileStream _stream;
        private int _chunkLength, _chunkFrames, _chunkRound;
        private float _chunkStart, _chunkEnd, _lastTime = float.NegativeInfinity;
        private int _sequence;
        private int _impactEvents;

        public MatchReplayArchiveWriter(string temporaryPath, ReplayArchiveInfo info)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            if (info.Tracks.Count > MatchReplayArchive.MaxTracks) throw new ArgumentException("Too many replay tracks.");
            info.AssignColumns();
            TemporaryPath = temporaryPath;
            _chunk = new byte[MatchReplayArchive.ChunkBytes(MatchReplayArchive.FramesPerChunk, info.DynamicTrackCount, info.WearTrackCount)];
            _marks = new Vector4[info.WearTrackCount * MatchReplayArchive.MarksPerWearTrack];
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(temporaryPath)));
            _stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024);
            lock (OpenPaths) OpenPaths.Add(Path.GetFullPath(temporaryPath));
            MatchReplayArchive.WriteHeader(_stream, 0, 0, 0);
            BytesWritten = MatchReplayArchive.HeaderBytes;
        }

        public ReplayArchiveInfo Info { get; }
        public string TemporaryPath { get; }
        public bool IsOpen => _stream != null;
        /// <summary>The file budget is used up; further frames are refused and the archive is marked incomplete.</summary>
        public bool IsFull { get; private set; }
        public string Error { get; private set; }
        public long BytesWritten { get; private set; }
        public int FramesWritten { get; private set; }
        public float LastTime => _lastTime;
        /// <summary>
        /// Managed memory this writer holds: the chunk buffer, hit marks and the footer lists that grow with
        /// the recording (chunk index, events, rounds, tracks). Frames themselves are on disk.
        /// </summary>
        public long HeldBytes => _chunk.Length + _marks.Length * 16L + Info.Chunks.Capacity * 32L + Info.Events.Capacity * 64L +
                                 Info.Rounds.Capacity * 24L + Info.Tracks.Count * 256L + 64 * 1024;
        /// <summary>True when the next frame starts a chunk: fill <see cref="Marks"/> before writing it.</summary>
        public bool NeedsMarks => _chunkFrames == 0;
        /// <summary>Hit marks of every wear track (four per track), stored once at the start of each chunk.</summary>
        public Vector4[] Marks => _marks;

        internal static bool IsBeingWritten(string path)
        {
            lock (OpenPaths) return OpenPaths.Contains(Path.GetFullPath(path));
        }

        /// <summary>
        /// Appends a frame. <paramref name="poses"/> has one entry per dynamic track column and
        /// <paramref name="wear"/> one per wear column. Returns false when the frame was not written
        /// (closed, full, not later than the last frame, or an I/O error).
        /// </summary>
        public bool WriteFrame(float time, int round, MatchReplayClip.Pose[] poses, byte[] wear)
        {
            if (_stream == null || IsFull || !MatchReplayArchive.IsFinite(time) || time < 0f || time <= _lastTime) return false;
            if (poses == null || poses.Length < Info.DynamicTrackCount || wear == null || wear.Length < Info.WearTrackCount) return false;
            try
            {
                if (_chunkFrames > 0 && round != _chunkRound) FlushChunk();
                if (_chunkFrames == 0 && !BeginChunk(time, round)) return false;
                int offset = _chunkLength;
                MatchReplayArchive.WriteSingle(_chunk, ref offset, time);
                foreach (ReplayTrack track in Info.Tracks)
                {
                    if (track.Column < 0) continue;
                    byte amount = track.WearColumn >= 0 ? wear[track.WearColumn] : (byte)0;
                    MatchReplayArchive.WritePose(_chunk, ref offset, poses[track.Column], amount);
                }
                _chunkLength = offset;
                _chunkFrames++;
                _chunkEnd = _lastTime = time;
                FramesWritten++;
                if (_chunkFrames == MatchReplayArchive.FramesPerChunk) FlushChunk();
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Fail("Kayıt yazılamadı: " + exception.Message);
                return false;
            }
        }

        /// <summary>Closes the current chunk early so the next one stores fresh hit marks (called after a hit).</summary>
        public void BreakChunk()
        {
            if (_stream == null) return;
            try { FlushChunk(); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Fail("Kayıt yazılamadı: " + exception.Message);
            }
        }

        /// <summary>
        /// Adds an event in arrival order. Impacts beyond <see cref="MatchReplayArchive.MaxEvents"/> are
        /// dropped and the archive is marked incomplete; round and encounter boundaries use a reserve.
        /// </summary>
        public bool AddEvent(ReplayEvent item)
        {
            bool boundary = item.Type == ReplayEventType.RoundStart || item.Type == ReplayEventType.RoundEnd ||
                            item.Type == ReplayEventType.EncounterEnd;
            if (Info.Events.Count >= MatchReplayArchive.MaxEvents + MatchReplayArchive.ReservedBoundaryEvents ||
                (!boundary && _impactEvents >= MatchReplayArchive.MaxEvents))
            {
                Info.Completeness |= ReplayCompleteness.EventLimit;
                return false;
            }
            if (!MatchReplayArchive.IsFinite(item.Time)) return false;
            // Events arrive in time order from one clock; a late timestamp is clamped so the list stays sorted.
            if (Info.Events.Count > 0 && item.Time < Info.Events[Info.Events.Count - 1].Time) item.Time = Info.Events[Info.Events.Count - 1].Time;
            if (item.Time < 0f) item.Time = 0f;
            item.Sequence = _sequence++;
            Info.Events.Add(item);
            if (!boundary) _impactEvents++;
            return true;
        }

        public void MarkIncomplete(ReplayCompleteness reason, string warning = null)
        {
            Info.Completeness |= reason;
            if (!string.IsNullOrEmpty(warning)) Info.Warning = warning;
        }

        /// <summary>Writes the footer, points the header at it and closes the file. The file stays temporary.</summary>
        public bool Finish()
        {
            if (_stream == null) return false;
            try
            {
                FlushChunk();
                if (Info.Duration < _lastTime || float.IsNegativeInfinity(_lastTime)) Info.Duration = Mathf.Max(0f, _lastTime);
                ClampToDuration();
                byte[] footer = MatchReplayArchive.EncodeFooter(Info);
                if (footer.Length > MatchReplayArchive.MaxFooterBytes) throw new IOException("Kayıt dizini çok büyük.");
                long footerOffset = _stream.Position;
                _stream.Write(footer, 0, footer.Length);
                _stream.Position = 0;
                MatchReplayArchive.WriteHeader(_stream, footerOffset, footer.Length, MatchReplayArchive.Crc32(footer, 0, footer.Length));
                _stream.Flush(true);
                BytesWritten = footerOffset + footer.Length;
                Info.FileBytes = BytesWritten;
                Close();
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Fail("Kayıt tamamlanamadı: " + exception.Message);
                return false;
            }
        }

        /// <summary>Closes and deletes the temporary file.</summary>
        public void Abort()
        {
            Close();
            try { if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public void Dispose()
        {
            if (_stream != null) Abort();
        }

        private bool BeginChunk(float time, int round)
        {
            long chunkBytes = MatchReplayArchive.ChunkBytes(MatchReplayArchive.FramesPerChunk, Info.DynamicTrackCount, Info.WearTrackCount);
            if (BytesWritten + chunkBytes + MatchReplayArchive.MaxFooterBytes > MatchReplayArchive.MaxFileBytes ||
                Info.Chunks.Count >= MatchReplayArchive.MaxChunks)
            {
                IsFull = true;
                MarkIncomplete(ReplayCompleteness.FileLimit, "Dosya boyutu sınırına ulaşıldı; kaydın sonu eksik.");
                return false;
            }
            int offset = 0;
            MatchReplayArchive.WriteInt32(_chunk, ref offset, 0); // frame count, set when the chunk is flushed
            MatchReplayArchive.WriteInt32(_chunk, ref offset, Info.DynamicTrackCount);
            MatchReplayArchive.WriteInt32(_chunk, ref offset, Info.WearTrackCount);
            for (int i = 0; i < _marks.Length; i++)
            {
                Vector4 mark = _marks[i];
                bool finite = MatchReplayArchive.IsFinite(mark.x) && MatchReplayArchive.IsFinite(mark.y) &&
                              MatchReplayArchive.IsFinite(mark.z) && MatchReplayArchive.IsFinite(mark.w);
                if (!finite) mark = Vector4.zero;
                MatchReplayArchive.WriteSingle(_chunk, ref offset, mark.x);
                MatchReplayArchive.WriteSingle(_chunk, ref offset, mark.y);
                MatchReplayArchive.WriteSingle(_chunk, ref offset, mark.z);
                MatchReplayArchive.WriteSingle(_chunk, ref offset, mark.w);
            }
            _chunkLength = offset;
            _chunkStart = time;
            _chunkRound = round;
            return true;
        }

        private void FlushChunk()
        {
            if (_chunkFrames == 0 || _stream == null) return;
            int offset = 0;
            MatchReplayArchive.WriteInt32(_chunk, ref offset, _chunkFrames);
            uint crc = MatchReplayArchive.Crc32(_chunk, 0, _chunkLength);
            long position = BytesWritten;
            _stream.Write(_chunk, 0, _chunkLength);
            BytesWritten += _chunkLength;
            Info.Chunks.Add(new ReplayChunkInfo
            {
                Offset = position, Length = _chunkLength, StartTime = _chunkStart, EndTime = _chunkEnd,
                FrameCount = _chunkFrames, Round = _chunkRound, Crc = crc,
            });
            _chunkFrames = 0;
            _chunkLength = 0;
        }

        private void ClampToDuration()
        {
            for (int i = 0; i < Info.Rounds.Count; i++)
            {
                ReplayRound round = Info.Rounds[i];
                round.EndTime = Mathf.Clamp(round.EndTime, round.StartTime, Info.Duration);
                Info.Rounds[i] = round;
            }
            for (int i = 0; i < Info.Events.Count; i++)
            {
                ReplayEvent item = Info.Events[i];
                if (item.Time > Info.Duration) item.Time = Info.Duration;
                Info.Events[i] = item;
            }
        }

        private void Fail(string message)
        {
            Error = message;
            Abort();
        }

        private void Close()
        {
            if (_stream == null) return;
            try { _stream.Dispose(); }
            catch (IOException) { }
            _stream = null;
            lock (OpenPaths) OpenPaths.Remove(Path.GetFullPath(TemporaryPath));
        }
    }

    /// <summary>
    /// Reads a finished archive for playback. The file stays open; frames are decoded one chunk at a
    /// time into two reused slots, so memory does not depend on the recording's length and a seek costs
    /// one chunk read from the nearest chunk.
    /// </summary>
    public sealed class MatchReplayArchiveReader : IDisposable
    {
        private sealed class DecodedChunk
        {
            public int Index = -1;
            public int FrameCount;
            public float[] Times;
            public MatchReplayClip.Pose[] Poses;
            public byte[] Wear;
            public Vector4[] Marks;
            public int LastUse;
        }

        private readonly DecodedChunk[] _slots = new DecodedChunk[2];
        private readonly byte[] _buffer;
        private FileStream _stream;
        private int _use;

        private MatchReplayArchiveReader(FileStream stream, ReplayArchiveInfo info)
        {
            _stream = stream;
            Info = info;
            int frames = MatchReplayArchive.FramesPerChunk, columns = info.DynamicTrackCount, wear = info.WearTrackCount;
            _buffer = new byte[MatchReplayArchive.ChunkBytes(frames, columns, wear)];
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = new DecodedChunk
                {
                    Times = new float[frames],
                    Poses = new MatchReplayClip.Pose[frames * columns],
                    Wear = new byte[frames * Math.Max(1, wear)],
                    Marks = new Vector4[wear * MatchReplayArchive.MarksPerWearTrack],
                };
        }

        public ReplayArchiveInfo Info { get; }
        public float StartTime => Info.Chunks.Count > 0 ? Info.Chunks[0].StartTime : 0f;
        public float EndTime => Info.Chunks.Count > 0 ? Info.Chunks[Info.Chunks.Count - 1].EndTime : 0f;
        /// <summary>Chunks read from disk so far (tests use it to show seeks read only what they need).</summary>
        public int ChunkReads { get; private set; }
        /// <summary>Bytes of frame data held in memory, whatever the recording's length.</summary>
        public long DecodedBytes => _buffer.Length + 2L * (_slots[0].Times.Length * 4 + _slots[0].Poses.Length * 32 + _slots[0].Wear.Length + _slots[0].Marks.Length * 16);

        public static MatchReplayArchiveReader Open(string path, out string error)
        {
            FileStream stream = null;
            try
            {
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024);
                ReplayArchiveInfo info = MatchReplayArchive.ReadInfo(stream, out error);
                if (info == null)
                {
                    stream.Dispose();
                    return null;
                }
                return new MatchReplayArchiveReader(stream, info);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                stream?.Dispose();
                error = "Kayıt açılamadı: " + exception.Message;
                return null;
            }
        }

        /// <summary>Reads every chunk once and checks its checksum and layout. Memory stays at one chunk.</summary>
        public bool VerifyAllChunks(out string error)
        {
            for (int i = 0; i < Info.Chunks.Count; i++)
                if (!ReadChunk(i, out error)) return false;
            error = null;
            return true;
        }

        /// <summary>
        /// Writes every dynamic track's pose at <paramref name="time"/> (clamped to the recording) the way
        /// <see cref="MatchReplayClip.Sample"/> does: linear position, spherical rotation, stepped
        /// visibility. Frames are never blended across a round boundary. Wear steps with the earlier frame;
        /// <paramref name="marks"/> receives the hit marks stored with that frame's chunk.
        /// </summary>
        public bool Sample(float time, MatchReplayClip.Pose[] poses, byte[] wear, Vector4[] marks, out string error)
        {
            error = null;
            int columns = Info.DynamicTrackCount;
            if (Info.Chunks.Count == 0 || poses == null || poses.Length < columns) return false;
            if (!MatchReplayArchive.IsFinite(time)) time = StartTime;
            time = Mathf.Clamp(time, StartTime, EndTime);
            int index = FindChunk(time);
            DecodedChunk lower = Load(index, out error);
            if (lower == null) return false;

            DecodedChunk upperChunk = lower;
            int from, to;
            if (time <= lower.Times[lower.FrameCount - 1])
            {
                to = FindUpperFrame(lower, time);
                from = Mathf.Max(0, to - 1);
            }
            else
            {
                from = to = lower.FrameCount - 1;
                bool sameRound = index + 1 < Info.Chunks.Count && Info.Chunks[index + 1].Round == Info.Chunks[index].Round;
                if (sameRound)
                {
                    upperChunk = Load(index + 1, out error, lower);
                    if (upperChunk == null) return false;
                    to = 0;
                }
            }

            float t0 = lower.Times[from], t1 = upperChunk.Times[to];
            float blend = t1 > t0 ? Mathf.Clamp01((time - t0) / (t1 - t0)) : 1f;
            int a = from * columns, b = to * columns;
            for (int i = 0; i < columns; i++)
            {
                MatchReplayClip.Pose start = lower.Poses[a + i], end = upperChunk.Poses[b + i];
                poses[i] = new MatchReplayClip.Pose(
                    Vector3.Lerp(start.Position, end.Position, blend),
                    Quaternion.Slerp(Normalize(start.Rotation), Normalize(end.Rotation), blend),
                    blend >= 1f ? end.Visible : start.Visible && end.Visible);
            }
            int wearColumns = Info.WearTrackCount;
            if (wear != null && wear.Length >= wearColumns)
            {
                DecodedChunk wearChunk = blend >= 1f ? upperChunk : lower;
                int wearFrame = blend >= 1f ? to : from;
                Array.Copy(wearChunk.Wear, wearFrame * Math.Max(1, wearColumns), wear, 0, wearColumns);
            }
            if (marks != null && marks.Length >= lower.Marks.Length)
                Array.Copy((blend >= 1f ? upperChunk : lower).Marks, marks, lower.Marks.Length);
            return true;
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
        }

        private int FindChunk(float time)
        {
            int low = 0, high = Info.Chunks.Count - 1;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (Info.Chunks[mid].StartTime <= time) low = mid;
                else high = mid - 1;
            }
            return low;
        }

        private static int FindUpperFrame(DecodedChunk chunk, float time)
        {
            int low = 0, high = chunk.FrameCount - 1;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (chunk.Times[mid] < time) low = mid + 1;
                else high = mid;
            }
            return low;
        }

        private DecodedChunk Load(int index, out string error, DecodedChunk keep = null)
        {
            error = null;
            foreach (DecodedChunk slot in _slots)
                if (slot.Index == index)
                {
                    slot.LastUse = ++_use;
                    return slot;
                }
            DecodedChunk target = _slots[0] == keep ? _slots[1] : _slots[1] == keep ? _slots[0]
                : _slots[0].LastUse <= _slots[1].LastUse ? _slots[0] : _slots[1];
            if (!ReadChunk(index, out error)) return null;
            Decode(index, target);
            target.LastUse = ++_use;
            return target;
        }

        private bool ReadChunk(int index, out string error)
        {
            error = null;
            if (_stream == null)
            {
                error = "Kayıt kapalı.";
                return false;
            }
            ReplayChunkInfo chunk = Info.Chunks[index];
            try
            {
                _stream.Position = chunk.Offset;
                if (!MatchReplayArchive.ReadExactly(_stream, _buffer, chunk.Length))
                {
                    error = "Kayıt parçası eksik.";
                    return false;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = "Kayıt parçası okunamadı: " + exception.Message;
                return false;
            }
            ChunkReads++;
            if (MatchReplayArchive.Crc32(_buffer, 0, chunk.Length) != chunk.Crc)
            {
                error = $"Kayıt parçası {index + 1} bozuk (sağlama).";
                return false;
            }
            int offset = 0;
            int frames = MatchReplayArchive.ReadInt32(_buffer, ref offset);
            int columns = MatchReplayArchive.ReadInt32(_buffer, ref offset);
            int wear = MatchReplayArchive.ReadInt32(_buffer, ref offset);
            if (frames != chunk.FrameCount || columns != Info.DynamicTrackCount || wear != Info.WearTrackCount)
            {
                error = $"Kayıt parçası {index + 1} dizinle uyuşmuyor.";
                return false;
            }
            offset = MatchReplayArchive.ChunkPrefixBytes(wear);
            float previous = float.NegativeInfinity;
            int frameBytes = MatchReplayArchive.FrameBytes(columns);
            for (int f = 0; f < frames; f++)
            {
                int at = offset + f * frameBytes;
                float time = MatchReplayArchive.ReadSingle(_buffer, ref at);
                if (!MatchReplayArchive.IsFinite(time) || time <= previous || time < chunk.StartTime || time > chunk.EndTime)
                {
                    error = $"Kayıt parçası {index + 1} zaman sırası bozuk.";
                    return false;
                }
                previous = time;
                for (int c = 0; c < columns; c++)
                {
                    int x = at + c * MatchReplayArchive.PoseBytes;
                    float px = MatchReplayArchive.ReadSingle(_buffer, ref x), py = MatchReplayArchive.ReadSingle(_buffer, ref x), pz = MatchReplayArchive.ReadSingle(_buffer, ref x);
                    if (!MatchReplayArchive.IsFinite(px) || !MatchReplayArchive.IsFinite(py) || !MatchReplayArchive.IsFinite(pz) ||
                        Mathf.Abs(px) > 1e5f || Mathf.Abs(py) > 1e5f || Mathf.Abs(pz) > 1e5f)
                    {
                        error = $"Kayıt parçası {index + 1} geçersiz konum içeriyor.";
                        return false;
                    }
                }
            }
            return true;
        }

        private void Decode(int index, DecodedChunk target)
        {
            int offset = 0;
            int frames = MatchReplayArchive.ReadInt32(_buffer, ref offset);
            int columns = MatchReplayArchive.ReadInt32(_buffer, ref offset);
            int wearColumns = MatchReplayArchive.ReadInt32(_buffer, ref offset);
            for (int i = 0; i < target.Marks.Length; i++)
                target.Marks[i] = new Vector4(MatchReplayArchive.ReadSingle(_buffer, ref offset), MatchReplayArchive.ReadSingle(_buffer, ref offset),
                    MatchReplayArchive.ReadSingle(_buffer, ref offset), MatchReplayArchive.ReadSingle(_buffer, ref offset));
            int wearStride = Math.Max(1, wearColumns);
            for (int f = 0; f < frames; f++)
            {
                target.Times[f] = MatchReplayArchive.ReadSingle(_buffer, ref offset);
                int wearColumn = 0;
                for (int c = 0; c < columns; c++)
                {
                    target.Poses[f * columns + c] = MatchReplayArchive.ReadPose(_buffer, ref offset, out byte wear);
                    if (IsWearColumn(c)) target.Wear[f * wearStride + wearColumn++] = wear;
                }
            }
            target.FrameCount = frames;
            target.Index = index;
        }

        private bool[] _wearColumns;

        private bool IsWearColumn(int column)
        {
            if (_wearColumns == null)
            {
                _wearColumns = new bool[Info.DynamicTrackCount];
                foreach (ReplayTrack track in Info.Tracks)
                    if (track.Column >= 0) _wearColumns[track.Column] = track.WearColumn >= 0;
            }
            return _wearColumns[column];
        }

        private static Quaternion Normalize(Quaternion q)
        {
            float magnitude = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return magnitude > 1e-6f ? new Quaternion(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude) : Quaternion.identity;
        }
    }

    /// <summary>
    /// The replay archive folder: finished archives (&lt;guid&gt;.rpl), files being written (&lt;guid&gt;.tmp)
    /// and at most one finished recording waiting for room (pending/&lt;guid&gt;.rpl). It only ever opens,
    /// moves or deletes files whose names are its own GUID pattern inside its folder; match history,
    /// settings, bindings, records and ghosts live elsewhere and are never touched.
    /// </summary>
    public sealed class MatchReplayArchiveStore
    {
        public const int MaxRecordings = 10;
        public const long MaxArchiveBytes = 512L * 1024 * 1024;
        public const string PendingFolder = "pending";

        public enum CommitResult { Archived, Pending, Failed }

        public sealed class Entry
        {
            public string Id;
            public string Path;
            /// <summary>Null when the file could not be read; see <see cref="Error"/>.</summary>
            public ReplayArchiveInfo Info;
            public string Error;
            public long Bytes;
            public DateTime ModifiedUtc;
            public bool IsReadable => Info != null;
        }

        public MatchReplayArchiveStore(string directory = null)
        {
            DirectoryPath = System.IO.Path.GetFullPath(directory ?? DefaultDirectory);
        }

        /// <summary>
        /// The player's archive is persistentDataPath/replays. The Editor uses a folder in the project's
        /// Library instead, so Editor sessions and test runs never fill a player's archive.
        /// </summary>
        public static string DefaultDirectory => Application.isEditor
            ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", "Library", "RobotSavaslariReplays")
            : System.IO.Path.Combine(Application.persistentDataPath, "replays");

        public string DirectoryPath { get; }
        public string PendingPath => System.IO.Path.Combine(DirectoryPath, PendingFolder);
        public string LastError { get; private set; }

        /// <summary>Finished archives, newest first. Unreadable files are listed with their error so they can be deleted.</summary>
        public List<Entry> List()
        {
            var entries = new List<Entry>();
            LastError = null;
            try
            {
                if (!Directory.Exists(DirectoryPath)) return entries;
                foreach (string path in Directory.GetFiles(DirectoryPath, "*" + MatchReplayArchive.FileExtension))
                {
                    Entry entry = ReadEntry(path);
                    if (entry != null) entries.Add(entry);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                LastError = "Arşiv okunamadı: " + exception.Message;
            }
            entries.Sort((a, b) =>
            {
                DateTime left = a.Info != null ? a.Info.CreatedLocal : a.ModifiedUtc.ToLocalTime();
                DateTime right = b.Info != null ? b.Info.CreatedLocal : b.ModifiedUtc.ToLocalTime();
                int order = right.CompareTo(left);
                return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
            });
            return entries;
        }

        /// <summary>The finished recording waiting for room in the archive, or null.</summary>
        public Entry Pending()
        {
            try
            {
                if (!Directory.Exists(PendingPath)) return null;
                foreach (string path in Directory.GetFiles(PendingPath, "*" + MatchReplayArchive.FileExtension))
                {
                    Entry entry = ReadEntry(path);
                    if (entry != null) return entry;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                LastError = "Bekleyen kayıt okunamadı: " + exception.Message;
            }
            return null;
        }

        public static long TotalBytes(List<Entry> entries)
        {
            long total = 0;
            foreach (Entry entry in entries) total += entry.Bytes;
            return total;
        }

        /// <summary>True when one more archive of <paramref name="bytes"/> fits the count and size quota.</summary>
        public bool HasRoomFor(long bytes, List<Entry> entries = null)
        {
            entries = entries ?? List();
            return entries.Count < MaxRecordings && TotalBytes(entries) + bytes <= MaxArchiveBytes;
        }

        /// <summary>Starts a new recording in a temporary file named after a fresh GUID.</summary>
        public MatchReplayArchiveWriter CreateWriter(ReplayArchiveInfo info)
        {
            info.Id = Guid.NewGuid().ToString("N");
            string path = System.IO.Path.Combine(DirectoryPath, info.Id + MatchReplayArchive.TemporaryExtension);
            return new MatchReplayArchiveWriter(path, info);
        }

        /// <summary>
        /// Finishes a writer and moves its file into the archive in one rename. When the archive is full the
        /// finished file waits in the pending folder instead; old archives are never removed to make room.
        /// When a recording is already waiting, the new one is dropped and the message says so.
        /// </summary>
        public CommitResult Commit(MatchReplayArchiveWriter writer, out string message)
        {
            message = null;
            if (writer == null || !writer.Finish())
            {
                message = "Tam kayıt yok: " + (writer?.Error ?? "kayıt yazılamadı.");
                return CommitResult.Failed;
            }
            string id = writer.Info.Id;
            try
            {
                List<Entry> entries = List();
                if (HasRoomFor(writer.BytesWritten, entries))
                {
                    File.Move(writer.TemporaryPath, ArchivePath(id));
                    message = writer.Info.IsComplete ? "Tam tekrar arşive kaydedildi." : "Tekrar arşive kaydedildi (" + MatchReplayArchive.DescribeCompleteness(writer.Info) + ").";
                    return CommitResult.Archived;
                }
                if (Pending() != null)
                {
                    File.Delete(writer.TemporaryPath);
                    message = "Tekrar kaydedilemedi: arşiv dolu ve bekleyen bir kayıt var. Tekrar arşivinden kayıt silin.";
                    return CommitResult.Failed;
                }
                Directory.CreateDirectory(PendingPath);
                File.Move(writer.TemporaryPath, System.IO.Path.Combine(PendingPath, id + MatchReplayArchive.FileExtension));
                message = $"Arşiv dolu ({MaxRecordings} kayıt / {MaxArchiveBytes / (1024 * 1024)} MiB): yeni kayıt bekliyor. Tekrar arşivinden kayıt silerek yer açın.";
                return CommitResult.Pending;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                try { if (File.Exists(writer.TemporaryPath)) File.Delete(writer.TemporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                message = "Tam kayıt yok: arşive taşınamadı (" + exception.Message + ").";
                return CommitResult.Failed;
            }
        }

        /// <summary>Moves the waiting recording into the archive when it fits. False (with a reason) otherwise.</summary>
        public bool TryPromotePending(out string message)
        {
            message = null;
            Entry pending = Pending();
            if (pending == null) return false;
            List<Entry> entries = List();
            if (!HasRoomFor(pending.Bytes, entries))
            {
                message = "Bekleyen kayıt için yer yok.";
                return false;
            }
            try
            {
                File.Move(pending.Path, ArchivePath(pending.Id));
                message = "Bekleyen kayıt arşive eklendi.";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                message = "Bekleyen kayıt taşınamadı: " + exception.Message;
                return false;
            }
        }

        /// <summary>Deletes one archive by id. Only an id of the archive's own GUID form inside its folder is accepted.</summary>
        public bool Delete(string id, out string error) => DeleteFile(id, false, out error);

        /// <summary>Deletes the waiting recording, after the player confirmed it.</summary>
        public bool DiscardPending(string id, out string error) => DeleteFile(id, true, out error);

        /// <summary>The archive file for an id, or false when the id is not a valid archive id.</summary>
        public bool TryGetArchivePath(string id, out string path)
        {
            path = null;
            if (!MatchReplayArchive.IsValidId(id)) return false;
            string candidate = ArchivePath(id);
            if (!IsInside(candidate, DirectoryPath)) return false;
            path = candidate;
            return true;
        }

        /// <summary>
        /// Removes temporary files left by an interrupted session (a crash or a killed process). Files a
        /// writer in this process is still filling are kept. Returns the number removed.
        /// </summary>
        public int CleanupTemporaryFiles()
        {
            int removed = 0;
            try
            {
                if (!Directory.Exists(DirectoryPath)) return 0;
                foreach (string path in Directory.GetFiles(DirectoryPath, "*" + MatchReplayArchive.TemporaryExtension))
                {
                    if (!MatchReplayArchive.IsValidId(System.IO.Path.GetFileNameWithoutExtension(path)) || MatchReplayArchiveWriter.IsBeingWritten(path)) continue;
                    File.Delete(path);
                    removed++;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                LastError = "Yarım kayıtlar temizlenemedi: " + exception.Message;
            }
            return removed;
        }

        private bool DeleteFile(string id, bool pending, out string error)
        {
            error = null;
            if (!MatchReplayArchive.IsValidId(id))
            {
                error = "Geçersiz kayıt kimliği.";
                return false;
            }
            string folder = pending ? PendingPath : DirectoryPath;
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, id + MatchReplayArchive.FileExtension));
            if (!IsInside(path, folder))
            {
                error = "Geçersiz kayıt yolu.";
                return false;
            }
            try
            {
                if (!File.Exists(path))
                {
                    error = "Kayıt bulunamadı.";
                    return false;
                }
                File.Delete(path);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = "Kayıt silinemedi: " + exception.Message;
                return false;
            }
        }

        private string ArchivePath(string id) => System.IO.Path.Combine(DirectoryPath, id + MatchReplayArchive.FileExtension);

        private static bool IsInside(string path, string folder)
        {
            string full = System.IO.Path.GetFullPath(path);
            string root = System.IO.Path.GetFullPath(folder).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) +
                          System.IO.Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                   full.IndexOf(System.IO.Path.DirectorySeparatorChar, root.Length) < 0;
        }

        private static Entry ReadEntry(string path)
        {
            string id = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!MatchReplayArchive.IsValidId(id)) return null;
            var entry = new Entry { Id = id, Path = path };
            try
            {
                var file = new FileInfo(path);
                entry.Bytes = file.Length;
                entry.ModifiedUtc = file.LastWriteTimeUtc;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                entry.Error = "Kayıt okunamadı: " + exception.Message;
                return entry;
            }
            entry.Info = MatchReplayArchive.ReadInfo(path, out string error);
            if (entry.Info != null && entry.Info.Id != id)
            {
                entry.Info = null;
                error = "Kayıt kimliği dosya adıyla uyuşmuyor.";
            }
            entry.Error = error;
            return entry;
        }
    }
}
