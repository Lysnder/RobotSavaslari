using System;
using System.Collections.Generic;
using RobotSavaslari.Match.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace RobotSavaslari.UI
{
    // Bake only the kit's visible LOD0 surfaces into the existing detachable part format.
    // The robot keeps its original collider, Rigidbody, motor, attack, camera and target.
    internal sealed class WorkshopModel
    {
        public readonly GameObject Root;
        public readonly List<RobotDamageVisuals.Part> Parts = new List<RobotDamageVisuals.Part>();

        private WorkshopModel(Transform robot)
        {
            Root = new GameObject("Robot Model");
            Root.transform.SetParent(robot, false);
            Root.layer = robot.gameObject.layer;
            Root.SetActive(false);
        }

        public static WorkshopModel Build(WorkshopKit kit, WorkshopLoadout loadout, Transform robot,
            BoxCollider hull, IReadOnlyList<RobotDamageVisuals.Part> stock)
        {
            var result = new WorkshopModel(robot);
            var staging = new GameObject("Workshop source");
            staging.SetActive(false);
            staging.transform.SetParent(robot, false);
            try
            {
                var art = kit.Build(loadout, staging.transform);
                if (art == null) throw new InvalidOperationException("Atölye görsel seti bulunamadı.");
                var sockets = new Dictionary<Transform, string>();
                foreach (string suffix in new[] { "FL", "FR", "RL", "RR" })
                    sockets.Add(ModularRobotPreview.Find(art.transform, "WheelMount_" + suffix), "Wheel_" + suffix);
                foreach (string suffix in new[] { "L", "R" })
                    sockets.Add(ModularRobotPreview.Find(art.transform, "ArmorMount_" + suffix), "Armor_" + suffix);
                sockets.Add(ModularRobotPreview.Find(art.transform, "WeaponMount_A"), loadout.weaponId == "spinner" ? "Spinner" : "Lifter");

                var renderers = new Dictionary<string, List<MeshRenderer>>();
                var pivots = new Dictionary<string, Vector3>();
                foreach (var socket in sockets)
                {
                    renderers[socket.Value] = new List<MeshRenderer>();
                    pivots[socket.Value] = robot.InverseTransformPoint(socket.Key.position);
                }
                renderers["Body"] = new List<MeshRenderer>();
                foreach (var renderer in art.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var group = renderer.GetComponentInParent<LODGroup>(true);
                    if (group != null && Array.IndexOf(group.GetLODs()[0].renderers, renderer) < 0)
                    {
                        // Kama is an ungrouped primitive assembly mounted below the chassis LOD.
                        var weapon = renderer.GetComponentInParent<RobotSavaslari.Combat.ForkLoadout>(true);
                        if (weapon == null && renderer.transform.parent != art.transform) continue;
                    }
                    string slot = "Body";
                    for (var t = renderer.transform; t != art.transform; t = t.parent)
                        if (sockets.TryGetValue(t, out var mounted)) { slot = mounted; break; }
                    renderers[slot].Add(renderer);
                }

                var bounds = new Bounds();
                bool first = true;
                foreach (var slot in renderers)
                {
                    var kind = slot.Key.StartsWith("Wheel_") ? RobotDamageVisuals.PartKind.Wheel :
                        slot.Key.StartsWith("Armor_") ? RobotDamageVisuals.PartKind.Armor :
                        slot.Key == "Body" ? RobotDamageVisuals.PartKind.Body : RobotDamageVisuals.PartKind.Weapon;
                    RobotDamageVisuals.Part template = null;
                    foreach (var existing in stock)
                        if (existing.Kind == kind) { template = existing; break; }
                    if (slot.Value.Count == 0 || template == null) throw new InvalidOperationException("Eksik montaj yuvası: " + slot.Key);
                    Vector3 pivot = pivots.TryGetValue(slot.Key, out var socketPivot) ? socketPivot : Vector3.zero;
                    var part = result.Bake(slot.Key, kind, pivot, slot.Value, template);
                    if (kind == RobotDamageVisuals.PartKind.Armor)
                        part.Threshold *= loadout.armorId == "heavy" ? 1.10f : .92f;
                    var box = part.Mesh.bounds;
                    box.center += pivot;
                    if (first) { bounds = box; first = false; } else bounds.Encapsulate(box);
                }
                Vector3 size = bounds.size;
                Vector3 envelope = hull.size;
                float fit = Mathf.Min(1f, Mathf.Min(envelope.x / size.x, Mathf.Min(envelope.y / size.y, envelope.z / size.z)));
                if (float.IsNaN(fit) || float.IsInfinity(fit) || fit < .35f || size.x <= 0 || size.y <= 0 || size.z <= 0)
                    throw new InvalidOperationException("Görsel set şasi çarpışma zarfına sığmıyor.");
                result.Root.transform.localScale = Vector3.one * fit;
                result.Root.transform.localPosition = new Vector3(hull.center.x - bounds.center.x * fit,
                    hull.center.y - envelope.y * .5f - bounds.min.y * fit, hull.center.z - bounds.center.z * fit);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
            finally
            {
                staging.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(staging);
            }
        }

        private RobotDamageVisuals.Part Bake(string id, RobotDamageVisuals.PartKind kind, Vector3 pivot,
            List<MeshRenderer> sources, RobotDamageVisuals.Part template)
        {
            var node = new GameObject(id);
            node.layer = Root.layer;
            node.transform.SetParent(Root.transform, false);
            node.transform.localPosition = pivot;
            var pieces = new List<CombineInstance>();
            var materials = new List<Material>();
            foreach (var source in sources)
            {
                var filter = source.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                var shared = source.sharedMaterials;
                if (mesh == null || !mesh.isReadable || shared.Length < mesh.subMeshCount)
                    throw new InvalidOperationException("Parça mesh veya malzemesi okunamadı: " + id);
                var matrix = node.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    if (shared[i] == null) throw new InvalidOperationException("Eksik parça malzemesi: " + id);
                    pieces.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = matrix });
                    materials.Add(shared[i]);
                }
            }
            var baked = new Mesh { name = "Workshop " + id, indexFormat = IndexFormat.UInt32 };
            var part = new RobotDamageVisuals.Part { Name = id, Kind = kind, Transform = node.transform, Mesh = baked,
                Threshold = template.Threshold, RestPosition = pivot, RestRotation = Quaternion.identity };
            Parts.Add(part);
            baked.CombineMeshes(pieces.ToArray(), false, true);
            baked.RecalculateBounds();
            if (baked.vertexCount == 0) throw new InvalidOperationException("Boş parça: " + id);
            node.AddComponent<MeshFilter>().sharedMesh = baked;
            node.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
            var overlay = new GameObject("Damage Wear");
            overlay.layer = Root.layer;
            overlay.transform.SetParent(node.transform, false);
            overlay.AddComponent<MeshFilter>().sharedMesh = baked;
            part.Wear = overlay.AddComponent<MeshRenderer>();
            var wear = new Material[materials.Count];
            for (int i = 0; i < wear.Length; i++) wear[i] = template.Wear.sharedMaterial;
            part.Wear.sharedMaterials = wear;
            part.Wear.shadowCastingMode = ShadowCastingMode.Off;
            part.Wear.receiveShadows = false;
            part.Wear.enabled = false;
            return part;
        }

        public void Dispose(bool detach = true)
        {
            if (Root != null)
            {
                Root.SetActive(false);
                if (detach) Root.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(Root);
            }
            foreach (var part in Parts)
                if (part.Mesh != null) UnityEngine.Object.Destroy(part.Mesh);
        }
    }
}
