using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Combat;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using RobotSavaslari.Training;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace RobotSavaslari.Gameplay.Tests
{
    public sealed partial class WorkshopDuelTests
    {
        private WorkshopSelection Selection => match.GetComponent<WorkshopSelection>();
        private static WorkshopLoadout Parts(string tire, string armor, string weapon) =>
            new WorkshopLoadout { chassisId = "standard", tireId = tire, armorId = armor, weaponId = weapon };

        [UnityTest]
        public IEnumerator OpenPhysicalMeasurementsForEveryBuild()
        {
            var trace = new Dictionary<WorkshopLoadout, (float speed, float acceleration, float brake, float yaw)>();
            foreach (string tire in new[] { "soft", "hard" })
            foreach (string armor in new[] { "light", "heavy" })
            foreach (string weapon in new[] { "spinner", "fork" })
            {
                var parts = Parts(tire, armor, weapon);
                Apply(parts);
                yield return StartDuel(DuelSetup.Quick());
                yield return MeasureOpen(tire + "-" + armor + "-" + weapon);
                trace.Add(parts, measured);
                match.Prepare();
                yield return null;
            }
            foreach (string weapon in new[] { "spinner", "fork" })
            {
                var softLight = trace[Parts("soft", "light", weapon)];
                var hardLight = trace[Parts("hard", "light", weapon)];
                var softHeavy = trace[Parts("soft", "heavy", weapon)];
                Assert.That(softLight.brake, Is.LessThan(hardLight.brake));
                Assert.That(hardLight.speed, Is.GreaterThan(softLight.speed));
                Assert.That(softLight.acceleration, Is.GreaterThan(softHeavy.acceleration));
                Assert.That(softLight.yaw, Is.GreaterThan(hardLight.yaw));
                Assert.That(softLight.yaw, Is.GreaterThan(softHeavy.yaw));
            }
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator AllEightOpenDuelsAndOppositeBuildsInTacticalAndPulse()
        {
            foreach (string tire in new[] { "soft", "hard" })
            foreach (string armor in new[] { "light", "heavy" })
            foreach (string weapon in new[] { "spinner", "fork" })
            {
                var loadout = Parts(tire, armor, weapon);
                Apply(loadout);
                yield return StartDuel(DuelSetup.Quick());
                yield return PilotDuel(loadout);
                match.Prepare();
                yield return null;
            }
            foreach (string layout in new[] { "Tactical", "PulseArena" })
            foreach (var loadout in new[] { Parts("soft", "light", "spinner"), Parts("hard", "heavy", "fork") })
            {
                var setup = DuelSetup.Tactical();
                setup.arenaId = layout;
                Apply(loadout);
                yield return StartDuel(setup);
                yield return PilotDuel(loadout);
                match.Prepare();
                yield return null;
            }
        }

        private void Apply(WorkshopLoadout loadout)
        {
            var player = match.Player;
            Assert.That(menu.OpenWorkshop(), Is.True);
            Assert.That(menu.SetWorkshopDraft(loadout), Is.True);
            var studio = GameObject.Find("Workshop preview studio");
            Assert.That(studio.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(studio.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(studio.GetComponentsInChildren<RobotHealth>(true), Is.Empty);
            Assert.That(studio.GetComponentsInChildren<MeleeAttack>(true), Is.Empty);
            Assert.That(menu.ApplyWorkshop(), Is.True, Selection.Notice);
            Assert.That(menu.WorkshopOpen, Is.False);
            Assert.That(match.Player, Is.SameAs(player));
            Assert.That(Selection.LastModular, Is.EqualTo(loadout));
            Assert.That(Selection.IsModular, Is.True);
        }

        // Same steering policy in every arena. No teleporting, health edits, artificial clock
        // or bot override. Real keyboard, spawn positions, melee, contact, hazards and time limit.
        private IEnumerator PilotDuel(WorkshopLoadout value, bool checkBothTools = false)
        {
            Keyboard previous = Keyboard.current;
            InputSettings settings = InputSystem.settings;
            var isolated = UnityEngine.Object.Instantiate(settings);
            isolated.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            isolated.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = isolated;
            var pilot = InputSystem.AddDevice<Keyboard>();
            var player = match.Player;
            var opponent = match.Opponent;
            var attack = player.GetComponent<MeleeAttack>();
            int hits = 0, botHits = 0, swings = 0, botSwings = 0;
            float firstReach = -1f, firstBotHit = -1f, closest = float.PositiveInfinity;
            float start = Time.time, wallStart = Time.realtimeSinceStartup;
            int startFrame = Time.frameCount;
            var pilotInterval = new WaitForSeconds(1f / 30f);
            var trace = checkBothTools ? new ToolReplayMotion(match) : null;
            var tracking = trace != null ? match.StartCoroutine(trace.TrackPhysics(match)) : null;
            var enemyHull = opponent.GetComponent<BoxCollider>();
            var botTool = opponent.GetComponent<MeleeAttack>();
            void CountHit(MeleeAttack source, RobotHealth target, float damage) => hits++;
            void CountBotHit(MeleeAttack source, RobotHealth target, float damage)
            {
                if (botHits++ == 0) firstBotHit = Time.time - start;
            }
            void CountBotSwing(MeleeAttack source) => botSwings++;
            void CountSwing(MeleeAttack source) => swings++;
            attack.TargetHit += CountHit;
            attack.Started += CountSwing;
            botTool.TargetHit += CountBotHit;
            botTool.Started += CountBotSwing;
            Vector3 playerSpawn = player.transform.position, botSpawn = opponent.transform.position;
            float playerTravel = 0f, botTravel = 0f;
            try
            {
                while (match.State == MatchController.MatchState.Playing && Time.time - start < match.Rules.timeLimitSeconds + 4f)
                {
                    Vector3 offset = opponent.transform.position - player.transform.position;
                    offset.y = 0f;
                    float angle = Vector3.SignedAngle(player.transform.forward, offset, Vector3.up);
                    var input = new KeyboardState();
                    bool contact = checkBothTools && attack.IsInReach(enemyHull, out _);
                    closest = Mathf.Min(closest, offset.magnitude);
                    if (contact && firstReach < 0f) firstReach = Time.time - start;
                    float elapsed = Time.time - start;
                    bool botOpening = checkBothTools && botHits == 0 && elapsed < 16f;
                    if (!checkBothTools || !botOpening && contact) input.Set(Key.Space, true);
                    if (angle > 8f) input.Set(Key.D, true);
                    if (angle < -8f) input.Set(Key.A, true);
                    if (botOpening)
                    {
                        // Let the approaching bot finish its telegraph against a stationary target.
                        // If it stops just outside striking distance, creep into range without a charging ram.
                        if (elapsed > 4f && !botTool.IsAttacking && offset.magnitude > 1.60f &&
                            Mathf.Abs(angle) < 25f && Mathf.Repeat(elapsed, .5f) < .16f) input.Set(Key.W, true);
                    }
                    else if (checkBothTools && (hits == 0 || botHits == 0))
                    {
                        // Close until the equipped tool actually reaches the hull. Back away to regain a frontal angle after a glancing contact.
                        if (Mathf.Abs(angle) < 36f && !contact) input.Set(Key.W, true);
                        if (Mathf.Abs(angle) > 64f && offset.magnitude < 2.4f) input.Set(Key.S, true);
                    }
                    else if (Mathf.Abs(angle) < 55f) input.Set((Time.time - start) % 5f < .8f ? Key.S : Key.W, true);
                    pilot.MakeCurrent();
                    InputSystem.QueueStateEvent(pilot, input);
                    yield return checkBothTools ? pilotInterval : null;
                    playerTravel = Mathf.Max(playerTravel, Vector3.Distance(playerSpawn, player.transform.position));
                    botTravel = Mathf.Max(botTravel, Vector3.Distance(botSpawn, opponent.transform.position));
                }
                if (checkBothTools) PreserveToolRecording(value.weaponId);
                Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Result));
                Assert.That(match.ResultCount, Is.EqualTo(1));
                Assert.That(botTravel, Is.GreaterThan(.3f), "bot moved");
                Assert.That(playerTravel, Is.GreaterThan(.3f), "player moved");
                var round = match.LastRound;
                Assert.That(round.player.receivedDamage + round.opponent.receivedDamage, Is.GreaterThan(0f), "combat occurred");
                if (checkBothTools)
                {
                    TestContext.WriteLine($"PVE_NATURAL player={value.weaponId} hits={hits} swings={swings} bot={botTool.Profile.name} hits={botHits} " +
                        $"botSwings={botSwings} firstBotHit={firstBotHit:F2} fps={(Time.frameCount - startFrame) / Mathf.Max(.001f, Time.realtimeSinceStartup - wallStart):F1} " +
                        $"outcome={round.outcome} reason={round.reason} hp={round.player.remainingHealth:F1}/{round.opponent.remainingHealth:F1} " +
                        $"operational={player.GetComponent<RobotDamageVisuals>().WeaponOperational} nearest={closest:F2} firstReach={firstReach:F2} seconds={round.durationSeconds:F2}");
                    trace.Report(value.weaponId);
                    Assert.That(hits, Is.GreaterThan(0), value.weaponId + " player weapon");
                    Assert.That(botHits, Is.GreaterThan(0), botTool.Profile.name + " bot weapon");
                }
                TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "WORKSHOP_DUEL {0} {1}-{2}-{3} outcome={4} reason={5} seconds={6:F2} hp={7:F1}/{8:F1} hits={9} travel={10:F2}/{11:F2}",
                    arena.Layout.name, value.tireId, value.armorId, value.weaponId, round.outcome, round.reason,
                    round.durationSeconds, round.player.remainingHealth, round.opponent.remainingHealth, hits, playerTravel, botTravel));
            }
            finally
            {
                if (tracking != null) match.StopCoroutine(tracking);
                attack.TargetHit -= CountHit;
                attack.Started -= CountSwing;
                botTool.TargetHit -= CountBotHit;
                botTool.Started -= CountBotSwing;
                InputSystem.RemoveDevice(pilot);
                InputSystem.settings = settings;
                if (previous != null && previous.added) previous.MakeCurrent();
                UnityEngine.Object.Destroy(isolated);
            }
        }

        [UnityTest]
        public IEnumerator CancelApplyTwiceArenaTrainingAndReload()
        {
            var owner = match.Player;
            var body = owner.GetComponent<Rigidbody>();
            var collider = owner.GetComponent<BoxCollider>();
            var driver = owner.GetComponent<PlayerKeyboardDriver>();
            var first = Parts("soft", "light", "spinner");
            var second = Parts("hard", "heavy", "fork");
            Apply(first);
            float mass = body.mass;
            Assert.That(menu.OpenWorkshop(), Is.True);
            Assert.That(menu.SetWorkshopDraft(second), Is.True);
            Assert.That(body.mass, Is.EqualTo(mass));
            Assert.That(menu.CloseWorkshop(), Is.True);
            Assert.That(Selection.LastModular, Is.EqualTo(first));
            Apply(second);
            Assert.That(body.mass, Is.GreaterThan(mass));
            var store = new WorkshopStore(Path.Combine(temporary, "workshop.json"));
            Assert.That(store.Load().lastModular, Is.EqualTo(second));
            Assert.That(Selection.SelectStock(false), Is.True);
            Assert.That(store.Load().selected, Is.EqualTo("spinner"));
            Assert.That(store.Load().lastModular, Is.EqualTo(second));
            Assert.That(Selection.UseStore(store), Is.True);
            Assert.That(Selection.IsModular, Is.False);
            Apply(second);
            // Test players boot their own scene before MainArena. Install the normal scene
            // feature just as the existing driving player tests do.
            var installer = UnityEngine.Object.FindAnyObjectByType<DrivingFeatureInstaller>();
            if (installer == null) installer = new GameObject("Driving Features").AddComponent<DrivingFeatureInstaller>();
            Assert.That(installer.Install(), Is.True);
            yield return null;
            var training = installer.TimeTrial;
            training.ExerciseStore = new DrivingExerciseStore(Path.Combine(temporary, "exercises.json"));
            Assert.That(training.Enter(), Is.True);
            Assert.That(menu.OpenWorkshop(), Is.False);
            training.Exit();
            yield return null;
            Assert.That(match.Player, Is.SameAs(owner));
            Assert.That(owner.GetComponent<BoxCollider>(), Is.SameAs(collider));
            Assert.That(owner.GetComponent<PlayerKeyboardDriver>(), Is.SameAs(driver));
            Assert.That(Selection.LastModular, Is.EqualTo(second));
            yield return StartDuel(DuelSetup.Tactical());
            Assert.That(Selection.TryApply(first), Is.False);
            Assert.That(menu.OpenWorkshop(), Is.False);
        }

        [UnityTest]
        public IEnumerator SeriesLocksBuildAndStockReplayChecksTheModel()
        {
            Assert.That(Selection.SelectStock(false), Is.True);
            yield return StartDuel(DuelSetup.Quick());
            yield return GameplayTestScene.FixedSteps(10);
            match.Opponent.GetComponent<RobotHealth>().TakeDamage(1000f);
            yield return null;
            match.Prepare();
            yield return null;
            var archive = new MatchReplayArchiveStore(Path.Combine(temporary, "replays"));
            var oldClip = archive.List().Single();
            var playback = match.GetComponent<MatchReplayArchivePlayer>();
            Assert.That(playback.CheckCompatibility(oldClip.Info), Is.Null);
            var loadout = Parts("hard", "heavy", "fork");
            Apply(loadout);
            Assert.That(playback.CheckCompatibility(oldClip.Info), Is.Null);
            Assert.That(playback.OpenArchiveMode(), Is.True);
            Assert.That(playback.Open(oldClip.Path), Is.True, playback.LastError);
            Assert.That(Selection.LastModular, Is.EqualTo(loadout));
            playback.CloseArchiveMode();
            yield return StartDuel(DuelSetup.Series());
            string id = match.EncounterId;
            match.Opponent.GetComponent<RobotHealth>().TakeDamage(1000f);
            yield return null;
            Assert.That(match.EncounterComplete, Is.False);
            menu.ContinueResult();
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
            Assert.That(Selection.SelectStock(true), Is.False);
            yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
            Assert.That(match.EncounterId, Is.EqualTo(id));
            Assert.That(Selection.IsModular, Is.True);
            Assert.That(Selection.LastModular, Is.EqualTo(loadout));
            match.Opponent.GetComponent<RobotHealth>().TakeDamage(1000f);
            yield return null;
            Assert.That(match.EncounterComplete, Is.True);
            Assert.That(match.PlayerWins, Is.EqualTo(2));
            match.Prepare();
            yield return null;
            Assert.That(Selection.SelectStock(false), Is.True);
            Assert.That(playback.CheckCompatibility(oldClip.Info), Is.Null);
        }

        [UnityTest]
        public IEnumerator UnknownMissingAndFutureRecordsRemainIntact()
        {
            var fallback = Parts("soft", "light", "spinner");
            var unknown = fallback;
            unknown.tireId = "absent";
            var missing = fallback;
            missing.armorId = null;
            string file = Path.Combine(temporary, "protected.json");
            foreach (var invalid in new[] {
                new WorkshopSave { version = 1, selected = "modular", lastModular = unknown },
                new WorkshopSave { version = 1, selected = "modular", lastModular = missing },
                new WorkshopSave { version = 72, selected = "modular", lastModular = fallback } })
            {
                string original = JsonUtility.ToJson(invalid);
                File.WriteAllText(file, original);
                var store = new WorkshopStore(file);
                store.Load();
                Assert.That(store.IsProtected, Is.True);
                Assert.That(store.Notice, Is.Not.Null.And.Not.Empty);
                Assert.That(store.Save(new WorkshopSave { version = 1, selected = "spinner", lastModular = fallback }), Is.False);
                Assert.That(Selection.UseStore(store), Is.True);
                Assert.That(Selection.Notice, Is.Not.Null.And.Not.Empty);
                Assert.That(Selection.SelectStock(true), Is.True);
                Assert.That(Selection.IsModular, Is.False);
                Assert.That(File.ReadAllText(file), Is.EqualTo(original));
            }
            Assert.That(Selection.TryApply(unknown), Is.False);
            Assert.That(Selection.TryApply(missing), Is.False);
            yield return null;
        }

        [UnityTest]
        [UnityPlatform(RuntimePlatform.WindowsPlayer)]
        [Timeout(180000)]
        public IEnumerator WindowsWorkshopToResultAndRematch()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            var mode = Screen.fullScreenMode;
            int width = Screen.width, height = Screen.height;
            string folder = Environment.GetEnvironmentVariable("WORKSHOP_WINDOWS_FRAMES");
            if (string.IsNullOrEmpty(folder)) folder = Path.Combine(Application.temporaryCachePath, "WorkshopWindowsFrames");
            Directory.CreateDirectory(folder);
            try
            {
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                float until = Time.realtimeSinceStartup + 10f;
                while ((Screen.width != 1280 || Screen.height != 720) && Time.realtimeSinceStartup < until) yield return null;
                var value = Parts("hard", "heavy", "fork");
                Assert.That(menu.OpenWorkshop(), Is.True);
                Assert.That(menu.SetWorkshopDraft(value), Is.True);
                yield return CaptureFrame(folder, "01-workshop");
                Assert.That(menu.ApplyWorkshop(), Is.True);
                yield return StartDuel(DuelSetup.Quick());
                Assert.That(Selection.TryApply(Parts("soft", "light", "spinner")), Is.False);
                yield return CaptureFrame(folder, "02-playing");
                yield return PilotDuel(value);
                yield return CaptureFrame(folder, "03-result");
                string encounter = match.EncounterId;
                int count = match.MatchCount;
                menu.ContinueResult();
                Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown));
                yield return CaptureFrame(folder, "04-rematch");
                yield return new WaitUntil(() => match.State == MatchController.MatchState.Playing);
                Assert.That(match.MatchCount, Is.EqualTo(count + 1));
                Assert.That(match.EncounterId, Is.Not.EqualTo(encounter));
                Assert.That(Selection.LastModular, Is.EqualTo(value));
                Assert.That(Selection.IsModular, Is.True);
                Assert.That(match.Player.GetComponent<RobotDamageVisuals>().DetachedCount, Is.Zero);
                Assert.That(match.Player.GetComponent<MeleeAttack>().CooldownRemaining, Is.Zero);
                var health = match.Player.GetComponent<RobotHealth>();
                Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
                yield return PilotDuel(value);
            }
            finally { Screen.SetResolution(width, height, mode); }
        }

        private static IEnumerator CaptureFrame(string folder, string phase)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That(frame.width, Is.EqualTo(1280));
                Assert.That(frame.height, Is.EqualTo(720));
                File.WriteAllBytes(Path.Combine(folder, phase + ".png"), frame.EncodeToPNG());
                TestContext.WriteLine($"WORKSHOP_FRAME {phase} {frame.width}x{frame.height} {SystemInfo.graphicsDeviceType}");
            }
            finally { UnityEngine.Object.Destroy(frame); }
        }
    }
}
