using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RobotSavaslari.Presentation
{
    /// <summary>
    /// Builds look-only copies of robot meshes for replay and inspection. A proxy holds nothing but
    /// Transform, MeshFilter and MeshRenderer: no Rigidbody, Collider, health, combat, bot, audio or
    /// listener component can exist on it, so it cannot collide, take damage or report a result.
    /// Meshes and materials are shared with the source; the damage wear property block is copied.
    /// </summary>
    public static class RobotVisualProxyFactory
    {
        /// <summary>Mesh renderers that make up a robot's look, including inactive (detached) parts.</summary>
        public static void CollectMeshRenderers(Transform robot, List<MeshRenderer> output)
        {
            output.Clear();
            if (robot == null) return;
            foreach (MeshRenderer renderer in robot.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) output.Add(renderer);
            }
        }

        /// <summary>True when the renderer is currently drawn by the live robot.</summary>
        public static bool IsVisible(Renderer renderer)
        {
            return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff;
        }

        public static Transform CreateRoot(string name, Transform parent = null)
        {
            var root = new GameObject(name).transform;
            if (parent != null) root.SetParent(parent, false);
            return root;
        }

        /// <summary>Copies one renderer's current world pose, mesh, materials and wear into a new proxy.</summary>
        public static MeshRenderer CreateProxy(MeshRenderer source, Transform parent)
        {
            if (source == null) return null;
            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return null;

            var proxy = new GameObject(source.name);
            proxy.layer = source.gameObject.layer;
            proxy.transform.SetParent(parent, false);
            proxy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            proxy.transform.localScale = source.transform.lossyScale;
            proxy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = proxy.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            if (source.HasPropertyBlock())
            {
                var block = new MaterialPropertyBlock();
                source.GetPropertyBlock(block);
                renderer.SetPropertyBlock(block);
            }
            renderer.enabled = IsVisible(source);
            return renderer;
        }

        /// <summary>
        /// Hides renderers with <see cref="Renderer.forceRenderingOff"/> and remembers the previous value.
        /// The damage system drives <see cref="Renderer.enabled"/>, which is left untouched.
        /// </summary>
        public static void Hide(IEnumerable<Renderer> renderers, List<KeyValuePair<Renderer, bool>> previous)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                previous.Add(new KeyValuePair<Renderer, bool>(renderer, renderer.forceRenderingOff));
                renderer.forceRenderingOff = true;
            }
        }

        /// <summary>Restores what <see cref="Hide"/> changed, in reverse order, and clears the record.</summary>
        public static void Restore(List<KeyValuePair<Renderer, bool>> previous)
        {
            for (int i = previous.Count - 1; i >= 0; i--)
                if (previous[i].Key != null) previous[i].Key.forceRenderingOff = previous[i].Value;
            previous.Clear();
        }

        /// <summary>
        /// Detached-part debris at the scene root, recognised by the damage system's naming. Used only to
        /// hide it during a presentation; if the naming changes, debris simply stays visible.
        /// </summary>
        public static void CollectDebrisRenderers(Transform robot, List<Renderer> output)
        {
            if (robot == null) return;
            string prefix = robot.name + " Debris ";
            foreach (GameObject root in robot.gameObject.scene.GetRootGameObjects())
                if (root.name.StartsWith(prefix) && root.GetComponent<Rigidbody>() != null)
                    output.AddRange(root.GetComponentsInChildren<Renderer>(true));
        }
    }
}
