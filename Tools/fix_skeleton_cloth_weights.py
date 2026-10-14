"""Remove limb influence from the pink cloth using this mesh's actual albedo."""
import bpy, numpy as np
from pathlib import Path
project = Path(r'C:\Users\mbc\Desktop\NC_AI\DogLife')
scene = bpy.context.scene
mesh = next(o for o in scene.objects if o.type == 'MESH')
rig = next(o for o in scene.objects if o.type == 'ARMATURE')
# Always start from the authored weights; rerunning the correction is deterministic.
with bpy.data.libraries.load(str(project/'ArtSources/BlossomSkeleton/BlossomSkeleton_Rigged.blend'),link=False) as (a,b): b.objects=['texture_mapping']
original=b.objects[0]
for group in mesh.vertex_groups: group.remove(list(range(len(mesh.data.vertices))))
for v in original.data.vertices:
    for g in v.groups: mesh.vertex_groups[original.vertex_groups[g.group].name].add([v.index],g.weight,'REPLACE')
bpy.data.objects.remove(original,do_unlink=True)
shader = next(n for n in mesh.data.materials[0].node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
image = shader.inputs['Base Color'].links[0].from_node.image
w,h = image.size
pixels = np.empty(w*h*4,dtype=np.float32)
image.pixels.foreach_get(pixels)
pixels = pixels.reshape(h,w,4)
cloth = set()
for loop in mesh.data.loops:
    uv = mesh.data.uv_layers.active.data[loop.index].uv
    r,g,b = pixels[min(h-1,max(0,int(uv.y*h))),min(w-1,max(0,int(uv.x*w))),:3]
    v = mesh.data.vertices[loop.vertex_index]
    # Exclude the blossom crown, boots and weapon. Include dark pink cape folds.
    if .45 < v.co.z < .79 and v.co.x > .13 and v.co.y > .045 and r > g*1.13 and b > g*1.04 and r > .16:
        cloth.add(v.index)
# Include dark hems and UV-seam duplicates so a few missed vertices cannot stretch a cloth face.
neighbours = [set() for v in mesh.data.vertices]
weld = {}
for v in mesh.data.vertices: weld.setdefault(tuple(round(c,5) for c in v.co),[]).append(v.index)
for edge in mesh.data.edges:
    a,b = edge.vertices
    neighbours[a].add(b); neighbours[b].add(a)
for ids in weld.values():
    for i in ids: neighbours[i].update(ids)
for _ in range(4):
    cloth |= {j for i in cloth for j in neighbours[i] if .43 < mesh.data.vertices[j].co.z < .79 and mesh.data.vertices[j].co.y > .02 and mesh.data.vertices[j].co.x > .23}
# The cape's rear silhouette includes pale edges that are not pink in the albedo.
cloth |= {v.index for v in mesh.data.vertices if .43 < v.co.z < .79 and ((v.co.x>.23 and v.co.y>.015) or (v.co.x>.12 and v.co.y>.075))}
arm_names = {'LeftArm','LeftForeArm','LeftHand','RightArm','RightForeArm','RightHand','LeftShoulder','RightShoulder'}
affected = sum(any(mesh.vertex_groups[g.group].name in arm_names and g.weight>.01 for g in mesh.data.vertices[i].groups) for i in cloth)
for i in cloth:
    z = mesh.data.vertices[i].co.z
    for group in mesh.vertex_groups: group.remove([i])
    # Cape hangs from chest; the lower tabard follows the pelvis, not the arms.
    if z < .49:
        mesh.vertex_groups['Hips'].add([i],1,'REPLACE')
    else:
        mesh.vertex_groups['Spine2'].add([i],1,'REPLACE')
print('Cloth vertices',len(cloth),'with incorrect arm influence',affected,'albedo',image.name)
bpy.data.libraries.write(str(project/'ArtSources/BlossomSkeleton/BlossomSkeleton_ClothWeights.blend'),{scene},fake_user=True)
for o in scene.objects: o.select_set(o.type in {'ARMATURE','MESH'})
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(project/'Assets/Art/Enemies/BlossomSkeleton/BlossomSkeleton_ClothWeights.fbx'),
    use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,
    axis_forward='-Z',axis_up='Y',path_mode='STRIP')
