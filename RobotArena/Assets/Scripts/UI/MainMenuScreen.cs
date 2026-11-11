using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.UI
{
    /// <summary>
    /// Ana menu sahnesi: baslik, Oyna ve Ayarlar. Ayarlar penceresi arenadakiyle ayni
    /// MainMenuController uzerinden acilir, boylece ayar ekrani tek yerde tutulur.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        // Tum olculer bu referans yukseklige gore yazilir ve ekran boyutuna gore olceklenir.
        private const float ReferenceHeight = 720f;

        [SerializeField] private string gameSceneName = "MainArena";

        private MainMenuController _settings;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;

        private void Awake()
        {
            // Sahne dosyasinda yoksa ayarlar penceresi calisma aninda eklenir.
            _settings = GetComponent<MainMenuController>();
            if (_settings == null)
                _settings = gameObject.AddComponent<MainMenuController>();
        }

        private void OnGUI()
        {
            EnsureStyles();

            Matrix4x4 previousMatrix = GUI.matrix;
            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            GUI.Label(new Rect(0f, 110f, width, 70f), "ROBOT SAVASLARI", _titleStyle);

            // Ayarlar penceresi acikken ana butonlar onunla cakismasin diye gizlenir.
            if (!_settings.IsOpen)
            {
                float x = (width - 280f) / 2f;
                if (GUI.Button(new Rect(x, 300f, 280f, 60f), "OYNA", _buttonStyle))
                    SceneManager.LoadScene(gameSceneName);

                if (GUI.Button(new Rect(x, 380f, 280f, 60f), "AYARLAR", _buttonStyle))
                    _settings.ToggleSettings();
            }

            GUI.matrix = previousMatrix;
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
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
