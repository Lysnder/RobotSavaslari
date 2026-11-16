using UnityEngine;

namespace RobotSavaslari
{
    public enum BotPersonality { Aggressive, Counter }
    public enum BotDifficulty { Easy, Hard }

    // Decision parameters only. Motor, health and weapon profiles remain authoritative.
    public readonly struct BotDecisionProfile
    {
        public readonly float ReactionMultiplier, AimErrorDegrees, PositionError, MissedDecisionChance;
        public BotDecisionProfile(BotDifficulty difficulty)
        {
            bool easy = difficulty == BotDifficulty.Easy;
            ReactionMultiplier = easy ? 2.5f : 1f;
            AimErrorDegrees = easy ? 12f : 2f;
            PositionError = easy ? 0.45f : 0.08f;
            MissedDecisionChance = easy ? 0.15f : 0f;
        }
    }

    public readonly struct BotDamageState
    {
        public readonly float HealthFraction, Mobility, Turning;
        public readonly bool WeaponOperational;
        public readonly int LostWheels;
        public bool LimitedMovement => Mobility < 0.65f || Turning < 0.65f;
        public BotDamageState(RobotHealth health, RobotDamageVisuals damage)
        {
            HealthFraction = health != null ? health.CurrentHealth / Mathf.Max(1f, health.MaxHealth) : 1f;
            WeaponOperational = damage == null || damage.WeaponOperational;
            Mobility = damage != null ? damage.Mobility : 1f;
            Turning = damage != null ? damage.Turning : 1f;
            LostWheels = damage != null ? damage.LostWheels : 0;
        }
    }

    public readonly struct BotThreatObservation
    {
        public readonly bool Incoming;
        public readonly float ClosingSpeed, ClosestDistance, TimeToClosest;
        public BotThreatObservation(Vector3 separation, Vector3 ownVelocity, Vector3 targetVelocity,
            Vector3 targetForward, float horizon = 0.85f, float contactRadius = 1.35f)
        {
            separation.y = ownVelocity.y = targetVelocity.y = targetForward.y = 0;
            Vector3 relative = targetVelocity - ownVelocity;
            float distance = separation.magnitude;
            ClosingSpeed = distance > 0.001f ? -Vector3.Dot(relative, separation / distance) : 0;
            TimeToClosest = relative.sqrMagnitude > 0.001f ? -Vector3.Dot(separation, relative) / relative.sqrMagnitude : -1f;
            ClosestDistance = (separation + relative * Mathf.Clamp(TimeToClosest, 0, horizon)).magnitude;
            // A stationary target is not charging just because our own robot is pursuing it.
            float targetApproach = distance > 0.001f ? Vector3.Dot(targetVelocity, -separation / distance) : 0;
            float heading = distance > 0.001f ? Vector3.Dot(targetForward.normalized, -separation / distance) : 0;
            Incoming = distance > 1.05f && ClosingSpeed > 0.8f && targetApproach > 0.7f &&
                heading > 0.35f && TimeToClosest > 0 && TimeToClosest <= horizon && ClosestDistance < contactRadius;
        }
    }
}
