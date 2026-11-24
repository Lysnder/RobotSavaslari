using System.Collections.Generic;
using RobotSavaslari.Arena;
using UnityEngine;
using UnityEngine.Rendering;

namespace RobotSavaslari.Presentation
{
    [DisallowMultipleComponent]
    public sealed class ArenaVenueModel : MonoBehaviour
    {
        private enum Ink { Steel, Frame, Concrete, Nosing, Seat, Navy, Blue, Orange, Amber, Ice, Ivory, Skin, Ochre, Cap, DarkSkin, White }

        private static readonly Color32[] Swatches = {
            new Color32(53,64,76,255), new Color32(26,33,42,255), new Color32(87,94,99,255), new Color32(139,150,158,255),
            new Color32(62,80,101,255), new Color32(43,59,76,255), new Color32(63,159,230,255), new Color32(226,128,61,255),
            new Color32(234,171,78,255), new Color32(180,221,238,255), new Color32(215,211,191,255), new Color32(177,135,108,255),
            new Color32(147,109,75,255), new Color32(89,103,119,255), new Color32(116,83,67,255), new Color32(255,255,255,255)
        };
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Transform[] sides = new Transform[4];
        private readonly Transform[] spectators = new Transform[6];
        private readonly Transform[] pilots = new Transform[2];
        private readonly MeshRenderer[] lamps = new MeshRenderer[3];
        private readonly float[] lampLevels = { -1, -1, -1 };
        private MaterialPropertyBlock emission;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private Material material;
        private Texture2D atlas;
        private Camera view;
        private GameObject scenery;
        private ArenaLayout current;
        private Vector2 half;
        private bool economy;
        private float blueEnd;

        public void Build(ArenaLayout layout, bool low)
        {
            if (scenery && current == layout && economy == low) return;
            Release();
            current = layout;
            economy = low;
            var bounds = GetComponentInParent<ArenaBounds>() ?? FindAnyObjectByType<ArenaBounds>();
            if (!layout || !bounds) return;
            half = bounds.HalfExtents;
            scenery = new GameObject("Arena stands and team booths");
            scenery.transform.SetParent(transform, false);
            scenery.transform.SetPositionAndRotation(new Vector3(bounds.Center.x, bounds.SurfaceHeight, bounds.Center.y), Quaternion.identity);
            view = Camera.main;
            emission = new MaterialPropertyBlock();
            atlas = new Texture2D(Swatches.Length, 1, TextureFormat.RGBA32, false) { name = "Venue metal and uniforms", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            atlas.SetPixels32(Swatches);
            atlas.Apply(false, true);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Venue palette" };
            material.SetTexture("_BaseMap", atlas);
            material.SetFloat("_Smoothness", .22f);
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColor, Color.black);
            var blueSpawn = layout.cells.Find(cell => cell.spawn == ArenaSpawn.Player);
            blueEnd = blueSpawn != null && ArenaLayout.Position(blueSpawn.coordinate).x > 0 ? 1f : -1f;

            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? 1 : -1;
                var bank = Side(i, i == 0 ? "North grandstand" : "South grandstand", new Vector3(0, 0, sign * half.y), i * 180);
                BuildStand(bank, i);
                float end = sign * blueEnd;
                var cabin = Side(i + 2, i == 0 ? "Team 1 - blue booth" : "Team 2 - orange booth", new Vector3(end * half.x, 0, 0), end * 90);
                BuildCabin(cabin, i);
            }
            var clock = new Geometry();
            // Countdown indicators sit on the middle grandstand fascia, away from the hazard grid.
            for (int i = -1; i <= 1; i++)
                clock.Block(i * .22f, .54f, 1.31f, .13f, .09f, .11f, Ink.White);
            lamps[2] = Draw(sides[0], "Referee signal", clock);
            Present(0, 0, 0, 0, 0);
        }

        private Transform Side(int index, string label, Vector3 place, float yaw)
        {
            var side = new GameObject(label).transform;
            side.SetParent(scenery.transform, false);
            side.localPosition = place;
            side.localRotation = Quaternion.Euler(0, yaw, 0);
            sides[index] = side;
            return side;
        }

        private void BuildStand(Transform parent, int side)
        {
            var steel = new Geometry();
            float pitch = (half.x * 2 - 1.2f) / 3;
            int rows = economy ? 2 : 3;
            for (int bay = 0; bay < 3; bay++)
            {
                float x = (bay - 1) * pitch;
                var people = economy ? steel : new Geometry();
                for (int row = 0; row < rows; row++)
                {
                    float z = 1.68f + row * .78f, y = -.32f + row * .34f;
                    float width = pitch - .52f;
                    steel.Block(x, y - .18f, z, width, .36f, .79f, Ink.Steel);
                    steel.Block(x, y + .015f, z - .33f, width, .045f, .09f, Ink.Nosing);
                    steel.Block(x, y + .17f, z + .10f, width - .14f, .10f, .29f, Ink.Seat);
                    int count = economy ? 3 : 6;
                    for (int seat = 0; seat < count; seat++)
                    {
                        int pattern = side * 59 + bay * 17 + row * 7 + seat;
                        if (pattern % 9 == 3) continue;
                        float px = x + ((seat + .5f) / count - .5f) * (width - .20f);
                        Spectator(people, px, y + .21f, z + .06f, pattern, !economy && row == 0);
                    }
                }
                if (!economy)
                    spectators[side * 3 + bay] = Draw(parent, "Supporters bay " + (bay + 1), people).transform;
                float back = 1.77f + rows * .78f, high = (rows - 1) * .34f;
                steel.Beam(new Vector3(x - pitch * .46f, -.42f, back), new Vector3(x - pitch * .46f, high + .33f, back), .055f, Ink.Frame);
                steel.Beam(new Vector3(x + pitch * .46f, -.42f, back), new Vector3(x + pitch * .46f, high + .33f, back), .055f, Ink.Frame);
                steel.Block(x, high + .32f, back, pitch - .12f, .055f, .055f, Ink.Nosing);
                if (bay < 2)
                    for (int step = 0; step < rows * 2; step++)
                        steel.Block(x + pitch * .5f, -.40f + step * .17f, 1.41f + step * .39f,
                            .38f, .10f, .40f, step % 2 == 0 ? Ink.Concrete : Ink.Nosing);
            }
            Draw(parent, "Steel tiers and aisles", steel);
        }

        private static void Spectator(Geometry mesh, float x, float y, float z, int id, bool detailed)
        {
            Ink jacket = (id % 5) switch { 0 => Ink.Cap, 1 => Ink.Ochre, 2 => Ink.Navy, 3 => Ink.Steel, _ => Ink.Seat };
            Ink skin = id % 3 == 0 ? Ink.DarkSkin : Ink.Skin;
            float k = .92f + (id % 4) * .045f;
            mesh.Taper(new Vector3(x, y + .18f * k, z), .20f, .16f, .31f, .20f, .33f * k, jacket, Quaternion.identity);
            mesh.Head(new Vector3(x, y + .44f * k, z - .008f), .105f, .19f, skin);
            mesh.Head(new Vector3(x, y + .52f * k, z + .009f), .109f, .075f, id % 2 == 0 ? Ink.Frame : jacket);
            if (!detailed) return;
            for (int i = -1; i <= 1; i += 2)
            {
                mesh.Beam(new Vector3(x + i * .15f, y + .30f * k, z), new Vector3(x + i * .18f, y + .12f * k, z - .11f), .075f, jacket);
                mesh.Beam(new Vector3(x + i * .068f, y + .035f, z - .11f), new Vector3(x + i * .075f, y - .12f, z - .19f), .095f, Ink.Frame);
            }
        }

        private void BuildCabin(Transform parent, int team)
        {
            Ink color = team == 0 ? Ink.Blue : Ink.Orange;
            var steel = new Geometry();
            steel.Block(0, -.22f, 2.20f, 3.02f, .25f, 1.74f, Ink.Frame);
            steel.Block(0, -.077f, 2.20f, 3.07f, .055f, 1.77f, Ink.Nosing);
            // Open front preserves the sight line to the pilot and the two desk monitors.
            steel.Block(0, .31f, 1.53f, 2.30f, .63f, .24f, Ink.Steel);
            steel.Block(0, .65f, 1.64f, 2.51f, .10f, .53f, Ink.Nosing);
            steel.Block(0, .46f, 1.393f, 2.24f, .076f, .028f, color);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                steel.Block(sign * 1.32f, .64f, 2.78f, .09f, 1.46f, .11f, Ink.Steel);
                steel.Beam(new Vector3(sign * 1.32f, 1.32f, 2.78f), new Vector3(sign * 1.32f, 1.45f, 2.30f), .066f, Ink.Nosing);
                steel.Block(sign * .48f, .79f, 1.79f, .055f, .19f, .08f, Ink.Frame);
                steel.Block(sign * .48f, .92f, 1.76f, .59f, .29f, .072f, Ink.Frame);
                steel.Block(sign * .48f, .93f, 1.802f, .50f, .18f, .018f, Ink.Ice);
                // The arena-facing monitor backs carry the team stripes.
                for (int line = 0; line <= team; line++)
                    steel.Block(sign * .48f - .03f + line * .09f, .91f, 1.72f, .045f, .16f, .018f, color);
            }
            steel.Block(0, 1.42f, 2.54f, 2.85f, .13f, .60f, Ink.Steel);
            steel.Block(0, 1.34f, 2.31f, 2.57f, .043f, .082f, Ink.Ice);
            steel.Block(-1.01f, .97f, 2.72f, .57f, .79f, .072f, Ink.Frame);
            Digit(steel, team + 1, -1.02f, .97f, 2.67f, .66f, Ink.Ivory);
            // Same marking on the fascia is visible from both follow and elevated duel views.
            Digit(steel, team + 1, -.89f, .26f, 1.386f, .37f, Ink.Ivory);
            var people = economy ? steel : new Geometry();
            Pilot(people, .12f, 2.24f, color, true);
            if (!economy) Pilot(people, .91f, 2.43f, Ink.Navy, false);
            if (!economy) pilots[team] = Draw(parent, "Team pilots", people).transform;
            Draw(parent, "Control desk and light gantry", steel);

            var signal = new Geometry();
            signal.Block(.73f, 1.44f, 2.22f, .87f, .065f, .063f, Ink.White);
            signal.Block(.67f, .41f, 1.39f, .76f, .052f, .045f, Ink.White);
            lamps[team] = Draw(parent, "Team signal " + (team + 1), signal);
        }

        private static void Pilot(Geometry g, float x, float z, Ink jacket, bool primary)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                g.Beam(new Vector3(x + side * .075f, .43f, z), new Vector3(x + side * .11f, .06f, z + .02f), .12f, Ink.Frame);
                g.Block(x + side * .11f, .012f, z - .028f, .13f, .07f, .19f, Ink.Steel);
                g.Beam(new Vector3(x + side * .15f, .74f, z), new Vector3(x + side * .21f, .56f, z - .08f), .096f, jacket);
                g.Beam(new Vector3(x + side * .21f, .56f, z - .08f), new Vector3(x + side * .14f, .62f, z - .25f), .073f, Ink.Cap);
            }
            g.Taper(new Vector3(x, .605f, z), .22f, .19f, .36f, .21f, .39f, jacket, Quaternion.identity);
            g.Head(new Vector3(x, .92f, z - .014f), .119f, .23f, primary ? Ink.Skin : Ink.DarkSkin);
            g.Head(new Vector3(x, 1.027f, z), .122f, .058f, Ink.Frame);
            g.Block(x + .12f, .94f, z, .045f, .11f, .09f, Ink.Frame);
            g.Block(x + .075f, .875f, z - .09f, .087f, .023f, .028f, Ink.Frame);
        }

        private static void Digit(Geometry g, int n, float x, float y, float z, float scale, Ink ink)
        {
            float w = .52f * scale, h = scale, t = .095f * scale;
            if (n == 1)
            {
                g.Block(x, y, z, t, h, .031f, ink);
                g.Block(x, y - h * .45f, z, w * .65f, t, .031f, ink);
                g.Beam(new Vector3(x - w * .21f, y + h * .28f, z), new Vector3(x, y + h * .46f, z), t, ink);
                return;
            }
            for (int i = -1; i <= 1; i++) g.Block(x, y + i * (h - t) * .5f, z, w, t, .031f, ink);
            g.Block(x + w * .40f, y + h * .23f, z, t, h * .47f, .031f, ink);
            g.Block(x - w * .40f, y - h * .23f, z, t, h * .47f, .031f, ink);
        }

        public void Present(float time, float cheer, float blueSignal, float orangeSignal, float amberSignal)
        {
            if (!scenery) return;
            if (!view) view = Camera.main;
            if (view)
            {
                var p = scenery.transform.InverseTransformPoint(view.transform.position);
                for (int i = 0; i < 4; i++)
                {
                    float edge = i == 0 ? p.z - half.y : i == 1 ? -p.z - half.y : i == 2 ? blueEnd * p.x - half.x : -blueEnd * p.x - half.x;
                    float across = i < 2 ? Mathf.Abs(p.x) : Mathf.Abs(p.z);
                    // Sightline from the lens to a robot at the nearest legal edge.
                    float sightHeight = .35f + (p.y - .35f) * 2.0f / Mathf.Max(2.0f, edge + .7f);
                    bool obscure = edge > .85f && across < (i < 2 ? half.x + .6f : 1.9f) && sightHeight < (i < 2 ? 1.85f : 1.75f);
                    if (sides[i].gameObject.activeSelf == obscure) sides[i].gameObject.SetActive(!obscure);
                }
            }
            if (!economy)
            {
                float strength = Mathf.Clamp01(cheer);
                for (int i = 0; i < spectators.Length; i++)
                {
                    float wave = Mathf.Sin(time * (4.1f + i * .09f) + i * 1.67f);
                    spectators[i].localPosition = Vector3.up * (.009f * Mathf.Sin(time * 1.7f + i) + strength * .115f * (.5f + .5f * wave));
                }
                for (int i = 0; i < 2; i++)
                    pilots[i].localPosition = new Vector3(.012f * Mathf.Sin(time * 1.2f + i * 2.2f), strength * .027f, 0);
            }
            Signal(0, blueSignal, Ink.Blue);
            Signal(1, orangeSignal, Ink.Orange);
            Signal(2, amberSignal, Ink.Amber);
        }

        private void Signal(int index, float value, Ink ink)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(lampLevels[index] - value) < .01f) return;
            lampLevels[index] = value;
            Color color = Swatches[(int)ink];
            emission.SetColor(BaseColor, color * (.28f + value * .72f));
            emission.SetColor(EmissionColor, color * (.10f + value * 1.2f));
            lamps[index].SetPropertyBlock(emission);
        }

        private MeshRenderer Draw(Transform parent, string label, Geometry geometry)
        {
            var node = new GameObject(label);
            node.transform.SetParent(parent, false);
            var mesh = geometry.Finish(label);
            meshes.Add(mesh);
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private void Release()
        {
            if (scenery) { scenery.SetActive(false); Destroy(scenery); }
            foreach (var mesh in meshes) if (mesh) Destroy(mesh);
            meshes.Clear();
            if (material) Destroy(material);
            if (atlas) Destroy(atlas);
            for (int i = 0; i < lampLevels.Length; i++) lampLevels[i] = -1;
            scenery = null;
        }

        private void OnDestroy() => Release();

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> indices = new List<int>();

            public void Block(float x, float y, float z, float w, float h, float d, Ink ink) =>
                Taper(new Vector3(x, y, z), w, d, w, d, h, ink, Quaternion.identity);

            public void Beam(Vector3 a, Vector3 b, float width, Ink ink) =>
                Taper((a + b) * .5f, width, width, width, width, (b - a).magnitude, ink, Quaternion.FromToRotation(Vector3.up, b - a));

            public void Taper(Vector3 c, float w0, float d0, float w1, float d1, float height, Ink ink, Quaternion rotation)
            {
                Vector3 P(float x, float y, float z) => c + rotation * new Vector3(x * .5f, y * .5f, z * .5f);
                Vector3 a = P(-w0, -height, -d0), b = P(w0, -height, -d0), d = P(-w0, -height, d0), e = P(w0, -height, d0);
                Vector3 f = P(-w1, height, -d1), g = P(w1, height, -d1), h = P(w1, height, d1), j = P(-w1, height, d1);
                Face(a, f, g, b, ink); Face(b, g, h, e, ink); Face(e, h, j, d, ink); Face(d, j, f, a, ink);
                Face(f, j, h, g, ink); Face(a, b, e, d, ink);
            }

            public void Head(Vector3 c, float radius, float height, Ink ink)
            {
                const int facets = 6;
                Vector3 up = Vector3.up * height * .5f;
                for (int i = 0; i < facets; i++)
                {
                    float a = i * Mathf.PI * 2 / facets, b = (i + 1) * Mathf.PI * 2 / facets;
                    Vector3 v = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                    Vector3 w = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius;
                    Face(c + v - up, c + v + up, c + w + up, c + w - up, ink);
                    Triangle(c + up, c + w + up, c + v + up, ink);
                }
            }

            private void Triangle(Vector3 a, Vector3 b, Vector3 c, Ink ink)
            {
                int start = vertices.Count;
                Point(a, ink); Point(b, ink); Point(c, ink);
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            }

            private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Ink ink)
            {
                int start = vertices.Count;
                Point(a, ink); Point(b, ink); Point(c, ink); Point(d, ink);
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            }

            private void Point(Vector3 p, Ink ink)
            {
                vertices.Add(p);
                uv.Add(new Vector2(((int)ink + .5f) / Swatches.Length, .5f));
            }

            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
