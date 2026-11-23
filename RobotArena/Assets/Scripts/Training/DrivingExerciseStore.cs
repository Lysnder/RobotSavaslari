using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RobotSavaslari.Training
{
    public sealed class DrivingExerciseStore
    {
        public const int MaxSamples = 6001;
        private const int MaxBytes = 16 * 1024 * 1024;
        private const int MaxKeys = 20;
        private const int MaxHistory = 20;

        [Serializable]
        private sealed class Data
        {
            public int version;
            public List<Record> entries;
            public bool hasLegacy;
            public GhostLapRecording legacy;
        }

        [Serializable]
        public sealed class Record
        {
            public string key;
            public List<DrivingExerciseResult> history = new List<DrivingExerciseResult>();
            public DrivingExerciseResult best;
            public bool hasGhost;
            public GhostLapRecording ghost;
        }

        private readonly string path;
        private Data data;

        public DrivingExerciseStore(string path) => this.path = path;
        public string Warning { get; private set; }

        public Record Find(string key)
        {
            if (data == null) Load();
            return data?.entries.Find(entry => entry.key == key);
        }

        public void Load()
        {
            Warning = null;
            try
            {
                if (!File.Exists(path))
                {
                    data = new Data { version = 1, entries = new List<Record>() };
                    return;
                }

                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                if (input.Length == 0 || input.Length > MaxBytes) throw new InvalidDataException();
                using var reader = new StreamReader(input);
                var loaded = JsonUtility.FromJson<Data>(reader.ReadToEnd());
                if (!Valid(loaded)) throw new InvalidDataException();
                foreach (var record in loaded.entries)
                    if (!record.hasGhost) record.ghost = null;
                if (!loaded.hasLegacy) loaded.legacy = null;
                data = loaded;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                data = null;
                Warning = "Alıştırma kaydı okunamadı (biçim veya sürüm); mevcut dosya korundu.";
            }
        }

        public GhostLapRecording MigrateLegacy(string courseId, Func<string, GhostLapRecording> loadLegacy)
        {
            if (data == null) Load();
            if (data == null) return null;
            if (GhostValid(data.legacy, courseId)) return data.legacy;
            if (data.legacy != null) return null;

            var legacy = loadLegacy(courseId);
            if (!GhostValid(legacy, courseId)) return null;
            Write(current =>
            {
                if (current.legacy != null) return false;
                current.legacy = legacy;
                current.hasLegacy = true;
                return true;
            });
            return GhostValid(data?.legacy, courseId) ? data.legacy : null;
        }

        public bool Save(string key, DrivingExerciseResult result, GhostLapRecording recording)
        {
            if (!KeyValid(key) || !ResultValid(result)) return false;
            return Write(current =>
            {
                Record record = current.entries.Find(entry => entry.key == key);
                if (record == null)
                {
                    if (current.entries.Count >= MaxKeys)
                    {
                        Warning = "Alıştırma kayıt sınırı dolu (20 parkur/ayar). Önceki kayıtlar korundu.";
                        return false;
                    }

                    record = new Record { key = key };
                    current.entries.Add(record);
                }

                record.history.Insert(0, result);
                if (record.history.Count > MaxHistory) record.history.RemoveAt(MaxHistory);
                if (result.success && GhostValid(recording, key) && recording.lapTime == result.seconds &&
                    (record.ghost == null || IsBetter(result, record.best)))
                {
                    record.best = result;
                    record.ghost = recording;
                    record.hasGhost = true;
                }

                return true;
            });
        }

        private static bool IsBetter(DrivingExerciseResult candidate, DrivingExerciseResult best)
        {
            if (candidate.success != best.success) return candidate.success;
            if (candidate.Faults != best.Faults) return candidate.Faults < best.Faults;
            if (candidate.overshoot != best.overshoot) return candidate.overshoot < best.overshoot;
            return candidate.seconds < best.seconds;
        }

        private bool Write(Func<Data, bool> change)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                using var writeLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.Write,
                    FileShare.None, 1, FileOptions.DeleteOnClose);
                Load();
                if (data == null || !change(data)) return false;
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                {
                    using var writer = new StreamWriter(output, new System.Text.UTF8Encoding(false), 4096, true);
                    writer.Write(JsonUtility.ToJson(data));
                    writer.Flush();
                    if (output.Length > MaxBytes) throw new IOException("Kayıt boyut sınırı.");
                    output.Flush(true);
                }

                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                Load();
                Warning = "Alıştırma kaydı yazılamadı; önceki kayıtlar korundu.";
                return false;
            }
            finally
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static bool Valid(Data candidate)
        {
            if (candidate == null || candidate.version != 1 || candidate.entries == null || candidate.entries.Count > MaxKeys)
                return false;
            if (candidate.hasLegacy && !GhostValid(candidate.legacy, candidate.legacy?.courseId)) return false;
            var keys = new HashSet<string>();
            foreach (var record in candidate.entries)
            {
                if (record == null || !KeyValid(record.key) || !keys.Add(record.key) || record.history == null ||
                    record.history.Count > MaxHistory || record.history.Exists(result => !ResultValid(result))) return false;
                if (record.hasGhost && (!GhostValid(record.ghost, record.key) || !record.best.success ||
                    !ResultValid(record.best) || record.best.seconds != record.ghost.lapTime)) return false;
            }

            return true;
        }

        private static bool KeyValid(string key) => !string.IsNullOrEmpty(key) && key.Length <= 160;
        private static bool NonNegative(float value) => DrivingExerciseDefinition.Finite(value) && value >= 0f;
        private static bool ResultValid(DrivingExerciseResult result) =>
            NonNegative(result.seconds) && result.seconds <= DrivingExerciseSession.TimeLimit &&
            result.gateErrors >= 0 && result.contacts >= 0 && result.directionErrors >= 0 &&
            (!result.success || result.Faults == 0) && NonNegative(result.overshoot) &&
            NonNegative(result.yawError) && result.yawError <= 180f &&
            NonNegative(result.positionError) && NonNegative(result.finalSpeed);

        private static bool GhostValid(GhostLapRecording ghost, string courseId)
        {
            if (ghost == null || !KeyValid(courseId) || !ghost.IsUsableFor(courseId) ||
                !NonNegative(ghost.lapTime) || ghost.samples.Length > MaxSamples) return false;
            float previous = -1f;
            foreach (var sample in ghost.samples)
            {
                if (!NonNegative(sample.time) || sample.time <= previous || sample.time > ghost.lapTime ||
                    !DrivingExerciseDefinition.Finite(sample.position) || !DrivingExerciseDefinition.Finite(sample.yaw)) return false;
                previous = sample.time;
            }

            return true;
        }
    }
}
