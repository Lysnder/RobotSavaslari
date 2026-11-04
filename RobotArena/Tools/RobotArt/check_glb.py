"""Dependency-free runtime GLB integrity and size-budget check."""
import json
import math
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[2]


def load(path):
    raw = path.read_bytes()
    magic, version, total = struct.unpack_from('<III', raw)
    assert (magic, version, total) == (0x46546C67, 2, len(raw)), path
    length, kind = struct.unpack_from('<II', raw, 12)
    assert kind == 0x4E4F534A
    doc = json.loads(raw[20:20 + length])
    size, kind = struct.unpack_from('<II', raw, 20 + length)
    assert kind == 0x004E4942
    binary = raw[28 + length:]
    assert size == len(binary)
    assert doc['buffers'][0]['byteLength'] <= size
    return doc, binary


def check(path):
    doc, binary = load(path)
    for view in doc['bufferViews']:
        assert view.get('byteOffset', 0) % 4 == 0
        assert view.get('byteOffset', 0) + view['byteLength'] <= len(binary)
    widths = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}
    sizes = {5121: 1, 5123: 2, 5125: 4, 5126: 4}
    for accessor in doc['accessors']:
        view = doc['bufferViews'][accessor['bufferView']]
        width = widths[accessor['type']] * sizes[accessor['componentType']]
        end = accessor.get('byteOffset', 0) + max(0, accessor['count'] - 1) * view.get('byteStride', width) + width
        assert end <= view['byteLength'], (path, accessor)
        if accessor['componentType'] == 5126:
            start = view.get('byteOffset', 0) + accessor.get('byteOffset', 0)
            stride = view.get('byteStride', width)
            for i in range(accessor['count']):
                values = struct.unpack_from('<' + 'f' * widths[accessor['type']], binary, start + i * stride)
                assert all(math.isfinite(v) for v in values), path
                if 'min' in accessor and 'max' in accessor:
                    assert all(lo - 1e-5 <= v <= hi + 1e-5 for lo, v, hi in zip(accessor['min'], values, accessor['max']))
    triangles = 0
    for mesh in doc['meshes']:
        for primitive in mesh['primitives']:
            indices = doc['accessors'][primitive['indices']]
            vertices = doc['accessors'][primitive['attributes']['POSITION']]['count']
            assert indices['count'] % 3 == 0
            triangles += indices['count'] // 3
            view = doc['bufferViews'][indices['bufferView']]
            offset = view.get('byteOffset', 0) + indices.get('byteOffset', 0)
            code = {5121: 'B', 5123: 'H', 5125: 'I'}[indices['componentType']]
            end = offset + indices['count'] * sizes[indices['componentType']]
            assert all(i[0] < vertices for i in struct.iter_unpack('<' + code, binary[offset:end]))
            assert all(doc['accessors'][i]['count'] == vertices for i in primitive['attributes'].values())
            if 'NORMAL' in primitive['attributes']:
                normal = doc['accessors'][primitive['attributes']['NORMAL']]
                view = doc['bufferViews'][normal['bufferView']]
                start = view.get('byteOffset', 0) + normal.get('byteOffset', 0)
                stride = view.get('byteStride', 12)
                for i in range(normal['count']):
                    xyz = struct.unpack_from('<fff', binary, start + i * stride)
                    assert abs(sum(v * v for v in xyz) - 1) < 0.02, (path, 'non-unit normal')
    for image in doc.get('images', []):
        view = doc['bufferViews'][image['bufferView']]
        offset = view.get('byteOffset', 0)
        if binary[offset:offset + 8] == b'\x89PNG\r\n\x1a\n':
            width, height = struct.unpack_from('>II', binary, offset + 16)
            assert max(width, height) <= 2048, (path, width, height)
    original = ROOT / 'ArtSource/RuntimeOriginals' / path.relative_to(ROOT / 'Assets/Models')
    if original.exists():
        before, _ = load(original)
        for key in ('nodes', 'scenes', 'scene', 'materials', 'textures', 'samplers', 'animations', 'skins'):
            assert before.get(key) == doc.get(key), (path, key)
        assert len(before['meshes']) == len(doc['meshes'])
        assert len(before['accessors']) == len(doc['accessors'])
    assert path.stat().st_size <= 45_000_000, (path, '45 MB per-file budget exceeded')
    assert triangles <= 150_000, (path, '150k triangle budget exceeded')
    print(f'PASS {path.name}: {path.stat().st_size / 1e6:.2f} MB, {triangles:,} triangles')


if __name__ == '__main__':
    paths = sorted((ROOT / 'Assets/Models').rglob('*.glb'))
    for path in paths:
        check(path)
    total = sum(p.stat().st_size for p in paths)
    assert total <= 160_000_000, '160 MB total runtime model budget exceeded'
    print(f'PASS total: {total / 1e6:.2f} MB')
