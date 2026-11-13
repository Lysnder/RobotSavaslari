using RobotSavaslari.Robots;
using UnityEngine;

namespace RobotSavaslari.Driving
{
    /// <summary>
    /// El freni: hız karşılığında daha keskin dönüş. Ayrı bir sürüş motoru
    /// yazmaz; ortak <see cref="RobotMotor"/> üzerindeki geçici çarpan kanalına
    /// açık bir manevra koşulu uygular — dönüş sınırı yükselir, ileri üst hız
    /// düşer.
    ///
    /// Normal frenden farkı: normal fren gazı bırakıp durmaktır ve dönüşe
    /// dokunmaz. El freni hızın bir kısmını korur, bedeli üst hızdan öder ve
    /// dönüşü belirgin biçimde keskinleştirir.
    ///
    /// Yalnızca <see cref="engageSpeed"/> üstünde devreye girer: sıfır hızda
    /// sınırsız dönüş üretmez ve duvara sıkışan robotta kendiliğinden düşer.
    /// Hiçbir durumda hız üretmez, yalnızca sınırı aşağı çeker.
    /// </summary>
    [DefaultExecutionOrder(106)]
    [RequireComponent(typeof(RobotMotor))]
    [AddComponentMenu("Robot Savaslari/Driving/El Freni")]
    public class DriveHandbrake : MonoBehaviour
    {
        [Header("Manevra")]
        [Tooltip("Dönüş hızı çarpanı. Motorun çarpan tavanı üst sınırı belirler.")]
        [SerializeField, Min(1f)] private float turnScale = 1.8f;

        [Tooltip("İleri üst hız çarpanı: manevranın bedeli.")]
        [SerializeField, Range(0.1f, 1f)] private float speedScale = 0.55f;

        [Tooltip("Bu hızın altında el freni devreye girmez; yerinde spin olmasın diye.")]
        [SerializeField, Min(0f)] private float engageSpeed = 1.5f;

        private RobotMotor motor;
        private DriveBoost boost;

        /// <summary>Sürücü el freni istiyor mu?</summary>
        public bool IsRequested { get; private set; }

        /// <summary>El freni bu an uygulanıyor mu?</summary>
        public bool IsEngaged { get; private set; }

        /// <summary>
        /// İstek bu adımda uygulanabilir mi? Boost önceliği bu değere bakar,
        /// böylece iki özellik aynı karede çelişmez.
        /// </summary>
        public bool WouldEngage => IsRequested
                                   && motor != null
                                   && Mathf.Abs(motor.ForwardSpeed) >= engageSpeed;

        /// <summary>Uygulanan dönüş çarpanı.</summary>
        public float TurnScale => turnScale;

        /// <summary>Uygulanan hız çarpanı.</summary>
        public float SpeedScale => speedScale;

        /// <summary>Devreye girmek için gereken en az hız (m/s).</summary>
        public float EngageSpeed => engageSpeed;

        /// <summary>
        /// Boost şu an çarpanı uyguluyor mu? Bileşenler çalışma anında
        /// eklendiği için referans ilk ihtiyaçta çözülür.
        /// </summary>
        private bool BoostIsApplying
        {
            get
            {
                if (boost == null)
                {
                    boost = GetComponent<DriveBoost>();
                }

                return boost != null && boost.IsBoosting;
            }
        }

        private void Awake()
        {
            motor = GetComponent<RobotMotor>();
            boost = GetComponent<DriveBoost>();
        }

        /// <summary>Sürücünün el freni isteğini bildirir.</summary>
        public void SetHandbrakeRequested(bool requested) => IsRequested = requested;

        /// <summary>
        /// Maç sıfırlaması ve ikinci maç için: istek ve geçici dönüş durumu
        /// bırakılır.
        /// </summary>
        public void ResetState()
        {
            IsRequested = false;
            IsEngaged = false;

            if (motor != null)
            {
                motor.ClearDriveModifier();
            }
        }

        private void OnDisable()
        {
            IsRequested = false;
            IsEngaged = false;

            if (motor != null)
            {
                motor.ClearDriveModifier();
            }
        }

        private void FixedUpdate()
        {
            bool engaged = WouldEngage;

            if (engaged)
            {
                // Boost'tan sonra çalışır ve onun yazdığını ezer: el freni
                // önceliklidir, çünkü ikisi birlikte çelişkili bir istek olur.
                motor.SetDriveModifier(speedScale, 1f, turnScale);
            }
            else if (!BoostIsApplying)
            {
                // Boost uygulanmıyorsa çarpanı bırakmak bu bileşenin işi;
                // uygulanıyorsa boost'un yazdığı değer korunur.
                motor.ClearDriveModifier();
            }

            IsEngaged = engaged;
        }
    }
}
