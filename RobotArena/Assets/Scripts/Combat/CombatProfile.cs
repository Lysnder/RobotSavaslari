using UnityEngine;

namespace RobotSavaslari.Combat
{
    /// <summary>Kisa menzilli saldirinin ayar profili. Farkli profiller ayni sahnede denenip karsilastirilabilir.</summary>
    [CreateAssetMenu(menuName = "RobotSavaslari/Combat Profile", fileName = "CombatProfile")]
    public sealed class CombatProfile : ScriptableObject
    {
        [Tooltip("Gecerli her isabette hedefe yazilan hasar.")]
        [Min(0f)] public float damage = 25f;
        [Tooltip("Saldiri merkezinden hedef collider yuzeyine olan en buyuk mesafe (m).")]
        [Min(0.05f)] public float range = 1.5f;
        [Tooltip("Saldiri kullanildiktan sonra yeni saldiri icin beklenecek sure (s).")]
        [Min(0f)] public float cooldown = 1f;
        [Tooltip("Saldirinin temas aradigi pencere (s). 0: tek anlik kontrol.")]
        [Min(0f)] public float activeDuration = 0.15f;
        [Tooltip("Robotun onunde saldirinin kapsadigi yay acisi (derece).")]
        [Range(1f, 360f)] public float arcDegrees = 120f;

        /// <summary>Verilen candan sifira inmek icin gereken gecerli vurus sayisi.</summary>
        public int HitsToDefeat(float health)
        {
            return damage > 0f ? Mathf.CeilToInt(health / damage) : int.MaxValue;
        }

        public static CombatProfile CreateRuntime(string name, float damage, float range = 1.5f, float cooldown = 1f,
            float activeDuration = 0.15f, float arcDegrees = 120f)
        {
            var profile = CreateInstance<CombatProfile>();
            profile.name = name;
            profile.damage = damage;
            profile.range = range;
            profile.cooldown = cooldown;
            profile.activeDuration = activeDuration;
            profile.arcDegrees = arcDegrees;
            return profile;
        }
    }
}
