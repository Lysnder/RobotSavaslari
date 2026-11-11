"""Task-specific Meshy staging client. Never prints or persists credentials.

No automatic POST retry: an ambiguous create must be reconciled in Meshy first.
Raw task responses and source downloads remain in ignored ArtSource.
"""
import argparse
import json
import os
from pathlib import Path
import tomllib
import time
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "ArtSource" / "ModularKit"
STATE = OUT / "tasks.json"
PARTS = {
    "chassis": (12000, "A standalone low-profile modular combat robot chassis, original industrial design for an arena physics game. Rectangular welded steel frame, wide wheel clearance cutouts at four corners, open front mounting bay, layered machined top deck, ventilation slots, exposed structural rails, dark gunmetal brushed steel, restrained blue accents, realistic hard surface engineering. No wheels, no weapon, no tracks, no environment, no text, no logos. Symmetric compact horizontal chassis, isolated object, PBR game asset. Small bolts and scratches as texture detail."),
    "vertical": (6000, "A standalone vertical spinner rotor for a modular combat robot game. One thick circular machined hardened steel disc with four balanced impact teeth and a clear central axle hub. Perfectly balanced rotational silhouette, industrial brushed steel with restrained blue accents, mechanically plausible, realistic hard surface game prop. Rotor only, no chassis, no wheels, no support frame, no background, no text, no logos. Flat circular disc, PBR surface, clean geometry."),
    "drum": (5000, "A standalone cylindrical drum spinner rotor for an original modular combat robot game. Wide hardened steel cylinder with balanced replaceable impact teeth around the circumference, machined end caps and central axle hub at both ends. Industrial brushed steel, dark recessed seams, physically plausible engineering, symmetric rotor, isolated object, realistic PBR game asset. No chassis, no wheels, no brackets, no environment, no text, no logos."),
    "horizontal": (5000, "A standalone horizontal spinning bar weapon rotor for an original modular combat robot game. Long balanced thick steel bar with two aggressive reinforced hammer-like impact ends and a circular machined central hub. Symmetrical around its center, heavy industrial brushed steel, dark gunmetal with blue small accents, realistic PBR hard surface game asset. Bar rotor only, no chassis, no wheels, no support frame, no environment, no text, no logos."),
    "armor_light": (2500, "One standalone lightweight removable side armor plate for a modular arena combat robot game. Wide low rectangular beveled steel plate with layered reinforced rim, four recessed mounting holes, light ventilation detail, thin profile, brushed gunmetal and subtle blue paint, original realistic industrial PBR hard surface game asset. Single flat panel only, no robot, no chassis, no weapon, no environment, no text, no logos."),
    "armor_heavy": (3500, "One standalone heavy removable side armor plate for a modular arena combat robot game. Wide low rectangular thick steel panel, angled reinforced outer face, layered abrasion-resistant steel rim, four recessed mounting holes and visible gusset detail, brushed steel and dark gunmetal, small safety yellow accents, original realistic industrial PBR hard surface game asset. Single armor panel only, no robot, no chassis, no weapon, no environment, no text, no logos."),
}


def request(path, body=None):
    config = tomllib.loads((Path.home() / ".codex/config.toml").read_text(encoding="utf-8"))
    key = os.environ.get("MESHY_API_KEY") or config["mcp_servers"]["meshy"]["env"]["MESHY_API_KEY"]
    req = urllib.request.Request("https://api.meshy.ai" + path,
        data=json.dumps(body).encode() if body is not None else None,
        headers={"Authorization": "Bearer " + key, "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=45) as response:
        return json.load(response)


def save(state):
    OUT.mkdir(parents=True, exist_ok=True)
    temp = STATE.with_suffix(".tmp")
    temp.write_text(json.dumps(state, indent=2), encoding="utf-8")
    for attempt in range(5):
        try:
            temp.replace(STATE)
            break
        except PermissionError:
            if attempt == 4:
                raise
            time.sleep(.2)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=["submit", "status", "refine", "download"])
    parser.add_argument("--parts", nargs="*", choices=list(PARTS), default=list(PARTS))
    args = parser.parse_args()
    state = json.loads(STATE.read_text()) if STATE.exists() else {}
    for name in args.parts:
        item = state.setdefault(name, {})
        if args.action == "submit" and "preview" not in item:
            count, prompt = PARTS[name]
            # Record intent before POST; do not accidentally retry an ambiguous request.
            if item.get("preview_pending"):
                raise RuntimeError(f"Reconcile ambiguous preview in Meshy before retrying: {name}")
            item["preview_pending"] = True
            save(state)
            response = request("/openapi/v2/text-to-3d", {"mode": "preview", "model_type": "smart-topology", "ai_model": "meshy-t2.5", "topology": "triangle", "target_polycount": count, "prompt": prompt, "target_formats": ["glb"]})
            item["preview"] = response["result"]
            item.pop("preview_pending")
            save(state)
        if args.action == "refine" and "refine" not in item:
            if item.get("preview_status") != "SUCCEEDED":
                print(name, "preview not ready")
                continue
            if item.get("refine_pending"):
                raise RuntimeError(f"Reconcile ambiguous refine in Meshy before retrying: {name}")
            item["refine_pending"] = True
            save(state)
            response = request("/openapi/v2/text-to-3d", {"mode": "refine", "preview_task_id": item["preview"], "enable_pbr": True, "texture_resolution": "2k", "target_formats": ["glb"], "texture_prompt": "Industrial combat robot component, brushed gunmetal steel, restrained blue accent paint, realistic metal roughness, subtle edge wear and machining marks. No text, no logos, no baked shadows."})
            item["refine"] = response["result"]
            item.pop("refine_pending")
            save(state)
        if args.action in ("status", "download"):
            kind = "refine" if "refine" in item else "preview"
            if kind not in item:
                continue
            data = request("/openapi/v2/text-to-3d/" + item[kind])
            item[kind + "_status"] = data["status"]
            item[kind + "_progress"] = data.get("progress")
            save(state)
            (OUT / f"{name}_{kind}.json").write_text(json.dumps(data, indent=2), encoding="utf-8")
            print(name, kind, data["status"], data.get("progress"))
            if args.action == "download" and data["status"] == "SUCCEEDED":
                url = data["model_urls"]["glb"]
                target = OUT / f"{name}_{kind}.glb"
                if not target.exists():
                    urllib.request.urlretrieve(url, target)
                print(name, "downloaded", target.stat().st_size)
        else:
            print(name, args.action, "recorded")


if __name__ == "__main__":
    main()
