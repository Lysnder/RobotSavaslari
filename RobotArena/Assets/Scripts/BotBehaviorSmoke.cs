using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RobotSavaslari
{
    // Explicit verification only; shared by Editor checks and Windows -bot-behavior-smoke.
    public sealed class BotBehaviorSmoke : MonoBehaviour
    {
        public bool Completed { get; private set; }
        public bool Passed { get; private set; }
        public string Report { get; private set; }
        private readonly List<string> _lines = new List<string>();
        private Keyboard _keyboard, _previousKeyboard;
        private InputSettings _previousSettings, _settings;
        private bool _exitPlayer;
        private string _captureDirectory;
        private float _nextCapture;
        private int _captureFrame;
        private RobotBotController _observedBot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-bot-behavior-smoke") < 0) return;
            var smoke = new GameObject("Opponent Behavior Verification").AddComponent<BotBehaviorSmoke>();
            smoke._exitPlayer = true;
            string[] args = Environment.GetCommandLineArgs();
            int capture = Array.IndexOf(args, "-bot-evidence-dir");
            if (capture >= 0 && capture + 1 < args.Length)
            {
                smoke._captureDirectory = Path.GetFullPath(args[capture + 1]);
                Directory.CreateDirectory(smoke._captureDirectory);
            }
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            _previousKeyboard = Keyboard.current;
            _previousSettings = InputSystem.settings;
            _settings = Instantiate(_previousSettings);
            _settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = _settings;
            _keyboard = InputSystem.AddDevice<Keyboard>(); _keyboard.MakeCurrent();
            IEnumerator run = Verify();
            while (true)
            {
                object wait;
                try { if (!run.MoveNext()) break; wait = run.Current; }
                catch (Exception error) { Finish(false, error.ToString()); yield break; }
                yield return wait;
            }
            Finish(true, "Four combinations, two matches each, real damage/restore, selection and pause checks.");
        }
        private IEnumerator Verify()
        {
            yield return null;
            var match = FindFirstObjectByType<MatchController>();
            var panel = FindFirstObjectByType<OpponentSelectionPanel>();
            Require(match != null && match.IsInitialized && panel != null, "MainArena and selection initialized");
            Require(FindObjectsByType<OpponentSelectionPanel>().Length == 1, "Idempotent selection bridge");
            var menu = match.GetComponent<MainMenuScreen>();
            var settings = match.GetComponent<SettingsPanelController>();
            Require(match.Opponent.GetComponent<RobotBotController>() == null, "Ready does not create a premature bot");
            settings.ToggleSettings();
            Require(!panel.Select(BotPersonality.Counter, BotDifficulty.Easy), "Settings blocks selection");
            settings.ToggleSettings(); menu.enabled = false;
            Require(!panel.Select(BotPersonality.Counter, BotDifficulty.Easy), "Hidden menu blocks selection");
            menu.enabled = true;
            var player = match.Player;
            var opponent = match.Opponent;
            var health = opponent.GetComponent<RobotHealth>();
            var damage = opponent.GetComponent<RobotDamageVisuals>();
            var motor = opponent.GetComponent<RobotDriveMotor>();
            float maxHealth = health.MaxHealth, maxSpeed = motor.maxSpeed, acceleration = motor.acceleration;
            var weapon = opponent.GetComponent<Combat.MeleeAttack>();
            float weaponDamage = weapon.Damage;
            foreach (BotPersonality personality in Enum.GetValues(typeof(BotPersonality)))
            foreach (BotDifficulty difficulty in Enum.GetValues(typeof(BotDifficulty)))
            {
                if (match.State == MatchController.MatchState.Result) match.Prepare();
                Require(panel.Select(personality, difficulty), "Ready selects combination");
                for (int round = 0; round < 2; round++)
                {
                    if (match.State == MatchController.MatchState.Result) match.Prepare();
                    Require(match.StartMatch(), "Start real match");
                    panel.ApplyToCombatBot();
                    var bot = opponent.GetComponent<RobotBotController>();
                    _observedBot = bot;
                    Require(bot != null && bot.personality == personality && bot.difficulty == difficulty, "Late bot receives selection");
                    Require(bot.enableCombatTactics && Mathf.Approximately(bot.attackEnterDistance, 1.85f), "ConfigureCombat defaults retained");
                    Require(bot.Memory.Count == 0 && bot.DecisionSamples == 0 && damage.WeaponOperational && damage.LostWheels == 0, "Clean next match");
                    Require(health.MaxHealth == maxHealth && motor.maxSpeed == maxSpeed && motor.acceleration == acceleration && weapon.Damage == weaponDamage,
                        "No profile stat bonuses");
                    if (round == 0) CheckRealDamage(bot, damage, health);
                    Vector3 start = opponent.transform.position;
                    Vector3 playerStart = player.transform.position;
                    bool moved = false, attacked = false, playerMoved = false, playerDamagedBot = false;
                    float deadline = Time.time + 75;
                    var driver = player.GetComponent<Driving.PlayerKeyboardDriver>();
                    while (match.State == MatchController.MatchState.Playing && Time.time < deadline)
                    {
                        // Opt-in automation may run with a hidden/unfocused player window. Exercise
                        // the real keyboard path as focused; never change production focus handling.
                        if (driver != null && !driver.HasFocus)
                            player.gameObject.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
                        _keyboard.MakeCurrent();
                        Vector3 offset = opponent.transform.position - player.transform.position; offset.y = 0;
                        float angle = Vector3.SignedAngle(player.transform.forward, offset, Vector3.up);
                        var keys = new List<Key> { Key.Space };
                        if (angle > 8) keys.Add(Key.D);
                        if (angle < -8) keys.Add(Key.A);
                        if (Mathf.Abs(angle) < 55) keys.Add(Time.time % 5 < .8f ? Key.S : Key.W);
                        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys.ToArray()));
                        moved |= Vector3.Distance(start, opponent.transform.position) > .5f;
                        playerMoved |= Vector3.Distance(playerStart, player.transform.position) > .5f &&
                            driver != null && (Mathf.Abs(driver.ForwardInput) > 0 || Mathf.Abs(driver.TurnInput) > 0);
                        playerDamagedBot |= health.CurrentHealth < health.MaxHealth;
                        attacked |= bot.AttackRequested;
                        if (_captureDirectory != null && Time.unscaledTime >= _nextCapture)
                        {
                            _nextCapture = Time.unscaledTime + .2f;
                            ScreenCapture.CaptureScreenshot(Path.Combine(_captureDirectory, $"frame-{_captureFrame++:D5}.png"));
                        }
                        yield return null;
                    }
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    Require(moved && attacked, "Bot moves and uses shared melee intent");
                    Require(playerMoved && playerDamagedBot, "Real keyboard driver moves the player and player combat damages the bot");
                    Require(match.State == MatchController.MatchState.Result && match.ResultCount == 1, "Physical combat reaches one result within 75 seconds");
                    Add($"{personality}/{difficulty} round={round + 1} PASS result={match.Outcome} playerHP={player.GetComponent<RobotHealth>().CurrentHealth:F1} botHP={health.CurrentHealth:F1} reaction={bot.EffectiveReactionSeconds:F2}");
                    yield return null;
                }
            }
        }
        private void CheckRealDamage(RobotBotController bot, RobotDamageVisuals damage, RobotHealth health)
        {
            damage.Initialize();
            var body = bot.GetComponent<Rigidbody>();
            var targetBody = bot.Target.GetComponent<Rigidbody>();
            Vector3 savedPosition = body.position, savedTarget = targetBody.position;
            Quaternion savedRotation = body.rotation, savedTargetRotation = targetBody.rotation;
            try
            {
                Place(body, new Vector3(0, .82f, -1), Quaternion.identity);
                Place(targetBody, new Vector3(0, .82f, .6f), Quaternion.Euler(0, 180, 0));
                Physics.SyncTransforms();
                var weapon = damage.Parts.First(part => part.Kind == RobotDamageVisuals.PartKind.Weapon);
                Hit(health, weapon);
                Require(!damage.WeaponOperational, "Real weapon detached");
                int requests = 0;
                Action<RobotBotController, Transform> record = (_, __) => requests++;
                bot.AttackRequest += record;
                try { for (int i = 0; i < 50; i++) { bot.Tick(.02f); Require(!bot.AttackRequested, "Unarmed request gate"); } }
                finally { bot.AttackRequest -= record; }
                Require(requests == 0, "No unarmed attack events");
                health.Restore(); bot.Tick(.02f);
                Require(bot.AttackRequested && damage.WeaponOperational, "Restore resumes real weapon intent");
                foreach (var wheel in damage.Parts.Where(part => part.Kind == RobotDamageVisuals.PartKind.Wheel).Take(2)) Hit(health, wheel);
                bot.Tick(.02f);
                Require(bot.OwnCondition.LimitedMovement && bot.OwnCondition.LostWheels == 2, "Read real wheel loss");
                Require(bot.State != RobotBotController.Decision.Repositioning && bot.State != RobotBotController.Decision.Evading, "Limited mobility avoids wide maneuvers");
                health.Restore();
                targetBody.position = new Vector3(0, .82f, 2); targetBody.transform.position = targetBody.position;
                targetBody.linearVelocity = Vector3.back * 4; Physics.SyncTransforms(); bot.Tick(.02f);
                Require(bot.Memory.Count == 1, "Real Rigidbody charge recorded");
                var settings = FindFirstObjectByType<SettingsPanelController>();
                int samples = bot.DecisionSamples; float aim = bot.AimError;
                settings.ToggleSettings();
                for (int i = 0; i < 100; i++) bot.Tick(.02f);
                Require(bot.Memory.Count == 1 && bot.DecisionSamples == samples && bot.AimError == aim, "Pause preserves memory and random sequence");
                settings.ToggleSettings();
                Add($"{bot.personality}/{bot.difficulty} damage PASS weaponRequests={requests} wheels=2 restored=true closingSpeed={bot.Threat.ClosingSpeed:F2} closest={bot.Threat.ClosestDistance:F2}");
            }
            finally
            {
                health.Restore();
                Place(body, savedPosition, savedRotation); Place(targetBody, savedTarget, savedTargetRotation);
                bot.ResetDecision(); Physics.SyncTransforms();
            }
        }
        private static void Place(Rigidbody body, Vector3 position, Quaternion rotation)
        {
            body.position = position; body.rotation = rotation;
            body.transform.SetPositionAndRotation(position, rotation);
            body.linearVelocity = body.angularVelocity = Vector3.zero;
        }
        private static void Hit(RobotHealth health, RobotDamageVisuals.Part part) =>
            health.TakeDamage(part.Threshold + 1, part.Transform.position, Vector3.up, Vector3.zero, true);
        private void Add(string line) { _lines.Add(line); Debug.Log("BOT_BEHAVIOR " + line); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Finish(bool passed, string detail)
        {
            Passed = passed; Completed = true;
            Report = string.Join("\n", _lines) + "\n" + detail;
            Cleanup();
            if (passed) Debug.Log("BOT_BEHAVIOR PASS " + Report); else Debug.LogError("BOT_BEHAVIOR FAIL " + Report);
            if (_exitPlayer && !Application.isEditor) Application.Quit(passed ? 0 : 1);
        }
        private void OnDestroy() => Cleanup();
        private void OnGUI()
        {
            if (_captureDirectory == null || _observedBot == null) return;
            var bot = _observedBot;
            GUI.Box(new Rect(16, Screen.height - 100, 580, 84),
                $"VERIFICATION   {bot.personality} / {bot.difficulty}   {bot.State}\n" +
                $"Distance {bot.Distance:F2}m | closing {bot.Threat.ClosingSpeed:F2}m/s | memory {bot.Memory.Count}/6\n" +
                $"Weapon {bot.OwnCondition.WeaponOperational} | wheels lost {bot.OwnCondition.LostWheels} | aim error {bot.AimError:F2}");
        }
        private void Cleanup()
        {
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if (_previousSettings != null) InputSystem.settings = _previousSettings;
            if (_settings != null) { Destroy(_settings); _settings = null; }
            if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
        }
    }
}
