using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace RobotSavaslari.Editor
{
    public static class BotScenarioChecks
    {
        private const float Dt = 0.02f;
        [MenuItem("Robot Savaslari/Bot/Run H1-H4 Checks (Play Mode)")]
        public static void RunFromMenu() { Debug.Log(RunInPlayMode()); }
        // Drives the actual Rigidbody motor, collisions and decision code, with reproducible physics time.
        public static string RunInPlayMode()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Open BotScenarios and enter Play Mode first.");
            var runner = UnityEngine.Object.FindFirstObjectByType<BotScenarioRunner>();
            if (runner == null) throw new InvalidOperationException("BotScenarios is required.");
            var bot = runner.bot; var motor = bot.GetComponent<RobotDriveMotor>();
            var probe = bot.GetComponent<BotAttackProbe>();
            bool runnerEnabled = runner.enabled, botEnabled = bot.enabled, motorEnabled = motor.enabled;
            SimulationMode mode = Physics.simulationMode;
            RigidbodyInterpolation interpolation = motor.Body.interpolation;
            motor.Body.interpolation = RigidbodyInterpolation.None;
            var report = new List<string>();
            runner.enabled = bot.enabled = motor.enabled = false;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                Require(bot.GetComponent<RobotPrototypeController>() == null, "Lab must not use player controller");
                foreach (var scenario in new[] { BotScenarioRunner.Scenario.Front, BotScenarioRunner.Scenario.Side, BotScenarioRunner.Scenario.Behind })
                {
                    runner.Select(scenario); Steps(bot, motor, 500);
                    Require(bot.Distance <= bot.attackEnterDistance + 0.15f && probe.Count > 0, scenario + " must approach and request attack: " + bot.Distance);
                    report.Add(scenario + ": PASS");
                }
                runner.Select(BotScenarioRunner.Scenario.Front);
                SetTarget(bot, runner.testTarget, 1.5f, 0);
                Steps(bot, motor, 5); Require(probe.Count > 0, "Front in range requests attack");
                for (int i = 0; i < 300; i++)
                {
                    SetTarget(bot, runner.testTarget, bot.attackEnterDistance + Mathf.Sin(i * 0.1f) * 0.08f, 0);
                    Steps(bot, motor, 1); Require(bot.Command.x == 0, "Boundary band must hold its stance");
                    if (bot.Distance > bot.attackEnterDistance) Require(!bot.AttackRequested, "Outside range must not request attack");
                }
                Require(bot.RecoveryCount == 0, "Attack hold must not recover");
                foreach (float angle in new[] { 90f, 180f })
                {
                    SetTarget(bot, runner.testTarget, 1.5f, angle); Steps(bot, motor, 1);
                    Require(!bot.AttackRequested, "Side/rear must not attack");
                }
                SetTarget(bot, runner.testTarget, 4f, 0); Steps(bot, motor, 1);
                Require(bot.Command.x > 0, "Retreating target resumes follow");
                bot.Target = null; Steps(bot, motor, 1);
                Require(bot.Command == Vector2.zero && !bot.AttackRequested && motor.LastCommand == Vector2.zero, "Lost target clears motion");
                bot.Target = runner.testTarget; Steps(bot, motor, 2); Require(bot.Command.x > 0, "Restored target resumes follow");
                runner.testTarget.gameObject.SetActive(false); Steps(bot, motor, 1);
                Require(bot.State == RobotBotController.Decision.Idle, "Inactive target must stop");
                runner.testTarget.gameObject.SetActive(true);
                var temporaryTarget = new GameObject("Temporary target loss check");
                temporaryTarget.transform.position = runner.testTarget.position;
                bot.Target = temporaryTarget.transform; Steps(bot, motor, 1);
                UnityEngine.Object.DestroyImmediate(temporaryTarget); Steps(bot, motor, 1);
                Require(bot.Command == Vector2.zero && !bot.AttackRequested, "Destroyed target clears commands");
                bot.Target = runner.testTarget;
                report.Add("Range hysteresis, attack gates, target loss/restore: PASS");
                for (int trial = 0; trial < 3; trial++)
                {
                    runner.Select(BotScenarioRunner.Scenario.Corner);
                    int recoveryStart = bot.RecoveryCount;
                    Steps(bot, motor, 125); // Begins recovery near the L corner.
                    Require(bot.State == RobotBotController.Decision.BackingOut || bot.State == RobotBotController.Decision.EscapeTurn ||
                        bot.State == RobotBotController.Decision.EscapeDrive, "Stop check must interrupt an active recovery");
                    bot.SetMatchRunning(false); Steps(bot, motor, 50);
                    Require(bot.Command == Vector2.zero && !bot.AttackRequested && motor.LastCommand == Vector2.zero, "Stopping during maneuver must clear command");
                    bot.SetMatchRunning(true); Require(bot.State == RobotBotController.Decision.Idle, "Restart resets maneuver");
                    runner.Select(BotScenarioRunner.Scenario.Corner);
                    Steps(bot, motor, 1500);
                    Require(bot.RecoveryCount > recoveryStart && bot.Distance <= bot.attackEnterDistance + 0.15f,
                        "Corner trial " + trial + " failed: " + bot.Distance + " at " + bot.transform.position + " state " + bot.State);
                }
                report.Add("Corner escape x3, stop and restart: PASS");
                runner.Select(BotScenarioRunner.Scenario.AngledWall); Steps(bot, motor, 1500);
                Require(bot.Distance <= bot.attackEnterDistance + 0.15f, "Angled wall must escape");
                report.Add("Angled wall: PASS");
                float savedEnter = bot.attackEnterDistance, savedResume = bot.followResumeDistance;
                try
                {
                    bot.attackEnterDistance = 1.5f; bot.followResumeDistance = 2;
                    runner.Select(BotScenarioRunner.Scenario.Behind); Steps(bot, motor, 500);
                    Require(bot.Distance <= bot.attackEnterDistance && probe.Count > 0, "Tuned range must still follow");
                    runner.Select(BotScenarioRunner.Scenario.Corner); Steps(bot, motor, 1500);
                    Require(bot.Distance <= bot.attackEnterDistance && probe.Count > 0, "Tuned range must still escape corner");
                }
                finally { bot.attackEnterDistance = savedEnter; bot.followResumeDistance = savedResume; }
                report.Add("Range tuning 1.50/2.00m follow + corner: PASS");
                runner.Select(BotScenarioRunner.Scenario.Moving);
                float maxDistance = 0, maxStationarySeconds = 0, stationary = 0;
                int attacks = probe.Count; Vector3 anchor = motor.Body.position;
                for (int i = 0; i < 27000; i++) // Three 180-second matches.
                {
                    float time = i * Dt;
                    runner.testTarget.position = new Vector3(Mathf.Sin(time * 0.45f) * 4, 0.5f, Mathf.Sin(time * 0.7f) * 3);
                    Steps(bot, motor, 1);
                    maxDistance = Mathf.Max(maxDistance, bot.Distance);
                    if (bot.State != RobotBotController.Decision.Attacking && Vector3.Distance(anchor, motor.Body.position) < 0.12f) stationary += Dt;
                    else { stationary = 0; anchor = motor.Body.position; }
                    maxStationarySeconds = Mathf.Max(maxStationarySeconds, stationary);
                    Require(!bot.AttackRequested || bot.Distance <= bot.attackEnterDistance && Mathf.Abs(bot.TargetAngle) <= bot.attackAngle, "Soak attack gate");
                }
                Require(probe.Count > attacks && maxStationarySeconds < 8, "Soak must follow and attack without prolonged stalls: " + maxStationarySeconds);
                report.Add($"Moving target 540 simulated seconds: PASS; max distance {maxDistance:0.00}m; longest non-attack stall {maxStationarySeconds:0.00}s; requests {probe.Count}");
                return string.Join("\n", report);
            }
            finally
            {
                Physics.simulationMode = mode;
                motor.Body.interpolation = interpolation;
                runner.Select(BotScenarioRunner.Scenario.Front);
                runner.enabled = runnerEnabled; bot.enabled = botEnabled; motor.enabled = motorEnabled;
            }
        }
        private static void SetTarget(RobotBotController bot, Transform target, float distance, float angle)
        {
            target.position = bot.GetComponent<Rigidbody>().position + Quaternion.Euler(0, angle, 0) * (bot.GetComponent<Rigidbody>().rotation * Vector3.forward) * distance;
        }
        private static void Steps(RobotBotController bot, RobotDriveMotor motor, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Physics.SyncTransforms(); bot.Tick(Dt); motor.Step(Dt); Physics.Simulate(Dt);
            }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
