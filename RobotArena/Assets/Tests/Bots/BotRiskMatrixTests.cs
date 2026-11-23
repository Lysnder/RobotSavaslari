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
        private float oldScale;

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            Time.timeScale = oldScale;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (data != null && Directory.Exists(data)) Directory.Delete(data, true);
        }

        [UnityTest]
        public IEnumerator SixteenSeededArenaDuelsFinishWithinBudget()
        {
            oldScale = Time.timeScale;
            Time.timeScale = 1;
            const string path = "Assets/Scenes/Gameplay/MainArena.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(path);
            yield return null;
            var arena = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ArenaEnvironment>()).Single();
            var match = arena.Match;
            data = Path.Combine(Application.temporaryCachePath, "bot03-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(data);
            match.HistoryStore = new MatchHistoryStore(Path.Combine(data, "history.json"));
            Assert.IsTrue(MainArenaPresentationInstaller.UseArchiveStore(match, new MatchReplayArchiveStore(Path.Combine(data, "replays"))));
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
