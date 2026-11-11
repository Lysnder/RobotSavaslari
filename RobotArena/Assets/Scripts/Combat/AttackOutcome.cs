namespace RobotSavaslari.Combat
{
    /// <summary>Saldiri isteginin veya tamamlanan saldirinin sonucu.</summary>
    public enum AttackOutcome
    {
        /// <summary>Henuz saldiri yapilmadi.</summary>
        None,
        /// <summary>Istek kabul edildi; saldiri penceresi suruyor.</summary>
        Started,
        /// <summary>Saldiri en az bir rakibe hasar verdi.</summary>
        Hit,
        /// <summary>Saldiri penceresi hic gecerli hedefe degmeden bitti.</summary>
        Miss,
        /// <summary>Bekleme suresi dolmadigi icin istek reddedildi.</summary>
        Cooldown,
        /// <summary>Mac kapali: istek reddedildi ya da suren saldiri kesildi.</summary>
        MatchClosed,
        /// <summary>Saldiran robot yenik oldugu icin istek reddedildi ya da saldiri kesildi.</summary>
        AttackerDefeated
    }

    /// <summary>Son saldiri isteginin ozeti; deneme ekraninda ve olaylarda kullanilir.</summary>
    public readonly struct AttackReport
    {
        public readonly AttackOutcome Outcome;
        public readonly int TargetsHit;
        public readonly float Time;

        public AttackReport(AttackOutcome outcome, int targetsHit, float time)
        {
            Outcome = outcome;
            TargetsHit = targetsHit;
            Time = time;
        }

        public string Describe()
        {
            switch (Outcome)
            {
                case AttackOutcome.Started: return "SALDIRI SURUYOR";
                case AttackOutcome.Hit: return "ISABET (" + TargetsHit + " hedef)";
                case AttackOutcome.Miss: return "ISKA";
                case AttackOutcome.Cooldown: return "BEKLEME - istek reddedildi";
                case AttackOutcome.MatchClosed: return TargetsHit > 0 ? "MAC KAPANDI - saldiri kesildi" : "MAC KAPALI";
                case AttackOutcome.AttackerDefeated: return "SALDIRAN YENIK";
                default: return "-";
            }
        }
    }
}
