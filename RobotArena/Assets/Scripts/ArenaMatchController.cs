using UnityEngine;

namespace RobotSavaslari
{
    public sealed class ArenaMatchController : MonoBehaviour
    {
        private const float DamagePerHit = 25f;
        private RobotHealth _robotOne;
        private RobotHealth _robotTwo;
        private bool _matchFinished;
        private string _winner;
        private GUIStyle _titleStyle;

        private readonly Vector3 _robotOneStart = new Vector3(-4.15f, 1.6f, 0f);
        private readonly Vector3 _robotTwoStart = new Vector3(4.15f, 1.6f, 0f);

        private void Awake()
        {
            _robotOne = CreateRobot("Kirmizi Robot", _robotOneStart, new Color(0.85f, 0.16f, 0.12f, 1f));
            _robotTwo = CreateRobot("Mavi Robot", _robotTwoStart, new Color(0.12f, 0.38f, 0.9f, 1f));

            _robotOne.Defeated += HandleRobotDefeated;
            _robotTwo.Defeated += HandleRobotDefeated;
            PositionCamera();
        }

        private static void PositionCamera()
        {
            Camera arenaCamera = Camera.main;
            if (arenaCamera == null)
            {
                return;
            }

            arenaCamera.transform.position = new Vector3(0f, 8f, -14f);
            arenaCamera.transform.LookAt(new Vector3(0f, 0.8f, 0f));
            arenaCamera.fieldOfView = 55f;
        }

        private static RobotHealth CreateRobot(string robotName, Vector3 spawnPosition, Color color)
        {
            GameObject robot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            robot.name = robotName;
            robot.transform.position = spawnPosition;
            robot.GetComponent<Renderer>().material = CreateMaterial(color);
            return robot.AddComponent<RobotHealth>();
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
            DrawRobotPanel(_robotOne, 24f, 105f, new Color(0.85f, 0.16f, 0.12f, 1f), DamageRobotOne);
            DrawRobotPanel(_robotTwo, Screen.width - 324f, 105f, new Color(0.12f, 0.38f, 0.9f, 1f), DamageRobotTwo);

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
                    "Robotu yenmek icin 25 hasar butonuna basin. Mac bitince yeniden baslatabilirsiniz.");
            }
        }

        private void DrawRobotPanel(RobotHealth robot, float x, float y, Color color, System.Action hitAction)
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

            if (!_matchFinished && GUI.Button(new Rect(x + 12f, y + 79f, 276f, 22f), "25 hasar ver"))
            {
                hitAction();
            }
        }

        private void DamageRobotOne()
        {
            if (!_matchFinished)
            {
                _robotOne.TakeDamage(DamagePerHit);
            }
        }

        private void DamageRobotTwo()
        {
            if (!_matchFinished)
            {
                _robotTwo.TakeDamage(DamagePerHit);
            }
        }

        private void RestartMatch()
        {
            _matchFinished = false;
            _winner = string.Empty;
            _robotOne.transform.position = _robotOneStart;
            _robotTwo.transform.position = _robotTwoStart;
            _robotOne.Restore();
            _robotTwo.Restore();
        }
    }
}

