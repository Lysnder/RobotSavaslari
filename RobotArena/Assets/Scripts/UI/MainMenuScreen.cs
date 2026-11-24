using System.Collections.Generic;
using RobotSavaslari.Arena;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using RobotSavaslari.Driving;
using RobotSavaslari.Training;
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
            _match.State == MatchController.MatchState.Ready && !_match.IsPaused && !_showHistory && !BindingsOpen && !ChallengeDialogOpen && !WorkshopOpen;

        private WorkshopScreen _workshop;
        public bool WorkshopOpen => _workshop != null;

        public bool OpenWorkshop()
        {
            if (!IsShowingReadyMenu || _settings.IsOpen) return false;
            var selection = GetComponent<WorkshopSelection>();
            if (!selection || !selection.CanSelect)
            {
                _setupNotice = "Atölye seçimi şu anda kullanılamıyor.";
                return false;
            }
            var kit = WorkshopKit.Load();
            if (!kit || !kit.HasParts)
            {
                _setupNotice = "Atölye parçası eksik. Hazır robot seçebilirsiniz.";
                return false;
            }
            _workshop = new WorkshopScreen(selection, kit);
            if (!_workshop.Draft.IsValid)
            {
                CloseWorkshop();
                _setupNotice = "Şasi ve parça yuvaları doğrulanamadı. Hazır robot seçebilirsiniz.";
                return false;
            }
            _setupNotice = null;
            return true;
        }

        public bool SetWorkshopDraft(WorkshopLoadout value) => WorkshopOpen && _workshop.SetDraft(value);

        public bool ApplyWorkshop()
        {
            if (!WorkshopOpen || _match.State != MatchController.MatchState.Ready) return false;
            var current = _workshop;
            _workshop = null;
            if (GetComponent<WorkshopSelection>().TryApply(current.Draft))
            {
                current.Dispose();
                return true;
            }
            _workshop = current;
            return false;
        }

        public bool CloseWorkshop()
        {
            if (!WorkshopOpen) return false;
            _workshop.Dispose();
            _workshop = null;
            return true;
        }

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
        private string _setupNotice;
        private bool _loadedSetup;
        private bool _protectPreference;
        private DuelSetup _playedSetup;
        private string _playedEncounter;
        private readonly DuelSetup[] _presets = { DuelSetup.Quick(), DuelSetup.Tactical(), DuelSetup.Series() };
        private static readonly string[] PresetNames = { "Hızlı düello", "Taktik düello", "Seri" };
        private ArenaLayout SelectedArena => _pendingArena != null ? _pendingArena : _arena != null ? _arena.Layout : null;
        private DrivingFeatureInstaller _driving;
        private bool _showExercises;
        private Vector2 _exerciseScroll;
        private string _exerciseNotice;
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

        private void OnDisable()
        {
            CloseWorkshop();
            Challenge?.Close();
        }

        private void Update()
        {
            if (WorkshopOpen && _match.State != MatchController.MatchState.Ready) CloseWorkshop();
            if (WorkshopOpen || BindingsOpen || ChallengeDialogOpen) return;
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
                ContinueResult();
        }

        private void OnGUI()
        {
            if (!_match.isActiveAndEnabled || !_match.IsInitialized || _settings.IsOpen) return;
            EnsureStyles();
            if (BindingsOpen) return;
            Matrix4x4 previous = GUI.matrix;
            try
            {
                float scale = IsShowingReadyMenu || WorkshopOpen
                    ? ReadyScale(Screen.width, Screen.height)
                    : Mathf.Max(0.01f, Mathf.Min(Screen.height / ReferenceHeight, Screen.width / 600f));
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                if (WorkshopOpen) { _workshop.Draw(this, width, height); return; }
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
            EnsureLobbySetup();
            if (_driving == null) _driving = FindAnyObjectByType<DrivingFeatureInstaller>();
            if (_archive == null) _archive = GetComponent<MatchReplayArchiveScreen>();

            ArenaMenuStyle.DrawGarage(width, height);
            var dock = new Rect(Outer, Outer, 316f, height - Outer * 2f);
            ArenaMenuStyle.Panel(dock);
            ArenaMenuStyle.Fill(new Rect(dock.x, dock.y, dock.width, 3f), ArenaMenuStyle.Accent);
            ArenaMenuStyle.HazardStripe(new Rect(dock.x + 22f, dock.yMax - 24f, 88f, 8f));

            float x = dock.x + 22f;
            float w = dock.width - 44f;
            GUI.Label(new Rect(x, 58, w, 114), "ROBOT\nSAVAŞLARI", theme.Logo);
            if (GUI.Button(new Rect(x, 190, w * .59f - 4, 56), "Maça başla", theme.Primary))
            {
                StartLobbyMatch();
                return;
            }

            if (GUI.Button(new Rect(x + w * .59f + 4, 190, w * .41f - 4, 56), "Atölye", theme.Button))
            {
                OpenWorkshop();
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
            if (trial != null && DrawExerciseMenu(trial, x, w)) return;

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

        private bool DrawExerciseMenu(TimeTrialSession trial, float x, float width)
        {
            var exercises = _driving.ExerciseCatalog?.exercises;
            if (exercises == null || exercises.Length == 0) return false;
            if (Button(new Rect(x, 434, width, 46),
                _showExercises ? "Alıştırmaları kapat" : "Sürüş alıştırmaları", trial.CanEnter))
            {
                _showExercises = !_showExercises;
                _exerciseNotice = null;
            }
            if (!_showExercises || !trial.CanEnter) return false;

            float cell = (width - 8f) * .5f;
            float rowsHeight = Mathf.CeilToInt(exercises.Length * .5f) * 37f;
            var theme = ArenaMenuStyle.Current;
            var rows = new Rect(x, 488f, width, 74f);
            bool scroll = rowsHeight > rows.height;
            if (scroll)
            {
                _exerciseScroll = GUI.BeginScrollView(rows, _exerciseScroll, new Rect(0f, 0f, width - 18f, rowsHeight));
                cell = (width - 26f) * .5f;
            }
            Vector2 origin = scroll ? Vector2.zero : rows.position;
            bool entered = false;
            for (int i = 0; i < exercises.Length; i++)
            {
                var exercise = exercises[i];
                if (exercise == null) continue;
                var rect = new Rect(origin.x + i % 2 * (cell + 8f), origin.y + i / 2 * 37f, cell, 32f);
                if (!GUI.Button(rect, exercise.title, theme.Chip)) continue;
                entered = trial.EnterExercise(exercise);
                if (entered)
                {
                    _showExercises = false;
                    _exerciseNotice = null;
                    break;
                }
                _exerciseNotice = "Bu arenada güvenli rota bulunamadı.";
            }
            if (scroll) GUI.EndScrollView();
            if (_exerciseNotice != null)
                GUI.Label(new Rect(x, 562f, width, 30f), _exerciseNotice, theme.Hint);
            return entered;
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
                EditSetup(rules: rules);
            }
            if (Choice(new Rect(x + (w + 8) * .5f, 242, (w - 8) * .5f, 34), "İlk 2 galibiyet", rules.mode == MatchMode.FirstToTwo))
            {
                rules.mode = MatchMode.FirstToTwo;
                EditSetup(rules: rules);
            }

            GUI.Label(new Rect(x, 288, w, 24), "RAUND SÜRESİ", theme.Heading);
            float cell = (w - 18f) / 4f;
            for (int i = 0; i < RoundLimits.Length; i++)
            {
                if (!Choice(new Rect(x + i * (cell + 6), 315, cell, 34), RoundNames[i],
                    rules.timeLimitSeconds == RoundLimits[i])) continue;
                rules.timeLimitSeconds = RoundLimits[i];
                EditSetup(rules: rules);
            }

            GUI.Label(new Rect(x, 353, w, 38), _setupNotice ?? (rules.timeLimitSeconds > 0f
                ? "Süre dolunca can oranı sonucu belirler. Eşitlikte beraberlik."
                : "Raund, robotlardan biri yenildiğinde biter."), theme.Hint);

            if (_opponentPanel != null && _opponentPanel.CanSelect)
                DrawOpponent(x, w);

            var current = CaptureSetup();
            GUI.Label(new Rect(x, 551f, w, 22f), "HAZIR DÜELLO", theme.Heading);
            float presetWidth = (w - 12f) / 3f;
            for (int i = 0; i < _presets.Length; i++)
                if (Choice(new Rect(x + i * (presetWidth + 6f), 572f, presetWidth, 34f),
                    PresetNames[i], !_protectPreference && _presets[i].SameAs(current))) SelectPreset(_presets[i]);

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
                EditSetup(layout: layout);
            }
            if (scrolling) GUI.EndScrollView();
        }

        private void EnsureLobbySetup()
        {
            if (_arena == null) _arena = FindAnyObjectByType<ArenaEnvironment>();
            if (_opponentPanel == null) _opponentPanel = GetComponent<OpponentSelectionPanel>();
            if (_loadedSetup || !IsShowingReadyMenu || _arena?.Layout == null || _opponentPanel == null) return;
            _loadedSetup = true;
            if (!PlayerPrefs.HasKey(DuelSetup.PreferenceKey)) return;
            var stored = DuelSetup.Read(PlayerPrefs.GetString(DuelSetup.PreferenceKey));
            if (stored != null && StageSetup(stored)) return;
            _protectPreference = true;
            _setupNotice = "Kayıtlı kurulum okunamadı. Yeni tercih kaydetmek için hazır düello seç.";
        }

        private DuelSetup CaptureSetup() => SelectedArena == null || _opponentPanel == null ? null :
            DuelSetup.Create(SelectedArena.name, _opponentPanel.Personality, _opponentPanel.Difficulty, _match.Rules);

        private ArenaLayout ResolveArena(DuelSetup selection)
        {
            if (selection == null || !selection.Valid || _arena?.Layouts == null) return null;
            foreach (var layout in _arena.Layouts)
                if (layout != null && layout.name == selection.arenaId) return layout;
            return null;
        }

        public bool SelectPreset(DuelSetup selection)
        {
            EnsureLobbySetup();
            if (!StageSetup(selection)) return false;
            _protectPreference = false;
            SavePreference(selection);
            return true;
        }

        private void EditSetup(ArenaLayout layout = null, BotPersonality? bot = null, BotDifficulty? level = null, MatchRules rules = null)
        {
            if (_opponentPanel == null || SelectedArena == null) return;
            SelectPreset(DuelSetup.Create((layout ?? SelectedArena).name, bot ?? _opponentPanel.Personality,
                level ?? _opponentPanel.Difficulty, rules ?? _match.Rules));
        }

        private bool StageSetup(DuelSetup selection)
        {
            var layout = ResolveArena(selection);
            if (!IsShowingReadyMenu || layout == null || _opponentPanel == null || !_opponentPanel.CanSelect ||
                ArenaLayoutValidator.Validate(layout, _arena.ClearanceRadius).Count != 0)
            {
                _setupNotice = "Kurulum kullanılamıyor. Başka bir arena veya hazır düello seç.";
                return false;
            }
            var currentRules = _match.Rules;
            if (currentRules.mode != selection.mode || currentRules.timeLimitSeconds != selection.seconds)
                _match.ConfigureRules(selection.Rules);
            _opponentPanel.Select(selection.personality, selection.difficulty);
            _pendingArena = layout;
            _setupNotice = null;
            return true;
        }

        public void StartLobbyMatch()
        {
            if (!IsShowingReadyMenu || !_match.IsInitialized || (Challenge != null && Challenge.InChallenge)) return;
            EnsureLobbySetup();
            var setup = CaptureSetup();
            if (setup == null || !setup.Valid || !TryApplyArena()) return;
            if (_match.RequestCountdown())
            {
                if (!_protectPreference) _setupNotice = null;
                RememberEncounter(setup);
            }
        }

        private void RememberEncounter(DuelSetup setup)
        {
            _playedSetup = setup;
            _playedEncounter = _match.EncounterId;
            if (!_protectPreference) SavePreference(setup);
        }

        private static void SavePreference(DuelSetup setup)
        {
            if (setup == null) return;
            PlayerPrefs.SetString(DuelSetup.PreferenceKey, JsonUtility.ToJson(setup));
            PlayerPrefs.Save();
        }

        private bool TryApplyArena()
        {
            if (_arena?.Layout == null || _pendingArena == null || _pendingArena == _arena.Layout) return _arena?.Layout != null;
            for (int i = 0; i < _arena.Layouts.Count; i++)
                if (_arena.Layouts[i] == _pendingArena && _arena.Select(i)) return true;
            _setupNotice = "Bu arena yüklenemedi. Başka bir arena seç.";
            return false;
        }

        public void ContinueResult()
        {
            if (!isActiveAndEnabled || _match.State != MatchController.MatchState.Result || _match.IsPaused ||
                BindingsOpen || (Challenge != null && Challenge.InChallenge)) return;
            if (!_match.EncounterComplete || _playedEncounter != _match.EncounterId || _playedSetup == null)
            {
                _match.RequestCountdown();
                return;
            }
            var layout = ResolveArena(_playedSetup);
            if (layout == null || ArenaLayoutValidator.Validate(layout, _arena.ClearanceRadius).Count != 0)
            {
                _setupNotice = "Oynanan kurulum yeniden yüklenemedi.";
                return;
            }
            _match.Prepare();
            if (StageSetup(_playedSetup)) StartLobbyMatch();
        }

        private void DrawOpponent(float x, float w)
        {
            var theme = ArenaMenuStyle.Current;
            float cell = (w - 8f) * .5f;
            bool aggressive = _opponentPanel.Personality == BotPersonality.Aggressive;
            bool easy = _opponentPanel.Difficulty == BotDifficulty.Easy;
            GUI.Label(new Rect(x, 395, w, 24), "RAKİP DAVRANIŞI", theme.Heading);
            if (Choice(new Rect(x, 423, cell, 34), "Baskıcı", aggressive))
                EditSetup(bot: BotPersonality.Aggressive);
            if (Choice(new Rect(x + cell + 8, 423, cell, 34), "Karşı saldırı", !aggressive))
                EditSetup(bot: BotPersonality.Counter);
            GUI.Label(new Rect(x, 462, w, 28), aggressive ? "Yaklaşır ve baskı kurar." : "Açı arar ve fırsatta karşılık verir.", theme.Hint);
            GUI.Label(new Rect(x, 490, w, 24), "ZORLUK", theme.Heading);
            if (Choice(new Rect(x, 517, cell, 34), "Kolay", easy))
                EditSetup(level: BotDifficulty.Easy);
            if (Choice(new Rect(x + cell + 8, 517, cell, 34), "Zor", !easy))
                EditSetup(level: BotDifficulty.Hard);
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
            ArenaMenuStyle.Panel(panel);
            string title = _match.IsSeriesComplete ? "Seri bitti - " + _match.ResultText : _match.ResultText;
            _resultStyle.fontSize = _match.IsSeriesComplete ? 22 : 24;
            GUI.Label(new Rect(panel.x, panel.y + 4f, panel.width, 40f), title, _resultStyle);
            var result = _match.LastRound;
            if (result != null)
            {
                GUI.Label(new Rect(panel.x + 12, panel.y + 44, 536, 22),
                    $"Raund {result.roundNumber} · {(result.reason == RoundFinishReason.Defeat ? "Nakavt" : "Süre doldu · can oranı")} | {result.durationSeconds:0.0} sn | Skor {_match.PlayerWins}-{_match.OpponentWins} | Berabere: {_match.DrawnRounds}");
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
            if (_setupNotice != null) GUI.Label(new Rect(panel.x + 12, panel.y + 135, 536, 20), _setupNotice);
            string next = !_match.EncounterComplete ? (_match.Outcome == MatchController.MatchOutcome.Draw ? "Raundu tekrarla" : "Sonraki raund")
                : "Aynı ayarla yeniden oyna";
            if (GUI.Button(left, next, _buttonStyle)) ContinueResult();
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
                    if (record != null && record.IsValid && TryApplyArena() && _match.Rematch(record))
                        RememberEncounter(CaptureSetup());
                    else _showHistory = true;
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
