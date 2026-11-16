using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Driving;
using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>
    /// The driving test rig with the real setting-set prefabs: spawning on routes, switching sets and
    /// routes by key and button, the probe that drives without a keyboard, collision reports, the
    /// follow camera and the telemetry text.
    /// </summary>
    public sealed class DrivingRigPlayModeTests : InputTestFixture
    {
        private const string StableVariantPath = "Assets/Prefabs/Driving/Robot_AyarA_Sabit.prefab";
        private const string AgileVariantPath = "Assets/Prefabs/Driving/Robot_AyarB_Cevik.prefab";

        private Keyboard _keyboard;
        private DriveRig _rig;
        private DriveTelemetryHud _hud;
        private TestCameraFollow _follow;
        private RouteCourse _first, _second;

        [UnityTearDown]
        public IEnumerator Cleanup() => GameplayTestScene.Unload();

        private static GameObject LoadPrefab(string path)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }

        private static RouteCourse CreateRoute(string name, Vector3 start, float yaw, string instructions, int gates)
        {
            var routeObject = new GameObject(name);
            var startPoint = new GameObject(name + " Start").transform;
            startPoint.SetPositionAndRotation(start, Quaternion.Euler(0f, yaw, 0f));
            var gateList = new Transform[gates + 1];
            for (int i = 0; i < gates; i++)
            {
                gateList[i] = new GameObject(name + " Gate " + i).transform;
                gateList[i].position = start + new Vector3(0f, 0f, 4f * (i + 1));
            }
            routeObject.SetActive(false);
            var route = routeObject.AddComponent<RouteCourse>();
            GameplayTestScene.SetField(route, "routeName", name);
            GameplayTestScene.SetField(route, "instructions", instructions);
            GameplayTestScene.SetField(route, "startPoint", startPoint);
            GameplayTestScene.SetField(route, "gates", gateList); // the last entry stays empty on purpose
            routeObject.SetActive(true);
            return route;
        }

        private IEnumerator CreateRig()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            GameObject stable = LoadPrefab(StableVariantPath), agile = LoadPrefab(AgileVariantPath);
            Assume.That(stable != null && agile != null, "driving prefabs are available in the Editor");

            _first = CreateRoute("Duz", new Vector3(0f, 0.1f, 0f), 0f, "Duz git", 2);
            _second = CreateRoute("Viraj", new Vector3(10f, 0.1f, 0f), 90f, "", 1);
            _follow = Camera.main.gameObject.AddComponent<TestCameraFollow>();
            _hud = new GameObject("HUD").AddComponent<DriveTelemetryHud>();

            var rigObject = new GameObject("Rig");
            rigObject.SetActive(false);
            _rig = rigObject.AddComponent<DriveRig>();
            GameplayTestScene.SetField(_rig, "settingSets", new[]
            {
                new DriveSettingSet { setName = "Sabit", robotPrefab = stable },
                new DriveSettingSet { setName = "Cevik", robotPrefab = agile },
            });
            GameplayTestScene.SetField(_rig, "routes", new[] { _first, _second });
            GameplayTestScene.SetField(_rig, "cameraFollow", _follow);
            GameplayTestScene.SetField(_rig, "hud", _hud);
            rigObject.SetActive(true);
            yield return null;
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpawnsTheFirstSetOnTheFirstRoute()
        {
            yield return CreateRig();
            Assert.That(_rig.ActiveSet.setName, Is.EqualTo("Sabit"));
            Assert.That(_rig.ActiveRoute, Is.EqualTo(_first));
            Assert.That(_rig.Motor, Is.Not.Null);
            Assert.That(_rig.Driver, Is.Not.Null);
            Assert.That(_rig.Probe, Is.Not.Null);
            Assert.That(_rig.Reporter, Is.Not.Null);
            Assert.That(Vector3.Distance(_rig.Motor.transform.position, _first.StartPosition), Is.LessThan(0.2f));
            Assert.That(_follow.Target, Is.EqualTo(_rig.Motor.transform));
            Assert.That(_first.RouteName, Is.EqualTo("Duz"));
            Assert.That(_first.Instructions, Is.EqualTo("Duz git"));
            Assert.That(_first.GateCount, Is.EqualTo(3));
            yield return new WaitForSeconds(0.2f);
            Assert.That(_rig.RunTime, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator KeysSwitchRoutesSetsAndControl()
        {
            yield return CreateRig();
            yield return Tap(Key.Tab);
            Assert.That(_rig.ActiveRoute, Is.EqualTo(_second), "Tab: next route");
            Assert.That(Vector3.Distance(_rig.Motor.transform.position, _second.StartPosition), Is.LessThan(0.2f));
            yield return Tap(Key.Digit2);
            Assert.That(_rig.ActiveSet.setName, Is.EqualTo("Cevik"), "2: second set on the same route");
            Assert.That(_rig.ActiveRoute, Is.EqualTo(_second));
            yield return Tap(Key.Digit1);
            Assert.That(_rig.ActiveSet.setName, Is.EqualTo("Sabit"));
            yield return Tap(Key.C);
            Assert.That(_rig.Driver.enabled, Is.False, "C: control off");
            yield return Tap(Key.C);
            Assert.That(_rig.Driver.enabled, Is.True);
            GameObject before = _rig.Motor.gameObject;
            yield return Tap(Key.R);
            Assert.That(before == null, Is.True, "R: robot respawned");
            Assert.That(_rig.RunTime, Is.LessThan(0.2f));
            yield return Tap(Key.Tab);
            Assert.That(_rig.ActiveRoute, Is.EqualTo(_first), "routes wrap around");
        }

        [UnityTest]
        public IEnumerator ButtonsResetAndAdvanceAndToggleControl()
        {
            yield return CreateRig();
            const float width = 190f;
            float x = Screen.width - width - 12f;
            ImmediateGui.Click(_rig, new Rect(x + 10f, 52f, width - 20f, 26f).center);
            Assert.That(_rig.ActiveRoute, Is.EqualTo(_second), "next route button");
            ImmediateGui.Click(_rig, new Rect(x + 10f, 82f, width - 20f, 26f).center);
            Assert.That(_rig.Driver.enabled, Is.False, "control button");
            ImmediateGui.Click(_rig, new Rect(x + 10f, 22f, width - 20f, 26f).center);
            Assert.That(_rig.Driver.enabled, Is.True, "reset spawns a fresh robot");
        }

        [UnityTest]
        public IEnumerator ProbeDrivesTheSameMotorWithoutAKeyboard()
        {
            yield return CreateRig();
            DriveProbe probe = _rig.Probe;
            yield return Tap(Key.P);
            Assert.That(probe.IsDriving, Is.True);
            Assert.That(_rig.Driver.enabled, Is.False, "keyboard driver handed over");
            yield return null;
            Assert.That(_rig.Motor.MoveCommand, Is.EqualTo(probe.ProbeForward));
            Assert.That(_rig.Motor.TurnCommand, Is.EqualTo(probe.ProbeTurn));
            ImmediateGui.Draw(_hud);

            probe.StartDriving(); // already driving: no change
            probe.Toggle();
            Assert.That(probe.IsDriving, Is.False);
            Assert.That(_rig.Driver.enabled, Is.True);
            Assert.That(_rig.Motor.MoveCommand, Is.Zero);
            probe.StopDriving(); // already stopped

            probe.Toggle();
            probe.enabled = false;
            Assert.That(probe.IsDriving, Is.False, "disabling stops the probe");
        }

        [UnityTest]
        public IEnumerator TelemetryDescribesEveryInputSource()
        {
            yield return CreateRig();
            _hud.Bind(_rig);
            ImmediateGui.Draw(_hud); // keyboard
            _rig.Driver.SendMessage("OnApplicationFocus", false);
            Assert.That(_rig.Driver.HasFocus, Is.False);
            ImmediateGui.Draw(_hud); // out of focus
            _rig.Driver.SendMessage("OnApplicationFocus", true);
            _rig.ToggleControl();
            ImmediateGui.Draw(_hud); // control off
            _hud.Bind(null);
            ImmediateGui.Draw(_hud);
        }

        [UnityTest]
        public IEnumerator CollisionIsReportedWithSpeedAngleAndResidual()
        {
            yield return CreateRig();
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Duvar";
            wall.transform.position = _first.StartPosition + new Vector3(0f, 0.5f, 3f);
            wall.transform.localScale = new Vector3(6f, 1f, 0.3f);
            CollisionReporter reporter = _rig.Reporter;
            Assert.That(reporter.TimeSinceImpact, Is.EqualTo(-1f));
            _rig.Probe.StartDriving();
            GameplayTestScene.SetField(_rig.Probe, "probeTurn", 0f);
            float deadline = Time.time + 4f;
            yield return new WaitUntil(() => reporter.HasReport || Time.time > deadline);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(reporter.HasReport, Is.True, "drove into the wall");
            Assert.That(reporter.ImpactTarget, Is.EqualTo("Duvar"));
            Assert.That(reporter.ImpactSpeed, Is.GreaterThan(0.4f));
            Assert.That(reporter.ImpactAngle, Is.LessThan(30f), "head-on");
            Assert.That(reporter.TimeSinceImpact, Is.GreaterThanOrEqualTo(0f));
            ImmediateGui.Draw(_hud);
            reporter.Clear();
            Assert.That(reporter.HasReport, Is.False);
            Assert.That(reporter.ImpactTarget, Is.Empty);
        }

        [UnityTest]
        public IEnumerator FollowCameraTracksPositionNotRotation()
        {
            yield return CreateRig();
            yield return new WaitForSeconds(0.6f);
            Vector3 expected = _rig.Motor.transform.position + new Vector3(0f, 14f, -14f);
            Assert.That(Vector3.Distance(_follow.transform.position, expected), Is.LessThan(0.5f));
            GameplayTestScene.SetField(_follow, "smoothTime", 0f);
            yield return null;
            Assert.That(Vector3.Distance(_follow.transform.position, _rig.Motor.transform.position + new Vector3(0f, 14f, -14f)), Is.LessThan(1e-3f));
            _follow.Target = null;
            Vector3 still = _follow.transform.position;
            yield return null;
            Assert.That(_follow.transform.position, Is.EqualTo(still));
        }

        [UnityTest]
        public IEnumerator MissingSetsOrPrefabsAreReported()
        {
            yield return CreateRig();
            GameplayTestScene.SetField(_rig, "settingSets", new[] { new DriveSettingSet { setName = "Bos" } });
            LogAssert.Expect(LogType.Error, "DriveRig: 'Bos' setinde prefab yok.");
            _rig.Spawn(0, 0);
            GameplayTestScene.SetField(_rig, "settingSets", new DriveSettingSet[0]);
            LogAssert.Expect(LogType.Error, "DriveRig: ayar seti tanimli degil.");
            _rig.Spawn(0, 0);
            Assert.That(_rig.ActiveSet, Is.Null);
            GameplayTestScene.SetField(_rig, "routes", new RouteCourse[0]);
            _rig.NextRoute();
            Assert.That(_rig.ActiveRoute, Is.Null);
        }

        [UnityTest]
        public IEnumerator RouteWithoutStartPointUsesItsOwnTransform()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            var route = new GameObject("Rota").AddComponent<RouteCourse>();
            route.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 45f, 0f));
            Assert.That(route.StartPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(Quaternion.Angle(route.StartRotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.01f));
            Assert.That(route.GateCount, Is.Zero);
            GameplayTestScene.SetField(route, "gates", null);
            Assert.That(route.GateCount, Is.Zero);
        }
    }
}
