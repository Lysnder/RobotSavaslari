using UnityEngine;

namespace RobotSavaslari
{
    public readonly struct RobotHit
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Vector3 Impulse;
        public readonly bool Weapon;

        public RobotHit(float amount, Vector3 point, Vector3 normal, Vector3 impulse, bool weapon)
        {
            Amount = amount;
            Point = point;
            Normal = normal;
            Impulse = impulse;
            Weapon = weapon;
        }
    }
}
