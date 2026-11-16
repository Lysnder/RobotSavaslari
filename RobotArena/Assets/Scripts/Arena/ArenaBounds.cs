using UnityEngine;

namespace RobotSavaslari.Arena
{
    // The grid, visible perimeter and robot clamp share these world-space dimensions.
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class ArenaBounds : MonoBehaviour
    {
        [SerializeField] private Vector2Int gridSize = new Vector2Int(8, 8);
        [SerializeField, Min(.1f)] private float cellSize = 2f;
        [SerializeField] private float surfaceHeight = .82f;
        [SerializeField, Min(0)] private float perimeterInset = .19f;
        [SerializeField] private RobotPrototypeController[] robots = new RobotPrototypeController[0];
        public Vector2Int GridSize => gridSize;
        public float CellSize => cellSize;
        public float SurfaceHeight => surfaceHeight;
        public Vector2 HalfExtents => new Vector2(gridSize.x, gridSize.y) * (cellSize * .5f);
        public Vector2 Center => new Vector2(transform.position.x, transform.position.z);
        public Vector2 InnerHalfExtents => HalfExtents - Vector2.one * perimeterInset;

        public Vector2 CenterLimits(float radius) => Vector2.Max(Vector2.zero, InnerHalfExtents - Vector2.one * radius);
        public Vector3 CellCenter(int column, int row) => new Vector3(
            Center.x + (column - (gridSize.x - 1) * .5f) * cellSize, surfaceHeight,
            Center.y + (row - (gridSize.y - 1) * .5f) * cellSize);

        public void Configure(Vector2Int size, float spacing, float height, RobotPrototypeController[] sceneRobots)
        {
            gridSize = size; cellSize = spacing; surfaceHeight = height; robots = sceneRobots;
        }

        private void Update() => SynchronizeBotBounds();
        private void FixedUpdate() => SynchronizeBotBounds();

        // Match creates the bot at runtime. This runs after Match.Update but before bot decisions.
        public void SynchronizeBotBounds()
        {
            foreach (var robot in robots)
            {
                if (robot == null) continue;
                var bot = robot.GetComponent<RobotBotController>();
                if (bot != null) bot.tacticalHalfExtents = CenterLimits(robot.ArenaFootprintRadius);
            }
        }
    }
}
