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

        public const string CameraHorizontalSensitivityKey = "Settings.Camera.HorizontalSensitivity";
        public const string CameraVerticalSensitivityKey = "Settings.Camera.VerticalSensitivity";
        public const string CameraInvertYKey = "Settings.Camera.InvertY";
        public const string CameraSmoothingKey = "Settings.Camera.Smoothing";
        public const string CameraDefaultDistanceKey = "Settings.Camera.DefaultDistance";

        /// <summary>Sensitivity is a multiplier of the camera's base degrees-per-pixel rate.</summary>
        public const float CameraSensitivityMin = 0.1f;
        public const float CameraSensitivityMax = 3f;
        public const float DefaultCameraSensitivity = 1f;
        /// <summary>0 follows instantly, 1 is the softest follow.</summary>
        public const float CameraSmoothingMin = 0f;
        public const float CameraSmoothingMax = 1f;
        public const float DefaultCameraSmoothing = 0.5f;
        /// <summary>Follow distance in metres; matches the follow camera's zoom range.</summary>
        public const float CameraDistanceMin = 2.5f;
        public const float CameraDistanceMax = 16f;
        public const float DefaultCameraDistance = 8f;

        public static float MasterVolume { get; set; } = 1f;
        public static bool Fullscreen { get; set; } = true;
        public static int QualityLevel { get; set; }

        private static float _cameraHorizontalSensitivity = DefaultCameraSensitivity;
        private static float _cameraVerticalSensitivity = DefaultCameraSensitivity;
        private static float _cameraSmoothing = DefaultCameraSmoothing;
        private static float _cameraDefaultDistance = DefaultCameraDistance;

        public static float CameraHorizontalSensitivity
        {
            get => _cameraHorizontalSensitivity;
            set => _cameraHorizontalSensitivity = Sanitize(value, CameraSensitivityMin, CameraSensitivityMax, DefaultCameraSensitivity);
        }

        public static float CameraVerticalSensitivity
        {
            get => _cameraVerticalSensitivity;
            set => _cameraVerticalSensitivity = Sanitize(value, CameraSensitivityMin, CameraSensitivityMax, DefaultCameraSensitivity);
        }

        public static bool CameraInvertY { get; set; }

        public static float CameraSmoothing
        {
            get => _cameraSmoothing;
            set => _cameraSmoothing = Sanitize(value, CameraSmoothingMin, CameraSmoothingMax, DefaultCameraSmoothing);
        }

        public static float CameraDefaultDistance
        {
            get => _cameraDefaultDistance;
            set => _cameraDefaultDistance = Sanitize(value, CameraDistanceMin, CameraDistanceMax, DefaultCameraDistance);
        }

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
            LoadCamera();
        }

        /// <summary>Missing, out-of-range or corrupt camera values fall back to safe defaults.</summary>
        private static void LoadCamera()
        {
            CameraHorizontalSensitivity = PlayerPrefs.GetFloat(CameraHorizontalSensitivityKey, DefaultCameraSensitivity);
            CameraVerticalSensitivity = PlayerPrefs.GetFloat(CameraVerticalSensitivityKey, DefaultCameraSensitivity);
            CameraInvertY = PlayerPrefs.GetInt(CameraInvertYKey, 0) == 1;
            CameraSmoothing = PlayerPrefs.GetFloat(CameraSmoothingKey, DefaultCameraSmoothing);
            CameraDefaultDistance = PlayerPrefs.GetFloat(CameraDefaultDistanceKey, DefaultCameraDistance);
        }

        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (Screen.fullScreen != Fullscreen)
                Screen.fullScreen = Fullscreen;
            if (QualitySettings.GetQualityLevel() != QualityLevel)
                QualitySettings.SetQualityLevel(QualityLevel, true);
            // Camera values are read by the camera every frame; nothing to push here.
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
            PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(QualityKey, QualityLevel);
            PlayerPrefs.SetFloat(CameraHorizontalSensitivityKey, CameraHorizontalSensitivity);
            PlayerPrefs.SetFloat(CameraVerticalSensitivityKey, CameraVerticalSensitivity);
            PlayerPrefs.SetInt(CameraInvertYKey, CameraInvertY ? 1 : 0);
            PlayerPrefs.SetFloat(CameraSmoothingKey, CameraSmoothing);
            PlayerPrefs.SetFloat(CameraDefaultDistanceKey, CameraDefaultDistance);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            MasterVolume = 1f;
            Fullscreen = true;
            QualityLevel = QualitySettings.names.Length - 1;
            ResetCameraToDefaults();
        }

        /// <summary>Restores only the camera values. No stored key, camera or otherwise, is deleted.</summary>
        public static void ResetCameraToDefaults()
        {
            CameraHorizontalSensitivity = DefaultCameraSensitivity;
            CameraVerticalSensitivity = DefaultCameraSensitivity;
            CameraInvertY = false;
            CameraSmoothing = DefaultCameraSmoothing;
            CameraDefaultDistance = DefaultCameraDistance;
        }

        private static float Sanitize(float value, float min, float max, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }
    }
}
