using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using RobotSavaslari.Arena;

namespace RobotSavaslari.Presentation
{
    /// <summary>What a track draws and where its look comes from when an archive is played.</summary>
    public enum ReplayTrackKind : byte
    {
        /// <summary>A mesh renderer inside a match robot; the template is the same path on the live robot.</summary>
        RobotPart = 1,
        /// <summary>A detached part flying as debris; the template is the robot part it came from.</summary>
        RobotDebris = 2,
        /// <summary>Arena layout geometry that never moves; its pose is stored once in the track table.</summary>
        LayoutStatic = 3,
        /// <summary>Arena layout geometry that moves or blinks (the warning barrier).</summary>
        LayoutDynamic = 4,
        HazardMarker = 5,
    }

    public struct ReplayHazardSample
    {
        public Vector3 Scale;
        public HazardPhase Phase;
    }

    public enum ReplayImpactSource : byte { Unknown, Weapon, RobotContact }

    public enum ReplayEventType : byte
    {
        RoundStart = 1,
        Impact = 2,
        PartDetached = 3,
        RoundEnd = 4,
        EncounterEnd = 5,
        HazardPhaseChanged = 6,
    }

    /// <summary>Why an archive is not a full record of its encounter. Zero means complete.</summary>
    [Flags]
    public enum ReplayCompleteness
    {
        Complete = 0,
        /// <summary>The series was left before it was decided.</summary>
        Abandoned = 1,
        /// <summary>More renderers than <see cref="MatchReplayArchive.MaxTracks"/>; the rest were not recorded.</summary>
        TrackLimit = 2,
        /// <summary>The file reached <see cref="MatchReplayArchive.MaxFileBytes"/>; later frames were not recorded.</summary>
        FileLimit = 4,
        /// <summary>More events than <see cref="MatchReplayArchive.MaxEvents"/>; later impacts were not listed.</summary>
        EventLimit = 8,
        /// <summary>Recording stopped because the recorder was disabled or the application closed mid-round.</summary>
        Interrupted = 16,
    }

    public sealed class ReplayTrack
    {
        public string Key;
        public ReplayTrackKind Kind;
        /// <summary><see cref="MatchReplayRecorder.PlayerRobot"/>, <see cref="MatchReplayRecorder.OpponentRobot"/> or -1 for the arena.</summary>
        public int Robot;
        /// <summary>The track is a damage wear overlay; frames carry its wear amount and chunks its hit marks.</summary>
        public bool Wear;
        /// <summary>Vertex count of the template mesh; a different count means the look changed since recording.</summary>
        public int MeshSignature;
        public Vector3 Scale = Vector3.one;
        public Vector3 StaticPosition;
        public Quaternion StaticRotation = Quaternion.identity;
        public bool StaticVisible;
        public bool HasColor;
        public Color Color = Color.white;
        /// <summary>Column in each frame, or -1 for static tracks. Assigned by <see cref="ReplayArchiveInfo.AssignColumns"/>.</summary>
        public int Column = -1;
        /// <summary>Wear column in each frame, or -1. Assigned by <see cref="ReplayArchiveInfo.AssignColumns"/>.</summary>
        public int WearColumn = -1;

        public int HazardColumn = -1;
        public bool IsLayout => Kind == ReplayTrackKind.LayoutStatic || Kind == ReplayTrackKind.LayoutDynamic || Kind == ReplayTrackKind.HazardMarker;
        public bool IsDynamic => Kind != ReplayTrackKind.LayoutStatic;
    }

    public struct ReplayRound
    {
        public int MatchId;
        public int RoundNumber;
        public float StartTime;
        public float EndTime;
        /// <summary>MatchController.MatchOutcome as an integer; 0 while the round had no result.</summary>
        public int Outcome;
        /// <summary>RoundFinishReason as an integer.</summary>
        public int Reason;
    }

    public struct ReplayEvent
    {
        public float Time;
        /// <summary>Order of arrival; ties in <see cref="Time"/> keep this order.</summary>
        public int Sequence;
        public ReplayEventType Type;
        public ReplayImpactSource Source;
        public HazardPhase Phase;
        public int Robot;
        public int Round;
        /// <summary>The detached part's track, or -1.</summary>
        public int Track;
        public Vector3 Point;
        public Vector3 Normal;
        public float Amount;
        public int Outcome;
    }

    public struct ReplayCell
    {
        public int X, Y;
        /// <summary>ArenaContent by name, so a value unknown to this build stays recognisable.</summary>
        public string Content;
        public int Spawn, Wall;
        public Vector2 Direction;
    }

    public struct ReplayChunkInfo
    {
        public long Offset;
        public int Length;
        public float StartTime;
        public float EndTime;
        public int FrameCount;
        public int Round;
        public uint Crc;
    }

    /// <summary>Everything about an archive except its frames: the footer of the file.</summary>
    public sealed class ReplayArchiveInfo
    {
        public int Schema = MatchReplayArchive.SchemaVersion;
        public int ContentRevision = MatchReplayArchive.ContentRevision;
        public string Id;
        /// <summary>MatchRecord.id of the encounter when it finished; null when it never finished.</summary>
        public string EncounterId;
        public string CreatedUtc;
        public string Title;
        public string PlayerName;
        public string OpponentName;
        public string ArenaName;
        /// <summary>Name of the material the arena layout objects were drawn with.</summary>
        public string LayoutMaterial;
        public int RulesMode;
        public float TimeLimit;
        public int Outcome;
        public int PlayerWins, OpponentWins, DrawnRounds;
        public float Duration;
        public int SampleRate = MatchReplayArchive.SampleRate;
        public ReplayCompleteness Completeness;
        public string Warning;
        public readonly List<ReplayTrack> Tracks = new List<ReplayTrack>();
        public readonly List<ReplayRound> Rounds = new List<ReplayRound>();
        public readonly List<ReplayEvent> Events = new List<ReplayEvent>();
        public readonly List<ReplayCell> Cells = new List<ReplayCell>();
        public readonly List<ReplayChunkInfo> Chunks = new List<ReplayChunkInfo>();
        /// <summary>Size of the archive file; filled in when read.</summary>
        public long FileBytes;

        public int DynamicTrackCount { get; private set; }
        public int HazardTrackCount { get; private set; }
        public int WearTrackCount { get; private set; }
        public bool IsComplete => Completeness == ReplayCompleteness.Complete;

        /// <summary>Gives every dynamic track its frame column and every wear track its wear column.</summary>
        public void AssignColumns()
        {
            int columns = 0, wear = 0, hazards = 0;
            foreach (ReplayTrack track in Tracks)
            {
                track.Column = track.IsDynamic ? columns++ : -1;
                track.HazardColumn = track.Kind == ReplayTrackKind.HazardMarker ? hazards++ : -1;
                track.WearColumn = track.IsDynamic && track.Wear ? wear++ : -1;
            }
            DynamicTrackCount = columns;
            WearTrackCount = wear;
            HazardTrackCount = hazards;
        }

        public DateTime CreatedLocal => DateTime.TryParse(CreatedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime value)
            ? value.ToLocalTime() : DateTime.MinValue;
    }

    /// <summary>
    /// Version 1 of the full-encounter replay file and its rules. A file is a fixed header, frame chunks
    /// and a footer (<see cref="ReplayArchiveInfo"/>) whose position the header records once the file is
    /// finished. Every count, length, offset and number is checked against the limits below before
    /// anything is allocated for it, so a damaged, future or hostile file is rejected, never trusted.
    /// </summary>
    /// <remarks>
    /// Binary, not JSON: a frame of 128 tracks is 128 × 22 bytes here and about 128 × 150 bytes as
    /// JsonUtility text (measured by MatchReplayArchiveTests.BinaryFramesAreSmallerThanJson), so fifteen
    /// minutes at 20 Hz would not fit the 128 MiB file budget as JSON.
    /// </remarks>
    public static class MatchReplayArchive
    {
        public const int SchemaVersion = 1;
        /// <summary>Revision 2 adds hazard samples and known impact sources. Revision 1 retains its original layout.</summary>
        public const int ContentRevision = 2;
        public static bool SupportsRevision(int revision) => revision == 1 || revision == ContentRevision;
        public const int SampleRate = 20;
        public const int FramesPerChunk = 20;
        public const int HazardBytes = 13; // world scale + phase for each hazard marker
        public const int MaxTracks = 128;
        public const int MaxEvents = 4096;
        /// <summary>Round and encounter boundaries may use this many slots beyond <see cref="MaxEvents"/>.</summary>
        public const int ReservedBoundaryEvents = 256;
        public const int MaxRounds = 64;
        public const int MaxCells = 64;
        public const int MaxChunks = 65536;
        public const int MaxStringBytes = 512;
        public const long MaxFileBytes = 128L * 1024 * 1024;
        public const int MaxFooterBytes = 4 * 1024 * 1024;
        public const int HeaderBytes = 32;
        /// <summary>Position (12), rotation as four signed 16-bit values (8), flags (1), wear (1).</summary>
        public const int PoseBytes = 22;
        public const int MarksPerWearTrack = 4;
        public const string FileExtension = ".rpl";
        public const string TemporaryExtension = ".tmp";

        private static readonly byte[] Magic = { (byte)'R', (byte)'S', (byte)'R', (byte)'A' };

        public static string ReadFailureLabel(string error) =>
            error != null && error.StartsWith("Uyumsuz:", StringComparison.Ordinal)
                ? error : "Bozuk kayıt: " + error;

        public static int FrameBytes(int dynamicTracks, int hazards = 0) => 4 + dynamicTracks * PoseBytes + hazards * HazardBytes;
        public static int ChunkPrefixBytes(int wearTracks) => 12 + wearTracks * MarksPerWearTrack * 16;
        public static long ChunkBytes(int frames, int dynamicTracks, int wearTracks, int hazards = 0) =>
            ChunkPrefixBytes(wearTracks) + (long)frames * FrameBytes(dynamicTracks, hazards);

        // ---- Track keys -------------------------------------------------------------------------

        public const string RobotPrefix = "robot/";
        public const string DebrisPrefix = "debris/";
        public const string LayoutPrefix = "layout/";

        /// <summary>Names of layout objects this build can draw. Anything else under Layout Contents is unsupported.</summary>
        public static readonly string[] LayoutNames =
        {
            "Arena Obstacle", "Reinforced Perimeter", "Arena Warning Barrier", "Moving Barrier", "Barrier Warning",
            "Arena Conveyor", "Conveyor Deck", "Conveyor Direction", "Direction Mark",
        };

        /// <summary>Layout objects that move or blink during play.</summary>
        public static bool IsDynamicLayoutName(string name) => name == "Moving Barrier" || name == "Barrier Warning";

        /// <summary>ArenaContent values whose visuals this build records and draws.</summary>
        public static readonly string[] SupportedCellContents = { "Floor", "Obstacle", "Hazard", "Conveyor", "Pulse", "Sweep" };

        public static bool IsSupportedLayoutName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name == "Pulse floor marker" || name == "Sweep floor marker" ||
                IsHazardParent(name)) return true;
            foreach (string allowed in LayoutNames)
                if (name == allowed || name.StartsWith(allowed + " ", StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsHazardParent(string name) =>
            name.Length == 12 && (name.StartsWith("Pulse (", StringComparison.Ordinal) || name.StartsWith("Sweep (", StringComparison.Ordinal)) &&
            name[7] >= '0' && name[7] <= '7' && name[8] == ',' && name[9] == ' ' && name[10] >= '0' && name[10] <= '7' && name[11] == ')';

        public static bool IsSupportedCellContent(string content) => Array.IndexOf(SupportedCellContents, content) >= 0;

        /// <summary>
        /// The whitelist for track keys: a known prefix, then non-empty segments of letters, digits and
        /// a few punctuation marks. No backslash, "..", control or path-root characters can appear, so a key
        /// can never be used as a file path or reach outside the robot it names.
        /// </summary>
        public static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) > MaxStringBytes) return false;
            string prefix = key.StartsWith(RobotPrefix, StringComparison.Ordinal) ? RobotPrefix
                : key.StartsWith(DebrisPrefix, StringComparison.Ordinal) ? DebrisPrefix
                : key.StartsWith(LayoutPrefix, StringComparison.Ordinal) ? LayoutPrefix : null;
            if (prefix == null) return false;
            string[] segments = key.Substring(prefix.Length).Split('/');
            if (segments.Length < 2) return false;
            foreach (string segment in segments)
            {
                if (segment.Length == 0 || segment == "." || segment == ".." || segment.Trim().Length != segment.Length) return false;
                foreach (char c in segment)
                    if (!(char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-' || c == '(' || c == ')' || c == ',' || c == '.' || c == '#'))
                        return false;
            }
            if (prefix == LayoutPrefix)
            {
                // layout/<index>/<object path>: the index is a number and every object is a known layout object.
                if (!int.TryParse(segments[0], out int index) || index < 0) return false;
                for (int i = 1; i < segments.Length; i++)
                {
                    string segment = segments[i];
                    if (!IsSupportedLayoutName(segment)) return false;
                    if (IsHazardParent(segment) || segment.EndsWith(" floor marker", StringComparison.Ordinal))
                        return segments.Length == 3 && i == 1 && IsHazardParent(segment) && segments[2] == segment.Substring(0, 5) + " floor marker";
                }
            }
            return true;
        }

        /// <summary>The robot name inside a robot or debris key.</summary>
        public static string KeyRobotName(string key)
        {
            string rest = key.StartsWith(RobotPrefix, StringComparison.Ordinal) ? key.Substring(RobotPrefix.Length)
                : key.StartsWith(DebrisPrefix, StringComparison.Ordinal) ? key.Substring(DebrisPrefix.Length) : null;
            if (rest == null) return null;
            int slash = rest.IndexOf('/');
            return slash > 0 ? rest.Substring(0, slash) : null;
        }

        /// <summary>The hierarchy path under the robot inside a robot or debris key.</summary>
        public static string KeyRobotPath(string key)
        {
            string name = KeyRobotName(key);
            if (name == null) return null;
            int prefix = key.StartsWith(RobotPrefix, StringComparison.Ordinal) ? RobotPrefix.Length : DebrisPrefix.Length;
            return key.Substring(prefix + name.Length + 1);
        }

        // ---- Frame encoding ----------------------------------------------------------------------

        public static void WriteInt32(byte[] buffer, ref int offset, int value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 24);
        }

        public static int ReadInt32(byte[] buffer, ref int offset)
        {
            int value = buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24;
            offset += 4;
            return value;
        }

        public static void WriteSingle(byte[] buffer, ref int offset, float value) => WriteInt32(buffer, ref offset, BitConverter.SingleToInt32Bits(value));
        public static float ReadSingle(byte[] buffer, ref int offset) => BitConverter.Int32BitsToSingle(ReadInt32(buffer, ref offset));

        private static void WriteInt16(byte[] buffer, ref int offset, short value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
        }

        private static short ReadInt16(byte[] buffer, ref int offset)
        {
            short value = (short)(buffer[offset] | buffer[offset + 1] << 8);
            offset += 2;
            return value;
        }

        /// <summary>Writes one pose in <see cref="PoseBytes"/> bytes. Non-finite values are stored as an invisible origin pose.</summary>
        public static void WritePose(byte[] buffer, ref int offset, MatchReplayClip.Pose pose, byte wear)
        {
            Vector3 p = pose.Position;
            Quaternion q = pose.Rotation;
            bool finite = IsFinite(p) && IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);
            float magnitude = finite ? Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w) : 0f;
            if (!finite) p = Vector3.zero;
            if (magnitude < 1e-6f) q = Quaternion.identity;
            else q = new Quaternion(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude);
            WriteSingle(buffer, ref offset, p.x);
            WriteSingle(buffer, ref offset, p.y);
            WriteSingle(buffer, ref offset, p.z);
            WriteInt16(buffer, ref offset, QuantizeUnit(q.x));
            WriteInt16(buffer, ref offset, QuantizeUnit(q.y));
            WriteInt16(buffer, ref offset, QuantizeUnit(q.z));
            WriteInt16(buffer, ref offset, QuantizeUnit(q.w));
            buffer[offset++] = (byte)(finite && pose.Visible ? 1 : 0);
            buffer[offset++] = wear;
        }

        public static MatchReplayClip.Pose ReadPose(byte[] buffer, ref int offset, out byte wear)
        {
            var position = new Vector3(ReadSingle(buffer, ref offset), ReadSingle(buffer, ref offset), ReadSingle(buffer, ref offset));
            var rotation = new Quaternion(ReadInt16(buffer, ref offset) / 32767f, ReadInt16(buffer, ref offset) / 32767f,
                ReadInt16(buffer, ref offset) / 32767f, ReadInt16(buffer, ref offset) / 32767f);
            bool visible = (buffer[offset++] & 1) != 0;
            wear = buffer[offset++];
            return new MatchReplayClip.Pose(position, rotation, visible);
        }

        private static short QuantizeUnit(float value) => (short)Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) * 32767f);

        public static byte QuantizeWear(float wear) => (byte)Mathf.RoundToInt(Mathf.Clamp01(IsFinite(wear) ? wear : 0f) * 255f);

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(Vector4 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);

        // ---- CRC-32 (IEEE) -------------------------------------------------------------------------

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                table[i] = value;
            }
            return table;
        }

        public static uint Crc32(byte[] buffer, int offset, int count)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = offset, end = offset + count; i < end; i++) crc = CrcTable[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            return ~crc;
        }

        // ---- Header ---------------------------------------------------------------------------------

        public static void WriteHeader(Stream stream, long footerOffset, int footerLength, uint footerCrc)
        {
            var header = new byte[HeaderBytes];
            int offset = 0;
            for (int i = 0; i < Magic.Length; i++) header[offset++] = Magic[i];
            WriteInt32(header, ref offset, SchemaVersion);
            WriteInt32(header, ref offset, (int)(footerOffset & 0xFFFFFFFF));
            WriteInt32(header, ref offset, (int)(footerOffset >> 32));
            WriteInt32(header, ref offset, footerLength);
            WriteInt32(header, ref offset, unchecked((int)footerCrc));
            stream.Write(header, 0, header.Length);
        }

        // ---- Footer ---------------------------------------------------------------------------------

        public static byte[] EncodeFooter(ReplayArchiveInfo info)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory, Encoding.UTF8))
            {
                writer.Write(info.ContentRevision);
                WriteString(writer, info.Id);
                WriteString(writer, info.EncounterId);
                WriteString(writer, info.CreatedUtc);
                WriteString(writer, info.Title);
                WriteString(writer, info.PlayerName);
                WriteString(writer, info.OpponentName);
                WriteString(writer, info.ArenaName);
                WriteString(writer, info.LayoutMaterial);
                writer.Write(info.RulesMode);
                writer.Write(info.TimeLimit);
                writer.Write(info.Outcome);
                writer.Write(info.PlayerWins);
                writer.Write(info.OpponentWins);
                writer.Write(info.DrawnRounds);
                writer.Write(info.Duration);
                writer.Write(info.SampleRate);
                writer.Write((int)info.Completeness);
                WriteString(writer, info.Warning);

                writer.Write(info.Tracks.Count);
                foreach (ReplayTrack track in info.Tracks)
                {
                    WriteString(writer, track.Key);
                    writer.Write((byte)track.Kind);
                    writer.Write((sbyte)track.Robot);
                    writer.Write(track.Wear);
                    writer.Write(track.MeshSignature);
                    WriteVector(writer, track.Scale);
                    WriteVector(writer, track.StaticPosition);
                    WriteQuaternion(writer, track.StaticRotation);
                    writer.Write(track.StaticVisible);
                    writer.Write(track.HasColor);
                    writer.Write(track.Color.r); writer.Write(track.Color.g); writer.Write(track.Color.b); writer.Write(track.Color.a);
                }

                writer.Write(info.Rounds.Count);
                foreach (ReplayRound round in info.Rounds)
                {
                    writer.Write(round.MatchId);
                    writer.Write(round.RoundNumber);
                    writer.Write(round.StartTime);
                    writer.Write(round.EndTime);
                    writer.Write(round.Outcome);
                    writer.Write(round.Reason);
                }

                writer.Write(info.Events.Count);
                foreach (ReplayEvent item in info.Events)
                {
                    writer.Write(item.Time);
                    writer.Write(item.Sequence);
                    writer.Write((byte)item.Type);
                    writer.Write((sbyte)item.Robot);
                    writer.Write(item.Round);
                    writer.Write(item.Track);
                    WriteVector(writer, item.Point);
                    WriteVector(writer, item.Normal);
                    writer.Write(item.Amount);
                    writer.Write(item.Outcome);
                    if (info.ContentRevision >= 2 && item.Type == ReplayEventType.Impact) writer.Write((byte)item.Source);
                    if (info.ContentRevision >= 2 && item.Type == ReplayEventType.HazardPhaseChanged) writer.Write((byte)item.Phase);
                }

                writer.Write(info.Cells.Count);
                foreach (ReplayCell cell in info.Cells)
                {
                    writer.Write(cell.X);
                    writer.Write(cell.Y);
                    WriteString(writer, cell.Content);
                    writer.Write(cell.Spawn);
                    writer.Write(cell.Wall);
                    writer.Write(cell.Direction.x);
                    writer.Write(cell.Direction.y);
                }

                writer.Write(info.Chunks.Count);
                foreach (ReplayChunkInfo chunk in info.Chunks)
                {
                    writer.Write(chunk.Offset);
                    writer.Write(chunk.Length);
                    writer.Write(chunk.StartTime);
                    writer.Write(chunk.EndTime);
                    writer.Write(chunk.FrameCount);
                    writer.Write(chunk.Round);
                    writer.Write(chunk.Crc);
                }
                writer.Flush();
                return memory.ToArray();
            }
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > MaxStringBytes) throw new ArgumentException("Replay text is too long.");
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static void WriteVector(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x); writer.Write(value.y); writer.Write(value.z);
        }

        private static void WriteQuaternion(BinaryWriter writer, Quaternion value)
        {
            writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w);
        }

        /// <summary>
        /// Reads and checks an archive's header and footer. Frames are not read. Returns null with a
        /// player-readable reason when the file is damaged, too large, unfinished or from another version.
        /// </summary>
        public static ReplayArchiveInfo ReadInfo(string path, out string error)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                    return ReadInfo(stream, out error);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                               exception is ArgumentException || exception is NotSupportedException)
            {
                error = "Kayıt okunamadı: " + exception.Message;
                return null;
            }
        }

        public static ReplayArchiveInfo ReadInfo(Stream stream, out string error)
        {
            error = null;
            long length = stream.Length;
            if (length > MaxFileBytes) return Fail("Kayıt dosyası izin verilen boyuttan büyük.", out error);
            if (length < HeaderBytes) return Fail("Kayıt dosyası eksik.", out error);
            var header = new byte[HeaderBytes];
            stream.Position = 0;
            if (!ReadExactly(stream, header, HeaderBytes)) return Fail("Kayıt başlığı eksik.", out error);
            for (int i = 0; i < Magic.Length; i++)
                if (header[i] != Magic[i]) return Fail("Bu bir tekrar kaydı değil.", out error);
            int offset = Magic.Length;
            int schema = ReadInt32(header, ref offset);
            if (schema > SchemaVersion) return Fail($"Uyumsuz: Kayıt daha yeni bir sürümden (biçim {schema}); bu sürüm açamaz.", out error);
            if (schema != SchemaVersion) return Fail($"Uyumsuz: Bilinmeyen kayıt biçimi {schema}.", out error);
            long footerOffset = (uint)ReadInt32(header, ref offset) | (long)ReadInt32(header, ref offset) << 32;
            int footerLength = ReadInt32(header, ref offset);
            uint footerCrc = unchecked((uint)ReadInt32(header, ref offset));
            if (footerOffset == 0) return Fail("Kayıt tamamlanmamış.", out error);
            if (footerOffset < HeaderBytes || footerLength <= 0 || footerLength > MaxFooterBytes || footerOffset + footerLength != length)
                return Fail("Kayıt dizini bozuk.", out error);

            var footer = new byte[footerLength];
            stream.Position = footerOffset;
            if (!ReadExactly(stream, footer, footerLength)) return Fail("Kayıt dizini eksik.", out error);
            if (Crc32(footer, 0, footerLength) != footerCrc) return Fail("Kayıt dizini bozuk (sağlama).", out error);

            ReplayArchiveInfo info;
            try
            {
                info = DecodeFooter(footer, footerOffset, out error);
            }
            catch (Exception exception) when (exception is EndOfStreamException || exception is ArgumentException ||
                                               exception is InvalidDataException || exception is DecoderFallbackException)
            {
                return Fail("Kayıt dizini okunamadı: " + exception.Message, out error);
            }
            if (info == null) return null;
            info.Schema = schema;
            info.FileBytes = length;
            return info;
        }

        private static ReplayArchiveInfo DecodeFooter(byte[] footer, long footerOffset, out string error)
        {
            error = null;
            using (var memory = new MemoryStream(footer, false))
            using (var reader = new BinaryReader(memory, Encoding.UTF8))
            {
                var info = new ReplayArchiveInfo();
                info.ContentRevision = reader.ReadInt32();
                if (!SupportsRevision(info.ContentRevision)) return Fail($"Uyumsuz: içerik revizyonu {info.ContentRevision}.", out error);
                info.Id = ReadString(reader);
                info.EncounterId = ReadString(reader);
                info.CreatedUtc = ReadString(reader);
                info.Title = ReadString(reader);
                info.PlayerName = ReadString(reader);
                info.OpponentName = ReadString(reader);
                info.ArenaName = ReadString(reader);
                info.LayoutMaterial = ReadString(reader);
                info.RulesMode = reader.ReadInt32();
                info.TimeLimit = reader.ReadSingle();
                info.Outcome = reader.ReadInt32();
                info.PlayerWins = reader.ReadInt32();
                info.OpponentWins = reader.ReadInt32();
                info.DrawnRounds = reader.ReadInt32();
                info.Duration = reader.ReadSingle();
                info.SampleRate = reader.ReadInt32();
                info.Completeness = (ReplayCompleteness)reader.ReadInt32();
                info.Warning = ReadString(reader);

                if (!IsValidId(info.Id)) return Fail("Kayıt kimliği geçersiz.", out error);
                if (info.EncounterId != null && (info.EncounterId.Length == 0 || info.EncounterId.Length > 64)) return Fail("Karşılaşma kimliği geçersiz.", out error);
                if (!IsFinite(info.Duration) || info.Duration < 0f) return Fail("Kayıt süresi geçersiz.", out error);
                if (!IsFinite(info.TimeLimit) || info.TimeLimit < 0f) return Fail("Raund süresi geçersiz.", out error);
                if (info.SampleRate < 1 || info.SampleRate > 120) return Fail("Örnekleme hızı geçersiz.", out error);
                if (info.PlayerWins < 0 || info.OpponentWins < 0 || info.DrawnRounds < 0) return Fail("Skor geçersiz.", out error);
                if (((int)info.Completeness & ~63) != 0) return Fail("Kayıt tamlık bilgisi geçersiz.", out error);

                int trackCount = reader.ReadInt32();
                if (trackCount < 0 || trackCount > MaxTracks) return Fail("İz sayısı sınır dışında.", out error);
                var keys = new HashSet<string>();
                for (int i = 0; i < trackCount; i++)
                {
                    var track = new ReplayTrack
                    {
                        Key = ReadString(reader),
                        Kind = (ReplayTrackKind)reader.ReadByte(),
                        Robot = reader.ReadSByte(),
                        Wear = reader.ReadBoolean(),
                        MeshSignature = reader.ReadInt32(),
                        Scale = ReadVector(reader),
                        StaticPosition = ReadVector(reader),
                        StaticRotation = ReadQuaternion(reader),
                        StaticVisible = reader.ReadBoolean(),
                        HasColor = reader.ReadBoolean(),
                        Color = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                    };
                    if (!IsValidKey(track.Key) || !keys.Add(track.Key)) return Fail("İz anahtarı geçersiz veya yinelenmiş.", out error);
                    if (!Enum.IsDefined(typeof(ReplayTrackKind), track.Kind) || track.Robot < -1 || track.Robot > 1 || track.MeshSignature < 0)
                        return Fail("İz türü geçersiz.", out error);
                    bool layoutKey = track.Key.StartsWith(LayoutPrefix, StringComparison.Ordinal);
                    bool layoutKind = track.IsLayout;
                    bool markerKey = layoutKey && IsHazardKey(track.Key);
                    if ((track.Kind == ReplayTrackKind.HazardMarker) != markerKey ||
                        (markerKey && (info.ContentRevision == 1 || track.Wear || track.Robot != -1)))
                        return Fail("Tehlike izi içerik revizyonuyla uyuşmuyor.", out error);
                    if (layoutKey != layoutKind || (track.Kind == ReplayTrackKind.RobotDebris) != track.Key.StartsWith(DebrisPrefix, StringComparison.Ordinal))
                        return Fail("İz anahtarı türüyle uyuşmuyor.", out error);
                    if (!IsFinite(track.Scale) || !IsFinite(track.StaticPosition) || !IsFinite(new Vector4(track.StaticRotation.x, track.StaticRotation.y, track.StaticRotation.z, track.StaticRotation.w)) ||
                        !IsFinite(new Vector4(track.Color.r, track.Color.g, track.Color.b, track.Color.a)) || Mathf.Abs(track.Scale.x) > 1000f ||
                        Mathf.Abs(track.Scale.y) > 1000f || Mathf.Abs(track.Scale.z) > 1000f)
                        return Fail("İz değerleri geçersiz.", out error);
                    info.Tracks.Add(track);
                }
                info.AssignColumns();

                int roundCount = reader.ReadInt32();
                if (roundCount < 0 || roundCount > MaxRounds) return Fail("Raund sayısı sınır dışında.", out error);
                float previousEnd = 0f;
                for (int i = 0; i < roundCount; i++)
                {
                    var round = new ReplayRound
                    {
                        MatchId = reader.ReadInt32(), RoundNumber = reader.ReadInt32(),
                        StartTime = reader.ReadSingle(), EndTime = reader.ReadSingle(),
                        Outcome = reader.ReadInt32(), Reason = reader.ReadInt32(),
                    };
                    if (!IsFinite(round.StartTime) || !IsFinite(round.EndTime) || round.StartTime < previousEnd ||
                        round.EndTime < round.StartTime || round.EndTime > info.Duration + 0.001f || round.RoundNumber < 1 ||
                        round.Outcome < 0 || round.Outcome > 3 || round.Reason < 0 || round.Reason > 1)
                        return Fail("Raund sınırları geçersiz.", out error);
                    previousEnd = round.EndTime;
                    info.Rounds.Add(round);
                }

                int eventCount = reader.ReadInt32();
                if (eventCount < 0 || eventCount > MaxEvents + ReservedBoundaryEvents) return Fail("Olay sayısı sınır dışında.", out error);
                for (int i = 0; i < eventCount; i++)
                {
                    var item = new ReplayEvent
                    {
                        Time = reader.ReadSingle(), Sequence = reader.ReadInt32(), Type = (ReplayEventType)reader.ReadByte(),
                        Robot = reader.ReadSByte(), Round = reader.ReadInt32(), Track = reader.ReadInt32(),
                        Point = ReadVector(reader), Normal = ReadVector(reader), Amount = reader.ReadSingle(), Outcome = reader.ReadInt32(),
                    };
                    if (info.ContentRevision >= 2 && item.Type == ReplayEventType.Impact)
                    {
                        item.Source = (ReplayImpactSource)reader.ReadByte();
                        if (item.Source > ReplayImpactSource.RobotContact) return Fail("Darbe kaynağı geçersiz.", out error);
                    }
                    if (item.Type == ReplayEventType.HazardPhaseChanged)
                    {
                        if (info.ContentRevision < 2 || item.Track < 0 || item.Track >= trackCount ||
                            info.Tracks[item.Track].Kind != ReplayTrackKind.HazardMarker) return Fail("Tehlike olayı geçersiz.", out error);
                        item.Phase = (HazardPhase)reader.ReadByte();
                        if (item.Phase > HazardPhase.Recovery) return Fail("Tehlike evresi geçersiz.", out error);
                    }
                    if (!IsFinite(item.Time) || item.Time < 0f || item.Time > info.Duration + 0.001f || !Enum.IsDefined(typeof(ReplayEventType), item.Type) ||
                        item.Robot < -1 || item.Robot > 1 || item.Round < 0 || item.Round >= Math.Max(1, roundCount) ||
                        item.Track < -1 || item.Track >= trackCount || !IsFinite(item.Point) || !IsFinite(item.Normal) || !IsFinite(item.Amount))
                        return Fail("Olay kaydı geçersiz.", out error);
                    if (i > 0 && CompareEvents(info.Events[i - 1], item) > 0) return Fail("Olay sırası bozuk.", out error);
                    info.Events.Add(item);
                }

                int cellCount = reader.ReadInt32();
                if (cellCount < 0 || cellCount > MaxCells) return Fail("Arena hücre sayısı sınır dışında.", out error);
                for (int i = 0; i < cellCount; i++)
                {
                    var cell = new ReplayCell
                    {
                        X = reader.ReadInt32(), Y = reader.ReadInt32(), Content = ReadString(reader),
                        Spawn = reader.ReadInt32(), Wall = reader.ReadInt32(), Direction = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                    };
                    if (cell.X < 0 || cell.X > 7 || cell.Y < 0 || cell.Y > 7 || string.IsNullOrEmpty(cell.Content) ||
                        !IsFinite(cell.Direction.x) || !IsFinite(cell.Direction.y))
                        return Fail("Arena hücresi geçersiz.", out error);
                    info.Cells.Add(cell);
                }

                int chunkCount = reader.ReadInt32();
                if (chunkCount < 0 || chunkCount > MaxChunks) return Fail("Parça sayısı sınır dışında.", out error);
                long expectedOffset = HeaderBytes;
                float previousChunkEnd = float.NegativeInfinity;
                for (int i = 0; i < chunkCount; i++)
                {
                    var chunk = new ReplayChunkInfo
                    {
                        Offset = reader.ReadInt64(), Length = reader.ReadInt32(), StartTime = reader.ReadSingle(), EndTime = reader.ReadSingle(),
                        FrameCount = reader.ReadInt32(), Round = reader.ReadInt32(), Crc = reader.ReadUInt32(),
                    };
                    if (chunk.Offset != expectedOffset || chunk.FrameCount < 1 || chunk.FrameCount > FramesPerChunk ||
                        chunk.Length != ChunkBytes(chunk.FrameCount, info.DynamicTrackCount, info.WearTrackCount, info.HazardTrackCount) ||
                        chunk.Offset + chunk.Length > footerOffset || !IsFinite(chunk.StartTime) || !IsFinite(chunk.EndTime) ||
                        chunk.StartTime < 0f || chunk.EndTime < chunk.StartTime || chunk.StartTime <= previousChunkEnd ||
                        chunk.EndTime > info.Duration + 0.001f || chunk.Round < 0 || chunk.Round >= Math.Max(1, roundCount))
                        return Fail("Kayıt parça dizini bozuk.", out error);
                    expectedOffset += chunk.Length;
                    previousChunkEnd = chunk.EndTime;
                    info.Chunks.Add(chunk);
                }
                if (expectedOffset != footerOffset) return Fail("Kayıt parçaları dizinle uyuşmuyor.", out error);
                if (memory.Position != memory.Length) return Fail("Kayıt dizininde fazladan veri var.", out error);
                return info;
            }
        }

        /// <summary>Events are ordered by time, then by arrival.</summary>
        public static int CompareEvents(ReplayEvent a, ReplayEvent b)
        {
            int byTime = a.Time.CompareTo(b.Time);
            return byTime != 0 ? byTime : a.Sequence.CompareTo(b.Sequence);
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == -1) return null;
            if (length < 0 || length > MaxStringBytes) throw new InvalidDataException("Metin uzunluğu sınır dışında.");
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return new UTF8Encoding(false, true).GetString(bytes);
        }

        private static Vector3 ReadVector(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        private static Quaternion ReadQuaternion(BinaryReader reader) => new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        public static bool ReadExactly(Stream stream, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int step = stream.Read(buffer, read, count - read);
                if (step <= 0) return false;
                read += step;
            }
            return true;
        }

        /// <summary>Archive ids are 32 lowercase hex digits (a GUID), so an id can never be a path.</summary>
        public static bool IsValidId(string id)
        {
            if (id == null || id.Length != 32) return false;
            foreach (char c in id)
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
            return true;
        }

        private static ReplayArchiveInfo Fail(string message, out string error)
        {
            error = message;
            return null;
        }

        // ---- Text for the archive list ----------------------------------------------------------------

        public static string DescribeCompleteness(ReplayArchiveInfo info)
        {
            if (info.IsComplete) return "Tam";
            var parts = new List<string>();
            if ((info.Completeness & ReplayCompleteness.Abandoned) != 0) parts.Add("seri yarıda bırakıldı");
            if ((info.Completeness & ReplayCompleteness.Interrupted) != 0) parts.Add("kayıt yarıda kesildi");
            if ((info.Completeness & ReplayCompleteness.TrackLimit) != 0) parts.Add("iz sınırı");
            if ((info.Completeness & ReplayCompleteness.FileLimit) != 0) parts.Add("dosya boyutu sınırı");
            if ((info.Completeness & ReplayCompleteness.EventLimit) != 0) parts.Add("olay sınırı");
            return "Eksik: " + string.Join(", ", parts);
        }

        public static string DescribeEvent(ReplayEvent item, ReplayArchiveInfo info)
        {
            string robot = item.Robot == MatchReplayRecorder.PlayerRobot ? "Oyuncu" : item.Robot == MatchReplayRecorder.OpponentRobot ? "Rakip" : "";
            int roundNumber = item.Round < info.Rounds.Count ? info.Rounds[item.Round].RoundNumber : item.Round + 1;
            switch (item.Type)
            {
                case ReplayEventType.RoundStart: return $"Raund {roundNumber} başladı";
                case ReplayEventType.Impact:
                    string source = item.Source == ReplayImpactSource.Weapon ? "Silah" :
                        item.Source == ReplayImpactSource.RobotContact ? "Robot kaynaklı" : "Kaynak belirsiz";
                    return $"{source} · {robot} -{item.Amount:0.#}";
                case ReplayEventType.HazardPhaseChanged:
                    string phase = item.Phase == HazardPhase.Warning ? "Uyarı" : item.Phase == HazardPhase.Active ? "Etkin" :
                        item.Phase == HazardPhase.Recovery ? "Toparlanma" : "Bekleme";
                    string markerKey = info.Tracks[item.Track].Key;
                    string hazard = LastSegment(markerKey.Substring(0, markerKey.LastIndexOf('/')));
                    return $"{hazard} · {phase}";
                case ReplayEventType.PartDetached:
                    string part = item.Track >= 0 && item.Track < info.Tracks.Count ? LastSegment(info.Tracks[item.Track].Key) : "parça";
                    return $"Parça koptu: {robot} {part}";
                case ReplayEventType.RoundEnd: return $"Raund {roundNumber} bitti: {DescribeOutcome(item.Outcome)}";
                case ReplayEventType.EncounterEnd: return $"Karşılaşma bitti: {DescribeOutcome(item.Outcome)}";
                default: return item.Type.ToString();
            }
        }

        public static string DescribeOutcome(int outcome) =>
            outcome == 1 ? "Oyuncu kazandı" : outcome == 2 ? "Rakip kazandı" : outcome == 3 ? "Berabere" : "Sonuçsuz";

        public static bool IsHazardKey(string key) => key.EndsWith("/Pulse floor marker", StringComparison.Ordinal) ||
            key.EndsWith("/Sweep floor marker", StringComparison.Ordinal);

        public static string LastSegment(string key)
        {
            int slash = key.LastIndexOf('/');
            return slash >= 0 ? key.Substring(slash + 1) : key;
        }
    }
}
