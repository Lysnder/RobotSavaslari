using System.Collections.Generic;
using RobotSavaslari.Arena;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
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
        private Vector2 _arenaScroll;
        private ArenaEnvironment _arena;
        private ArenaLayout _pendingArena;
        private bool _arenaStartError;
        private ArenaLayout SelectedArena => _pendingArena != null ? _pendingArena : _arena != null ? _arena.Layout : null;
        private DrivingFeatureInstaller _driving;
        private OpponentSelectionPanel _opponentPanel;
        private MatchReplayArchiveScreen _archive;

        private static readonly float[] RoundLimits = { 0f, 60f, 120f, 180f };
        private static readonly string[] RoundNames = { "Süresiz", "60 sn", "120 sn", "180 sn" };
        private const float SetupWidth = 360f;
        private const float Outer = 24f;

        public static float ReadyScale(float width, float height) => ArenaMenuStyle.Scale(width, height);

        public static Rect ReadyArchiveRect(float width, float height) =>
            new Rect(width - Outer - SetupWidth * .5f + 4f, height - 85f, SetupWidth * .5f - 24f, 38f);

        public bool TryCloseHistory()
        {
            if (!_showHistory) return false;
            _showHistory = false;
            return true;
        }

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
            if (_match.State == MatchController.MatchState.Ready && enter)
                StartLobbyMatch();
            else if (_match.State == MatchController.MatchState.Result && keyboard.rKey.wasPressedThisFrame)
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
                float scale = IsShowingReadyMenu
                    ? ReadyScale(Screen.width, Screen.height)
                    : Mathf.Max(0.01f, Mathf.Min(Screen.height / ReferenceHeight, Screen.width / 600f));
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
                    if (IsShowingReadyMenu) DrawReady(width, height, challenge);
                    return;
                }

                if (GUI.Button(new Rect(width - 174f, 16f, 158f, 36f), "Duraklat"))
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

        private void DrawReady(float width, float height, ChallengeSessionController challenge)
        {
            var theme = ArenaMenuStyle.Current;
            if (_arena == null) _arena = FindAnyObjectByType<ArenaEnvironment>();
            if (_driving == null) _driving = FindAnyObjectByType<DrivingFeatureInstaller>();
            if (_opponentPanel == null) _opponentPanel = GetComponent<OpponentSelectionPanel>();
            if (_archive == null) _archive = GetComponent<MatchReplayArchiveScreen>();

            ArenaMenuStyle.DrawGarage(width, height);
            var dock = new Rect(Outer, Outer, 316f, height - Outer * 2f);
            ArenaMenuStyle.Panel(dock);
            ArenaMenuStyle.Fill(new Rect(dock.x, dock.y, dock.width, 3f), ArenaMenuStyle.Accent);
            ArenaMenuStyle.HazardStripe(new Rect(dock.x + 22f, dock.yMax - 24f, 88f, 8f));

            float x = dock.x + 22f;
            float w = dock.width - 44f;
            GUI.Label(new Rect(x, 58, w, 114), "ROBOT\nSAVAŞLARI", theme.Logo);
            if (GUI.Button(new Rect(x, 190, w, 56), "Maça başla", theme.Primary))
            {
                StartLobbyMatch();
                return;
            }

            ArenaMenuStyle.Fill(new Rect(x, 270, w, 1), ArenaMenuStyle.Border);
            GUI.Label(new Rect(x, 285, w, 28), "MÜCADELE VE EĞİTİM", theme.Heading);
            if (Button(new Rect(x, 322, w, 46), "Yerel mücadele", challenge != null && challenge.isActiveAndEnabled))
            {
                _challengeScreen.Open(challenge);
                return;
            }

            var trial = _driving != null && _driving.isActiveAndEnabled ? _driving.TimeTrial : null;
            if (Button(new Rect(x, 378, w, 46), "Sürüş eğitimi", trial != null && trial.CanEnter))
            {
                trial.Enter();
                return;
            }

            ArenaMenuStyle.Fill(new Rect(x, height - 126, w, 1), ArenaMenuStyle.Border);
            if (GUI.Button(new Rect(x, height - 106, w * .65f - 4, 49), "Ayarlar", theme.Button))
            {
                _settings.ToggleSettings();
                return;
            }

            if (GUI.Button(new Rect(x + w * .65f + 4, height - 106, w * .35f - 4, 49), "Çıkış", theme.Button))
                QuitGame();

            DrawSetup(new Rect(width - Outer - SetupWidth, Outer, SetupWidth, height - Outer * 2), width, height);
        }

        private static bool Button(Rect rect, string label, bool enabled)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            try { return GUI.Button(rect, label, ArenaMenuStyle.Current.Button); }
            finally { GUI.enabled = previous; }
        }

        private void DrawSetup(Rect dock, float width, float height)
        {
            var theme = ArenaMenuStyle.Current;
            ArenaMenuStyle.Panel(dock);
            float x = dock.x + 20f;
            float w = dock.width - 40f;
            GUI.Label(new Rect(x, 43, w, 44), "Maç kurulumu", theme.Title);

            GUI.Label(new Rect(x, 98, w, 24), "MAÇ ARENASI", theme.Heading);
            DrawArenas(new Rect(x, 125, w, 82));

            MatchRules rules = _match.Rules;
            GUI.Label(new Rect(x, 215, w, 24), "MAÇ BİÇİMİ", theme.Heading);
            if (Choice(new Rect(x, 242, (w - 8) * .5f, 34), "Tek maç", rules.mode == MatchMode.Single))
            {
                rules.mode = MatchMode.Single;
                _match.ConfigureRules(rules);
            }
            if (Choice(new Rect(x + (w + 8) * .5f, 242, (w - 8) * .5f, 34), "İlk 2 galibiyet", rules.mode == MatchMode.FirstToTwo))
            {
                rules.mode = MatchMode.FirstToTwo;
                _match.ConfigureRules(rules);
            }

            GUI.Label(new Rect(x, 288, w, 24), "RAUND SÜRESİ", theme.Heading);
            float cell = (w - 18f) / 4f;
            for (int i = 0; i < RoundLimits.Length; i++)
            {
                if (!Choice(new Rect(x + i * (cell + 6), 315, cell, 34), RoundNames[i],
                    rules.timeLimitSeconds == RoundLimits[i])) continue;
                rules.timeLimitSeconds = RoundLimits[i];
                _match.ConfigureRules(rules);
            }

            GUI.Label(new Rect(x, 353, w, 34), rules.timeLimitSeconds > 0f
                ? "Süre dolunca can oranı sonucu belirler. Eşitlikte beraberlik."
                : "Raund, robotlardan biri yenildiğinde biter.", theme.Hint);

            if (_opponentPanel != null && _opponentPanel.CanSelect)
                DrawOpponent(x, w);

            ArenaMenuStyle.Fill(new Rect(x, height - 103, w, 1), ArenaMenuStyle.Border);
            Rect archive = ReadyArchiveRect(width, height);
            if (GUI.Button(new Rect(x, archive.y, archive.width, archive.height), "Maç geçmişi", theme.Button))
            {
                _historyScroll = Vector2.zero;
                _showHistory = true;
                return;
            }
            if (Button(archive, "Tekrar arşivi", _archive != null && _archive.IsEntryVisible))
                _archive.OpenArchive();
        }

        private void DrawArenas(Rect rect)
        {
            if (_arena == null || _arena.Layouts == null) return;
            IReadOnlyList<ArenaLayout> layouts = _arena.Layouts;
            int rows = (layouts.Count + 1) / 2;
            bool scrolling = rows > 2;
            float w = rect.width - (scrolling ? 18f : 0f);
            if (scrolling)
                _arenaScroll = GUI.BeginScrollView(rect, _arenaScroll, new Rect(0, 0, w, rows * 41f));
            float x = scrolling ? 0 : rect.x;
            float y = scrolling ? 0 : rect.y;
            float cell = (w - 8f) * .5f;
            for (int i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                if (layout == null) continue;
                Rect chip = new Rect(x + i % 2 * (cell + 8f), y + i / 2 * 41f, cell, 34f);
                if (!Choice(chip, layout.displayName, layout == SelectedArena)) continue;
                _pendingArena = layout;
                _arenaStartError = false;
            }
            if (scrolling) GUI.EndScrollView();
            if (_arenaStartError)
                GUI.Label(new Rect(rect.x, 562f, rect.width, 44f), "Bu arena yüklenemedi. Başka bir arena seç.", ArenaMenuStyle.Current.Hint);
        }

        private void StartLobbyMatch()
        {
            if (IsShowingReadyMenu && TryApplyArena()) _match.RequestCountdown();
        }

        private bool TryApplyArena()
        {
            if (_pendingArena == null || _arena == null || _pendingArena == _arena.Layout) return true;
            for (int i = 0; i < _arena.Layouts.Count; i++)
            {
                if (_arena.Layouts[i] != _pendingArena) continue;
                _arenaStartError = !_arena.Select(i);
                return !_arenaStartError;
            }
            _arenaStartError = true;
            return false;
        }

        private void DrawOpponent(float x, float w)
        {
            var theme = ArenaMenuStyle.Current;
            float cell = (w - 8f) * .5f;
            bool aggressive = _opponentPanel.Personality == BotPersonality.Aggressive;
            bool easy = _opponentPanel.Difficulty == BotDifficulty.Easy;
            GUI.Label(new Rect(x, 395, w, 24), "RAKİP DAVRANIŞI", theme.Heading);
            if (Choice(new Rect(x, 423, cell, 34), "Baskıcı", aggressive))
                _opponentPanel.Select(BotPersonality.Aggressive, _opponentPanel.Difficulty);
            if (Choice(new Rect(x + cell + 8, 423, cell, 34), "Karşı saldırı", !aggressive))
                _opponentPanel.Select(BotPersonality.Counter, _opponentPanel.Difficulty);
            GUI.Label(new Rect(x, 462, w, 28), aggressive ? "Yaklaşır ve baskı kurar." : "Açı arar ve fırsatta karşılık verir.", theme.Hint);
            GUI.Label(new Rect(x, 490, w, 24), "ZORLUK", theme.Heading);
            if (Choice(new Rect(x, 517, cell, 34), "Kolay", easy))
                _opponentPanel.Select(_opponentPanel.Personality, BotDifficulty.Easy);
            if (Choice(new Rect(x + cell + 8, 517, cell, 34), "Zor", !easy))
                _opponentPanel.Select(_opponentPanel.Personality, BotDifficulty.Hard);
        }

        private static bool Choice(Rect rect, string label, bool selected)
        {
            var theme = ArenaMenuStyle.Current;
            bool pressed = GUI.Button(rect, label, selected ? theme.Selected : theme.Chip);
            if (selected)
                ArenaMenuStyle.Fill(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), ArenaMenuStyle.Accent);
            return pressed && !selected;
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
            string next = !_match.EncounterComplete ? (_match.Outcome == MatchController.MatchOutcome.Draw ? "Raundu tekrarla" : "Sonraki raund")
                : _match.Rules.mode == MatchMode.FirstToTwo ? "Yeni seri" : "Yeni maç";
            if (GUI.Button(left, next, _buttonStyle)) _match.RequestCountdown();
            if (GUI.Button(right, "Ana menü", _buttonStyle)) _match.Prepare();
            if (!string.IsNullOrEmpty(_match.HistoryNotice)) GUI.Label(new Rect(16, 175, 600, 42), _match.HistoryNotice);
        }

        private static void DrawStatistics(Rect rect, string name, RobotMatchStatistics value)
        {
            GUI.Label(rect, $"{name}: Can {value.remainingHealth:0.#}/{value.maximumHealth:0.#} | Verilen {value.dealtDamage:0.#} | Alınan {value.receivedDamage:0.#} | Kopan parça {value.detachedParts}");
        }

        private void DrawHistory(float width, float height)
        {
            var panel = new Rect((width - 560) * .5f, 70, 560, Mathf.Min(480, height - 220));
            var theme = ArenaMenuStyle.Current;
            ArenaMenuStyle.DrawGarage(width, height, ArenaMenuStyle.Canvas);
            ArenaMenuStyle.Panel(panel);
            GUI.Label(new Rect(panel.x + 16, panel.y + 8, 528, 32), "Maç geçmişi", theme.Title);
            var records = _match.HistoryStore.Load();
            GUI.Label(new Rect(panel.x + 16, panel.y + 38, 528, 34),
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
                if (GUI.Button(new Rect(380, y + 8, 116, 38), "Rövanş"))
                {
                    _showHistory = false;
                    if (TryApplyArena() && !_match.Rematch(record)) _showHistory = true;
                }
            }
            GUI.EndScrollView();
            if (GUI.Button(new Rect(panel.x + 180, panel.yMax - 52, 200, 36), "Geri", theme.Button)) _showHistory = false;
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
