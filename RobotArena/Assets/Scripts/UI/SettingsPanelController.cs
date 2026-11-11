using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.UI
{
    /// <summary>
    /// Arena icinden ses ve goruntu ayarlarini duzenler.
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
            GUI.Label(new Rect(0f, 185f, width, 36f), "AYARLAR", _subtitleStyle);

            const float panelWidth = 460f;
            float x = (width - panelWidth) / 2f;
            GUI.Box(new Rect(x, 235f, panelWidth, 250f), string.Empty);

            float labelX = x + 24f;
            float valueX = x + 190f;
            float valueWidth = panelWidth - 214f;

            // Ses
            GUI.Label(new Rect(labelX, 260f, 160f, 30f), "Ses", _labelStyle);
            float volume = GUI.HorizontalSlider(new Rect(valueX, 270f, valueWidth - 60f, 20f), GameSettings.MasterVolume, 0f, 1f);
            GUI.Label(new Rect(valueX + valueWidth - 50f, 260f, 50f, 30f), Mathf.RoundToInt(volume * 100f) + "%", _labelStyle);
            if (!Mathf.Approximately(volume, GameSettings.MasterVolume))
            {
                GameSettings.MasterVolume = volume;
                GameSettings.Apply();
            }

            // Tam ekran
            GUI.Label(new Rect(labelX, 315f, 160f, 30f), "Tam ekran", _labelStyle);
            bool fullscreen = GUI.Toggle(new Rect(valueX, 320f, valueWidth, 24f), GameSettings.Fullscreen,
                GameSettings.Fullscreen ? " Acik" : " Kapali");
            if (fullscreen != GameSettings.Fullscreen)
            {
                GameSettings.Fullscreen = fullscreen;
                GameSettings.Apply();
            }

            // Grafik kalitesi
            string[] qualityNames = QualitySettings.names;
            GUI.Label(new Rect(labelX, 365f, 160f, 30f), "Grafik kalitesi", _labelStyle);
            int quality = GameSettings.QualityLevel;
            // Oklar yalnizca gidilecek bir seviye varken aktif olur.
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && quality > 0;
            if (GUI.Button(new Rect(valueX, 365f, 36f, 30f), "<"))
                quality--;
            GUI.enabled = previousEnabled && quality < qualityNames.Length - 1;
            if (GUI.Button(new Rect(valueX + valueWidth - 36f, 365f, 36f, 30f), ">"))
                quality++;
            GUI.enabled = previousEnabled;
            GUI.Label(new Rect(valueX + 40f, 365f, valueWidth - 80f, 30f), qualityNames[quality], _qualityStyle);
            if (quality != GameSettings.QualityLevel)
            {
                GameSettings.QualityLevel = quality;
                GameSettings.Apply();
            }

            if (GUI.Button(new Rect(labelX, 425f, 180f, 40f), "Varsayilanlar"))
            {
                GameSettings.ResetToDefaults();
                GameSettings.Apply();
            }

            if (GUI.Button(new Rect(x + panelWidth - 204f, 425f, 180f, 40f), "Kaydet ve Kapat"))
                CloseSettings();
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
        }
    }
}
