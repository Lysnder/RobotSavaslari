using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.Atalay
{
    /// <summary>
    /// Oyuncunun klavye girdisini okuyup ortak <see cref="RobotMotor"/> bileşenine
    /// hareket komutu gönderir. Ortak hareket koduna dokunmaz; motorun tek bildiği
    /// <c>SetMoveCommand(forward, turn)</c> çağrısıdır.
    ///
    /// Kurallar:
    /// * İleri ve geri aynı anda basılıysa ileri/geri değeri 0 olur.
    /// * Sola ve sağa aynı anda basılıysa dönüş değeri 0 olur.
    /// * Tuş bırakıldığında ilgili eksen 0 gönderilir, yani komut kesilir.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Atalay/Oyuncu Klavye Surucusu")]
    public class PlayerKeyboardDriver : MonoBehaviour
    {
        [Tooltip("Boş bırakılırsa aynı nesnedeki (ya da üst nesnedeki) motor kullanılır.")]
        [SerializeField] private RobotMotor motor;

        [Header("Tuşlar")]
        [SerializeField] private Key forwardKey = Key.W;
        [SerializeField] private Key backwardKey = Key.S;
        [SerializeField] private Key turnLeftKey = Key.A;
        [SerializeField] private Key turnRightKey = Key.D;

        /// <summary>Bu karede okunan ileri/geri değeri (-1, 0 ya da 1).</summary>
        public float ForwardInput { get; private set; }

        /// <summary>Bu karede okunan dönüş değeri (-1, 0 ya da 1).</summary>
        public float TurnInput { get; private set; }

        /// <summary>Bağlı bir klavye var mı? Yoksa komut gönderilmez.</summary>
        public bool HasKeyboard { get; private set; }

        /// <summary>Komutların gönderildiği motor.</summary>
        public RobotMotor Motor
        {
            get => motor;
            set
            {
                if (motor != null && motor != value)
                {
                    motor.Stop();
                }

                motor = value;
            }
        }

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponentInParent<RobotMotor>();
            }

            if (motor == null)
            {
                Debug.LogError($"{nameof(PlayerKeyboardDriver)}: bagli bir {nameof(RobotMotor)} bulunamadi.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            // Kontrol kapatılınca okunan girdi de gönderilen komut da sıfırlanır,
            // böylece tekrar açıldığında eski değer kullanılmaz.
            ForwardInput = 0f;
            TurnInput = 0f;
            HasKeyboard = false;

            if (motor != null)
            {
                motor.Stop();
            }
        }

        private void Update()
        {
            if (motor == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            HasKeyboard = keyboard != null;

            if (!HasKeyboard)
            {
                ForwardInput = 0f;
                TurnInput = 0f;
                motor.Stop();
                return;
            }

            ForwardInput = ReadAxis(keyboard, forwardKey, backwardKey);
            TurnInput = ReadAxis(keyboard, turnRightKey, turnLeftKey);

            motor.SetMoveCommand(ForwardInput, TurnInput);
        }

        /// <summary>
        /// Tek eksen okur. İki tuş da basılıysa ya da hiçbiri basılı değilse 0 döner;
        /// karşıt tuşlar birbirini bu noktada sıfırlar.
        /// </summary>
        private static float ReadAxis(Keyboard keyboard, Key positiveKey, Key negativeKey)
        {
            bool positive = positiveKey != Key.None && keyboard[positiveKey].isPressed;
            bool negative = negativeKey != Key.None && keyboard[negativeKey].isPressed;

            if (positive == negative)
            {
                return 0f;
            }

            return positive ? 1f : -1f;
        }
    }
}
