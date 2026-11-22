using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RobotSavaslari.Presentation.Tests
{
    /// <summary>
    /// The replay archive file and folder without a scene: round trip, chunk and round boundaries,
    /// random seek, rejection of damaged, future, oversized and hostile files, partial writes, quota and
    /// reopening. Every test works in its own temporary folder.
    /// </summary>
    public sealed class MatchReplayArchiveTests
    {
        private string _directory;
        private MatchReplayArchiveStore _store;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "RobotSavaslariArchiveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _store = new MatchReplayArchiveStore(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
            catch (IOException) { }
        }

        // ---- Synthetic recordings -----------------------------------------------------------------

        internal static ReplayArchiveInfo NewInfo(int robotTracks = 3, bool layout = true)
        {
            var info = new ReplayArchiveInfo
            {
                CreatedUtc = DateTime.UtcNow.ToString("O"), Title = "Test", PlayerName = "Kirmizi Robot", OpponentName = "Mavi Robot",
                ArenaName = "Open", LayoutMaterial = "Arena", RulesMode = 1,
            };
            for (int i = 0; i < robotTracks; i++)
                info.Tracks.Add(new ReplayTrack
                {
                    Key = $"robot/Kirmizi Robot/Robot Model/Part {i}" + (i == 1 ? "/Damage Wear" : ""),
                    Kind = ReplayTrackKind.RobotPart, Robot = i % 2, Wear = i == 1, MeshSignature = 24,
                });
            info.Tracks.Add(new ReplayTrack { Key = "debris/Kirmizi Robot/Robot Model/Part 0", Kind = ReplayTrackKind.RobotDebris, Robot = 0, MeshSignature = 24 });
            if (layout)
            {
                info.Tracks.Add(new ReplayTrack
                {
                    Key = "layout/0/Arena Obstacle (3, 3)", Kind = ReplayTrackKind.LayoutStatic, Robot = -1, MeshSignature = 24,
                    StaticPosition = new Vector3(1f, 0.65f, 2f), StaticVisible = true, Scale = new Vector3(1.4f, 1.3f, 1.4f),
                });
                info.Tracks.Add(new ReplayTrack
                {
                    Key = "layout/1/Arena Warning Barrier (6, 6)/Moving Barrier", Kind = ReplayTrackKind.LayoutDynamic, Robot = -1, MeshSignature = 24,
                    HasColor = true, Color = new Color(0.1f, 0.2f, 0.3f, 1f),
                });
                info.Cells.Add(new ReplayCell { X = 6, Y = 6, Content = "Hazard", Direction = Vector2.right });
            }
            return info;
        }

        /// <summary>Position of a column at a frame: easy to predict when checking samples.</summary>
        internal static Vector3 Expected(int frame, int column) => new Vector3(frame * 0.1f, column, -frame * 0.05f);

        internal static bool ExpectedVisible(int frame, int column) => !(column == 0 && frame >= 30);

        /// <summary>Writes rounds of frames at the archive sample rate; round r starts after the previous one.</summary>
        internal static MatchReplayArchiveWriter WriteFrames(MatchReplayArchiveStore store, ReplayArchiveInfo info, params int[] framesPerRound)
        {
            MatchReplayArchiveWriter writer = store.CreateWriter(info);
            var poses = new MatchReplayClip.Pose[info.DynamicTrackCount];
            var wear = new byte[info.WearTrackCount];
            int frame = 0;
            float time = 0f;
            for (int round = 0; round < framesPerRound.Length; round++)
            {
                float start = time;
                info.Rounds.Add(new ReplayRound { MatchId = round + 1, RoundNumber = round + 1, StartTime = start, EndTime = start, Outcome = 0 });
                writer.AddEvent(new ReplayEvent { Time = start, Type = ReplayEventType.RoundStart, Robot = -1, Round = round, Track = -1 });
                for (int i = 0; i < framesPerRound[round]; i++, frame++)
                {
                    time = frame / (float)MatchReplayArchive.SampleRate;
                    for (int c = 0; c < poses.Length; c++)
                        poses[c] = new MatchReplayClip.Pose(Expected(frame, c), Quaternion.Euler(0f, frame, 0f), ExpectedVisible(frame, c));
                    for (int c = 0; c < wear.Length; c++) wear[c] = (byte)Mathf.Min(255, frame);
                    if (writer.NeedsMarks)
                        for (int m = 0; m < writer.Marks.Length; m++) writer.Marks[m] = new Vector4(frame, m, 0f, 0.5f);
                    Assert.That(writer.WriteFrame(time, round, poses, wear), Is.True, "frame " + frame);
                }
                ReplayRound written = info.Rounds[round];
                written.EndTime = time;
                written.Outcome = 1;
                info.Rounds[round] = written;
                writer.AddEvent(new ReplayEvent { Time = time, Type = ReplayEventType.RoundEnd, Robot = -1, Round = round, Track = -1, Outcome = 1 });
                time += 1f / MatchReplayArchive.SampleRate;
            }
            info.Duration = writer.LastTime;
            return writer;
        }

        private MatchReplayArchiveStore.Entry CommitOne(ReplayArchiveInfo info = null, params int[] rounds)
        {
            info = info ?? NewInfo();
            MatchReplayArchiveWriter writer = WriteFrames(_store, info, rounds.Length == 0 ? new[] { 45 } : rounds);
            Assert.That(_store.Commit(writer, out string message), Is.EqualTo(MatchReplayArchiveStore.CommitResult.Archived), message);
            return _store.List().Find(e => e.Id == info.Id);
        }

        // ---- Round trip and sampling ----------------------------------------------------------------

        [Test]
        public void RoundTripKeepsMetadataTracksRoundsEventsAndCells()
        {
            ReplayArchiveInfo info = NewInfo();
            info.EncounterId = "abc";
            MatchReplayArchiveStore.Entry entry = CommitOne(info, 25, 20);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.Error, Is.Null);
            ReplayArchiveInfo read = entry.Info;
            Assert.That(read.Id, Is.EqualTo(info.Id));
            Assert.That(read.Schema, Is.EqualTo(MatchReplayArchive.SchemaVersion));
            Assert.That(read.ContentRevision, Is.EqualTo(MatchReplayArchive.ContentRevision));
            Assert.That(read.EncounterId, Is.EqualTo("abc"));
            Assert.That(read.Title, Is.EqualTo("Test"));
            Assert.That(read.LayoutMaterial, Is.EqualTo("Arena"));
            Assert.That(read.SampleRate, Is.EqualTo(20));
            Assert.That(read.Duration, Is.EqualTo(44 / 20f).Within(1e-5f));
            Assert.That(read.Tracks.Count, Is.EqualTo(info.Tracks.Count));
            for (int i = 0; i < info.Tracks.Count; i++)
            {
                Assert.That(read.Tracks[i].Key, Is.EqualTo(info.Tracks[i].Key));
                Assert.That(read.Tracks[i].Kind, Is.EqualTo(info.Tracks[i].Kind));
                Assert.That(read.Tracks[i].Wear, Is.EqualTo(info.Tracks[i].Wear));
                Assert.That(read.Tracks[i].Scale, Is.EqualTo(info.Tracks[i].Scale));
            }
            Assert.That(read.Tracks[5].Color, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 1f)));
            Assert.That(read.Rounds.Count, Is.EqualTo(2));
            Assert.That(read.Rounds[1].StartTime, Is.GreaterThan(read.Rounds[0].EndTime), "round boundaries do not overlap");
            Assert.That(read.Events.Count, Is.EqualTo(4));
            Assert.That(read.Cells.Count, Is.EqualTo(1));
            Assert.That(read.Cells[0].Content, Is.EqualTo("Hazard"));
            Assert.That(read.Chunks.Count, Is.EqualTo(3), "25 frames = 20 + 5, then 20: rounds never share a chunk");
            Assert.That(read.IsComplete, Is.True);
        }

        [Test]
        public void SamplesReturnTheWrittenFramesAndWear()
        {
            MatchReplayArchiveStore.Entry entry = CommitOne(null, 45);
            using (MatchReplayArchiveReader reader = MatchReplayArchiveReader.Open(entry.Path, out string error))
            {
                Assert.That(reader, Is.Not.Null, error);
                Assert.That(reader.VerifyAllChunks(out error), Is.True, error);
                int columns = reader.Info.DynamicTrackCount;
                var poses = new MatchReplayClip.Pose[columns];
                var wear = new byte[reader.Info.WearTrackCount];
                var marks = new Vector4[reader.Info.WearTrackCount * 4];
                for (int frame = 0; frame < 45; frame++)
                {
                    Assert.That(reader.Sample(frame / 20f, poses, wear, marks, out error), Is.True, error);
                    for (int c = 0; c < columns; c++)
                    {
                        Assert.That(Vector3.Distance(poses[c].Position, Expected(frame, c)), Is.LessThan(1e-4f), $"frame {frame} column {c}");
                        Assert.That(Quaternion.Angle(poses[c].Rotation, Quaternion.Euler(0f, frame, 0f)), Is.LessThan(0.05f));
                        Assert.That(poses[c].Visible, Is.EqualTo(ExpectedVisible(frame, c)));
                    }
                    Assert.That(wear[0], Is.EqualTo((byte)frame));
                    int chunkStart = frame / 20 * 20;
                    Assert.That(marks[0].x, Is.EqualTo(chunkStart), "marks come from the chunk the frame is in");
                }
            }
        }

        [Test]
        public void InterpolatesAcrossChunksButNeverAcrossRounds()
        {
            MatchReplayArchiveStore.Entry entry = CommitOne(null, 25, 10);
            using (MatchReplayArchiveReader reader = MatchReplayArchiveReader.Open(entry.Path, out string error))
            {
                var poses = new MatchReplayClip.Pose[reader.Info.DynamicTrackCount];
                // Between frame 19 (end of chunk 0) and 20 (start of chunk 1) of the same round: blended.
                Assert.That(reader.Sample(19.5f / 20f, poses, null, null, out error), Is.True, error);
                Assert.That(poses[1].Position.x, Is.EqualTo((Expected(19, 1).x + Expected(20, 1).x) * 0.5f).Within(1e-4f));
                // Between the last frame of round 1 (24) and the first of round 2 (25): held, not blended.
                float gap = (reader.Info.Rounds[0].EndTime + reader.Info.Rounds[1].StartTime) * 0.5f;
                Assert.That(reader.Sample(gap, poses, null, null, out error), Is.True, error);
                Assert.That(poses[1].Position.x, Is.EqualTo(Expected(24, 1).x).Within(1e-4f));
            }
        }

        [Test]
        public void RandomSeeksReadOnlyNearbyChunksInFixedMemory()
        {
            var info = NewInfo();
            MatchReplayArchiveWriter writer = WriteFrames(_store, info, 20 * 60 * 2);
            Assert.That(_store.Commit(writer, out string message), Is.EqualTo(MatchReplayArchiveStore.CommitResult.Archived), message);
            using (MatchReplayArchiveReader reader = MatchReplayArchiveReader.Open(_store.List()[0].Path, out string error))
            {
                long memory = reader.DecodedBytes;
                var poses = new MatchReplayClip.Pose[reader.Info.DynamicTrackCount];
                var random = new System.Random(45);
                for (int i = 0; i < 300; i++)
                {
                    int frame = random.Next(0, 2400);
                    int before = reader.ChunkReads;
                    Assert.That(reader.Sample(frame / 20f, poses, null, null, out error), Is.True, error);
                    Assert.That(reader.ChunkReads - before, Is.LessThanOrEqualTo(2), "a seek reads at most the chunk and its neighbour");
                    Assert.That(Vector3.Distance(poses[2].Position, Expected(frame, 2)), Is.LessThan(1e-3f), "frame " + frame);
                }
                Assert.That(reader.DecodedBytes, Is.EqualTo(memory), "memory does not grow with seeks");
            }
        }

        [Test]
        public void BreakingAChunkStoresFreshHitMarks()
        {
            var info = NewInfo();
            MatchReplayArchiveWriter writer = _store.CreateWriter(info);
            var poses = new MatchReplayClip.Pose[info.DynamicTrackCount];
            var wear = new byte[info.WearTrackCount];
            info.Rounds.Add(new ReplayRound { RoundNumber = 1 });
            for (int frame = 0; frame < 10; frame++)
            {
                if (frame == 6) writer.BreakChunk();
                if (writer.NeedsMarks) writer.Marks[0] = new Vector4(frame, 0f, 0f, 1f);
                Assert.That(writer.WriteFrame(frame / 20f, 0, poses, wear), Is.True);
            }
            info.Duration = writer.LastTime;
            Assert.That(_store.Commit(writer, out _), Is.EqualTo(MatchReplayArchiveStore.CommitResult.Archived));
            using (MatchReplayArchiveReader reader = MatchReplayArchiveReader.Open(_store.List()[0].Path, out string error))
            {
                var marks = new Vector4[reader.Info.WearTrackCount * 4];
                Assert.That(reader.Info.Chunks.Count, Is.EqualTo(2));
                reader.Sample(5 / 20f, poses, wear, marks, out error);
                Assert.That(marks[0].x, Is.EqualTo(0f));
                reader.Sample(7 / 20f, poses, wear, marks, out error);
                Assert.That(marks[0].x, Is.EqualTo(6f), "the frame after a hit shows the new marks");
            }
        }

        [Test]
        public void SameTimeEventsKeepArrivalOrderAndLateTimesAreClamped()
        {
            var info = NewInfo();
            MatchReplayArchiveWriter writer = WriteFrames(_store, info, 10);
            float t = writer.LastTime;
            writer.AddEvent(new ReplayEvent { Time = t, Type = ReplayEventType.Impact, Robot = 1, Amount = 5f, Track = -1 });
            writer.AddEvent(new ReplayEvent { Time = t, Type = ReplayEventType.PartDetached, Robot = 1, Track = 0 });
            writer.AddEvent(new ReplayEvent { Time = t - 1f, Type = ReplayEventType.EncounterEnd, Robot = -1, Outcome = 1, Track = -1 });
            Assert.That(_store.Commit(writer, out _), Is.EqualTo(MatchReplayArchiveStore.CommitResult.Archived));
            List<ReplayEvent> events = _store.List()[0].Info.Events;
            Assert.That(events.ConvertAll(e => e.Type), Is.EqualTo(new[]
            {
                ReplayEventType.RoundStart, ReplayEventType.RoundEnd, ReplayEventType.Impact, ReplayEventType.PartDetached, ReplayEventType.EncounterEnd,
            }));
            for (int i = 1; i < events.Count; i++) Assert.That(MatchReplayArchive.CompareEvents(events[i - 1], events[i]), Is.LessThan(0));
        }

        [Test]
        public void EventListIsBoundedAndBoundariesUseTheReserve()
        {
            var info = NewInfo();
            MatchReplayArchiveWriter writer = WriteFrames(_store, info, 5);
            int accepted = 0;
            for (int i = 0; i < MatchReplayArchive.MaxEvents + 50; i++)
                if (writer.AddEvent(new ReplayEvent { Time = writer.LastTime, Type = ReplayEventType.Impact, Robot = 0, Track = -1 })) accepted++;
            Assert.That(accepted, Is.EqualTo(MatchReplayArchive.MaxEvents));
            Assert.That(info.Completeness & ReplayCompleteness.EventLimit, Is.EqualTo(ReplayCompleteness.EventLimit));
            Assert.That(writer.AddEvent(new ReplayEvent { Time = writer.LastTime, Type = ReplayEventType.EncounterEnd, Robot = -1, Track = -1 }), Is.True,
                "the encounter end is still listed");
            Assert.That(_store.Commit(writer, out _), Is.EqualTo(MatchReplayArchiveStore.CommitResult.Archived));
            ReplayArchiveInfo read = _store.List()[0].Info;
            Assert.That(read.IsComplete, Is.False);
            StringAssert.Contains("olay sınırı", MatchReplayArchive.DescribeCompleteness(read));
        }

        [Test]
        public void FileSizeLimitStopsRecordingAndMarksTheArchiveIncomplete()
        {
            var info = new ReplayArchiveInfo { CreatedUtc = DateTime.UtcNow.ToString("O"), Title = "Limit" };
            for (int i = 0; i < MatchReplayArchive.MaxTracks; i++)
                info.Tracks.Add(new ReplayTrack { Key = "robot/R/Part " + i, Kind = ReplayTrackKind.RobotPart, Robot = 0 });
            MatchReplayArchiveWriter writer = _store.CreateWriter(info);
            info.Rounds.Add(new ReplayRound { RoundNumber = 1 });
            var poses = new MatchReplayClip.Pose[info.DynamicTrackCount];
            var wear = new byte[0];
            int frames = 0;
            while (writer.WriteFrame(frames / 20f, 0, poses, wear)) frames++;
            Assert.That(writer.IsFull, Is.True);
            Assert.That(writer.Error, Is.Null);
            info.Duration = writer.LastTime;
            Assert.That(_store.Commit(writer, out string message), Is.EqualTo(MatchReplayArchiveStore.CommitResult.Archived), message);
            MatchReplayArchiveStore.Entry entry = _store.List()[0];
            Assert.That(entry.Bytes, Is.LessThanOrEqualTo(MatchReplayArchive.MaxFileBytes));
            Assert.That(entry.Info.Completeness & ReplayCompleteness.FileLimit, Is.EqualTo(ReplayCompleteness.FileLimit));
            Debug.Log($"[ReplayArchive] file limit: {frames} frames of {MatchReplayArchive.MaxTracks} tracks = {frames / 20f / 60f:0.0} min at 20 Hz in {entry.Bytes / 1048576f:0.0} MiB");
            Assert.That(frames / 20f / 60f, Is.GreaterThan(15f), "fifteen minutes of 128 tracks fit one file");
        }

        // ---- Rejection -------------------------------------------------------------------------------

        private string WriteArchiveFile(Action<ReplayArchiveInfo> change = null)
        {
            ReplayArchiveInfo info = NewInfo();
            MatchReplayArchiveWriter writer = WriteFrames(_store, info, 30);
            Assert.That(writer.Finish(), Is.True);
            string path = Path.Combine(_directory, info.Id + ".rpl");
            File.Move(writer.TemporaryPath, path);
            if (change != null) RewriteFooter(path, change);
            return path;
        }

        /// <summary>
        /// Replaces a finished file's footer with a changed copy and a correct checksum, the way a buggy or
        /// hostile writer could, so validation (not the checksum) has to refuse it.
        /// </summary>
        private static void RewriteFooter(string path, Action<ReplayArchiveInfo> change)
        {
            ReplayArchiveInfo info = MatchReplayArchive.ReadInfo(path, out string error);
            Assert.That(info, Is.Not.Null, error);
            change(info);
            byte[] bytes = File.ReadAllBytes(path);
            int offset = 8;
            long footerOffset = (uint)MatchReplayArchive.ReadInt32(bytes, ref offset);
            byte[] footer = MatchReplayArchive.EncodeFooter(info);
            using (var stream = new FileStream(path, FileMode.Create))
            {
                MatchReplayArchive.WriteHeader(stream, footerOffset, footer.Length, MatchReplayArchive.Crc32(footer, 0, footer.Length));
                stream.Write(bytes, MatchReplayArchive.HeaderBytes, (int)footerOffset - MatchReplayArchive.HeaderBytes);
                stream.Write(footer, 0, footer.Length);
            }
        }

        private static void Rejects(string path, string expected)
        {
            Assert.That(MatchReplayArchive.ReadInfo(path, out string error), Is.Null);
            StringAssert.Contains(expected, error);
        }

        [Test]
        public void RejectsFutureAndUnknownVersions()
        {
            string path = WriteArchiveFile();
            byte[] bytes = File.ReadAllBytes(path);
            bytes[4] = 2;
            File.WriteAllBytes(path, bytes);
            Rejects(path, "daha yeni bir sürüm");
            bytes[4] = 0;
            File.WriteAllBytes(path, bytes);
            Rejects(path, "Bilinmeyen kayıt biçimi");
            bytes[0] = (byte)'X';
            File.WriteAllBytes(path, bytes);
            Rejects(path, "tekrar kaydı değil");
        }

        [Test]
        public void RejectsNonFiniteAndNegativeValues()
        {
            Rejects(WriteArchiveFile(info => info.Duration = float.NaN), "süresi");
            Rejects(WriteArchiveFile(info => info.Duration = -1f), "süresi");
            Rejects(WriteArchiveFile(info => info.TimeLimit = float.PositiveInfinity), "Raund süresi");
            Rejects(WriteArchiveFile(info => info.Tracks[0].Scale = new Vector3(float.NaN, 1f, 1f)), "İz değerleri");
            Rejects(WriteArchiveFile(info =>
            {
                ReplayRound round = info.Rounds[0];
                round.EndTime = round.StartTime - 1f;
                info.Rounds[0] = round;
            }), "Raund sınırları");
        }

        [Test]
        public void RejectsHostileKeysAndCounts()
        {
            Rejects(WriteArchiveFile(info => info.Tracks[0].Key = "robot/Kirmizi Robot/../../Windows"), "İz anahtarı");
            Rejects(WriteArchiveFile(info => info.Tracks[0].Key = "robot/Kirmizi Robot\\Robot Model"), "İz anahtarı");
            Rejects(WriteArchiveFile(info => info.Tracks[4].Key = "layout/0/Unknown Hazard"), "İz anahtarı");
            Rejects(WriteArchiveFile(info => info.Tracks[1].Key = info.Tracks[0].Key), "yinelenmiş");

            // A footer claiming more tracks than allowed, with a valid checksum: refused before allocating.
            string path = WriteArchiveFile();
            byte[] bytes = File.ReadAllBytes(path);
            int offset = 8;
            long footerOffset = (uint)MatchReplayArchive.ReadInt32(bytes, ref offset);
            offset = 16;
            int footerLength = MatchReplayArchive.ReadInt32(bytes, ref offset);
            byte[] footer = new byte[footerLength];
            Array.Copy(bytes, footerOffset, footer, 0, footerLength);
            int trackCountAt = FindTrackCount(footer);
            int at = trackCountAt;
            MatchReplayArchive.WriteInt32(footer, ref at, int.MaxValue);
            Array.Copy(footer, 0, bytes, footerOffset, footerLength);
            at = 20;
            MatchReplayArchive.WriteInt32(bytes, ref at, unchecked((int)MatchReplayArchive.Crc32(footer, 0, footerLength)));
            File.WriteAllBytes(path, bytes);
            Rejects(path, "İz sayısı");
        }

        // The track count follows the fixed-size metadata; find it by re-encoding a known footer layout.
        private static int FindTrackCount(byte[] footer)
        {
            using (var reader = new BinaryReader(new MemoryStream(footer)))
            {
                reader.ReadInt32();
                for (int i = 0; i < 8; i++)
                {
                    int length = reader.ReadInt32();
                    if (length > 0) reader.ReadBytes(length);
                }
                for (int i = 0; i < 9; i++) reader.ReadInt32();
                int warning = reader.ReadInt32();
                if (warning > 0) reader.ReadBytes(warning);
                return (int)reader.BaseStream.Position;
            }
        }

        [Test]
        public void RejectsOversizedUnfinishedAndCorruptFiles()
        {
            string oversized = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".rpl");
            using (var stream = new FileStream(oversized, FileMode.CreateNew)) stream.SetLength(MatchReplayArchive.MaxFileBytes + 1);
            Rejects(oversized, "boyuttan büyük");
            File.Delete(oversized);

            // A writer that never finished: the header still points at no footer.
            ReplayArchiveInfo info = NewInfo();
            MatchReplayArchiveWriter writer = WriteFrames(_store, info, 30);
            writer.BreakChunk();
            string copy = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".rpl");
            writer.Abort();
            File.WriteAllBytes(copy, new byte[MatchReplayArchive.HeaderBytes + 100]);
            byte[] header = File.ReadAllBytes(copy);
            header[0] = (byte)'R'; header[1] = (byte)'S'; header[2] = (byte)'R'; header[3] = (byte)'A'; header[4] = 1;
            File.WriteAllBytes(copy, header);
            Rejects(copy, "tamamlanmamış");

            string path = WriteArchiveFile();
            byte[] bytes = File.ReadAllBytes(path);
            bytes[bytes.Length - 3] ^= 0x5A;
            File.WriteAllBytes(path, bytes);
            Rejects(path, "sağlama");

            // A damaged frame inside a chunk passes the footer checks and fails chunk verification.
            path = WriteArchiveFile();
            bytes = File.ReadAllBytes(path);
            bytes[MatchReplayArchive.HeaderBytes + 200] ^= 0x33;
            File.WriteAllBytes(path, bytes);
            using (MatchReplayArchiveReader reader = MatchReplayArchiveReader.Open(path, out string error))
            {
                Assert.That(reader, Is.Not.Null, error);
                Assert.That(reader.VerifyAllChunks(out error), Is.False);
                StringAssert.Contains("bozuk", error);
                var poses = new MatchReplayClip.Pose[reader.Info.DynamicTrackCount];
                Assert.That(reader.Sample(0f, poses, null, null, out error), Is.False, "a damaged chunk is not played");
            }
        }

        [Test]
        public void StoreAcceptsOnlyItsOwnGuidFiles()
        {
            MatchReplayArchiveStore.Entry entry = CommitOne();
            string history = Path.Combine(_directory, "match-history.json");
            string foreign = Path.Combine(_directory, "notes.rpl");
            File.WriteAllText(history, "{}");
            File.WriteAllText(foreign, "x");
            string outside = Path.Combine(Path.GetDirectoryName(_directory), Guid.NewGuid().ToString("N") + ".rpl");
            File.WriteAllText(outside, "outside");
            try
            {
                Assert.That(_store.List().Count, Is.EqualTo(1), "only GUID-named archives are listed");
                foreach (string id in new[] { "../" + Path.GetFileNameWithoutExtension(outside), "..\\x", "notes", entry.Id.ToUpperInvariant(), "", null,
                             entry.Id + "/../" + entry.Id, "C:\\Windows\\win" })
                {
                    Assert.That(_store.Delete(id, out string error), Is.False, "id " + id);
                    Assert.That(error, Is.Not.Null);
                    Assert.That(_store.TryGetArchivePath(id, out _), Is.False);
                }
                Assert.That(File.Exists(history) && File.Exists(foreign) && File.Exists(outside), Is.True, "nothing else was touched");
                Assert.That(_store.Delete(entry.Id, out string deleteError), Is.True, deleteError);
                Assert.That(File.Exists(entry.Path), Is.False);
                Assert.That(File.Exists(history) && File.Exists(foreign), Is.True);
            }
            finally
            {
                File.Delete(outside);
            }
        }

        [Test]
        public void UnfinishedWritesAreNeverListedAndStaleOnesAreCleaned()
        {
            ReplayArchiveInfo info = NewInfo();
            MatchReplayArchiveWriter open = WriteFrames(_store, info, 30);
            Assert.That(File.Exists(open.TemporaryPath), Is.True);
            Assert.That(_store.List(), Is.Empty, "a file being written is not in the archive");

            string stale = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllBytes(stale, new byte[64]);
            Assert.That(_store.CleanupTemporaryFiles(), Is.EqualTo(1));
            Assert.That(File.Exists(stale), Is.False, "left over from a crash");
            Assert.That(File.Exists(open.TemporaryPath), Is.True, "the active writer's file is kept");
            open.Abort();
            Assert.That(File.Exists(open.TemporaryPath), Is.False);
            Assert.That(_store.List(), Is.Empty);
        }

        [Test]
        public void FullArchiveKeepsOldRecordingsAndQueuesTheNewOne()
        {
            var ids = new List<string>();
            for (int i = 0; i < MatchReplayArchiveStore.MaxRecordings; i++) ids.Add(CommitOne(null, 5).Id);
            Assert.That(_store.List().Count, Is.EqualTo(MatchReplayArchiveStore.MaxRecordings));

            ReplayArchiveInfo eleventh = NewInfo();
            MatchReplayArchiveWriter writer = WriteFrames(_store, eleventh, 5);
            Assert.That(_store.Commit(writer, out string message), Is.EqualTo(MatchReplayArchiveStore.CommitResult.Pending));
            StringAssert.Contains("Arşiv dolu", message);
            Assert.That(_store.Pending().Id, Is.EqualTo(eleventh.Id));

            ReplayArchiveInfo twelfth = NewInfo();
            writer = WriteFrames(_store, twelfth, 5);
            Assert.That(_store.Commit(writer, out message), Is.EqualTo(MatchReplayArchiveStore.CommitResult.Failed));
            StringAssert.Contains("bekleyen bir kayıt var", message);
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty, "no partial file is left");

            List<MatchReplayArchiveStore.Entry> listed = _store.List();
            Assert.That(listed.ConvertAll(e => e.Id), Is.EquivalentTo(ids), "no old recording was removed");
            Assert.That(_store.TryPromotePending(out _), Is.False, "still no room");

            Assert.That(_store.Delete(ids[3], out _), Is.True);
            Assert.That(_store.TryPromotePending(out message), Is.True, message);
            Assert.That(_store.Pending(), Is.Null);
            Assert.That(_store.List().Exists(e => e.Id == eleventh.Id), Is.True);
        }

        [Test]
        public void ArchivesReopenFromANewStoreInstance()
        {
            MatchReplayArchiveStore.Entry entry = CommitOne(null, 30);
            var reopened = new MatchReplayArchiveStore(_directory);
            List<MatchReplayArchiveStore.Entry> entries = reopened.List();
            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Info.Id, Is.EqualTo(entry.Id));
            Assert.That(entries[0].Bytes, Is.EqualTo(new FileInfo(entry.Path).Length));
        }

        [Test]
        public void KeysFollowTheWhitelist()
        {
            Assert.That(MatchReplayArchive.IsValidKey("robot/Kirmizi Robot/Robot Model/Wheel_FL/Damage Wear"), Is.True);
            Assert.That(MatchReplayArchive.IsValidKey("robot/Kırmızı Robot/Robot Model/Body#2"), Is.True);
            Assert.That(MatchReplayArchive.IsValidKey("debris/Mavi Robot/Robot Model/Spinner"), Is.True);
            Assert.That(MatchReplayArchive.IsValidKey("layout/3/Arena Conveyor (2, 1)/Conveyor Direction/Direction Mark"), Is.True);
            foreach (string key in new[] { null, "", "robot/", "robot/Name", "robot/Name//Part", "robot/../Part", "robot/Name/ Part", "robot/Name/Part\\x",
                         "robot/Name/Part:1", "file/Name/Part", "layout/x/Arena Obstacle", "layout/1/Pulse (2, 2)", "layout/1/Arena Obstacle/Hazard Cylinder",
                         "robot/Name/" + new string('a', 600) })
                Assert.That(MatchReplayArchive.IsValidKey(key), Is.False, "key " + key);
            Assert.That(MatchReplayArchive.KeyRobotName("debris/Mavi Robot/Robot Model/Spinner"), Is.EqualTo("Mavi Robot"));
            Assert.That(MatchReplayArchive.KeyRobotPath("debris/Mavi Robot/Robot Model/Spinner"), Is.EqualTo("Robot Model/Spinner"));
        }

        // ---- Format budget ---------------------------------------------------------------------------

        [Serializable]
        private struct JsonPose
        {
            public Vector3 position;
            public Quaternion rotation;
            public bool visible;
            public float wear;
        }

        [Serializable]
        private sealed class JsonFrame
        {
            public float time;
            public JsonPose[] poses;
        }

        [Test]
        public void BinaryFramesAreSmallerThanJson()
        {
            const int tracks = MatchReplayArchive.MaxTracks;
            var frame = new JsonFrame { time = 123.45f, poses = new JsonPose[tracks] };
            var random = new System.Random(3);
            for (int i = 0; i < tracks; i++)
                frame.poses[i] = new JsonPose
                {
                    position = new Vector3((float)random.NextDouble() * 10f, 0.8f + (float)random.NextDouble(), (float)random.NextDouble() * -10f),
                    rotation = Quaternion.Euler((float)random.NextDouble() * 360f, (float)random.NextDouble() * 360f, 0f),
                    visible = true, wear = (float)random.NextDouble(),
                };
            int json = System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(frame));
            int binary = MatchReplayArchive.FrameBytes(tracks);
            float jsonFifteenMinutes = json * 20f * 900f / 1048576f;
            float binaryFifteenMinutes = binary * 20f * 900f / 1048576f;
            Debug.Log($"[ReplayArchive] one {tracks}-track frame: binary {binary} B, JsonUtility {json} B; 15 min at 20 Hz: binary {binaryFifteenMinutes:0.0} MiB, JSON {jsonFifteenMinutes:0.0} MiB");
            Assert.That(binaryFifteenMinutes, Is.LessThan(MatchReplayArchive.MaxFileBytes / 1048576f));
            Assert.That(jsonFifteenMinutes, Is.GreaterThan(MatchReplayArchive.MaxFileBytes / 1048576f), "JSON would not fit the per-file budget");
        }

        [Test]
        [Category("Performance")]
        public void FifteenMinuteArchiveWritesOpensAndSeeksWithinBudget()
        {
            // 84 dynamic tracks: the measured MainArena count (two robots, their parts, wear and debris).
            var info = new ReplayArchiveInfo { CreatedUtc = DateTime.UtcNow.ToString("O"), Title = "15 min" };
            for (int i = 0; i < 84; i++)
                info.Tracks.Add(new ReplayTrack { Key = "robot/R/Part " + i + (i % 2 == 0 ? "" : "/Damage Wear"), Kind = ReplayTrackKind.RobotPart, Robot = i % 2, Wear = i % 2 == 1 });
            const int frames = 20 * 60 * 15;
            var write = new Stopwatch();
            write.Start();
            MatchReplayArchiveWriter writer = WriteFrames(_store, info, frames);
            write.Stop();
            Assert.That(_store.Commit(writer, out string message), Is.EqualTo(MatchReplayArchiveStore.CommitResult.Archived), message);
            MatchReplayArchiveStore.Entry entry = _store.List()[0];

            var open = Stopwatch.StartNew();
            using (MatchReplayArchiveReader reader = MatchReplayArchiveReader.Open(entry.Path, out string error))
            {
                Assert.That(reader.VerifyAllChunks(out error), Is.True, error);
                open.Stop();
                var poses = new MatchReplayClip.Pose[reader.Info.DynamicTrackCount];
                var wear = new byte[reader.Info.WearTrackCount];
                var marks = new Vector4[reader.Info.WearTrackCount * 4];
                var random = new System.Random(15);
                var seeks = new List<double>();
                for (int i = 0; i < 200; i++)
                {
                    float time = (float)random.NextDouble() * reader.EndTime;
                    long start = Stopwatch.GetTimestamp();
                    Assert.That(reader.Sample(time, poses, wear, marks, out error), Is.True, error);
                    seeks.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                }
                seeks.Sort();
                double p50 = seeks[99], p95 = seeks[189], max = seeks[199];
                Debug.Log($"[ReplayArchive] 15 min, 84 tracks, {frames} frames: file {entry.Bytes / 1048576f:0.0} MiB, " +
                          $"write {write.Elapsed.TotalMilliseconds / frames:0.000} ms/frame avg, open+verify {open.Elapsed.TotalMilliseconds:0} ms, " +
                          $"seek p50 {p50:0.000} ms p95 {p95:0.000} ms max {max:0.000} ms, decoded memory {reader.DecodedBytes / 1024f:0} KiB");
                Assert.That(p95, Is.LessThanOrEqualTo(200.0), "seek p95");
                Assert.That(reader.DecodedBytes, Is.LessThan(64L * 1024 * 1024), "playback memory");
            }
            Assert.That(entry.Bytes, Is.LessThan(MatchReplayArchive.MaxFileBytes));
        }
    }
}
