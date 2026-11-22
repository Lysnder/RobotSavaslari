using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using RobotSavaslari.Arena;
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
        private int _failedMatches;
        private int[] _seeds = { 740 };
        private StreamWriter _trace;
        private RoundMetrics _metrics;
        private int _roundNumber;
        private bool _navigationMatrix;
        private string _layoutName;
        private int _matchesRun;
        private float _previousTimeScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool matrix = Array.IndexOf(args, "-bot-navigation-matrix") >= 0;
            if (!matrix && Array.IndexOf(args, "-bot-behavior-smoke") < 0) return;
            var smoke = new GameObject("Opponent Behavior Verification").AddComponent<BotBehaviorSmoke>();
            smoke._exitPlayer = true;
            smoke._navigationMatrix = matrix;
            int capture = Array.IndexOf(args, "-bot-evidence-dir");
            if (capture >= 0 && capture + 1 < args.Length)
            {
                smoke._captureDirectory = Path.GetFullPath(args[capture + 1]);
                Directory.CreateDirectory(smoke._captureDirectory);
            }
        }
        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int seeds = Array.IndexOf(args, "-bot-seeds");
            if (seeds >= 0 && seeds + 1 < args.Length)
                _seeds = args[seeds + 1].Split(',').Select(int.Parse).ToArray();
            if (_navigationMatrix)
            {
                _seeds = new[] { 740, 741, 123 };
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 8;
            }
            int trace = Array.IndexOf(args, "-bot-trace-file");
            if (trace >= 0 && trace + 1 < args.Length)
            {
                _trace = new StreamWriter(Path.GetFullPath(args[trace + 1]), false);
                _trace.WriteLine("seed\tpersonality\tdifficulty\tround\ttime\tstate\tdistance\tangle\tweapon\tcooldown\tsamples\taimError\trequests\tstarts\thits\tdamage\teligible\tlayout\tplans\tqueries\tnodes\treplanReason");
            }
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
            int expected = _navigationMatrix ? 48 : 8 * _seeds.Length;
            Finish(_failedMatches == 0 && _matchesRun == expected,
                $"seeds={string.Join(",", _seeds)} matches={_matchesRun}/{expected} failed={_failedMatches}; real damage/restore, selection and pause checks.");
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
            var arena = FindFirstObjectByType<ArenaEnvironment>();
            if (!_navigationMatrix)
            {
                _layoutName = arena?.Layout?.displayName;
                var current = VerifyLayout(match, panel);
                while (current.MoveNext()) yield return current.Current;
                yield break;
            }
            Require(arena != null, "Navigation matrix requires MainArena arena layouts");
            foreach (string layout in new[] { "Open", "Tactical" })
            {
                if (match.State == MatchController.MatchState.Result) match.Prepare();
                int index = -1;
                for (int i = 0; i < arena.Layouts.Count; i++)
                    if (arena.Layouts[i].displayName == layout) { index = i; break; }
                Require(index >= 0 && arena.Select(index), $"Select {layout} while Ready");
                Require(match.ConfigureRules(new MatchRules { mode = MatchMode.Single, timeLimitSeconds = 35 }), "Timed matrix matches");
                _layoutName = layout;
                var run = VerifyLayout(match, panel);
                while (run.MoveNext()) yield return run.Current;
            }
        }

        private IEnumerator VerifyLayout(MatchController match, OpponentSelectionPanel panel)
        {
            var player = match.Player;
            var opponent = match.Opponent;
            var health = opponent.GetComponent<RobotHealth>();
            var damage = opponent.GetComponent<RobotDamageVisuals>();
            var motor = opponent.GetComponent<RobotDriveMotor>();
            float maxHealth = health.MaxHealth, maxSpeed = motor.maxSpeed, acceleration = motor.acceleration;
            var weapon = opponent.GetComponent<Combat.MeleeAttack>();
            float weaponDamage = weapon.Damage;
            foreach (int seed in _seeds)
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
                    bot.ConfigureDecisions(personality, difficulty, seed);
                    if (round == 0) CheckRealDamage(bot, damage, health);
                    _roundNumber = round + 1;
                    _metrics = new RoundMetrics(bot, weapon, player.GetComponent<RobotHealth>());
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
                            CaptureCameraFrame(bot);
                        }
                        yield return null;
                    }
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                    _metrics.Sample(_trace, seed, personality, difficulty, round + 1, _layoutName, true);
                    bool success = moved && _metrics.Requests > 0 && _metrics.Starts > 0 && playerMoved && playerDamagedBot &&
                        match.State == MatchController.MatchState.Result && match.ResultCount == 1;
                    if (_navigationMatrix) success &= _metrics.Hits > 0 && _metrics.Damage > 0;
                    if (!success) _failedMatches++;
                    _matchesRun++;
                    Add($"{personality}/{difficulty} seed={seed} round={round + 1} {(success ? "PASS" : "FAIL")} layout={_layoutName} " +
                        $"result={match.Outcome} moved={moved} sampledIntent={attacked} requests={_metrics.Requests} starts={_metrics.Starts} hits={_metrics.Hits} damage={_metrics.Damage:F1} " +
                        $"eligibleSteps={_metrics.EligibleSamples} playerMoved={playerMoved} playerDamagedBot={playerDamagedBot} weaponWorking={damage.WeaponOperational} " +
                        $"playerHP={player.GetComponent<RobotHealth>().CurrentHealth:F1} botHP={health.CurrentHealth:F1} reaction={bot.EffectiveReactionSeconds:F2} " +
                        $"plans={_metrics.Plans} maxQueries={_metrics.MaxQueries} maxNodes={_metrics.MaxNodes}");
                    _metrics.Dispose(); _metrics = null;
                    yield return null;
                }
            }
        }
        private void FixedUpdate()
        {
            if (_metrics != null && _observedBot != null)
                _metrics.Sample(_trace, _observedBot.decisionSeed, _observedBot.personality, _observedBot.difficulty, _roundNumber, _layoutName);
        }
        private sealed class RoundMetrics : IDisposable
        {
            private readonly RobotBotController bot;
            private readonly Combat.MeleeAttack weapon;
            private readonly RobotHealth target;
            private float nextSample;
            private RobotBotController.Decision previousState;
            public int Requests, Starts, Hits, EligibleSamples;
            public int Plans, MaxQueries, MaxNodes;
            public float Damage;
            public RoundMetrics(RobotBotController bot, Combat.MeleeAttack weapon, RobotHealth target)
            {
                this.bot = bot; this.weapon = weapon; this.target = target;
                bot.AttackRequest += OnRequest; weapon.Started += OnStart;
                weapon.TargetHit += OnHit; target.Damaged += OnDamage;
            }
            private void OnRequest(RobotBotController _, Transform __) => Requests++;
            private void OnStart(Combat.MeleeAttack _) => Starts++;
            private void OnHit(Combat.MeleeAttack _, RobotHealth __, float ___) => Hits++;
            private void OnDamage(RobotHit hit) { if (hit.Weapon && hit.Source == bot.GetComponent<RobotHealth>()) Damage += hit.Amount; }
            public void Sample(StreamWriter trace, int seed, BotPersonality personality, BotDifficulty difficulty, int round, string layout, bool force = false)
            {
                Plans = Mathf.Max(Plans, bot.NavigationPlans);
                MaxQueries = Mathf.Max(MaxQueries, bot.NavigationQueries);
                MaxNodes = Mathf.Max(MaxNodes, bot.NavigationExpansions);
                bool eligible = bot.MatchRunning && bot.Target != null && bot.OwnCondition.WeaponOperational &&
                    bot.Distance <= bot.attackEnterDistance && Mathf.Abs(bot.TargetAngle) <= bot.attackAngle && weapon.CooldownRemaining <= 0;
                if (eligible) EligibleSamples++;
                if (!force && Time.time < nextSample && previousState == bot.State) return;
                nextSample = Time.time + .1f; previousState = bot.State;
                trace?.WriteLine(FormattableString.Invariant($"{seed}\t{personality}\t{difficulty}\t{round}\t{Time.time:F3}\t{bot.State}\t{bot.Distance:F3}\t{bot.TargetAngle:F3}\t{bot.OwnCondition.WeaponOperational}\t{weapon.CooldownRemaining:F3}\t{bot.DecisionSamples}\t{bot.AimError:F3}\t{Requests}\t{Starts}\t{Hits}\t{Damage:F3}\t{eligible}\t{layout}\t{bot.NavigationPlans}\t{bot.NavigationQueries}\t{bot.NavigationExpansions}\t{bot.NavigationReason}"));
            }
            public void Dispose()
            {
                bot.AttackRequest -= OnRequest; weapon.Started -= OnStart;
                weapon.TargetHit -= OnHit; target.Damaged -= OnDamage;
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
        private void CaptureCameraFrame(RobotBotController bot)
        {
            // Hidden Windows players may have a black swapchain. Render the actual arena camera
            // offscreen instead; the sidecar records the corresponding decision (HUD is excluded).
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Evidence requires the arena camera");
            RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                string frame = $"frame-{_captureFrame++:D5}";
                File.WriteAllBytes(Path.Combine(_captureDirectory, frame + ".png"), pixels.EncodeToPNG());
                File.AppendAllText(Path.Combine(_captureDirectory, "decisions.tsv"),
                    FormattableString.Invariant($"{frame}\t{bot.personality}\t{bot.difficulty}\t{bot.State}\t{bot.Distance:F3}\t{bot.Threat.ClosingSpeed:F3}\t{bot.Memory.Count}\t{bot.DefenseSide}\n"));
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target); Destroy(pixels);
            }
        }
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
            if (_previousTimeScale > 0) { Time.timeScale = _previousTimeScale; _previousTimeScale = 0; }
            _metrics?.Dispose(); _metrics = null;
            _trace?.Dispose(); _trace = null;
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if (_previousSettings != null) InputSystem.settings = _previousSettings;
            if (_settings != null) { Destroy(_settings); _settings = null; }
            if (_previousKeyboard != null && _previousKeyboard.added) _previousKeyboard.MakeCurrent();
        }
    }
}
