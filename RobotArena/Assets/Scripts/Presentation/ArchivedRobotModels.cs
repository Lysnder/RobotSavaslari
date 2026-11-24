using System;
using System.Collections.Generic;
using RobotSavaslari.Match;
using RobotSavaslari.UI;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    // A recording may predate the current weapon pairing or describe a different workshop robot.
    // Build missing visuals outside both live robots. Playback owns these meshes until it closes.
    internal sealed class ArchivedRobotModels : IDisposable
    {
        private readonly MatchController match;
        private readonly PlayerRobotSelection catalog;
        private readonly WorkshopSelection workshop;
        private readonly Dictionary<string, MeshRenderer> weapons = new Dictionary<string, MeshRenderer>();
        private WorkshopModel modular;
        private GameObject root;

        public ArchivedRobotModels(MatchController match)
        {
            this.match = match;
            if (!match) return;
            catalog = match.GetComponent<PlayerRobotSelection>();
            workshop = match.GetComponent<WorkshopSelection>();
        }

        public bool Known(string name, bool player)
        {
            var robot = player ? match.Player : match.Opponent;
            if (!robot) return false;
            if (name == robot.name) return true;
            if (!player || !catalog || !catalog.PrepareKit()) return false;
            return name == catalog.StockName(false) || name == catalog.StockName(true) || ReadModular(name, out _);
        }

        private bool ReadModular(string name, out WorkshopLoadout parts)
        {
            parts = default;
            if (!catalog || string.IsNullOrEmpty(name)) return false;
            string prefix = catalog.StockName(false) + " - ";
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var ids = name.Substring(prefix.Length).Split('-');
            if (ids.Length != 3) return false;
            parts = new WorkshopLoadout { chassisId = "standard", tireId = ids[0], armorId = ids[1], weaponId = ids[2] };
            return parts.IsValid;
        }

        private Transform Root
        {
            get
            {
                if (!root)
                {
                    root = new GameObject("Archive model templates");
                    root.SetActive(false);
                }
                return root.transform;
            }
        }

        public MeshRenderer Resolve(string name, bool player, string path, int vertices)
        {
            var robot = player ? match.Player : match.Opponent;
            var node = robot.transform.Find(path);
            const string model = "Robot Model";
            bool modelPath = path == model || path.StartsWith(model + "/", StringComparison.Ordinal);
            if (name == robot.name || !modelPath)
            {
                var existing = Matching(node, vertices);
                if (existing) return existing;
            }
            if (!modelPath || !catalog || !workshop || !catalog.PrepareKit()) return null;
            string partPath = path.Length == model.Length ? "" : path.Substring(model.Length + 1);
            if (player && ReadModular(name, out var parts))
            {
                try { modular ??= workshop.BuildReplayModel(parts, Root); }
                catch (Exception error) when (error is InvalidOperationException || error is ArgumentException || error is UnityException) { return null; }
                if (modular == null) return null;
                return Matching(partPath.Length == 0 ? modular.Root.transform : modular.Root.transform.Find(partPath), vertices);
            }
            var stock = player ? workshop.StockReplayModel : robot.transform.Find(model);
            if (!stock) return null;
            var stockNode = partPath.Length == 0 ? stock : stock.Find(partPath);
            var renderer = Matching(stockNode, vertices);
            if (renderer) return renderer;
            var segments = partPath.Split('/');
            if (segments.Length > 2 || (segments.Length == 2 && segments[1] != "Damage Wear")) return null;
            string key = (player ? "player/" : "bot/") + partPath + "/" + vertices;
            if (weapons.TryGetValue(key, out renderer)) return renderer;
            Mesh mesh;
            Material material;
            if (player)
            {
                bool fork = name == catalog.StockName(true);
                if (segments[0] != (fork ? "Lifter" : "Spinner")) return null;
                mesh = catalog.StockWeaponMesh(fork);
                material = catalog.StockWeaponMaterial(fork);
            }
            else if (!match.TryReplayBotWeapon(segments[0], vertices, out mesh, out material)) return null;
            if (!mesh || mesh.vertexCount != vertices) return null;
            if (segments.Length == 2)
            {
                var wear = stock.Find("Body/Damage Wear");
                if (!wear || !wear.TryGetComponent(out MeshRenderer wearRenderer)) return null;
                material = wearRenderer.sharedMaterial;
            }
            var proxy = new GameObject(segments[0]);
            proxy.transform.SetParent(Root, false);
            proxy.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = proxy.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            weapons.Add(key, renderer);
            return renderer;
        }

        private static MeshRenderer Matching(Transform node, int vertices) =>
            node && node.TryGetComponent(out MeshFilter filter) && filter.sharedMesh && filter.sharedMesh.vertexCount == vertices
                ? node.GetComponent<MeshRenderer>() : null;

        public void Dispose()
        {
            modular?.Dispose(false);
            if (root) UnityEngine.Object.Destroy(root);
        }
    }
}
