using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.UI
{
    /// <summary>
    /// Arena icinden ses, goruntu ve kamera ayarlarini duzenler.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]
    public sealed class SettingsPanelController : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        public bool IsOpen { get; private set; }

        private float _previousTimeScale;
        private GUIStyle _subtitleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _qualityStyle;
        private GUIStyle _sectionStyle;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ToggleSettings();
        }

        public void ToggleSettings()
        {
            if (IsOpen)
                CloseSettings();
            else
            {
                GameSettings.Load();
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                IsOpen = true;
            }
        }

        private void OnDisable()
        {
            if (IsOpen) CloseSettings();
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            EnsureStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            bool previousEnabled = GUI.enabled;
            try
            {
                float scale = Mathf.Max(0.01f, Mathf.Min(Screen.height / ReferenceHeight, Screen.width / 600f));
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                float width = Screen.width / scale;
                DrawSettingsPage(width);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.enabled = previousEnabled;
            }
        }

        private void DrawSettingsPage(float width)
        {
            GUI.Label(new Rect(0f, 60f, width, 36f), "AYARLAR", _subtitleStyle);

            const float panelTop = 105f;
            const float panelWidth = 520f;
            const float row = 42f;
            float x = (width - panelWidth) / 2f;
            GUI.Box(new Rect(x, panelTop, panelWidth, 470f), string.Empty);

            float labelX = x + 24f;
            float valueX = x + 230f;
            float valueWidth = panelWidth - 254f;
            float y = panelTop + 20f;

            // Ses
            GUI.Label(new Rect(labelX, y, 200f, 30f), "Ses", _labelStyle);
            float volume = GUI.HorizontalSlider(new Rect(valueX, y + 10f, valueWidth - 60f, 20f), GameSettings.MasterVolume, 0f, 1f);
            GUI.Label(new Rect(valueX + valueWidth - 50f, y, 50f, 30f), Mathf.RoundToInt(volume * 100f) + "%", _labelStyle);
            if (!Mathf.Approximately(volume, GameSettings.MasterVolume))
            {
                GameSettings.MasterVolume = volume;
                GameSettings.Apply();
            }
            y += row;

            // Tam ekran
            GUI.Label(new Rect(labelX, y, 200f, 30f), "Tam ekran", _labelStyle);
            bool fullscreen = GUI.Toggle(new Rect(valueX, y + 5f, valueWidth, 24f), GameSettings.Fullscreen,
                GameSettings.Fullscreen ? " Acik" : " Kapali");
            if (fullscreen != GameSettings.Fullscreen)
            {
                GameSettings.Fullscreen = fullscreen;
                GameSettings.Apply();
            }
            y += row;

            // Grafik kalitesi
            string[] qualityNames = QualitySettings.names;
            GUI.Label(new Rect(labelX, y, 200f, 30f), "Grafik kalitesi", _labelStyle);
            int quality = GameSettings.QualityLevel;
            // Oklar yalnizca gidilecek bir seviye varken aktif olur.
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && quality > 0;
            if (GUI.Button(new Rect(valueX, y, 36f, 30f), "<"))
                quality--;
            GUI.enabled = previousEnabled && quality < qualityNames.Length - 1;
            if (GUI.Button(new Rect(valueX + valueWidth - 36f, y, 36f, 30f), ">"))
                quality++;
            GUI.enabled = previousEnabled;
            GUI.Label(new Rect(valueX + 40f, y, valueWidth - 80f, 30f), qualityNames[quality], _qualityStyle);
            if (quality != GameSettings.QualityLevel)
            {
                GameSettings.QualityLevel = quality;
                GameSettings.Apply();
            }
            y += row + 4f;

            DrawCameraSettings(labelX, valueX, valueWidth, y, row);

            float buttonY = panelTop + 470f - 60f;
            if (GUI.Button(new Rect(x + 20f, buttonY, 150f, 40f), "Varsayilanlar"))
            {
                GameSettings.ResetToDefaults();
                GameSettings.Apply();
            }

            if (GUI.Button(new Rect(x + 185f, buttonY, 150f, 40f), "Kamera varsayilanlari"))
                GameSettings.ResetCameraToDefaults();

            if (GUI.Button(new Rect(x + 350f, buttonY, 150f, 40f), "Kaydet ve Kapat"))
                CloseSettings();
        }

        /// <summary>Camera section. Values are clamped by <see cref="GameSettings"/> and read live by the camera.</summary>
        private float DrawCameraSettings(float labelX, float valueX, float valueWidth, float y, float row)
        {
            GUI.Label(new Rect(labelX, y, 200f, 26f), "KAMERA", _sectionStyle);
            y += 34f;

            GameSettings.CameraHorizontalSensitivity = DrawSliderRow("Yatay hassasiyet", labelX, valueX, valueWidth, y,
                GameSettings.CameraHorizontalSensitivity, GameSettings.CameraSensitivityMin, GameSettings.CameraSensitivityMax,
                GameSettings.CameraHorizontalSensitivity.ToString("0.0") + "x");
            y += row;

            GameSettings.CameraVerticalSensitivity = DrawSliderRow("Dikey hassasiyet", labelX, valueX, valueWidth, y,
                GameSettings.CameraVerticalSensitivity, GameSettings.CameraSensitivityMin, GameSettings.CameraSensitivityMax,
                GameSettings.CameraVerticalSensitivity.ToString("0.0") + "x");
            y += row;

            GUI.Label(new Rect(labelX, y, 200f, 30f), "Dikey ters cevir", _labelStyle);
            GameSettings.CameraInvertY = GUI.Toggle(new Rect(valueX, y + 5f, valueWidth, 24f), GameSettings.CameraInvertY,
                GameSettings.CameraInvertY ? " Acik" : " Kapali");
            y += row;

            GameSettings.CameraSmoothing = DrawSliderRow("Yumusatma", labelX, valueX, valueWidth, y,
                GameSettings.CameraSmoothing, GameSettings.CameraSmoothingMin, GameSettings.CameraSmoothingMax,
                Mathf.RoundToInt(GameSettings.CameraSmoothing * 100f) + "%");
            y += row;

            GameSettings.CameraDefaultDistance = DrawSliderRow("Varsayilan mesafe", labelX, valueX, valueWidth, y,
                GameSettings.CameraDefaultDistance, GameSettings.CameraDistanceMin, GameSettings.CameraDistanceMax,
                GameSettings.CameraDefaultDistance.ToString("0.0") + " m");
            return y + row;
        }

        private float DrawSliderRow(string label, float labelX, float valueX, float valueWidth, float y,
            float value, float min, float max, string valueText)
        {
            GUI.Label(new Rect(labelX, y, 200f, 30f), label, _labelStyle);
            float result = GUI.HorizontalSlider(new Rect(valueX, y + 10f, valueWidth - 70f, 20f), value, min, max);
            GUI.Label(new Rect(valueX + valueWidth - 62f, y, 62f, 30f), valueText, _labelStyle);
            return result;
        }

        private void CloseSettings()
        {
            GameSettings.Save();
            Time.timeScale = _previousTimeScale;
            IsOpen = false;
        }

        private void EnsureStyles()
        {
            if (_subtitleStyle != null || GUI.skin == null)
                return;

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft
            };
            _qualityStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
        }
    }
}
