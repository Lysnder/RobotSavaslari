using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using RobotSavaslari.Combat;
using RobotSavaslari.Arena;
using RobotSavaslari.Driving;
using RobotSavaslari.Match;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using State = RobotSavaslari.Match.MatchController.MatchState;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>
    /// The arena's real robots: keyboard driving and weapon, ram damage, arena limits, selection,
    /// regional damage with detachable parts, smoke, and the model animation.
    /// </summary>
    public sealed class ArenaRobotPlayModeTests : InputTestFixture
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return GameplayTestScene.Unload();
            if (keyboardRecords != null && Directory.Exists(keyboardRecords)) Directory.Delete(keyboardRecords, true);
            keyboardRecords = null;
        }

        private string keyboardRecords;

        private Keyboard _keyboard;
        private static MatchController Match => GameplayTestScene.Match;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        private void Hold(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        /// <summary>
        /// A batch-mode run has no focused window, so the keyboard driver starts unfocused; tell it the
        /// game has focus, as the arena smoke run does.
        /// </summary>
        private static void FocusPlayer() => Match.Player.gameObject.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);

        /// <summary>Player 2.2 m west of the opponent, both facing each other, bodies free to move.</summary>
        private static void FaceOff()
        {
            GameplayTestScene.Place(Match.Player, new Vector3(-1.1f, GameplayTestScene.Floor, 0f), false, Quaternion.Euler(0f, 90f, 0f));
            GameplayTestScene.Place(Match.Opponent, new Vector3(1.1f, GameplayTestScene.Floor, 0f), true, Quaternion.Euler(0f, -90f, 0f));
        }

        [UnityTest]
        public IEnumerator KeyboardDrivesTheRobotAndSpaceSwingsTheWeapon()
        {
            keyboardRecords = Path.Combine(Application.temporaryCachePath, "KeyboardArena-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(keyboardRecords);
            yield return GameplayTestScene.LoadMainArena(keyboardRecords);
            Assert.That(Match.EquippedPlayerId, Is.EqualTo(DuelSetup.Spinner));
            yield return GameplayTestScene.StartPlaying();
            FocusPlayer();
            Match.Opponent.GetComponent<RobotBotController>().SetMatchRunning(false);
            // Measure keyboard driving at a distance, then position the robots for a stationary weapon contact.
            GameplayTestScene.Place(Match.Player, new Vector3(-3f, GameplayTestScene.Floor, 0f), false, Quaternion.Euler(0f, 90f, 0f));
            GameplayTestScene.Place(Match.Opponent, new Vector3(3f, GameplayTestScene.Floor, 0f), true, Quaternion.Euler(0f, -90f, 0f));
            Vector3 driveStart = Match.Player.GetComponent<Rigidbody>().position;
            for (int i = 0; i < 25; i++)
            {
                Hold(Key.W);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(Vector3.Distance(Match.Player.GetComponent<Rigidbody>().position, driveStart), Is.GreaterThan(0.2f), "W drives forward without waiting for melee damage");
            Hold();
            GameplayTestScene.Place(Match.Player, new Vector3(0f, GameplayTestScene.Floor, -.82f), false, Quaternion.identity);
            GameplayTestScene.Place(Match.Opponent, new Vector3(0f, GameplayTestScene.Floor, .82f), false, Quaternion.Euler(0f, 180f, 0f));
            var opponentHealth = Match.Opponent.GetComponent<RobotHealth>();
            Transform spinner = Match.Player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Spinner");
            Quaternion spinnerRest = spinner != null ? spinner.localRotation : Quaternion.identity;

            bool sawWeapon = false;
            int attackStarts = 0, weaponHits = 0;
            var melee = Match.Player.GetComponent<MeleeAttack>();
            Assert.That(melee.IsInReach(Match.Opponent.GetComponent<BoxCollider>(), out _), Is.True, "opponent inside the stock swing arc");
            System.Action<MeleeAttack> onStart = _ => attackStarts++;
            System.Action<MeleeAttack, RobotHealth, float> onHit = (_, target, __) => { if (target == opponentHealth) weaponHits++; };
            melee.Started += onStart; melee.TargetHit += onHit;
            try
            {
                float deadline = Time.time + 3f;
                while (Time.time < deadline && (weaponHits == 0 || spinner != null && Quaternion.Angle(spinner.localRotation, spinnerRest) <= .1f))
                {
                    Hold(Key.Space);
                    yield return new WaitForFixedUpdate();
                    sawWeapon |= Match.Player.WeaponActive;
                }
            }
            finally
            {
                Hold();
                melee.Started -= onStart; melee.TargetHit -= onHit;
            }
            Assert.That(attackStarts, Is.GreaterThan(0), "Space starts a real attack");
            Assert.That(weaponHits, Is.GreaterThan(0), "weapon damage is distinct from ram damage");
            Assert.That(opponentHealth.CurrentHealth, Is.LessThan(opponentHealth.MaxHealth), "weapon damaged the opponent");
            Assert.That(sawWeapon, Is.True, "Space activates the weapon");
            if (spinner != null)
                Assert.That(Quaternion.Angle(spinner.localRotation, spinnerRest), Is.GreaterThan(0.1f), "spinner animated");
            Assert.That(Match.Player.GetComponent<MeleeAttack>().LastReport.Outcome, Is.Not.EqualTo(AttackOutcome.None));
        }

        [UnityTest]
        public IEnumerator RammingDealsMassScaledDamageAndKnocksBack()
        {
            yield return GameplayTestScene.LoadMainArena();
            yield return GameplayTestScene.StartPlaying();
            FaceOff();
            GameplayTestScene.Place(Match.Opponent, Match.Opponent.transform.position, false);
            Match.Opponent.GetComponent<RobotBotController>().SetMatchRunning(false);
            RobotHit? ram = null;
            System.Action<RobotHit> onHit = hit => { if (!hit.Weapon) ram = hit; };
            Match.Opponent.GetComponent<RobotHealth>().Damaged += onHit;
            try
            {
                for (int i = 0; i < 60 && ram == null; i++)
                {
                    Match.Player.GetComponent<Rigidbody>().linearVelocity = Match.Player.transform.forward * 5f;
                    yield return new WaitForFixedUpdate();
                }
            }
            finally { Match.Opponent.GetComponent<RobotHealth>().Damaged -= onHit; }
            Assert.That(ram.HasValue, Is.True, "ram registered");
            Assert.That(ram.Value.Amount, Is.GreaterThan(0f));
            Assert.That(ram.Value.Impulse.sqrMagnitude, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator RobotsAreKeptInsideTheArenaWalls()
        {
            yield return GameplayTestScene.LoadMainArena();
            yield return GameplayTestScene.StartPlaying();
            GameplayTestScene.Place(Match.Player, new Vector3(9f, GameplayTestScene.Floor, -8f), false);
            Match.Player.GetComponent<Rigidbody>().linearVelocity = new Vector3(3f, 0f, -3f);
            Match.Player.ConstrainToArena();
            Vector3 position = Match.Player.GetComponent<Rigidbody>().position;
            var bounds = Object.FindFirstObjectByType<ArenaBounds>();
            Assert.That(bounds.GridSize, Is.EqualTo(new Vector2Int(8, 8)), "visible 8 by 8 grid");
            Assert.That(bounds.CellSize, Is.EqualTo(2f), "two metre cells");
            Vector2 limits = bounds.CenterLimits(Match.Player.ArenaFootprintRadius);
            Assert.That(position.x, Is.EqualTo(bounds.Center.x + limits.x).Within(0.001f));
            Assert.That(position.z, Is.EqualTo(bounds.Center.y - limits.y).Within(0.001f));
            Vector3 velocity = Match.Player.GetComponent<Rigidbody>().linearVelocity;
            Assert.That(velocity.x, Is.Zero, "no velocity into the east wall");
            Assert.That(velocity.z, Is.Zero, "no velocity into the south wall");
        }

        [UnityTest]
        public IEnumerator SelectionIgnoresTheComputerAndFollowsEnableState()
        {
            yield return GameplayTestScene.LoadMainArena();
            yield return GameplayTestScene.StartPlaying();
            RobotPrototypeController.Select(Match.Player);
            Assert.That(Match.Player.IsSelected, Is.True);
            RobotPrototypeController.Select(Match.Opponent);
            Assert.That(RobotPrototypeController.Selected, Is.EqualTo(Match.Player), "computer robots cannot be selected");
            yield return null;
            Assert.That(Match.Player.transform.Find("Selection Ring").GetComponent<LineRenderer>().enabled, Is.True);

            Match.Player.enabled = false;
            Assert.That(RobotPrototypeController.Selected, Is.Null, "disabled robot is deselected");
            Assert.That(Match.Player.GetComponent<PlayerKeyboardDriver>().enabled, Is.False);
            Match.Player.enabled = true;
            Assert.That(Match.Player.GetComponent<PlayerKeyboardDriver>().enabled, Is.True);

            Match.Opponent.enabled = false;
            Assert.That(Match.Opponent.GetComponent<RobotBotController>().enabled, Is.False);
            Match.Opponent.enabled = true;
            Assert.That(Match.Opponent.GetComponent<RobotBotController>().enabled, Is.True);
            Assert.That(Match.Opponent.GetComponent<RobotBotController>().MatchRunning, Is.True);
        }

        [UnityTest]
        public IEnumerator StoppedCombatClosesTheWeaponGate()
        {
            yield return GameplayTestScene.LoadMainArena();
            yield return GameplayTestScene.StartPlaying();
            IMatchGate gate = Match.Player;
            Assert.That(gate.IsOpen, Is.True);
            Match.Player.StopCombat();
            Assert.That(gate.IsOpen, Is.False);
            Assert.That(Match.Player.WeaponActive, Is.False);
            Assert.That(Match.Player.GetComponent<PlayerKeyboardDriver>().enabled, Is.False);
            Match.Player.TickCombatControls();
            Assert.That(Match.Player.GetComponent<Rigidbody>().linearVelocity, Is.EqualTo(Vector3.zero));
        }

        [UnityTest]
        public IEnumerator PartsDetachWearShowsAndANewMatchRepairsEverything()
        {
            yield return GameplayTestScene.LoadMainArena();
            yield return GameplayTestScene.StartPlaying();
            GameplayTestScene.SeparateRobots();
            RobotPrototypeController robot = Match.Opponent;
            var visuals = robot.GetComponent<RobotDamageVisuals>();
            var health = robot.GetComponent<RobotHealth>();
            Assert.That(visuals.Parts.Count, Is.EqualTo(10), "imported mesh split into body, wheels, weapon and armour");
            Assert.That(visuals.OriginalTriangleCount, Is.GreaterThan(0));

            foreach (RobotDamageVisuals.PartKind kind in new[] { RobotDamageVisuals.PartKind.Wheel, RobotDamageVisuals.PartKind.Armor, RobotDamageVisuals.PartKind.Weapon })
            {
                RobotDamageVisuals.Part part = visuals.Parts.First(p => p.Kind == kind && !p.Detached);
                health.TakeDamage(part.Threshold + 0.5f, part.Transform.position, Vector3.up, Vector3.right, kind == RobotDamageVisuals.PartKind.Weapon);
                Assert.That(part.Detached, Is.True, kind + " detached");
                Assert.That(part.Transform.gameObject.activeSelf, Is.False);
                yield return new WaitForSeconds(0.1f);
            }
            Assert.That(visuals.DetachedCount, Is.EqualTo(3));
            Assert.That(visuals.LostWheels, Is.EqualTo(1));
            Assert.That(visuals.Mobility, Is.LessThan(1f));
            Assert.That(visuals.Turning, Is.LessThan(1f));
            Assert.That(visuals.SteeringPull, Is.Not.Zero);
            Assert.That(visuals.WeaponOperational, Is.False);
            Assert.That(((IMatchGate)robot).IsOpen, Is.False, "no attacks without a weapon");
            Assert.That(visuals.Parts.Any(p => !p.Detached && p.Wear.enabled), Is.True, "wear overlay shown on damaged parts");
            string debrisPrefix = robot.name + " Debris ";
            Assert.That(Object.FindObjectsByType<Rigidbody>().Count(b => b.name.StartsWith(debrisPrefix)), Is.EqualTo(3));

            // Low health keeps smoking.
            yield return new WaitForSeconds(0.6f);
            ParticleSystem smoke = robot.transform.Find("Damage Smoke").GetComponent<ParticleSystem>();
            Assert.That(health.CurrentHealth / health.MaxHealth, Is.LessThanOrEqualTo(0.35f));
            Assert.That(smoke.particleCount, Is.GreaterThan(0), "damage smoke");

            Match.Defeat(false);
            Assert.That(Match.RequestCountdown(), Is.True);
            yield return null;
            Assert.That(visuals.DetachedCount, Is.Zero);
            Assert.That(visuals.WeaponOperational, Is.True);
            Assert.That(visuals.Parts.All(p => !p.Detached && p.Damage == 0f && p.Transform.gameObject.activeSelf && !p.Wear.enabled), Is.True);
            yield return null;
            Assert.That(Object.FindObjectsByType<Rigidbody>().Count(b => b.name.StartsWith(debrisPrefix)), Is.Zero, "debris cleared");
        }

        [UnityTest]
        public IEnumerator WheelsTurnWhileTheRobotDrives()
        {
            yield return GameplayTestScene.LoadMainArena();
            yield return GameplayTestScene.StartPlaying();
            GameplayTestScene.Place(Match.Opponent, new Vector3(4.5f, GameplayTestScene.Floor, 3.5f), true);
            GameplayTestScene.Place(Match.Player, new Vector3(-3f, GameplayTestScene.Floor, -2f), false, Quaternion.Euler(0f, 90f, 0f));
            FocusPlayer();
            Transform wheel = Match.Player.GetComponentsInChildren<Transform>(true).First(t => t.name.StartsWith("Wheel_"));
            Quaternion rest = wheel.localRotation;
            for (int i = 0; i < 25; i++)
            {
                Hold(Key.UpArrow, Key.RightArrow);
                yield return new WaitForFixedUpdate();
            }
            Hold();
            yield return null;
            Assert.That(Quaternion.Angle(wheel.localRotation, rest), Is.GreaterThan(1f));
            Match.Player.GetComponent<RobotModelAnimator>().ResetPose();
            Assert.That(Quaternion.Angle(wheel.localRotation, rest), Is.LessThan(0.01f));
        }
    }
}
