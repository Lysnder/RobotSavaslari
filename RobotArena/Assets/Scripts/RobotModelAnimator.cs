using System.Collections.Generic;
using UnityEngine;

namespace RobotSavaslari
{
    [RequireComponent(typeof(RobotPrototypeController))]
    public sealed class RobotModelAnimator : MonoBehaviour
    {
        [SerializeField] private float wheelRadius = 0.22f;
        [SerializeField] private float spinnerSpeed = 900f;
        [SerializeField] private float liftAngle = 35f;
        private RobotPrototypeController _controller;
        private readonly List<Transform> _wheels = new List<Transform>();
        private readonly List<Quaternion> _wheelRest = new List<Quaternion>();
        private readonly List<Vector3> _wheelPrevious = new List<Vector3>();
        private readonly List<float> _wheelAngles = new List<float>();
        private Transform _spinner, _lifter;
        private Quaternion _spinnerRest, _lifterRest, _toolRest;
        private Transform _tool;
        private Combat.MeleeAttack _attack;
        private float _spin, _lift, _attackTime;

        private void Awake()
        {
            _controller = GetComponent<RobotPrototypeController>();
            _attack = GetComponent<Combat.MeleeAttack>();
            BindParts();
        }

        public void BindParts()
        {
            _wheels.Clear();
            _wheelRest.Clear();
            _wheelPrevious.Clear();
            _wheelAngles.Clear();
            _spinner = _lifter = _tool = null;
            if (!_attack) _attack = GetComponent<Combat.MeleeAttack>();
            _spin = _lift = _attackTime = 0f;
            Transform model = transform.Find("Robot Model");
            if (model == null) return;
            foreach (Transform part in model.GetComponentsInChildren<Transform>())
            {
                if (part.name.StartsWith("Wheel_"))
                {
                    _wheels.Add(part);
                    _wheelRest.Add(part.localRotation);
                    _wheelPrevious.Add(part.position);
                    _wheelAngles.Add(0f);
                }
                else if (part.name == "Spinner") { _spinner = part; _spinnerRest = part.localRotation; }
                else if (part.name == "Lifter") { _lifter = part; _lifterRest = part.localRotation; }
                else if (Combat.WorkshopWeapons.PartIndex(part.name) >= 2) { _tool = part; _toolRest = part.localRotation; }
            }
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _wheels.Count; i++)
            {
                var wheel = _wheels[i];
                if (!wheel.gameObject.activeSelf) continue;
                Vector3 movement = wheel.position - _wheelPrevious[i];
                _wheelPrevious[i] = wheel.position;
                // Actual wheel travel includes opposite motion during a pivot turn.
                if (movement.sqrMagnitude < 1f && !_controller.CombatStopped)
                    _wheelAngles[i] = Mathf.Repeat(_wheelAngles[i] + Vector3.Dot(movement, transform.forward) / wheelRadius * Mathf.Rad2Deg, 360f);
                Vector3 axle = wheel.parent.InverseTransformDirection(transform.right);
                wheel.localRotation = Quaternion.AngleAxis(_wheelAngles[i], axle) * _wheelRest[i];
            }
            bool active = _controller.WeaponActive && !_controller.CombatStopped;
            if (_tool && _tool.gameObject.activeSelf && _attack && _attack.Profile) AnimateTool(active);
            if (_spinner != null && _spinner.gameObject.activeSelf)
            {
                if (active) _spin = Mathf.Repeat(_spin + spinnerSpeed * Time.deltaTime, 360f);
                _spinner.localRotation = Quaternion.AngleAxis(_spin, _spinner.parent.InverseTransformDirection(transform.up)) * _spinnerRest;
            }
            if (_lifter != null && _lifter.gameObject.activeSelf)
            {
                _attackTime = active ? _attackTime + Time.deltaTime : 0f;
                float target = active ? Mathf.Sin(Mathf.Repeat(_attackTime, 1.2f) / 1.2f * Mathf.PI) * liftAngle : 0f;
                _lift = Mathf.MoveTowards(_lift, target, 100f * Time.deltaTime);
                _lifter.localRotation = Quaternion.AngleAxis(-_lift, _lifter.parent.InverseTransformDirection(transform.right)) * _lifterRest;
            }
        }

        private void AnimateTool(bool active)
        {
            var style = _attack.Profile.style;
            Vector3 axis = transform.right;
            if (style == Combat.WeaponStyle.Drum || style == Combat.WeaponStyle.Saw)
            {
                if (style == Combat.WeaponStyle.Saw) axis = transform.forward;
                if (active) _spin = Mathf.Repeat(_spin + (style == Combat.WeaponStyle.Saw ? 1180 : 900 * _attack.WindupFraction) * Time.deltaTime, 360f);
                _tool.localRotation = Quaternion.AngleAxis(_spin, _tool.parent.InverseTransformDirection(axis)) * _toolRest;
                return;
            }
            float target = 0f;
            if (style == Combat.WeaponStyle.Hammer && active) target = _attack.WindingUp ? -65f * _attack.WindupFraction : 29f;
            if (style == Combat.WeaponStyle.Flipper && active && !_attack.WindingUp) target = -58f;
            if (style == Combat.WeaponStyle.Clamp) target = _attack.HoldingTarget ? 18f : active ? -27f : 0f;
            _lift = Mathf.MoveTowards(_lift, target, (active ? 640f : 105f) * Time.deltaTime);
            _tool.localRotation = Quaternion.AngleAxis(_lift, _tool.parent.InverseTransformDirection(axis)) * _toolRest;
        }

        public void ResetPose()
        {
            _spin = _lift = _attackTime = 0f;
            for (int i = 0; i < _wheels.Count; i++)
            {
                _wheelAngles[i] = 0f;
                _wheels[i].localRotation = _wheelRest[i];
                _wheelPrevious[i] = _wheels[i].position;
            }
            if (_tool != null) _tool.localRotation = _toolRest;
            if (_spinner != null) _spinner.localRotation = _spinnerRest;
            if (_lifter != null) _lifter.localRotation = _lifterRest;
        }
    }
}
