using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RobotSavaslari.Robots
{
#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Klavyeyi okur ve <see cref="RobotMotor"/> bileşenine hareket komutu
    /// gönderir. Hareket mantığı burada değil motorda durur; bu bileşen
    /// kaldırıldığında ya da kapatıldığında robot aynı motorla başka bir
    /// kaynaktan (yapay zekâ, ağ, senaryo) sürülebilir.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Klavye Robot Girdisi")]
    public class KeyboardRobotInput : MonoBehaviour
    {
        [Tooltip("Boş bırakılırsa aynı nesnedeki (ya da üst nesnedeki) motor kullanılır.")]
        [SerializeField] private RobotMotor motor;

        [Header("Tuşlar")]
        [SerializeField] private Key forwardKey = Key.W;
        [SerializeField] private Key backwardKey = Key.S;
        [SerializeField] private Key turnLeftKey = Key.A;
        [SerializeField] private Key turnRightKey = Key.D;

        [Tooltip("Yön tuşları da aynı komutları üretsin.")]
        [SerializeField] private bool alsoUseArrowKeys = true;

        /// <summary>Komutların gönderildiği motor; sahnede sonradan değiştirilebilir.</summary>
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
                Debug.LogError($"{nameof(KeyboardRobotInput)}: bagli bir {nameof(RobotMotor)} bulunamadi.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            // Girdi kaynağı susunca komut da bırakılır.
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
            if (keyboard == null)
            {
                // Klavye yoksa robot komutsuz kalır, son komutla kaymaya devam etmez.
                motor.Stop();
                return;
            }

            float forward = KeyboardDriveAxes.ReadAxis(keyboard, forwardKey, backwardKey,
                alsoUseArrowKeys ? Key.UpArrow : Key.None, alsoUseArrowKeys ? Key.DownArrow : Key.None);
            float turn = KeyboardDriveAxes.ReadAxis(keyboard, turnRightKey, turnLeftKey,
                alsoUseArrowKeys ? Key.RightArrow : Key.None, alsoUseArrowKeys ? Key.LeftArrow : Key.None);

            // Tuş bırakıldığında eksen 0 olur, yani hareket girdisi kesilir.
            motor.SetMoveCommand(forward, turn);
        }

    }
#else
    /// <summary>
    /// Input System arka ucu kapalıyken derlenen boş sürüm. Project Settings >
    /// Player > Active Input Handling "Input System Package" (ya da "Both")
    /// olmalıdır.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Klavye Robot Girdisi")]
    public class KeyboardRobotInput : MonoBehaviour
    {
        private void Awake()
        {
            Debug.LogError(
                $"{nameof(KeyboardRobotInput)} Input System paketini gerektirir. " +
                "Player ayarlarinda Active Input Handling'i 'Input System Package' yapin.", this);
            enabled = false;
        }
    }
#endif
}
