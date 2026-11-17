using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RobotSavaslari.Match
{
    public sealed class MatchHistoryStore
    {
        public const int Capacity = 20;
        private const int MaximumFileBytes = 512 * 1024;
        public string FilePath { get; }
        public string LastError { get; private set; }

        [Serializable]
        private sealed class HistoryFile
        {
            public int version = 1;
            public List<MatchRecord> records = new List<MatchRecord>();
        }

        public MatchHistoryStore(string filePath = null)
        {
            FilePath = filePath ?? Path.Combine(Application.persistentDataPath, "match-history.json");
        }

        public List<MatchRecord> Load()
        {
            LastError = null;
            try
            {
                if (!File.Exists(FilePath)) return new List<MatchRecord>();
                if (new FileInfo(FilePath).Length > MaximumFileBytes) throw new InvalidDataException("Geçmiş dosyası çok büyük.");
                var data = JsonUtility.FromJson<HistoryFile>(File.ReadAllText(FilePath));
                if (data == null || data.version != 1 || data.records == null) throw new InvalidDataException("Geçmiş sürümü veya biçimi geçersiz.");
                var records = new List<MatchRecord>();
                var ids = new HashSet<string>();
                foreach (var record in data.records)
                    if (record != null && record.IsValid && ids.Add(record.id) && records.Count < Capacity) records.Add(record);
                return records;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                LastError = "Geçmiş okunamadı: " + error.Message;
                return new List<MatchRecord>();
            }
        }

        public bool Append(MatchRecord record)
        {
            if (record == null || !record.IsValid) { LastError = "Geçersiz karşılaşma kaydı."; return false; }
            var records = Load();
            if (records.Exists(item => item.id == record.id)) return true;
            records.Insert(0, record);
            if (records.Count > Capacity) records.RemoveRange(Capacity, records.Count - Capacity);
            string temporary = FilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath)));
                File.WriteAllText(temporary, JsonUtility.ToJson(new HistoryFile { records = records }, true));
                // Replace the old complete file only after the new one has been written.
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
                else File.Move(temporary, FilePath);
                LastError = null;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                LastError = "Geçmiş kaydedilemedi: " + error.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
