using RobotSavaslari.Robots;
using UnityEngine;

namespace RobotSavaslari.Atalay
{
    /// <summary>
    /// Deneme sahnesinde üç satırlık küçük bir metin gösterir:
    ///
    /// * <b>Girdi</b> — bileşenin klavyeden okuduğu değer.
    /// * <b>Komut</b> — motorun gerçekten aldığı değer.
    /// * <b>Fizik</b> — gövdenin o anki hızı.
    ///
    /// Üçünü yan yana görmek kontrol arızasını fizik davranışından ayırır:
    /// girdi 0 ama fizik hâlâ hareketliyse bu kalan fiziksel kaymadır, komut
    /// kesilmemesi değildir.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Atalay/Surus Telemetri Metni")]
    public class DriveTelemetryHud : MonoBehaviour
    {
        [SerializeField] private RobotMotor motor;
        [SerializeField] private PlayerKeyboardDriver keyboardDriver;
        [SerializeField] private DriveProbe driveProbe;

        [Header("Yerleşim")]
        [SerializeField] private Vector2 position = new Vector2(12f, 12f);
        [SerializeField] private Vector2 size = new Vector2(360f, 132f);
        [SerializeField] private int fontSize = 14;

        private GUIStyle style;

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

            if (driveProbe == null)
            {
                driveProbe = GetComponentInParent<DriveProbe>();
            }
        }

        private void OnGUI()
        {
            if (motor == null)
            {
                return;
            }

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    alignment = TextAnchor.UpperLeft,
                    richText = false,
                };
            }

            Rect box = new Rect(position.x, position.y, size.x, size.y);
            GUI.Box(box, GUIContent.none);

            Rect text = new Rect(box.x + 10f, box.y + 8f, box.width - 20f, box.height - 16f);
            GUI.Label(text, BuildText(), style);
        }

        private string BuildText()
        {
            string source = "yok";
            float inputForward = 0f;
            float inputTurn = 0f;

            if (driveProbe != null && driveProbe.IsDriving)
            {
                source = "deneme kontrolu (klavye kapali)";
                inputForward = driveProbe.ProbeForward;
                inputTurn = driveProbe.ProbeTurn;
            }
            else if (keyboardDriver != null && keyboardDriver.enabled)
            {
                source = keyboardDriver.HasKeyboard ? "klavye" : "klavye yok";
                inputForward = keyboardDriver.ForwardInput;
                inputTurn = keyboardDriver.TurnInput;
            }

            return
                $"Kaynak : {source}\n" +
                $"Girdi  : ileri/geri {inputForward,5:0.00}   donus {inputTurn,5:0.00}\n" +
                $"Komut  : ileri/geri {motor.MoveCommand,5:0.00}   donus {motor.TurnCommand,5:0.00}\n" +
                $"Fizik  : hiz {motor.ForwardSpeed,5:0.00} m/s   donus {motor.TurnSpeed,6:0.0} derece/s\n" +
                "\n" +
                "W/S ileri-geri   A/D donus   P deneme kontrolu";
        }
    }
}
