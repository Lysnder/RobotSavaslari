using System;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RobotSavaslari.Combat.Testing
{
    /// <summary>Deneme duzenegindeki bir robot ve onun deneme kayitlari.</summary>
    public sealed class CombatTestRobot
    {
        public readonly string Name;
        public readonly GameObject Root;
        public readonly RobotHealth Health;
        public readonly MeleeAttack Attack;
        public readonly AttackTestTrigger Trigger;
        public readonly Collider[] Colliders;
        public readonly Vector3 SpawnPosition;
        public readonly Quaternion SpawnRotation;

        public float StartHealth { get; set; }
        public int DefeatNotifications { get; internal set; }

        internal CombatTestRobot(string name, GameObject root, Vector3 spawnPosition, Quaternion spawnRotation, float startHealth)
        {
            Name = name;
            Root = root;
            Health = root.GetComponent<RobotHealth>();
            Attack = root.GetComponent<MeleeAttack>();
            Trigger = root.GetComponent<AttackTestTrigger>();
            Colliders = root.GetComponentsInChildren<Collider>();
            SpawnPosition = spawnPosition;
            SpawnRotation = spawnRotation;
            StartHealth = startHealth;
        }
    }

    /// <summary>
    /// GECICI COZUM: Repoda ortak robot prefab'i olmadigi icin iki deneme robotu kodla kurulur.
    /// Her robotta ortak <see cref="RobotHealth"/>, bir <see cref="MeleeAttack"/> ve
    /// govde + iki tekerlek olmak uzere uc collider bulunur. Ortak prefab geldiginde
    /// <see cref="CreateRobot"/> yerine prefab orneklenmelidir.
    /// </summary>
    public sealed class CombatTestRig : IDisposable
    {
        /// <summary>Govdenin ileri yondeki yari uzunlugu (m). Menzil hesaplarinda kullanilir.</summary>
        public const float BodyHalfLength = 0.65f;
        /// <summary>Iki robotun varsayilan merkez mesafesi: govde ve tekerlek ayni anda menzilde kalir.</summary>
        public const float DefaultSpacing = 1.5f;

        private readonly GameObject _root;
        private readonly bool _manualClock;

        public CombatTestRobot A { get; }
        public CombatTestRobot B { get; }
        public LocalMatchGate Gate { get; }
        public Vector3 Center { get; }

        /// <summary>Elle ilerletilen zaman (yalniz manualClock ile kurulduysa).</summary>
        public float Now { get; private set; } = 100f;

        public event Action<string> Log;

        private CombatTestRig(Vector3 center, bool manualClock, float startHealthA, float startHealthB)
        {
            Center = center;
            _manualClock = manualClock;
            _root = new GameObject("Combat Test Rig");
            _root.transform.position = center;
            Gate = _root.AddComponent<LocalMatchGate>();

            A = CreateRobot("Kirmizi Robot", center + new Vector3(-DefaultSpacing / 2f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f),
                new Color(0.85f, 0.16f, 0.12f, 1f), startHealthA);
            B = CreateRobot("Mavi Robot", center + new Vector3(DefaultSpacing / 2f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f),
                new Color(0.12f, 0.38f, 0.9f, 1f), startHealthB);
        }

        public static CombatTestRig Create(Vector3 center, bool manualClock, float startHealthA = 100f, float startHealthB = 100f)
        {
            var rig = new CombatTestRig(center, manualClock, startHealthA, startHealthB);
            rig.NewMatch();
            return rig;
        }

        public CombatTestRobot Other(CombatTestRobot robot)
        {
            return robot == A ? B : A;
        }

        /// <summary>Yeni mac: robotlar baslangic yerine ve canina doner; bekleme ve hedef kayitlari sifirlanir; mac acilir.</summary>
        public void NewMatch()
        {
            foreach (CombatTestRobot robot in new[] { A, B })
            {
                robot.Root.transform.SetPositionAndRotation(robot.SpawnPosition, robot.SpawnRotation);
                SetStartHealth(robot.Health, robot.StartHealth);
                robot.Health.Restore();
                robot.Attack.ResetForNewMatch();
                robot.DefeatNotifications = 0;
            }

            Gate.SetOpen(true);
            Physics.SyncTransforms();
            Log?.Invoke(string.Format(CultureInfo.InvariantCulture, "Yeni mac: {0} {1:0} can, {2} {3:0} can",
                A.Name, A.StartHealth, B.Name, B.StartHealth));
        }

        public void ApplyProfile(CombatProfile profile)
        {
            A.Attack.ApplyProfile(profile);
            B.Attack.ApplyProfile(profile);
        }

        /// <summary>Zamani ilerletir ve suren saldiri pencerelerini isletir (elle zaman modunda).</summary>
        public void Advance(float seconds)
        {
            if (!_manualClock) throw new InvalidOperationException("Advance yalniz elle zaman modunda kullanilir.");
            Now += seconds;
            A.Attack.Tick();
            B.Attack.Tick();
        }

        /// <summary>Saldiri ister ve penceresi bitene kadar zamani ilerletir. Istek sonucunu dondurur.</summary>
        public AttackOutcome Swing(CombatTestRobot attacker)
        {
            AttackOutcome outcome = attacker.Attack.RequestAttack();
            if (outcome == AttackOutcome.Started)
            {
                const float step = 0.05f;
                for (float t = 0f; attacker.Attack.IsAttacking && t <= attacker.Attack.ActiveDuration + step; t += step)
                    Advance(step);
            }

            return outcome;
        }

        /// <summary>Robotu saldirana gore verilen mesafe ve aciya yerlestirir (0 derece = saldiranin tam onu).</summary>
        public void Place(CombatTestRobot target, CombatTestRobot attacker, float centerDistance, float angleDegrees, float targetYawOffset = 0f)
        {
            Transform attackerTransform = attacker.Root.transform;
            Vector3 direction = Quaternion.AngleAxis(angleDegrees, Vector3.up) * attackerTransform.forward;
            direction.y = 0f;
            direction.Normalize();
            Vector3 position = attackerTransform.position + direction * centerDistance;
            Quaternion facing = Quaternion.LookRotation(-direction, Vector3.up) * Quaternion.Euler(0f, targetYawOffset, 0f);
            target.Root.transform.SetPositionAndRotation(position, facing);
            Physics.SyncTransforms();
        }

        public void Dispose()
        {
            DestroyObject(_root);
            DestroyObject(A.Root);
            DestroyObject(B.Root);
        }

        private CombatTestRobot CreateRobot(string name, Vector3 position, Quaternion rotation, Color color, float startHealth)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, rotation);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            CreatePart(root.transform, PrimitiveType.Cube, "Govde", new Vector3(0f, 0.35f, 0f), Quaternion.identity,
                new Vector3(0.9f, 0.4f, BodyHalfLength * 2f), color, true);
            Color wheelColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Tekerlek Sol", new Vector3(-0.55f, 0.2f, 0f), Quaternion.Euler(0f, 0f, 90f),
                new Vector3(0.4f, 0.08f, 0.4f), wheelColor, true);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Tekerlek Sag", new Vector3(0.55f, 0.2f, 0f), Quaternion.Euler(0f, 0f, 90f),
                new Vector3(0.4f, 0.08f, 0.4f), wheelColor, true);
            // On yonu gosteren silah ucu; collider'i yok, sadece gorsel.
            CreatePart(root.transform, PrimitiveType.Cube, "Silah", new Vector3(0f, 0.45f, BodyHalfLength + 0.1f), Quaternion.identity,
                new Vector3(0.3f, 0.12f, 0.2f), new Color(0.9f, 0.8f, 0.2f, 1f), false);

            root.AddComponent<RobotHealth>();
            var attack = root.AddComponent<MeleeAttack>();
            var trigger = root.AddComponent<AttackTestTrigger>();
            trigger.Attack = attack;
            if (_manualClock) attack.Clock = () => Now;
            attack.Gate = Gate;

            var robot = new CombatTestRobot(name, root, position, rotation, startHealth);
            robot.Health.Defeated += defeated =>
            {
                robot.DefeatNotifications++;
                Log?.Invoke(robot.Name + " YENILDI (bildirim " + robot.DefeatNotifications + ")");
            };
            attack.TargetHit += (source, target, amount) =>
                Log?.Invoke(string.Format(CultureInfo.InvariantCulture, "{0} -> {1}: {2:0} hasar, kalan {3:0}",
                    robot.Name, target.gameObject.name, amount, target.CurrentHealth));
            return robot;
        }

        private static void CreatePart(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Quaternion localRotation,
            Vector3 scale, Color color, bool keepCollider)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = scale;
            if (!keepCollider) Object.DestroyImmediate(part.GetComponent<Collider>());

            Material material = GetMaterial(color);
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
        }

        // Senaryolar cok sayida robot kurdugu icin malzemeler renk basina bir kez olusturulur.
        private static readonly System.Collections.Generic.Dictionary<Color, Material> Materials =
            new System.Collections.Generic.Dictionary<Color, Material>();

        private static Material GetMaterial(Color color)
        {
            if (Materials.TryGetValue(color, out Material cached) && cached != null) return cached;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            var material = new Material(shader) { color = color, name = "Combat Test " + ColorUtility.ToHtmlStringRGB(color) };
            Materials[color] = material;
            return material;
        }

        /// <summary>Ortak RobotHealth'e dokunmadan, serilestirilmis baslangic canini ayarlar.</summary>
        private static void SetStartHealth(RobotHealth health, float value)
        {
            string json = "{\"maxHealth\":" + Mathf.Max(1f, value).ToString("R", CultureInfo.InvariantCulture) + "}";
            JsonUtility.FromJsonOverwrite(json, health);
        }

        private static void DestroyObject(GameObject target)
        {
            if (target != null) Object.DestroyImmediate(target);
        }
    }
}
