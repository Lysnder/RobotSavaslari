using System;
using System.IO;
using UnityEngine;

namespace RobotSavaslari.Training
{
    /// <summary>
    /// En iyi turu yerel bir dosyada saklar. Kendi dosyasına yazar; başka
    /// paketin ya da ayar sisteminin verisine dokunmaz.
    ///
    /// Bozuk, eksik ya da başka bir parkura/sürüme ait kayıt sessizce atlanır:
    /// eğitim her durumda rekorsuz başlar, hata vermez.
    /// </summary>
    public static class GhostLapStore
    {
        private const string FileName = "driving-personal-best.json";

        /// <summary>Kaydın tutulduğu dosya yolu.</summary>
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// Bu parkura ait kaydı okur. Kullanılabilir bir kayıt yoksa null
        /// döner; okuma hiçbir durumda istisna fırlatmaz.
        /// </summary>
        public static GhostLapRecording Load(string courseId)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return null;
                }

                string json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                GhostLapRecording recording = JsonUtility.FromJson<GhostLapRecording>(json);
                return recording != null && recording.IsUsableFor(courseId) ? recording : null;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Kisisel rekor okunamadi, rekorsuz devam ediliyor: {error.Message}");
                return null;
            }
        }

        /// <summary>Kaydı yazar. Yazma başarısız olursa eğitim etkilenmez.</summary>
        public static bool Save(GhostLapRecording recording)
        {
            if (recording == null || !recording.IsUsableFor(recording.courseId))
            {
                return false;
            }

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(recording));
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Kisisel rekor yazilamadi: {error.Message}");
                return false;
            }
        }

        /// <summary>Kaydı siler; test ve "rekoru temizle" için.</summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Kisisel rekor silinemedi: {error.Message}");
            }
        }
    }
}
