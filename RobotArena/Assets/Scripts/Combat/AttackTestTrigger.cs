using UnityEngine;

namespace RobotSavaslari.Combat
{
    /// <summary>
    /// Deneme bileseni: klavye kullanmadan saldiri istegi gonderir.
    /// Bot veya baska bir sistem de ayni sekilde <see cref="MeleeAttack.RequestAttack"/> cagirir.
    /// </summary>
    public sealed class AttackTestTrigger : MonoBehaviour
    {
        [SerializeField] private MeleeAttack attack;
        [Tooltip("Acikken belirtilen aralikla kendiliginden saldiri istegi gonderir.")]
        [SerializeField] private bool autoFire;
        [SerializeField, Min(0.05f)] private float interval = 0.4f;

        private float _nextFireTime;

        public MeleeAttack Attack
        {
            get => attack;
            set => attack = value;
        }

        public bool AutoFire
        {
            get => autoFire;
            set => autoFire = value;
        }

        public AttackOutcome LastOutcome { get; private set; }

        public AttackOutcome Fire()
        {
            if (attack == null) attack = GetComponent<MeleeAttack>();
            LastOutcome = attack != null ? attack.RequestAttack() : AttackOutcome.None;
            return LastOutcome;
        }

        private void Update()
        {
            if (!autoFire || Time.time < _nextFireTime) return;
            _nextFireTime = Time.time + interval;
            Fire();
        }
    }
}
