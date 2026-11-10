using UnityEngine;

namespace RobotSavaslari.UI
{
    /// <summary>
    /// Oyuncu ayarlarini PlayerPrefs ile saklar ve uygular.
    /// Oyun hangi sahneden baslarsa baslasin ayarlar ilk sahne yuklenmeden uygulanir.
    /// </summary>
    public static class GameSettings
    {
        private const string VolumeKey = "Settings.MasterVolume";
        private const string FullscreenKey = "Settings.Fullscreen";
        private const string QualityKey = "Settings.QualityLevel";

        public static float MasterVolume { get; set; } = 1f;
        public static bool Fullscreen { get; set; } = true;
        public static int QualityLevel { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Load();
            Apply();
        }

        public static void Load()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            int maxQuality = QualitySettings.names.Length - 1;
            QualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, maxQuality);
        }

        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (Screen.fullScreen != Fullscreen)
                Screen.fullScreen = Fullscreen;
            if (QualitySettings.GetQualityLevel() != QualityLevel)
                QualitySettings.SetQualityLevel(QualityLevel, true);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
            PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(QualityKey, QualityLevel);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            MasterVolume = 1f;
            Fullscreen = true;
            QualityLevel = QualitySettings.names.Length - 1;
        }
    }
}
