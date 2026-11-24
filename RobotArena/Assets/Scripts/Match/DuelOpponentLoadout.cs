using RobotSavaslari.Combat;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Match
{
    // Both slot directions retain the scene's bot, damage track, target and movement controller.
    internal sealed class DuelOpponentLoadout
    {
        private RobotDamageVisuals.Part weapon;
        private Mesh original;
        private Material originalMaterial;
        private readonly Mesh[] fitted = new Mesh[WorkshopWeapons.Count];
        private readonly Material[] materials = new Material[WorkshopWeapons.Count];
        private readonly CombatProfile[] profiles = new CombatProfile[WorkshopWeapons.Count];
        private readonly Vector3[] pivots = new Vector3[WorkshopWeapons.Count];
        private int current = -1;

        private bool Prepare(RobotPrototypeController bot, PlayerRobotSelection catalog, int index)
        {
            if (index < 0 || bot == null || catalog == null) return false;
            if (weapon == null)
            {
                var visuals = bot.GetComponent<RobotDamageVisuals>();
                visuals.Initialize();
                foreach (var part in visuals.Parts)
                    if (part.Kind == RobotDamageVisuals.PartKind.Weapon) { weapon = part; break; }
                if (weapon == null) return false;
                original = weapon.Mesh;
                originalMaterial = weapon.Transform.GetComponent<MeshRenderer>().sharedMaterial;
            }
            if (fitted[index]) return true;
            if (!catalog.BuildOpponentWeapon(WorkshopWeapons.Id(index), bot.transform, weapon.Transform.parent,
                    out var mesh, out var pivot, out var material, out var profile))
            {
                if (mesh) Object.Destroy(mesh);
                return false;
            }
            fitted[index] = mesh;
            pivots[index] = pivot;
            materials[index] = material;
            profiles[index] = profile;
            return true;
        }

        internal bool TryReplayWeapon(RobotPrototypeController bot, PlayerRobotSelection catalog, string partName,
            int vertexCount, out Mesh mesh, out Material material)
        {
            mesh = null;
            material = null;
            int index = WorkshopWeapons.PartIndex(partName);
            if (!Prepare(bot, catalog, index)) return false;
            if (partName == "Lifter" && original && original.vertexCount == vertexCount)
            {
                mesh = original;
                material = originalMaterial;
            }
            else if (fitted[index].vertexCount == vertexCount)
            {
                mesh = fitted[index];
                material = materials[index];
            }
            return mesh && material;
        }

        internal bool Fit(RobotPrototypeController bot, PlayerRobotSelection catalog, string id)
        {
            int index = WorkshopWeapons.Index(id);
            if (!Prepare(bot, catalog, index)) return false;
            if (current == index) return true;
            weapon.Name = WorkshopWeapons.Part(id);
            weapon.Transform.name = weapon.Name;
            weapon.Mesh = fitted[index];
            weapon.Transform.GetComponent<MeshFilter>().sharedMesh = weapon.Mesh;
            weapon.Wear.GetComponent<MeshFilter>().sharedMesh = weapon.Mesh;
            weapon.Transform.GetComponent<MeshRenderer>().sharedMaterial = materials[index];
            weapon.RestPosition = pivots[index];
            weapon.RestRotation = Quaternion.identity;
            weapon.Transform.SetLocalPositionAndRotation(weapon.RestPosition, weapon.RestRotation);
            bot.SelectWeaponProfile(profiles[index]);
            bot.GetComponent<RobotModelAnimator>().BindParts();
            current = index;
            return true;
        }

        internal void Dispose()
        {
            // The damage component releases its active mesh at scene teardown.
            if (current >= 0 && original) Object.Destroy(original);
            for (int i = 0; i < fitted.Length; i++)
                if (i != current && fitted[i]) Object.Destroy(fitted[i]);
        }
    }
}
