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
        private GUIStyle titleStyle;
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
        public bool ShowReadyShortcut { get; set; } = true;

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
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleLeft };
                titleStyle = new GUIStyle(labelStyle) { fontSize = 29, fontStyle = FontStyle.Bold };
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
            if (!ShowReadyShortcut || !AccessAllowed)
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
            Matrix4x4 previous = GUI.matrix;
            Color color = GUI.color;
            try
            {
                float scale = Mathf.Max(.01f, Mathf.Min(Screen.height / 720f, Screen.width / 840f));
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                float width = Screen.width / scale, height = Screen.height / scale;
                float w = Mathf.Max(panelSize.x, 482f), h = Mathf.Max(panelSize.y, 422f);
                var panel = new Rect((width - w) * .5f, (height - h) * .5f, w, h);
                if (Event.current.type == EventType.Repaint)
                {
                    GUI.color = new Color32(13, 19, 25, 224);
                    GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
                    GUI.color = new Color32(21, 29, 38, 255);
                    GUI.DrawTexture(panel, Texture2D.whiteTexture);
                    GUI.color = new Color32(255, 183, 85, 255);
                    GUI.DrawTexture(new Rect(panel.x, panel.y, 4f, 64f), Texture2D.whiteTexture);
                    GUI.color = color;
                }

                float x = panel.x + 24f, inner = panel.width - 48f;
                GUI.Label(new Rect(x, panel.y + 15, inner, 38), "Sürüş tuşları", titleStyle);
                float y = panel.y + 68f;
                foreach (DriveAction action in Actions)
                {
                    GUI.Label(new Rect(x, y, inner * .51f, 32f), DriveBindings.Describe(action), labelStyle);
                    bool capturing = Capturing.HasValue && Capturing.Value == action;
                    string caption = capturing ? "Yeni tuşa bas…" : BindingLabel(action);
                    if (GUI.Button(new Rect(x + inner * .53f, y, inner * .47f, 30f), caption) && !capturing)
                        BeginCapture(action);
                    y += 36f;
                }

                GUI.Label(new Rect(x, y + 6f, inner, 58f), string.IsNullOrEmpty(message)
                    ? "Değiştirmek istediğin eylemin tuşuna tıkla." : message, labelStyle);

                float buttonY = panel.yMax - 57f;
                if (GUI.Button(new Rect(x, buttonY, inner * .56f, 36f), "Varsayılanlara dön")) ResetToDefaults();
                if (GUI.Button(new Rect(x + inner * .59f, buttonY, inner * .41f, 36f), "Geri")) Close();
            }
            finally
            {
                GUI.color = color;
                GUI.matrix = previous;
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
