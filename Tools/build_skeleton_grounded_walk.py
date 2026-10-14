"""Bake a planted-foot walk on the existing BlossomSkeleton rig in Blender."""
import bpy, math, sys
from pathlib import Path
from mathutils import Vector, Quaternion

project = Path(r'C:\Users\mbc\Desktop\NC_AI\DogLife')
sys.path.insert(0, str(project / 'Tools'))
from monster_pose_utils import capture_feet, plant_feet
scene = bpy.context.scene
rig = next(o for o in scene.objects if o.type == 'ARMATURE')
rest = {b.name: b.matrix_local.to_quaternion() for b in rig.data.bones}
def reset():
    for b in rig.pose.bones:
        b.rotation_mode = 'QUATERNION'
        b.rotation_quaternion = Quaternion()
        b.location = (0, 0, 0)
        b.scale = (1, 1, 1)
def pose(name, x=0, y=0, z=0):
    q = (Quaternion((0,0,1), math.radians(z)) @ Quaternion((0,1,0), math.radians(y))
         @ Quaternion((1,0,0), math.radians(x)))
    rig.pose.bones[name].rotation_quaternion = rest[name].inverted() @ q @ rest[name]
def world_rotation(name, q):
    b = rig.pose.bones[name]
    parent_pose = b.parent.matrix.to_quaternion() if b.parent else Quaternion()
    parent_rest = rest[b.parent.name] if b.parent else Quaternion()
    b.rotation_quaternion = (parent_rest.inverted() @ rest[name]).inverted() @ parent_pose.inverted() @ q
    bpy.context.view_layer.update()

reset()
feet = capture_feet(rig)
rig.animation_data_create()
rig.animation_data.action = bpy.data.actions.new('GroundedWalk')
scene.render.fps = 60
scene.frame_start, scene.frame_end = 1, 61
max_error = 0
for f in range(61):
    reset()
    cycle = 2 * math.pi * f / 60
    # Low pelvis, small weight transfer; feet solve independently of torso bob.
    rig.pose.bones['Root'].location = rest['Root'].inverted() @ Vector((.004*math.sin(cycle), 0, -.012+.002*math.cos(2*cycle)))
    pose('Hips', y=1.5*math.sin(cycle), z=3*math.sin(cycle))
    pose('Spine', x=2, z=-1.5*math.sin(cycle))
    pose('Spine1', x=1, z=-1.5*math.sin(cycle))
    pose('Head', x=-3)
    targets = {}
    for side, offset in [('Left',0), ('Right',.5)]:
        phase = (f/60 + offset) % 1
        target, rotation = feet[side]
        target = target.copy()
        # 60% stance gives double support; the swing foot clears the floor gently.
        if phase < .6:
            target.y += -.08 + .16 * phase/.6
        else:
            u = (phase-.6)/.4
            smooth = u*u*(3-2*u)
            target.y += .08-.16*smooth
            target.z += .035*math.sin(math.pi*u)**2
        targets[side] = (target, rotation)
        pose(side+'Arm', x=(15 if side=='Left' else 9)*math.sin(2*math.pi*phase))
        pose(side+'ForeArm', x=-4)
    plant_feet(rig, world_rotation, targets)
    for side, (target, _) in targets.items():
        max_error = max(max_error, (rig.pose.bones[side+'Foot'].matrix.translation-target).length)
    for b in rig.pose.bones:
        b.keyframe_insert('rotation_quaternion', frame=f+1, group=b.name)
        b.keyframe_insert('location', frame=f+1, group=b.name)
scene.frame_set(1)
print('Maximum foot target error:', max_error)
bpy.data.libraries.write(str(project/'ArtSources/BlossomSkeleton/BlossomSkeleton_GroundedWalk.blend'), {scene}, fake_user=True)
for o in scene.objects: o.select_set(o.type in {'ARMATURE','MESH'})
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(project/'Assets/Art/Enemies/BlossomSkeleton/GroundedWalk_Source.fbx'), use_selection=True,
    object_types={'ARMATURE','MESH'}, add_leaf_bones=False, bake_anim=True,
    bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
    axis_forward='-Z', axis_up='Y', path_mode='STRIP')
