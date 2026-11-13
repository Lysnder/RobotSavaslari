using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.Driving
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
    [AddComponentMenu("Robot Savaslari/Driving/Oyuncu Klavye Surucusu")]
    public class PlayerKeyboardDriver : MonoBehaviour
    {
        [Tooltip("Boş bırakılırsa aynı nesnedeki (ya da üst nesnedeki) motor kullanılır.")]
        [SerializeField] private RobotMotor motor;

        [Header("Tuşlar")]
        [SerializeField] private Key forwardKey = Key.W;
        [SerializeField] private Key backwardKey = Key.S;
        [SerializeField] private Key turnLeftKey = Key.A;
        [SerializeField] private Key turnRightKey = Key.D;

        [Tooltip("Space kamera/silah tarafında olduğu için boost varsayılanı LeftShift.")]
        [SerializeField] private Key boostKey = Key.LeftShift;

        [Tooltip("Space kamera/silah tarafında olduğu için el freni varsayılanı LeftCtrl.")]
        [SerializeField] private Key handbrakeKey = Key.LeftCtrl;

        [Tooltip("Boost enerjisi; boş bırakılırsa aynı nesnedeki bileşen kullanılır. Yoksa boost yok sayılır.")]
        [SerializeField] private DriveBoost boost;

        [Tooltip("El freni; boş bırakılırsa aynı nesnedeki bileşen kullanılır. Yoksa el freni yok sayılır.")]
        [SerializeField] private DriveHandbrake handbrake;

        /// <summary>Bu karede okunan ileri/geri değeri (-1, 0 ya da 1).</summary>
        public float ForwardInput { get; private set; }

        /// <summary>Bu karede okunan dönüş değeri (-1, 0 ya da 1).</summary>
        public float TurnInput { get; private set; }

        /// <summary>Boost tuşu bu karede basılı mı?</summary>
        public bool BoostInput { get; private set; }

        /// <summary>El freni tuşu bu karede basılı mı?</summary>
        public bool HandbrakeInput { get; private set; }

        /// <summary>Bağlı bir klavye var mı? Yoksa komut gönderilmez.</summary>
        public bool HasKeyboard { get; private set; }

        /// <summary>Oyun penceresi odakta mı? Değilse girdi okunmaz.</summary>
        public bool HasFocus { get; private set; } = true;

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

        /// <summary>
        /// Boost enerjisi. Üretim bağlantısı bileşeni çalışma anında eklediği
        /// için dışarıdan atanabilir; atanmazsa boost tuşu yok sayılır.
        /// </summary>
        public DriveBoost Boost
        {
            get => boost;
            set
            {
                if (boost != null && boost != value)
                {
                    boost.SetBoostRequested(false);
                }

                boost = value;
            }
        }

        /// <summary>
        /// El freni. Üretim bağlantısı bileşeni çalışma anında eklediği için
        /// dışarıdan atanabilir; atanmazsa el freni tuşu yok sayılır.
        /// </summary>
        public DriveHandbrake Handbrake
        {
            get => handbrake;
            set
            {
                if (handbrake != null && handbrake != value)
                {
                    handbrake.SetHandbrakeRequested(false);
                }

                handbrake = value;
            }
        }

        /// <summary>
        /// Eşlemeleri uygular. Tuşlar bu bileşende durduğu için kalıcı eşleme
        /// deposu değerleri buraya yazar; depo yokken sahnede kurulan
        /// varsayılanlar geçerli kalır.
        /// </summary>
        public void ApplyBindings(Key forward, Key backward, Key left, Key right, Key boostAction, Key handbrakeAction)
        {
            forwardKey = forward;
            backwardKey = backward;
            turnLeftKey = left;
            turnRightKey = right;
            boostKey = boostAction;
            handbrakeKey = handbrakeAction;
        }

        /// <summary>Verilen eylemin şu anki tuşu; eşleme ekranı bunu gösterir.</summary>
        public Key GetBinding(DriveAction action)
        {
            switch (action)
            {
                case DriveAction.Forward: return forwardKey;
                case DriveAction.Backward: return backwardKey;
                case DriveAction.TurnLeft: return turnLeftKey;
                case DriveAction.TurnRight: return turnRightKey;
                case DriveAction.Boost: return boostKey;
                case DriveAction.Handbrake: return handbrakeKey;
                default: return Key.None;
            }
        }

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponentInParent<RobotMotor>();
            }

            if (boost == null)
            {
                boost = GetComponent<DriveBoost>();
            }

            if (handbrake == null)
            {
                handbrake = GetComponent<DriveHandbrake>();
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
            HasKeyboard = false;
            ClearInput();
        }

        /// <summary>
        /// Pencere değişince basılı kalan tuş komutu sürdürmesin: odak
        /// kaybında girdi de komut da temizlenir. Odak geri geldiğinde değerler
        /// o karede klavyeden yeniden okunur, yani tuşa basılmadıkça robot
        /// komut almaz.
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            HasFocus = hasFocus;

            if (hasFocus)
            {
                return;
            }

            ClearInput();
        }

        private void ClearInput()
        {
            ForwardInput = 0f;
            TurnInput = 0f;
            BoostInput = false;
            HandbrakeInput = false;

            if (motor != null)
            {
                motor.Stop();
            }

            // Odak kaybında ya da kontrol kapanınca basılı boost ve el freni
            // sürmemeli; geçici dönüş durumu da burada bırakılır.
            if (boost != null)
            {
                boost.SetBoostRequested(false);
            }

            if (handbrake != null)
            {
                handbrake.SetHandbrakeRequested(false);
            }
        }

        private void Update()
        {
            if (motor == null)
            {
                return;
            }

            if (!HasFocus)
            {
                ClearInput();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            HasKeyboard = keyboard != null;

            if (!HasKeyboard)
            {
                // Klavye yoksa robot komutsuz kalır, son komutla kaymaya devam etmez.
                ClearInput();
                return;
            }

            ForwardInput = KeyboardDriveAxes.ReadAxis(keyboard, forwardKey, backwardKey, Key.UpArrow, Key.DownArrow);
            TurnInput = KeyboardDriveAxes.ReadAxis(keyboard, turnRightKey, turnLeftKey, Key.RightArrow, Key.LeftArrow);
            BoostInput = KeyboardDriveAxes.IsHeld(keyboard, boostKey);
            HandbrakeInput = KeyboardDriveAxes.IsHeld(keyboard, handbrakeKey);

            motor.SetMoveCommand(ForwardInput, TurnInput);

            if (boost != null)
            {
                boost.SetBoostRequested(BoostInput);
            }

            if (handbrake != null)
            {
                handbrake.SetHandbrakeRequested(HandbrakeInput);
            }
        }
    }
}
