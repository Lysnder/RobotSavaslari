using System;
using UnityEngine;

namespace RobotSavaslari.Match.Editor
{
    public static class MatchFlowChecks
    {
        public static string Run()
        {
            Require(Application.isPlaying, "Play mode required");
            var match = UnityEngine.Object.FindFirstObjectByType<MatchController>();
            Require(match != null, "B1 harness exists");
            var robots = UnityEngine.Object.FindObjectsByType<RobotPrototypeController>();
            Require(robots.Length == 2, "Exactly two robots");
            Require(UnityEngine.Object.FindObjectsByType<ArenaMatchController>().Length == 0, "No shared match controller");
            Require(match.State == MatchController.MatchState.Ready, "Initial Ready");
            Require(!match.PlayerAllowed && !match.OpponentAllowed, "Ready locks both");
            Require(!match.Probe(true) && !match.Probe(false), "Ready rejects input");
            foreach (var robot in robots)
            {
                var spawn = GameObject.Find(robot.name == "Mavi Robot" ? "Player Spawn" : "Opponent Spawn").transform;
                Require(Vector2.Distance(new Vector2(robot.transform.position.x, robot.transform.position.z), new Vector2(spawn.position.x, spawn.position.z)) < .05f, "Own spawn XZ");
                var other = robots[0] == robot ? robots[1] : robots[0];
                Require(Vector3.Dot(robot.transform.forward, (other.transform.position - robot.transform.position).normalized) > .95f, "Facing opponent");
            }
            Physics.SyncTransforms();
            foreach (var a in robots[0].GetComponentsInChildren<Collider>())
                foreach (var b in robots[1].GetComponentsInChildren<Collider>())
                    Require(!Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out _), "No robot penetration");
            Require(match.StartMatch(), "First Start accepted");
            for (int i = 0; i < 25; i++) Require(!match.StartMatch(), "Repeated Start rejected");
            Require(match.MatchCount == 1 && match.PlayerAllowed && match.OpponentAllowed, "One match; Playing unlocks both");
            Require(match.Probe(true) && match.Probe(false), "Playing accepts input");
            match.Prepare();
            Require(match.State == MatchController.MatchState.Playing, "Prepare cannot interrupt Playing");
            match.Defeat(false);
            Require(match.State == MatchController.MatchState.Result && !match.PlayerAllowed && !match.OpponentAllowed, "Result locks both");
            Require(!match.Probe(true) && !match.Probe(false) && !match.StartMatch(), "Result rejects input and Start");
            Require(UnityEngine.Object.FindObjectsByType<RobotPrototypeController>().Length == 2, "Still two robots");
            match.Prepare();
            Require(match.State == MatchController.MatchState.Ready, "Return Ready");
            Require(match.StartMatch() && match.MatchCount == 2, "Explicit second match");
            match.Defeat(true);
            Require(match.State == MatchController.MatchState.Result, "Player defeat also ends match");
            match.Prepare();
            return "PASS: spawn, facing, no penetration, Ready/Playing/Result gates, 25 repeated Starts, two robots, both defeats, reset";
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("B1 FAIL: " + message);
        }
    }
}
