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

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (IsDefeated || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            HealthChanged?.Invoke(this);

            if (IsDefeated)
            {
                Defeated?.Invoke(this);
            }
        }

        public void Restore()
        {
            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(this);
        }
    }
}