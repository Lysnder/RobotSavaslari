using RobotSavaslari.Match.Art;
using UnityEngine;

namespace RobotSavaslari.UI
{
    public sealed class WorkshopKit : ScriptableObject
    {
        [SerializeField] private GameObject chassis, wheel, lightArmor, heavyArmor, spinner, fork;
        [SerializeField] private Mesh stripeMesh;
        [SerializeField] private Material teamBlue;

        public static WorkshopKit Load() => Resources.Load<WorkshopKit>("WorkshopKit");

        public bool HasParts => chassis && wheel && lightArmor && heavyArmor && spinner && fork && stripeMesh && teamBlue;

        public GameObject Build(WorkshopLoadout loadout, Transform parent)
        {
            if (!HasParts || !loadout.IsValid) return null;
            var robot = Instantiate(chassis, parent, false);
            robot.name = "Workshop model";
            var body = robot.transform;
            var mount = ModularRobotPreview.Find(body, "WeaponMount_A");
            if (body.InverseTransformPoint(mount.position).z < 0)
                body.localRotation = Quaternion.Euler(0, 180, 0);
            var stripe = new GameObject("Team 1 - single stripe", typeof(MeshFilter), typeof(MeshRenderer));
            stripe.transform.SetParent(body, false);
            stripe.transform.localPosition = new Vector3(-.19f, .405f, 0);
            stripe.transform.localScale = new Vector3(.09f, .014f, .54f);
            stripe.GetComponent<MeshFilter>().sharedMesh = stripeMesh;
            stripe.GetComponent<MeshRenderer>().sharedMaterial = teamBlue;

            foreach (string socket in new[] { "FL", "FR", "RL", "RR" })
            {
                var tire = Instantiate(wheel, ModularRobotPreview.Find(body, "WheelMount_" + socket), false);
                if (loadout.tireId == "soft") tire.transform.localScale = new Vector3(1.12f, 1, 1);
            }
            var armor = loadout.armorId == "heavy" ? heavyArmor : lightArmor;
            Instantiate(armor, ModularRobotPreview.Find(body, "ArmorMount_L"), false);
            Instantiate(armor, ModularRobotPreview.Find(body, "ArmorMount_R"), false);

            if (loadout.weaponId == "spinner")
                Instantiate(spinner, mount, false);
            else
            {
                // The stock fork is authored in robot space. Seat its hinge on the kit socket.
                var tool = Instantiate(fork, mount, false);
                var module = tool.GetComponent<RobotSavaslari.Combat.ForkLoadout>();
                tool.transform.rotation = parent.rotation;
                tool.transform.localScale = Vector3.one * .76f;
                tool.transform.position += mount.position - tool.transform.TransformPoint(module.hinge);
            }
            return robot;
        }
    }
}
