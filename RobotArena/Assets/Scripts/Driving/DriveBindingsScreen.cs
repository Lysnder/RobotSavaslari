using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace RobotSavaslari.Driving
{
    /// <summary>
    /// Sürüş tuşlarını değiştirme ekranı. Altı eylemi listeler, tuş yakalar ve
    /// çakışmayı görünür biçimde reddeder.
    ///
    /// Ekran açıkken sürüş kontrolü kapatılır: yakalama sırasında basılan tuş
    /// oyuna komut olarak gitmez. Kapanınca kontrol eski durumuna döner.
    ///
    /// Erişimin ne zaman açık olduğuna sahneyi tanıyan taraf karar verir ve
    /// <see cref="AccessAllowed"/> ile bildirir; bu bileşen maç durumunu
    /// bilmez.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Driving/Surus Tuslari Ekrani")]
    public class DriveBindingsScreen : MonoBehaviour
    {
        [SerializeField] private PlayerKeyboardDriver driver;

        [Header("Yerleşim")]
        [SerializeField] private Vector2 panelSize = new Vector2(360f, 268f);
        [SerializeField] private Vector2 accessSize = new Vector2(190f, 30f);
        [SerializeField] private float accessBottomMargin = 24f;

        private static readonly DriveAction[] Actions =
        {
            DriveAction.Forward,
            DriveAction.Backward,
            DriveAction.TurnLeft,
            DriveAction.TurnRight,
            DriveAction.Boost,
            DriveAction.Handbrake,
        };

        private GUIStyle labelStyle;
        private bool driverWasEnabled;
        private string message = string.Empty;

        /// <summary>Eşlemelerin uygulanacağı sürücü.</summary>
        public PlayerKeyboardDriver Driver
        {
            get => driver;
            set => driver = value;
        }

        /// <summary>Ekran açık mı?</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Şu an hangi eylem için tuş bekleniyor? Yoksa null.</summary>
        public DriveAction? Capturing { get; private set; }

        /// <summary>Erişim düğmesi gösterilsin mi? Sahneyi tanıyan taraf belirler.</summary>
        public bool AccessAllowed { get; set; }

        /// <summary>Son reddetme ya da bilgi mesajı.</summary>
        public string Message => message;

        private void Awake()
        {
            if (driver == null)
            {
                driver = GetComponent<PlayerKeyboardDriver>();
            }
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        /// <summary>Ekranı açar ve sürüş kontrolünü geçici olarak kapatır.</summary>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            Capturing = null;
            message = string.Empty;

            if (driver != null)
            {
                driverWasEnabled = driver.enabled;
                driver.enabled = false;
            }
        }

        /// <summary>Ekranı kapatır ve kontrolü eski durumuna döndürür.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            Capturing = null;

            if (driver != null)
            {
                driver.enabled = driverWasEnabled;
            }
        }

        /// <summary>Verilen eylem için tuş yakalamaya başlar.</summary>
        public void BeginCapture(DriveAction action)
        {
            Capturing = action;
            message = $"{DriveBindings.Describe(action)} icin bir tusa bas (Esc iptal).";
        }

        /// <summary>Yakalamayı iptal eder.</summary>
        public void CancelCapture()
        {
            Capturing = null;
            message = "Degisiklik yapilmadi.";
        }

        /// <summary>
        /// Yakalanan tuşu uygular. Çakışma ya da sahiplenilmeyen tuş durumunda
        /// eşleme değişmez ve sebep mesaja yazılır.
        /// </summary>
        public bool TryCapture(Key key)
        {
            if (!Capturing.HasValue)
            {
                return false;
            }

            DriveAction action = Capturing.Value;

            if (key == Key.Escape)
            {
                CancelCapture();
                return false;
            }

            if (!DriveBindings.TryAssign(action, key, out string rejection))
            {
                message = rejection;
                return false;
            }

            DriveBindings.Save();
            DriveBindings.Apply(driver);
            Capturing = null;
            message = $"{DriveBindings.Describe(action)}: {key}";
            return true;
        }

        /// <summary>Yalnızca sürüş eşlemelerini varsayılana döndürür.</summary>
        public void ResetToDefaults()
        {
            DriveBindings.ResetToDefaults();
            DriveBindings.Save();
            DriveBindings.Apply(driver);
            Capturing = null;
            message = "Surus tuslari varsayilana dondu.";
        }

        private void Update()
        {
            // Maç başladıysa ya da ayarlar açıldıysa ekran kendiliğinden kapanır.
            if (IsOpen && !AccessAllowed)
            {
                Close();
                return;
            }

            if (!IsOpen || !Capturing.HasValue)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                // Klavye çıkarıldıysa yakalama güvenle biter.
                Capturing = null;
                message = "Klavye bulunamadi.";
                return;
            }

            foreach (KeyControl control in keyboard.allKeys)
            {
                if (control.wasPressedThisFrame)
                {
                    TryCapture(control.keyCode);
                    return;
                }
            }
        }

        private void OnGUI()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            }

            if (!IsOpen)
            {
                DrawAccess();
                return;
            }

            DrawPanel();
        }

        private void DrawAccess()
        {
            if (!AccessAllowed)
            {
                return;
            }

            // Ready erişimi alt ortada.
            Rect rect = new Rect((Screen.width - accessSize.x) * 0.5f,
                Screen.height - accessSize.y - accessBottomMargin,
                accessSize.x, accessSize.y);

            if (GUI.Button(rect, "Surus tuslari"))
            {
                Open();
            }
        }

        private void DrawPanel()
        {
            Rect panel = new Rect((Screen.width - panelSize.x) * 0.5f,
                (Screen.height - panelSize.y) * 0.5f,
                panelSize.x, panelSize.y);

            GUI.Box(panel, "Surus tuslari");

            float x = panel.x + 16f;
            float width = panel.width - 32f;
            float y = panel.y + 32f;

            foreach (DriveAction action in Actions)
            {
                GUI.Label(new Rect(x, y + 4f, width * 0.5f, 22f), DriveBindings.Describe(action), labelStyle);

                bool capturing = Capturing.HasValue && Capturing.Value == action;
                string caption = capturing ? "... tusa bas" : BindingLabel(action);

                if (GUI.Button(new Rect(x + width * 0.5f, y, width * 0.5f, 24f), caption) && !capturing)
                {
                    BeginCapture(action);
                }

                y += 28f;
            }

            y += 6f;
            GUI.Label(new Rect(x, y, width, 36f), message, labelStyle);

            float buttonY = panel.yMax - 36f;
            if (GUI.Button(new Rect(x, buttonY, width * 0.48f, 26f), "Varsayilana don"))
            {
                ResetToDefaults();
            }

            if (GUI.Button(new Rect(x + width * 0.52f, buttonY, width * 0.48f, 26f), "Kapat"))
            {
                Close();
            }
        }

        private string BindingLabel(DriveAction action)
        {
            Key key = driver != null ? driver.GetBinding(action) : DriveBindings.Get(action);

            bool hasArrowAlias = action == DriveAction.Forward || action == DriveAction.Backward
                                 || action == DriveAction.TurnLeft || action == DriveAction.TurnRight;

            return hasArrowAlias ? $"{key} / ok" : key.ToString();
        }
    }
}
