"""Validate the isolated kit, including per-assembly—not just per-file—budgets."""
import io
import json
from pathlib import Path
from PIL import Image
from check_glb import load

ROOT = Path(__file__).resolve().parents[2]
FOLDER = ROOT / "Assets/Art/ModularRobots/Models"
stats = {}
for path in sorted(FOLDER.glob("*.glb")):
    doc, binary = load(path)
    triangles = sum(doc["accessors"][p["indices"]]["count"] // 3 for m in doc["meshes"] for p in m["primitives"])
    assert path.stat().st_size < 20_000_000
    names = {n.get("name") for n in doc["nodes"]}
    if path.stem.startswith("KitChassis"):
        assert {"WeaponMount_A", "WheelMount_FL", "WheelMount_FR", "WheelMount_RL", "WheelMount_RR", "ArmorMount_L", "ArmorMount_R"}.issubset(names)
    if path.stem.startswith("Weapon_"):
        assert any(n and n.startswith("Rotor_") for n in names)
    for image in doc.get("images", []):
        view = doc["bufferViews"][image["bufferView"]]
        start = view.get("byteOffset", 0)
        with Image.open(io.BytesIO(binary[start:start + view["byteLength"]])) as im:
            assert max(im.size) <= 2048
    stats[path.stem] = {"triangles": triangles, "bytes": path.stat().st_size}

assemblies = {}
for weapon in ["vertical", "drum", "horizontal"]:
    for armor in ["light", "heavy"]:
        levels = []
        for level in range(3):
            suffix = "" if level == 0 else f"_LOD{level}"
            parts = [("KitChassis", 1), ("KitWheel", 4), ("Weapon_" + weapon, 1), ("Armor_armor_" + armor, 2)]
            tris = sum(stats[name + suffix]["triangles"] * count for name, count in parts)
            # Reused wheel / armor geometry and textures are stored once per variant.
            size = sum(stats[name + suffix]["bytes"] for name, _ in parts)
            assert tris <= [45000, 22000, 11000][level], (weapon, armor, level, tris)
            assert size <= 20_000_000
            levels.append({"triangles": tris, "unique_module_bytes": size})
        assert levels[0]["triangles"] > levels[1]["triangles"] > levels[2]["triangles"]
        assemblies[weapon + "_" + armor] = levels
result = {"files": stats, "assemblies": assemblies}
(FOLDER.parent / "validated-budget.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(assemblies, indent=2))
