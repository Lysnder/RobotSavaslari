using System;
using System.Reflection;
using RobotSavaslari.Driving;
using RobotSavaslari.Arena;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using RobotSavaslari.Robots;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Training
{
    /// <summary>Rotadaki tek bir kontrol noktası.</summary>
    [Serializable]
    public struct TimeTrialCheckpoint
    {
        public Vector3 position;
        [Min(0.2f)] public float radius;
    }

    /// <summary>Eğitimin durumu.</summary>
    public enum TimeTrialState
    {
        Inactive,
        Running,
        Finished,
    }

    /// <summary>
    /// MainArena'da oynanan sürüş eğitimi: sıralı kontrol noktaları, süre,
    /// yanlış sıra uyarısı, bitiş ve yeniden deneme.
    ///
    /// Aynı gerçek robotu ve aynı fizik dünyasını kullanır; ikinci bir arena ya
    /// da ayrı bir demo sahnesi kurmaz. Maça ait bileşenleri yalnızca geçici
    /// olarak kapatır ve çıkışta sakladığı durumları eksiksiz geri yükler.
    /// Maç durumu Ready kalır, MatchCount ve ResultCount artmaz.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class TimeTrialSession : MonoBehaviour
    {
        [Header("Rota")]
        [Tooltip("Eğitimin başlangıç pozu. Arena sınırları x +-5.89, z +-4.89.")]
        [SerializeField] private Vector3 startPosition = new Vector3(-4.3f, 0f, -3.3f);
        [SerializeField] private float startYaw;

        [SerializeField]
        private TimeTrialCheckpoint[] checkpoints =
        {
            new TimeTrialCheckpoint { position = new Vector3(-4.3f, 0f, 3.3f), radius = 1.1f },
            new TimeTrialCheckpoint { position = new Vector3(0f, 0f, 3.6f), radius = 1.1f },
            new TimeTrialCheckpoint { position = new Vector3(4.3f, 0f, 3.3f), radius = 1.1f },
            new TimeTrialCheckpoint { position = new Vector3(4.3f, 0f, -3.3f), radius = 1.1f },
            new TimeTrialCheckpoint { position = new Vector3(-0.5f, 0f, -3.5f), radius = 1.1f },
        };

        private MatchController match;
        private MainMenuScreen menu;
        private SettingsPanelController settings;
        private RobotPrototypeController playerPrototype;
        private RobotPrototypeController opponentPrototype;
        private PlayerKeyboardDriver driver;
        private RobotMotor motor;
        private ArenaEnvironment arena;
        private ArenaBounds arenaBounds;

        private bool savedMatchEnabled;
        private bool savedMenuEnabled;
        private bool savedPlayerEnabled;
        private bool savedOpponentEnabled;
        private bool savedDriverEnabled;
        private bool stateSaved;

        // The Ready-menu property arrived after the original driving task base.
        private static readonly MethodInfo ReadyMenuGetter =
            typeof(MainMenuScreen).GetProperty("IsShowingReadyMenu")?.GetGetMethod();
        private static readonly FieldInfo LegacyHistory =
            typeof(MainMenuScreen).GetField("_showHistory", BindingFlags.Instance | BindingFlags.NonPublic);
        private Func<bool> menuReady;

        public bool ReadyMenuVisible => menu == null || (menu.isActiveAndEnabled &&
            (menuReady != null ? menuReady() : LegacyHistory?.GetValue(menu) is false));

        private DrivingExerciseSession exerciseRun;
        private CollisionReporter collisionReporter;
        private CombatCameraController cameraController;
        private bool savedCameraEnabled;
        private bool savedCamera;
        private float lastImpactTime;
        private Vector3 legacyStart;
        private float legacyYaw;
        private TimeTrialCheckpoint[] legacyRoute;

        public DrivingExerciseDefinition Exercise { get; private set; }
        public DrivingExerciseStore ExerciseStore { get; set; }
        public string ExerciseKey { get; private set; }
        public DrivingExerciseResult ExerciseResult => exerciseRun?.Result ?? default;
        public float ExerciseDwell => exerciseRun?.Dwell ?? 0f;
        public int AttemptRevision { get; private set; }

        private bool lapValid;
        private bool finishReported;

        /// <summary>Eğitim durumu.</summary>
        public TimeTrialState State { get; private set; } = TimeTrialState.Inactive;

        /// <summary>Eğitime girilebilir mi? Sahneyi tanıyan taraf belirler.</summary>
        public bool CanEnter { get; set; }

        /// <summary>Sıradaki kontrol noktasının sırası (0 tabanlı).</summary>
        public int NextCheckpointIndex { get; private set; }

        /// <summary>Rotadaki kontrol noktası sayısı.</summary>
        public int CheckpointCount => checkpoints != null ? checkpoints.Length : 0;

        /// <summary>Bu denemede geçen süre (saniye).</summary>
        public float ElapsedTime { get; private set; }

        public int AttemptNumber { get; private set; }
        public float SurfaceHeight => arenaBounds != null ? arenaBounds.SurfaceHeight : startPosition.y;

        /// <summary>Son tamamlanan turun süresi; tur yoksa 0.</summary>
        public float LastLapTime { get; private set; }

        /// <summary>Son tamamlanan tur geçerli miydi?</summary>
        public bool LastLapValid { get; private set; }

        /// <summary>Bu deneme hâlâ geçerli mi? Yanlış sıra geçersiz kılar.</summary>
        public bool LapValid => lapValid;

        /// <summary>Ekranda gösterilecek son bilgi ya da uyarı.</summary>
        public string Notice { get; private set; } = string.Empty;

        /// <summary>Eğitimin sürdüğü robot; tanı ve test için.</summary>
        public RobotMotor Motor => motor;

        /// <summary>Sıradaki kontrol noktasının konumu; gösterge bunu işaretler.</summary>
        public bool TryGetNextCheckpoint(out TimeTrialCheckpoint checkpoint)
        {
            if (State == TimeTrialState.Running && NextCheckpointIndex < CheckpointCount)
            {
                checkpoint = checkpoints[NextCheckpointIndex];
                return true;
            }

            checkpoint = default;
            return false;
        }

        /// <summary>Verilen sıradaki kontrol noktası; gösterge işaretleri bunu okur.</summary>
        public TimeTrialCheckpoint GetCheckpoint(int index)
        {
            if (checkpoints == null || index < 0 || index >= checkpoints.Length)
            {
                return default;
            }

            return checkpoints[index];
        }

        /// <summary>Eğitimin başlangıç pozu.</summary>
        public Vector3 StartPosition => startPosition;

        /// <summary>
        /// Parkurun kimliği: arena düzeni, başlangıç pozu ve kontrol noktalarından türer.
        /// Rota değişirse kimlik de değişir, böylece eski bir rekor yeni
        /// parkurda aynı turmuş gibi kullanılamaz.
        /// </summary>
        public string CourseId
        {
            get
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + Mathf.RoundToInt(startPosition.x * 100f);
                    hash = hash * 31 + Mathf.RoundToInt(startPosition.z * 100f);
                    hash = hash * 31 + Mathf.RoundToInt(startYaw * 10f);

                    for (int index = 0; index < CheckpointCount; index++)
                    {
                        TimeTrialCheckpoint checkpoint = checkpoints[index];
                        hash = hash * 31 + Mathf.RoundToInt(checkpoint.position.x * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(checkpoint.position.z * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(checkpoint.radius * 100f);
                    }

                    // Include authored content so editing an existing layout also invalidates its old lap.
                    string layoutId = arena != null && arena.Layout != null
                        ? Hash128.Compute(JsonUtility.ToJson(arena.Layout)).ToString() : "none";
                    return $"arena-v2-{CheckpointCount}-{hash:x8}-{layoutId}";
                }
            }
        }

        /// <summary>Rotayı koddan kurmak için; test ve ayar amaçlı.</summary>
        public void ConfigureRoute(Vector3 start, float yaw, TimeTrialCheckpoint[] route)
        {
            startPosition = start;
            startYaw = yaw;
            checkpoints = route ?? Array.Empty<TimeTrialCheckpoint>();
        }

        /// <summary>Eğitimin süreceği bileşenleri bağlar.</summary>
        public void Bind(MatchController matchController, MainMenuScreen mainMenu,
            SettingsPanelController settingsPanel, PlayerKeyboardDriver playerDriver)
        {
            match = matchController;
            menu = mainMenu;
            menuReady = menu != null && ReadyMenuGetter != null
                ? (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), menu, ReadyMenuGetter) : null;
            settings = settingsPanel;
            driver = playerDriver;
            arena = match != null ? FindAnyObjectByType<ArenaEnvironment>() : null;
            arenaBounds = arena != null ? arena.GetComponent<ArenaBounds>() : null;
            cameraController = match != null ? FindAnyObjectByType<CombatCameraController>() : null;

            if (driver != null)
            {
                motor = driver.GetComponent<RobotMotor>();
                playerPrototype = driver.GetComponent<RobotPrototypeController>();
                collisionReporter = driver.GetComponent<CollisionReporter>();
            }

            if (match != null)
            {
                opponentPrototype = match.Opponent;
                if (playerPrototype == null)
                {
                    playerPrototype = match.Player;
                }
            }
        }

        /// <summary>
        /// Eğitime girer. Yalnızca kurulmuş ve hazır bir maçta, ayarlar
        /// kapalıyken girilir.
        /// </summary>
        public bool Enter() => Begin(null);

        public bool EnterExercise(DrivingExerciseDefinition definition)
        {
            if (!AllowsEntry() || definition == null || !definition.IsValid() ||
                !TryResolveRoute(definition, out var start, out var route)) return false;

            legacyStart = startPosition;
            legacyYaw = startYaw;
            legacyRoute = checkpoints;
            Exercise = definition;
            ConfigureRoute(start, definition.startYaw, route);
            var profile = Hash128.Compute("drive-v1:" + JsonUtility.ToJson(motor) +
                (driver.Boost != null ? JsonUtility.ToJson(driver.Boost) : "") +
                (driver.Handbrake != null ? JsonUtility.ToJson(driver.Handbrake) : ""));
            var rules = Hash128.Compute(JsonUtility.ToJson(definition));
            ExerciseKey = $"{definition.exerciseId}/{definition.rulesRevision}/{CourseId}/{profile}/{rules}";
            ExerciseStore?.Load();
            return Begin(definition);
        }

        private bool AllowsEntry() => State == TimeTrialState.Inactive && isActiveAndEnabled && CanEnter &&
            driver != null && motor != null &&
            (match == null || (match.isActiveAndEnabled && match.IsInitialized && !match.IsPaused &&
                match.State == MatchController.MatchState.Ready)) &&
            ReadyMenuVisible && (settings == null || !settings.IsOpen) &&
            (cameraController == null || !cameraController.HasPresentationTargets);

        private bool Begin(DrivingExerciseDefinition definition)
        {
            if (!AllowsEntry())
            {
                return false;
            }

            SaveSceneState();
            if (definition != null && cameraController != null)
            {
                savedCamera = true;
                savedCameraEnabled = cameraController.enabled;
                cameraController.enabled = false;
            }

            // Dövüş durur: eğitimde hasar ve sonuç üretilmez.
            if (playerPrototype != null) playerPrototype.StopCombat();
            if (opponentPrototype != null) opponentPrototype.StopCombat();

            if (match != null) match.enabled = false;
            if (menu != null) menu.enabled = false;
            if (opponentPrototype != null) opponentPrototype.enabled = false;

            // Oyuncu Prototype'ı her fizik adımında ResetMotion çağırabildiği
            // için kapatılmalı. Kapatılırken kendi OnDisable'ı sürücüyü de
            // kapatır, bu yüzden sürücü ondan SONRA açılır.
            if (playerPrototype != null) playerPrototype.enabled = false;
            driver.enabled = true;

            Retry();
            return true;
        }

        /// <summary>Eğitimi bitirir ve sahneyi eski durumuna döndürür.</summary>
        public void Exit()
        {
            if (State == TimeTrialState.Inactive)
            {
                return;
            }

            bool wasExercise = Exercise != null;
            State = TimeTrialState.Inactive;
            Notice = string.Empty;

            if (wasExercise)
            {
                ConfigureRoute(legacyStart, legacyYaw, legacyRoute);
                Exercise = null;
                ExerciseKey = null;
                exerciseRun = null;
                legacyRoute = null;
            }

            if (savedCamera && cameraController != null)
                cameraController.enabled = savedCameraEnabled;
            savedCamera = false;

            if (motor != null)
            {
                motor.ResetMotion();
            }

            RestoreSceneState();

            // Maçın kendi hazırlığı robotları spawn'a döndürür; sayaçlar artmaz.
            if (match != null && match.IsInitialized)
            {
                match.Prepare();
            }

            if (wasExercise && driver != null) driver.enabled = savedDriverEnabled;
        }

        /// <summary>Denemeyi baştan alır: poz, hareket, kontrol noktaları ve süre sıfırlanır.</summary>
        public void Retry()
        {
            if (Exercise == null || State != TimeTrialState.Running) AttemptNumber++;
            AttemptRevision++;
            NextCheckpointIndex = 0;
            ElapsedTime = 0f;
            lapValid = true;
            finishReported = false;
            State = TimeTrialState.Running;
            Notice = "Kontrol noktalarini sirayla gec.";

            PlaceAtStart();
            if (Exercise != null)
            {
                driver.enabled = true;
                driver.Boost?.ResetState();
                driver.Handbrake?.ResetState();
                lastImpactTime = Time.time;
                exerciseRun = new DrivingExerciseSession(Exercise, checkpoints, startPosition);
                LastLapTime = 0f;
                LastLapValid = false;
                Notice = Exercise.instruction;
            }
        }

        private void PlaceAtStart()
        {
            if (motor == null)
            {
                return;
            }

            Rigidbody body = motor.Body;
            Quaternion rotation = Quaternion.Euler(0f, startYaw, 0f);
            Vector3 position = new Vector3(startPosition.x,
                body != null ? body.position.y : startPosition.y, startPosition.z);

            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
            }

            motor.transform.SetPositionAndRotation(position, rotation);
            motor.ResetMotion();
        }

        private void SaveSceneState()
        {
            savedMatchEnabled = match != null && match.enabled;
            savedMenuEnabled = menu != null && menu.enabled;
            savedPlayerEnabled = playerPrototype != null && playerPrototype.enabled;
            savedOpponentEnabled = opponentPrototype != null && opponentPrototype.enabled;
            savedDriverEnabled = driver != null && driver.enabled;
            stateSaved = true;
        }

        private void RestoreSceneState()
        {
            if (!stateSaved)
            {
                return;
            }

            // Sürücü önce bırakılır: Prototype açılırken kendi kuralına göre
            // sürücünün durumunu belirler.
            if (driver != null) driver.enabled = savedDriverEnabled;
            if (playerPrototype != null) playerPrototype.enabled = savedPlayerEnabled;
            if (opponentPrototype != null) opponentPrototype.enabled = savedOpponentEnabled;
            if (menu != null) menu.enabled = savedMenuEnabled;
            if (match != null) match.enabled = savedMatchEnabled;

            stateSaved = false;
        }

        private void OnDisable()
        {
            if (State != TimeTrialState.Inactive)
            {
                Exit();
            }
        }

        private void Update()
        {
            if (State != TimeTrialState.Inactive && settings != null && settings.IsOpen)
            {
                // Ayarlar açılırsa eğitim beklemez, güvenle çıkılır.
                Exit();
                return;
            }

            if (State != TimeTrialState.Running || motor == null || Exercise != null)
            {
                return;
            }

            ElapsedTime += Time.deltaTime;
            EvaluateCheckpoints();
        }

        private void FixedUpdate()
        {
            if (State != TimeTrialState.Running || exerciseRun == null || motor == null ||
                (settings != null && settings.IsOpen) || motor.Body == null) return;

            int impacts = 0;
            if (collisionReporter != null && collisionReporter.HasReport)
            {
                float timestamp = Time.time - collisionReporter.TimeSinceImpact;
                if (timestamp > lastImpactTime)
                {
                    lastImpactTime = timestamp;
                    impacts = 1;
                }
            }

            Rigidbody body = motor.Body;
            exerciseRun.Step(body.position, body.rotation.eulerAngles.y, body.linearVelocity, Time.fixedDeltaTime, impacts);
            NextCheckpointIndex = exerciseRun.NextGate;
            ElapsedTime = exerciseRun.Result.seconds;
            lapValid = exerciseRun.Result.Faults == 0;
            Notice = exerciseRun.Feedback ?? Exercise.instruction;
            if (!exerciseRun.Finished) return;

            LastLapTime = ElapsedTime;
            LastLapValid = exerciseRun.Result.success;
            State = TimeTrialState.Finished;
            driver.enabled = false;
            motor.ResetMotion();
        }

        private bool TryResolveRoute(DrivingExerciseDefinition definition, out Vector3 start, out TimeTrialCheckpoint[] route)
        {
            start = definition.start;
            route = (TimeTrialCheckpoint[])definition.gates.Clone();
            if (arenaBounds == null || playerPrototype == null) return true;

            float footprint = ArenaLayoutValidator.MeasureRadius(playerPrototype);
            float opponentRadius = opponentPrototype != null ? ArenaLayoutValidator.MeasureRadius(opponentPrototype) : 0f;
            Vector2 center = arenaBounds.Center;
            int selectedLane = 0;
            bool found = false;
            // Try the authored lane first, then translate the whole exercise to adjacent lanes.
            for (int offset = 0; offset <= 4 && !found; offset++)
                found = Fits(offset) || (offset != 0 && Fits(-offset));
            if (!found) return false;

            Vector3 translation = new Vector3(center.x + selectedLane * 1.5f, 0f, center.y);
            start += translation;
            for (int index = 0; index < route.Length; index++) route[index].position += translation;
            return true;

            bool Fits(int lane)
            {
                Vector3 shift = new Vector3(center.x + lane * 1.5f, 0f, center.y);
                Vector3 previous = definition.start + shift;
                if (!Clear(previous, footprint)) return false;
                foreach (var gate in definition.gates)
                {
                    Vector3 target = gate.position + shift;
                    if (!Clear(target, footprint + gate.radius)) return false;
                    int steps = Mathf.CeilToInt(Vector3.Distance(previous, target) * 4f);
                    for (int step = 1; step < steps; step++)
                        if (!Clear(Vector3.Lerp(previous, target, (float)step / steps), footprint)) return false;
                    previous = target;
                }

                selectedLane = lane;
                return true;
            }

            bool Clear(Vector3 point, float radius)
            {
                Vector2 local = new Vector2(point.x, point.z) - center;
                Vector2 limits = arenaBounds.CenterLimits(radius);
                if (Mathf.Abs(local.x) > limits.x || Mathf.Abs(local.y) > limits.y) return false;
                if (opponentPrototype != null)
                {
                    Vector3 delta = point - opponentPrototype.transform.position;
                    delta.y = 0f;
                    if (delta.sqrMagnitude < (radius + opponentRadius) * (radius + opponentRadius)) return false;
                }

                return arena == null || arena.Layout == null || ArenaLayoutValidator.IsClear(arena.Layout, local, radius);
            }
        }

        private void EvaluateCheckpoints()
        {
            if (CheckpointCount == 0)
            {
                return;
            }

            Vector3 position = motor.transform.position;

            for (int index = 0; index < checkpoints.Length; index++)
            {
                if (!IsInside(checkpoints[index], position))
                {
                    continue;
                }

                if (index == NextCheckpointIndex)
                {
                    NextCheckpointIndex++;

                    if (NextCheckpointIndex >= checkpoints.Length)
                    {
                        ReportFinish();
                    }
                    else
                    {
                        Notice = $"Kontrol noktasi {NextCheckpointIndex}/{checkpoints.Length}";
                    }

                    return;
                }

                if (index > NextCheckpointIndex && lapValid)
                {
                    // Sıra atlandı: tur geçersiz, ilerleme durur.
                    lapValid = false;
                    Notice = $"Yanlis sira: once {NextCheckpointIndex + 1}. noktaya git.";
                }

                return;
            }
        }

        private void ReportFinish()
        {
            if (finishReported)
            {
                return;
            }

            // Bitiş yalnızca bir kez raporlanır.
            finishReported = true;
            State = TimeTrialState.Finished;
            LastLapTime = ElapsedTime;
            LastLapValid = lapValid;
            Notice = lapValid
                ? $"Tur tamamlandi: {ElapsedTime:0.00} s"
                : "Tur gecersiz: kontrol noktalari sirayla gecilmedi.";

            if (motor != null)
            {
                motor.Stop();
            }
        }

        private static bool IsInside(TimeTrialCheckpoint checkpoint, Vector3 position)
        {
            Vector3 offset = position - checkpoint.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= checkpoint.radius * checkpoint.radius;
        }
    }
}
