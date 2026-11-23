using UnityEngine;
using UnityEngine.InputSystem;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;

namespace RobotSavaslari.UI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]
    public sealed class SettingsPanelController : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        private float _previousTimeScale;
        private bool _cameraPage;
        private MatchController _match;
        private MainMenuScreen _menu;
        private DriveBindingsScreen Bindings => _match != null && _match.Player != null
            ? _match.Player.GetComponent<DriveBindingsScreen>()
            : null;

        private void Awake()
        {
            _match = GetComponent<MatchController>();
            _menu = GetComponent<MainMenuScreen>();
        }

        private void Update()
        {
            if (Bindings is { IsOpen: true }) return;
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (!IsOpen && _menu != null && _menu.TryCloseHistory()) return;
            ToggleSettings();
        }

        public void ToggleSettings()
        {
            if (IsOpen)
            {
                CloseSettings();
                return;
            }

            GameSettings.Load();
            _cameraPage = false;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            IsOpen = true;
        }

        private void OnDisable()
        {
            if (IsOpen) CloseSettings();
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            var bindings = Bindings;
            if (bindings != null && bindings.IsOpen) return;
            Matrix4x4 previous = GUI.matrix;
            try
            {
                float scale = ArenaMenuStyle.Scale(Screen.width, Screen.height);
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                DrawPanel(Screen.width / scale, Screen.height / scale, bindings);
            }
            finally { GUI.matrix = previous; }
        }

        private void DrawPanel(float width, float height, DriveBindingsScreen bindings)
        {
            var theme = ArenaMenuStyle.Current;
            var rect = new Rect((width - 676) * .5f, (height - 538) * .5f, 676, 538);
            if (_match != null && _match.State == MatchController.MatchState.Ready)
                ArenaMenuStyle.DrawGarage(width, height, ArenaMenuStyle.Canvas);
            else
                ArenaMenuStyle.Fill(new Rect(0, 0, width, height), ArenaMenuStyle.Canvas);
            ArenaMenuStyle.Panel(rect);
            ArenaMenuStyle.Fill(new Rect(rect.x, rect.y, 4, 89), ArenaMenuStyle.Accent);
            float x = rect.x + 28f;
            float w = rect.width - 56f;
            GUI.Label(new Rect(x, rect.y + 22, w, 44), "Ayarlar", theme.Title);
            GUI.Label(new Rect(x, rect.y + 66, w, 26), "Değişiklikler anında uygulanır, çıkarken kaydedilir.", theme.Hint);

            if (GUI.Button(new Rect(x, rect.y + 109, 188, 39), "Ses ve görüntü", _cameraPage ? theme.Chip : theme.Selected))
                _cameraPage = false;
            if (GUI.Button(new Rect(x + 196, rect.y + 109, 126, 39), "Kamera", _cameraPage ? theme.Selected : theme.Chip))
                _cameraPage = true;
            if (bindings != null && bindings.isActiveAndEnabled && bindings.AccessAllowed &&
                GUI.Button(new Rect(x + 330, rect.y + 109, 154, 39), "Sürüş tuşları", theme.Button))
            {
                bindings.Open();
                return;
            }
            ArenaMenuStyle.Fill(new Rect(x, rect.y + 164, w, 1), ArenaMenuStyle.Border);
            if (_cameraPage) DrawCamera(x, rect.y + 177, w);
            else DrawGeneral(x, rect.y + 177, w);

            ArenaMenuStyle.Fill(new Rect(x, rect.yMax - 75, w, 1), ArenaMenuStyle.Border);
            if (GUI.Button(new Rect(x, rect.yMax - 58, 242, 40),
                _cameraPage ? "Kamera varsayılanları" : "Tüm ayarları sıfırla", theme.Button))
            {
                if (_cameraPage) GameSettings.ResetCameraToDefaults();
                else
                {
                    GameSettings.ResetToDefaults();
                    GameSettings.Apply();
                }
            }

            if (GUI.Button(new Rect(x + w - 192, rect.yMax - 58, 192, 40), "Kaydet ve dön", theme.Primary))
                CloseSettings();
        }

        private void DrawGeneral(float x, float y, float width)
        {
            var theme = ArenaMenuStyle.Current;
            float before = GameSettings.MasterVolume;
            GameSettings.MasterVolume = Slider(x, y, width, "Ana ses", before, 0f, 1f, $"{Mathf.RoundToInt(before * 100f)}%");
            if (!Mathf.Approximately(before, GameSettings.MasterVolume)) GameSettings.Apply();

            y += 55;
            GUI.Label(new Rect(x, y, 228, 46), "Ekran modu", theme.Body);
            bool fullscreen = GameSettings.Fullscreen;
            if (GUI.Button(new Rect(x + 253, y + 4, 164, 36), "Tam ekran", fullscreen ? theme.Selected : theme.Chip) && !fullscreen)
            {
                GameSettings.Fullscreen = true;
                GameSettings.Apply();
            }
            if (GUI.Button(new Rect(x + 425, y + 4, 164, 36), "Pencereli", fullscreen ? theme.Chip : theme.Selected) && fullscreen)
            {
                GameSettings.Fullscreen = false;
                GameSettings.Apply();
            }
            Divide(x, y, width);

            y += 55;
            GUI.Label(new Rect(x, y, 228, 46), "Grafik kalitesi", theme.Body);
            string[] names = QualitySettings.names;
            int quality = GameSettings.QualityLevel;
            if (names.Length == 0) return;
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && quality > 0;
            if (GUI.Button(new Rect(x + 253, y + 4, 45, 36), "‹", theme.Button)) quality--;
            GUI.enabled = enabled && quality < names.Length - 1;
            if (GUI.Button(new Rect(x + width - 45, y + 4, 45, 36), "›", theme.Button)) quality++;
            GUI.enabled = enabled;
            GUI.Label(new Rect(x + 304, y, width - 358, 46), names[quality], theme.Value);
            Divide(x, y, width);
            if (quality == GameSettings.QualityLevel) return;
            GameSettings.QualityLevel = quality;
            GameSettings.Apply();
        }

        private void DrawCamera(float x, float y, float width)
        {
            var theme = ArenaMenuStyle.Current;
            GameSettings.CameraHorizontalSensitivity = Slider(x, y, width, "Yatay hassasiyet",
                GameSettings.CameraHorizontalSensitivity, GameSettings.CameraSensitivityMin, GameSettings.CameraSensitivityMax,
                $"{GameSettings.CameraHorizontalSensitivity:0.0}×");
            GameSettings.CameraVerticalSensitivity = Slider(x, y += 52, width, "Dikey hassasiyet",
                GameSettings.CameraVerticalSensitivity, GameSettings.CameraSensitivityMin, GameSettings.CameraSensitivityMax,
                $"{GameSettings.CameraVerticalSensitivity:0.0}×");
            GUI.Label(new Rect(x, y += 52, 230, 46), "Dikey ekseni ters çevir", theme.Body);
            bool inverted = GameSettings.CameraInvertY;
            if (GUI.Button(new Rect(x + 253, y + 4, 171, 36), inverted ? "Açık" : "Kapalı",
                inverted ? theme.Selected : theme.Button))
                GameSettings.CameraInvertY = !inverted;
            Divide(x, y, width);
            GameSettings.CameraSmoothing = Slider(x, y += 52, width, "Yumuşatma",
                GameSettings.CameraSmoothing, GameSettings.CameraSmoothingMin, GameSettings.CameraSmoothingMax,
                $"{Mathf.RoundToInt(GameSettings.CameraSmoothing * 100f)}%");
            GameSettings.CameraDefaultDistance = Slider(x, y + 52, width, "Varsayılan mesafe",
                GameSettings.CameraDefaultDistance, GameSettings.CameraDistanceMin, GameSettings.CameraDistanceMax,
                $"{GameSettings.CameraDefaultDistance:0.0} m");
        }

        private static float Slider(float x, float y, float width, string label, float value, float min, float max,
            string text)
        {
            var theme = ArenaMenuStyle.Current;
            GUI.Label(new Rect(x, y, 230, 46), label, theme.Body);
            float updated = GUI.HorizontalSlider(new Rect(x + 253, y + 17, width - 334, 18), value, min, max);
            GUI.Label(new Rect(x + width - 74, y, 74, 46), text, theme.Value);
            Divide(x, y, width);
            return updated;
        }

        private static void Divide(float x, float y, float width) =>
            ArenaMenuStyle.Fill(new Rect(x, y + 47, width, 1), ArenaMenuStyle.Border);

        private void CloseSettings()
        {
            GameSettings.Save();
            Time.timeScale = _previousTimeScale;
            IsOpen = false;
        }
    }
}
