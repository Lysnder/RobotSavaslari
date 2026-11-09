using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.UI
{
    /// <summary>
    /// Ana menu: Oyna ve Ayarlar. Proje genelindeki diger arayuzler gibi IMGUI ile cizilir.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum Page
        {
            Main,
            Settings
        }

        // Tum olculer bu referans yukseklige gore yazilir ve ekran boyutuna gore olceklenir.
        private const float ReferenceHeight = 720f;

        [SerializeField] private string gameSceneName = "Arena";

        private Page _page = Page.Main;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _qualityStyle;

        private void OnGUI()
        {
            EnsureStyles();

            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            HandleEscape();

            GUI.Label(new Rect(0f, 110f, width, 70f), "ROBOT SAVASLARI", _titleStyle);

            if (_page == Page.Main)
                DrawMainPage(width);
            else
                DrawSettingsPage(width);

            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawMainPage(float width)
        {
            float x = (width - 280f) / 2f;
            if (GUI.Button(new Rect(x, 300f, 280f, 60f), "OYNA", _buttonStyle))
                SceneManager.LoadScene(gameSceneName);

            if (GUI.Button(new Rect(x, 380f, 280f, 60f), "AYARLAR", _buttonStyle))
            {
                GameSettings.Load();
                _page = Page.Settings;
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
            GUI.enabled = quality > 0;
            if (GUI.Button(new Rect(valueX, 365f, 36f, 30f), "<"))
                quality--;
            GUI.enabled = quality < qualityNames.Length - 1;
            if (GUI.Button(new Rect(valueX + valueWidth - 36f, 365f, 36f, 30f), ">"))
                quality++;
            GUI.enabled = true;
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

            if (GUI.Button(new Rect(x + panelWidth - 204f, 425f, 180f, 40f), "Kaydet ve Geri"))
                CloseSettings();
        }

        private void HandleEscape()
        {
            Event current = Event.current;
            if (_page == Page.Settings && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                CloseSettings();
                current.Use();
            }
        }

        private void CloseSettings()
        {
            GameSettings.Save();
            _page = Page.Main;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null || GUI.skin == null)
                return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
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
