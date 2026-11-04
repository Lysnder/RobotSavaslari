using System;
using UnityEngine;

namespace RobotSavaslari
{
    public sealed class RobotHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsDefeated => CurrentHealth <= 0f;

        public event Action<RobotHealth> HealthChanged;
        public event Action<RobotHealth> Defeated;
        public event Action<RobotHit> Damaged;
        public event Action Restored;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, transform.position + Vector3.up * 0.4f, Vector3.up, Vector3.zero, false);
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 normal, Vector3 impulse, bool weapon)
        {
            if (IsDefeated || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return;
            }

            float applied = Mathf.Min(CurrentHealth, amount);
            CurrentHealth -= applied;
            Damaged?.Invoke(new RobotHit(applied, point, normal, impulse, weapon));
            HealthChanged?.Invoke(this);

            if (IsDefeated)
            {
                Defeated?.Invoke(this);
            }
        }

        public void Restore()
        {
            CurrentHealth = maxHealth;
            Restored?.Invoke();
            HealthChanged?.Invoke(this);
        }
    }
}
