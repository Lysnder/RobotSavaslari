using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RobotSavaslari.Driving.Editor
{
    /// <summary>
    /// Atalay'ın deneme sahnelerini üreten betiklerin paylaştığı küçük
    /// yardımcılar: klasör, malzeme ve blok oluşturma.
    /// </summary>
    internal static class DrivingSceneBuildUtility
    {
        public const string MaterialFolder = "Assets/Materials/Driving";

        /// <summary>Verilen klasör yoksa üst klasörleriyle birlikte oluşturur.</summary>
        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>Verilen adla bir URP malzemesi oluşturur ya da rengini günceller.</summary>
        public static Material CreateMaterial(string name, Color color)
        {
            EnsureFolder(MaterialFolder);

            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = GetDefaultShader();

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            EditorUtility.SetDirty(material);

            return material;
        }

        public static Shader GetDefaultShader()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultShader != null)
            {
                return pipeline.defaultShader;
            }

            return Shader.Find("Standard");
        }

        /// <summary>
        /// Küp blok oluşturur. İnce bloklar (yükseklik &lt; 0.1) yalnızca işaret
        /// sayılır ve çarpışma hacimleri kaldırılır, robota takılmasınlar diye.
        /// </summary>
        public static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, float yaw = 0f)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            block.isStatic = true;

            if (scale.y < 0.1f)
            {
                Object.DestroyImmediate(block.GetComponent<Collider>());
            }

            return block;
        }

        /// <summary>Seri hale getirilmiş gizli bir float alanı yazar.</summary>
        public static void SetFloat(Object target, string fieldName, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"{target.GetType().Name}.{fieldName} alani bulunamadi.");
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Seri hale getirilmiş gizli bir bool alanı yazar.</summary>
        public static void SetBool(Object target, string fieldName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"{target.GetType().Name}.{fieldName} alani bulunamadi.");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Seri hale getirilmiş gizli bir nesne referansını yazar.</summary>
        public static void SetReference(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"{target.GetType().Name}.{fieldName} alani bulunamadi.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
