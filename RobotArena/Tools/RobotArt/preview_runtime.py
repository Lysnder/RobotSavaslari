"""Blender background visual QA of originals against optimized runtime art."""
from pathlib import Path
import sys
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'ArtSource/OptimizationPreview'
OUT.mkdir(parents=True, exist_ok=True)
for relative in ('Robots/BlueCombatRobot.glb', 'Robots/RedLifterRobot.glb', 'ModularArena/SawBlade.glb'):
    if '--saw-only' in sys.argv and 'SawBlade' not in relative:
        continue
    for variant, directory in [('before', ROOT / 'ArtSource/RuntimeOriginals'), ('after', ROOT / 'Assets/Models')]:
        if '--after-only' in sys.argv and variant != 'after':
            continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(directory / relative))
        meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
        low = Vector([min(c[i] for c in corners) for i in range(3)])
        high = Vector([max(c[i] for c in corners) for i in range(3)])
        center = (low + high) / 2
        extent = max(high - low)
        scene = bpy.context.scene
        scene.render.engine = 'BLENDER_WORKBENCH'
        scene.display.shading.light = 'STUDIO'
        scene.display.shading.color_type = 'TEXTURE'
        scene.display.shading.show_shadows = True
        scene.display.shading.show_cavity = True
        scene.render.resolution_x = 768
        scene.render.resolution_y = 768
        scene.render.resolution_percentage = 100
        camera_data = bpy.data.cameras.new('QA')
        camera = bpy.data.objects.new('QA', camera_data)
        scene.collection.objects.link(camera)
        scene.camera = camera
        camera_data.type = 'ORTHO'
        camera_data.ortho_scale = extent * 1.7
        camera.location = center + Vector((1.4, -1.8, 1.5)) * extent
        camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(OUT / f'{Path(relative).stem}_{variant}.png')
        bpy.ops.render.render(write_still=True)
