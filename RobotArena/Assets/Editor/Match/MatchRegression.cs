using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RobotSavaslari.Match.Editor
{
    [InitializeOnLoad]
    public static class MatchRegression
    {
        private const string BatchKey = "Match.Regression.Batch";
        private const string PendingKey = "Match.Regression.RunPending";
        public static string Status { get; private set; } = "Not run";
        private static double deadline;
        static MatchRegression() { EditorApplication.playModeStateChanged += OnPlayMode; }

        [MenuItem("RobotSavaslari/Match/Run Match Regression")]
        public static void RunFresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before starting a fresh regression.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(MatchRegressionSceneBuilder.ScenePath);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        public static void RunBatch()
        {
            SessionState.SetBool(BatchKey, true);
            RunFresh();
        }

        private static void CompleteBatch(int exitCode)
        {
            if (!SessionState.GetBool(BatchKey, false)) return;
            SessionState.SetBool(BatchKey, false);
            EditorApplication.Exit(exitCode);
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                Status = "Waiting for Start";
                deadline = EditorApplication.timeSinceStartup + 40;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                if (Status == "Running" || Status == "Waiting for Start") Status = "Cancelled: Play mode stopped";
            }
        }

        private static void Tick()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Regression timeout");
                if (Status == "Waiting for Start")
                {
                    if (Time.frameCount < 2) return;
                    var match = UnityEngine.Object.FindFirstObjectByType<MatchController>();
                    Require(match.ManualNotificationsOnly, "Manual mode default");
                    foreach (var robot in UnityEngine.Object.FindObjectsByType<RobotPrototypeController>())
                        Require(!robot.enabled, "No combat/bot ticks required");
                    MatchFlowChecks.Run();
                    MatchResultChecks.Run();
                    CheckNotificationScenarios(match);
                    MatchResetChecks.Begin();
                    Status = "Running";
                    return;
                }
                string b3 = MatchResetChecks.Status;
                if (b3 == "Running") return;
                Require(b3.StartsWith("PASS:"), b3);
                Status = "PASS: B1-B3, manual-only buttons, invalid notifications before/during/after match, duplicates, both same-evaluation orders (current first-notice behavior). PENDING: Emin tie policy.";
                EditorApplication.update -= Tick;
                Debug.Log(Status);
                CompleteBatch(0);
            }
            catch (Exception ex)
            {
                Status = "FAIL: " + ex.Message;
                EditorApplication.update -= Tick;
                Debug.LogError(Status);
                CompleteBatch(1);
            }
        }

        private static void CheckNotificationScenarios(MatchController match)
        {
            match.DemoInvalidNotifications();
            Require(match.State == MatchController.MatchState.Ready && match.ResultCount == 0, "Invalid before match");
            foreach (bool playerFirst in new[] { true, false })
            {
                Require(match.StartMatch(), "Scenario start");
                match.DemoInvalidNotifications();
                Require(match.State == MatchController.MatchState.Playing && match.ResultCount == 0, "Invalid during match");
                match.DemoSameEvaluation(playerFirst);
                var expected = playerFirst ? MatchController.MatchOutcome.OpponentVictory : MatchController.MatchOutcome.PlayerVictory;
                Require(match.Outcome == expected && match.ResultCount == 1, "Current first-notice behavior; tie policy pending");
                match.DemoRepeatedNotifications();
                match.DemoInvalidNotifications();
                Require(match.Outcome == expected && match.ResultCount == 1 && !match.PlayerAllowed && !match.OpponentAllowed, "Locked after result");
                match.Prepare();
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("B4: " + message);
        }
    }
}
