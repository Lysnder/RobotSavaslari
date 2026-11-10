using UnityEngine;

namespace RobotSavaslari.Driving
{
    /// <summary>
    /// Deneme sahnesi için basit takip kamerası. Hedefin dönüşünü izlemez,
    /// yalnızca konumunu izler; böylece robot dönerken ekran dönmez ve ileri,
    /// geri, sağ, sol ayrımı gözle takip edilebilir.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Driving/Deneme Kamerasi")]
    public class TestCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -14f);

        [Tooltip("0 = anında takip. Büyüdükçe kamera yumuşar.")]
        [SerializeField, Min(0f)] private float smoothTime = 0.15f;

        private Vector3 velocity;

        /// <summary>Takip edilen nesne; sahnede sonradan değiştirilebilir.</summary>
        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desired = target.position + offset;

            transform.position = smoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime)
                : desired;

            transform.LookAt(target.position + Vector3.up * 0.5f);
        }
    }
}
