using UnityEngine;

namespace RobotSavaslari.Driving
{
    /// <summary>
    /// Tek bir deneme rotası: adı, başlangıç noktası ve sırayla geçilecek
    /// kapılar. Her rota aynı konum ve yönden başlar, böylece iki ayar seti
    /// aynı koşulda karşılaştırılabilir.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Driving/Rota")]
    public class RouteCourse : MonoBehaviour
    {
        [SerializeField] private string routeName = "Rota";

        [TextArea(1, 3)]
        [SerializeField] private string instructions = "";

        [Tooltip("Robotun doğacağı nokta. Boşsa bu nesnenin kendi konumu kullanılır.")]
        [SerializeField] private Transform startPoint;

        [Tooltip("Sırayla geçilmesi beklenen kapılar; yalnızca gözle takip içindir.")]
        [SerializeField] private Transform[] gates = new Transform[0];

        public string RouteName => routeName;
        public string Instructions => instructions;
        public int GateCount => gates != null ? gates.Length : 0;

        /// <summary>Robotun bırakılacağı konum.</summary>
        public Vector3 StartPosition => startPoint != null ? startPoint.position : transform.position;

        /// <summary>Robotun bırakılacağı yön.</summary>
        public Quaternion StartRotation => startPoint != null ? startPoint.rotation : transform.rotation;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(StartPosition, 0.6f);
            Gizmos.DrawRay(StartPosition, StartRotation * Vector3.forward * 2f);

            if (gates == null)
            {
                return;
            }

            Vector3 previous = StartPosition;
            Gizmos.color = new Color(0.3f, 0.8f, 0.95f, 0.8f);
            foreach (Transform gate in gates)
            {
                if (gate == null)
                {
                    continue;
                }

                Gizmos.DrawLine(previous, gate.position);
                Gizmos.DrawWireCube(gate.position, new Vector3(1.5f, 0.2f, 1.5f));
                previous = gate.position;
            }
        }
    }
}
