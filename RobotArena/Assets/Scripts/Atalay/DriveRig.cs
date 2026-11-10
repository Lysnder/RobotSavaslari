using System;
using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.Atalay
{
    /// <summary>Karşılaştırılan tek bir ayar seti: adı ve o ayarları taşıyan prefab varyantı.</summary>
    [Serializable]
    public class DriveSettingSet
    {
        public string setName = "Ayar";

        [TextArea(1, 3)]
        public string note = "";

        public GameObject robotPrefab;
    }

    /// <summary>
    /// Deneme düzeneği: seçili ayar setinden robotu doğurur, seçili rotanın
    /// başlangıcına bırakır, kamerayı ve telemetriyi ona bağlar.
    ///
    /// Aynı rotayı iki ayar setiyle art arda denemek ve her denemeyi aynı
    /// noktadan başlatmak bu bileşenin tek işidir; sürüş mantığı hâlâ
    /// <see cref="PlayerKeyboardDriver"/> ile ortak <see cref="RobotMotor"/>
    /// arasındadır.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Atalay/Deneme Duzenegi")]
    public class DriveRig : MonoBehaviour
    {
        [SerializeField] private DriveSettingSet[] settingSets = new DriveSettingSet[0];
        [SerializeField] private RouteCourse[] routes = new RouteCourse[0];
        [SerializeField] private TestCameraFollow cameraFollow;
        [SerializeField] private DriveTelemetryHud hud;

        [Header("Tuşlar")]
        [SerializeField] private Key resetKey = Key.R;
        [SerializeField] private Key nextRouteKey = Key.Tab;
        [SerializeField] private Key firstSetKey = Key.Digit1;
        [SerializeField] private Key secondSetKey = Key.Digit2;
        [SerializeField] private Key toggleControlKey = Key.C;

        private GameObject robot;

        /// <summary>Doğurulmuş robotun motoru.</summary>
        public RobotMotor Motor { get; private set; }

        /// <summary>Doğurulmuş robotun klavye bileşeni.</summary>
        public PlayerKeyboardDriver Driver { get; private set; }

        /// <summary>Doğurulmuş robotun deneme kontrolü.</summary>
        public DriveProbe Probe { get; private set; }

        /// <summary>Doğurulmuş robotun çarpışma raporu.</summary>
        public CollisionReporter Reporter { get; private set; }

        /// <summary>Şu an kullanılan ayar seti.</summary>
        public DriveSettingSet ActiveSet =>
            settingSets != null && activeSetIndex >= 0 && activeSetIndex < settingSets.Length
                ? settingSets[activeSetIndex]
                : null;

        /// <summary>Şu an seçili rota.</summary>
        public RouteCourse ActiveRoute =>
            routes != null && activeRouteIndex >= 0 && activeRouteIndex < routes.Length
                ? routes[activeRouteIndex]
                : null;

        /// <summary>Robot son sıfırlamadan beri kaç saniyedir sürülüyor?</summary>
        public float RunTime { get; private set; }

        private int activeSetIndex;
        private int activeRouteIndex;

        private void Start()
        {
            Spawn(activeSetIndex, activeRouteIndex);
        }

        /// <summary>Seçili ayar setini ve rotayı kurar, robotu baştan doğurur.</summary>
        public void Spawn(int setIndex, int routeIndex)
        {
            if (settingSets == null || settingSets.Length == 0)
            {
                Debug.LogError($"{nameof(DriveRig)}: ayar seti tanimli degil.", this);
                return;
            }

            activeSetIndex = Mathf.Clamp(setIndex, 0, settingSets.Length - 1);
            activeRouteIndex = routes != null && routes.Length > 0
                ? Mathf.Clamp(routeIndex, 0, routes.Length - 1)
                : 0;

            if (robot != null)
            {
                Destroy(robot);
            }

            GameObject prefab = settingSets[activeSetIndex].robotPrefab;
            if (prefab == null)
            {
                Debug.LogError($"{nameof(DriveRig)}: '{settingSets[activeSetIndex].setName}' setinde prefab yok.", this);
                return;
            }

            RouteCourse route = ActiveRoute;
            Vector3 position = route != null ? route.StartPosition : transform.position;
            Quaternion rotation = route != null ? route.StartRotation : transform.rotation;

            robot = Instantiate(prefab, position, rotation);
            robot.name = $"Robot ({settingSets[activeSetIndex].setName})";

            Motor = robot.GetComponent<RobotMotor>();
            Driver = robot.GetComponent<PlayerKeyboardDriver>();
            Probe = robot.GetComponent<DriveProbe>();
            Reporter = robot.GetComponent<CollisionReporter>();

            if (cameraFollow != null)
            {
                cameraFollow.Target = robot.transform;
            }

            if (hud != null)
            {
                hud.Bind(this);
            }

            RunTime = 0f;
        }

        /// <summary>Robotu seçili rotanın başlangıcına, duruş halinde bırakır.</summary>
        public void ResetToRouteStart()
        {
            Spawn(activeSetIndex, activeRouteIndex);
        }

        /// <summary>Bir sonraki rotaya geçer ve robotu oraya bırakır.</summary>
        public void NextRoute()
        {
            if (routes == null || routes.Length == 0)
            {
                return;
            }

            Spawn(activeSetIndex, (activeRouteIndex + 1) % routes.Length);
        }

        /// <summary>Ayar setini değiştirir; robot aynı rota başlangıcında yeniden doğar.</summary>
        public void SelectSet(int setIndex)
        {
            Spawn(setIndex, activeRouteIndex);
        }

        /// <summary>
        /// Oyuncu kontrolünü kapatır ya da açar. A3'ün "kontrol kapatılıp tekrar
        /// açılabilsin" maddesi için sahnedeki deneme düğmesi de bunu çağırır.
        /// </summary>
        public void ToggleControl()
        {
            if (Driver == null)
            {
                return;
            }

            Driver.enabled = !Driver.enabled;
        }

        private void Update()
        {
            RunTime += Time.deltaTime;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (Pressed(keyboard, resetKey))
            {
                ResetToRouteStart();
            }
            else if (Pressed(keyboard, nextRouteKey))
            {
                NextRoute();
            }
            else if (Pressed(keyboard, firstSetKey))
            {
                SelectSet(0);
            }
            else if (Pressed(keyboard, secondSetKey))
            {
                SelectSet(1);
            }
            else if (Pressed(keyboard, toggleControlKey))
            {
                ToggleControl();
            }
        }

        private static bool Pressed(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        private void OnGUI()
        {
            const float width = 190f;
            float x = Screen.width - width - 12f;

            GUI.Box(new Rect(x, 12f, width, 112f), GUIContent.none);

            if (GUI.Button(new Rect(x + 10f, 22f, width - 20f, 26f), "Rotayi bastan al (R)"))
            {
                ResetToRouteStart();
            }

            if (GUI.Button(new Rect(x + 10f, 52f, width - 20f, 26f), "Sonraki rota (Tab)"))
            {
                NextRoute();
            }

            bool controlOn = Driver != null && Driver.enabled;
            string label = controlOn ? "Kontrolu kapat (C)" : "Kontrolu ac (C)";
            if (GUI.Button(new Rect(x + 10f, 82f, width - 20f, 26f), label))
            {
                ToggleControl();
            }
        }
    }
}
