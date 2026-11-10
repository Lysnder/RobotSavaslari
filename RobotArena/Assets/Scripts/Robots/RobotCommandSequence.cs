using System;
using UnityEngine;

namespace RobotSavaslari.Robots
{
    /// <summary>Belirli bir süre boyunca gönderilecek tek hareket komutu.</summary>
    [Serializable]
    public struct TimedRobotCommand
    {
        [Tooltip("Komutun uygulanacağı süre (saniye).")]
        [Min(0f)] public float duration;

        [Range(-1f, 1f)] public float forward;
        [Range(-1f, 1f)] public float turn;
    }

    /// <summary>
    /// Klavye olmadan aynı <see cref="RobotMotor"/> bileşenine komut gönderir.
    /// Sıradaki her adım süresi kadar uygulanır; yapay zekâ ya da ağ sürücüsü de
    /// aynı yoldan (SetMoveCommand) robotu sürecektir.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Robot Komut Dizisi")]
    public class RobotCommandSequence : MonoBehaviour
    {
        [Tooltip("Boş bırakılırsa aynı nesnedeki (ya da üst nesnedeki) motor kullanılır.")]
        [SerializeField] private RobotMotor motor;

        [Tooltip("Oyun başlarken diziyi kendiliğinden çalıştır.")]
        [SerializeField] private bool playOnStart;

        [Tooltip("Dizi bittiğinde başa dön.")]
        [SerializeField] private bool loop = true;

        [Tooltip("Dizi çalışırken klavye girdisini kapat, bittiğinde geri aç.")]
        [SerializeField] private bool overrideKeyboardInput = true;

        [SerializeField]
        private TimedRobotCommand[] commands =
        {
            new TimedRobotCommand { duration = 1.5f, forward = 1f, turn = 0f },
            new TimedRobotCommand { duration = 0.75f, forward = 0f, turn = 1f },
            new TimedRobotCommand { duration = 1.5f, forward = -1f, turn = 0f },
            new TimedRobotCommand { duration = 0.75f, forward = 0f, turn = -1f },
            new TimedRobotCommand { duration = 0.5f, forward = 0f, turn = 0f },
        };

        private KeyboardRobotInput keyboardInput;
        private int index;
        private float elapsed;

        /// <summary>Dizi şu an çalışıyor mu?</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Komutların gönderildiği motor.</summary>
        public RobotMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        /// <summary>Diziyi kod tarafından değiştirmek için.</summary>
        public void SetCommands(TimedRobotCommand[] newCommands)
        {
            commands = newCommands ?? Array.Empty<TimedRobotCommand>();
            index = 0;
            elapsed = 0f;
        }

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponentInParent<RobotMotor>();
            }

            keyboardInput = GetComponentInParent<KeyboardRobotInput>();

            if (motor == null)
            {
                Debug.LogError($"{nameof(RobotCommandSequence)}: bagli bir {nameof(RobotMotor)} bulunamadi.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        /// <summary>Diziyi baştan başlatır.</summary>
        public void Play()
        {
            if (motor == null || commands == null || commands.Length == 0)
            {
                return;
            }

            index = 0;
            elapsed = 0f;
            IsPlaying = true;

            if (overrideKeyboardInput && keyboardInput != null)
            {
                keyboardInput.enabled = false;
            }
        }

        /// <summary>Diziyi durdurur ve komutu bırakır.</summary>
        public void Stop()
        {
            IsPlaying = false;

            if (motor != null)
            {
                motor.Stop();
            }

            if (overrideKeyboardInput && keyboardInput != null)
            {
                keyboardInput.enabled = true;
            }
        }

        private void OnDisable()
        {
            if (IsPlaying)
            {
                Stop();
            }
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            TimedRobotCommand current = commands[index];
            motor.SetMoveCommand(current.forward, current.turn);

            elapsed += Time.deltaTime;
            if (elapsed < current.duration)
            {
                return;
            }

            elapsed = 0f;
            index++;

            if (index < commands.Length)
            {
                return;
            }

            index = 0;
            if (!loop)
            {
                Stop();
            }
        }
    }
}
