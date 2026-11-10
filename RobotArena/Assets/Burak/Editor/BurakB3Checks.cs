using System;
using UnityEditor;
using UnityEngine;

namespace RobotSavaslari.Burak.Editor
{
    public static class BurakB3Checks
    {
        public static string Status { get; private set; } = "Not run";
        private static BurakMatchController match;
        private static int cycle, events, previousMatch;
        private static double deadline;

        public static string Begin()
        {
            Require(Application.isPlaying, "Play mode required");
            Require(Status != "Running", "Already running");
            match = UnityEngine.Object.FindFirstObjectByType<BurakMatchController>();
            Require(match != null && match.State == BurakMatchController.MatchState.Ready, "Start Ready");
            cycle = events = 0;
            Status = "Running";
            match.ResultDetermined += CountResult;
            Require(match.StartMatch(), "Initial match starts");
            BeginRestart();
            deadline = EditorApplication.timeSinceStartup + 30;
            EditorApplication.update += Tick;
            return Status;
        }

        private static void CountResult(BurakMatchController.MatchOutcome _) { events++; }

        private static void BeginRestart()
        {
            var player = GameObject.Find("Mavi Robot");
            var opponent = GameObject.Find("Kirmizi Robot");
            match.DirtyDemoState();
            Require(match.DemoAttackCooldown > 0 && match.DemoBotTarget == null, "Dirty demo state");
            player.transform.position += Vector3.forward;
            opponent.transform.position -= Vector3.forward;
            match.Defeat(cycle % 2 == 1);
            Require(match.Outcome == (cycle % 2 == 1 ? BurakMatchController.MatchOutcome.OpponentVictory : BurakMatchController.MatchOutcome.PlayerVictory), "Alternating winners");
            Require(match.ResultCount == 1 && events == cycle + 1, "Single result event");
            previousMatch = match.MatchCount;
            Require(match.RestartMatch(), "Same restart method accepts both outcomes");
            for (int i = 0; i < 25; i++) Require(!match.RestartMatch(), "Rapid clicks rejected");
            match.Prepare();
            Require(match.IsRestarting && !match.StartMatch() && !match.PlayerAllowed && !match.OpponentAllowed, "Loading locked");
        }

        private static void Tick()
        {
            try
            {
                Require(Application.isPlaying && match != null, "Play session remains active");
                Require(EditorApplication.timeSinceStartup < deadline, "Restart timeout");
                if (match.IsRestarting) return;
                Require(match.State == BurakMatchController.MatchState.Playing && match.MatchCount == previousMatch + 1, "Exactly one new Playing match");
                Require(match.Outcome == BurakMatchController.MatchOutcome.None && match.ResultCount == 0 && match.ResultText == "", "No previous result");
                Require(match.PlayerAllowed && match.OpponentAllowed, "Both commands open");
                foreach (var robot in UnityEngine.Object.FindObjectsByType<RobotPrototypeController>())
                {
                    var spawn = GameObject.Find(robot.name == "Mavi Robot" ? "Burak Player Spawn" : "Burak Opponent Spawn").transform;
                    Require(Vector3.Distance(robot.transform.position, spawn.position) < .15f, "Spawn restored");
                    var health = robot.GetComponent<RobotHealth>();
                    Require(health.CurrentHealth == health.MaxHealth, "Fresh health");
                    var body = robot.GetComponent<Rigidbody>();
                    Require(body.linearVelocity.magnitude < .5f, "Old motion cleared");
                }
                Require(match.DemoAttackCooldown == 0 && match.DemoBotTarget == GameObject.Find("Mavi Robot"), "Fresh cooldown and target");
                Require(!match.NotifyDefeat(GameObject.Find("Mavi Robot"), previousMatch), "Old match notification rejected");
                Require(UnityEngine.Object.FindObjectsByType<RobotPrototypeController>().Length == 2, "Two robots only");
                cycle++;
                if (cycle < 3) { BeginRestart(); return; }
                match.Defeat(false);
                match.Defeat(false);
                match.Defeat(true);
                Require(match.ResultCount == 1 && events == 4 && match.Outcome == BurakMatchController.MatchOutcome.PlayerVictory, "One defeat yields one result after three restarts");
                Status = "PASS: three clean restarts, both outcomes, 25 rapid clicks each, fresh health/cooldown/target/spawns/result, single events, stale notice rejected";
                Cleanup();
                Debug.Log(Status);
            }
            catch (Exception ex)
            {
                Status = "FAIL: " + ex.Message;
                Cleanup();
                Debug.LogError(Status);
            }
        }

        private static void Cleanup()
        {
            EditorApplication.update -= Tick;
            if (match != null) match.ResultDetermined -= CountResult;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("B3: " + message);
        }
    }
}
