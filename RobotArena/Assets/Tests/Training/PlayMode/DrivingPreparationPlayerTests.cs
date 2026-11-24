using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using RobotSavaslari.Match;
using RobotSavaslari.Presentation;
using RobotSavaslari.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RobotSavaslari.Training.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsPlayer)]
    public sealed class DrivingPreparationPlayerTests
    {
        private string records;

        [UnityTest]
        public IEnumerator TwoPreparationStepsReturnToReadyAndStartADuelAtBothResolutions()
        {
            var evidence = Environment.GetEnvironmentVariable("ROBOTARENA_PREPARATION_EVIDENCE");
            if (string.IsNullOrEmpty(evidence))
                evidence = Path.Combine(Application.temporaryCachePath, "PreparationFrames", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(evidence);
            records = Path.Combine(Application.temporaryCachePath, "preparation-records-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(records);
            var originalMode = Screen.fullScreenMode;
            int originalWidth = Screen.width, originalHeight = Screen.height;
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
                {
                    yield return SceneManager.LoadSceneAsync("MainArena");
                    yield return null;
                    Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                    for (int frame = 0; frame < 120 && (Screen.width != size.x || Screen.height != size.y); frame++)
                        yield return null;
                    Assert.AreEqual(size, new Vector2Int(Screen.width, Screen.height), "Actual Windows framebuffer size");

                    var match = Object.FindAnyObjectByType<MatchController>();
                    var menu = match.GetComponent<MainMenuScreen>();
                    match.HistoryStore = new MatchHistoryStore(Path.Combine(records, "history.json"));
                    MainArenaPresentationInstaller.Install(SceneManager.GetActiveScene());
                    Assert.IsTrue(MainArenaPresentationInstaller.UseArchiveStore(match, new MatchReplayArchiveStore(Path.Combine(records, "replays"))));
                    // The standalone test runner boots its own scene before MainArena.
                    var installer = Object.FindAnyObjectByType<DrivingFeatureInstaller>();
                    if (installer == null) installer = new GameObject("Driving Features").AddComponent<DrivingFeatureInstaller>();
                    Assert.IsTrue(installer.Install());
                    yield return null;
                    var session = installer.TimeTrial;
                    session.ExerciseStore = new DrivingExerciseStore(Path.Combine(records, "exercises.json"));
                    Assert.IsTrue(menu.IsShowingReadyMenu);
                    Assert.IsTrue(session.Enter());
                    Assert.IsTrue(session.StartPreparation(installer.ExerciseCatalog));
                    // The preparation menu button opens its key legend before starting the session.
                    typeof(TimeTrialHud).GetField("showControls", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(installer.GetComponent<TimeTrialHud>(), true);
                    Assert.IsNotEmpty(session.PreparationControls);

                    var fixedStep = new WaitForFixedUpdate();
                    for (int step = 1; step <= 2; step++)
                    {
                        Assert.AreEqual(step, session.PreparationStep);
                        yield return SaveFrame(evidence, size, "step-" + step);
                        var body = session.Motor.Body;
                        Vector3 target = session.GetCheckpoint(0).position;
                        target.y = body.position.y;
                        for (int tick = 0; tick < 130 && session.State == TimeTrialState.Running; tick++)
                        {
                            // Repeat the physical checkpoint pose while the live evaluator measures dwell time.
                            body.position = target;
                            body.rotation = Quaternion.Euler(0f, session.Exercise.targetYaw, 0f);
                            body.linearVelocity = Vector3.zero;
                            yield return fixedStep;
                        }
                        Assert.AreEqual(TimeTrialState.Finished, session.State);
                        Assert.IsTrue(session.ExerciseResult.success, session.Notice);
                        if (step == 1) Assert.IsTrue(session.NextPreparationStep());
                    }
                    yield return SaveFrame(evidence, size, "completed");
                    session.Exit();
                    yield return null;
                    Assert.IsTrue(menu.IsShowingReadyMenu);
                    Assert.AreEqual(0, session.PreparationStep);
                    int matches = match.MatchCount;
                    Assert.IsTrue(match.RequestCountdown());
                    for (int frame = 0; frame < 350 && match.State == MatchController.MatchState.Countdown; frame++)
                        yield return fixedStep;
                    Assert.AreEqual(MatchController.MatchState.Playing, match.State);
                    Assert.AreEqual(matches + 1, match.MatchCount);
                    yield return SaveFrame(evidence, size, "duel");
                    Debug.Log($"Preparation Windows {size.x}x{size.y}: two successful steps, Ready restored, duel {match.MatchCount} began.");
                }
            }
            finally
            {
                Screen.SetResolution(originalWidth, originalHeight, originalMode);
                Application.runInBackground = background;
            }

        }

        [UnityTearDown]
        public IEnumerator ReleaseSceneAndRecords()
        {
            var scene = SceneManager.GetSceneByName("MainArena");
            if (scene.IsValid() && scene.isLoaded)
            {
                var empty = SceneManager.CreateScene("PreparationPlayerCleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (records != null && Directory.Exists(records)) Directory.Delete(records, true);
        }

        private static IEnumerator SaveFrame(string folder, Vector2Int size, string stage)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            Assert.NotNull(frame);
            try
            {
                Assert.AreEqual(size, new Vector2Int(frame.width, frame.height));
                float min = 1f, max = 0f;
                for (int x = 1; x < 20; x++)
                    for (int y = 1; y < 12; y++)
                    {
                        float value = frame.GetPixelBilinear(x / 20f, y / 12f).grayscale;
                        min = Mathf.Min(value, min);
                        max = Mathf.Max(value, max);
                    }
                Assert.Greater(max - min, .12f, "A captured game frame must contain visible content.");
                File.WriteAllBytes(Path.Combine(folder, $"{size.x}x{size.y}-{stage}.png"), frame.EncodeToPNG());
            }
            finally { Object.Destroy(frame); }
        }
    }
}
