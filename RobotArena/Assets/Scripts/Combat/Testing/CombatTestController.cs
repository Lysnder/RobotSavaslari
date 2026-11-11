using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari.Combat.Testing
{
    /// <summary>
    /// Eray'in dovus deneme sahnesi (E1-E4). Iki robotu kurar, deneme dugmeleri ve
    /// klavye ile can, saldiri, bekleme, mac izni ve profil davranislarini gosterir.
    /// </summary>
    public sealed class CombatTestController : MonoBehaviour
    {
        [Header("E1 - Can")]
        [SerializeField, Min(1f)] private float redStartHealth = 100f;
        [SerializeField, Min(1f)] private float blueStartHealth = 100f;
        [Tooltip("'Test hasari' dugmesinin secili robota uyguladigi sabit hasar.")]
        [SerializeField, Min(0f)] private float testDamage = 25f;

        [Header("E3 - Profiller")]
        [SerializeField] private CombatProfile[] profiles;
        [SerializeField, Min(0)] private int profileIndex;

        [Header("Deneme hareketi")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0f)] private float turnSpeed = 140f;

        private const float ReferenceHeight = 720f;
        private const int MaxLogLines = 9;

        private readonly List<string> _log = new List<string>();
        private readonly Dictionary<CombatTestRobot, LineRenderer> _reachIndicators = new Dictionary<CombatTestRobot, LineRenderer>();
        private CombatTestRig _rig;
        private CombatTestRobot _selected;
        private List<CombatScenarios.Result> _results;
        private CombatProfile[] _runtimeProfiles;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _resultStyle;

        private CombatProfile[] Profiles => profiles != null && profiles.Any(p => p != null) ? profiles.Where(p => p != null).ToArray() : _runtimeProfiles;
        private CombatProfile CurrentProfile => Profiles[Mathf.Clamp(profileIndex, 0, Profiles.Length - 1)];

        private void Start()
        {
            _runtimeProfiles = new[]
            {
                CombatProfile.CreateRuntime("Profil 25", 25f),
                CombatProfile.CreateRuntime("Profil 40", 40f)
            };

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Zemin";
            ground.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            Material groundMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(0.32f, 0.34f, 0.38f, 1f));
            if (groundMaterial != null) ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 5.5f, -5.2f);
                camera.transform.LookAt(new Vector3(0f, 0.3f, 0.2f));
                camera.fieldOfView = 50f;
            }

            _rig = CombatTestRig.Create(Vector3.zero, false, redStartHealth, blueStartHealth);
            _rig.Log += AddLog;
            foreach (CombatTestRobot robot in new[] { _rig.A, _rig.B })
            {
                CombatTestRobot owner = robot;
                robot.Attack.Resolved += (attack, report) => AddLog(owner.Name + ": " + report.Describe());
                _reachIndicators[robot] = CreateReachIndicator(robot);
            }

            _rig.Gate.Changed += open => AddLog(open ? "Mac ACILDI" : "Mac KAPANDI");
            _selected = _rig.A;
            ApplyCurrentProfile();
        }

        private void OnDestroy()
        {
            _rig?.Dispose();
            if (_runtimeProfiles == null) return;
            foreach (CombatProfile profile in _runtimeProfiles)
                if (profile != null) Destroy(profile);
        }

        private void Update()
        {
            if (_rig == null) return;
            HandleKeyboard();
            foreach (KeyValuePair<CombatTestRobot, LineRenderer> pair in _reachIndicators)
                UpdateReachIndicator(pair.Key, pair.Value);
        }

        // ---------------- Deneme islemleri (dugme ve klavye ayni metotlari kullanir) ----------------

        private void ApplyTestDamage(CombatTestRobot robot)
        {
            float before = robot.Health.CurrentHealth;
            robot.Health.TakeDamage(testDamage);
            AddLog(string.Format(CultureInfo.InvariantCulture, "Test hasari {0:0} -> {1}: {2:0} -> {3:0}",
                testDamage, robot.Name, before, robot.Health.CurrentHealth));
        }

        private void NewMatch()
        {
            _rig.A.StartHealth = redStartHealth;
            _rig.B.StartHealth = blueStartHealth;
            _rig.NewMatch();
        }

        private void ToggleMatch()
        {
            _rig.Gate.SetOpen(!_rig.Gate.IsOpen);
        }

        private void NextProfile()
        {
            profileIndex = (profileIndex + 1) % Profiles.Length;
            ApplyCurrentProfile();
        }

        private void ApplyCurrentProfile()
        {
            CombatProfile profile = CurrentProfile;
            _rig.ApplyProfile(profile);
            AddLog(string.Format(CultureInfo.InvariantCulture, "Profil: {0} - {1:0} hasar, {2:0.##} m, {3:0.##} s bekleme; {4:0} cana {5} vurus",
                profile.name, profile.damage, profile.range, profile.cooldown, _rig.B.StartHealth, profile.HitsToDefeat(_rig.B.StartHealth)));
        }

        private void RunScenarios()
        {
            _results = CombatScenarios.RunAll();
            int passed = _results.Count(r => r.Passed);
            AddLog("Senaryolar: " + passed + "/" + _results.Count + " gecti");
            foreach (CombatScenarios.Result result in _results)
            {
                if (result.Passed) Debug.Log(result.ToString());
                else Debug.LogError(result.ToString());
            }
        }

        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.tabKey.wasPressedThisFrame) _selected = _rig.Other(_selected);
            if (keyboard.spaceKey.wasPressedThisFrame) _selected.Attack.RequestAttack();
            if (keyboard.hKey.wasPressedThisFrame) ApplyTestDamage(_selected);
            if (keyboard.mKey.wasPressedThisFrame) ToggleMatch();
            if (keyboard.pKey.wasPressedThisFrame) NextProfile();
            if (keyboard.rKey.wasPressedThisFrame) NewMatch();
            if (keyboard.tKey.wasPressedThisFrame) RunScenarios();

            float drive = 0f;
            float turn = 0f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) drive += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) drive -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) turn += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) turn -= 1f;
            if (drive == 0f && turn == 0f) return;

            Transform robot = _selected.Root.transform;
            robot.Rotate(0f, turn * turnSpeed * Time.deltaTime, 0f, Space.World);
            robot.position += robot.forward * (drive * moveSpeed * Time.deltaTime);
        }

        // ---------------- Ekran ----------------

        private void OnGUI()
        {
            if (_rig == null) return;
            EnsureStyles();
            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            GUI.Label(new Rect(0f, 8f, width, 30f), "ERAY - DOVUS DENEME SAHNESI (E1-E4)", _titleStyle);
            DrawToolbar(width);
            DrawRobotPanel(_rig.A, 12f, 92f);
            DrawRobotPanel(_rig.B, width - 332f, 92f);
            if (_results == null) DrawLog();
            GUI.Label(new Rect(12f, ReferenceHeight - 26f, width - 24f, 22f),
                "Tab: robot sec | WASD/Oklar: hareket | Space: saldiri (oyuncu tusu) | H: test hasari | M: mac ac/kapa | P: profil | R: yeni mac | T: senaryolar",
                _smallStyle);

            if (_results != null) DrawResults(width);
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawToolbar(float width)
        {
            CombatProfile profile = CurrentProfile;
            float x = (width - 760f) / 2f;
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = _rig.Gate.IsOpen ? new Color(0.4f, 1f, 0.45f) : new Color(1f, 0.4f, 0.35f);
            if (GUI.Button(new Rect(x, 44f, 170f, 34f), _rig.Gate.IsOpen ? "Mac: ACIK" : "Mac: KAPALI")) ToggleMatch();
            GUI.backgroundColor = previous;

            string profileText = string.Format(CultureInfo.InvariantCulture, "{0} ({1:0} hasar, {2} vurus)",
                profile.name, profile.damage, profile.HitsToDefeat(_rig.B.StartHealth));
            if (GUI.Button(new Rect(x + 180f, 44f, 250f, 34f), profileText)) NextProfile();
            if (GUI.Button(new Rect(x + 440f, 44f, 140f, 34f), "Yeni Mac")) NewMatch();
            if (GUI.Button(new Rect(x + 590f, 44f, 170f, 34f), "Senaryolari Calistir")) RunScenarios();
        }

        private void DrawRobotPanel(CombatTestRobot robot, float x, float y)
        {
            bool selected = robot == _selected;
            GUI.Box(new Rect(x, y, 320f, 236f), string.Empty);
            GUI.Label(new Rect(x + 12f, y + 6f, 296f, 24f), robot.Name + (selected ? "   [SECILI]" : string.Empty), _labelStyle);

            Rect bar = new Rect(x + 12f, y + 34f, 296f, 16f);
            Color previous = GUI.color;
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = robot == _rig.A ? new Color(0.85f, 0.16f, 0.12f, 1f) : new Color(0.12f, 0.38f, 0.9f, 1f);
            float fraction = Mathf.Clamp01(robot.Health.CurrentHealth / robot.Health.MaxHealth);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), Texture2D.whiteTexture);
            GUI.color = previous;

            MeleeAttack attack = robot.Attack;
            string state = attack.IsAttacking ? "SALDIRI!" :
                attack.CooldownRemaining > 0f ? string.Format(CultureInfo.InvariantCulture, "BEKLEME {0:0.00} s", attack.CooldownRemaining) : "HAZIR";
            GUI.Label(new Rect(x + 12f, y + 54f, 296f, 20f), string.Format(CultureInfo.InvariantCulture, "Can: {0:0} / {1:0}{2}",
                robot.Health.CurrentHealth, robot.Health.MaxHealth, robot.Health.IsDefeated ? "   YENILDI" : string.Empty), _smallStyle);
            GUI.Label(new Rect(x + 12f, y + 74f, 296f, 20f), "Yenilgi bildirimi: " + robot.DefeatNotifications, _smallStyle);
            GUI.Label(new Rect(x + 12f, y + 94f, 296f, 20f), "Silah: " + state, _smallStyle);
            GUI.Label(new Rect(x + 12f, y + 114f, 296f, 20f), "Son saldiri: " + attack.LastReport.Describe(), _smallStyle);
            GUI.Label(new Rect(x + 12f, y + 134f, 296f, 20f), "Hedefe mesafe: " + DistanceText(robot), _smallStyle);

            if (GUI.Button(new Rect(x + 12f, y + 160f, 140f, 30f), selected ? "Secili" : "Sec")) _selected = robot;
            if (GUI.Button(new Rect(x + 168f, y + 160f, 140f, 30f), string.Format(CultureInfo.InvariantCulture, "Test hasari -{0:0}", testDamage)))
                ApplyTestDamage(robot);
            if (GUI.Button(new Rect(x + 12f, y + 196f, 140f, 30f), "Saldir (dugme)")) robot.Trigger.Fire();
            if (GUI.Button(new Rect(x + 168f, y + 196f, 140f, 30f), robot.Trigger.AutoFire ? "Oto saldiri: ACIK" : "Oto saldiri: KAPALI"))
                robot.Trigger.AutoFire = !robot.Trigger.AutoFire;
        }

        private string DistanceText(CombatTestRobot robot)
        {
            CombatTestRobot other = _rig.Other(robot);
            float closest = float.MaxValue;
            bool inReach = false;
            foreach (Collider collider in other.Colliders)
            {
                closest = Mathf.Min(closest, Vector3.Distance(robot.Attack.Origin, collider.ClosestPoint(robot.Attack.Origin)));
                inReach |= robot.Attack.IsInReach(collider, out _);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.00} / {1:0.##} m  {2}",
                closest, robot.Attack.Range, inReach ? "MENZILDE" : "DISARIDA");
        }

        private void DrawLog()
        {
            float top = ReferenceHeight - 34f - MaxLogLines * 19f - 10f;
            GUI.Box(new Rect(12f, top, 470f, MaxLogLines * 19f + 10f), string.Empty);
            for (int i = 0; i < _log.Count; i++)
                GUI.Label(new Rect(20f, top + 5f + i * 19f, 456f, 19f), _log[i], _smallStyle);
        }

        // Sonuclar robot panellerinin altinda, iki sutun halinde gosterilir; acikken olay kaydi gizlenir.
        private void DrawResults(float width)
        {
            const float lineHeight = 17f;
            int rows = (_results.Count + 1) / 2;
            float x = 12f;
            float y = 336f;
            float panelWidth = width - 24f;
            float columnWidth = (panelWidth - 30f) / 2f;
            float height = 40f + rows * lineHeight;
            GUI.Box(new Rect(x, y, panelWidth, height), string.Empty);
            int passed = _results.Count(r => r.Passed);
            GUI.Label(new Rect(x + 10f, y + 6f, panelWidth - 120f, 22f),
                "Senaryo sonuclari: " + passed + "/" + _results.Count + (passed == _results.Count ? " GECTI" : " - HATA VAR"), _labelStyle);
            if (GUI.Button(new Rect(x + panelWidth - 100f, y + 6f, 90f, 24f), "Kapat")) _results = null;
            if (_results == null) return;

            Color previous = GUI.color;
            for (int i = 0; i < _results.Count; i++)
            {
                CombatScenarios.Result result = _results[i];
                GUI.color = result.Passed ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.5f, 0.45f);
                float column = i < rows ? 0f : 1f;
                float row = i < rows ? i : i - rows;
                GUI.Label(new Rect(x + 10f + column * (columnWidth + 10f), y + 34f + row * lineHeight, columnWidth, lineHeight),
                    new GUIContent(result.Passed ? "PASS [" + result.Card + "] " + result.Name : result.ToString(), result.ToString()), _resultStyle);
            }

            GUI.color = previous;
        }

        private void AddLog(string line)
        {
            _log.Add(string.Format(CultureInfo.InvariantCulture, "[{0,6:0.00}] {1}", Time.time, line));
            while (_log.Count > MaxLogLines) _log.RemoveAt(0);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _resultStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, clipping = TextClipping.Clip };
        }

        // ---------------- Menzil gostergesi ----------------

        private LineRenderer CreateReachIndicator(CombatTestRobot robot)
        {
            var go = new GameObject("Menzil Gostergesi");
            go.transform.SetParent(robot.Root.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.035f;
            line.positionCount = 26;
            line.sharedMaterial = CreateMaterial("Sprites/Default", Color.white);
            return line;
        }

        private static void UpdateReachIndicator(CombatTestRobot robot, LineRenderer line)
        {
            MeleeAttack attack = robot.Attack;
            Vector3 origin = attack.Origin;
            origin.y = 0.03f;
            float half = Mathf.Min(attack.ArcDegrees, 359f) * 0.5f;
            int arcPoints = line.positionCount - 2;
            line.SetPosition(0, origin);
            for (int i = 0; i < arcPoints; i++)
            {
                float angle = Mathf.Lerp(-half, half, i / (float)(arcPoints - 1));
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * robot.Root.transform.forward;
                direction.y = 0f;
                line.SetPosition(i + 1, origin + direction.normalized * attack.Range);
            }

            line.SetPosition(line.positionCount - 1, origin);
            Color color = robot.Health.IsDefeated ? new Color(0.4f, 0.4f, 0.4f, 0.5f) :
                attack.IsAttacking ? new Color(1f, 0.25f, 0.2f, 1f) :
                attack.CooldownRemaining > 0f ? new Color(1f, 0.65f, 0.2f, 0.6f) : new Color(1f, 0.95f, 0.3f, 0.9f);
            line.startColor = line.endColor = color;
        }

        private static Material CreateMaterial(string shaderName, Color color)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Standard");
            return shader != null ? new Material(shader) { color = color } : null;
        }
    }
}
