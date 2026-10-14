"""Refine existing dryad elbow pivots and skin weights without rebuilding the mesh."""
import bpy, json
from pathlib import Path
from mathutils import Vector, Quaternion

project = Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(project/'ArtSources/BlossomSpirit/BlossomSpirit_Rigged.blend'))
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
mesh = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
rig.animation_data.action = None
for bone in rig.pose.bones:
    bone.rotation_mode = 'QUATERNION'
    bone.rotation_quaternion = Quaternion()
    bone.location = (0, 0, 0)
    bone.scale = (1, 1, 1)

# The old elbow pivots sat outside the sculpted arms, especially the bent right arm.
# Translate head and tail together to retain the authored rotation axes and clips.
pivots = {'LeftForeArm': (.135, .032, .604), 'RightForeArm': (-.103, -.006, .599)}
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name, position in pivots.items():
    bone = rig.data.edit_bones[name]
    delta = Vector(position) - bone.head
    bone.head += delta
    bone.tail += delta
bpy.ops.object.mode_set(mode='OBJECT')

def smooth(value):
    value = max(0, min(1, value))
    return value * value * (3 - 2 * value)

changed = {}
for side in ('Left', 'Right'):
    upper, lower = [mesh.vertex_groups[side+n] for n in ('Arm', 'ForeArm')]
    shoulder, elbow, wrist = [rig.data.bones[side+n].head_local.copy() for n in ('Arm', 'ForeArm', 'Hand')]
    tangent = ((elbow-shoulder).normalized() + (wrist-elbow).normalized()).normalized()
    count = 0
    for vertex in mesh.data.vertices:
        weights = {g.group: g.weight for g in vertex.groups}
        total = weights.get(upper.index, 0) + weights.get(lower.index, 0)
        if total < .001:
            continue
        # A continuous cross-section blend avoids the former abrupt surface-distance seam.
        forearm = smooth(.5 + (vertex.co-elbow).dot(tangent)/.065)
        upper.add([vertex.index], total*(1-forearm), 'REPLACE')
        lower.add([vertex.index], total*forearm, 'REPLACE')
        count += 1
    changed[side] = count

# UV duplicates must carry identical weights so their triangles cannot pull apart.
weld = {}
for vertex in mesh.data.vertices:
    weld.setdefault(tuple(round(c, 5) for c in vertex.co), []).append(vertex.index)
for ids in weld.values():
    if len(ids) < 2:
        continue
    average = {}
    for i in ids:
        for g in mesh.data.vertices[i].groups:
            average[g.group] = average.get(g.group, 0) + g.weight/len(ids)
    if not any(mesh.vertex_groups[g].name.endswith(('Arm','Hand')) and w>.01 for g,w in average.items()):
        continue
    for group in mesh.vertex_groups:
        group.remove(ids)
    strongest = sorted(average.items(), key=lambda item: item[1], reverse=True)[:4]
    total = sum(w for _,w in strongest)
    for g, weight in strongest:
        mesh.vertex_groups[g].add(ids, weight/total, 'REPLACE')

out = project/'Temp/ElbowReview'
out.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(project/'ArtSources/BlossomSpirit/BlossomSpirit_ElbowRefined.blend'))
for obj in bpy.context.scene.objects:
    obj.select_set(obj.type in {'ARMATURE', 'MESH'})
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(out/'BlossomSpirit.fbx'), use_selection=True,
    object_types={'ARMATURE','MESH'}, add_leaf_bones=False, bake_anim=True,
    bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0, axis_forward='-Z', axis_up='Y', path_mode='STRIP')
print('ELBOW_REFINEMENT', json.dumps({'vertices':len(mesh.data.vertices),'affected':changed,'pivots':pivots}))
