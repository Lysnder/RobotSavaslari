using System;

namespace RobotSavaslari.Combat
{
    /// <summary>
    /// Saldiri hasarlarinin tek hesaplandigi yer. Saf C#: Unity sahnesine veya fizige bagli degildir.
    /// </summary>
    public static class DamageCalculator
    {
        public static float Calculate(in AttackData attack)
        {
            switch (attack.Type)
            {
                case AttackType.Ram:
                    return CalculateRamDamage(attack.ImpactSpeed, attack.AttackerMass, attack.DefenderMass);
                default:
                    throw new ArgumentOutOfRangeException(nameof(attack), attack.Type, "Bilinmeyen saldiri tipi.");
            }
        }

        /// <summary>
        /// Carpma hasari = hiz x (saldiran agirligi / hedef agirligi).
        /// Agir robot hafif robota daha cok, hafif robot agir robota daha az hasar verir.
        /// Gecersiz veya pozitif olmayan girdilerde 0 doner.
        /// </summary>
        public static float CalculateRamDamage(float impactSpeed, float attackerMass, float defenderMass)
        {
            if (!IsPositive(impactSpeed) || !IsPositive(attackerMass) || !IsPositive(defenderMass))
                return 0f;

            return impactSpeed * (attackerMass / defenderMass);
        }

        private static bool IsPositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
