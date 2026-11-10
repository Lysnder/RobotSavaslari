# Runtime art workflow

Commit only models used by scenes, prefabs, materials, or editor builders.
Runtime `.glb` files use Git LFS. Keep their `.meta` files: Unity references
depend on the asset GUID and imported subasset identities.

`ArtSource/` is a local, ignored directory. Blender sources,
automatic `.blend1`/`.blend2` saves, unused articulated variants, and original
runtime exports are preserved there on the artist's machine. They are **not
included in a fresh clone**. Back up editable sources in shared art storage
before deleting this working copy; no remote art archive is configured here.

Export static meshes with positions, normals, UVs, and indexed triangles.
Aim for 120k triangles per robot and 20k per arena part; use textures no larger
than 2048px. The guard allows 150k triangles and 45 MB per GLB, 160 MB total.
These are initial budgets; check silhouettes and gameplay camera appearance
in Unity before accepting new art.

From the repository root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --python-exit-code 1 --python RobotArena/Tools/RobotArt/optimize_glb.py -- --apply
python RobotArena/Tools/RobotArt/check_glb.py
```

Without `--apply`, optimization only reports projected sizes. Applying saves
a first-run original under `ArtSource/RuntimeOriginals/`, preserves GLB JSON
node/material identities, reduces static geometry with Blender Decimate, and
resizes embedded textures. Geometry simplification is lossy. Restore from the
local originals for further art work; avoid repeatedly simplifying a model.
Repeated runs use the first-run original when available, rather than further
simplifying the runtime mesh. This script does not support skinned/morph
meshes, animation, or additional attributes. `preview_runtime.py` renders the
original and optimized robots and saw blade for visual comparison in the
ignored `ArtSource/OptimizationPreview/` directory.

Removing tracked source files reduces the current checkout and future clone
downloads. Existing LFS objects remain in old commits and remote storage.
Reclaiming that historical storage requires a separately coordinated history
rewrite and host-side LFS cleanup; this commit does not rewrite shared history.
