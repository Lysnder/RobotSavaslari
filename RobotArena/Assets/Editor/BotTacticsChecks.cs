using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RobotSavaslari.Editor
{
    // Uses Arena's real colliders and both shared motors. Combat is stopped to isolate intent from damage.
    public static class BotTacticsChecks
    {
        public static string RunCombatInPlayMode()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Arena Play Mode first.");
            var match = UnityEngine.Object.FindFirstObjectByType<ArenaMatchController>();
            if (match == null) throw new InvalidOperationException("Arena is required.");
            match.RestartMatch();
            var bot = UnityEngine.Object.FindFirstObjectByType<RobotBotController>();
            var opponent = bot.GetComponent<RobotPrototypeController>();
            var player = bot.Target.GetComponent<RobotPrototypeController>();
            var motor = bot.GetComponent<RobotDriveMotor>(); var playerMotor = player.GetComponent<RobotDriveMotor>();
            var update = typeof(RobotPrototypeController).GetMethod("FixedUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var weapon = typeof(RobotPrototypeController).GetProperty("WeaponActive");
            var nextHit = typeof(RobotPrototypeController).GetField("_nextHitTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tractionUntil = typeof(RobotDriveMotor).GetField("_knockbackUntil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var simulation = Physics.simulationMode;
            var interpolation = motor.Body.interpolation; var playerInterpolation = playerMotor.Body.interpolation;
            var lines = new List<string>();
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                motor.Body.interpolation = playerMotor.Body.interpolation = RigidbodyInterpolation.None;
                foreach (bool spinningArmed in new[] { false, true })
                {
                    match.RestartMatch(); opponent.enabled = player.enabled = false;
                    motor.enabled = playerMotor.enabled = false; bot.enabled = true; bot.SetMatchRunning(true);
                    int before = bot.RepositionCount, attacks = 0;
                    // Collision cooldowns and recoil traction use Time.time. Advance their timers explicitly
                    // because Physics.Simulate advances physics, not the player-loop clock.
                    for (int i = 0; i < 3000 && !match.MatchFinished; i++)
                    {
                        foreach (var robot in new[] { opponent, player })
                        {
                            nextHit.SetValue(robot, (float)nextHit.GetValue(robot) - 0.02f);
                            var driveMotor = robot.GetComponent<RobotDriveMotor>();
                            tractionUntil.SetValue(driveMotor, (float)tractionUntil.GetValue(driveMotor) - 0.02f);
                        }
                        Physics.SyncTransforms(); bot.Tick(0.02f); update.Invoke(opponent, null);
                        weapon.SetValue(player, spinningArmed && player.GetComponent<RobotDamageVisuals>().WeaponOperational);
                        playerMotor.Submit(spinningArmed ? 0.4f : 0, spinningArmed ? 1 : 0);
                        motor.Step(0.02f); playerMotor.Step(0.02f); Physics.Simulate(0.02f);
                        opponent.ConstrainToArena(); player.ConstrainToArena();
                        if (bot.AttackRequested) attacks++;
                    }
                    float botHealth = opponent.GetComponent<RobotHealth>().CurrentHealth;
                    float playerHealth = player.GetComponent<RobotHealth>().CurrentHealth;
                    Require(attacks > 0 && bot.RepositionCount > before && playerHealth < 100, "Bot must still reach and damage the real player");
                    if (spinningArmed) Require(botHealth < 100, "Armed spinning player must have counterattack opportunities");
                    lines.Add($"Real combat {(spinningArmed ? "armed driving spin" : "stationary unarmed player")} 60s maximum: PASS; bot HP {botHealth:0.0}; player HP {playerHealth:0.0}; reposition {bot.RepositionCount - before}");
                }
                return string.Join("\n", lines);
            }
            finally
            {
                Physics.simulationMode = simulation;
                motor.Body.interpolation = interpolation; playerMotor.Body.interpolation = playerInterpolation;
                opponent.enabled = player.enabled = motor.enabled = playerMotor.enabled = bot.enabled = true;
                match.RestartMatch();
            }
        }
        [MenuItem("Robot Savaslari/Bot/Run Arena Tactics Checks (Play Mode)")]
        public static void RunFromMenu() { Debug.Log(RunInPlayMode()); }

        public static string RunInPlayMode()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Arena Play Mode first.");
            var match = UnityEngine.Object.FindFirstObjectByType<ArenaMatchController>();
            if (match == null) throw new InvalidOperationException("Arena is required.");
            match.RestartMatch();
            var bot = UnityEngine.Object.FindFirstObjectByType<RobotBotController>();
            var opponent = bot.GetComponent<RobotPrototypeController>();
            var player = bot.Target.GetComponent<RobotPrototypeController>();
            var motor = bot.GetComponent<RobotDriveMotor>();
            var playerMotor = player.GetComponent<RobotDriveMotor>();
            bool botEnabled = bot.enabled, motorEnabled = motor.enabled, playerMotorEnabled = playerMotor.enabled;
            bool opponentEnabled = opponent.enabled, playerEnabled = player.enabled;
            SimulationMode simulation = Physics.simulationMode;
            RigidbodyInterpolation botInterpolation = motor.Body.interpolation, playerInterpolation = playerMotor.Body.interpolation;
            var report = new List<string>();
            try
            {
                opponent.StopCombat(); player.StopCombat();
                opponent.enabled = player.enabled = bot.enabled = motor.enabled = playerMotor.enabled = false;
                motor.Body.interpolation = playerMotor.Body.interpolation = RigidbodyInterpolation.None;
                Physics.simulationMode = SimulationMode.Script;
                Require(bot.enableCombatTactics, "Arena must enable combat tactics");
                foreach (float playerDrive in new[] { 0f, 0.55f })
                {
                    Place(motor.Body, new Vector3(-2, 0.82f, 0), 90);
                    Place(playerMotor.Body, new Vector3(0, 0.82f, 0), 0);
                    motor.Clear(true); playerMotor.Clear(true); bot.SetMatchRunning(true);
                    int before = bot.RepositionCount;
                    float turningRun = 0, longestTurn = 0, attackingRun = 0, longestAttack = 0;
                    int retreat = 0, flank = 0, wait = 0, requests = 0;
                    for (int i = 0; i < 1500; i++)
                    {
                        Step(bot, motor, playerMotor, playerDrive, 1, opponent, player);
                        if (bot.State == RobotBotController.Decision.Disengaging) retreat++;
                        if (bot.State == RobotBotController.Decision.Repositioning) flank++;
                        if (bot.State == RobotBotController.Decision.Waiting) wait++;
                        if (bot.AttackRequested) requests++;
                        turningRun = bot.State == RobotBotController.Decision.Turning && bot.Command.x == 0 ? turningRun + 0.02f : 0;
                        attackingRun = bot.AttackRequested ? attackingRun + 0.02f : 0;
                        longestTurn = Mathf.Max(longestTurn, turningRun); longestAttack = Mathf.Max(longestAttack, attackingRun);
                        Require(!bot.AttackRequested || bot.Distance <= bot.attackEnterDistance && Mathf.Abs(bot.TargetAngle) <= bot.attackAngle, "Attack range/front gate");
                        Require(!IsManeuver(bot.State) || !bot.AttackRequested, "No attacks during reposition/opening");
                    }
                    Require(bot.RepositionCount > before && retreat > 0 && flank > 0 && wait > 0, "Spinning opponent must provoke retreat, flank and opening");
                    Require(longestTurn < bot.circleTimeout + 0.5f, "Bot must not endlessly pivot: " + longestTurn);
                    Require(requests > 0 && requests < 750 && longestAttack <= bot.attackBurstSeconds + 0.04f, "Attack pressure must leave openings");
                    report.Add($"Player {(playerDrive == 0 ? "pivot" : "driving circle")} 30s: PASS; reposition {bot.RepositionCount - before}; longest pivot {longestTurn:0.00}s; longest attack {longestAttack:0.00}s; attack frames {requests}/1500");
                }

                Place(motor.Body, new Vector3(-1.75f, 0.82f, 0), 90);
                Place(playerMotor.Body, new Vector3(0, 0.82f, 0), 0);
                motor.Clear(true); playerMotor.Clear(true); bot.SetMatchRunning(true);
                var stoppedPhases = new HashSet<RobotBotController.Decision>();
                int attackFrames = 0;
                for (int i = 0; i < 1500; i++)
                {
                    Step(bot, motor, playerMotor, 0, 0, opponent, player);
                    if (bot.AttackRequested) attackFrames++;
                    if (IsManeuver(bot.State) && stoppedPhases.Add(bot.State))
                    {
                        bot.SetMatchRunning(false);
                        for (int j = 0; j < 5; j++) Step(bot, motor, playerMotor, 0, 0, opponent, player);
                        Require(bot.State == RobotBotController.Decision.Idle && bot.Command == Vector2.zero &&
                            motor.LastCommand == Vector2.zero && !bot.AttackRequested, "Stop clears tactics and attack");
                        bot.SetMatchRunning(true);
                        Require(bot.State == RobotBotController.Decision.Idle, "Restart cannot restore old tactical phase");
                    }
                }
                Require(stoppedPhases.Count == 3 && attackFrames > 0, "Stationary target: must attack and visit all tactical phases");
                bot.Target = null; Step(bot, motor, playerMotor, 0, 0, opponent, player);
                Require(bot.State == RobotBotController.Decision.Idle && motor.LastCommand == Vector2.zero, "Lost target clears tactical movement");
                bot.Target = player.transform; bot.SetMatchRunning(true);
                report.Add("Stationary target: attack / retreat / flank / wait, stop in all 3 phases, restart and target loss: PASS");
                return string.Join("\n", report);
            }
            finally
            {
                Physics.simulationMode = simulation;
                motor.Body.interpolation = botInterpolation; playerMotor.Body.interpolation = playerInterpolation;
                opponent.enabled = opponentEnabled; player.enabled = playerEnabled;
                motor.enabled = motorEnabled; playerMotor.enabled = playerMotorEnabled; bot.enabled = botEnabled;
                match.RestartMatch();
            }
        }
        private static bool IsManeuver(RobotBotController.Decision state)
        {
            return state == RobotBotController.Decision.Disengaging || state == RobotBotController.Decision.Repositioning || state == RobotBotController.Decision.Waiting;
        }
        private static void Place(Rigidbody body, Vector3 position, float yaw)
        {
            body.position = position; body.rotation = Quaternion.Euler(0, yaw, 0);
            body.transform.SetPositionAndRotation(position, body.rotation);
        }
        private static void Step(RobotBotController bot, RobotDriveMotor motor, RobotDriveMotor playerMotor,
            float drive, float steer, RobotPrototypeController opponent, RobotPrototypeController player)
        {
            Physics.SyncTransforms(); bot.Tick(0.02f); playerMotor.Submit(drive, steer);
            motor.Step(0.02f); playerMotor.Step(0.02f); Physics.Simulate(0.02f);
            opponent.ConstrainToArena(); player.ConstrainToArena();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
