using System;

namespace RobotSavaslari.Combat
{
    /// <summary>
    /// Carpma sonrasi geri tepme hizinin hesabi. Saf C#: Unity sahnesine veya fizige bagli degildir.
    /// </summary>
    public static class KnockbackCalculator
    {
        /// <summary>
        /// Geri tepme hizi (m/s) = carpma hizi x oran x (karsi robot agirligi / kendi agirligi), en fazla maxSpeed.
        /// Hafif robot agir robottan daha cok savrulur.
        /// Gecersiz veya pozitif olmayan girdilerde 0 doner.
        /// </summary>
        public static float Calculate(float impactSpeed, float ownMass, float otherMass, float speedRatio, float maxSpeed)
        {
            if (!IsPositive(impactSpeed) || !IsPositive(ownMass) || !IsPositive(otherMass) ||
                !IsPositive(speedRatio) || !IsPositive(maxSpeed))
                return 0f;

            return Math.Min(impactSpeed * speedRatio * (otherMass / ownMass), maxSpeed);
        }

        private static bool IsPositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
