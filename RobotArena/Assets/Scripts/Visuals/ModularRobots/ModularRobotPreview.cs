using UnityEngine;

namespace RobotSavaslari.Match.Art
{
    // Isolated art-fit preview, intentionally independent of shared combat/damage code.
    public sealed class ModularRobotPreview : MonoBehaviour
    {
        [SerializeField] private GameObject chassisAsset;
        [SerializeField] private GameObject wheelAsset;
        [SerializeField] private GameObject[] weaponAssets;
        [SerializeField] private GameObject[] armorAssets;
        private Transform chassis, mount;
        private readonly System.Collections.Generic.List<Transform> rotors = new System.Collections.Generic.List<Transform>();
        private GameObject weapon;
        private readonly GameObject[] armor = new GameObject[2];
        private int weaponIndex;
        private bool spinning;
        public int WeaponIndex => weaponIndex;
        public bool HasWeapon => weapon != null;

        public void Configure(GameObject c, GameObject w, GameObject[] weapons, GameObject[] armors)
        {
            chassisAsset = c; wheelAsset = w; weaponAssets = weapons; armorAssets = armors;
        }

        private void Start()
        {
            chassis = Instantiate(chassisAsset, transform).transform;
            mount = Find(chassis, "WeaponMount_A");
            foreach (string name in new[] { "FL", "FR", "RL", "RR" })
                Instantiate(wheelAsset, Find(chassis, "WheelMount_" + name), false);
            EquipWeapon(0);
            EquipArmor(0);
        }

        public static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            throw new System.InvalidOperationException("Missing art socket: " + name);
        }

        public void EquipWeapon(int index)
        {
            if (weapon != null) { weapon.SetActive(false); Destroy(weapon); }
            weapon = null; rotors.Clear();
            weaponIndex = index;
            if (index < 0) return;
            weapon = Instantiate(weaponAssets[index], mount, false);
            foreach (var child in weapon.GetComponentsInChildren<Transform>())
                if (child.name.StartsWith("Rotor")) rotors.Add(child);
            if (rotors.Count == 0) throw new System.InvalidOperationException("Weapon rotor pivot missing");
        }

        public void EquipArmor(int index)
        {
            for (int i = 0; i < 2; i++)
            {
                if (armor[i] != null) { armor[i].SetActive(false); Destroy(armor[i]); }
                armor[i] = null;
                if (index >= 0)
                    armor[i] = Instantiate(armorAssets[index], Find(chassis, i == 0 ? "ArmorMount_L" : "ArmorMount_R"), false);
            }
        }

        private void Update()
        {
            if (spinning)
                foreach (var rotor in rotors)
                    rotor.Rotate(weaponIndex == 2 ? Vector3.up : Vector3.right, 160f * Time.deltaTime, Space.Self);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 370, 360), GUI.skin.box);
            GUILayout.Label("MESHY + BLENDER / MODULER ROBOT ILK SET");
            GUILayout.Label("Gorsel montaj denemesi / dovus baglantisi yok");
            if (GUILayout.Button("Dikey disk tak")) EquipWeapon(0);
            if (GUILayout.Button("Drum tak")) EquipWeapon(1);
            if (GUILayout.Button("Yatay bar tak")) EquipWeapon(2);
            if (GUILayout.Button("Silahi sok")) EquipWeapon(-1);
            spinning = GUILayout.Toggle(spinning, "Pivot hareketini goster");
            if (GUILayout.Button("Hafif zirh seti")) EquipArmor(0);
            if (GUILayout.Button("Agir zirha yukselt")) EquipArmor(1);
            if (GUILayout.Button("Zirhi sok")) EquipArmor(-1);
            if (GUILayout.Button("Robotu 45 derece cevir")) transform.Rotate(Vector3.up, 45);
            GUILayout.Label("Ayri silah / zirh mesh'leri, ortak montaj socket'leri.");
            GUILayout.EndArea();
        }
    }
}
