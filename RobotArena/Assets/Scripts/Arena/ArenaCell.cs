using UnityEngine;

namespace RobotSavaslari.Arena
{
    [DisallowMultipleComponent]
    public sealed class ArenaCell : MonoBehaviour
    {
        [SerializeField] private Vector2Int coordinate;
        public Vector2Int Coordinate => coordinate;
        public void Configure(int column, int row) { coordinate = new Vector2Int(column, row); }
    }
}
