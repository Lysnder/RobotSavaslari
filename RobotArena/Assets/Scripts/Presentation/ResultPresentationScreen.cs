using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Result-only presentation controls: offers the last-hit replay and robot inspection under
    /// MainMenuScreen's result panel, and draws the replay bar or inspection panel while one runs.
    /// Both show the gameplay counters so a recording proves they do not change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultPresentationScreen : MonoBehaviour
    {
        private const float ReferenceHeight = 720f;
        // MainMenuScreen draws its result panel centred at this size.
        private const float ResultPanelHeight = 218f;

        private MatchController _match;
        private SettingsPanelController _settings;
        private MatchReplayPlayer _replay;
        private PostMatchInspection _inspection;
        private GUIStyle _titleStyle, _buttonStyle, _partStyle, _missingPartStyle;

        public void Bind(MatchController match, MatchReplayPlayer replay, PostMatchInspection inspection)
        {
            _match = match;
            _settings = match != null ? match.GetComponent<SettingsPanelController>() : null;
            _replay = replay;
            _inspection = inspection;
        }

        private void OnGUI()
        {
            if (_match == null || !_match.IsInitialized || (_settings != null && _settings.IsOpen)) return;
            bool replaying = _replay != null && _replay.IsActive;
            bool inspecting = _inspection != null && _inspection.IsActive;
            if (!replaying && !inspecting && _match.State != MatchController.MatchState.Result) return;
            EnsureStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            bool previousEnabled = GUI.enabled;
            try
            {
                float scale = Mathf.Max(0.01f, Mathf.Min(Screen.height / ReferenceHeight, Screen.width / 600f));
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                if (replaying) DrawReplayBar(height);
                else if (inspecting) DrawInspection(height);
                else DrawResultActions(width, height);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.enabled = previousEnabled;
            }
        }

        private void DrawResultActions(float width, float height)
        {
            float y = (height + ResultPanelHeight) * 0.5f + 12f;
            float x = (width - 512f) * 0.5f;
            if (_replay != null)
            {
                GUI.enabled = _replay.CanBegin;
                if (GUI.Button(new Rect(x, y, 250f, 44f), _replay.CanBegin ? "Son vuruşu izle" : "Tekrar kaydı yok", _buttonStyle))
                    _replay.Begin();
            }
            if (_inspection != null)
            {
                GUI.enabled = _inspection.CanBegin;
                if (GUI.Button(new Rect(x + 262f, y, 250f, 44f), "Robotları incele", _buttonStyle))
                    _inspection.Begin(PostMatchInspection.PlayerRobot);
            }
            GUI.enabled = true;
        }

        private void DrawInspection(float height)
        {
            if (_inspection.IsInterfaceHidden)
            {
                if (GUI.Button(new Rect(16f, 16f, 170f, 30f), "Arayüzü göster (H)")) _inspection.SetInterfaceHidden(false);
                return;
            }
            RobotInspectionReport report = _inspection.Report;
            int rows = report != null && report.HasPartData ? report.Parts.Count : 1;
            var area = new Rect(16f, 16f, 360f, Mathf.Min(height - 32f, 214f + rows * 22f));
            GUI.Box(area, string.Empty);
            float x = area.x + 12f, w = area.width - 24f, y = area.y + 8f;

            bool player = _inspection.SelectedRobot == PostMatchInspection.PlayerRobot;
            GUI.Label(new Rect(x, y, w, 26f), "ROBOT İNCELEME - " + (player ? "Oyuncu" : "Rakip"), _titleStyle);
            y += 30f;
            GUI.enabled = !player;
            if (GUI.Button(new Rect(x, y, 162f, 30f), "Oyuncu")) _inspection.Select(PostMatchInspection.PlayerRobot);
            GUI.enabled = player;
            if (GUI.Button(new Rect(x + 174f, y, 162f, 30f), "Rakip")) _inspection.Select(PostMatchInspection.OpponentRobot);
            GUI.enabled = true;
            y += 38f;

            if (report != null)
            {
                GUI.Label(new Rect(x, y, w, 22f), $"{report.RobotName}   Can %{Mathf.RoundToInt(report.HealthRatio * 100f)}");
                y += 22f;
                string summary = report.IsUndamaged ? "Hasar yok"
                    : report.DetachedCount == 0 ? "Kopan parça yok"
                    : $"Kopan parça: {report.DetachedCount} (kırmızı çerçeve)";
                GUI.Label(new Rect(x, y, w, 22f), summary + (report.WeaponOperational ? "   Silah çalışıyor" : "   Silah devre dışı"));
                y += 26f;
                if (!report.HasPartData)
                {
                    GUI.Label(new Rect(x, y, w, 22f), "Parça bilgisi yok");
                    y += 22f;
                }
                else
                    foreach (RobotInspectionReport.PartState part in report.Parts)
                    {
                        GUIStyle style = part.Detached ? _missingPartStyle : _partStyle;
                        GUI.Label(new Rect(x, y, 200f, 22f), part.DisplayName, style);
                        GUI.Label(new Rect(x + 200f, y, w - 200f, 22f), part.Condition, style);
                        y += 22f;
                    }
            }

            y += 6f;
            var playerHealth = _match.Player.GetComponent<RobotHealth>();
            var opponentHealth = _match.Opponent.GetComponent<RobotHealth>();
            GUI.Label(new Rect(x, y, w, 22f),
                $"Maç {_match.MatchCount}   Sonuç sayısı {_match.ResultCount}   Can {playerHealth.CurrentHealth:0} / {opponentHealth.CurrentHealth:0}");
            y += 28f;
            if (GUI.Button(new Rect(x, y, 162f, 32f), "Arayüzü gizle (H)")) _inspection.SetInterfaceHidden(true);
            if (GUI.Button(new Rect(x + 174f, y, 162f, 32f), "Çık")) _inspection.Stop();
        }

        private void DrawReplayBar(float height)
        {
            var area = new Rect(16f, height - 160f, 560f, 144f);
            GUI.Box(area, string.Empty);
            string state = _replay.IsPaused ? "duraklatıldı" : _replay.IsFinished ? "bitti" : _replay.IsSlowMotion ? "0.5x" : "1x";
            GUI.Label(new Rect(area.x + 12f, area.y + 6f, area.width - 24f, 26f),
                $"SON VURUŞ TEKRARI   {_replay.PlaybackTime:0.0} / {_replay.Duration:0.0} sn   ({state})", _titleStyle);

            float seek = GUI.HorizontalSlider(new Rect(area.x + 12f, area.y + 40f, area.width - 24f, 18f),
                _replay.PlaybackTime, 0f, Mathf.Max(0.01f, _replay.Duration));
            if (!Mathf.Approximately(seek, _replay.PlaybackTime)) _replay.Seek(seek);

            float buttonY = area.y + 62f;
            if (GUI.Button(new Rect(area.x + 12f, buttonY, 170f, 36f), "Tekrar oynat")) _replay.Restart();
            if (GUI.Button(new Rect(area.x + 195f, buttonY, 170f, 36f), _replay.IsSlowMotion ? "Normal hız" : "Yavaş (0.5x)"))
                _replay.SetSlowMotion(!_replay.IsSlowMotion);
            if (GUI.Button(new Rect(area.x + 378f, buttonY, 170f, 36f), "Çık")) _replay.Stop();

            var playerHealth = _match.Player.GetComponent<RobotHealth>();
            var opponentHealth = _match.Opponent.GetComponent<RobotHealth>();
            GUI.Label(new Rect(area.x + 12f, area.y + 106f, area.width - 24f, 26f),
                $"Maç {_match.MatchCount}   Sonuç sayısı {_match.ResultCount}   Can {playerHealth.CurrentHealth:0} / {opponentHealth.CurrentHealth:0}");
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            _partStyle = new GUIStyle(GUI.skin.label);
            _missingPartStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _missingPartStyle.normal.textColor = new Color(1f, 0.4f, 0.35f);
        }
    }
}
