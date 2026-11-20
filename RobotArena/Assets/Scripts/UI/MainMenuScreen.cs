using RobotSavaslari.Match;
using RobotSavaslari.Driving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.UI
{
    /// <summary>The menu, countdown and match HUD share the arena's existing match and robots.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MatchController), typeof(SettingsPanelController))]
    public sealed class MainMenuScreen : MonoBehaviour
    {
        private bool BindingsOpen => _match.Player != null &&
            _match.Player.TryGetComponent<DriveBindingsScreen>(out var bindings) && bindings.IsOpen;

        public bool IsShowingReadyMenu => isActiveAndEnabled && _match != null && _match.isActiveAndEnabled &&
            _match.State == MatchController.MatchState.Ready && !_match.IsPaused && !_showHistory && !BindingsOpen && !ChallengeDialogOpen;

        private const float ReferenceHeight = 720f;
        private MatchController _match;
        private SettingsPanelController _settings;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _resultStyle;
        private GUIStyle _countdownStyle;
        private ChallengeSessionController Challenge => GetComponent<ChallengeSessionController>();
        private bool ChallengeDialogOpen => Challenge != null && Challenge.DialogOpen;
        private readonly ChallengeScreen _challengeScreen = new ChallengeScreen();
        private bool _showHistory;
        private Vector2 _historyScroll;

        private void Awake()
        {
            _match = GetComponent<MatchController>();
            _settings = GetComponent<SettingsPanelController>();
        }

        private void OnDisable() => Challenge?.Close();

        private void Update()
        {
            if (BindingsOpen || ChallengeDialogOpen) return;
            var keyboard = Keyboard.current;
            if (!_match.isActiveAndEnabled || !_match.IsInitialized || _showHistory || _settings.IsOpen || keyboard == null ||
                keyboard.escapeKey.wasPressedThisFrame) return;

            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            if (Challenge != null && Challenge.InChallenge)
            {
                if (keyboard.rKey.wasPressedThisFrame && !_match.EncounterComplete) _match.ContinueSeries();
                return;
            }
            if ((_match.State == MatchController.MatchState.Ready && enter) ||
                (_match.State == MatchController.MatchState.Result && keyboard.rKey.wasPressedThisFrame))
                _match.RequestCountdown();
        }

        private void OnGUI()
        {
            if (!_match.isActiveAndEnabled || !_match.IsInitialized || _settings.IsOpen) return;
            EnsureStyles();
            if (BindingsOpen) return;
            Matrix4x4 previous = GUI.matrix;
            try
            {
                float scale = Mathf.Max(0.01f, Mathf.Min(Screen.height / ReferenceHeight, Screen.width / 600f));
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                var challenge = Challenge;
                if (challenge != null && challenge.DialogOpen)
                {
                    _challengeScreen.DrawDialog(challenge, width, height);
                    return;
                }
                if (_showHistory && _match.State == MatchController.MatchState.Ready)
                {
                    DrawHistory(width, height);
                    return;
                }
                if (_match.State == MatchController.MatchState.Ready)
                {
                    DrawRules(width);
                    DrawMenu(width, height);
                    if (challenge != null && IsShowingReadyMenu) _challengeScreen.DrawEntry(challenge, width);
                    return;
                }

                if (GUI.Button(new Rect(width - 174f, 16f, 158f, 36f), "Duraklat (Esc)"))
                    _settings.ToggleSettings();
                if (_settings.IsOpen) return;

                if (challenge != null && challenge.InChallenge)
                    ChallengeScreen.DrawProgress(challenge, width);

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
            GUILayout.BeginArea(new Rect(16, 16, 430, 150), GUI.skin.box);
            string mode = _match.Rules.mode == MatchMode.Single ? "Tek maç" : $"Seri {_match.PlayerWins}-{_match.OpponentWins} | Raund {_match.RoundNumber}";
            GUILayout.Label(mode + " | Maç " + _match.MatchCount);
            Color previousColor = GUI.color;
            if (_match.IsTimeWarning) GUI.color = new Color(1f, .75f, .2f);
            GUILayout.Label(_match.IsTimed ? $"Kalan: {Mathf.CeilToInt(_match.RemainingSeconds)} sn" : $"Süre: {_match.ElapsedSeconds:0.0} sn");
            GUI.color = previousColor;
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
            // Keep the height used by the existing replay/inspection controls.
            var panel = new Rect((width - 560f) * .5f, (height - 218f) * .5f, 560f, 218f);
            GUI.Box(panel, string.Empty);
            string title = _match.IsSeriesComplete ? "Seri bitti - " + _match.ResultText : _match.ResultText;
            _resultStyle.fontSize = _match.IsSeriesComplete ? 22 : 24;
            GUI.Label(new Rect(panel.x, panel.y + 4f, panel.width, 40f), title, _resultStyle);
            var result = _match.LastRound;
            if (result != null)
            {
                GUI.Label(new Rect(panel.x + 12, panel.y + 44, 536, 22),
                    $"Raund {result.roundNumber} | {result.durationSeconds:0.0} sn | Skor {_match.PlayerWins}-{_match.OpponentWins} | Berabere: {_match.DrawnRounds}");
                DrawStatistics(new Rect(panel.x + 12, panel.y + 68, 536, 22), "Oyuncu", result.player);
                DrawStatistics(new Rect(panel.x + 12, panel.y + 92, 536, 22), "Rakip", result.opponent);
                string summary = _match.LastMatch != null
                    ? $"Toplam {_match.LastMatch.durationSeconds:0.0} sn | Verilen hasar {_match.LastMatch.player.dealtDamage:0.#} / {_match.LastMatch.opponent.dealtDamage:0.#}"
                    : "Seri sürüyor; beraberlikte aynı raund tekrar edilir.";
                GUI.Label(new Rect(panel.x + 12, panel.y + 116, 536, 22), summary);
            }
            var challenge = Challenge;
            var left = new Rect(panel.x + 12, panel.y + 154, 262, 44);
            var right = new Rect(panel.x + 286, panel.y + 154, 262, 44);
            if (challenge != null && challenge.InChallenge)
            {
                _challengeScreen.DrawResultButtons(challenge, _match, left, right, _buttonStyle);
                if (!string.IsNullOrEmpty(challenge.Notice)) GUI.Label(new Rect(16, 224, width - 32, 24), challenge.Notice);
                if (!string.IsNullOrEmpty(_match.HistoryNotice)) GUI.Label(new Rect(16, 250, width - 32, 24), _match.HistoryNotice);
                return;
            }
            string next = !_match.EncounterComplete ? (_match.Outcome == MatchController.MatchOutcome.Draw ? "Raundu tekrarla (R)" : "Sonraki raund (R)")
                : _match.Rules.mode == MatchMode.FirstToTwo ? "Yeni seri (R)" : "Yeni maç (R)";
            if (GUI.Button(left, next, _buttonStyle)) _match.RequestCountdown();
            if (GUI.Button(right, "Ana menü", _buttonStyle)) _match.Prepare();
            if (!string.IsNullOrEmpty(_match.HistoryNotice)) GUI.Label(new Rect(16, 175, 600, 42), _match.HistoryNotice);
        }

        private static void DrawStatistics(Rect rect, string name, RobotMatchStatistics value)
        {
            GUI.Label(rect, $"{name}: Can {value.remainingHealth:0.#}/{value.maximumHealth:0.#} | Verilen {value.dealtDamage:0.#} | Alınan {value.receivedDamage:0.#} | Kopan parça {value.detachedParts}");
        }

        private void DrawRules(float width)
        {
            var rules = _match.Rules;
            GUI.Box(new Rect(16, 16, 430, 130), "Karşılaşma kuralları");
            bool single = rules.mode == MatchMode.Single;
            if (GUI.Toggle(new Rect(30, 46, 180, 26), single, "Tek maç") && !single)
            { rules.mode = MatchMode.Single; _match.ConfigureRules(rules); }
            if (GUI.Toggle(new Rect(220, 46, 210, 26), !single, "İlk 2 galibiyet") && single)
            { rules.mode = MatchMode.FirstToTwo; _match.ConfigureRules(rules); }
            GUI.Label(new Rect(30, 78, 110, 26), "Raund süresi:");
            float[] limits = { 0, 60, 120, 180 };
            for (int i = 0; i < limits.Length; i++)
            {
                float limit = limits[i];
                bool selected = rules.timeLimitSeconds == limit;
                if (GUI.Toggle(new Rect(140 + i * 72, 78, 72, 26), selected, limit == 0 ? "Süresiz" : limit + " sn") && !selected)
                { rules.timeLimitSeconds = limit; _match.ConfigureRules(rules); }
            }
            GUI.Label(new Rect(30, 108, 400, 24), "Süre dolunca can oranı; eşitse beraberlik.");
            if (GUI.Button(new Rect(width - 174, 16, 158, 36), "Maç geçmişi")) _showHistory = true;
        }

        private void DrawHistory(float width, float height)
        {
            var panel = new Rect((width - 560) * .5f, 70, 560, Mathf.Min(480, height - 220));
            GUI.Box(panel, "Yerel maç geçmişi");
            var records = _match.HistoryStore.Load();
            GUI.Label(new Rect(panel.x + 16, panel.y + 28, 528, 42),
                "Rövanş temel maç kurallarını kullanır. Mevcut arena ve rakip seçimi korunur.");
            if (records.Count == 0) GUI.Label(new Rect(panel.x + 16, panel.y + 76, 520, 44),
                _match.HistoryStore.LastError ?? "Henüz tamamlanmış karşılaşma yok.");
            _historyScroll = GUI.BeginScrollView(new Rect(panel.x + 12, panel.y + 78, 536, panel.height - 142),
                _historyScroll, new Rect(0, 0, 510, Mathf.Max(1, records.Count) * 90));
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i]; float y = i * 90;
                string mode = record.rules.mode == MatchMode.Single ? "Tek maç" : "Seri";
                string outcome = record.outcome == MatchController.MatchOutcome.PlayerVictory ? "Galibiyet"
                    : record.outcome == MatchController.MatchOutcome.OpponentVictory ? "Mağlubiyet" : "Berabere";
                GUI.Label(new Rect(6, y, 360, 24), $"{mode}: {outcome} | {record.playerWins}-{record.opponentWins} | {record.durationSeconds:0.0} sn");
                GUI.Label(new Rect(6, y + 26, 360, 24), record.endedAtUtc + (record.rules.timeLimitSeconds == 0 ? " | Süresiz" : $" | {record.rules.timeLimitSeconds:0} sn"));
                if (GUI.Button(new Rect(380, y + 8, 116, 38), "Rövanş") && _match.Rematch(record)) _showHistory = false;
            }
            GUI.EndScrollView();
            if (GUI.Button(new Rect(panel.x + 180, panel.yMax - 52, 200, 36), "Kapat")) _showHistory = false;
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
