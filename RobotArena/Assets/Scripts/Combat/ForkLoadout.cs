using UnityEngine;

namespace RobotSavaslari.Combat
{
    /// <summary>Visual module authored in robot-local metres, with the origin at the lift hinge.</summary>
    public sealed class ForkLoadout : MonoBehaviour
    {
        public CombatProfile profile;
        public Material material;
        public Vector3 hinge;

        internal Mesh BuildMesh(Transform robot, Transform weaponParent)
        {
            var filters = GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0 || profile == null || material == null) return null;
            var pieces = new CombineInstance[filters.Length];
            Vector3 pivot = weaponParent.InverseTransformPoint(robot.TransformPoint(hinge));
            Matrix4x4 toPart = Matrix4x4.Translate(-pivot) * weaponParent.worldToLocalMatrix * robot.localToWorldMatrix;
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null || !filters[i].sharedMesh.isReadable) return null;
                pieces[i].mesh = filters[i].sharedMesh;
                pieces[i].transform = toPart * transform.worldToLocalMatrix * filters[i].transform.localToWorldMatrix;
            }
            var mesh = new Mesh { name = "Kama - lifting fork" };
            mesh.CombineMeshes(pieces);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
