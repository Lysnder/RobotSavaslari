using System;
using UnityEngine;

namespace RobotSavaslari.Training
{
    public enum DrivingExerciseKind { Slalom, ControlledStop, PrecisionTurn, ReverseParking }

    [Serializable]
    public sealed class DrivingExerciseDefinition
    {
        public DrivingExerciseKind kind;
        public string exerciseId;
        [Min(1)] public int rulesRevision = 1;
        public string title;
        [TextArea] public string instruction;
        public Vector3 start;
        public float startYaw;
        public TimeTrialCheckpoint[] gates;
        public float targetYaw;
        [Min(0f)] public float yawTolerance;
        [Min(0f)] public float stopSpeed = .15f;
        [Min(0f)] public float dwellSeconds = 1f;

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(exerciseId) || exerciseId.Contains("/") || rulesRevision < 1 ||
                !Enum.IsDefined(typeof(DrivingExerciseKind), kind) || !Finite(start) || !Finite(startYaw) ||
                !Finite(targetYaw) || !Finite(stopSpeed) || stopSpeed < 0f || !Finite(dwellSeconds) ||
                dwellSeconds <= 0f || !Finite(yawTolerance) || yawTolerance < 0f || yawTolerance > 180f ||
                gates == null || (kind == DrivingExerciseKind.Slalom ? gates.Length < 2 : gates.Length != 1))
                return false;

            for (int index = 0; index < gates.Length; index++)
            {
                var gate = gates[index];
                if (!Finite(gate.position) || !Finite(gate.radius) || gate.radius <= 0f)
                    return false;
                if (kind == DrivingExerciseKind.Slalom &&
                    gate.position.z <= (index == 0 ? start.z : gates[index - 1].position.z))
                    return false;
            }

            return true;
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }

    public sealed class DrivingExerciseCatalog : ScriptableObject
    {
        public DrivingExerciseDefinition[] exercises;
    }
}
