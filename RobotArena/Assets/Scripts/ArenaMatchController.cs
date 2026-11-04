using UnityEngine;

namespace RobotSavaslari
{
    public sealed class ArenaMatchController : MonoBehaviour
    {
        private RobotHealth _robotOne;
        private RobotHealth _robotTwo;
        private bool _matchFinished;
        private string _winner;
        private GUIStyle _titleStyle;

        private readonly Vector3 _robotOneStart = new Vector3(3.15f, 0.82f, 0f);
        private readonly Vector3 _robotTwoStart = new Vector3(-3.15f, 0.82f, 0f);

        public bool MatchFinished => _matchFinished;

        private void Start()
        {
            _robotOne = FindSceneRobot("Kirmizi Robot") ??
                        CreateRobot("Kirmizi Robot", _robotOneStart, new Color(0.85f, 0.16f, 0.12f, 1f));
            _robotTwo = FindSceneRobot("Mavi Robot") ??
                        CreateRobot("Mavi Robot", _robotTwoStart, new Color(0.12f, 0.38f, 0.9f, 1f));

            ResetRobot(_robotOne, _robotOneStart, Quaternion.Euler(0f, -90f, 0f));
            ResetRobot(_robotTwo, _robotTwoStart, Quaternion.Euler(0f, 90f, 0f));

            _robotOne.Defeated += HandleRobotDefeated;
            _robotTwo.Defeated += HandleRobotDefeated;
            ConfigureCombat();
            PositionCamera();
        }

        private void ConfigureCombat()
        {
            var red = _robotOne.GetComponent<RobotPrototypeController>();
            var blue = _robotTwo.GetComponent<RobotPrototypeController>();
            red.ConfigureCombat(true, blue);
            blue.ConfigureCombat(false, red);
            RobotPrototypeController.Select(blue);
        }

        private void OnDestroy()
        {
            if (_robotOne != null) _robotOne.Defeated -= HandleRobotDefeated;
            if (_robotTwo != null) _robotTwo.Defeated -= HandleRobotDefeated;
        }

        private static RobotHealth FindSceneRobot(string robotName)
        {
            GameObject robot = GameObject.Find(robotName);
            return robot != null ? robot.GetComponent<RobotHealth>() : null;
        }

        private static void ResetRobot(RobotHealth robot, Vector3 position, Quaternion rotation)
        {
            robot.transform.SetPositionAndRotation(position, rotation);
            RobotPrototypeController controller = robot.GetComponent<RobotPrototypeController>();
            if (controller != null)
                controller.ResetMotion();
        }

        private static void PositionCamera()
        {
            Camera arenaCamera = Camera.main;
            if (arenaCamera == null)
            {
                return;
            }

            arenaCamera.transform.position = new Vector3(0f, 14f, -10f);
            arenaCamera.transform.LookAt(new Vector3(0f, 0.8f, 0f));
            arenaCamera.fieldOfView = 50f;
        }

        private static RobotHealth CreateRobot(string robotName, Vector3 spawnPosition, Color color)
        {
            GameObject robot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            robot.name = robotName;
            robot.transform.position = spawnPosition;
            robot.GetComponent<Renderer>().material = CreateMaterial(color);
            robot.AddComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            robot.AddComponent<RobotPrototypeController>();
            return robot.GetComponent<RobotHealth>();
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = color;
            return material;
        }
        private void HandleRobotDefeated(RobotHealth defeatedRobot)
        {
            RobotHealth remainingRobot = defeatedRobot == _robotOne ? _robotTwo : _robotOne;
            _winner = remainingRobot.IsDefeated ? "Berabere" : remainingRobot.gameObject.name;
            _matchFinished = true;
            _robotOne.GetComponent<RobotPrototypeController>().StopCombat();
            _robotTwo.GetComponent<RobotPrototypeController>().StopCombat();
        }

        private void OnGUI()
        {
            if (_titleStyle == null && GUI.skin != null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            GUI.Label(new Rect(0f, 12f, Screen.width, 34f), "ROBOT SAVASLARI - ARENA", _titleStyle);
            DrawRobotPanel(_robotOne, 24f, 105f, new Color(0.85f, 0.16f, 0.12f, 1f), "BILGISAYAR - Maviyi takip eder");
            DrawRobotPanel(_robotTwo, Screen.width - 324f, 105f, new Color(0.12f, 0.38f, 0.9f, 1f), "OYUNCU - WASD / Ok tuslari");

            if (_matchFinished)
            {
                Rect resultPanel = new Rect((Screen.width - 360f) / 2f, (Screen.height - 150f) / 2f, 360f, 150f);
                GUI.Box(resultPanel, string.Empty);
                GUI.Label(new Rect(resultPanel.x + 10f, resultPanel.y + 16f, 340f, 42f),
                    string.Format("{0} kazandi!", _winner), _titleStyle);

                if (GUI.Button(new Rect(resultPanel.x + 80f, resultPanel.y + 82f, 200f, 42f), "Yeniden Baslat"))
                {
                    RestartMatch();
                }
            }
            else
            {
                GUI.Label(new Rect(24f, Screen.height - 42f, Screen.width - 48f, 28f),
                    "Mavi: W/S veya yukari/asagi: sur | A/D veya sol/sag: don | Carpma hasari = hiz x (agirlik / rakip agirligi)");
                GUI.Label(new Rect(24f, Screen.height - 68f, Screen.width - 48f, 24f),
                    "Kirmizi bilgisayar seni takip eder. Hasar vermek icin rakibe hizla carp.");
            }
        }

        public bool BlocksRobotSelection(Vector2 screenPoint)
        {
            Vector2 guiPoint = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            return _matchFinished || new Rect(24f, 105f, 300f, 106f).Contains(guiPoint) ||
                   new Rect(Screen.width - 324f, 105f, 300f, 106f).Contains(guiPoint);
        }

        private void DrawRobotPanel(RobotHealth robot, float x, float y, Color color, string role)
        {
            if (robot == null)
            {
                return;
            }

            GUI.Box(new Rect(x, y, 300f, 106f), string.Empty);
            GUI.Label(new Rect(x + 12f, y + 8f, 276f, 24f), robot.gameObject.name);

            Rect bar = new Rect(x + 12f, y + 36f, 276f, 18f);
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(robot.CurrentHealth / robot.MaxHealth), bar.height),
                Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 12f, y + 57f, 276f, 20f),
                string.Format("{0:0} / {1:0} CAN", robot.CurrentHealth, robot.MaxHealth));

            GUI.Label(new Rect(x + 12f, y + 79f, 276f, 22f), role);
        }

        public void RestartMatch()
        {
            RobotPrototypeController.Select(null);
            _matchFinished = false;
            _winner = string.Empty;
            ResetRobot(_robotOne, _robotOneStart, Quaternion.Euler(0f, -90f, 0f));
            ResetRobot(_robotTwo, _robotTwoStart, Quaternion.Euler(0f, 90f, 0f));
            _robotOne.Restore();
            _robotTwo.Restore();
            ConfigureCombat();
        }
    }
}

