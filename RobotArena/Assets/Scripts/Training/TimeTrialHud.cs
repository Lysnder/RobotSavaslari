using System.Collections.Generic;
using UnityEngine;

namespace RobotSavaslari.Training
{
    /// <summary>
    /// Eğitimin ekran tarafı: Ready erişimi, süre, kontrol noktası ilerlemesi,
    /// uyarılar ve rota işaretleri.
    ///
    /// İşaretler çalışma anında kurulur ve çıkışta yok edilir; çarpışma hacmi
    /// taşımazlar, yani fiziğe karışmazlar.
    /// </summary>
    public sealed class TimeTrialHud : MonoBehaviour
    {
        [SerializeField] private TimeTrialSession session;

        [Header("Yerleşim")]
        [SerializeField] private Vector2 accessSize = new Vector2(190f, 30f);

        [Tooltip("Sürüş tuşları düğmesinin üstünde kalsın diye.")]
        [SerializeField] private float accessBottomMargin = 62f;

        private readonly List<GameObject> markers = new List<GameObject>();
        private GUIStyle panelStyle;


        [SerializeField] private GhostRunner ghost;

        /// <summary>Gösterilecek oturum.</summary>
        public TimeTrialSession Session
        {
            get => session;
            set => session = value;
        }

        /// <summary>Kişisel rekoru ve hayaleti yöneten bileşen.</summary>
        public GhostRunner Ghost
        {
            get => ghost;
            set => ghost = value;
        }


        private void Awake()
        {
            if (session == null)
            {
                session = GetComponent<TimeTrialSession>();
            }
        }

        private void OnDisable() => ClearMarkers();

        private void OnDestroy()
        {
            ClearMarkers();

        }

        private void Update()
        {
            if (session == null)
            {
                return;
            }

            if (session.State == TimeTrialState.Inactive)
            {
                ClearMarkers();
                return;
            }

            EnsureMarkers();
            RefreshMarkers();
        }

        private void EnsureMarkers()
        {
            if (markers.Count == session.CheckpointCount)
            {
                return;
            }

            ClearMarkers();


            for (int index = 0; index < session.CheckpointCount; index++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"Egitim Kontrol Noktasi {index + 1}";

                // Fiziğe karışmasın: yalnızca görsel.
                Destroy(marker.GetComponent<Collider>());


                markers.Add(marker);
            }
        }

        private void RefreshMarkers()
        {
            for (int index = 0; index < markers.Count; index++)
            {
                GameObject marker = markers[index];
                if (marker == null)
                {
                    continue;
                }

                TimeTrialCheckpoint checkpoint = session.GetCheckpoint(index);
                marker.transform.position = new Vector3(checkpoint.position.x, 0.03f, checkpoint.position.z);
                marker.transform.localScale = new Vector3(checkpoint.radius * 2f, 0.02f, checkpoint.radius * 2f);

                bool passed = index < session.NextCheckpointIndex;
                bool isNext = index == session.NextCheckpointIndex;

                Renderer renderer = marker.GetComponent<Renderer>();
                
                renderer.material.color = passed
                    ? new Color(0.25f, 0.45f, 0.3f, 0.35f)
                    : isNext
                        ? new Color(0.95f, 0.78f, 0.2f, 0.75f)
                        : new Color(0.3f, 0.6f, 0.85f, 0.4f);
            }
        }

        private void ClearMarkers()
        {
            foreach (GameObject marker in markers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }

            markers.Clear();
        }

        private void OnGUI()
        {
            if (session == null)
            {
                return;
            }

            if (panelStyle == null)
            {
                panelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter };
            }

            if (session.State == TimeTrialState.Inactive)
            {
                DrawAccess();
                return;
            }

            DrawSession();
        }

        private void DrawAccess()
        {
            if (!session.CanEnter)
            {
                return;
            }

            Rect rect = new Rect((Screen.width - accessSize.x) * 0.5f,
                Screen.height - accessSize.y - accessBottomMargin,
                accessSize.x, accessSize.y);

            if (GUI.Button(rect, "Surus egitimi"))
            {
                session.Enter();
            }
        }

        private void DrawSession()
        {
            const float width = 320f;
            float x = (Screen.width - width) * 0.5f;

            GUI.Box(new Rect(x, 12f, width, 74f), GUIContent.none);

            string progress = session.State == TimeTrialState.Finished
                ? (session.LastLapValid
                    ? $"Tur: {session.LastLapTime:0.00} s"
                    : $"Gecersiz tur ({session.LastLapTime:0.00} s)")
                : $"{session.NextCheckpointIndex}/{session.CheckpointCount}   {session.ElapsedTime:0.00} s";

            GUI.Label(new Rect(x, 18f, width, 22f), progress, panelStyle);
            GUI.Label(new Rect(x, 42f, width, 36f), session.Notice, panelStyle);

            if (ghost != null && ghost.PersonalBestTime > 0f)
            {
                string record = ghost.HasGhost
                    ? $"Rekor: {ghost.PersonalBestTime:0.00} s (hayalet ekranda)"
                    : $"Rekor: {ghost.PersonalBestTime:0.00} s";
                GUI.Label(new Rect(x, 64f, width, 20f), record, panelStyle);
            }

            float buttonY = 92f;
            if (GUI.Button(new Rect(x + 10f, buttonY, width * 0.45f, 26f), "Yeniden dene"))
            {
                session.Retry();
            }

            if (GUI.Button(new Rect(x + width * 0.52f, buttonY, width * 0.45f, 26f), "Egitimden cik"))
            {
                session.Exit();
            }
        }
    }
}
