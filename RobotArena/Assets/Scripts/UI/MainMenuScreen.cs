using RobotSavaslari.Match;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.UI
{
    /// <summary>The menu, countdown and match HUD share the arena's existing match and robots.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MatchController), typeof(SettingsPanelController))]
    public sealed class MainMenuScreen : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        private MatchController _match;
        private SettingsPanelController _settings;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _resultStyle;
        private GUIStyle _countdownStyle;

        private void Awake()
        {
            _match = GetComponent<MatchController>();
            _settings = GetComponent<SettingsPanelController>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (!_match.IsInitialized || _settings.IsOpen || keyboard == null ||
                keyboard.escapeKey.wasPressedThisFrame) return;

            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            if ((_match.State == MatchController.MatchState.Ready && enter) ||
                (_match.State == MatchController.MatchState.Result && keyboard.rKey.wasPressedThisFrame))
                _match.RequestCountdown();
        }

        private void OnGUI()
        {
            if (!_match.IsInitialized || _settings.IsOpen) return;
            EnsureStyles();
            Matrix4x4 previous = GUI.matrix;
            try
            {
                float scale = Mathf.Max(0.01f, Mathf.Min(Screen.height / ReferenceHeight, Screen.width / 600f));
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                if (_match.State == MatchController.MatchState.Ready)
                {
                    DrawMenu(width, height);
                    return;
                }

                if (GUI.Button(new Rect(width - 174f, 16f, 158f, 36f), "Ayarlar (Esc)"))
                    _settings.ToggleSettings();
                if (_settings.IsOpen) return;

                if (_match.State == MatchController.MatchState.Countdown)
                {
                    DrawAnnouncement(width, height, Mathf.Max(1, Mathf.CeilToInt(_match.CountdownRemaining)).ToString());
                    return;
                }
                if (_match.State == MatchController.MatchState.Restarting) return;

                DrawMatchHud();
                if (_match.ShowStartMessage) DrawAnnouncement(width, height, "BAŞLA!");
                if (_match.State == MatchController.MatchState.Result)
                    DrawResult(width, height);
            }
            finally { GUI.matrix = previous; }
        }

        private void DrawMenu(float width, float height)
        {
            float center = height * .5f;
            float x = (width - 280f) * .5f;
            GUI.Label(new Rect(0, center - 190f, width, 75f), "ROBOT SAVAŞLARI", _titleStyle);
            if (GUI.Button(new Rect(x, center - 84f, 280f, 52f), "Oyna (Enter)", _buttonStyle))
                _match.RequestCountdown();
            if (GUI.Button(new Rect(x, center - 14f, 280f, 52f), "Ayarlar (Esc)", _buttonStyle))
                _settings.ToggleSettings();
            if (GUI.Button(new Rect(x, center + 56f, 280f, 52f), "Çıkış", _buttonStyle))
                QuitGame();
        }

        private void DrawMatchHud()
        {
            GUILayout.BeginArea(new Rect(16, 16, 430, 135), GUI.skin.box);
            GUILayout.Label("Maç " + _match.MatchCount);
            GUILayout.Label("Oyuncu: " + Mathf.CeilToInt(_match.Player.GetComponent<RobotHealth>().CurrentHealth) +
                            "   Rakip: " + Mathf.CeilToInt(_match.Opponent.GetComponent<RobotHealth>().CurrentHealth));
            GUILayout.Label("W/S veya ↑/↓: sürüş    A/D veya ←/→: dönüş    Space: saldırı");
            GUILayout.EndArea();
        }

        private void DrawAnnouncement(float width, float height, string message)
        {
            GUI.Box(new Rect((width - 260f) * .5f, (height - 115f) * .5f, 260f, 115f), string.Empty);
            bool countingDown = _match.State == MatchController.MatchState.Countdown;
            float pulse = countingDown ? Mathf.Repeat(_match.CountdownRemaining, 1f) : 0f;
            _countdownStyle.fontSize = Mathf.RoundToInt(58f + pulse * 15f);
            Color previous = GUI.color;
            GUI.color = countingDown ? new Color(1f, .85f, .24f) : new Color(.45f, 1f, .50f);
            GUI.Label(new Rect(0f, (height - 115f) * .5f, width, 115f), message, _countdownStyle);
            GUI.color = previous;
        }

        private void DrawResult(float width, float height)
        {
            var panel = new Rect((width - 420f) * .5f, (height - 218f) * .5f, 420f, 218f);
            GUI.Box(panel, string.Empty);
            GUI.Label(new Rect(panel.x, panel.y + 10f, panel.width, 54f), _match.ResultText, _resultStyle);
            if (GUI.Button(new Rect(panel.x + 85f, panel.y + 74f, 250f, 50f), "Yeni maç (R)", _buttonStyle))
                _match.RequestCountdown();
            if (GUI.Button(new Rect(panel.x + 85f, panel.y + 142f, 250f, 50f), "Ana menü", _buttonStyle))
                _match.Prepare();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            _resultStyle = new GUIStyle(_titleStyle) { fontSize = 28 };
            _countdownStyle = new GUIStyle(_titleStyle) { fontSize = 58 };
        }
    }
}
