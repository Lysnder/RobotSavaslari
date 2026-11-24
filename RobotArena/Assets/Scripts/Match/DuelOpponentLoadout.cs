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
        private readonly Mesh[] fitted = new Mesh[2];
        private readonly Material[] materials = new Material[2];
        private readonly CombatProfile[] profiles = new CombatProfile[2];
        private readonly Vector3[] pivots = new Vector3[2];
        private int current = -1;

        internal bool Fit(RobotPrototypeController bot, PlayerRobotSelection catalog, string id)
        {
            if (id != DuelSetup.Fork && id != DuelSetup.Spinner) return false;
            int index = id == DuelSetup.Fork ? 1 : 0;
            if (weapon == null)
            {
                var visuals = bot.GetComponent<RobotDamageVisuals>();
                visuals.Initialize();
                foreach (var part in visuals.Parts)
                    if (part.Kind == RobotDamageVisuals.PartKind.Weapon) { weapon = part; break; }
                if (weapon == null) return false;
                original = weapon.Mesh;
            }
            if (fitted[index] == null && !catalog.BuildOpponentWeapon(index == 1, bot.transform, weapon.Transform.parent,
                    out fitted[index], out pivots[index], out materials[index], out profiles[index])) return false;
            if (current == index) return true;
            weapon.Name = index == 1 ? "Lifter" : "Spinner";
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
