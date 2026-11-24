using System.Collections.Generic;
using RobotSavaslari.Combat;
using UnityEngine;

namespace RobotSavaslari.UI
{
    // Mesh coordinates follow robot-local forward. Preview, detachable parts and the bot use the same artwork.
    internal static class WorkshopWeaponArt
    {
        internal static Mesh Build(string id, Mesh block, Mesh roller)
        {
            var pieces = new List<CombineInstance>();
            void Add(Mesh mesh, Vector3 pos, Vector3 size, Vector3 angles = default)
            {
                pieces.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, Quaternion.Euler(angles), size) });
            }

            switch (id)
            {
                case "drum":
                    Add(roller, Vector3.zero, new Vector3(.42f, .27f, .42f), new Vector3(0, 0, 90));
                    for (int i = 0; i < 4; i++)
                    {
                        var rotation = Quaternion.Euler(i * 90f, 0, 0);
                        Add(block, rotation * Vector3.forward * .21f, new Vector3(.45f, .06f, .08f), rotation.eulerAngles);
                    }
                    break;
                case "hammer":
                    Add(block, new Vector3(0, .13f, .22f), new Vector3(.10f, .12f, .56f), new Vector3(-22, 0, 0));
                    Add(block, new Vector3(0, .20f, .50f), new Vector3(.39f, .21f, .18f));
                    break;
                case "flipper":
                    Add(block, new Vector3(0, -.05f, .19f), new Vector3(.45f, .09f, .45f), new Vector3(14, 0, 0));
                    Add(block, new Vector3(0, -.12f, .43f), new Vector3(.55f, .07f, .14f));
                    break;
                case "clamp":
                    Add(block, Vector3.zero, new Vector3(.52f, .10f, .12f));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Add(block, new Vector3(side * .235f, .04f, .17f), new Vector3(.09f, .12f, .37f));
                        Add(block, new Vector3(side * .18f, .01f, .36f), new Vector3(.09f, .16f, .20f), new Vector3(12, -side * 38, 0));
                    }
                    break;
                case "saw":
                    Add(roller, Vector3.zero, new Vector3(.52f, .025f, .52f), new Vector3(90, 0, 0));
                    for (int i = 0; i < 12; i++)
                    {
                        var rotation = Quaternion.Euler(0, 0, i * 30f);
                        Add(block, rotation * Vector3.up * .26f, new Vector3(.068f, .095f, .048f), new Vector3(0, 0, i * 30f - 20f));
                    }
                    break;
                default: return null;
            }
            var mesh = new Mesh { name = WorkshopWeapons.Part(id) };
            mesh.CombineMeshes(pieces.ToArray());
            return mesh;
        }
    }
}
