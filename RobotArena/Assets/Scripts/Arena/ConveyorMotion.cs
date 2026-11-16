using UnityEngine;

namespace RobotSavaslari.Arena
{
    // Remove only the previous surface contribution before RobotMotor (110) reads velocity.
    [DefaultExecutionOrder(105)]
    [DisallowMultipleComponent]
    public sealed class ConveyorMotion : MonoBehaviour
    {
        private void FixedUpdate() { ConveyorSurface.RemovePreviousMotion(); }
        private void OnDisable() { ConveyorSurface.RemovePreviousMotion(); }
    }
}
