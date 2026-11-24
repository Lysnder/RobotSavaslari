using System;
using System.IO;
using UnityEngine;

namespace RobotSavaslari.UI
{
    [Serializable]
    public struct WorkshopLoadout
    {
        public string chassisId, tireId, armorId, weaponId;

        public static WorkshopLoadout Default => new WorkshopLoadout {
            chassisId = "standard", tireId = "soft", armorId = "light", weaponId = "spinner"
        };

        public bool IsValid => chassisId == "standard" && (tireId == "soft" || tireId == "hard") &&
            (armorId == "light" || armorId == "heavy") && Combat.WorkshopWeapons.Index(weaponId) >= 0;
    }

    [Serializable]
    public sealed class WorkshopSave
    {
        public int version;
        public string selected;
        public WorkshopLoadout lastModular;

        public bool IsValid => version == 1 && (selected == "spinner" || selected == "fork" || selected == "modular") && lastModular.IsValid;
        public static WorkshopSave Default => new WorkshopSave { version = 1, selected = "spinner", lastModular = WorkshopLoadout.Default };
    }

    public sealed class WorkshopStore
    {
        public const string FileName = "pve-workshop-v1.json";
        public string FilePath { get; }
        public string Notice { get; private set; }
        public bool IsProtected { get; private set; }
        private string expected;

        public WorkshopStore(string filePath = null)
        {
            FilePath = Path.GetFullPath(filePath ?? Path.Combine(Application.persistentDataPath, FileName));
        }

        public WorkshopSave Load()
        {
            try
            {
                expected = Read();
                var save = expected == null ? WorkshopSave.Default : JsonUtility.FromJson<WorkshopSave>(expected);
                if (save == null || !save.IsValid)
                    throw new InvalidDataException(save != null && save.version > 1 ? "Kayıt daha yeni bir sürüme ait." : "Sürüm veya parça kimlikleri geçersiz.");
                IsProtected = false;
                Notice = null;
                return save;
            }
            catch (Exception e) when (IsStorageError(e))
            {
                IsProtected = true;
                Notice = "Atölye kaydı korunuyor. Hazır robot seçebilirsiniz. " + e.Message;
                return WorkshopSave.Default;
            }
        }

        public bool Save(WorkshopSave save)
        {
            if (IsProtected || save == null || !save.IsValid) return false;
            string temporary = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                using var guard = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.Write,
                    FileShare.None, 1, FileOptions.DeleteOnClose);
                if (Read() != expected)
                {
                    IsProtected = true;
                    Notice = "Atölye kaydı başka bir oturumda değişti. Dosya korundu.";
                    return false;
                }
                temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                string json = JsonUtility.ToJson(save, true);
                File.WriteAllText(temporary, json);
                if (expected == null) File.Move(temporary, FilePath);
                else File.Replace(temporary, FilePath, null);
                expected = json;
                Notice = null;
                return true;
            }
            catch (Exception e) when (IsStorageError(e))
            {
                Notice = "Atölye seçimi kaydedilemedi. " + e.Message;
                return false;
            }
            finally
            {
                try { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception e) when (IsStorageError(e)) { }
            }
        }

        private string Read()
        {
            if (!File.Exists(FilePath)) return null;
            if (new FileInfo(FilePath).Length > 4096) throw new InvalidDataException("Dosya 4 KiB sınırını aşıyor.");
            return File.ReadAllText(FilePath);
        }

        private static bool IsStorageError(Exception e) => e is IOException || e is InvalidDataException ||
            e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException;
    }
}
