using System.Collections;
using NUnit.Framework;
using RobotSavaslari.Robots;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace RobotSavaslari.Gameplay.Tests
{
    /// <summary>
    /// Shared motor and its command sources: scripted sequences, the generic keyboard input, the
    /// arena drive motor with damage conditions, and the motor's traction and stop rules.
    /// </summary>
    public sealed class RobotDrivingComponentsPlayModeTests : InputTestFixture
    {
        private Keyboard _keyboard;

        [UnityTearDown]
        public IEnumerator Cleanup() => GameplayTestScene.Unload();

        private static GameObject CreateRobot(string name, Vector3 position, bool withMotor = true)
        {
            var robot = new GameObject(name);
            robot.transform.position = position;
            var collider = robot.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.6f, 1.4f);
            collider.center = new Vector3(0f, 0.3f, 0f);
            robot.AddComponent<Rigidbody>().mass = 25f;
            if (withMotor) robot.AddComponent<RobotMotor>();
            return robot;
        }

        private void Hold(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator SequenceStepsThroughCommandsAndStopsWithoutLoop()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            GameObject robot = CreateRobot("Sirali", new Vector3(0f, 0.05f, 0f));
            var keyboardInput = robot.AddComponent<KeyboardRobotInput>();
            robot.SetActive(false);
            var sequence = robot.AddComponent<RobotCommandSequence>();
            GameplayTestScene.SetField(sequence, "loop", false);
            robot.SetActive(true);
            sequence.SetCommands(new[]
            {
                new TimedRobotCommand { duration = 0.1f, forward = 1f, turn = 0f },
                new TimedRobotCommand { duration = 0.1f, forward = 0f, turn = -1f },
            });
            Assert.That(sequence.Motor, Is.EqualTo(robot.GetComponent<RobotMotor>()));

            sequence.Play();
            Assert.That(sequence.IsPlaying, Is.True);
            Assert.That(keyboardInput.enabled, Is.False, "keyboard is overridden while the sequence plays");
            yield return null;
            Assert.That(sequence.Motor.MoveCommand, Is.EqualTo(1f));
            yield return new WaitForSeconds(0.12f);
            Assert.That(sequence.Motor.TurnCommand, Is.EqualTo(-1f).Or.EqualTo(0f));
            yield return new WaitForSeconds(0.25f);
            Assert.That(sequence.IsPlaying, Is.False, "no loop: stops at the end");
            Assert.That(keyboardInput.enabled, Is.True, "keyboard handed back");
            Assert.That(sequence.Motor.MoveCommand, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LoopingSequenceRepeatsUntilStoppedOrDisabled()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            GameObject robot = CreateRobot("Dongu", new Vector3(0f, 0.05f, 0f));
            var sequence = robot.AddComponent<RobotCommandSequence>();
            sequence.SetCommands(new[] { new TimedRobotCommand { duration = 0.05f, forward = 0.5f } });
            sequence.Play();
            yield return new WaitForSeconds(0.2f);
            Assert.That(sequence.IsPlaying, Is.True, "loop keeps playing");
            sequence.enabled = false;
            Assert.That(sequence.IsPlaying, Is.False, "disabling stops it");

            sequence.enabled = true;
            sequence.SetCommands(null);
            sequence.Play();
            Assert.That(sequence.IsPlaying, Is.False, "nothing to play");
            sequence.Motor = null;
            sequence.Play();
            Assert.That(sequence.IsPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator SequencePlaysOnStartWhenAsked()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            GameObject robot = CreateRobot("Baslangic", new Vector3(0f, 0.05f, 0f));
            robot.SetActive(false);
            var sequence = robot.AddComponent<RobotCommandSequence>();
            GameplayTestScene.SetField(sequence, "playOnStart", true);
            robot.SetActive(true);
            yield return null;
            Assert.That(sequence.IsPlaying, Is.True);
        }

        [UnityTest]
        public IEnumerator ComponentsWithoutAMotorReportAndDisableThemselves()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            var robot = new GameObject("Motorsuz");
            LogAssert.Expect(LogType.Error, "RobotCommandSequence: bagli bir RobotMotor bulunamadi.");
            Assert.That(robot.AddComponent<RobotCommandSequence>().enabled, Is.False);
            LogAssert.Expect(LogType.Error, "KeyboardRobotInput: bagli bir RobotMotor bulunamadi.");
            Assert.That(robot.AddComponent<KeyboardRobotInput>().enabled, Is.False);
            LogAssert.Expect(LogType.Error, "PlayerKeyboardDriver: bagli bir RobotMotor bulunamadi.");
            Assert.That(robot.AddComponent<Driving.PlayerKeyboardDriver>().enabled, Is.False);
            LogAssert.Expect(LogType.Error, "DriveProbe: bagli bir RobotMotor bulunamadi.");
            Assert.That(robot.AddComponent<Driving.DriveProbe>().enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator KeyboardInputSendsAxesAndStopsWhenKeyboardGoes()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            GameObject robot = CreateRobot("Klavye", new Vector3(0f, 0.05f, 0f));
            var input = robot.AddComponent<KeyboardRobotInput>();
            RobotMotor motor = robot.GetComponent<RobotMotor>();

            Hold(Key.W, Key.D);
            yield return null;
            Assert.That(motor.MoveCommand, Is.EqualTo(1f));
            Assert.That(motor.TurnCommand, Is.EqualTo(1f));
            Hold(Key.DownArrow, Key.LeftArrow);
            yield return null;
            Assert.That(motor.MoveCommand, Is.EqualTo(-1f));
            Assert.That(motor.TurnCommand, Is.EqualTo(-1f));

            GameObject other = CreateRobot("Diger", new Vector3(5f, 0.05f, 0f));
            input.Motor = other.GetComponent<RobotMotor>();
            Assert.That(motor.MoveCommand, Is.Zero, "previous motor released");
            input.Motor = input.Motor; // same motor: nothing to release

            InputSystem.RemoveDevice(_keyboard);
            yield return null;
            Assert.That(input.Motor.MoveCommand, Is.Zero, "no keyboard: no command");
            input.enabled = false;
        }

        [UnityTest]
        public IEnumerator MotorLosesTractionAfterARamAndRespectsDamage()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            GameObject robot = CreateRobot("Motor", new Vector3(0f, 0.05f, 0f));
            RobotMotor motor = robot.GetComponent<RobotMotor>();
            motor.SetMoveCommand(new RobotMoveCommand(2f, -3f));
            Assert.That(motor.MoveCommand, Is.EqualTo(1f));
            Assert.That(motor.TurnCommand, Is.EqualTo(-1f));
            Assert.That(motor.MaxForwardSpeed, Is.GreaterThan(0f));
            Assert.That(motor.MaxReverseSpeed, Is.GreaterThan(0f));
            Assert.That(motor.MaxTurnSpeed, Is.GreaterThan(0f));
            Assert.That(motor.Body, Is.EqualTo(robot.GetComponent<Rigidbody>()));

            motor.SetMoveCommand(1f, 0f);
            yield return GameplayTestScene.FixedSteps(20);
            float fullSpeed = motor.ForwardSpeed;
            Assert.That(fullSpeed, Is.GreaterThan(0.5f));

            motor.SetDriveCondition(0.5f, 0.5f, 0.3f, 0f);
            yield return GameplayTestScene.FixedSteps(5);
            Assert.That(motor.TurnSpeed, Is.Zero, "no steering without traction");
            Vector3 planar = Vector3.ProjectOnPlane(motor.Body.linearVelocity, Vector3.up);
            Assert.That(planar.magnitude, Is.LessThanOrEqualTo(fullSpeed + 0.01f), "sliding robot does not accelerate");

            motor.SetDriveCondition(0.5f, 0.5f, 0.3f, 1f);
            yield return GameplayTestScene.FixedSteps(30);
            Assert.That(motor.ForwardSpeed, Is.LessThan(motor.MaxForwardSpeed * 0.5f + 0.1f), "lost wheels cap the speed");
            Assert.That(Mathf.Abs(motor.TurnSpeed), Is.GreaterThan(0f), "a lost wheel pulls the steering");

            motor.ResetMotion();
            Assert.That(motor.ForwardSpeed, Is.Zero);
            Assert.That(motor.MoveCommand, Is.Zero);
            motor.Body.isKinematic = true;
            motor.SetMoveCommand(1f, 1f);
            yield return GameplayTestScene.FixedSteps(2);
            Assert.That(motor.ForwardSpeed, Is.Zero, "kinematic bodies are not driven");
            motor.ResetMotion();
            motor.enabled = false;
            Assert.That(motor.MoveCommand, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ArenaDriveMotorFeedsTheSharedMotorOrDrivesItself()
        {
            yield return GameplayTestScene.LoadEmptyScene();
            // Shared motor present: commands and damage conditions are forwarded.
            GameObject shared = CreateRobot("Ortak", new Vector3(0f, 0.05f, 0f));
            var forwarding = shared.AddComponent<RobotDriveMotor>();
            forwarding.Submit(3f, -0.5f);
            forwarding.Step(Time.fixedDeltaTime);
            Assert.That(forwarding.LastCommand, Is.EqualTo(new Vector2(1f, -0.5f)));
            Assert.That(shared.GetComponent<RobotMotor>().MoveCommand, Is.EqualTo(1f));
            forwarding.Step(Time.fixedDeltaTime);
            Assert.That(forwarding.LastCommand, Is.EqualTo(Vector2.zero), "no producer means braking");
            forwarding.Clear(true);

            // No shared motor: the legacy body drive.
            GameObject legacy = CreateRobot("Eski", new Vector3(6f, 0.05f, 0f), false);
            var drive = legacy.AddComponent<RobotDriveMotor>();
            Vector3 start = legacy.transform.position;
            for (int i = 0; i < 40; i++)
            {
                drive.Submit(1f, 0.3f);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(Vector3.Distance(legacy.transform.position, start), Is.GreaterThan(0.3f));
            Assert.That(Quaternion.Angle(legacy.transform.rotation, Quaternion.identity), Is.GreaterThan(1f));

            drive.ReleaseTraction(1f, 0.1f);
            drive.Submit(1f, 1f);
            Quaternion heading = legacy.transform.rotation;
            yield return new WaitForFixedUpdate();
            Assert.That(Quaternion.Angle(legacy.transform.rotation, heading), Is.LessThan(0.5f), "no steering while sliding");

            drive.Submit(-1f, 0f);
            yield return new WaitForFixedUpdate();
            drive.Clear(true);
            Assert.That(drive.Body.linearVelocity, Is.EqualTo(Vector3.zero));
            drive.enabled = false;
        }
    }
}
