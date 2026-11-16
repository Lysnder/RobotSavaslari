using System;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
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

        private bool savedMatchEnabled;
        private bool savedMenuEnabled;
        private bool savedPlayerEnabled;
        private bool savedOpponentEnabled;
        private bool savedDriverEnabled;
        private bool stateSaved;

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
        /// Parkurun kimliği: başlangıç pozu ve kontrol noktalarından türer.
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

                    return $"arena-{CheckpointCount}-{hash:x8}";
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
            settings = settingsPanel;
            driver = playerDriver;

            if (driver != null)
            {
                motor = driver.GetComponent<RobotMotor>();
                playerPrototype = driver.GetComponent<RobotPrototypeController>();
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
        public bool Enter()
        {
            if (State != TimeTrialState.Inactive || !CanEnter || driver == null || motor == null)
            {
                return false;
            }

            SaveSceneState();

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

            State = TimeTrialState.Inactive;
            Notice = string.Empty;

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
        }

        /// <summary>Denemeyi baştan alır: poz, hareket, kontrol noktaları ve süre sıfırlanır.</summary>
        public void Retry()
        {
            NextCheckpointIndex = 0;
            ElapsedTime = 0f;
            lapValid = true;
            finishReported = false;
            State = TimeTrialState.Running;
            Notice = "Kontrol noktalarini sirayla gec.";

            PlaceAtStart();
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
            if (State == TimeTrialState.Running && settings != null && settings.IsOpen)
            {
                // Ayarlar açılırsa eğitim beklemez, güvenle çıkılır.
                Exit();
                return;
            }

            if (State != TimeTrialState.Running || motor == null)
            {
                return;
            }

            ElapsedTime += Time.deltaTime;
            EvaluateCheckpoints();
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
