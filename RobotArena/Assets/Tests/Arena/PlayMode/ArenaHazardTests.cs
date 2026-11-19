using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotSavaslari.Arena.Tests
{
    public sealed class ArenaHazardTests
    {
        private Scene owned;
        private ArenaEnvironment arena;
        private string history;
        private float oldScale;
        private const string ScenePath = "Assets/Scenes/Gameplay/MainArena.unity";

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            oldScale = Time.timeScale;
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            owned = SceneManager.GetSceneByPath(ScenePath);
            arena = owned.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ArenaEnvironment>()).Single();
            yield return null;
            history = Path.Combine(Application.temporaryCachePath, "arena-" + Guid.NewGuid() + ".json");
            arena.Match.HistoryStore = new MatchHistoryStore(history);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = oldScale;
            if (owned.IsValid() && owned.isLoaded) yield return SceneManager.UnloadSceneAsync(owned);
            if (history != null && File.Exists(history)) File.Delete(history);
        }

        private void Select(string name)
        {
            Assert.That(arena.Select(arena.Layouts.ToList().FindIndex(layout => layout.displayName == name)), Is.True, name);
        }

        [UnityTest]
        public IEnumerator RepeatedSelectionRetiresEveryPreviousHazard()
        {
            for (int i = 0; i < 10; i++)
            {
                var old = arena.Contents;
                Select(i % 2 == 0 ? "PulseArena" : "SweepArena");
                Assert.That(old.gameObject.activeSelf, Is.False);
                Assert.That(old.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
                Assert.That(arena.Contents.GetComponentsInChildren<ArenaHazard>().Length, Is.EqualTo(2));
            }
            yield return null;
            Assert.That(arena.GetComponentsInChildren<ArenaHazard>(true).Length, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DamageIsEnvironmentalAndStopsOnPauseAndDefeat()
        {
            foreach (string name in new[] { "PulseArena", "SweepArena" })
            {
                Select(name);
                var hazard = arena.Contents.GetComponentInChildren<ArenaHazard>();
                var robot = arena.Match.Player;
                var health = robot.GetComponent<RobotHealth>();
                var lastHit = default(RobotHit);
                int hits = 0;
                void Observe(RobotHit hit) { lastHit = hit; hits++; }
                health.Damaged += Observe;
                try
                {
                    var fixedUpdate = Step(hazard);
                    void Tick(int n) { for (int i = 0; i < n; i++) fixedUpdate(); }
                    Tick(200);
                    Assert.AreEqual(HazardPhase.Idle, hazard.Phase);
                    Assert.IsTrue(arena.Match.RequestCountdown());
                    Tick(200);
                    Assert.AreEqual(HazardPhase.Idle, hazard.Phase);
                    Time.timeScale = 8;
                    yield return new WaitUntil(() => arena.Match.State == MatchController.MatchState.Playing);
                    Time.timeScale = 1;
                    hazard.ResetCycle();
                    AddCompoundCollider(robot.transform);
                    robot.transform.position = hazard.transform.position;
                    Physics.SyncTransforms();
                    Tick(Mathf.CeilToInt(1f / Time.fixedDeltaTime) + 1);
                    Tick(45);
                    Assert.AreEqual(HazardPhase.Warning, hazard.Phase);
                    Assert.AreEqual(0, hits);
                    Time.timeScale = 0;
                    Tick(200);
                    Assert.AreEqual(HazardPhase.Warning, hazard.Phase);
                    Time.timeScale = 1;
                    for (int i = 0; i < 100 && hazard.Phase != HazardPhase.Active; i++) fixedUpdate();
                    Assert.AreEqual(HazardPhase.Active, hazard.Phase);
                    Tick(24);
                    Assert.AreEqual(1, hits);
                    Assert.IsNull(lastHit.Source);
                    for (int i = 0; i < 100 && hazard.Phase == HazardPhase.Active; i++) fixedUpdate();
                    Assert.AreEqual(HazardPhase.Recovery, hazard.Phase);
                    Assert.AreEqual(name == "PulseArena" ? 3 : 1, hits);
                    for (int cycle = 0; cycle < 20 && !health.IsDefeated; cycle++) Tick(300);
                    Assert.IsTrue(health.IsDefeated);
                    Assert.AreEqual(1, arena.Match.ResultCount);
                    Assert.AreEqual(health.MaxHealth, arena.Match.LastRound.player.receivedDamage);
                    Assert.AreEqual(0, arena.Match.LastRound.opponent.dealtDamage);
                    int total = hits;
                    Tick(300);
                    Assert.AreEqual(total, hits);
                    arena.Match.Prepare();
                    Assert.AreEqual(HazardPhase.Idle, hazard.Phase);
                }
                finally { health.Damaged -= Observe; }
            }
        }

        private static void AddCompoundCollider(Transform robot)
        {
            var child = new GameObject("Compound contact");
            child.transform.SetParent(robot, false);
            child.transform.localPosition = Vector3.up * .25f;
            child.AddComponent<BoxCollider>().size = Vector3.one * .5f;
        }

        private static Action Step(ArenaHazard hazard) =>
            (Action)Delegate.CreateDelegate(typeof(Action), hazard,
                typeof(ArenaHazard).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic));

        [UnityTest]
        public IEnumerator MeasureEightActiveHazards()
        {
            var match = arena.Match;
            Assert.IsTrue(match.StartMatch());
            var ticks = new Action[8];
            Vector3 center = match.Player.transform.position;
            AddCompoundCollider(match.Player.transform);
            AddCompoundCollider(match.Opponent.transform);
            match.Opponent.transform.position = center;
            Physics.SyncTransforms();
            for (int i = 0; i < ticks.Length; i++)
            {
                var host = new GameObject("Measured hazard");
                host.transform.SetParent(arena.Contents);
                host.transform.position = center;
                var hazard = host.AddComponent<ArenaHazard>();
                hazard.Configure(match, new ArenaCellData { content = i % 2 == 0 ? ArenaContent.Pulse : ArenaContent.Sweep,
                    activeSeconds = 120, repeatSeconds = .5f, damageOverride = .01f }, null);
                ticks[i] = Step(hazard);
            }
            const int warmup = 250, count = 1500;
            for (int i = 0; i < warmup; i++) foreach (var tick in ticks) tick();
            var samples = new double[count];
            int playerColliders = match.Player.GetComponentsInChildren<Collider>().Length;
            int rivalColliders = match.Opponent.GetComponentsInChildren<Collider>().Length;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < count; i++)
            {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                foreach (var tick in ticks) tick();
                samples[i] = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Array.Sort(samples);
            Debug.Log($"Arena performance {Application.platform} {Application.unityVersion}, {SystemInfo.processorType}, " +
                $"{SystemInfo.systemMemorySize} MB, {Screen.width}x{Screen.height}, 8 hazards, 2 robots ({playerColliders}/{rivalColliders} colliders), " +
                $"{warmup} warmup, {count} samples, p50={samples[count/2]:F4} p95={samples[count*95/100]:F4} max={samples[count-1]:F4} ms, {allocated} managed bytes");
            Assert.That(allocated, Is.Zero);
            Assert.That(samples[count*95/100], Is.LessThanOrEqualTo(1d));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BotMatchMatrix()
        {
            Time.timeScale = 8;
            var match = arena.Match;
            var panel = match.GetComponent<OpponentSelectionPanel>();
            foreach (string layout in new[] { "PulseArena", "SweepArena" })
            foreach (BotPersonality personality in Enum.GetValues(typeof(BotPersonality)))
            foreach (BotDifficulty difficulty in Enum.GetValues(typeof(BotDifficulty)))
            for (int run = 1; run <= 2; run++)
            {
                Select(layout);
                Assert.IsTrue(panel.Select(personality, difficulty));
                Assert.IsTrue(match.ConfigureRules(new MatchRules { timeLimitSeconds = 30 }));
                Assert.IsTrue(match.RequestCountdown());
                float deadline = Time.realtimeSinceStartup + 90f;
                while (match.State != MatchController.MatchState.Result && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(MatchController.MatchState.Result, match.State);
                Assert.IsTrue(match.LastMatch.IsValid);
                Debug.Log($"Arena matrix {layout} {personality} {difficulty} #{run}: {match.LastMatch.outcome}, {match.LastMatch.durationSeconds:F2}s");
                match.Prepare();
            }
        }
    }
}
