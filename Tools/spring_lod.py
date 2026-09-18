"""Run with Blender in background after SpringLodBuilder.Export()."""
import bpy, bmesh, json, pathlib

work = pathlib.Path('Library/SpringLODWorking')
for source in sorted(work.glob('*.json')):
    if '_lod' in source.stem:
        continue
    data = json.loads(source.read_text())
    vertices = [(v['x'], v['y'], v['z']) for v in data['vertices']]
    faces, materials = [], []
    for slot, part in enumerate(data['parts']):
        indices = part['triangles']
        faces.extend(tuple(indices[i:i+3]) for i in range(0, len(indices), 3))
        materials.extend([slot] * (len(indices)//3))
    base = bpy.data.meshes.new('source')
    base.from_pydata(vertices, [], faces)
    for slot in data['parts']:
        base.materials.append(bpy.data.materials.new('slot'))
    uv = base.uv_layers.new()
    for poly, material, face in zip(base.polygons, materials, faces):
        poly.material_index = material
        ns = [data['normals'][v] for v in face]
        poly.use_smooth = any(sum((ns[0][axis] - n[axis])**2 for axis in ('x','y','z')) > 1e-6 for n in ns[1:])
    for loop in base.loops:
        v = data['uv'][loop.vertex_index]
        uv.data[loop.index].uv = (v['x'], v['y'])
    # Weld duplicated face vertices before collapse, retaining UVs on face corners.
    bm = bmesh.new(); bm.from_mesh(base)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.000001)
    bm.to_mesh(base); bm.free(); base.update()
    for level, ratio in [(1, .5), (2, .2)]:
        obj = bpy.data.objects.new('lod', base.copy())
        bpy.context.collection.objects.link(obj)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        mod = obj.modifiers.new('Reduce', 'DECIMATE')
        mod.ratio = ratio; mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mesh = obj.data; mesh.calc_loop_triangles()
        result = {'vertices': [], 'normals': [], 'uv': [], 'parts': [{'triangles': []} for _ in data['parts']]}
        # Separate corners preserve material/UV seams and evaluated split normals.
        normals = mesh.corner_normals
        for tri in mesh.loop_triangles:
            for index in tri.loops:
                loop = mesh.loops[index]; v = mesh.vertices[loop.vertex_index].co
                n = normals[index].vector; tex = mesh.uv_layers.active.data[index].uv
                out_index = len(result['vertices'])
                result['vertices'].append(dict(zip(('x','y','z'), v)))
                result['normals'].append(dict(zip(('x','y','z'), n)))
                result['uv'].append(dict(zip(('x','y'), tex)))
                result['parts'][tri.material_index]['triangles'].append(out_index)
        (work / (source.stem + '_lod' + str(level) + '.json')).write_text(json.dumps(result, separators=(',',':')))
        reduced = obj.data; bpy.data.objects.remove(obj, do_unlink=True); bpy.data.meshes.remove(reduced)
    bpy.data.meshes.remove(base)
    print('LOD completed: ' + source.stem, flush=True)
print('SPRING_LOD_DONE', flush=True)
