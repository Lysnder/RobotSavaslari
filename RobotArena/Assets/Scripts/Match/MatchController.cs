using System;
using RobotSavaslari.Combat;
using RobotSavaslari.Arena;
using RobotSavaslari.Driving;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.Match
{
    // Owns round state for both the production arena and the explicit regression harness.
    [DefaultExecutionOrder(-100)]
    public sealed class MatchController : MonoBehaviour
    {
        public enum MatchState { Ready, Playing, Result, Restarting, Countdown }
        public enum MatchOutcome { None, PlayerVictory, OpponentVictory, Draw }
        [SerializeField] private RobotPrototypeController player;
        [SerializeField] private RobotPrototypeController opponent;
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private Transform opponentSpawn;
        public MatchState State { get; private set; }
        public string EncounterId => encounterId;
        public DuelStart LockedSetup { get; private set; }
        private readonly DuelOpponentLoadout botLoadout = new DuelOpponentLoadout();
        private bool IsMainArena => gameplayInterface && string.Equals(gameObject.scene.name, "MainArena", StringComparison.OrdinalIgnoreCase);
        public int MatchCount { get; private set; }
        public MatchOutcome Outcome { get; private set; }
        public int ResultCount { get; private set; }
        public string ResultText => result;
        public bool IsRestarting => State == MatchState.Restarting;
        public float DemoAttackCooldown { get; private set; }
        public GameObject DemoBotTarget { get; private set; }
        [SerializeField] private bool manualNotificationsOnly = true;
        public bool ManualNotificationsOnly => manualNotificationsOnly;
        [SerializeField] private bool gameplayInterface;
        public RobotPrototypeController Player => player;
        public RobotPrototypeController Opponent => opponent;
        public bool IsInitialized => initialized;
        [SerializeField, Min(1)] private int countdownSeconds = 3;
        [SerializeField, Min(0f)] private float startMessageDuration = 0.8f;
        private float countdownEndsAt;
        private float startMessageEndsAt;
        public float CountdownRemaining => State == MatchState.Countdown ? Mathf.Max(0f, countdownEndsAt - Time.time) : 0f;
        public bool ShowStartMessage => State == MatchState.Playing && Time.time < startMessageEndsAt;

        [SerializeField] private MatchRules rules = new MatchRules();
        private SettingsPanelController settings;
        private PlayerKeyboardDriver pausedDriver;
        private bool driverWasEnabled, pauseObserved, waitForInputRelease;
        private RobotHealth playerHealth, opponentHealth;
        private double lastClock;
        private float elapsedSeconds, totalSeconds;
        private int roundsPlayed, roundAtStart;
        private string encounterId;
        private RobotMatchStatistics playerRound = new RobotMatchStatistics(), opponentRound = new RobotMatchStatistics();
        private RobotMatchStatistics playerTotal = new RobotMatchStatistics(), opponentTotal = new RobotMatchStatistics();
        private PauseCombatGate playerGate, opponentGate;
        private MatchHistoryStore historyStore;

        public MatchRules Rules => rules.Copy();
        public int PlayerWins { get; private set; }
        public int OpponentWins { get; private set; }
        public int DrawnRounds { get; private set; }
        public int RoundNumber => State == MatchState.Result && LastRound != null ? LastRound.roundNumber : PlayerWins + OpponentWins + 1;
        public bool EncounterComplete { get; private set; }
        public bool IsSeriesComplete => rules.mode == MatchMode.FirstToTwo && EncounterComplete;
        public MatchOutcome SeriesOutcome => IsSeriesComplete && LastMatch != null ? LastMatch.outcome : MatchOutcome.None;
        public bool IsPaused => (settings != null && settings.IsOpen) || Time.timeScale <= 0f;
        public float ElapsedSeconds => elapsedSeconds;
        public bool IsTimed => rules.timeLimitSeconds > 0f;
        public float RemainingSeconds => IsTimed ? Mathf.Max(0f, rules.timeLimitSeconds - elapsedSeconds) : 0f;
        public bool IsTimeWarning => State == MatchState.Playing && IsTimed && RemainingSeconds <= 10f;
        public RoundResult LastRound { get; private set; }
        public MatchRecord LastMatch { get; private set; }
        public MatchHistoryStore HistoryStore { get => historyStore ?? (historyStore = new MatchHistoryStore()); set => historyStore = value; }
        public string HistoryNotice { get; private set; }

        public bool ConfigureRules(MatchRules selection)
        {
            if (!initialized || !isActiveAndEnabled || State != MatchState.Ready || IsPaused || selection == null || !selection.IsValid) return false;
            rules = selection.Copy();
            ResetEncounter();
            return true;
        }

        public bool Rematch(MatchRecord record)
        {
            if (!initialized || !isActiveAndEnabled || IsPaused || record == null || !record.IsValid ||
                (State != MatchState.Ready && State != MatchState.Result)) return false;
            Prepare();
            return ConfigureRules(record.rules) && RequestCountdown();
        }

        public bool ContinueSeries()
        {
            return rules.mode == MatchMode.FirstToTwo && !EncounterComplete && State == MatchState.Result && RequestCountdown();
        }

        private void OnEnable() { lastClock = Time.timeAsDouble; }
        private void OnDisable() { RestorePausedDriver(); }

        private sealed class PauseCombatGate : IMatchGate
        {
            public MatchController match;
            public IMatchGate inner;
            public bool player;
            public bool IsOpen => match != null && match.isActiveAndEnabled && match.State == MatchState.Playing &&
                !match.IsPaused && (!player || !match.waitForInputRelease) && inner != null && inner.IsOpen;
        }

        public void ConfigureGameplay()
        {
            gameplayInterface = true;
            manualNotificationsOnly = false;
        }

        private void Update()
        {
            if (!initialized) return;
            SynchronizePause();
            if (IsPaused) return;
            if (State == MatchState.Countdown && Time.time >= countdownEndsAt)
            {
                BeginPlaying();
                startMessageEndsAt = Time.time + startMessageDuration;
            }
            AdvanceClock();
            ResolveTimeLimit();
        }

        private void FixedUpdate()
        {
            if (!initialized || IsPaused) return;
            AdvanceClock();
            ResolveTimeLimit();
        }

        private void AdvanceClock()
        {
            double now = Time.timeAsDouble;
            if (State == MatchState.Playing && !IsPaused && now > lastClock) elapsedSeconds += (float)(now - lastClock);
            lastClock = Math.Max(lastClock, now);
        }

        private void ResolveTimeLimit()
        {
            if (State != MatchState.Playing || !IsTimed || elapsedSeconds < rules.timeLimitSeconds) return;
            elapsedSeconds = rules.timeLimitSeconds;
            CompleteRound(MatchRules.CompareHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth,
                opponentHealth.CurrentHealth, opponentHealth.MaxHealth), RoundFinishReason.TimeOver);
        }

        private void SynchronizePause()
        {
            bool paused = IsPaused;
            if (paused && !pauseObserved)
            {
                pausedDriver = player.GetComponent<PlayerKeyboardDriver>();
                driverWasEnabled = pausedDriver != null && pausedDriver.enabled;
                if (driverWasEnabled) pausedDriver.enabled = false;
            }
            if (!paused && pauseObserved) waitForInputRelease = driverWasEnabled;
            pauseObserved = paused;
            if (!paused && waitForInputRelease && (Keyboard.current == null || !Keyboard.current.anyKey.isPressed)) RestorePausedDriver();
            BindCombatGates();
        }

        private void RestorePausedDriver()
        {
            if (pausedDriver != null && driverWasEnabled && State == MatchState.Playing && player != null &&
                player.isActiveAndEnabled && !player.CombatStopped) pausedDriver.enabled = true;
            pausedDriver = null;
            driverWasEnabled = waitForInputRelease = pauseObserved = false;
        }

        private void BindCombatGates()
        {
            if (State != MatchState.Playing) return;
            BindCombatGate(player, ref playerGate, true);
            BindCombatGate(opponent, ref opponentGate, false);
        }

        private void BindCombatGate(RobotPrototypeController robot, ref PauseCombatGate gate, bool forPlayer)
        {
            var attack = robot.GetComponent<MeleeAttack>();
            if (attack == null || attack.Gate == gate) return;
            if (gate == null) gate = new PauseCombatGate { match = this, player = forPlayer };
            gate.inner = attack.Gate;
            attack.Gate = gate;
        }
        public event System.Action<MatchOutcome> ResultDetermined;
        public bool PlayerAllowed => isActiveAndEnabled && !IsPaused && !waitForInputRelease && State == MatchState.Playing && player != null && !player.CombatStopped;
        public bool OpponentAllowed => isActiveAndEnabled && !IsPaused && State == MatchState.Playing && opponent != null && !opponent.CombatStopped;
        private string playerProbe = "Henuz denenmedi", opponentProbe = "Henuz denenmedi";
        private string result = "";
        private bool initialized;
        private string lastNotification = "Henuz bildirim yok";

        public void Configure(RobotPrototypeController playerRobot, RobotPrototypeController opponentRobot, Transform playerStart, Transform opponentStart)
        {
            player = playerRobot;
            opponent = opponentRobot;
            playerSpawn = playerStart;
            opponentSpawn = opponentStart;
        }

        private void Start()
        {
            if (player == null || opponent == null || player == opponent || playerSpawn == null || opponentSpawn == null)
            {
                Debug.LogError("Maç için iki farklı robot ve iki doğma noktası atanmalıdır.", this);
                enabled = false;
                return;
            }
            settings = GetComponent<SettingsPanelController>();
            playerHealth = player.GetComponent<RobotHealth>();
            opponentHealth = opponent.GetComponent<RobotHealth>();
            playerHealth.Defeated += OnDefeated;
            opponentHealth.Defeated += OnDefeated;
            playerHealth.Damaged += OnPlayerDamaged;
            opponentHealth.Damaged += OnOpponentDamaged;
            if (rules == null || !rules.IsValid) rules = new MatchRules();
            initialized = true;
            SetManualMode(manualNotificationsOnly);
            Prepare();
        }

        private void OnDestroy()
        {
            botLoadout.Dispose();
            if (!initialized) return;
            if (playerHealth != null) { playerHealth.Defeated -= OnDefeated; playerHealth.Damaged -= OnPlayerDamaged; }
            if (opponentHealth != null) { opponentHealth.Defeated -= OnDefeated; opponentHealth.Damaged -= OnOpponentDamaged; }
        }

        public void Prepare()
        {
            if (!initialized || State == MatchState.Playing || State == MatchState.Countdown || IsRestarting) return;
            ResetEncounter();
            PrepareRound();
        }

        private void ResetEncounter()
        {
            LockedSetup = null;
            PlayerWins = OpponentWins = DrawnRounds = roundsPlayed = 0;
            totalSeconds = 0f;
            playerTotal = new RobotMatchStatistics(); opponentTotal = new RobotMatchStatistics();
            EncounterComplete = false;
            encounterId = Guid.NewGuid().ToString("N");
            LastMatch = null;
            HistoryNotice = null;
        }

        private void PrepareRound()
        {
            RestorePausedDriver();
            State = MatchState.Ready;
            elapsedSeconds = 0f; lastClock = Time.timeAsDouble;
            LastRound = null;
            playerRound = new RobotMatchStatistics(); opponentRound = new RobotMatchStatistics();
            countdownEndsAt = startMessageEndsAt = 0f;
            ResetRobot(player, playerSpawn);
            ResetRobot(opponent, opponentSpawn);
            result = "";
            Outcome = MatchOutcome.None;
            ResultCount = 0;
            lastNotification = "Henuz bildirim yok";
            playerProbe = opponentProbe = "Henuz denenmedi";
            DemoAttackCooldown = 0f;
            DemoBotTarget = null;
            RobotPrototypeController.Select(null);
        }

        private static void ResetRobot(RobotPrototypeController robot, Transform spawn)
        {
            robot.StopCombat();
            robot.GetComponent<RobotHealth>().Restore();
            var body = robot.GetComponent<Rigidbody>();
            body.position = spawn.position;
            body.rotation = spawn.rotation;
            robot.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            robot.ResetMotion();
        }

        // User input goes through the countdown. Direct start is kept for existing tools.
        public string EquippedPlayerId
        {
            get
            {
                var modular = GetComponent<IEquippedDuelRobot>()?.ModularRobotId;
                var catalog = GetComponent<PlayerRobotSelection>();
                return modular ?? (catalog != null && catalog.ForkSelected ? DuelSetup.Fork : DuelSetup.Spinner);
            }
        }

        public DuelSetup CreateDuelStart(DuelSetup lobby)
        {
            if (lobby == null || lobby.version != 1 || !lobby.Valid) return null;
            string id = EquippedPlayerId;
            return lobby.ForEncounter(EncounterId, id, DuelSetup.OtherRobot(id));
        }

        private DuelSetup CurrentDuelStart()
        {
            var arena = FindAnyObjectByType<ArenaEnvironment>();
            var bot = GetComponent<OpponentSelectionPanel>();
            if (arena == null || arena.Layout == null || bot == null) return null;
            return CreateDuelStart(DuelSetup.Create(arena.Layout.name, bot.Personality, bot.Difficulty, rules));
        }

        private bool LockDuel(DuelSetup start)
        {
            if (start == null || start.version != 2 || !start.Valid || start.encounterId != encounterId) return false;
            var live = CurrentDuelStart();
            if (live == null || !live.SameAs(start) || live.playerRobotId != start.playerRobotId ||
                live.opponentRobotId != start.opponentRobotId) return false;
            var arena = FindAnyObjectByType<ArenaEnvironment>();
            if (ArenaLayoutValidator.Validate(arena.Layout, arena.ClearanceRadius).Count != 0) return false;
            var catalog = GetComponent<PlayerRobotSelection>();
            if (catalog == null || !botLoadout.Fit(opponent, catalog, start.opponentRobotId)) return false;
            LockedSetup = new DuelStart(start);
            return true;
        }

        public bool RequestCountdown(DuelSetup start)
        {
            if (!IsMainArena || !initialized || !isActiveAndEnabled || IsPaused || State != MatchState.Ready ||
                LockedSetup != null || !GetComponent<MainMenuScreen>().IsShowingReadyMenu || !LockDuel(start)) return false;
            BeginCountdown();
            return true;
        }

        public bool RequestCountdown()
        {
            if (!initialized || !isActiveAndEnabled || IsPaused || (State != MatchState.Ready && State != MatchState.Result)) return false;
            if (State == MatchState.Result)
            {
                if (EncounterComplete) Prepare();
                else PrepareRound();
            }
            if (IsMainArena && LockedSetup == null && !LockDuel(CurrentDuelStart())) return false;
            BeginCountdown();
            return true;
        }

        private void BeginCountdown()
        {
            countdownEndsAt = Time.time + Mathf.Max(1, countdownSeconds);
            State = MatchState.Countdown;
        }

        public bool StartMatch()
        {
            if (!initialized || !isActiveAndEnabled || IsPaused || State != MatchState.Ready) return false;
            if (IsMainArena && LockedSetup == null && !LockDuel(CurrentDuelStart())) return false;
            BeginPlaying();
            return true;
        }

        private void BeginPlaying()
        {
            State = MatchState.Playing;
            elapsedSeconds = 0f; lastClock = Time.timeAsDouble;
            roundAtStart = PlayerWins + OpponentWins + 1;
            MatchCount++;
            player.ConfigureCombat(false, opponent);
            opponent.ConfigureCombat(true, player);
            RobotPrototypeController.Select(player);
            DemoBotTarget = player.gameObject;
            BindCombatGates();
        }

        public bool RestartMatch()
        {
            if (!initialized || !isActiveAndEnabled || IsPaused || State != MatchState.Result) return false;
            State = MatchState.Restarting;
            StartCoroutine(RestartRoutine());
            return true;
        }

        private System.Collections.IEnumerator RestartRoutine()
        {
            // Yield once so the loading guard also covers rapid clicks in the same frame.
            yield return null;
            State = MatchState.Result;
            if (!isActiveAndEnabled || IsPaused) yield break;
            if (EncounterComplete) Prepare(); else PrepareRound();
            StartMatch();
        }

        public void DirtyDemoState()
        {
            if (State != MatchState.Playing) return;
            DemoAttackCooldown = 5f;
            DemoBotTarget = null;
            player.GetComponent<RobotHealth>().TakeDamage(10f);
        }

        public void SetManualMode(bool manual)
        {
            manualNotificationsOnly = manual;
            if (player == null || opponent == null) return;
            // The match buttons do not require autonomous combat or bot ticks.
            player.enabled = opponent.enabled = !manual;
            player.ResetMotion();
            opponent.ResetMotion();
        }

        public void DemoRepeatedNotifications()
        {
            Defeat(false);
            Defeat(false);
            Defeat(true);
        }

        public void DemoSameEvaluation(bool playerFirst)
        {
            // Both calls occur in this evaluation; no invented tie policy.
            Defeat(playerFirst);
            Defeat(!playerFirst);
        }

        public void DemoInvalidNotifications()
        {
            NotifyDefeat(null, MatchCount);
            NotifyDefeat(gameObject, MatchCount);
            NotifyDefeat(player.gameObject, MatchCount - 1);
        }

        public bool Probe(bool forPlayer)
        {
            bool accepted = forPlayer ? PlayerAllowed : OpponentAllowed;
            string message = (accepted ? "KABUL" : "RED") + " / " + State;
            if (forPlayer) playerProbe = message; else opponentProbe = message;
            // An observable, harmless command through the same permission gate.
            if (accepted) (forPlayer ? player : opponent).ResetMotion();
            return accepted;
        }

        public void Defeat(bool defeatPlayer)
        {
            // Test buttons deliberately use the same entry point as future health adapters.
            NotifyDefeat((defeatPlayer ? player : opponent).gameObject, MatchCount);
        }

        private void OnDefeated(RobotHealth defeated)
        {
            NotifyDefeat(defeated.gameObject, MatchCount);
        }

        // Capture matchId when an asynchronous producer starts work, not when it completes.
        // The current rule accepts the first valid defeat notification in this frame.
        public bool NotifyDefeat(GameObject robot, int matchId)
        {
            if (!initialized || !isActiveAndEnabled || IsPaused || State != MatchState.Playing || matchId != MatchCount ||
                robot == null || (robot != player.gameObject && robot != opponent.gameObject))
            {
                lastNotification = "YOK SAYILDI (durum / mac / kimlik)";
                return false;
            }
            AdvanceClock();
            CompleteRound(robot == player.gameObject ? MatchOutcome.OpponentVictory : MatchOutcome.PlayerVictory, RoundFinishReason.Defeat);
            return true;
        }

        private void OnPlayerDamaged(RobotHit hit)
        {
            if (State != MatchState.Playing || !isActiveAndEnabled || IsPaused) return;
            playerRound.receivedDamage += hit.Amount;
            if (hit.Source == opponentHealth) opponentRound.dealtDamage += hit.Amount;
        }

        private void OnOpponentDamaged(RobotHit hit)
        {
            if (State != MatchState.Playing || !isActiveAndEnabled || IsPaused) return;
            opponentRound.receivedDamage += hit.Amount;
            if (hit.Source == playerHealth) playerRound.dealtDamage += hit.Amount;
        }

        private void CompleteRound(MatchOutcome outcome, RoundFinishReason reason)
        {
            if (State != MatchState.Playing) return;
            // Lock before stopping combat or publishing any callbacks.
            State = MatchState.Result;
            Outcome = outcome;
            result = outcome == MatchOutcome.PlayerVictory ? "Oyuncu kazandi" : outcome == MatchOutcome.OpponentVictory ? "Rakip kazandi" : "Berabere";
            if (reason == RoundFinishReason.TimeOver) result = "Süre doldu - " + result;
            ResultCount++;
            lastNotification = "KABUL: " + outcome;
            player.StopCombat(); opponent.StopCombat();
            SnapshotStatistics(playerRound, player); SnapshotStatistics(opponentRound, opponent);
            LastRound = new RoundResult { matchId = MatchCount, roundNumber = roundAtStart, durationSeconds = elapsedSeconds,
                outcome = outcome, reason = reason, player = playerRound, opponent = opponentRound };
            if (outcome == MatchOutcome.PlayerVictory) PlayerWins++;
            else if (outcome == MatchOutcome.OpponentVictory) OpponentWins++;
            else DrawnRounds++;
            roundsPlayed++; totalSeconds += elapsedSeconds;
            Accumulate(playerTotal, playerRound); Accumulate(opponentTotal, opponentRound);
            EncounterComplete = rules.mode == MatchMode.Single || PlayerWins == 2 || OpponentWins == 2;
            if (EncounterComplete)
            {
                LastMatch = new MatchRecord { id = encounterId, endedAtUtc = DateTimeOffset.UtcNow.ToString("O"), rules = rules.Copy(),
                    outcome = outcome, playerWins = PlayerWins, opponentWins = OpponentWins, drawnRounds = DrawnRounds,
                    roundsPlayed = roundsPlayed, durationSeconds = totalSeconds, player = playerTotal, opponent = opponentTotal };
                if (gameplayInterface && !HistoryStore.Append(LastMatch)) HistoryNotice = HistoryStore.LastError;
            }
            ResultDetermined?.Invoke(Outcome);
        }

        private static void SnapshotStatistics(RobotMatchStatistics statistics, RobotPrototypeController robot)
        {
            var health = robot.GetComponent<RobotHealth>();
            statistics.remainingHealth = health.CurrentHealth; statistics.maximumHealth = health.MaxHealth;
            var damage = robot.GetComponent<RobotDamageVisuals>();
            statistics.detachedParts = damage != null ? damage.DetachedCount : 0;
        }

        private static void Accumulate(RobotMatchStatistics total, RobotMatchStatistics round)
        {
            total.remainingHealth = round.remainingHealth; total.maximumHealth = round.maximumHealth;
            total.dealtDamage += round.dealtDamage; total.receivedDamage += round.receivedDamage;
            total.detachedParts += round.detachedParts;
        }

        private void OnGUI()
        {
            // The shared arena's MainMenuScreen owns its HUD. Only test scenes use this panel.
            if (gameplayInterface) return;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1000f, Screen.height / 700f, 1f));
            GUILayout.BeginArea(new Rect(16, 16, 430, 450), GUI.skin.box);
            GUILayout.Label("Mac akisi ve sonuc denemesi");
            string stateName = State == MatchState.Ready ? "Hazir" : State == MatchState.Playing ? "Oynaniyor" : IsRestarting ? "Yeni mac hazirlaniyor" : "Sonuc";
            int robotCount = (player != null ? 1 : 0) + (opponent != null && opponent != player ? 1 : 0);
            GUILayout.Label("Durum: " + stateName + " | Baslayan mac: " + MatchCount + " | Robot: " + robotCount);
            GUILayout.Label("Oyuncu izni: " + (PlayerAllowed ? "ACIK" : "KAPALI") + " | Rakip izni: " + (OpponentAllowed ? "ACIK" : "KAPALI"));
            if (GUILayout.Button("Baslat (tekrar basmayi da deneyin)")) StartMatch();
            if (GUILayout.Button("Oyuncu deneme girdisi")) Probe(true);
            GUILayout.Label("Oyuncu: " + playerProbe);
            if (GUILayout.Button("Rakip deneme girdisi")) Probe(false);
            GUILayout.Label("Rakip: " + opponentProbe);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Oyuncuyu yen")) Defeat(true);
            if (GUILayout.Button("Rakibi yen")) Defeat(false);
            GUILayout.EndHorizontal();
            GUILayout.Label(result);
            if (State == MatchState.Result || IsRestarting)
            {
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && !IsRestarting;
                if (GUILayout.Button(IsRestarting ? "Hazirlaniyor..." : "Yeniden baslat / Yeni mac")) RestartMatch();
                GUI.enabled = previousEnabled;
            }
            GUILayout.Label("Bu macta sonuc sayisi: " + ResultCount + " | " + lastNotification);
            GUILayout.Label("Eşzamanlı yenilgi: ilk geçerli bildirim kabul edilir.");
            if (GUILayout.Button("Hazira don / spawnlari sifirla")) Prepare();
            if (player != null && opponent != null)
                GUILayout.Label("Can: " + player.GetComponent<RobotHealth>().CurrentHealth + " / " + opponent.GetComponent<RobotHealth>().CurrentHealth +
                    " | Deneme bekleme: " + DemoAttackCooldown + " | Bot hedefi: " + (DemoBotTarget != null ? DemoBotTarget.name : "yok"));
            if (GUILayout.Button("Yerel deneme durumunu kirlet (can / bekleme / hedef)")) DirtyDemoState();
            GUILayout.Label(manualNotificationsOnly ? "Yerel deneme: hareket / dovus / bot tick'leri kapali." : "Oynarken: WASD / oklar, Space. Hazir ve Sonuc: kilitli.");
            GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(580, 16, 400, 230), GUI.skin.box);
            GUILayout.Label("Bildirim regresyonu");
            bool manual = GUILayout.Toggle(manualNotificationsOnly, "Yalniz deneme bildirimleri (dovus / bot kapali)");
            if (manual != manualNotificationsOnly) SetManualMode(manual);
            if (GUILayout.Button("Tekrar: rakip, rakip, oyuncu")) DemoRepeatedNotifications();
            if (GUILayout.Button("Gecersiz: null / yabanci robot / eski mac")) DemoInvalidNotifications();
            if (GUILayout.Button("Ayni an: oyuncu sonra rakip")) DemoSameEvaluation(true);
            if (GUILayout.Button("Ayni an: rakip sonra oyuncu")) DemoSameEvaluation(false);
            GUILayout.Label("Aynı karede yalnızca bir sonuç üretilir.");
            GUILayout.Label("Otomatik: RobotSavaslari > Match > Run Match Regression");
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
    }
}
