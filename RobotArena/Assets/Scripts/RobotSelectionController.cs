using UnityEngine;
using UnityEngine.InputSystem;

namespace RobotSavaslari
{
    public sealed class RobotSelectionController : MonoBehaviour
    {
        private RobotPrototypeController[] _robots;

        private void Start()
        {
            _robots = FindObjectsByType<RobotPrototypeController>(FindObjectsSortMode.None);
            foreach (var robot in _robots)
                if (robot.name.StartsWith("Mavi")) RobotPrototypeController.Select(robot);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Camera.main == null) return;
            Vector2 point = mouse.position.ReadValue();
            var match = GetComponent<ArenaMatchController>();
            if (match != null && match.BlocksRobotSelection(point)) return;
            SelectFromRay(Camera.main.ScreenPointToRay(point));
        }

        public void SelectFromRay(Ray ray)
        {
            RobotPrototypeController nearest = null;
            float distance = float.PositiveInfinity;
            // Pick robot colliders directly so the cage wires don't block selection.
            foreach (var robot in _robots)
            {
                if (robot == null || !robot.isActiveAndEnabled || robot.IsComputer) continue;
                foreach (var collider in robot.GetComponentsInChildren<Collider>())
                {
                    if (collider.enabled && collider.Raycast(ray, out RaycastHit hit, 200f) && hit.distance < distance)
                    {
                        nearest = robot;
                        distance = hit.distance;
                    }
                }
            }
            if (nearest != null) RobotPrototypeController.Select(nearest);
        }
    }
}
