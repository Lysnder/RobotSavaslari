using System.Collections.Generic;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// A read-only summary of one robot's damage at the moment it was captured: health, weapon state
    /// and every part with its damage ratio and whether it came off. Capturing only reads health and
    /// the damage system's part list; it never changes them.
    /// </summary>
    public sealed class RobotInspectionReport
    {
        public readonly struct PartState
        {
            public readonly string Name;
            public readonly RobotDamageVisuals.PartKind Kind;
            /// <summary>Damage relative to the part's detach threshold, 0..1.</summary>
            public readonly float DamageRatio;
            public readonly bool Detached;

            public PartState(string name, RobotDamageVisuals.PartKind kind, float damageRatio, bool detached)
            {
                Name = name;
                Kind = kind;
                DamageRatio = float.IsNaN(damageRatio) ? 0f : Mathf.Clamp01(damageRatio);
                Detached = detached;
            }

            public string DisplayName => DescribePart(Name);
            public string Condition => DescribeCondition(DamageRatio, Detached);
        }

        private readonly List<PartState> _parts;

        public RobotInspectionReport(string robotName, float healthRatio, bool weaponOperational, IEnumerable<PartState> parts)
        {
            RobotName = robotName ?? string.Empty;
            HealthRatio = float.IsNaN(healthRatio) ? 0f : Mathf.Clamp01(healthRatio);
            WeaponOperational = weaponOperational;
            _parts = parts != null ? new List<PartState>(parts) : new List<PartState>();
            foreach (PartState part in _parts)
                if (part.Detached) DetachedCount++;
        }

        public string RobotName { get; }
        public float HealthRatio { get; }
        public bool WeaponOperational { get; }
        public int DetachedCount { get; }
        public IReadOnlyList<PartState> Parts => _parts;
        /// <summary>False for robots without a partitioned mesh; only health is known then.</summary>
        public bool HasPartData => _parts.Count > 0;

        public bool IsUndamaged
        {
            get
            {
                if (HealthRatio < 1f || DetachedCount > 0) return false;
                foreach (PartState part in _parts)
                    if (part.DamageRatio > 0f) return false;
                return true;
            }
        }

        public static RobotInspectionReport Capture(Component robot)
        {
            if (robot == null) return new RobotInspectionReport(string.Empty, 0f, true, null);
            var health = robot.GetComponent<RobotHealth>();
            var visuals = robot.GetComponent<RobotDamageVisuals>();
            float healthRatio = health != null && health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 1f;
            var parts = new List<PartState>();
            if (visuals != null)
                foreach (RobotDamageVisuals.Part part in visuals.Parts)
                    parts.Add(new PartState(part.Name, part.Kind,
                        part.Threshold > 0f ? part.Damage / part.Threshold : 0f, part.Detached));
            return new RobotInspectionReport(robot.name, healthRatio, visuals == null || visuals.WeaponOperational, parts);
        }

        /// <summary>Player-facing Turkish name for a damage system part name; unknown names are returned as is.</summary>
        public static string DescribePart(string partName)
        {
            switch (partName)
            {
                case "Body": return "Gövde";
                case "Spinner": return "Döner silah";
                case "Lifter": return "Kaldırıcı silah";
            }
            if (string.IsNullOrEmpty(partName)) return string.Empty;
            string kind = partName.StartsWith("Wheel_") ? "tekerlek" : partName.StartsWith("Armor_") ? "zırh" : null;
            if (kind == null || partName.Length != partName.IndexOf('_') + 3) return partName;
            string corner = partName.Substring(partName.Length - 2);
            string front = corner[0] == 'F' ? "ön" : corner[0] == 'R' ? "arka" : null;
            string side = corner[1] == 'L' ? "Sol" : corner[1] == 'R' ? "Sağ" : null;
            return front != null && side != null ? side + " " + front + " " + kind : partName;
        }

        public static string DescribeCondition(float damageRatio, bool detached)
        {
            if (detached) return "Koptu";
            int percent = Mathf.RoundToInt(Mathf.Clamp01(damageRatio) * 100f);
            return percent <= 0 ? "Sağlam" : "%" + percent + " hasarlı";
        }
    }
}
