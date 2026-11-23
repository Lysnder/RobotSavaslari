using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RobotSavaslari.Training
{
    /// <summary>
    /// Kişisel rekoru kaydeder ve sonraki denemede saydam, etkileşimsiz bir
    /// hayalet olarak oynatır.
    ///
    /// Hayalet yalnızca görsel parçalardan kurulur: Rigidbody, Collider,
    /// sağlık, dövüş, bot ve kontrol noktası üreticisi taşımaz. Fizik, hasar,
    /// kontrol noktası ilerlemesi ve maç sonucu üzerinde hiçbir etkisi yoktur.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class GhostRunner : MonoBehaviour
    {
        [SerializeField] private TimeTrialSession session;

        [Tooltip("Kayıt örnekleri arasındaki süre (saniye).")]
        [SerializeField, Min(0.02f)] private float sampleInterval = 0.05f;

        [Tooltip("Hayaletin saydamlığı.")]
        [SerializeField, Range(0.05f, 1f)] private float ghostAlpha = 0.35f;

        private readonly List<GhostLapSample> samples = new List<GhostLapSample>();
        private readonly List<Material> createdMaterials = new List<Material>();

        private GhostLapRecording personalBest;
        public DrivingExerciseStore.Record ExerciseRecord { get; private set; }
        private GameObject ghost;
        private Transform source;
        private TimeTrialState previousState = TimeTrialState.Inactive;
        private float lastSampleTime;
        private string loadedCourseId;
        private int recordedAttempt;

        /// <summary>İzlenen eğitim oturumu.</summary>
        public TimeTrialSession Session
        {
            get => session;
            set => session = value;
        }

        /// <summary>Bu parkurdaki kişisel rekor; yoksa null.</summary>
        public GhostLapRecording PersonalBest => personalBest;

        /// <summary>Kişisel rekorun süresi; yoksa 0.</summary>
        public float PersonalBestTime => personalBest != null ? personalBest.lapTime : 0f;

        /// <summary>Hayalet şu an sahnede mi?</summary>
        public bool HasGhost => ghost != null;

        /// <summary>Bu denemede alınan örnek sayısı.</summary>
        public int SampleCount => samples.Count;

        private void Awake()
        {
            if (session == null)
            {
                session = GetComponent<TimeTrialSession>();
            }
        }

        private void OnDisable() => ClearGhost();

        private void OnDestroy() => ClearGhost();

        /// <summary>
        /// Kaydı diskten tazeler. Parkur kimliği değiştiyse eski rekor
        /// kullanılmaz.
        /// </summary>
        public void ReloadPersonalBest()
        {
            if (session == null)
            {
                return;
            }

            loadedCourseId = session.ExerciseKey ?? session.CourseId;
            ExerciseRecord = session.Exercise != null ? session.ExerciseStore?.Find(loadedCourseId) : null;
            var legacy = session.Exercise == null ? GhostLapStore.Load(loadedCourseId) : null;
            personalBest = session.Exercise != null
                ? ExerciseRecord?.ghost
                : legacy ??
                  session.ExerciseStore?.MigrateLegacy(loadedCourseId, GhostLapStore.Load);
            if (legacy != null) session.ExerciseStore?.MigrateLegacy(loadedCourseId, _ => legacy);
        }

        private void Update()
        {
            if (session == null || session.Exercise == null) Tick();
        }

        private void FixedUpdate()
        {
            if (session != null && session.Exercise != null) Tick();
        }

        private void Tick()
        {
            if (session == null)
            {
                return;
            }

            TimeTrialState state = session.State;

            if (state == TimeTrialState.Inactive || session.PreparationStep != 0)
            {
                if (previousState != TimeTrialState.Inactive)
                {
                    // Eğitimden çıkış hayaleti temizler.
                    ClearGhost();
                    samples.Clear();
                }

                previousState = TimeTrialState.Inactive;
                return;
            }

            if (state == TimeTrialState.Running &&
                (previousState != TimeTrialState.Running || recordedAttempt != session.AttemptRevision))
            {
                BeginAttempt();
                recordedAttempt = session.AttemptRevision;
            }

            if (state == TimeTrialState.Running)
            {
                RecordSample();
                PlayGhost();
            }

            if (previousState == TimeTrialState.Running && state == TimeTrialState.Finished)
            {
                if (session.Exercise != null) ReportExercise();
                else ReportLap();
            }

            previousState = state;
        }

        private void BeginAttempt()
        {
            samples.Clear();
            lastSampleTime = -1f;

            if (session.Exercise != null)
            {
                samples.Capacity = Mathf.Max(samples.Capacity, DrivingExerciseStore.MaxSamples);
                if (session.Motor != null) AddSample(0f);
            }

            if (session.Exercise != null || loadedCourseId != session.CourseId || personalBest == null)
            {
                ReloadPersonalBest();
            }

            if (personalBest != null)
            {
                EnsureGhost();
                PlayGhost();
            }
            else
            {
                ClearGhost();
            }
        }

        private void RecordSample()
        {
            if (session.Motor == null)
            {
                return;
            }

            if (session.Exercise != null)
            {
                float sampleTime = samples.Count / 10f;
                if (sampleTime <= session.ElapsedTime && samples.Count < DrivingExerciseStore.MaxSamples)
                    AddSample(sampleTime);
                return;
            }

            float time = session.ElapsedTime;
            if (lastSampleTime >= 0f && time - lastSampleTime < sampleInterval)
            {
                return;
            }

            lastSampleTime = time;
            AddSample(time);
        }

        private void AddSample(float time)
        {
            Transform robot = session.Motor.transform;

            samples.Add(new GhostLapSample
            {
                time = time,
                position = robot.position,
                yaw = robot.eulerAngles.y,
            });
        }

        private void PlayGhost()
        {
            if (ghost == null || personalBest == null)
            {
                return;
            }

            // Hayalet denemeyle aynı saatte ilerler: Retry süreyi sıfırlayınca
            // hayalet de başa döner.
            personalBest.Evaluate(session.ElapsedTime, out Vector3 position, out float yaw);
            ghost.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        private void ReportExercise()
        {
            var store = session.ExerciseStore;
            if (store == null) return;
            var result = session.ExerciseResult;
            if (samples.Count == 0 || samples[samples.Count - 1].time < result.seconds)
            {
                if (samples.Count < DrivingExerciseStore.MaxSamples) AddSample(result.seconds);
            }

            var recording = result.success ? new GhostLapRecording
            {
                courseId = session.ExerciseKey,
                lapTime = result.seconds,
                samples = samples.ToArray()
            } : null;
            store.Save(session.ExerciseKey, result, recording);
            ExerciseRecord = store.Find(session.ExerciseKey);
        }

        private void ReportLap()
        {
            // Geçersiz ya da daha yavaş tur rekoru ezmez.
            if (!session.LastLapValid || session.LastLapTime <= 0f)
            {
                return;
            }

            if (personalBest != null && session.LastLapTime >= personalBest.lapTime)
            {
                return;
            }

            if (samples.Count < 2)
            {
                return;
            }

            GhostLapRecording recording = new GhostLapRecording
            {
                version = GhostLapRecording.CurrentVersion,
                courseId = session.CourseId,
                lapTime = session.LastLapTime,
                samples = samples.ToArray(),
            };

            if (GhostLapStore.Save(recording))
            {
                personalBest = recording;
                loadedCourseId = recording.courseId;
            }
        }

        private void EnsureGhost()
        {
            if (ghost != null)
            {
                return;
            }

            source = session.Motor != null ? session.Motor.transform : null;
            if (source == null)
            {
                return;
            }

            ghost = new GameObject("Kisisel Rekor Hayaleti");
            CopyVisualParts(source, ghost.transform);

            if (ghost.transform.childCount == 0)
            {
                // Kopyalanacak görsel parça yoksa hayalet kurulmaz.
                ClearGhost();
            }
        }

        private void CopyVisualParts(Transform robot, Transform ghostRoot)
        {
            foreach (Renderer renderer in robot.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh = ExtractMesh(renderer);
                if (mesh == null)
                {
                    continue;
                }

                GameObject part = new GameObject(renderer.name);
                part.transform.SetParent(ghostRoot, false);
                part.transform.localPosition = robot.InverseTransformPoint(renderer.transform.position);
                part.transform.localRotation = Quaternion.Inverse(robot.rotation) * renderer.transform.rotation;
                part.transform.localScale = renderer.transform.lossyScale;

                part.AddComponent<MeshFilter>().sharedMesh = mesh;

                MeshRenderer ghostRenderer = part.AddComponent<MeshRenderer>();
                ghostRenderer.sharedMaterials = BuildTransparentMaterials(renderer.sharedMaterials);
                ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
                ghostRenderer.receiveShadows = false;
            }
        }

        private static Mesh ExtractMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private Material[] BuildTransparentMaterials(Material[] sources)
        {
            Material[] result = new Material[sources.Length];

            for (int index = 0; index < sources.Length; index++)
            {
                if (sources[index] == null)
                {
                    continue;
                }

                Material copy = new Material(sources[index]);
                createdMaterials.Add(copy);

                if (copy.HasProperty("_Surface"))
                {
                    copy.SetFloat("_Surface", 1f);
                }

                copy.SetOverrideTag("RenderType", "Transparent");
                copy.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                copy.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                copy.SetInt("_ZWrite", 0);
                copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                copy.DisableKeyword("_ALPHATEST_ON");
                copy.renderQueue = (int)RenderQueue.Transparent;

                FadeColor(copy, "_BaseColor");
                FadeColor(copy, "_Color");

                result[index] = copy;
            }

            return result;
        }

        private void FadeColor(Material material, string property)
        {
            if (!material.HasProperty(property))
            {
                return;
            }

            Color color = material.GetColor(property);
            color.a = ghostAlpha;
            material.SetColor(property, color);
        }

        private void ClearGhost()
        {
            if (ghost != null)
            {
                ghost.SetActive(false);
                Destroy(ghost);
                ghost = null;
            }

            foreach (Material material in createdMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            createdMaterials.Clear();
        }
    }
}
