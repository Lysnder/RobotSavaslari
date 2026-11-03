using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobotSavaslari.Editor
{
    public static class ArenaSceneBuilder
    {
        private const string MaterialFolder = "Assets/Materials/Arena";
        private const float HalfWidth = 6f;
        private const float HalfDepth = 5f;
        private const float FenceHeight = 3.4f;
        private const float FenceBottom = 0.3f;

        [MenuItem("RobotSavaslari/Build Arena Environment")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Materials", "Arena");

            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Arena.unity", OpenSceneMode.Single);
            GameObject oldRoot = GameObject.Find("BattleBots Arena");
            if (oldRoot != null)
                Object.DestroyImmediate(oldRoot);

            Material floor = GetMaterial("ArenaFloor", new Color(0.075f, 0.095f, 0.12f), 0.78f, 0.25f);
            Material steel = GetMaterial("ArenaSteel", new Color(0.23f, 0.29f, 0.35f), 0.82f, 0.65f);
            Material darkSteel = GetMaterial("ArenaDarkSteel", new Color(0.045f, 0.06f, 0.075f), 0.82f, 0.55f);
            Material yellow = GetMaterial("ArenaSafetyYellow", new Color(1f, 0.67f, 0.08f), 0.35f, 0.15f);
            Material red = GetMaterial("ArenaRed", new Color(0.78f, 0.09f, 0.08f), 0.45f, 0.2f);
            Material blue = GetMaterial("ArenaBlue", new Color(0.08f, 0.32f, 0.88f), 0.45f, 0.2f);
            Material mesh = GetMaterial("ArenaMesh", new Color(0.31f, 0.43f, 0.49f), 0.58f, 0.72f);
            Material white = GetMaterial("ArenaMarkings", new Color(0.78f, 0.83f, 0.84f), 0.4f, 0.1f);

            GameObject root = new GameObject("BattleBots Arena");
            BuildFloor(root.transform, floor, steel, darkSteel, yellow, red, blue, white);
            BuildFence(root.transform, steel, darkSteel, mesh, yellow);
            BuildLighting(root.transform);
            ConfigureCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.pivot = new Vector3(0f, 1.3f, 0f);
                view.rotation = Quaternion.Euler(27f, 0f, 0f);
                view.size = 17f;
                view.Repaint();
            }
            Debug.Log("BattleBots cage arena generated and saved to Arena.unity.");
        }

        private static void BuildFloor(Transform root, Material floor, Material steel, Material darkSteel,
            Material yellow, Material red, Material blue, Material white)
        {
            Box(root, "Foundation", Vector3.zero, new Vector3(14f, 0.6f, 12f), darkSteel, true);
            Box(root, "Combat Floor", new Vector3(0f, 0.34f, 0f), new Vector3(12.5f, 0.12f, 10.5f), floor, true);
            Box(root, "Outer Steel Rim", new Vector3(0f, 0.47f, 0f), new Vector3(12.9f, 0.18f, 10.9f), steel, true);
            Box(root, "Inner Combat Plate", new Vector3(0f, 0.57f, 0f), new Vector3(12f, 0.04f, 10f), floor, true);

            Box(root, "Safety Line North", new Vector3(0f, 0.60f, 4.55f), new Vector3(10.8f, 0.012f, 0.045f), yellow, false);
            Box(root, "Safety Line South", new Vector3(0f, 0.60f, -4.55f), new Vector3(10.8f, 0.012f, 0.045f), yellow, false);
            Box(root, "Safety Line East", new Vector3(5.55f, 0.60f, 0f), new Vector3(0.045f, 0.012f, 9.1f), yellow, false);
            Box(root, "Safety Line West", new Vector3(-5.55f, 0.60f, 0f), new Vector3(0.045f, 0.012f, 9.1f), yellow, false);

            Box(root, "Red Start Pad", new Vector3(-4.15f, 0.605f, 0f), new Vector3(1.35f, 0.018f, 2.6f), red, false);
            Box(root, "Blue Start Pad", new Vector3(4.15f, 0.605f, 0f), new Vector3(1.35f, 0.018f, 2.6f), blue, false);
            Box(root, "Center Stripe", new Vector3(0f, 0.602f, 0f), new Vector3(0.035f, 0.012f, 2.5f), white, false);
            Box(root, "Center Stripe Crossbar", new Vector3(0f, 0.602f, 0f), new Vector3(2.5f, 0.012f, 0.035f), white, false);

            for (int i = -4; i <= 4; i++)
            {
                float x = i * 0.68f;
                Box(root, "Floor Panel Seam", new Vector3(x, 0.601f, 0f), new Vector3(0.012f, 0.008f, 8.8f), steel, false);
            }
            for (int i = -3; i <= 3; i++)
            {
                float z = i * 0.62f;
                Box(root, "Floor Panel Seam", new Vector3(0f, 0.601f, z), new Vector3(10.8f, 0.008f, 0.012f), steel, false);
            }

            Text(root, "RED", new Vector3(-4.15f, 0.64f, -1.2f), red.color, 0.42f);
            Text(root, "BLUE", new Vector3(4.15f, 0.64f, -1.2f), blue.color, 0.42f);
            Text(root, "ROBOT ARENA  //  01", new Vector3(0f, 0.62f, -4.8f), white.color, 0.24f);
        }

        private static void BuildFence(Transform root, Material steel, Material darkSteel, Material mesh, Material yellow)
        {
            float yMid = FenceBottom + FenceHeight * 0.5f;
            float yTop = FenceBottom + FenceHeight;
            float sideLengthX = HalfWidth * 2f;
            float sideLengthZ = HalfDepth * 2f;

            // Opaque lower kick plates keep robots inside; their colliders form the continuous cage wall.
            Wall(root, "North Kick Plate", new Vector3(0f, 0.87f, HalfDepth), new Vector3(sideLengthX, 0.55f, 0.18f), darkSteel);
            Wall(root, "South Kick Plate", new Vector3(0f, 0.87f, -HalfDepth), new Vector3(sideLengthX, 0.55f, 0.18f), darkSteel);
            Wall(root, "East Kick Plate", new Vector3(HalfWidth, 0.87f, 0f), new Vector3(0.18f, 0.55f, sideLengthZ), darkSteel);
            Wall(root, "West Kick Plate", new Vector3(-HalfWidth, 0.87f, 0f), new Vector3(0.18f, 0.55f, sideLengthZ), darkSteel);

            // Continuous invisible collision panels behind the visible mesh.
            Barrier(root, "North Safety Barrier", new Vector3(0f, yMid + 0.27f, HalfDepth), new Vector3(sideLengthX, FenceHeight - 0.55f, 0.18f));
            Barrier(root, "South Safety Barrier", new Vector3(0f, yMid + 0.27f, -HalfDepth), new Vector3(sideLengthX, FenceHeight - 0.55f, 0.18f));
            Barrier(root, "East Safety Barrier", new Vector3(HalfWidth, yMid + 0.27f, 0f), new Vector3(0.18f, FenceHeight - 0.55f, sideLengthZ));
            Barrier(root, "West Safety Barrier", new Vector3(-HalfWidth, yMid + 0.27f, 0f), new Vector3(0.18f, FenceHeight - 0.55f, sideLengthZ));

            Beam(root, "Top Rail North", new Vector3(0f, yTop, HalfDepth), new Vector3(sideLengthX + 0.3f, 0.22f, 0.22f), steel);
            Beam(root, "Top Rail South", new Vector3(0f, yTop, -HalfDepth), new Vector3(sideLengthX + 0.3f, 0.22f, 0.22f), steel);
            Beam(root, "Top Rail East", new Vector3(HalfWidth, yTop, 0f), new Vector3(0.22f, 0.22f, sideLengthZ + 0.3f), steel);
            Beam(root, "Top Rail West", new Vector3(-HalfWidth, yTop, 0f), new Vector3(0.22f, 0.22f, sideLengthZ + 0.3f), steel);
            Beam(root, "Bottom Rail North", new Vector3(0f, FenceBottom, HalfDepth), new Vector3(sideLengthX, 0.16f, 0.2f), steel);
            Beam(root, "Bottom Rail South", new Vector3(0f, FenceBottom, -HalfDepth), new Vector3(sideLengthX, 0.16f, 0.2f), steel);
            Beam(root, "Bottom Rail East", new Vector3(HalfWidth, FenceBottom, 0f), new Vector3(0.2f, 0.16f, sideLengthZ), steel);
            Beam(root, "Bottom Rail West", new Vector3(-HalfWidth, FenceBottom, 0f), new Vector3(0.2f, 0.16f, sideLengthZ), steel);

            // Square steel mesh above the kick plates.
            float meshBottom = 1.16f;
            float meshTop = yTop - 0.18f;
            for (float y = meshBottom; y <= meshTop; y += 0.38f)
            {
                Beam(root, "North Mesh Wire", new Vector3(0f, y, HalfDepth), new Vector3(sideLengthX, 0.035f, 0.035f), mesh);
                Beam(root, "South Mesh Wire", new Vector3(0f, y, -HalfDepth), new Vector3(sideLengthX, 0.035f, 0.035f), mesh);
                Beam(root, "East Mesh Wire", new Vector3(HalfWidth, y, 0f), new Vector3(0.035f, 0.035f, sideLengthZ), mesh);
                Beam(root, "West Mesh Wire", new Vector3(-HalfWidth, y, 0f), new Vector3(0.035f, 0.035f, sideLengthZ), mesh);
            }
            for (float x = -HalfWidth; x <= HalfWidth; x += 0.55f)
            {
                Beam(root, "North Mesh Upright", new Vector3(x, (meshBottom + meshTop) * 0.5f, HalfDepth), new Vector3(0.035f, meshTop - meshBottom, 0.035f), mesh);
                Beam(root, "South Mesh Upright", new Vector3(x, (meshBottom + meshTop) * 0.5f, -HalfDepth), new Vector3(0.035f, meshTop - meshBottom, 0.035f), mesh);
            }
            for (float z = -HalfDepth; z <= HalfDepth; z += 0.55f)
            {
                Beam(root, "East Mesh Upright", new Vector3(HalfWidth, (meshBottom + meshTop) * 0.5f, z), new Vector3(0.035f, meshTop - meshBottom, 0.035f), mesh);
                Beam(root, "West Mesh Upright", new Vector3(-HalfWidth, (meshBottom + meshTop) * 0.5f, z), new Vector3(0.035f, meshTop - meshBottom, 0.035f), mesh);
            }

            foreach (float x in new[] { -HalfWidth, HalfWidth })
            foreach (float z in new[] { -HalfDepth, HalfDepth })
            {
                Box(root, "Armored Corner Post", new Vector3(x, yMid, z), new Vector3(0.34f, FenceHeight, 0.34f), darkSteel, true);
                Box(root, "Corner Safety Band", new Vector3(x, 1.35f, z), new Vector3(0.36f, 0.12f, 0.36f), yellow, false);
            }
        }

        private static void BuildLighting(Transform root)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -7.4f : 7.4f;
                float z = i % 2 == 0 ? -6.4f : 6.4f;
                GameObject rig = new GameObject("Arena Floodlight " + (i + 1));
                rig.transform.SetParent(root, false);
                rig.transform.position = new Vector3(x, 7f, z);
                Light light = rig.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 22f;
                light.spotAngle = 72f;
                light.intensity = 950f;
                light.color = new Color(0.72f, 0.84f, 1f);
                rig.transform.LookAt(new Vector3(0f, 0.4f, 0f));
            }
        }

        private static void ConfigureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;
            camera.transform.position = new Vector3(0f, 11.5f, -17.5f);
            camera.transform.LookAt(new Vector3(0f, 1.25f, 0f));
            camera.fieldOfView = 52f;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Beam(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            return Box(parent, name, position, scale, material, false);
        }

        private static GameObject Wall(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = Box(parent, name, position, scale, material, true);
            return go;
        }

        private static void Barrier(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            GameObject go = Box(parent, name, position, scale, GetMaterial("ArenaBarrier", new Color(0f, 0f, 0f), 0f, 0f), true);
            go.GetComponent<Renderer>().enabled = false;
        }

        private static void Text(Transform parent, string text, Vector3 position, Color color, float size)
        {
            GameObject go = new GameObject(text + " Floor Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = size / 4f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
        }

        private static Material GetMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}



