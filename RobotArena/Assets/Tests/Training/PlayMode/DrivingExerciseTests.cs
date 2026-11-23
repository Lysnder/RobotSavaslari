using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RobotSavaslari.Training.Tests
{
    public sealed class DrivingExerciseTests
    {
        private string folder;
        private string SavePath => Path.Combine(folder, "driving-exercises.json");

        [SetUp]
        public void SetUp() => folder = Path.Combine(Path.GetTempPath(), "DrivingExercises-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        private static DrivingExerciseDefinition Definition(DrivingExerciseKind kind) =>
            Array.Find(Resources.Load<DrivingExerciseCatalog>("Training/DrivingExerciseCatalog").exercises,
                definition => definition.kind == kind);

        private static DrivingExerciseSession Start(DrivingExerciseDefinition definition) =>
            new DrivingExerciseSession(definition, definition.gates, definition.start);

        [Test]
        public void SlalomCountsMissedGateAndRealImpactAndFinishesOnlyOnce()
        {
            var definition = Definition(DrivingExerciseKind.Slalom);
            var clean = Start(definition);
            var missed = Start(definition);
            for (int i = 0; i < definition.gates.Length; i++)
            {
                Vector3 center = definition.gates[i].position;
                clean.Step(center, 0f, Vector3.forward, .1f, 0);
                missed.Step(center + (i == 0 ? Vector3.right * 2f : Vector3.zero),
                    0f, Vector3.forward, .1f, i == 0 ? 1 : 0);
            }

            Assert.That(clean.Result.success, Is.True);
            Assert.That(missed.Finished, Is.True);
            Assert.That(missed.Result.success, Is.False);
            Assert.That(missed.Result.gateErrors, Is.EqualTo(1));
            Assert.That(missed.Result.contacts, Is.EqualTo(1));
            missed.Step(Vector3.zero, 0f, Vector3.zero, .2f, 1);
            Assert.That(missed.Result.contacts, Is.EqualTo(1));
            Assert.That(missed.Result.seconds, Is.EqualTo(.4f).Within(.001f));
        }

        [Test]
        public void StopResetsDwellWhenSpeedExceedsThresholdAndReportsOvershoot()
        {
            var definition = Definition(DrivingExerciseKind.ControlledStop);
            var trial = Start(definition);
            var target = definition.gates[0];
            trial.Step(target.position + Vector3.forward * (target.radius + .5f), 0f, Vector3.zero, .2f, 0);
            trial.Step(target.position, 0f, Vector3.forward * .15f, .8f, 0);
            trial.Step(target.position, 0f, Vector3.forward * .151f, .2f, 0);
            Assert.That(trial.Dwell, Is.Zero);
            trial.Step(target.position, 0f, Vector3.forward * .15f, .99f, 0);
            Assert.That(trial.Finished, Is.False);
            trial.Step(target.position, 0f, Vector3.zero, .01f, 0);
            Assert.That(trial.Result.success, Is.True);
            Assert.That(trial.Result.overshoot, Is.EqualTo(.5f).Within(.001f));
        }

        [Test]
        public void TurnRequiresBothPositionAndYawAtSameStep()
        {
            var definition = Definition(DrivingExerciseKind.PrecisionTurn);
            var trial = Start(definition);
            var position = definition.gates[0].position;
            trial.Step(position + Vector3.right * .501f, 90f, Vector3.zero, .1f, 0);
            trial.Step(position, 95.01f, Vector3.zero, .1f, 0);
            Assert.That(trial.Finished, Is.False);
            trial.Step(position + Vector3.right * .5f, 95f, Vector3.zero, .1f, 0);
            Assert.That(trial.Result.success, Is.True);
            Assert.That(trial.Result.yawError, Is.EqualTo(5f));
        }

        [Test]
        public void ReverseParkRejectsFrontEntryAndRequiresAngleAndDwell()
        {
            var definition = Definition(DrivingExerciseKind.ReverseParking);
            var trial = Start(definition);
            var position = definition.gates[0].position;
            trial.Step(position, 0f, Vector3.forward, .1f, 0);
            trial.Step(position, 0f, Vector3.zero, 1f, 0);
            Assert.That(trial.Finished, Is.False);
            Assert.That(trial.Result.directionErrors, Is.EqualTo(1));

            trial = Start(definition);
            trial.Step(position, 0f, Vector3.back, .1f, 0);
            trial.Step(position, 8.01f, Vector3.zero, 1f, 0);
            Assert.That(trial.Finished, Is.False);
            trial.Step(position, 8f, Vector3.zero, 1f, 0);
            Assert.That(trial.Result.success, Is.True);
        }

        [Test]
        public void FourKeysPersistIndependentlyAndInvalidOrSlowerAttemptsKeepGhost()
        {
            var first = new DrivingExerciseStore(SavePath);
            foreach (var definition in Resources.Load<DrivingExerciseCatalog>("Training/DrivingExerciseCatalog").exercises)
            {
                string key = definition.exerciseId + "/1/layout/route/drive-v1";
                Assert.That(first.Save(key, new DrivingExerciseResult { success = true, seconds = 2f }, Ghost(key, 2f)), Is.True);
                for (int i = 0; i < 21; i++)
                    Assert.That(first.Save(key, new DrivingExerciseResult { seconds = 1f, contacts = 1 }, null), Is.True);
                Assert.That(first.Save(key, new DrivingExerciseResult { success = true, seconds = 3f }, Ghost(key, 3f)), Is.True);
                var reopened = new DrivingExerciseStore(SavePath);
                Assert.That(reopened.Find(key).history.Count, Is.EqualTo(20));
                Assert.That(reopened.Find(key).ghost.lapTime, Is.EqualTo(2f));
                Assert.That(reopened.Find(key.Replace("/1/", "/2/")), Is.Null);
                Assert.That(reopened.Find(key.Replace("/layout/", "/other-layout/")), Is.Null);
                Assert.That(reopened.Find(key.Replace("drive-v1", "drive-v2")), Is.Null);
            }
        }

        [TestCase("{broken")]
        [TestCase("{\"version\":99,\"entries\":[]}")]
        public void InvalidSaveStaysIntactAndLiveExerciseStillWorks(string raw)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(SavePath, raw);
            var store = new DrivingExerciseStore(SavePath);
            Assert.That(store.Find("exercise"), Is.Null);
            Assert.That(store.Warning, Is.Not.Empty);
            Assert.That(store.Save("exercise", new DrivingExerciseResult { seconds = 1f }, null), Is.False);
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(raw));
        }

        [Test]
        public void LegacyMigrationOnlyCopiesExactCourseAndDoesNotRewriteOnSecondLoad()
        {
            var store = new DrivingExerciseStore(SavePath);
            Assert.That(store.MigrateLegacy("arena-v2-new", _ => Ghost("arena-v2-old", 2f)), Is.Null);
            Assert.That(File.Exists(SavePath), Is.False);
            Assert.That(store.MigrateLegacy("arena-v2-old", _ => Ghost("arena-v2-old", 2f)), Is.Not.Null);
            string first = File.ReadAllText(SavePath);
            store = new DrivingExerciseStore(SavePath);
            Assert.That(store.MigrateLegacy("arena-v2-old", _ => throw new InvalidOperationException()), Is.Not.Null);
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(first));
            Assert.That(store.Find("arena-v2-old"), Is.Null);
        }

        [UnityTest]
        public IEnumerator MainArenaRoutesRetryAndExitKeepTheReadyMatchIntact()
        {
            yield return SceneManager.LoadSceneAsync("MainArena");
            yield return null;
            var match = Object.FindAnyObjectByType<MatchController>();
            var menu = match.GetComponent<MainMenuScreen>();
            var driver = match.Player.GetComponent<PlayerKeyboardDriver>();
            bool readyDriver = driver.enabled;
            var installer = Object.FindAnyObjectByType<DrivingFeatureInstaller>();
            bool ownsInstaller = installer == null;
            if (ownsInstaller) installer = new GameObject("Driving exercise test installer").AddComponent<DrivingFeatureInstaller>();

            yield return null;
            var session = installer.TimeTrial;
            var hud = installer.GetComponent<TimeTrialHud>();
            var runner = installer.GetComponent<GhostRunner>();
            session.ExerciseStore = new DrivingExerciseStore(SavePath);
            Assert.That(hud.ExerciseCatalog.exercises.Length, Is.EqualTo(4));
            int matches = match.MatchCount;
            int results = match.ResultCount;

            try
            {
                for (int i = 0; i < 10; i++)
                {
                    var exercise = hud.ExerciseCatalog.exercises[i % 4];
                    Assert.That(session.EnterExercise(exercise), Is.True, exercise.title);
                    Assert.That(match.enabled, Is.False);
                    Assert.That(menu.enabled, Is.False);
                    var current = session.ExerciseKey;
                    Assert.That(current, Does.StartWith(exercise.exerciseId + "/"));
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();
                    int attempt = session.AttemptNumber;
                    session.Retry();
                    Assert.That(session.ElapsedTime, Is.Zero);
                    Assert.That(session.ExerciseDwell, Is.Zero);
                    Assert.That(session.AttemptNumber, Is.EqualTo(attempt));
                    Assert.That(session.ExerciseKey, Is.EqualTo(current));
                    yield return new WaitForFixedUpdate();
                    Assert.That(runner.SampleCount, Is.LessThanOrEqualTo(1));
                    session.Exit();
                    yield return null;
                    Assert.That(match.enabled, Is.True);
                    Assert.That(menu.enabled, Is.True);
                    Assert.That(driver.enabled, Is.EqualTo(readyDriver));
                }

                Assert.That(match.MatchCount, Is.EqualTo(matches));
                Assert.That(match.ResultCount, Is.EqualTo(results));
                Assert.That(session.Enter(), Is.True);
                Assert.That(session.Exercise, Is.Null);
                session.Exit();
            }
            finally
            {
                session.Exit();
                if (ownsInstaller) Object.Destroy(installer.gameObject);
            }
        }

        [Test]
        public void MeasureWarmedEvaluationAndGhostInterpolation()
        {
            var definition = Definition(DrivingExerciseKind.ControlledStop);
            var trial = Start(definition);
            var ghost = Ghost("profiling", DrivingExerciseSession.TimeLimit);
            ghost.samples = new GhostLapSample[DrivingExerciseStore.MaxSamples];
            for (int i = 0; i < ghost.samples.Length; i++)
                ghost.samples[i] = new GhostLapSample { time = i / 10f, position = Vector3.forward * i };
            ghost.Evaluate(325.45f, out var interpolated, out _);
            Assert.That(interpolated.z, Is.EqualTo(3254.5f).Within(.01f));
            Vector3 position = new Vector3(5f, 0f, 4f);
            var ticks = new long[1000];
            for (int i = 0; i < 120; i++)
            {
                trial.Step(position, 0f, Vector3.forward, .02f, 0);
                ghost.Evaluate(350.15f + i * .01f, out _, out _);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < ticks.Length; i++)
            {
                long start = Stopwatch.GetTimestamp();
                trial.Step(position, 0f, Vector3.forward, .02f, 0);
                ghost.Evaluate(350.15f + i * .01f, out _, out _);
                ticks[i] = Stopwatch.GetTimestamp() - start;
            }

            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Array.Sort(ticks);
            double Scale(long count) => count * 1000.0 / Stopwatch.Frequency;
            TestContext.WriteLine($"DRIVING-02 core 120 warm-up, 1000 samples, 6001 ghost poses: p50 {Scale(ticks[499]):F5}, " +
                                  $"p95 {Scale(ticks[949]):F5}, max {Scale(ticks[999]):F5} ms, {bytes} bytes");
            Assert.That(bytes, Is.Zero);
            Assert.That(Scale(ticks[949]), Is.LessThanOrEqualTo(.5));
        }

        private static GhostLapRecording Ghost(string key, float time) => new GhostLapRecording
        {
            courseId = key,
            lapTime = time,
            samples = new[] { new GhostLapSample { time = 0f }, new GhostLapSample { time = time } }
        };
    }
}
