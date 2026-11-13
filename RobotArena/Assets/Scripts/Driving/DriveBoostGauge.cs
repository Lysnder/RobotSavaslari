using UnityEngine;

namespace RobotSavaslari.Driving
{
    /// <summary>
    /// Boost enerjisini ekranın sol altında gösterir: aktif sürüş göstergeleri
    /// için ayrılan alan burası. Sahneye elle yerleştirilmek yerine üretim
    /// bağlantısı tarafından eklenir, bu yüzden kendi referansını kendisi bulur.
    /// </summary>
    [AddComponentMenu("Robot Savaslari/Driving/Boost Gostergesi")]
    public class DriveBoostGauge : MonoBehaviour
    {
        [SerializeField] private DriveBoost boost;

        [Header("Yerleşim")]
        [SerializeField] private Vector2 margin = new Vector2(16f, 16f);
        [SerializeField] private Vector2 size = new Vector2(220f, 18f);
        [SerializeField] private int fontSize = 13;

        private GUIStyle labelStyle;
        private Texture2D barTexture;

        /// <summary>Gösterilecek enerji kaynağı; bağlantı bunu kurar.</summary>
        public DriveBoost Boost
        {
            get => boost;
            set => boost = value;
        }

        private void Awake()
        {
            if (boost == null)
            {
                boost = GetComponent<DriveBoost>();
            }
        }

        private void OnDestroy()
        {
            if (barTexture != null)
            {
                Destroy(barTexture);
            }
        }

        private void OnGUI()
        {
            if (boost == null)
            {
                return;
            }

            if (barTexture == null)
            {
                barTexture = new Texture2D(1, 1);
                barTexture.SetPixel(0, 0, Color.white);
                barTexture.Apply();
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize };
            }

            float x = margin.x;
            float y = Screen.height - margin.y - size.y - fontSize - 6f;

            GUI.Label(new Rect(x, y, size.x, fontSize + 4f),
                boost.IsBoosting ? "BOOST" : "Boost enerjisi", labelStyle);

            Rect bar = new Rect(x, y + fontSize + 6f, size.x, size.y);
            DrawBar(bar, 1f, new Color(0f, 0f, 0f, 0.45f));
            DrawBar(new Rect(bar.x, bar.y, bar.width * boost.EnergyRatio, bar.height), 1f,
                boost.IsBoosting ? new Color(1f, 0.72f, 0.2f, 0.95f) : new Color(0.35f, 0.72f, 0.95f, 0.9f));
        }

        private void DrawBar(Rect rect, float alpha, Color color)
        {
            Color previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);
            GUI.DrawTexture(rect, barTexture);
            GUI.color = previous;
        }
    }
}
