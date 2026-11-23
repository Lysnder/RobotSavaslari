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
        private static readonly int MarkerColor = Shader.PropertyToID("_Color");
        private Material markerMaterial;
        private MaterialPropertyBlock markerProperties;
        private int renderedRevision = -1;
        private int renderedCheckpoint = -1;
        private float renderedHeight;
        private float guiScale;
        private float guiWidth;
        private float guiHeight;
        private GUIStyle panelStyle;
        private GUIStyle wrapped;
        private bool showExercises;
        private Vector2 historyScroll;
        private string entryNotice;

        public DrivingExerciseCatalog ExerciseCatalog { get; set; }
        public bool ShowReadyShortcut { get; set; } = true;


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

        private void OnDisable()
        {
            showExercises = false;
            entryNotice = null;
            ClearMarkers();
            if (session != null && session.Exercise != null) session.Exit();
        }

        private void OnDestroy()
        {
            ClearMarkers();
            if (markerMaterial != null) Destroy(markerMaterial);
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
            float height = session.SurfaceHeight;
            if (renderedRevision == session.AttemptRevision && renderedCheckpoint == session.NextCheckpointIndex &&
                renderedHeight == height) return;
            RefreshMarkers();
            renderedRevision = session.AttemptRevision;
            renderedCheckpoint = session.NextCheckpointIndex;
            renderedHeight = height;
        }

        private void EnsureMarkers()
        {
            if (markers.Count == session.CheckpointCount)
            {
                return;
            }

            ClearMarkers();
            if (markerMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("SelectionRing");
                if (shader != null) markerMaterial = new Material(shader);
                markerProperties = new MaterialPropertyBlock();
            }

            for (int index = 0; index < session.CheckpointCount; index++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"Egitim Kontrol Noktasi {index + 1}";

                // Fiziğe karışmasın: yalnızca görsel.
                Collider collider = marker.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);


                if (markerMaterial != null) marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
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
                marker.transform.position = new Vector3(checkpoint.position.x, session.SurfaceHeight + 0.03f, checkpoint.position.z);
                marker.transform.localScale = new Vector3(checkpoint.radius * 2f, 0.02f, checkpoint.radius * 2f);

                bool passed = index < session.NextCheckpointIndex;
                bool isNext = index == session.NextCheckpointIndex;

                Renderer renderer = marker.GetComponent<Renderer>();
                
                var color = passed
                    ? new Color(0.25f, 0.45f, 0.3f, 0.35f)
                    : isNext
                        ? new Color(0.95f, 0.78f, 0.2f, 0.75f)
                        : new Color(0.3f, 0.6f, 0.85f, 0.4f);
                markerProperties.SetColor(MarkerColor, color);
                renderer.SetPropertyBlock(markerProperties);
            }
        }

        private void ClearMarkers()
        {
            foreach (GameObject marker in markers)
            {
                if (marker != null)
                {
                    marker.SetActive(false);
                    Destroy(marker);
                }
            }

            markers.Clear();
            renderedRevision = -1;
        }

        private void OnGUI()
        {
            if (session == null) return;

            guiScale = Mathf.Max(.01f, Mathf.Min(Screen.height / 720f, Screen.width / 620f));
            guiWidth = Screen.width / guiScale;
            guiHeight = Screen.height / guiScale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1f));
            try { DrawHud(); }
            finally { GUI.matrix = previous; }
        }

        private void DrawHud()
        {
            if (panelStyle == null)
            {
                panelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter };
            }

            if (session.State == TimeTrialState.Inactive)
            {
                DrawAccess();
                return;
            }

            if (session.Exercise == null) DrawSession();
            else DrawExercise();
        }

        private void DrawAccess()
        {
            if (!ShowReadyShortcut || !session.CanEnter)
            {
                showExercises = false;
                entryNotice = null;
                return;
            }

            Rect rect = new Rect((guiWidth - accessSize.x) * 0.5f,
                guiHeight - accessSize.y - Mathf.Max(34f / guiScale, accessBottomMargin),
                accessSize.x, accessSize.y);

            if (GUI.Button(rect, "Surus egitimi"))
            {
                session.Enter();
            }

            DrawExerciseChoices(rect);
        }

        private void DrawExerciseChoices(Rect access)
        {
            var definitions = ExerciseCatalog != null ? ExerciseCatalog.exercises : null;
            if (definitions == null || definitions.Length == 0) return;
            float width = Mathf.Min(400f, guiWidth - 18f);
            Rect button = new Rect((guiWidth - width) * .5f, access.y - 29f, width, 26f);
            if (GUI.Button(button, showExercises ? "Alıştırmaları kapat" : "Sürüş alıştırmaları"))
            {
                showExercises = !showExercises;
                entryNotice = null;
            }

            if (!showExercises) return;
            int rows = (definitions.Length + 1) / 2;
            float y = button.y - rows * 29f;
            if (y < 5f) return;
            float column = width * .5f;
            GUI.Box(new Rect(button.x - 2f, y - 3f, width + 4f, rows * 29f), GUIContent.none);
            for (int i = 0; i < definitions.Length; i++)
            {
                var exercise = definitions[i];
                if (exercise == null) continue;
                if (!GUI.Button(new Rect(button.x + column * (i % 2), y + i / 2 * 29f, column - 2f, 26f), exercise.title)) continue;
                if (session.EnterExercise(exercise))
                {
                    showExercises = false;
                    historyScroll = Vector2.zero;
                    entryNotice = null;
                    return;
                }

                entryNotice = "Bu arena düzeninde güvenli rota yok.";
            }

            if (entryNotice != null) GUI.Label(new Rect(button.x, y - 23f, width, 24f), entryNotice, panelStyle);
        }

        private void DrawExercise()
        {
            var exercise = session.Exercise;
            var result = session.ExerciseResult;
            bool finished = session.State == TimeTrialState.Finished;
            float width = Mathf.Min(455f, guiWidth - 16f);
            if (wrapped == null) wrapped = new GUIStyle(GUI.skin.label) { wordWrap = true };
            DrawTargetLabels(exercise);
            float height = Mathf.Min(finished ? 320f : 212f, guiHeight - 20f);
            GUILayout.BeginArea(new Rect((guiWidth - width) * .5f, 9f, width, height), GUI.skin.box);
            GUILayout.Label(exercise.title + $"  |  {session.ElapsedTime:0.00} sn", panelStyle);
            GUILayout.Label(session.Notice, wrapped);
            if (exercise.kind == DrivingExerciseKind.Slalom)
                GUILayout.Label($"Kapılar: {session.NextCheckpointIndex}/{session.CheckpointCount}   Kaçırılan: {result.gateErrors}");
            else
            {
                GUILayout.Label($"Merkeze uzaklık: {result.positionError:0.00} m   Hız: {result.finalSpeed:0.00} m/sn");
                if (exercise.kind == DrivingExerciseKind.ControlledStop)
                    GUILayout.Label($"Hedef kenarı aşımı (en çok): {result.overshoot:0.00} m");
                else
                    GUILayout.Label($"Açı sapması: {result.yawError:0.0}°   Yön hatası: {result.directionErrors}");
                if (!finished && exercise.kind != DrivingExerciseKind.PrecisionTurn)
                    GUILayout.Label($"Kesintisiz duruş: {session.ExerciseDwell:0.0}/{exercise.dwellSeconds:0.0} sn");
            }

            GUILayout.Label($"Sert temas: {result.contacts}");
            var record = ghost != null ? ghost.ExerciseRecord : null;
            if (record?.ghost != null)
                GUILayout.Label($"Rekor: {record.best.seconds:0.00} sn" + (!finished && ghost.HasGhost ? " (hayalet)" : ""));
            if (session.ExerciseStore?.Warning != null)
                GUILayout.Label(session.ExerciseStore.Warning, wrapped);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yeniden dene")) session.Retry();
            if (GUILayout.Button("Eğitimden çık")) session.Exit();
            GUILayout.EndHorizontal();
            if (finished && record != null)
            {
                historyScroll = GUILayout.BeginScrollView(historyScroll);
                for (int i = 0; i < record.history.Count; i++)
                {
                    var attempt = record.history[i];
                    GUILayout.Label($"{i + 1}. {(attempt.success ? "Başarılı" : "Geçersiz")} · " +
                        $"{attempt.seconds:0.00} sn · {attempt.Faults} hata");
                }
                GUILayout.EndScrollView();
            }

            GUILayout.EndArea();
        }

        private void DrawTargetLabels(DrivingExerciseDefinition exercise)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] == null) continue;
                Vector3 point = camera.WorldToScreenPoint(markers[i].transform.position + Vector3.up * .2f);
                if (point.z <= 0f) continue;
                string label = exercise.kind == DrivingExerciseKind.Slalom
                    ? $"{i + 1} {(i == session.NextCheckpointIndex ? "›" : "")}"
                    : exercise.kind == DrivingExerciseKind.ControlledStop ? "Dur" : $"Hedef {exercise.targetYaw:0}°";
                GUI.Label(new Rect(point.x / guiScale - 53f, guiHeight - point.y / guiScale - 16f, 106f, 24f), label, panelStyle);
            }
        }

        private void DrawSession()
        {
            const float width = 320f;
            float x = (guiWidth - width) * 0.5f;

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
