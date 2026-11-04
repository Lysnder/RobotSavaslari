"""Run with Blender --background --python ... -- [--apply].

Reduce static GLB geometry and cap embedded textures at 2048px. Original
node/material/accessor identities are preserved for Unity subasset references.
First-run originals are kept in ignored ArtSource/RuntimeOriginals.
"""
import json
from pathlib import Path
import shutil
import struct
import sys
import tempfile

import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
BACKUP = ROOT / 'ArtSource/RuntimeOriginals'


def normalize_normals(values):
    lengths = np.linalg.norm(values, axis=1)
    valid = lengths > 1e-10
    values[valid] /= lengths[valid, None]
    values[~valid] = (0, 1, 0)
    return values


def repair_normals(path):
    raw = bytearray(path.read_bytes())
    length = struct.unpack_from('<I', raw, 12)[0]
    doc = json.loads(raw[20:20 + length])
    seen = set()
    for mesh in doc['meshes']:
        for primitive in mesh['primitives']:
            index = primitive['attributes'].get('NORMAL')
            if index is None or index in seen:
                continue
            seen.add(index)
            a = doc['accessors'][index]
            v = doc['bufferViews'][a['bufferView']]
            values = np.ndarray((a['count'], 3), dtype='<f4', buffer=raw,
                                offset=28 + length + v.get('byteOffset', 0) + a.get('byteOffset', 0),
                                strides=(v.get('byteStride', 12), 4))
            values[:] = normalize_normals(values.copy())
    path.write_bytes(raw)


def optimize(path, apply=False):
    backup = BACKUP / path.relative_to(ROOT / 'Assets/Models')
    raw = (backup if backup.exists() else path).read_bytes()
    length, kind = struct.unpack_from('<II', raw, 12)
    assert kind == 0x4E4F534A
    doc = json.loads(raw[20:20 + length])
    assert not doc.get('skins') and not doc.get('animations'), 'Only static GLBs are supported'
    start = 20 + length
    size, kind = struct.unpack_from('<II', raw, start)
    assert kind == 0x004E4942
    binary = raw[start + 8:start + 8 + size]
    views = doc['bufferViews']
    chunks = [binary[v.get('byteOffset', 0):v.get('byteOffset', 0) + v['byteLength']] for v in views]

    def read(index):
        a = doc['accessors'][index]
        v = views[a['bufferView']]
        assert 'sparse' not in a
        types = {5126: '<f4', 5125: '<u4', 5123: '<u2', 5121: 'u1'}
        width = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
        dtype = np.dtype(types[a['componentType']])
        return np.ndarray((a['count'], width), dtype=dtype,
                          buffer=chunks[a['bufferView']], offset=a.get('byteOffset', 0),
                          strides=(v.get('byteStride', width * dtype.itemsize), dtype.itemsize)).copy()

    def replace(index, data, component_type, bounds=False):
        a = doc['accessors'][index]
        a['bufferView'] = len(chunks)
        a['byteOffset'] = 0
        a['componentType'] = component_type
        a['count'] = len(data)
        a.pop('min', None)
        a.pop('max', None)
        if bounds:
            a['min'] = data.min(axis=0).tolist()
            a['max'] = data.max(axis=0).tolist()
        chunks.append(data.tobytes())
        views.append({'buffer': 0, 'byteLength': data.nbytes})

    before_triangles = after_triangles = 0
    for mesh_doc in doc['meshes']:
        for prim in mesh_doc['primitives']:
            assert prim.get('mode', 4) == 4 and 'targets' not in prim
            attrs = prim['attributes']
            assert set(attrs) <= {'POSITION', 'NORMAL', 'TEXCOORD_0'}
            points = read(attrs['POSITION'])
            indices = read(prim['indices']).reshape(-1, 3)
            before_triangles += len(indices)
            target = 120000 if 'Robots' in str(path) else 20000
            if len(indices) <= target:
                after_triangles += len(indices)
                continue
            mesh = bpy.data.meshes.new('OptimizationWorkingMesh')
            mesh.from_pydata(points.tolist(), [], indices.tolist())
            if 'NORMAL' in attrs:
                mesh.polygons.foreach_set('use_smooth', [True] * len(mesh.polygons))
            if 'TEXCOORD_0' in attrs:
                uv = read(attrs['TEXCOORD_0'])
                layer = mesh.uv_layers.new(name='UVMap')
                vertex_indices = np.empty(len(mesh.loops), dtype=np.int32)
                mesh.loops.foreach_get('vertex_index', vertex_indices)
                layer.data.foreach_set('uv', uv[vertex_indices].reshape(-1))
            obj = bpy.data.objects.new('OptimizationWorkingObject', mesh)
            bpy.context.collection.objects.link(obj)
            modifier = obj.modifiers.new('RuntimeBudget', 'DECIMATE')
            modifier.ratio = target / len(indices)
            modifier.use_collapse_triangulate = True
            evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            result = evaluated.to_mesh()
            result.calc_loop_triangles()
            # A vertex per corner retains UV seams and computed corner normals.
            loops = [i for tri in result.loop_triangles for i in tri.loops]
            positions = np.array([result.vertices[result.loops[i].vertex_index].co[:] for i in loops], dtype='<f4')
            assert np.isfinite(positions).all()
            replace(attrs['POSITION'], positions, 5126, True)
            if 'NORMAL' in attrs:
                normals = np.array([result.corner_normals[i].vector[:] for i in loops], dtype='<f4')
                normals = normalize_normals(normals)
                replace(attrs['NORMAL'], normals, 5126)
            if 'TEXCOORD_0' in attrs:
                uvs = np.array([result.uv_layers.active.data[i].uv[:] for i in loops], dtype='<f4')
                replace(attrs['TEXCOORD_0'], uvs, 5126)
            replace(prim['indices'], np.arange(len(loops), dtype='<u4').reshape(-1, 1), 5125)
            after_triangles += len(loops) // 3
            evaluated.to_mesh_clear()
            bpy.data.objects.remove(obj, do_unlink=True)
            bpy.data.meshes.remove(mesh)

    with tempfile.TemporaryDirectory() as temporary:
        for i, image_doc in enumerate(doc.get('images', [])):
            view = image_doc['bufferView']
            source = Path(temporary) / f'{i}.png'
            source.write_bytes(chunks[view])
            image = bpy.data.images.load(str(source), check_existing=False)
            width, height = image.size
            if max(width, height) > 2048:
                ratio = 2048 / max(width, height)
                image.scale(max(1, round(width * ratio)), max(1, round(height * ratio)))
                image.filepath_raw = str(source)
                image.file_format = 'PNG'
                image.save()
                chunks[view] = source.read_bytes()
                image_doc['mimeType'] = 'image/png'
            bpy.data.images.remove(image)

    used = {a['bufferView'] for a in doc['accessors'] if 'bufferView' in a}
    used.update(i['bufferView'] for i in doc.get('images', []))
    mapping = {}
    new_views = []
    output = bytearray()
    for old in sorted(used):
        output.extend(b'\0' * (-len(output) % 4))
        mapping[old] = len(new_views)
        view = dict(views[old], byteOffset=len(output), byteLength=len(chunks[old]))
        new_views.append(view)
        output.extend(chunks[old])
    for a in doc['accessors']:
        if 'bufferView' in a:
            a['bufferView'] = mapping[a['bufferView']]
    for image_doc in doc.get('images', []):
        image_doc['bufferView'] = mapping[image_doc['bufferView']]
    doc['bufferViews'] = new_views
    doc['buffers'][0]['byteLength'] = len(output)
    encoded = json.dumps(doc, separators=(',', ':')).encode()
    encoded += b' ' * (-len(encoded) % 4)
    output.extend(b'\0' * (-len(output) % 4))
    final = (struct.pack('<III', 0x46546C67, 2, 28 + len(encoded) + len(output))
             + struct.pack('<II', len(encoded), 0x4E4F534A) + encoded
             + struct.pack('<II', len(output), 0x004E4942) + output)
    print(f'{path.name}: {len(raw)/1e6:.1f} -> {len(final)/1e6:.1f} MB; triangles {before_triangles:,} -> {after_triangles:,}', flush=True)
    if apply and len(final) < len(raw):
        backup.parent.mkdir(parents=True, exist_ok=True)
        if not backup.exists():
            shutil.copy2(path, backup)
        path.write_bytes(final)


if __name__ == '__main__':
    for path in sorted((ROOT / 'Assets/Models').rglob('*.glb')):
        if 'Articulated' not in str(path):
            if '--normalize-only' in sys.argv:
                repair_normals(path)
                continue
            if '--robots-only' in sys.argv and 'Robots' not in str(path):
                continue
            optimize(path, '--apply' in sys.argv)
