"""Blender post-processing of the Meshy kit. Does not edit shared robot assets."""
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/ModularKit"
OUT = ROOT / "Assets/Burak/ModularRobots/Models"
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
report = {}


def bounds(objects):
    p = [o.matrix_world @ Vector(c) for o in objects for c in o.bound_box]
    return Vector([min(v[i] for v in p) for i in range(3)]), Vector([max(v[i] for v in p) for i in range(3)])


def empty(name, parent=None, pos=(0, 0, 0)):
    o = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(o)
    o.parent = parent
    o.location = pos
    return o


def material(name, rgb, metal, rough):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*rgb, 1)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    return m


steel = material("Mount Steel", (.15, .19, .23), .85, .3)
rubber = material("Tire Rubber", (.025, .03, .035), 0, .8)


def cylinder(name, radius, depth, mat, parent, pos=(0, 0, 0), axis="X", vertices=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth)
    o = bpy.context.object
    o.name = name
    if axis == "X": o.rotation_euler.y = math.pi / 2
    elif axis == "Y": o.rotation_euler.x = math.pi / 2
    o.parent = parent
    o.location = pos
    o.data.materials.append(mat)
    if vertices >= 24:
        bevel = o.modifiers.new("Machined edge", "BEVEL")
        bevel.width = .006
        bevel.segments = 2
    return o


def import_part(name, dims, orientation="flat"):
    before = set(bpy.data.objects)
    path = SOURCE / f"{name}_refine.glb"
    if not path.exists(): raise RuntimeError(f"Missing textured Meshy source: {path}")
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported = list(set(bpy.data.objects) - before)
    meshes = [o for o in imported if o.type == "MESH"]
    # Bake imported node transforms before reorienting the visual shell.
    for o in meshes:
        world = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = world
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    non_meshes = [o for o in imported if o.type != "MESH"]
    bpy.ops.object.join()
    visual = bpy.context.object
    for o in non_meshes:
        if o.name in bpy.data.objects: bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.view_layer.update()
    # PCA-like axis sorting from AABB: long axis along X, thin along Z.
    size = visual.dimensions
    longest = max(range(3), key=lambda i: size[i])
    thinnest = min(range(3), key=lambda i: size[i])
    if orientation == "disc":
        if thinnest == 2: visual.rotation_euler.y = math.pi / 2
        elif thinnest == 1: visual.rotation_euler.z = math.pi / 2
    else:
        if longest == 1: visual.rotation_euler.z = math.pi / 2
        elif longest == 2: visual.rotation_euler.y = math.pi / 2
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.context.view_layer.update()
    lo, hi = bounds([visual])
    center = (lo + hi) / 2
    for v in visual.data.vertices:
        v.co -= center
    size = hi - lo
    for v in visual.data.vertices:
        for i in range(3): v.co[i] *= dims[i] / max(size[i], .00001)
    visual.name = name + "_Visual"
    visual.location = (0, 0, 0)
    images = {n.image for m in visual.data.materials if m and m.use_nodes for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image}
    limit = 2048 if name == "chassis" else 1024
    for image in images:
        if max(image.size) > limit:
            factor = limit / max(image.size)
            image.scale(max(1, round(image.size[0] * factor)), max(1, round(image.size[1] * factor)))
    return visual


def export_module(root):
    objects = [root] + list(root.children_recursive)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active = root
    # All pivots and geometry are local to a zero-position module root.
    file = OUT / (root.name + ".glb")
    bpy.ops.export_scene.gltf(filepath=str(file), export_format="GLB", use_selection=True, export_animations=False, export_apply=True, export_extras=True)
    tris = 0
    for o in objects:
        if o.type == "MESH":
            evaluated = o.evaluated_get(bpy.context.evaluated_depsgraph_get())
            mesh = evaluated.to_mesh()
            mesh.calc_loop_triangles()
            tris += len(mesh.loop_triangles)
            evaluated.to_mesh_clear()
    report[root.name] = {"triangles": tris, "bytes": file.stat().st_size}
    assert file.stat().st_size < 20_000_000, (root.name, "module file budget")
    for level, ratio in [(1, .5), (2, .12)]:
        modifiers = []
        for o in objects:
            if o.type == "MESH" and len(o.data.polygons) > 20:
                mod = o.modifiers.new("ExportLOD", "DECIMATE")
                mod.ratio = ratio
                modifiers.append((o, mod))
        bpy.ops.export_scene.gltf(filepath=str(OUT / (root.name + f"_LOD{level}.glb")), export_format="GLB", use_selection=True, export_animations=False, export_apply=True, export_extras=True)
        for o, mod in modifiers: o.modifiers.remove(mod)


chassis = empty("KitChassis")
shell = import_part("chassis", (1.12, 1.18, .24))
shell.parent = chassis
shell.location.z = .29
empty("WeaponMount_A", chassis, (0, -.88, .38))
for side, x in [("L", -.66), ("R", .66)]:
    for end, y in [("F", -.4), ("R", .4)]:
        empty("WheelMount_" + end + side, chassis, (x, y, .22))
    empty("ArmorMount_" + side, chassis, (x * .89, 0, .36))
export_module(chassis)

wheel = empty("KitWheel")
cylinder("Tire", .22, .14, rubber, wheel, vertices=48)
cylinder("Hub", .13, .155, steel, wheel, vertices=32)
for i in range(24):
    a = i * math.tau / 24
    cylinder("Tread_%02d" % i, .022, .145, rubber, wheel, (0, math.sin(a) * .21, math.cos(a) * .21), vertices=6)
# Preserve two shared materials while avoiding one renderer per tread block.
bpy.ops.object.select_all(action="DESELECT")
wheel_meshes = list(wheel.children)
for obj in wheel_meshes: obj.select_set(True)
bpy.context.view_layer.objects.active = wheel_meshes[0]
bpy.ops.object.convert(target="MESH")
bpy.ops.object.join()
bpy.context.object.name = "WheelVisual"
export_module(wheel)

weapons = []
for name, dims, orientation in [("vertical", (.13, .59, .59), "disc"), ("drum", (.78, .36, .36), "long"), ("horizontal", (.94, .19, .1), "long")]:
    root = empty("Weapon_" + name)
    rotor = empty("Rotor_" + name, root)
    rotor["spin_axis_unity"] = "X" if name != "horizontal" else "Y"
    visual = import_part(name, dims, orientation)
    visual.parent = rotor
    axis = "X" if name != "horizontal" else "Z"
    cylinder("Hub", .055, dims[0] + .03 if axis == "X" else .13, steel, rotor, axis=axis)
    # Non-rotating shaft and physical visual support stay outside Rotor.
    cylinder("FixedShaft", .025, .88 if axis == "X" else .22, steel, root, axis=axis)
    root["mount_standard"] = "WeaponMount_A_v1"
    export_module(root)
    weapons.append(root)

armors = []
for name, thickness in [("armor_light", .045), ("armor_heavy", .09)]:
    root = empty("Armor_" + name)
    visual = import_part(name, (.76, thickness, .23), "long")
    # Generated plate plane is X/Z; align long side to chassis fore-aft.
    visual.rotation_euler.z = math.pi / 2
    visual.parent = root
    root["mount_standard"] = "ArmorMount_Side_v1"
    export_module(root)
    armors.append(root)

# Editable master preserves separate modules at origin, not a destructive merged robot.
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "ModularKit_Master.blend"))
(OUT.parent / "budget.json").write_text(json.dumps(report, indent=2), encoding="utf-8")

# Presentation: instantiate three weapon variants without touching production meshes.
for index, weapon in enumerate(weapons):
    assembly = empty("Preview_" + weapon.name, pos=((index - 1) * 2.05, 0, 0))
    def duplicate_tree(src, parent, pos=(0, 0, 0)):
        clone = src.copy()
        bpy.context.collection.objects.link(clone)
        clone.parent = parent
        clone.location = pos
        for child in src.children:
            duplicate_tree(child, clone, child.location.copy())
        return clone
    duplicate_tree(chassis, assembly)
    for x in [-.66, .66]:
        for y in [-.4, .4]: duplicate_tree(wheel, assembly, (x, y, .22))
    duplicate_tree(weapon, assembly, (0, -.88, .38))
    for side in [-1, 1]: duplicate_tree(armors[index % 2], assembly, (side * .6, 0, .36))
for root in [chassis, wheel] + weapons + armors:
    root.hide_render = True
    for child in root.children_recursive: child.hide_render = True

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 32
scene.render.resolution_x = 1800
scene.render.resolution_y = 950
scene.render.resolution_percentage = 100
scene.world = bpy.data.worlds.new("Studio")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs[0].default_value = (.18, .21, .27, 1)
for pos, power, size in [((0, -3, 6), 1800, 6), ((0, 4, 5), 2200, 5), ((-5, -1, 3), 1000, 4)]:
    data = bpy.data.lights.new("Softbox", "AREA")
    data.energy = power
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new("Softbox", data)
    scene.collection.objects.link(obj)
    obj.location = pos
    obj.rotation_euler = (-obj.location).to_track_quat("-Z", "Y").to_euler()
bpy.ops.object.camera_add(location=(3, -6, 4.1))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, -.1, .2)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 7.3
scene.camera = camera
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(SOURCE / "kit_preview.png")
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "ModularKit_Preview.blend"))
bpy.ops.render.render(write_still=True)
print("KIT_REPORT", json.dumps(report))
