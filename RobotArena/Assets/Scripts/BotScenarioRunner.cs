using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari
{
    // Debug overlay, deliberately independent of player input and real combat.
    [DefaultExecutionOrder(-50)]
    public sealed class BotScenarioRunner : MonoBehaviour
    {
        public enum Scenario { Front, Side, Behind, Moving, RangeBoundary, TargetLoss, AngledWall, Corner, MatchEnd }
        public RobotBotController bot;
        public Transform testTarget;
        public GameObject obstacles;
        public Scenario scenario;
        public bool automaticTarget = true;
        public float soakSeconds = 540f;
        public float Elapsed { get; private set; }
        private BotAttackProbe _probe;
        private bool _lossDone, _restoreDone, _stopDone;
        private Vector2 _scroll;
        private readonly string[] _labels = { "Onde", "Yanda", "Arkada", "Hareketli", "Menzil siniri", "Hedef kaybi", "Acili duvar", "Kose", "Mac sonu" };
        private void Start() { _probe = bot.GetComponent<BotAttackProbe>(); Select(scenario); }
        public void Select(Scenario next)
        {
            scenario = next; Elapsed = 0; _lossDone = _restoreDone = _stopDone = false;
            automaticTarget = true; testTarget.gameObject.SetActive(true);
            bot.SetMatchRunning(false);
            bot.transform.SetPositionAndRotation(new Vector3(-3, 0.5f, -3), Quaternion.identity);
            bot.GetComponent<Rigidbody>().position = bot.transform.position;
            bot.GetComponent<Rigidbody>().rotation = bot.transform.rotation;
            obstacles.SetActive(next == Scenario.Corner || next == Scenario.AngledWall);
            Vector3 origin = bot.transform.position;
            testTarget.position = origin + Vector3.forward * 5;
            if (next == Scenario.Side) testTarget.position = origin + Vector3.right * 5;
            if (next == Scenario.Behind) testTarget.position = origin - Vector3.forward * 3;
            if (next == Scenario.RangeBoundary) testTarget.position = origin + Vector3.forward * bot.attackEnterDistance;
            if (next == Scenario.Corner || next == Scenario.AngledWall)
            {
                bot.transform.SetPositionAndRotation(new Vector3(1.15f, 0.5f, next == Scenario.Corner ? 1.15f : 0.1f), Quaternion.Euler(0, 45, 0));
                bot.GetComponent<Rigidbody>().position = bot.transform.position;
                bot.GetComponent<Rigidbody>().rotation = bot.transform.rotation;
                testTarget.position = new Vector3(4, 0.5f, 4);
            }
            bot.Target = testTarget; bot.SetMatchRunning(true);
            if (_probe != null) _probe.ResetCount();
            Physics.SyncTransforms();
        }
        private void FixedUpdate()
        {
            if (bot == null || testTarget == null) return;
            Elapsed += Time.fixedDeltaTime;
            if (automaticTarget && bot.MatchRunning)
            {
                if (scenario == Scenario.Moving)
                    testTarget.position = new Vector3(Mathf.Sin(Elapsed * 0.45f) * 4, 0.5f, Mathf.Sin(Elapsed * 0.7f) * 3);
                if (scenario == Scenario.RangeBoundary)
                    testTarget.position = new Vector3(-3, 0.5f, -3 + bot.attackEnterDistance + Mathf.Sin(Elapsed * 3) * 0.12f);
                if (scenario == Scenario.TargetLoss && Elapsed > 2 && !_lossDone) { bot.Target = null; _lossDone = true; }
                if (scenario == Scenario.TargetLoss && Elapsed > 4 && !_restoreDone) { bot.Target = testTarget; _restoreDone = true; }
                if ((scenario == Scenario.MatchEnd && Elapsed > 1.5f || scenario == Scenario.Moving && Elapsed >= soakSeconds) && !_stopDone)
                { bot.SetMatchRunning(false); _stopDone = true; }
            }
            var keyboard = Keyboard.current;
            if (!automaticTarget && keyboard != null)
            {
                Vector3 move = new Vector3((keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed ? 1 : 0), 0,
                    (keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.downArrowKey.isPressed ? 1 : 0));
                testTarget.position += move * (3 * Time.fixedDeltaTime);
            }
        }
        private void OnGUI()
        {
            if (bot == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 330, Mathf.Min(Screen.height - 24, 610)), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("BOT DAVRANIS LABORATUVARI");
            int next = GUILayout.SelectionGrid((int)scenario, _labels, 3);
            if (next != (int)scenario) Select((Scenario)next);
            GUILayout.Label($"Sure: {Elapsed:0.0}s   Durum: {bot.State}");
            GUILayout.Label($"Mesafe: {bot.Distance:0.00}m   Aci: {bot.TargetAngle:0.0}");
            GUILayout.Label($"Sur: {bot.Command.x:0.00}   Don: {bot.Command.y:0.00}");
            GUILayout.Label($"Saldiri istegi: {_probe.Count}   Son: {_probe.LastRequestTime:0.00}s");
            GUILayout.Label($"Toparlanma (toplam): {bot.RecoveryCount}");
            automaticTarget = GUILayout.Toggle(automaticTarget, "Otomatik hedef (kapali: ok tuslari / Scene tasi)");
            if (GUILayout.Button("Hedefi kaldir / geri ver")) bot.Target = bot.Target != null ? null : testTarget;
            if (GUILayout.Button(bot.MatchRunning ? "Maci durdur" : "Maci devam ettir")) bot.SetMatchRunning(!bot.MatchRunning);
            if (GUILayout.Button("Ayni senaryoyu yeniden baslat")) Select(scenario);
            GUILayout.Label($"Saldiriya gir: {bot.attackEnterDistance:0.00}m");
            bot.attackEnterDistance = GUILayout.HorizontalSlider(bot.attackEnterDistance, 1, 2.5f);
            GUILayout.Label($"Takibe don: {bot.followResumeDistance:0.00}m");
            bot.followResumeDistance = GUILayout.HorizontalSlider(bot.followResumeDistance, bot.attackEnterDistance + 0.1f, 3.5f);
            GUILayout.Label("Inspector: Bot > Target ile baska hedef secilebilir.");
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }
}
