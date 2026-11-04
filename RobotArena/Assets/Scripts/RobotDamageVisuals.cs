using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RobotSavaslari
{
    // Partition the existing readable runtime mesh once. No duplicate GLB/textures
    // or mesh slicing allocations are needed during a collision.
    [RequireComponent(typeof(RobotHealth))]
    public sealed class RobotDamageVisuals : MonoBehaviour
    {
        public enum PartKind { Body, Armor, Wheel, Weapon }

        public sealed class Part
        {
            public string Name;
            public PartKind Kind;
            public Transform Transform;
            public Mesh Mesh;
            public MeshRenderer Wear;
            public Vector3 RestPosition;
            public Quaternion RestRotation;
            public float Damage;
            public float Threshold;
            public bool Detached;
            public readonly Vector4[] Marks = new Vector4[4];
            public int NextMark;
        }

        [SerializeField, Min(1)] private float armorDurability = 22f;
        [SerializeField, Min(1)] private float wheelDurability = 30f;
        [SerializeField, Min(1)] private float weaponDurability = 45f;
        [SerializeField, Range(2, 30)] private float debrisLifetime = 12f;
        private readonly List<Part> _parts = new List<Part>();
        private readonly List<GameObject> _debris = new List<GameObject>();
        private MaterialPropertyBlock _properties;
        private RobotHealth _health;
        private Rigidbody _body;
        private Collider[] _ownerColliders;
        private Material _wearMaterial, _particleMaterial, _coreMaterial;
        private ParticleSystem _sparks, _smoke;
        private AudioSource _audio;
        private AudioClip _impactClip;
        private MeshRenderer _sourceRenderer;
        private MeshRenderer _coreRenderer;
        private float _nextSmoke, _nextEffect;
        private bool _initialized;

        public IReadOnlyList<Part> Parts => _parts;
        public int OriginalTriangleCount { get; private set; }
        public int DetachedCount { get; private set; }
        public int LostWheels { get; private set; }
        public bool WeaponOperational { get; private set; } = true;
        public float Mobility => Mathf.Max(0.3f, 1f - LostWheels * 0.18f);
        public float Turning => Mathf.Max(0.45f, 1f - LostWheels * 0.12f);
        public float SteeringPull { get; private set; }

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            _health = GetComponent<RobotHealth>();
            _body = GetComponent<Rigidbody>();
            _ownerColliders = GetComponentsInChildren<Collider>();
        }

        private void OnEnable()
        {
            _health.Damaged += ReceiveHit;
            _health.Restored += RestoreParts;
        }

        private void OnDisable()
        {
            _health.Damaged -= ReceiveHit;
            _health.Restored -= RestoreParts;
        }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Shader wearShader = Resources.Load<Shader>("RobotDamageWear");
            Shader particleShader = Resources.Load<Shader>("RobotImpactParticles");
            if (wearShader == null || particleShader == null)
            {
                Debug.LogError("Robot damage shaders are missing from Resources.", this);
                return;
            }
            _wearMaterial = new Material(wearShader) { name = "Runtime Damage Wear" };
            _particleMaterial = new Material(particleShader) { name = "Runtime Impact Particles" };
            BuildParts();
            _sparks = CreateParticles("Impact Sparks", 128, false);
            _smoke = CreateParticles("Damage Smoke", 64, true);
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.minDistance = 3f;
            _audio.maxDistance = 30f;
            _impactClip = CreateImpactClip();
        }

        private void BuildParts()
        {
            Transform model = transform.Find("Robot Model");
            if (model == null) return; // Prototype fallback robots still receive effects.
            MeshFilter filter = model.GetComponent<MeshFilter>();
            _sourceRenderer = model.GetComponent<MeshRenderer>();
            if (filter == null || _sourceRenderer == null || filter.sharedMesh == null) return;
            Mesh source = filter.sharedMesh;
            if (!source.isReadable || source.subMeshCount != 1)
            {
                Debug.LogWarning("Damage partitioning requires a readable, single-material robot mesh.", this);
                return;
            }

            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uv = source.uv;
            Vector4[] tangents = source.tangents;
            int[] triangles = source.triangles;
            OriginalTriangleCount = triangles.Length / 3;
            var groups = new List<int>[10];
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<int>();
            bool blue = name.StartsWith("Mavi");
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 center = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
                int region = Classify(center, blue);
                groups[region].Add(triangles[i]);
                groups[region].Add(triangles[i + 1]);
                groups[region].Add(triangles[i + 2]);
            }

            string[] names = { "Body", "Wheel_FL", "Wheel_RL", "Wheel_FR", "Wheel_RR",
                blue ? "Spinner" : "Lifter", "Armor_FL", "Armor_RL", "Armor_FR", "Armor_RR" };
            for (int group = 0; group < groups.Length; group++)
            {
                List<int> indices = groups[group];
                if (indices.Count == 0) continue;
                Bounds bounds = new Bounds(vertices[indices[0]], Vector3.zero);
                foreach (int index in indices) bounds.Encapsulate(vertices[index]);
                Vector3 pivot = bounds.center;
                var positions = new List<Vector3>(indices.Count);
                var partNormals = new List<Vector3>(indices.Count);
                var partUV = new List<Vector2>(indices.Count);
                var partTangents = new List<Vector4>(indices.Count);
                int[] faceIndices = new int[indices.Count];
                for (int j = 0; j < indices.Count; j++)
                {
                    int index = indices[j];
                    positions.Add(vertices[index] - pivot);
                    if (normals.Length == vertices.Length) partNormals.Add(normals[index]);
                    if (uv.Length == vertices.Length) partUV.Add(uv[index]);
                    if (tangents.Length == vertices.Length) partTangents.Add(tangents[index]);
                    faceIndices[j] = j;
                }
                var mesh = new Mesh { name = name + " " + names[group], indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(positions);
                mesh.SetTriangles(faceIndices, 0);
                if (partNormals.Count != 0) mesh.SetNormals(partNormals); else mesh.RecalculateNormals();
                if (partUV.Count != 0) mesh.SetUVs(0, partUV);
                if (partTangents.Count != 0) mesh.SetTangents(partTangents);
                mesh.RecalculateBounds();
                var partObject = new GameObject(names[group]);
                partObject.layer = gameObject.layer;
                partObject.transform.SetParent(model, false);
                partObject.transform.localPosition = pivot;
                partObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = partObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = _sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode = _sourceRenderer.shadowCastingMode;
                renderer.receiveShadows = _sourceRenderer.receiveShadows;
                var overlay = new GameObject("Damage Wear");
                overlay.transform.SetParent(partObject.transform, false);
                overlay.AddComponent<MeshFilter>().sharedMesh = mesh;
                var wear = overlay.AddComponent<MeshRenderer>();
                wear.sharedMaterial = _wearMaterial;
                wear.shadowCastingMode = ShadowCastingMode.Off;
                wear.receiveShadows = false;
                wear.enabled = false;
                PartKind kind = group == 0 ? PartKind.Body : group < 5 ? PartKind.Wheel : group == 5 ? PartKind.Weapon : PartKind.Armor;
                _parts.Add(new Part { Name = names[group], Kind = kind, Transform = partObject.transform, Mesh = mesh,
                    Wear = wear, RestPosition = pivot, RestRotation = Quaternion.identity,
                    Threshold = kind == PartKind.Wheel ? wheelDurability : kind == PartKind.Weapon ? weaponDurability : armorDurability });
            }
            // The sum of the generated parts is exactly the imported mesh, with its
            // UVs, normals and tangents retained. Only the original renderer is hidden.
            _sourceRenderer.enabled = false;
            BuildInnerChassis(model, source.bounds);
            var animator = GetComponent<RobotModelAnimator>();
            if (animator == null) gameObject.AddComponent<RobotModelAnimator>();
            else animator.BindParts();
        }

        private void BuildInnerChassis(Transform model, Bounds bounds)
        {
            var rootBounds = new Bounds(transform.InverseTransformPoint(model.TransformPoint(bounds.center)), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        rootBounds.Encapsulate(transform.InverseTransformPoint(model.TransformPoint(bounds.center +
                            Vector3.Scale(bounds.extents, new Vector3(x, y, z)))));
            var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.name = "Inner Chassis";
            core.transform.SetParent(transform, false);
            core.transform.localPosition = rootBounds.center - Vector3.up * rootBounds.size.y * 0.12f;
            core.transform.localScale = Vector3.Scale(rootBounds.size, new Vector3(0.64f, 0.23f, 0.66f));
            Destroy(core.GetComponent<Collider>());
            _coreMaterial = new Material(_sourceRenderer.sharedMaterial) { name = "Runtime Inner Chassis" };
            foreach (string texture in new[] { "baseColorTexture", "normalTexture", "metallicRoughnessTexture" })
                if (_coreMaterial.HasProperty(texture)) _coreMaterial.SetTexture(texture, null);
            if (_coreMaterial.HasProperty("baseColorFactor")) _coreMaterial.SetColor("baseColorFactor", new Color(0.055f, 0.065f, 0.075f));
            _coreRenderer = core.GetComponent<MeshRenderer>();
            _coreRenderer.sharedMaterial = _coreMaterial;
            _coreRenderer.enabled = false;
        }

        private static int Classify(Vector3 p, bool blue)
        {
            // glTF's Unity coordinates corresponding to the measured Blender cuts.
            float x = -p.x, y = -p.z, z = p.y;
            float axleX = blue ? 0.56f : (x < 0 ? 0.42f : 0.51f);
            float axleZ = blue ? -0.235f : -0.095f;
            float dx = Mathf.Abs(x) - axleX;
            float dz = z - axleZ;
            float radius = blue ? 0.245f : 0.275f;
            if (Mathf.Abs(y) > (blue ? 0.51f : 0.505f) && dx * dx + dz * dz < radius * radius && z < (blue ? 0.025f : 0.205f))
                return 1 + (y > 0 ? 2 : 0) + (x > 0 ? 1 : 0);
            if (blue ? z > 0.295f : ((x < 0.20f && Mathf.Abs(y) < 0.215f && z > 0.035f) || (x < -0.42f && Mathf.Abs(y) < 0.235f && z < 0.08f)))
                return 5;
            if (z > (blue ? -0.02f : -0.08f) && (Mathf.Abs(y) > 0.24f || Mathf.Abs(x) > 0.24f))
                return 6 + (y > 0 ? 2 : 0) + (x > 0 ? 1 : 0);
            return 0;
        }

        private void ReceiveHit(RobotHit hit)
        {
            Initialize();
            Part nearest = null;
            float best = float.PositiveInfinity;
            foreach (Part part in _parts)
            {
                if (part.Detached) continue;
                Vector3 localPoint = part.Transform.InverseTransformPoint(hit.Point);
                float score = Mathf.Sqrt(part.Mesh.bounds.SqrDistance(localPoint)) + localPoint.magnitude * 0.12f;
                if (part.Kind == PartKind.Body) score += 0.12f;
                if (score < best) { best = score; nearest = part; }
            }
            if (nearest != null)
            {
                nearest.Damage += hit.Amount;
                Vector3 point = nearest.Transform.InverseTransformPoint(hit.Point);
                nearest.Marks[nearest.NextMark++ % 4] = new Vector4(point.x, point.y, point.z, Mathf.Clamp01(nearest.Damage / nearest.Threshold));
            }
            UpdateWear();
            if (nearest != null && nearest.Kind != PartKind.Body && nearest.Damage >= nearest.Threshold)
                Detach(nearest, hit);
            EmitImpact(hit);
        }

        private void UpdateWear()
        {
            float overall = 1f - _health.CurrentHealth / _health.MaxHealth;
            foreach (Part part in _parts)
            {
                if (part.Detached) continue;
                float wear = Mathf.Clamp01(part.Damage / part.Threshold * 0.8f + overall * 0.2f);
                part.Wear.enabled = wear > 0.001f;
                _properties.Clear();
                _properties.SetFloat("_Wear", wear);
                for (int i = 0; i < 4; i++) _properties.SetVector("_Hit" + i, part.Marks[i]);
                part.Wear.SetPropertyBlock(_properties);
            }
        }

        private void Detach(Part part, RobotHit hit)
        {
            var debris = Instantiate(part.Transform.gameObject, part.Transform.position, part.Transform.rotation);
            debris.name = name + " Debris " + part.Name;
            debris.transform.localScale = part.Transform.lossyScale;
            part.Wear.GetPropertyBlock(_properties);
            debris.transform.Find("Damage Wear").GetComponent<MeshRenderer>().SetPropertyBlock(_properties);
            var box = debris.AddComponent<BoxCollider>();
            box.center = part.Mesh.bounds.center;
            Vector3 size = part.Mesh.bounds.size;
            box.size = new Vector3(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y), Mathf.Max(0.05f, size.z));
            foreach (Collider owner in _ownerColliders)
                if (owner != null) Physics.IgnoreCollision(box, owner);
            var body = debris.AddComponent<Rigidbody>();
            body.mass = part.Kind == PartKind.Wheel ? 0.45f : 0.8f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = _body != null ? _body.GetPointVelocity(debris.transform.position) : Vector3.zero;
            body.AddForce(hit.Impulse + Vector3.up * 2.2f, ForceMode.Impulse);
            body.AddTorque(Random.onUnitSphere * 0.65f, ForceMode.Impulse);
            _debris.RemoveAll(item => item == null);
            _debris.Add(debris);
            Destroy(debris, debrisLifetime);
            part.Detached = true;
            part.Transform.gameObject.SetActive(false);
            DetachedCount++;
            if (part.Kind == PartKind.Wheel)
            {
                LostWheels++;
                float side = transform.InverseTransformPoint(part.Transform.position).x;
                SteeringPull = Mathf.Clamp(SteeringPull + Mathf.Sign(side) * 0.075f, -0.25f, 0.25f);
            }
            if (part.Kind == PartKind.Weapon) WeaponOperational = false;
            if (part.Kind == PartKind.Armor && _coreRenderer != null) _coreRenderer.enabled = true;
        }

        public void RestoreParts()
        {
            foreach (GameObject debris in _debris) if (debris != null) Destroy(debris);
            _debris.Clear();
            DetachedCount = LostWheels = 0;
            SteeringPull = 0f;
            WeaponOperational = true;
            if (_coreRenderer != null) _coreRenderer.enabled = false;
            foreach (Part part in _parts)
            {
                part.Detached = false;
                part.Damage = 0f;
                part.NextMark = 0;
                System.Array.Clear(part.Marks, 0, part.Marks.Length);
                part.Transform.localPosition = part.RestPosition;
                part.Transform.localRotation = part.RestRotation;
                part.Transform.gameObject.SetActive(true);
                part.Wear.enabled = false;
                part.Wear.SetPropertyBlock(null);
            }
            if (_sparks != null) _sparks.Clear();
            if (_smoke != null) _smoke.Clear();
            if (_audio != null) _audio.Stop();
            _nextSmoke = _nextEffect = 0f;
            var animator = GetComponent<RobotModelAnimator>();
            if (animator != null) animator.ResetPose();
        }

        private ParticleSystem CreateParticles(string label, int maximum, bool smoke)
        {
            var host = new GameObject(label);
            host.transform.SetParent(transform, false);
            var system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maximum;
            main.gravityModifier = smoke ? -0.025f : 0.7f;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _particleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.renderMode = smoke ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            if (!smoke) { renderer.lengthScale = 0.4f; renderer.velocityScale = 0.06f; }
            return system;
        }

        private void EmitImpact(RobotHit hit)
        {
            if (_sparks == null || Time.time < _nextEffect) return;
            _nextEffect = Time.time + 0.08f;
            _sparks.Play();
            Vector3 normal = hit.Normal.sqrMagnitude > 0.01f ? hit.Normal.normalized : Vector3.up;
            for (int i = 0; i < (hit.Weapon ? 22 : 12); i++)
            {
                var particle = new ParticleSystem.EmitParams
                {
                    position = hit.Point + normal * 0.025f,
                    velocity = (normal + Random.insideUnitSphere * 1.2f + Vector3.up * 0.6f) * Random.Range(1.5f, 4f),
                    startLifetime = Random.Range(0.15f, 0.5f), startSize = Random.Range(0.015f, 0.04f),
                    startColor = Color.Lerp(new Color(1f, 0.38f, 0.04f), new Color(1f, 0.95f, 0.55f), Random.value)
                };
                _sparks.Emit(particle, 1);
            }
            EmitSmoke(hit.Point, 4, 0.35f);
            _audio.pitch = Random.Range(0.8f, 1.2f);
            _audio.PlayOneShot(_impactClip, hit.Weapon ? 0.32f : 0.2f);
        }

        private void Update()
        {
            if (_health.CurrentHealth / _health.MaxHealth > 0.35f || Time.time < _nextSmoke || _smoke == null) return;
            _nextSmoke = Time.time + (_health.IsDefeated ? 0.18f : 0.4f);
            EmitSmoke(transform.position + Vector3.up * 0.65f, 1, 0.6f);
        }

        private void EmitSmoke(Vector3 point, int count, float size)
        {
            if (_smoke == null) return;
            _smoke.Play();
            for (int i = 0; i < count; i++)
                _smoke.Emit(new ParticleSystem.EmitParams { position = point + Random.insideUnitSphere * 0.05f,
                    velocity = Vector3.up * Random.Range(0.3f, 0.7f) + Random.insideUnitSphere * 0.12f,
                    startLifetime = Random.Range(0.7f, 1.5f), startSize = size,
                    startColor = new Color(0.09f, 0.08f, 0.07f, 0.35f) }, 1);
        }

        private static AudioClip CreateImpactClip()
        {
            const int rate = 22050;
            var samples = new float[rate / 6];
            // Local deterministic noise avoids consuming gameplay's random sequence.
            var noise = new System.Random(740);
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / rate;
                samples[i] = ((float)noise.NextDouble() * 0.8f - 0.4f + Mathf.Sin(time * 1700f) * 0.3f) * Mathf.Exp(-time * 35f);
            }
            var clip = AudioClip.Create("Robot metal impact", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            foreach (GameObject debris in _debris) if (debris != null) Destroy(debris);
            foreach (Part part in _parts) if (part.Mesh != null) Destroy(part.Mesh);
            if (_wearMaterial != null) Destroy(_wearMaterial);
            if (_particleMaterial != null) Destroy(_particleMaterial);
            if (_impactClip != null) Destroy(_impactClip);
            if (_coreMaterial != null) Destroy(_coreMaterial);
        }
    }
}
