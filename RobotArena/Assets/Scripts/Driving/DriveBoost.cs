using RobotSavaslari.Robots;
using UnityEngine;

namespace RobotSavaslari.Driving
{
    /// <summary>
    /// Sınırlı enerjiyle çalışan kısa süreli hızlanma. Enerjiyi tutar, boost
    /// isteğinin uygulanıp uygulanmayacağına karar verir ve ortak
    /// <see cref="RobotMotor"/> bileşenine geçici çarpanı gönderir.
    ///
    /// Girdiyi kendisi okumaz: tuş <see cref="PlayerKeyboardDriver"/> üzerinde
    /// durur ve buraya <see cref="SetBoostRequested"/> ile gelir. Böylece bot
    /// robotlarda bu bileşen bulunsa bile istek gelmediği sürece motor
    /// davranışı değişmez.
    /// </summary>
    [DefaultExecutionOrder(105)]
    [RequireComponent(typeof(RobotMotor))]
    [AddComponentMenu("Robot Savaslari/Driving/Surus Boost")]
    public class DriveBoost : MonoBehaviour
    {
        [Header("Enerji")]
        [SerializeField, Min(1f)] private float capacity = 100f;

        [Tooltip("Maç başında ve her sıfırlamada yüklenen enerji.")]
        [SerializeField, Min(0f)] private float startingEnergy = 100f;

        [SerializeField, Min(0f)] private float drainPerSecond = 40f;
        [SerializeField, Min(0f)] private float regenPerSecond = 25f;

        [Tooltip("Boost bırakıldıktan sonra dolumun başlaması için beklenen süre.")]
        [SerializeField, Min(0f)] private float regenDelay = 1f;

        [Tooltip("Boost'un yeniden başlayabilmesi için gereken en az enerji. Boş enerjide tuş basılı kalsa bile titremeyi önler.")]
        [SerializeField, Min(0f)] private float engageThreshold = 5f;

        [Header("Etki")]
        [Tooltip("İleri üst hız çarpanı.")]
        [SerializeField, Min(1f)] private float speedScale = 1.5f;

        [Tooltip("Hızlanma çarpanı. Zemin sürtünmesi ivmeden düşüldüğü için üst hızla birlikte artırılır.")]
        [SerializeField, Min(1f)] private float accelerationScale = 1.5f;

        private RobotMotor motor;
        private DriveHandbrake handbrake;
        private float regenCountdown;

        /// <summary>Enerji kapasitesi.</summary>
        public float Capacity => capacity;

        /// <summary>Kalan enerji (0 ile <see cref="Capacity"/> arası).</summary>
        public float Energy { get; private set; }

        /// <summary>Kalan enerjinin orana çevrilmiş hâli (0–1); gösterge bunu okur.</summary>
        public float EnergyRatio => capacity > 0f ? Mathf.Clamp01(Energy / capacity) : 0f;

        /// <summary>Boost bu an uygulanıyor mu?</summary>
        public bool IsBoosting { get; private set; }

        /// <summary>Sürücü boost istiyor mu? Uygulanması ayrı karardır.</summary>
        public bool IsRequested { get; private set; }

        /// <summary>
        /// El freni bu adımda devreye giriyor mu? Boost ve el freni birlikte
        /// istendiğinde öncelik el frenindedir: manevra ile hızlanma çelişkili
        /// isteklerdir, bu yüzden boost ne uygulanır ne de enerji harcar.
        /// </summary>
        public bool HandbrakeHasPriority
        {
            get
            {
                // Bileşenler çalışma anında eklendiği için referans Awake'te
                // hazır olmayabilir; ilk ihtiyaçta çözülür.
                if (handbrake == null)
                {
                    handbrake = GetComponent<DriveHandbrake>();
                }

                return handbrake != null && handbrake.WouldEngage;
            }
        }

        /// <summary>İstek uygulanabilir durumda mı? (yeterli enerji ve ileri komut)</summary>
        public bool CanEngage => Energy >= (IsBoosting ? Mathf.Epsilon : engageThreshold)
                                 && motor != null
                                 && motor.MoveCommand > 0f
                                 && !HandbrakeHasPriority;

        private void Awake()
        {
            motor = GetComponent<RobotMotor>();
            handbrake = GetComponent<DriveHandbrake>();
            startingEnergy = Mathf.Min(startingEnergy, capacity);
            Energy = startingEnergy;
        }

        private void OnValidate()
        {
            startingEnergy = Mathf.Min(startingEnergy, capacity);
            engageThreshold = Mathf.Min(engageThreshold, capacity);
        }

        /// <summary>Sürücünün boost isteğini bildirir.</summary>
        public void SetBoostRequested(bool requested) => IsRequested = requested;

        /// <summary>
        /// Maç sıfırlaması ve ikinci maç için: enerji başlangıç değerine döner,
        /// boost durur, motordaki geçici çarpan bırakılır.
        /// </summary>
        public void ResetState()
        {
            IsRequested = false;
            IsBoosting = false;
            regenCountdown = 0f;
            Energy = startingEnergy;

            if (motor != null)
            {
                motor.ClearDriveModifier();
            }
        }

        private void OnDisable()
        {
            // Kapatılınca istek de etki de bırakılır; enerji olduğu gibi kalır,
            // çünkü enerjiyi yalnızca ResetState tazeler.
            IsRequested = false;
            IsBoosting = false;

            if (motor != null)
            {
                motor.ClearDriveModifier();
            }
        }

        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;
            bool engaged = IsRequested && CanEngage;

            if (engaged)
            {
                Energy = Mathf.Max(0f, Energy - drainPerSecond * deltaTime);
                regenCountdown = regenDelay;
                motor.SetDriveModifier(speedScale, accelerationScale);

                // Enerji bu adımda bittiyse boost burada kesilir: tuş basılı
                // kalsa bile eşik dolana kadar yeniden başlamaz.
                engaged = Energy > 0f;
                if (!engaged)
                {
                    motor.ClearDriveModifier();
                }
            }
            else
            {
                motor.ClearDriveModifier();

                if (regenCountdown > 0f)
                {
                    regenCountdown = Mathf.Max(0f, regenCountdown - deltaTime);
                }
                else
                {
                    Energy = Mathf.Min(capacity, Energy + regenPerSecond * deltaTime);
                }
            }

            IsBoosting = engaged;
        }
    }
}
