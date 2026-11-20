using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RobotSavaslari.Match
{
    [Serializable]
    public sealed class ChallengeStageResult
    {
        public string stageId, arenaId, profileId;
        public MatchRecord encounter;
    }

    [Serializable]
    public sealed class ChallengeSession
    {
        public string definitionId, sessionId;
        public int definitionRevision, nextStageIndex;
        public List<ChallengeStageResult> results = new List<ChallengeStageResult>();

        public bool IsValid(ChallengeDefinition definition)
        {
            if (definitionId != definition.definitionId || definitionRevision != definition.revision ||
                !Guid.TryParseExact(sessionId, "N", out _) || results == null ||
                nextStageIndex != results.Count || results.Count > definition.stages.Length) return false;

            var encounters = new HashSet<string>();
            for (int i = 0; i < results.Count; i++)
            {
                var stage = definition.stages[i];
                var result = results[i];
                if (result == null || result.stageId != stage.stageId || result.arenaId != stage.arenaId ||
                    result.profileId != stage.profileId || result.encounter == null || !result.encounter.IsValid ||
                    result.encounter.rules.mode != stage.rules.mode ||
                    result.encounter.rules.timeLimitSeconds != stage.rules.timeLimitSeconds ||
                    !encounters.Add(result.encounter.id)) return false;
            }
            return true;
        }

        public ChallengeSession WithResult(ChallengeStage stage, MatchRecord encounter)
        {
            var next = (ChallengeSession)MemberwiseClone();
            next.results = new List<ChallengeStageResult>(results)
            {
                new ChallengeStageResult { stageId = stage.stageId, arenaId = stage.arenaId, profileId = stage.profileId, encounter = encounter }
            };
            next.nextStageIndex++;
            return next;
        }
    }

    [Serializable]
    public sealed class ChallengeSave
    {
        public const int ArchiveCapacity = 20;
        public int version = 1;
        public List<ChallengeSession> checkpoint = new List<ChallengeSession>();
        public ChallengeSession Active => checkpoint.Count == 0 ? null : checkpoint[0];
        public List<ChallengeSession> archive = new List<ChallengeSession>();

        public bool IsValid(ChallengeDefinition definition)
        {
            if (version != 1 || checkpoint == null || checkpoint.Count > 1 || archive == null || archive.Count > ArchiveCapacity) return false;
            var sessions = new HashSet<string>();
            if (checkpoint.Count == 1 && (Active == null || !Active.IsValid(definition) || !sessions.Add(Active.sessionId))) return false;
            foreach (var run in archive)
                if (run == null || !run.IsValid(definition) || !sessions.Add(run.sessionId)) return false;
            return true;
        }

        public ChallengeSave ChangeActive(ChallengeSession next, bool archivePrevious)
        {
            var save = new ChallengeSave { archive = new List<ChallengeSession>(archive) };
            if (next != null) save.checkpoint.Add(next);
            if (archivePrevious && Active != null) save.archive.Insert(0, Active);
            if (save.archive.Count > ArchiveCapacity) save.archive.RemoveAt(ArchiveCapacity);
            return save;
        }
    }

    public sealed class ChallengeSessionStore
    {
        private const int MaximumBytes = 256 * 1024;
        private readonly string _path;
        private readonly ChallengeDefinition _definition;
        private string _expected;
        public bool ReadFailed { get; private set; }
        public string Notice { get; private set; }

        public ChallengeSessionStore(ChallengeDefinition definition, string path)
        {
            _definition = definition;
            _path = Path.GetFullPath(path);
        }

        public ChallengeSave Load()
        {
            try
            {
                _expected = Read();
                var save = Parse(_expected);
                ReadFailed = false;
                Notice = null;
                return save;
            }
            catch (Exception error) when (IsStorageError(error))
            {
                ReadFailed = true;
                Notice = "Mücadele kaydı açılamadı. " + error.Message;
                return new ChallengeSave();
            }
        }

        public bool Save(ChallengeSave save)
        {
            if (ReadFailed) return false;
            string temporary = null;
            try
            {
                if (!save.IsValid(_definition)) throw new InvalidDataException("Geçersiz mücadele verisi.");
                string content = JsonUtility.ToJson(save);
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                if (bytes.Length > MaximumBytes) throw new InvalidDataException("Kayıt 256 KiB sınırını aşıyor.");

                using (Lock())
                {
                    if (Read() != _expected) throw new IOException("Kayıt başka bir oyun oturumunda değişti. Oyunu yeniden aç.");
                    temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    if (_expected == null) File.Move(temporary, _path);
                    else File.Replace(temporary, _path, null);
                    _expected = content;
                }
                Notice = null;
                return true;
            }
            catch (Exception error) when (IsStorageError(error))
            {
                Notice = "Mücadele kaydedilemedi. " + error.Message;
                return false;
            }
            finally
            {
                if (temporary != null)
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        public bool PreserveUnreadableSave()
        {
            if (!ReadFailed) return false;
            try
            {
                using (Lock())
                {
                    try
                    {
                        Parse(Read());
                        Notice = "Kayıt artık okunabiliyor. Oyunu yeniden aç.";
                        return false;
                    }
                    catch (Exception error) when (error is InvalidDataException || error is ArgumentException) { }

                    File.Move(_path, _path + ".preserved-" + Guid.NewGuid().ToString("N"));
                    _expected = null;
                }
                ReadFailed = false;
                Notice = "Önceki dosya saklandı. Yeni mücadele başlatabilirsin.";
                return true;
            }
            catch (Exception error) when (IsStorageError(error))
            {
                Notice = "Önceki kayıt saklanamadı. " + error.Message;
                return false;
            }
        }

        private FileStream Lock()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            return new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private string Read()
        {
            var info = new FileInfo(_path);
            if (!info.Exists)
            {
                if (Directory.Exists(_path)) throw new IOException("Kayıt yolu bir klasör.");
                return null;
            }
            if (info.Length > MaximumBytes) throw new InvalidDataException("Kayıt 256 KiB sınırını aşıyor.");
            return File.ReadAllText(_path);
        }

        private ChallengeSave Parse(string content)
        {
            if (content == null) return new ChallengeSave();
            var save = JsonUtility.FromJson<ChallengeSave>(content);
            if (save == null || !save.IsValid(_definition))
                throw new InvalidDataException("Kayıt biçimi, sürümü veya mücadele tanımı uyumsuz.");
            return save;
        }

        private static bool IsStorageError(Exception error) =>
            error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException;
    }
}
