namespace RobotSavaslari.Combat
{
    /// <summary>
    /// Tek bir saldirinin hasar hesabi icin gereken girdileri.
    /// Fizik veya sahne bilgisi tasimaz; boylece hesap Unity disinda da test edilebilir.
    /// </summary>
    public readonly struct AttackData
    {
        public readonly AttackType Type;

        /// <summary>Saldiranin hedefe dogru hizi (m/s). Uzaklasan veya yana kayan hareket sayilmaz.</summary>
        public readonly float ImpactSpeed;

        /// <summary>Saldiran robotun agirligi (Rigidbody kutlesi, kg).</summary>
        public readonly float AttackerMass;

        /// <summary>Hedef robotun agirligi (Rigidbody kutlesi, kg).</summary>
        public readonly float DefenderMass;

        public AttackData(AttackType type, float impactSpeed, float attackerMass, float defenderMass)
        {
            Type = type;
            ImpactSpeed = impactSpeed;
            AttackerMass = attackerMass;
            DefenderMass = defenderMass;
        }

        public static AttackData Ram(float impactSpeed, float attackerMass, float defenderMass)
        {
            return new AttackData(AttackType.Ram, impactSpeed, attackerMass, defenderMass);
        }
    }
}
