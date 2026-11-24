using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Combat;
using RobotSavaslari.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotSavaslari.Gameplay.Tests
{
    public sealed partial class WorkshopDuelTests
    {
        [UnityTest]
        public IEnumerator ClampOwnersShareDriveLimitAndReleaseOnSeparationPauseAndRematch()
        {
            Apply(Parts("hard", "heavy", "clamp"));
            yield return StartDuel(DuelSetup.Quick());
            DisableDrivers(match);
            var owner = match.Player;
            var target = match.Opponent;
            var attack = owner.GetComponent<MeleeAttack>();
            var targetDrive = target.GetComponent<RobotDriveMotor>();
            var body = targetDrive.Body;
            var tick = new WaitForFixedUpdate();
            var auxiliary = new GameObject("Second gripper");
            auxiliary.AddComponent<Rigidbody>().useGravity = false;
            auxiliary.AddComponent<RobotHealth>();
            var second = auxiliary.AddComponent<MeleeAttack>();
            second.ApplyProfile(attack.Profile);
            float now = Time.time + 2f;
            attack.Clock = second.Clock = () => now;
            void PlaceForGrip()
            {
                float floor = GameplayTestScene.Floor;
                GameplayTestScene.Place(owner, new Vector3(0, floor, -1.65f), false, Quaternion.identity);
                GameplayTestScene.Place(target, new Vector3(0, floor, 0), false, Quaternion.Euler(0, 90, 0));
                GameplayTestScene.Place(auxiliary.transform, new Vector3(0, floor, 1.65f), false, Quaternion.Euler(0, 180, 0));
            }
            void Grab(MeleeAttack source)
            {
                source.ResetForNewMatch();
                Assert.That(source.RequestAttack(), Is.EqualTo(AttackOutcome.Started));
                now += source.Profile.windup + .01f;
                source.Tick();
                Assert.That(source.HoldingTarget && targetDrive.IsClamped, Is.True);
            }
            try
            {
                PlaceForGrip();
                Grab(attack);
                Grab(second);
                second.enabled = false;
                Assert.That(second.HoldingTarget, Is.False);
                Assert.That(attack.HoldingTarget && targetDrive.IsClamped, Is.True, "other holder remains");
                for (int i = 0; i < 11; i++) { targetDrive.Submit(1, 0); yield return tick; }
                float clampedSpeed = body.linearVelocity.magnitude;
                Assert.That(clampedSpeed, Is.InRange(.35f, 1.7f), "full throttle while gripped");

                targetDrive.ReleaseTraction(.32f, 0f);
                body.AddForce(Vector3.right * 12f, ForceMode.VelocityChange);
                yield return GameplayTestScene.FixedSteps(22);
                Assert.That(body.position.x, Is.GreaterThan(2f), "external impulse remains physical");
                Assert.That(attack.HoldingTarget || targetDrive.IsClamped, Is.False, "target left the jaw");
                PlaceForGrip();
                for (int i = 0; i < 18; i++) { targetDrive.Submit(1, 0); yield return tick; }
                Assert.That(body.linearVelocity.magnitude, Is.GreaterThan(clampedSpeed + 2f), "released throttle");

                PlaceForGrip();
                Grab(attack);
                Time.timeScale = 0f;
                yield return null;
                yield return null;
                Assert.That(attack.HoldingTarget || targetDrive.IsClamped, Is.False, "pause releases without fixed ticks");
                Time.timeScale = 1f;
                yield return null;
                yield return null;
                PlaceForGrip();
                Grab(attack);
                match.Defeat(false);
                Assert.That(attack.HoldingTarget || targetDrive.IsClamped, Is.False, "result releases synchronously");
                Assert.That(match.RequestCountdown(), Is.True);
                Assert.That(targetDrive.IsClamped, Is.False, "new encounter starts unrestricted");
            }
            finally
            {
                Time.timeScale = 1f;
                attack.Clock = second.Clock = null;
                Object.Destroy(auxiliary);
            }
        }
    }
}
