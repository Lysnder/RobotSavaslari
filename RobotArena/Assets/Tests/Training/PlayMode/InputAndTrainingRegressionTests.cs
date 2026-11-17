using System.Collections;
using System.IO;
using NUnit.Framework;
using RobotSavaslari.Arena;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotSavaslari.Training.Tests
{
    public sealed class InputAndTrainingRegressionTests : InputTestFixture
    {
        private Keyboard keyboard;
        private MatchController match;
        private DrivingFeatureInstaller installer;
        private byte[] savedBest;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            Time.timeScale = 1f;
            savedBest = File.Exists(GhostLapStore.FilePath) ? File.ReadAllBytes(GhostLapStore.FilePath) : null;
            GhostLapStore.Clear();
            DriveBindings.ResetToDefaults();
        }

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            yield return SceneManager.LoadSceneAsync("MainArena");
            yield return null;
            yield return null;
            match = Object.FindAnyObjectByType<MatchController>();
            installer = Object.FindAnyObjectByType<DrivingFeatureInstaller>();
            if (installer == null) installer = new GameObject("Driving Features").AddComponent<DrivingFeatureInstaller>();
            Assert.IsTrue(installer.Install());
            MainArenaPresentationInstaller.Install(SceneManager.GetActiveScene());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadArena()
        {
            if (installer != null) installer.TimeTrial.Exit();
            Time.timeScale = 1f;
            var empty = SceneManager.CreateScene("InputTrainingCleanup");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync("MainArena");
            if (installer != null) Object.Destroy(installer.gameObject);
            yield return null;
        }

        public override void TearDown()
        {
            if (savedBest != null) File.WriteAllBytes(GhostLapStore.FilePath, savedBest);
            else GhostLapStore.Clear();
            DriveBindings.ResetToDefaults();
            Time.timeScale = 1f;
            base.TearDown();
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnterDuringCaptureDoesNotStartMatch()
        {
            installer.BindingsScreen.Open();
            foreach (var key in new[] { Key.Enter, Key.NumpadEnter })
            {
                installer.BindingsScreen.BeginCapture(DriveAction.Forward);
                yield return Tap(key);
                Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Ready));
                Assert.IsTrue(installer.BindingsScreen.IsOpen);
                Assert.That(DriveBindings.Forward, Is.EqualTo(Key.W));
            }
            installer.BindingsScreen.Close();
            yield return Tap(Key.Enter);
            Assert.That(match.State, Is.EqualTo(MatchController.MatchState.Countdown), "Normal menu shortcut must still work.");
        }

        [UnityTest]
        public IEnumerator CameraKeyIsRejectedAndDoesNotChangeCameraDuringCapture()
        {
            var camera = Object.FindAnyObjectByType<CombatCameraController>();
            var before = camera.Mode;
            installer.BindingsScreen.Open();
            installer.BindingsScreen.BeginCapture(DriveAction.Forward);
            yield return Tap(Key.C);
            Assert.That(DriveBindings.Forward, Is.EqualTo(Key.W));
            StringAssert.Contains("C", installer.BindingsScreen.Message);
            Assert.That(camera.Mode, Is.EqualTo(before));
            installer.BindingsScreen.Close();
            yield return Tap(Key.C);
            Assert.That(camera.Mode, Is.Not.EqualTo(before), "Camera shortcut must still work outside the modal.");
        }

        [UnityTest]
        public IEnumerator EscapeCancelsCaptureWithoutOpeningSettings()
        {
            installer.BindingsScreen.Open();
            installer.BindingsScreen.BeginCapture(DriveAction.Forward);
            yield return Tap(Key.Escape);
            Assert.IsNull(installer.BindingsScreen.Capturing);
            Assert.IsTrue(installer.BindingsScreen.IsOpen);
            Assert.IsFalse(match.GetComponent<SettingsPanelController>().IsOpen);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator CheckpointMarkersAreAboveArenaSurface()
        {
            var bounds = Object.FindAnyObjectByType<ArenaBounds>();
            Assert.IsTrue(installer.TimeTrial.Enter());
            yield return null;
            yield return null;
            for (int i = 0; i < installer.TimeTrial.CheckpointCount; i++)
            {
                var marker = GameObject.Find($"Egitim Kontrol Noktasi {i + 1}");
                Assert.IsNotNull(marker);
                Assert.That(marker.GetComponent<Renderer>().bounds.min.y, Is.GreaterThan(bounds.SurfaceHeight));
                Assert.IsNull(marker.GetComponent<Collider>());
            }
        }

        [UnityTest]
        public IEnumerator RetryDuringUnfinishedLapDiscardsOldSamples()
        {
            var trial = installer.TimeTrial;
            var runner = installer.GetComponent<GhostRunner>();
            Assert.IsTrue(trial.Enter());
            yield return new WaitForSeconds(.3f);
            Assert.That(runner.SampleCount, Is.GreaterThan(2));
            Assert.That(trial.State, Is.EqualTo(TimeTrialState.Running));
            trial.Retry();
            yield return null;
            yield return null;
            Assert.That(runner.SampleCount, Is.InRange(1, 2), "Old attempt samples must be discarded immediately.");
            yield return new WaitForSeconds(.15f);
            Assert.That(runner.SampleCount, Is.GreaterThan(2), "Fresh recording must continue from zero.");
            for (int i = 0; i < trial.CheckpointCount; i++)
            {
                var position = trial.GetCheckpoint(i).position;
                var body = trial.Motor.Body;
                position.y = body.position.y;
                body.position = position;
                trial.Motor.transform.position = position;
                trial.Motor.ResetMotion();
                yield return null;
                yield return null;
            }
            Assert.That(trial.State, Is.EqualTo(TimeTrialState.Finished));
            var saved = GhostLapStore.Load(trial.CourseId);
            Assert.IsNotNull(saved);
            for (int i = 1; i < saved.samples.Length; i++)
            {
                Assert.That(saved.samples[i].time, Is.GreaterThan(saved.samples[i - 1].time));
                Assert.That(saved.samples[i].time, Is.LessThanOrEqualTo(saved.lapTime));
            }
        }

        [UnityTest]
        public IEnumerator LayoutSelectionAndContentChangesInvalidateOldBest()
        {
            var arena = Object.FindAnyObjectByType<ArenaEnvironment>();
            var trial = installer.TimeTrial;
            var runner = installer.GetComponent<GhostRunner>();
            Assert.IsTrue(arena.Select(0));
            string openId = trial.CourseId;
            Assert.IsTrue(GhostLapStore.Save(new GhostLapRecording
            {
                courseId = openId, lapTime = 10f,
                samples = new[] { new GhostLapSample(), new GhostLapSample { time = 10f, position = Vector3.one } }
            }));
            runner.ReloadPersonalBest();
            Assert.IsNotNull(runner.PersonalBest);
            Assert.IsTrue(arena.Select(1));
            Assert.That(trial.CourseId, Is.Not.EqualTo(openId));
            runner.ReloadPersonalBest();
            Assert.IsNull(runner.PersonalBest);
            string tacticalId = trial.CourseId;
            var cell = arena.Layout.cells[0];
            var original = cell.content;
            try
            {
                cell.content = ArenaContent.Obstacle;
                Assert.That(trial.CourseId, Is.Not.EqualTo(tacticalId), "Edits to an existing layout must invalidate its old record too.");
            }
            finally { cell.content = original; }
            Assert.That(trial.CourseId, Is.EqualTo(tacticalId));
            Assert.IsTrue(arena.Select(0));
            Assert.That(trial.CourseId, Is.EqualTo(openId));
            runner.ReloadPersonalBest();
            Assert.IsNotNull(runner.PersonalBest);
            yield return null;
        }
    }
}
