using System.Collections.Generic;
using RobotSavaslari.Arena;
using UnityEngine;

namespace RobotSavaslari.Presentation
{
    public sealed partial class ArenaVenueModel
    {
        private const float RingHeight = 2.82f;
        private readonly GameObject[] frames = new GameObject[4];
        private readonly GameObject[] hallWalls = new GameObject[4];
        private readonly List<MeshRenderer> originalFence = new List<MeshRenderer>();
        private readonly List<MeshRenderer> deckTiles = new List<MeshRenderer>();
        private Material originalDeck, brushedDeck;
        private Material glass, wash, stripLight;

        private void BuildRing(ArenaBounds bounds, ArenaLayout layout)
        {
            // Only the old fence's renderers are replaced. Its colliders remain the authority for bots and camera.
            var structure = bounds.transform.parent;
            if (originalFence.Count == 0 && structure)
                foreach (Transform child in structure)
                {
                    string id = child.name;
                    if (!(id.EndsWith(" Mesh Wire") || id.EndsWith(" Mesh Upright") || id.EndsWith(" Kick Plate") ||
                        id.StartsWith("Top Rail ") || id.StartsWith("Bottom Rail ") ||
                        id == "Armored Corner Post" || id == "Corner Safety Band")) continue;
                    if (child.TryGetComponent(out MeshRenderer old) && old.enabled && !old.forceRenderingOff)
                        originalFence.Add(old);
                }
            if (!brushedDeck)
            {
                var tile = bounds.transform.Find("Floor 0-0");
                var sample = tile ? tile.GetComponentInChildren<MeshRenderer>() : null;
                if (sample && sample.sharedMaterial)
                {
                    originalDeck = sample.sharedMaterial;
                    brushedDeck = new Material(originalDeck) { name = "Work-worn ring steel" };
                    if (brushedDeck.HasProperty("metallicRoughnessTexture")) brushedDeck.SetTexture("metallicRoughnessTexture", Texture2D.whiteTexture);
                    if (brushedDeck.HasProperty("roughnessFactor")) brushedDeck.SetFloat("roughnessFactor", .66f);
                    if (brushedDeck.HasProperty("metallicFactor")) brushedDeck.SetFloat("metallicFactor", .62f);
                    if (brushedDeck.HasProperty("baseColorFactor")) brushedDeck.SetColor("baseColorFactor", new Color(.70f,.76f,.80f));
                    foreach (var cell in bounds.GetComponentsInChildren<ArenaCell>())
                        foreach (var renderer in cell.GetComponentsInChildren<MeshRenderer>())
                            if (renderer.sharedMaterial == originalDeck) deckTiles.Add(renderer);
                }
            }
            ApplyVenueSurfaces(isActiveAndEnabled);
            Shader coating = Resources.Load<Shader>("VenueCoating");
            glass = new Material(coating) { name = "Clear safety laminate" };
            glass.SetColor("_Tint", new Color(.52f, .80f, .86f, .105f));
            glass.SetFloat("_Grazing", .15f);
            wash = new Material(coating) { name = "Edge light on steel" };
            wash.SetColor("_Tint", new Color(.98f, .68f, .27f, .46f));
            wash.SetFloat("_Falloff", 1f);
            stripLight = new Material(material) { name = "Warm LED diffuser" };
            stripLight.SetColor(EmissionColor, new Color(1.25f, .77f, .30f));
            for (int side = 0; side < sides.Length; side++)
            {
                var anchor = new GameObject("Ring edge").transform;
                anchor.SetParent(scenery.transform, false);
                anchor.localPosition = sides[side].localPosition;
                anchor.localRotation = sides[side].localRotation;
                float span = 2 * (side < 2 ? half.x : half.y);
                BuildFrame(anchor, side, span);
                BuildHallWall(anchor, side, span);
            }
            MarkFloor(layout);
        }

        private void BuildHallWall(Transform anchor, int side, float span)
        {
            // The wall and catwalk sit behind the seating, leaving an access aisle around the ring.
            var shell = new Geometry();
            float width = span + 11.6f;
            const float back = 5.72f;
            Ink stripe = side == 2 ? Ink.Blue : side == 3 ? Ink.Orange : Ink.Cap;
            shell.Block(0, -.44f, back - .3f, width, .22f, 1.2f, Ink.Frame);
            shell.Block(0, 2.30f, back, width, 5.60f, .19f, Ink.Navy);
            shell.Block(0, 1.20f, back - .12f, width, .13f, .13f, stripe);
            shell.Block(0, 3.28f, back - .24f, width, .18f, .42f, Ink.Frame);
            shell.Block(0, 4.95f, back - .28f, width, .31f, .53f, Ink.Frame);
            int bays = Mathf.CeilToInt(width / 3.8f);
            float pitch = width / bays;
            for (int i = 0; i < bays; i++)
            {
                float x = (i + .5f) * pitch - width * .5f;
                shell.Block(x - pitch * .47f, 2.35f, back - .15f, .20f, 5.4f, .22f, Ink.Frame);
                shell.Beam(new Vector3(x - pitch * .45f, 3.32f, back - .31f),
                    new Vector3(x + pitch * .45f, 4.8f, back - .31f), .09f, Ink.Steel);
                shell.Block(x, 2.76f, back - .14f, pitch * .75f, .09f, .12f, Ink.Steel);
                shell.Block(x, 4.75f, back - .31f, pitch * .56f, .052f, .11f, Ink.Ice);
                if (i % 2 != 0) continue;
                shell.Block(x, 2.04f, back - .19f, 1.14f, 1.25f, .08f, Ink.Frame);
                shell.Block(x - .49f, 2.04f, back - .25f, .095f, 1.15f, .06f, stripe);
                Digit(shell, side == 3 || side == 1 ? 2 : 1, x + .05f, 2.04f, back - .26f, .76f, Ink.Ivory);
            }
            hallWalls[side] = Draw(anchor, "Hall wall and lighting truss", shell).gameObject;
        }

        private void BuildFrame(Transform anchor, int side, float span)
        {
            var frame = new GameObject("Protective frame");
            frames[side] = frame;
            frame.transform.SetParent(anchor, false);
            var steel = new Geometry();
            var panes = new Geometry();
            var leds = new Geometry();
            const float baseHeight = .43f;
            int bays = Mathf.Max(1, Mathf.RoundToInt(span / 2.7f));
            float pitch = span / bays;
            steel.Block(0, .15f, .013f, span, .51f, .19f, Ink.Frame);
            steel.Block(0, .19f, -.09f, span, .29f, .028f, Ink.Amber);
            steel.Block(0, baseHeight, .022f, span, .08f, .20f, Ink.Steel);
            steel.Block(0, RingHeight, .036f, span + .12f, .125f, .16f, Ink.Frame);
            steel.Block(0, RingHeight + .069f, .036f, span, .022f, .13f, Ink.Cap);
            for (int bay = 0; bay <= bays; bay++)
            {
                float left = -.5f * span + bay * pitch;
                steel.Block(left, 1.41f, .036f, .105f, RingHeight, .145f, Ink.Frame);
                steel.Block(left, .30f, -.055f, .22f, .62f, .15f, Ink.Steel);
                steel.Block(left, RingHeight - .12f, .02f, .19f, .22f, .18f, Ink.Steel);
                if (bay == bays) continue;
                float a = left + .061f, b = left + pitch - .061f;
                panes.Sheet(new Vector3(a, baseHeight, .039f), new Vector3(b, baseHeight, .039f),
                    new Vector3(b, RingHeight - .04f, .039f), new Vector3(a, RingHeight - .04f, .039f));
                leds.Block(left + pitch * .5f, .365f, -.096f, pitch - .34f, .033f, .046f, Ink.White);
                for (int stripe = 0; stripe < 3; stripe++)
                {
                    float x = left + .23f + stripe * .135f;
                    steel.Beam(new Vector3(x, .064f, -.112f), new Vector3(x + .095f, .279f, -.112f), .05f, Ink.Frame);
                }
            }
            Draw(frame.transform, "Steel posts and yellow kick rail", steel);
            Draw(frame.transform, "Clear safety panels", panes, glass);
            Draw(frame.transform, "Edge emitters", leds, stripLight);

            // Wide indirect spill remains visible between the practical lights and in the low tier.
            var spill = new Geometry();
            float h = .013f, w = span * .5f - .14f;
            spill.Sheet(new Vector3(-w, h, -.13f), new Vector3(w, h, -.13f),
                new Vector3(w, h, -1.11f), new Vector3(-w, h, -1.11f));
            Draw(anchor, "Floor light spill", spill, wash);
            if (!economy)
            {
                var lamp = new GameObject("Grazing edge light").AddComponent<Light>();
                lamp.transform.SetParent(anchor, false);
                lamp.transform.localPosition = new Vector3(0, .48f, -.1f);
                lamp.type = LightType.Point;
                lamp.range = 3.4f;
                lamp.intensity = 1.15f;
                lamp.color = new Color(1f, .77f, .48f);
                lamp.shadows = LightShadows.None;
            }
        }

        private void MarkFloor(ArenaLayout layout)
        {
            var wear = new Geometry();
            var markings = new Geometry();
            foreach (var cell in layout.cells)
            {
                if (cell.content != ArenaContent.Floor) continue;
                Vector2 p = ArenaLayout.Position(cell.coordinate);
                var random = new System.Random(519 + cell.coordinate.y * 71 + cell.coordinate.x * 209);
                float angle = (float)random.NextDouble() * Mathf.PI;
                var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var across = new Vector2(-along.y, along.x);
                var start = p + new Vector2((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f);
                for (int scratch = 0; scratch < 5; scratch++)
                {
                    var a = start + across * (.02f * scratch) + along * (.14f * (float)random.NextDouble());
                    FloorLine(wear, a, a + along * (.11f + .47f * (float)random.NextDouble()), .009f, .008f,
                        scratch % 3 == 0 ? Ink.Nosing : Ink.Steel);
                }
                if (cell.spawn == ArenaSpawn.None) continue;
                Ink paint = cell.spawn == ArenaSpawn.Player ? Ink.Blue : Ink.Orange;
                float forward = p.x < 0 ? 1 : -1;
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var corner = p + new Vector2(sx, sz) * .91f;
                    FloorLine(markings, corner, corner - new Vector2(.57f * sx, 0), .12f, .017f, paint);
                    FloorLine(markings, corner, corner - new Vector2(0, .61f * sz), .12f, .017f, paint);
                }
                int bars = cell.spawn == ArenaSpawn.Player ? 1 : 2;
                for (int n = 0; n < bars; n++)
                {
                    Vector2 head = p + new Vector2(forward * (.82f - n * .21f), 0);
                    for (int s = -1; s <= 1; s += 2)
                        FloorLine(markings, head, head + new Vector2(-forward * .14f, s * .20f), .06f, .017f, paint);
                }
            }
            Draw(scenery.transform, "Scored steel floor", wear);
            Draw(scenery.transform, "Team launch markings", markings);
        }

        private static void FloorLine(Geometry mesh, Vector2 a, Vector2 b, float width, float y, Ink ink)
        {
            Vector2 axis = (b - a).normalized;
            Vector2 n = new Vector2(-axis.y, axis.x) * (width * .5f);
            Vector3 At(Vector2 p) => new Vector3(p.x, y, p.y);
            mesh.Face(At(a - n), At(a + n), At(b + n), At(b - n), ink);
        }

        private void ApplyVenueSurfaces(bool active)
        {
            foreach (var renderer in originalFence)
                if (renderer) renderer.forceRenderingOff = active;
            foreach (var tile in deckTiles)
                if (tile) tile.sharedMaterial = active ? brushedDeck : originalDeck;
        }

        private void OnEnable() => ApplyVenueSurfaces(true);
        private void OnDisable() => ApplyVenueSurfaces(false);
    }
}
