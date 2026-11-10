using System;
using System.Linq;
using UnityEngine;

namespace RobotSavaslari.Editor
{
    // Run against the real imported robots in Play Mode; does not modify assets.
    public static class RobotDamageChecks
    {
        public static string RunInPlayMode()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var robots = UnityEngine.Object.FindObjectsByType<RobotDamageVisuals>();
            Require(robots.Length == 2, "Both arena robots must be present.");
            foreach (var robot in robots) robot.GetComponent<RobotPrototypeController>().StopCombat();
            int assertions = 0;
            foreach (var robot in robots)
            {
                var health = robot.GetComponent<RobotHealth>();
                robot.Initialize();
                try
                {
                    health.Restore();
                    Require(robot.Parts.Count == 10, robot.name + ": expected body, four wheels, weapon and four armor regions.");
                    Require(robot.Parts.Sum(p => p.Mesh.triangles.Length / 3) == robot.OriginalTriangleCount, "Partition must preserve all triangles.");
                    Require(robot.Parts.Count(p => p.Kind == RobotDamageVisuals.PartKind.Wheel) == 4, "Four wheel regions are required.");
                    Require(robot.Parts.All(p => p.Transform.gameObject.activeSelf), "Healthy parts must be visible.");
                    assertions += 4;

                    var wheel = robot.Parts.First(p => p.Kind == RobotDamageVisuals.PartKind.Wheel);
                    Hit(health, wheel, wheel.Threshold * 0.4f);
                    Require(!wheel.Detached && wheel.Damage > 0 && wheel.Wear.enabled, "A partial hit should add wear without detaching.");
                    Hit(health, wheel, wheel.Threshold * 0.6f + 1);
                    Require(wheel.Detached && !wheel.Transform.gameObject.activeSelf, "Repeated regional damage must detach its wheel.");
                    Require(robot.LostWheels == 1 && robot.Mobility < 1 && robot.Turning < 1, "Wheel loss must reduce mobility.");
                    Require(robot.DetachedCount == 1, "A single exhausted region must detach once.");
                    var debris = GameObject.Find(robot.name + " Debris " + wheel.Name);
                    Require(debris != null && debris.transform.parent == null && debris.GetComponent<Rigidbody>() != null,
                        "Detached mesh must be independent physics debris.");
                    assertions += 5;

                    health.Restore();
                    Require(robot.DetachedCount == 0 && robot.Mobility == 1 && robot.SteeringPull == 0 && robot.Parts.All(p => !p.Detached && p.Damage == 0), "Restore must reset damage and handling.");
                    Require(robot.Parts.All(p => p.Transform.gameObject.activeSelf && !p.Wear.enabled), "Restore must restore geometry and remove wear.");
                    assertions += 2;

                    var armor = robot.Parts.First(p => p.Kind == RobotDamageVisuals.PartKind.Armor);
                    Hit(health, armor, armor.Threshold + 1);
                    Require(armor.Detached && robot.LostWheels == 0 && robot.WeaponOperational, "Armor loss must not disable wheels or weapon.");
                    health.Restore();
                    var weapon = robot.Parts.First(p => p.Kind == RobotDamageVisuals.PartKind.Weapon);
                    Hit(health, weapon, weapon.Threshold + 1);
                    Require(weapon.Detached && !robot.WeaponOperational, "Weapon loss must disable attacks.");
                    assertions += 2;

                    health.Restore();
                    health.TakeDamage(-10);
                    health.TakeDamage(float.NaN);
                    health.TakeDamage(float.PositiveInfinity);
                    Require(health.CurrentHealth == health.MaxHealth, "Invalid damage must not change health.");
                    health.TakeDamage(1000);
                    int detached = robot.DetachedCount;
                    health.TakeDamage(10);
                    Require(health.CurrentHealth == 0 && robot.DetachedCount == detached, "Defeat must clamp health and reject further damage.");
                    assertions += 2;
                }
                finally { health.Restore(); }
            }
            return "PASS: " + assertions + " checks on both imported robots (partition, wear, regional detachment, physics debris, mobility, weapon loss, reset, invalid damage and defeat).";
        }

        private static void Hit(RobotHealth health, RobotDamageVisuals.Part part, float amount)
        {
            health.TakeDamage(amount, part.Transform.position, Vector3.up, Vector3.right * 2, true);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
