using System;
using UnityEngine;

namespace RobotSavaslari.Training
{
    [Serializable]
    public struct DrivingExerciseResult
    {
        public bool success;
        public float seconds;
        public int gateErrors;
        public int contacts;
        public int directionErrors;
        public float overshoot;
        public float yawError;
        public float positionError;
        public float finalSpeed;

        public long Faults => (long)gateErrors + contacts + directionErrors;
    }

    /// <summary>Measures the existing robot's attempt at the physics step.</summary>
    public sealed class DrivingExerciseSession
    {
        public const float TimeLimit = 600f;
        private readonly DrivingExerciseDefinition definition;
        private readonly TimeTrialCheckpoint[] route;
        private DrivingExerciseResult result;
        private float dwell;
        private bool wasInside;
        private bool reverseEntry;
        private Vector3 previousPosition;

        public DrivingExerciseSession(DrivingExerciseDefinition definition, TimeTrialCheckpoint[] route, Vector3 start)
        {
            this.definition = definition;
            this.route = route;
            previousPosition = start;
        }

        public DrivingExerciseResult Result => result;
        public bool Finished { get; private set; }
        public int NextGate { get; private set; }
        public float Dwell => dwell;
        public string Feedback { get; private set; }

        public void Step(Vector3 position, float yaw, Vector3 velocity, float seconds, int impacts)
        {
            if (Finished || seconds <= 0f) return;
            result.seconds = Mathf.Min(TimeLimit, result.seconds + seconds);
            result.contacts += impacts;
            if (impacts > 0) Feedback = "Sert temas: deneme geçersiz.";

            Vector3 previous = previousPosition;
            previousPosition = position;
            if (definition.kind == DrivingExerciseKind.Slalom)
                MeasureSlalom(previous, position);
            else
                MeasureTarget(position, yaw, velocity, seconds);

            if (!Finished && result.seconds >= TimeLimit)
            {
                Finished = true;
                Feedback = "10 dakikalık deneme sınırı doldu.";
            }
        }

        private void MeasureSlalom(Vector3 previous, Vector3 position)
        {
            while (NextGate < route.Length)
            {
                var gate = route[NextGate];
                if (previous.z > gate.position.z || position.z < gate.position.z || position.z <= previous.z)
                    return;

                float fraction = (gate.position.z - previous.z) / (position.z - previous.z);
                float crossingX = Mathf.Lerp(previous.x, position.x, fraction);
                if (Mathf.Abs(crossingX - gate.position.x) > gate.radius)
                {
                    result.gateErrors++;
                    Feedback = "Kapı kaçırıldı: deneme geçersiz.";
                }

                NextGate++;
            }

            Complete();
        }

        private void MeasureTarget(Vector3 position, float yaw, Vector3 velocity, float seconds)
        {
            var target = route[0];
            Vector3 offset = position - target.position;
            offset.y = 0f;
            velocity.y = 0f;
            result.positionError = offset.magnitude;
            result.yawError = Mathf.Abs(Mathf.DeltaAngle(yaw, definition.targetYaw));
            result.finalSpeed = velocity.magnitude;
            bool inside = result.positionError <= target.radius;
            bool slow = result.finalSpeed <= definition.stopSpeed;
            bool aligned = result.yawError <= definition.yawTolerance;

            switch (definition.kind)
            {
                case DrivingExerciseKind.ControlledStop:
                    Vector3 forward = Quaternion.Euler(0f, definition.startYaw, 0f) * Vector3.forward;
                    result.overshoot = Mathf.Max(result.overshoot, Vector3.Dot(offset, forward) - target.radius);
                    Hold(inside && slow, seconds);
                    break;
                case DrivingExerciseKind.PrecisionTurn:
                    if (inside && aligned) Complete();
                    break;
                case DrivingExerciseKind.ReverseParking:
                    if (inside && !wasInside)
                    {
                        Vector3 heading = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        reverseEntry = Vector3.Dot(velocity, heading) < -.05f;
                        if (!reverseEntry)
                        {
                            result.directionErrors++;
                            Feedback = "Bölgeye geri viteste gir; bu deneme geçersiz.";
                        }
                    }

                    Hold(inside && reverseEntry && aligned && slow, seconds);
                    break;
            }

            wasInside = inside;
        }

        private void Hold(bool qualifies, float seconds)
        {
            dwell = qualifies ? dwell + seconds : 0f;
            if (dwell + 1e-5f >= definition.dwellSeconds) Complete();
        }

        private void Complete()
        {
            Finished = true;
            result.success = result.Faults == 0;
            Feedback = result.success ? "Alıştırma tamamlandı." : "Alıştırma hatalarla tamamlandı.";
        }
    }
}
