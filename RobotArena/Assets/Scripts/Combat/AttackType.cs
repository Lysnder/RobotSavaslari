namespace RobotSavaslari.Combat
{
    /// <summary>
    /// Robotun rakibine hasar verebildigi saldiri hareketleri.
    /// Silahlar eklendiginde yeni tipler buraya eklenir ve
    /// <see cref="DamageCalculator"/> icinde kendi formulleriyle hesaplanir.
    /// </summary>
    public enum AttackType
    {
        /// <summary>Govdeyle carpma. Hasar = hiz x (saldiran agirligi / hedef agirligi).</summary>
        Ram
    }
}
