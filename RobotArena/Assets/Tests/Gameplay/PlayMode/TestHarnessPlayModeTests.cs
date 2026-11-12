using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RobotSavaslari.Combat;
using RobotSavaslari.Combat.Testing;
using RobotSavaslari.Match;
using RobotSavaslari.Match.Art;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>
    /// Development scenes and tools: the melee test scene, the modular robot art preview, health and
    /// attack components in real time, and the opt-in arena smoke run's command-line guard.
    /// </summary>
    public sealed class TestHarnessPlayModeTests : InputTestFixture
    {
        private Keyboard _keyboard;

        [UnityTearDown]
        public IEnumerator Cleanup() => GameplayTestScene.Unload();

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator MeleeTestSceneDrivesEveryActionFromTheKeyboard()
        {
            yield return GameplayTestScene.LoadEmptyScene(false);
            _keyboard = InputSystem.AddDevice<Keyboard>();
            var controller = new GameObject("Combat Test").AddComponent<CombatTestController>();
            yield return null;
            ImmediateGui.Draw(controller);
            RobotHealth red = GameObject.Find("Kirmizi Robot").GetComponent<RobotHealth>();
            RobotHealth blue = GameObject.Find("Mavi Robot").GetComponent<RobotHealth>();
            Assert.That(red, Is.Not.Null);
            Assert.That(blue, Is.Not.Null);

            yield return Tap(Key.H);
            Assert.That(red.CurrentHealth, Is.EqualTo(75f), "H: test damage to the selected (red) robot");
            yield return Tap(Key.Tab);
            yield return Tap(Key.H);
            Assert.That(blue.CurrentHealth, Is.EqualTo(75f), "Tab selects the other robot");
            yield return Tap(Key.Space);
            yield return new WaitForSeconds(0.3f);
            Assert.That(red.CurrentHealth, Is.EqualTo(50f), "Space: blue attacks red at the default spacing");
            yield return Tap(Key.M);
            yield return Tap(Key.Space);
            ImmediateGui.Draw(controller);
            yield return Tap(Key.M);
            yield return Tap(Key.P);
            yield return Tap(Key.R);
            Assert.That(red.CurrentHealth, Is.EqualTo(100f), "R: new match");

            Vector3 start = blue.transform.position;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W, Key.D));
            yield return new WaitForSeconds(0.2f);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.S, Key.A));
            yield return new WaitForSeconds(0.1f);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            Assert.That(Vector3.Distance(blue.transform.position, start), Is.GreaterThan(0.05f), "WASD moves the selected robot");

            LogAssert.ignoreFailingMessages = true; // a failing scenario is logged as an error by design
            yield return Tap(Key.T);
            LogAssert.ignoreFailingMessages = false;
            ImmediateGui.Draw(controller);
            red.TakeDamage(1000f);
            ImmediateGui.Draw(controller);
            Object.Destroy(controller.gameObject);
            yield return null;
            Assert.That(red == null, Is.True, "rig removed with the scene controller");
        }

        [UnityTest]
        public IEnumerator HealthReportsDamageDefeatAndRestore()
        {
            yield return GameplayTestScene.LoadEmptyScene(false);
            var health = new GameObject("Saglik").AddComponent<RobotHealth>();
            int changed = 0, defeated = 0, restored = 0;
            RobotHit last = default;
            health.HealthChanged += _ => changed++;
            health.Defeated += _ => defeated++;
            health.Damaged += hit => last = hit;
            health.Restored += () => restored++;

            health.TakeDamage(30f);
            Assert.That(health.CurrentHealth, Is.EqualTo(70f));
            Assert.That(last.Amount, Is.EqualTo(30f));
            Assert.That(last.Point, Is.EqualTo(health.transform.position + Vector3.up * 0.4f));
            foreach (float invalid in new[] { 0f, -5f, float.NaN, float.PositiveInfinity })
                health.TakeDamage(invalid);
            Assert.That(changed, Is.EqualTo(1), "invalid amounts are ignored");

            health.TakeDamage(500f, Vector3.one, Vector3.up, Vector3.zero, true);
            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(last.Amount, Is.EqualTo(70f), "only the remaining health is applied");
            Assert.That(last.Weapon, Is.True);
            Assert.That(health.IsDefeated, Is.True);
            health.TakeDamage(10f);
            Assert.That(defeated, Is.EqualTo(1), "defeat is reported once");

            health.Restore();
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
            Assert.That(restored, Is.EqualTo(1));
            Assert.That(health.IsDefeated, Is.False);
        }

        [UnityTest]
        public IEnumerator AttackComponentsRunOnTheirOwnInRealTime()
        {
            yield return GameplayTestScene.LoadEmptyScene(false);
            CombatTestRig rig = CombatTestRig.Create(new Vector3(0f, 0f, 50f), false);
            try
            {
                var gate = new GameObject("Kapi").AddComponent<LocalMatchGate>();
                int gateChanges = 0;
                gate.Changed += _ => gateChanges++;
                gate.SetOpen(true);
                Assert.That(gateChanges, Is.Zero, "same value is not a change");

                // A scene gate is used when no gate is assigned in code.
                MeleeAttack attack = rig.A.Attack;
                attack.Gate = null;
                GameplayTestScene.SetField(attack, "matchGate", gate);
                gate.SetOpen(false);
                Assert.That(gateChanges, Is.EqualTo(1));
                Assert.That(attack.RequestAttack(), Is.EqualTo(AttackOutcome.MatchClosed));
                gate.SetOpen(true);

                AttackReport resolved = default;
                attack.Resolved += (source, report) => resolved = report;
                AttackTestTrigger trigger = rig.A.Trigger;
                trigger.Attack = null; // found on the same object
                Assert.That(trigger.Fire(), Is.EqualTo(AttackOutcome.Started));
                Assert.That(trigger.Attack, Is.EqualTo(attack));
                yield return new WaitForSeconds(attack.ActiveDuration + 0.1f);
                Assert.That(resolved.Outcome, Is.EqualTo(AttackOutcome.Hit), "the physics tick closes the window");
                Assert.That(trigger.LastOutcome, Is.EqualTo(AttackOutcome.Started));

                trigger.AutoFire = true;
                Assert.That(trigger.AutoFire, Is.True);
                yield return new WaitForSeconds(0.3f);
                trigger.AutoFire = false;
                Assert.That(trigger.LastOutcome, Is.Not.EqualTo(AttackOutcome.None));

                // The attacker is defeated in the middle of its window.
                rig.B.Attack.ResetForNewMatch();
                Assert.That(rig.B.Attack.RequestAttack(), Is.EqualTo(AttackOutcome.Started).Or.EqualTo(AttackOutcome.Cooldown));
                rig.B.Health.TakeDamage(10000f);
                rig.B.Attack.Tick();
                Assert.That(rig.B.Attack.IsAttacking, Is.False);
                Assert.That(rig.B.Attack.RequestAttack(), Is.EqualTo(AttackOutcome.AttackerDefeated));

                var bare = new GameObject("Tetik").AddComponent<AttackTestTrigger>();
                Assert.That(bare.Fire(), Is.EqualTo(AttackOutcome.None), "no attack component");
                Object.Destroy(gate.gameObject);
            }
            finally { rig.Dispose(); }
        }

        [UnityTest]
        public IEnumerator ModularPreviewEquipsWeaponsAndArmour()
        {
            yield return GameplayTestScene.LoadEmptyScene(false);
            GameObject chassis = CreateAsset("Chassis", "WeaponMount_A", "WheelMount_FL", "WheelMount_FR", "WheelMount_RL", "WheelMount_RR",
                "ArmorMount_L", "ArmorMount_R");
            GameObject wheel = CreateAsset("Wheel");
            GameObject[] weapons = { CreateAsset("Disk", "Rotor"), CreateAsset("Drum", "Rotor_Main"), CreateAsset("Bar", "Rotor") };
            GameObject[] armours = { CreateAsset("Hafif"), CreateAsset("Agir") };

            var host = new GameObject("Preview");
            host.SetActive(false);
            var preview = host.AddComponent<ModularRobotPreview>();
            preview.Configure(chassis, wheel, weapons, armours);
            host.SetActive(true);
            yield return null;
            Assert.That(preview.HasWeapon, Is.True);
            Assert.That(preview.WeaponIndex, Is.Zero);
            Assert.That(host.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Wheel(Clone)")), Is.EqualTo(4));

            preview.EquipWeapon(2);
            GameplayTestScene.SetField(preview, "spinning", true);
            Transform rotor = host.GetComponentsInChildren<Transform>().First(t => t.name == "Rotor" && t.parent.name.StartsWith("Bar"));
            Quaternion before = rotor.localRotation;
            yield return null;
            yield return null;
            Assert.That(Quaternion.Angle(rotor.localRotation, before), Is.GreaterThan(0f), "rotor spins");
            preview.EquipWeapon(1);
            yield return null;
            preview.EquipWeapon(-1);
            Assert.That(preview.HasWeapon, Is.False);
            preview.EquipArmor(1);
            yield return null;
            Assert.That(host.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Agir")), Is.EqualTo(2));
            preview.EquipArmor(-1);
            yield return null;
            Assert.That(host.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Agir")), Is.Zero);
            ImmediateGui.Draw(preview);

            GameObject noRotor = CreateAsset("Kirik");
            preview.Configure(chassis, wheel, new[] { noRotor }, armours);
            Assert.Throws<InvalidOperationException>(() => preview.EquipWeapon(0), "weapon without a rotor pivot");
            Assert.Throws<InvalidOperationException>(() => ModularRobotPreview.Find(host.transform, "Yok"));
        }

        private static GameObject CreateAsset(string name, params string[] children)
        {
            var asset = new GameObject(name);
            foreach (string child in children) new GameObject(child).transform.SetParent(asset.transform, false);
            asset.transform.position = new Vector3(0f, -100f, 0f);
            return asset;
        }

        [Test]
        public void ArenaSmokeRunsOnlyWhenRequestedOnTheCommandLine()
        {
            MethodInfo launch = typeof(ArenaGameplaySmoke).GetMethod("Launch", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(launch, Is.Not.Null);
            Assume.That(Environment.GetCommandLineArgs(), Does.Not.Contain("-arena-smoke"));
            launch.Invoke(null, null);
            Assert.That(Object.FindAnyObjectByType<ArenaGameplaySmoke>(), Is.Null);
        }
    }
}
