using UnityEngine;

namespace RobotSavaslari.Match
{
    // Owns round state for both the production arena and the explicit regression harness.
    [DefaultExecutionOrder(-100)]
    public sealed class MatchController : MonoBehaviour
    {
        public enum MatchState { Ready, Playing, Result, Restarting, Countdown }
        public enum MatchOutcome { None, PlayerVictory, OpponentVictory }
        [SerializeField] private RobotPrototypeController player;
        [SerializeField] private RobotPrototypeController opponent;
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private Transform opponentSpawn;
        public MatchState State { get; private set; }
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

        public void ConfigureGameplay()
        {
            gameplayInterface = true;
            manualNotificationsOnly = false;
        }

        private void Update()
        {
            if (State == MatchState.Countdown && Time.time >= countdownEndsAt)
            {
                BeginPlaying();
                startMessageEndsAt = Time.time + startMessageDuration;
            }
        }
        public event System.Action<MatchOutcome> ResultDetermined;
        public bool PlayerAllowed => State == MatchState.Playing && player != null && !player.CombatStopped;
        public bool OpponentAllowed => State == MatchState.Playing && opponent != null && !opponent.CombatStopped;
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
            player.GetComponent<RobotHealth>().Defeated += OnDefeated;
            opponent.GetComponent<RobotHealth>().Defeated += OnDefeated;
            initialized = true;
            SetManualMode(manualNotificationsOnly);
            Prepare();
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            if (player != null) player.GetComponent<RobotHealth>().Defeated -= OnDefeated;
            if (opponent != null) opponent.GetComponent<RobotHealth>().Defeated -= OnDefeated;
        }

        public void Prepare()
        {
            if (!initialized || State == MatchState.Playing || State == MatchState.Countdown || IsRestarting) return;
            State = MatchState.Ready;
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
        public bool RequestCountdown()
        {
            if (!initialized || (State != MatchState.Ready && State != MatchState.Result)) return false;
            if (State == MatchState.Result) Prepare();
            countdownEndsAt = Time.time + Mathf.Max(1, countdownSeconds);
            State = MatchState.Countdown;
            return true;
        }

        public bool StartMatch()
        {
            if (!initialized || State != MatchState.Ready) return false;
            BeginPlaying();
            return true;
        }

        private void BeginPlaying()
        {
            State = MatchState.Playing;
            MatchCount++;
            player.ConfigureCombat(false, opponent);
            opponent.ConfigureCombat(true, player);
            RobotPrototypeController.Select(player);
            DemoBotTarget = player.gameObject;
        }

        public bool RestartMatch()
        {
            if (!initialized || State != MatchState.Result) return false;
            State = MatchState.Restarting;
            StartCoroutine(RestartRoutine());
            return true;
        }

        private System.Collections.IEnumerator RestartRoutine()
        {
            // Yield once so the loading guard also covers rapid clicks in the same frame.
            yield return null;
            State = MatchState.Result;
            Prepare();
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
            if (!initialized || State != MatchState.Playing || matchId != MatchCount ||
                robot == null || (robot != player.gameObject && robot != opponent.gameObject))
            {
                lastNotification = "YOK SAYILDI (durum / mac / kimlik)";
                return false;
            }
            // Lock before invoking external listeners, including reentrant notifications.
            State = MatchState.Result;
            Outcome = robot == player.gameObject ? MatchOutcome.OpponentVictory : MatchOutcome.PlayerVictory;
            result = Outcome == MatchOutcome.OpponentVictory ? "Rakip kazandi" : "Oyuncu kazandi";
            ResultCount++;
            lastNotification = "KABUL: " + robot.name;
            player.StopCombat();
            opponent.StopCombat();
            ResultDetermined?.Invoke(Outcome);
            return true;
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
