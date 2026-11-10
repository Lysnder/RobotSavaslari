using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RobotSavaslari.Match.Art.Editor
{
    public static class ModularRobotSceneBuilder
    {
        private const string Root = "Assets/Art/ModularRobots";
        private static GameObject Model(string name)
        {
            var wrapper = new GameObject(name);
            try
            {
                var lods = new LOD[3];
                for (int level = 0; level < 3; level++)
                {
                    string suffix = level == 0 ? "" : "_LOD" + level;
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + name + suffix + ".glb");
                    if (model == null) throw new System.InvalidOperationException("Model missing: " + name + suffix);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, wrapper.transform);
                    lods[level] = new LOD(level == 0 ? .25f : level == 1 ? .1f : .02f, instance.GetComponentsInChildren<Renderer>());
                }
                var group = wrapper.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.RecalculateBounds();
                return PrefabUtility.SaveAsPrefabAsset(wrapper, Root + "/Prefabs/" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(wrapper); }
        }

        [MenuItem("RobotSavaslari/Tools/Build Modular Robot Preview")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder("Assets/Scenes/Tools")) AssetDatabase.CreateFolder("Assets/Scenes", "Tools");
            if (!AssetDatabase.IsValidFolder(Root + "/Prefabs")) AssetDatabase.CreateFolder(Root, "Prefabs");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var robot = new GameObject("Modular Robot Art Preview").AddComponent<ModularRobotPreview>();
            robot.Configure(Model("KitChassis"), Model("KitWheel"),
                new[] { Model("Weapon_vertical"), Model("Weapon_drum"), Model("Weapon_horizontal") },
                new[] { Model("Armor_armor_light"), Model("Armor_armor_heavy") });
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Preview Ground";
            floor.transform.localScale = Vector3.one * .6f;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/PreviewGround.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(.11f, .13f, .16f);
                AssetDatabase.CreateAsset(mat, Root + "/PreviewGround.mat");
            }
            floor.GetComponent<Renderer>().sharedMaterial = mat;
            Camera.main.transform.position = new Vector3(2.6f, 2.2f, 3.2f);
            Camera.main.transform.LookAt(new Vector3(0, .25f, 0));
            Camera.main.fieldOfView = 40;
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Tools/ModularRobotPreview.unity");
        }
    }
}
