using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RobotSavaslari.Burak.Editor
{
    [InitializeOnLoad]
    public static class BurakB4Regression
    {
        private const string PendingKey = "Burak.B4.RunPending";
        public static string Status { get; private set; } = "Not run";
        private static double deadline;
        static BurakB4Regression() { EditorApplication.playModeStateChanged += OnPlayMode; }

        [MenuItem("RobotSavaslari/Burak/Run B1-B4 Regression")]
        public static void RunFresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before starting a fresh regression.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(BurakB1SceneBuilder.ScenePath);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
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
                    var match = UnityEngine.Object.FindFirstObjectByType<BurakMatchController>();
                    Require(match.ManualNotificationsOnly, "Manual mode default");
                    foreach (var robot in UnityEngine.Object.FindObjectsByType<RobotPrototypeController>())
                        Require(!robot.enabled, "No combat/bot ticks required");
                    BurakB1Checks.Run();
                    BurakB2Checks.Run();
                    CheckNotificationScenarios(match);
                    BurakB3Checks.Begin();
                    Status = "Running";
                    return;
                }
                string b3 = BurakB3Checks.Status;
                if (b3 == "Running") return;
                Require(b3.StartsWith("PASS:"), b3);
                Status = "PASS: B1-B3, manual-only buttons, invalid notifications before/during/after match, duplicates, both same-evaluation orders (current first-notice behavior). PENDING: Emin tie policy.";
                EditorApplication.update -= Tick;
                Debug.Log(Status);
            }
            catch (Exception ex)
            {
                Status = "FAIL: " + ex.Message;
                EditorApplication.update -= Tick;
                Debug.LogError(Status);
            }
        }

        private static void CheckNotificationScenarios(BurakMatchController match)
        {
            match.DemoInvalidNotifications();
            Require(match.State == BurakMatchController.MatchState.Ready && match.ResultCount == 0, "Invalid before match");
            foreach (bool playerFirst in new[] { true, false })
            {
                Require(match.StartMatch(), "Scenario start");
                match.DemoInvalidNotifications();
                Require(match.State == BurakMatchController.MatchState.Playing && match.ResultCount == 0, "Invalid during match");
                match.DemoSameEvaluation(playerFirst);
                var expected = playerFirst ? BurakMatchController.MatchOutcome.OpponentVictory : BurakMatchController.MatchOutcome.PlayerVictory;
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
