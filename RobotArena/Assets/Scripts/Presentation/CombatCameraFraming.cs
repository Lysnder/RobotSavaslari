using UnityEngine;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Pure orbit and framing math for the combat camera. Kept free of scene state so
    /// limits can be verified without loading the arena.
    /// </summary>
    public static class CombatCameraFraming
    {
        /// <summary>Unit vector from the pivot towards the camera for the given orbit angles.</summary>
        public static Vector3 OrbitDirection(float yaw, float pitch)
        {
            return Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
        }

        /// <summary>Rotation that looks from the orbit position back at the pivot.</summary>
        public static Quaternion OrbitRotation(float yaw, float pitch)
        {
            return Quaternion.Euler(pitch, yaw, 0f);
        }

        /// <summary>Wraps an angle into [-180, 180) so accumulated input never grows unbounded.</summary>
        public static float WrapYaw(float yaw)
        {
            return Mathf.Repeat(yaw + 180f, 360f) - 180f;
        }

        /// <summary>Frame-rate independent exponential smoothing weight in [0, 1].</summary>
        public static float SmoothingWeight(float sharpness, float deltaTime)
        {
            if (sharpness <= 0f) return 1f;
            if (deltaTime <= 0f) return 0f;
            return 1f - Mathf.Exp(-sharpness * deltaTime);
        }

        /// <summary>
        /// Maps the player's smoothing setting (0..1) to an exponential sharpness. 0 means no smoothing
        /// (sharpness 0 gives weight 1, an instant follow); larger values follow more softly.
        /// </summary>
        public static float SharpnessFromSmoothing(float smoothing, float softest, float firmest)
        {
            if (float.IsNaN(smoothing) || smoothing <= 0f) return 0f;
            return Mathf.Lerp(firmest, softest, Mathf.Clamp01(smoothing));
        }

        /// <summary>Converts one frame of wheel input into a multiplicative zoom.</summary>
        public static float ApplyZoom(float distance, float wheel, float stepRatio, float minDistance, float maxDistance)
        {
            if (Mathf.Approximately(wheel, 0f)) return Mathf.Clamp(distance, minDistance, maxDistance);
            // Platforms report one notch as 1 or 120; only the direction is used.
            float factor = wheel > 0f ? 1f - stepRatio : 1f / (1f - stepRatio);
            return Mathf.Clamp(distance * factor, minDistance, maxDistance);
        }

        /// <summary>Half diagonal of the near clip plane: the radius the camera must keep from geometry.</summary>
        public static float NearPlaneRadius(float nearClip, float verticalFieldOfView, float aspect)
        {
            float halfHeight = nearClip * Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * Mathf.Max(0.01f, aspect);
            return Mathf.Sqrt(nearClip * nearClip + halfHeight * halfHeight + halfWidth * halfWidth);
        }

        /// <summary>
        /// Distance at which a sphere of the given radius fits inside the view frustum, using the
        /// narrower of the vertical and horizontal fields of view.
        /// </summary>
        public static float FitDistance(float radius, float verticalFieldOfView, float aspect)
        {
            float halfVertical = verticalFieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * Mathf.Max(0.01f, aspect));
            float halfAngle = Mathf.Clamp(Mathf.Min(halfVertical, halfHorizontal), 1f * Mathf.Deg2Rad, 89f * Mathf.Deg2Rad);
            return Mathf.Max(0f, radius) / Mathf.Sin(halfAngle);
        }

        /// <summary>Smallest sphere around both robots' visible bounds.</summary>
        public static void EnclosePair(Bounds first, Bounds second, out Vector3 center, out float radius)
        {
            center = (first.center + second.center) * 0.5f;
            float halfSeparation = Vector3.Distance(first.center, second.center) * 0.5f;
            radius = halfSeparation + Mathf.Max(first.extents.magnitude, second.extents.magnitude);
        }

        /// <summary>
        /// Yaw that looks from the first robot towards the second along the ground. When the robots
        /// are closer than <paramref name="minimumSeparation"/> the direction is undefined, so the
        /// previous yaw is kept instead of reading a near-zero vector.
        /// </summary>
        public static float PairAxisYaw(Vector3 from, Vector3 to, float previousYaw, float minimumSeparation)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            if (flat.sqrMagnitude < minimumSeparation * minimumSeparation) return previousYaw;
            return WrapYaw(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);
        }

        /// <summary>Eases towards the goal yaw but never turns faster than the given rate.</summary>
        public static float RateLimitedYaw(float current, float goal, float smoothingWeight, float maxDegreesPerSecond, float deltaTime)
        {
            float eased = Mathf.LerpAngle(current, goal, Mathf.Clamp01(smoothingWeight));
            float step = Mathf.Max(0f, maxDegreesPerSecond) * Mathf.Max(0f, deltaTime);
            return WrapYaw(Mathf.MoveTowardsAngle(current, eased, step));
        }

        /// <summary>Raises a position so the near plane never dips below the walkable floor.</summary>
        public static Vector3 KeepAboveFloor(Vector3 position, float floorHeight, float clearance)
        {
            float minimum = floorHeight + Mathf.Max(0f, clearance);
            if (position.y < minimum) position.y = minimum;
            return position;
        }
    }
}
