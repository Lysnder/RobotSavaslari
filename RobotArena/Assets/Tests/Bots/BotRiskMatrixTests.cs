using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.Combat;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotSavaslari.Tests
{
    public sealed class BotRiskMatrixTests
    {
        private Scene scene;
        private string data;
        private ArenaEnvironment arena;
        private float oldScale;

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            Time.timeScale = oldScale;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (data != null && Directory.Exists(data)) Directory.Delete(data, true);
        }

        [UnitySetUp]
        public IEnumerator LoadMainArena()
        {
            oldScale = Time.timeScale;
            Time.timeScale = 1;
            const string path = "Assets/Scenes/Gameplay/MainArena.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(path);
            yield return null;
            arena = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ArenaEnvironment>()).Single();
            var match = arena.Match;
            data = Path.Combine(Application.temporaryCachePath, "bot03-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(data);
            match.HistoryStore = new MatchHistoryStore(Path.Combine(data, "history.json"));
            Assert.IsTrue(MainArenaPresentationInstaller.UseArchiveStore(match, new MatchReplayArchiveStore(Path.Combine(data, "replays"))));
        }

        [UnityTest]
        public IEnumerator SixteenSeededArenaDuelsFinishWithinBudget()
        {
            var match = arena.Match;
            var panel = match.GetComponent<OpponentSelectionPanel>();
            var next = new WaitForFixedUpdate();
            Time.timeScale = 5;
            foreach (string name in new[] { "Open", "Tactical", "PulseArena", "SweepArena" })
            foreach (BotPersonality personality in Enum.GetValues(typeof(BotPersonality)))
            foreach (BotDifficulty difficulty in Enum.GetValues(typeof(BotDifficulty)))
            {
                if (match.State == MatchController.MatchState.Result) match.Prepare();
                int index = arena.Layouts.ToList().FindIndex(layout => layout.displayName == name);
                Assert.IsTrue(arena.Select(index), name);
                Assert.IsTrue(match.ConfigureRules(new MatchRules { timeLimitSeconds = 18, mode = MatchMode.Single }));
                Assert.IsTrue(panel.Select(personality, difficulty));
                Assert.IsTrue(match.StartMatch());
                panel.ApplyToCombatBot();
                var bot = match.Opponent.GetComponent<RobotBotController>();
                var weapon = bot.GetComponent<MeleeAttack>();
                var health = bot.GetComponent<RobotHealth>();
                var hazard = arena.Contents.GetComponentInChildren<ArenaHazard>();
                var botBody = bot.GetComponent<Rigidbody>();
                // Place both robots on the same exposed approach for each hazard. Wait for the visible warning.
                if (hazard != null)
                {
                    for (int i = 0; i < 65 && hazard.Phase == HazardPhase.Idle; i++) yield return next;
                    Assert.AreEqual(HazardPhase.Warning, hazard.Phase);
                    Vector3 middle = hazard.transform.position;
                    middle.z = Mathf.Clamp(middle.z, -6.8f, 6.8f);
                    Place(botBody, middle + Vector3.left * 3.5f, 90);
                    Place(match.Player.GetComponent<Rigidbody>(), middle + Vector3.right * 3.5f, -90);
                    Physics.SyncTransforms();
                }
                bot.ConfigureDecisions(personality, difficulty, 740);
                float start = Time.fixedTime, firstHit = -1, hazardDamage = 0, distance = 0;
                int hits = 0, hazardHits = 0, queries = 0, nodes = 0, plans = 0, initialRecovery = bot.RecoveryCount;
                var previous = botBody.position;
                var sampleTicks = new long[1100];
                long maxBytes = 0;
                int measured = 0, warmup = 8;
                void OnTick(long ticks, long bytes)
                {
                    if (warmup-- > 0 || measured >= sampleTicks.Length) return;
                    sampleTicks[measured++] = ticks;
                    maxBytes = Math.Max(maxBytes, bytes);
                }
                void OnHit(MeleeAttack attack, RobotHealth victim, float amount)
                {
                    if (++hits == 1) firstHit = Time.fixedTime - start;
                }
                void OnDamage(RobotHit hit)
                {
                    if (hit.Weapon || hit.Source != null) return;
                    hazardHits++;
                    hazardDamage += hit.Amount;
                }
                bot.TickMeasured += OnTick;
                health.Damaged += OnDamage;
                weapon.TargetHit += OnHit;
                try
                {
                    for (int ticks = 0; ticks < 1050 && match.State == MatchController.MatchState.Playing; ticks++)
                    {
                        yield return next;
                        queries = Mathf.Max(queries, bot.NavigationQueries);
                        nodes = Mathf.Max(nodes, bot.NavigationExpansions);
                        plans = Mathf.Max(plans, bot.NavigationPlans);
                        distance += Vector3.Distance(previous, botBody.position);
                        previous = botBody.position;
                    }
                }
                finally
                {
                    bot.TickMeasured -= OnTick;
                    health.Damaged -= OnDamage;
                    weapon.TargetHit -= OnHit;
                }
                Array.Sort(sampleTicks, 0, measured);
                Assert.Greater(measured, 0);
                double p95 = sampleTicks[Mathf.CeilToInt(measured * .95f) - 1] * 1000d / System.Diagnostics.Stopwatch.Frequency;
                Debug.Log(FormattableString.Invariant($"BOT03,{name},{personality},{difficulty},740,{hazardHits},{hazardDamage:F1},{firstHit:F2},{hits},{distance:F2},{bot.RecoveryCount-initialRecovery},{plans},{queries},{nodes},{p95:F4},{maxBytes},{match.Outcome},{match.LastRound?.reason}"));
                Assert.AreEqual(MatchController.MatchState.Result, match.State);
                Assert.AreNotEqual(MatchController.MatchOutcome.None, match.Outcome);
                Assert.LessOrEqual(queries, 64);
                Assert.LessOrEqual(nodes, 128);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator PulseWarningDetoursWaitsForExposedTargetAndResumesAfterPauseAndRecovery()
        {
            int index = arena.Layouts.ToList().FindIndex(layout => layout.displayName == "PulseArena");
            Assert.IsTrue(arena.Select(index));
            var match = arena.Match;
            Time.timeScale = 5f;
            Assert.IsTrue(match.RequestCountdown());
            for (int i = 0; i < 500 && match.State == MatchController.MatchState.Countdown; i++)
                yield return new WaitForFixedUpdate();
            Assert.AreEqual(MatchController.MatchState.Playing, match.State);
            Time.timeScale = 1f;
            var bot = match.Opponent.GetComponent<RobotBotController>();
            var botBody = bot.GetComponent<Rigidbody>();
            var targetBody = match.Player.GetComponent<Rigidbody>();
            var hazards = arena.Contents.GetComponentsInChildren<ArenaHazard>();
            var hazard = hazards[0];
            var center = hazard.transform.position;
            var fixedUpdate = typeof(ArenaHazard).GetMethod("FixedUpdate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var steps = hazards.Select(source => (Action)Delegate.CreateDelegate(typeof(Action), source, fixedUpdate)).ToArray();
            foreach (var source in hazards) source.ResetCycle();
            void Phase(HazardPhase phase)
            {
                for (int i = 0; i < 225 && hazard.Phase != phase; i++)
                    foreach (var advance in steps) advance();
                Assert.IsTrue(hazards.All(source => source.Phase == phase));
            }
            Place(botBody, center + Vector3.left * 3.5f, 90);
            Place(targetBody, center + Vector3.right * 3.5f + Vector3.back * 1.75f, -90);
            Physics.SyncTransforms();
            Phase(HazardPhase.Warning);
            bot.ConfigureDecisions(BotPersonality.Aggressive, BotDifficulty.Hard);
            bool steered = false;
            for (int i = 0; i < 80; i++)
            {
                bot.Tick(.02f);
                Assert.LessOrEqual(bot.NavigationQueries, 64);
                if (bot.NavigationReason == BotRouteReason.HazardDetour && Mathf.Abs(bot.Command.y) > .1f) steered = true;
            }
            Assert.IsTrue(steered, "The warning should produce a lateral route to a safe opponent.");
            Phase(HazardPhase.Active);
            Place(targetBody, center, -90);
            Physics.SyncTransforms();
            bot.Tick(.02f);
            Assert.AreEqual(BotRouteReason.HazardWait, bot.NavigationReason);
            Assert.AreEqual(Vector2.zero, bot.Command);
            Assert.IsFalse(bot.AttackRequested);
            Place(botBody, center + Vector3.left, -90);
            Physics.SyncTransforms();
            bot.Tick(.02f);
            Assert.AreEqual(BotRouteReason.HazardEscape, bot.NavigationReason, "A bot already in danger must look for an exit.");
            Place(botBody, center + Vector3.left * 3.5f, 90);
            Physics.SyncTransforms();
            bot.Tick(.02f);
            Assert.AreEqual(BotRouteReason.HazardWait, bot.NavigationReason);

            Time.timeScale = 0f;
            yield return null;
            Assert.AreEqual(BotRouteReason.None, bot.NavigationReason);
            Assert.AreEqual(0, bot.NavigationPlans);
            Time.timeScale = 1f;
            bot.Tick(.02f);
            Assert.AreEqual(BotRouteReason.HazardWait, bot.NavigationReason);

            // Keep both robots intact while fast-forwarding the trap. Matrix runs measure actual damage.
            Place(targetBody, center + Vector3.right * 3.5f + Vector3.back * 1.75f, -90);
            Physics.SyncTransforms();
            Phase(HazardPhase.Recovery);
            Place(targetBody, center, -90);
            Physics.SyncTransforms();
            Vector3 beforeRecovery = botBody.position;
            var nextStep = new WaitForFixedUpdate();
            float travel = 0;
            for (int i = 0; i < 65; i++)
            {
                yield return nextStep;
                Vector3 offset = botBody.position - beforeRecovery;
                travel = new Vector2(offset.x, offset.z).magnitude;
                if (travel >= .15f && bot.NavigationReason != BotRouteReason.HazardWait) break;
            }
            Assert.AreNotEqual(BotRouteReason.HazardWait, bot.NavigationReason);
            Assert.Greater(travel, .14f, $"After recovery: phases {string.Join(",", hazards.Select(z => z.Phase))}, " +
                $"position {beforeRecovery} to {botBody.position}, target {targetBody.position}, state {bot.State}, " +
                $"route {bot.NavigationReason}, plans {bot.NavigationPlans}, command {bot.Command}, match {match.State}.");
            match.Player.GetComponent<RobotHealth>().TakeDamage(10000);
            Assert.AreEqual(MatchController.MatchState.Result, match.State);
            Assert.AreEqual(BotRouteReason.None, bot.NavigationReason);
            Assert.IsTrue(match.RestartMatch());
            for (int i = 0; i < 8 && match.State != MatchController.MatchState.Playing; i++) yield return null;
            Assert.AreEqual(MatchController.MatchState.Playing, match.State);
            Assert.AreEqual(HazardPhase.Idle, hazard.Phase);
            Assert.AreEqual(BotRouteReason.None, bot.NavigationReason);
        }

        private static void Place(Rigidbody body, Vector3 point, float yaw)
        {
            point.y = body.position.y;
            body.position = point;
            body.rotation = Quaternion.Euler(0, yaw, 0);
            body.transform.SetPositionAndRotation(body.position, body.rotation);
            body.linearVelocity = body.angularVelocity = Vector3.zero;
        }
    }
}
