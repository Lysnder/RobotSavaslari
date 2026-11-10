using UnityEngine;

namespace RobotSavaslari.Driving
{
    /// <summary>
    /// Son çarpışmayı kaydeder: giriş hızı, duvara göre açı ve çarpışmadan
    /// hemen sonra gövdede kalan hız. Kontrol arızasıyla "çarpışmanın bıraktığı
    /// fiziksel hareket" ayrımı bu kayıtla yapılır.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Driving/Carpisma Raporu")]
    [RequireComponent(typeof(Rigidbody))]
    public class CollisionReporter : MonoBehaviour
    {
        [Tooltip("Bu hızın altındaki temaslar sürtünme sayılır, rapora girmez.")]
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 0.4f;

        private Rigidbody body;

        /// <summary>Hiç çarpışma kaydedildi mi?</summary>
        public bool HasReport { get; private set; }

        /// <summary>Çarpışma anındaki giriş hızı (m/s).</summary>
        public float ImpactSpeed { get; private set; }

        /// <summary>Giriş yönüyle duvar normali arasındaki açı (0 = tam önden).</summary>
        public float ImpactAngle { get; private set; }

        /// <summary>Çarpışmadan sonra gövdede kalan hız (m/s).</summary>
        public float ResidualSpeed { get; private set; }

        /// <summary>Çarpışmanın üzerinden geçen süre (s).</summary>
        public float TimeSinceImpact => HasReport ? Time.time - impactTime : -1f;

        /// <summary>Çarpılan nesnenin adı.</summary>
        public string ImpactTarget { get; private set; } = string.Empty;

        private float impactTime;
        private bool residualPending;

        public void Clear()
        {
            HasReport = false;
            ImpactSpeed = 0f;
            ImpactAngle = 0f;
            ResidualSpeed = 0f;
            ImpactTarget = string.Empty;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Gövdenin o anki hızı çarpışma çözüldükten sonraki değeri verir;
            // giriş hızı için Unity'nin sakladığı göreli hız okunur.
            Vector3 approach = collision.relativeVelocity;
            approach.y = 0f;

            float speed = approach.magnitude;
            if (speed < minimumImpactSpeed)
            {
                return;
            }

            Vector3 normal = collision.GetContact(0).normal;
            normal.y = 0f;

            HasReport = true;
            impactTime = Time.time;
            ImpactSpeed = speed;
            ImpactTarget = collision.gameObject.name;

            // Normal duvardan robota doğru bakar; tam önden girişte robotun
            // burnu onun tersine bakar. 0 derece = tam önden, 90'a yaklaştıkça
            // duvarı yalayarak.
            Vector3 heading = transform.forward;
            heading.y = 0f;
            ImpactAngle = normal.sqrMagnitude > 1e-6f && heading.sqrMagnitude > 1e-6f
                ? Vector3.Angle(heading, -normal)
                : 0f;

            // Çarpışma çözüldükten sonraki hız bir sonraki fizik adımında okunur.
            residualPending = true;
        }

        private void FixedUpdate()
        {
            if (!residualPending)
            {
                return;
            }

            residualPending = false;

            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;
            ResidualSpeed = velocity.magnitude;
        }
    }
}
