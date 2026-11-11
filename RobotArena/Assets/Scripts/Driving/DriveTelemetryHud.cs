using RobotSavaslari.Robots;
using UnityEngine;

namespace RobotSavaslari.Driving
{
    /// <summary>
    /// Deneme sahnelerinde sol üstte küçük bir metin gösterir:
    ///
    /// * <b>Girdi</b> — bileşenin klavyeden okuduğu değer.
    /// * <b>Komut</b> — motorun gerçekten aldığı değer.
    /// * <b>Fizik</b> — gövdenin o anki hızı.
    ///
    /// Üçünü yan yana görmek kontrol arızasını fizik davranışından ayırır:
    /// girdi 0 ama fizik hâlâ hareketliyse bu kalan fiziksel kaymadır, komutun
    /// kesilmemesi değildir.
    ///
    /// Robotu sahnede <see cref="DriveRig"/> doğuruyorsa referanslar
    /// <see cref="Bind"/> ile her doğuşta yenilenir; robotun üstüne doğrudan
    /// eklendiğinde kendi nesnesinden bulur.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Driving/Surus Telemetri Metni")]
    public class DriveTelemetryHud : MonoBehaviour
    {
        [SerializeField] private RobotMotor motor;
        [SerializeField] private PlayerKeyboardDriver keyboardDriver;
        [SerializeField] private DriveProbe driveProbe;
        [SerializeField] private CollisionReporter collisionReporter;

        [Header("Yerleşim")]
        [SerializeField] private Vector2 position = new Vector2(12f, 12f);
        [SerializeField] private Vector2 size = new Vector2(380f, 190f);
        [SerializeField] private int fontSize = 14;

        private DriveRig rig;
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

            if (collisionReporter == null)
            {
                collisionReporter = GetComponentInParent<CollisionReporter>();
            }
        }

        /// <summary>Düzeneğin doğurduğu robota bağlanır.</summary>
        public void Bind(DriveRig driveRig)
        {
            rig = driveRig;

            if (rig == null)
            {
                return;
            }

            motor = rig.Motor;
            keyboardDriver = rig.Driver;
            driveProbe = rig.Probe;
            collisionReporter = rig.Reporter;
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
                source = !keyboardDriver.HasFocus
                    ? "odak disinda - girdi okunmuyor"
                    : keyboardDriver.HasKeyboard ? "klavye" : "klavye yok";
                inputForward = keyboardDriver.ForwardInput;
                inputTurn = keyboardDriver.TurnInput;
            }
            else if (keyboardDriver != null)
            {
                source = "kontrol kapali";
            }

            string header = string.Empty;
            if (rig != null)
            {
                string setName = rig.ActiveSet != null ? rig.ActiveSet.setName : "-";
                string routeName = rig.ActiveRoute != null ? rig.ActiveRoute.RouteName : "-";
                header =
                    $"Rota   : {routeName}   ({rig.RunTime,5:0.0} s)\n" +
                    $"Ayar   : {setName}\n";
            }

            string collision = string.Empty;
            if (collisionReporter != null && collisionReporter.HasReport)
            {
                collision =
                    $"\nCarpisma: {collisionReporter.ImpactSpeed,4:0.0} m/s, " +
                    $"{collisionReporter.ImpactAngle,3:0} derece -> kalan {collisionReporter.ResidualSpeed,4:0.0} m/s";
            }

            string instructions = rig != null && rig.ActiveRoute != null && !string.IsNullOrEmpty(rig.ActiveRoute.Instructions)
                ? $"\n{rig.ActiveRoute.Instructions}"
                : string.Empty;

            return
                header +
                $"Kaynak : {source}\n" +
                $"Girdi  : ileri/geri {inputForward,5:0.00}   donus {inputTurn,5:0.00}\n" +
                $"Komut  : ileri/geri {motor.MoveCommand,5:0.00}   donus {motor.TurnCommand,5:0.00}\n" +
                $"Fizik  : hiz {motor.ForwardSpeed,5:0.00} m/s   donus {motor.TurnSpeed,6:0.0} derece/s" +
                collision +
                "\n\nSurus: WASD veya ok tuslari   P: deneme kontrolu" +
                instructions;
        }
    }
}
