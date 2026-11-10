using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.Driving
{
    /// <summary>
    /// Deneme kontrolü: klavye bileşenini kapatıp aynı motora doğrudan komut
    /// gönderir. Botun kullanacağı yol da budur — motor, klavye bileşeninden
    /// bağımsız çalışır.
    ///
    /// Deneme sahnesinde <see cref="toggleKey"/> ile açılıp kapatılır.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Driving/Deneme Kontrolu")]
    public class DriveProbe : MonoBehaviour
    {
        [SerializeField] private RobotMotor motor;
        [SerializeField] private PlayerKeyboardDriver keyboardDriver;

        [Header("Deneme komutu")]
        [Range(-1f, 1f)] [SerializeField] private float probeForward = 1f;
        [Range(-1f, 1f)] [SerializeField] private float probeTurn = 0.35f;

        [Tooltip("Deneme kontrolünü açıp kapatan tuş.")]
        [SerializeField] private Key toggleKey = Key.P;

        /// <summary>Deneme kontrolü şu an motoru sürüyor mu?</summary>
        public bool IsDriving { get; private set; }

        /// <summary>Deneme komutunun ileri/geri değeri.</summary>
        public float ProbeForward => probeForward;

        /// <summary>Deneme komutunun dönüş değeri.</summary>
        public float ProbeTurn => probeTurn;

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponentInParent<RobotMotor>();
            }

            if (keyboardDriver == null)
            {
                keyboardDriver = GetComponentInParent<PlayerKeyboardDriver>();
            }

            if (motor == null)
            {
                Debug.LogError($"{nameof(DriveProbe)}: bagli bir {nameof(RobotMotor)} bulunamadi.", this);
                enabled = false;
            }
        }

        /// <summary>Klavyeyi kapatır ve deneme komutunu göndermeye başlar.</summary>
        public void StartDriving()
        {
            if (motor == null || IsDriving)
            {
                return;
            }

            if (keyboardDriver != null)
            {
                // Kapatılırken kendi OnDisable'ı motoru durdurur, yani eski
                // klavye komutu deneme komutuna karışmaz.
                keyboardDriver.enabled = false;
            }

            IsDriving = true;
        }

        /// <summary>Deneme komutunu bırakır ve klavyeyi geri açar.</summary>
        public void StopDriving()
        {
            if (!IsDriving)
            {
                return;
            }

            IsDriving = false;

            if (motor != null)
            {
                motor.Stop();
            }

            if (keyboardDriver != null)
            {
                keyboardDriver.enabled = true;
            }
        }

        /// <summary>Deneme kontrolünü açar ya da kapatır.</summary>
        public void Toggle()
        {
            if (IsDriving)
            {
                StopDriving();
            }
            else
            {
                StartDriving();
            }
        }

        private void OnDisable()
        {
            if (IsDriving)
            {
                StopDriving();
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
            {
                Toggle();
            }

            if (IsDriving && motor != null)
            {
                motor.SetMoveCommand(probeForward, probeTurn);
            }
        }
    }
}
