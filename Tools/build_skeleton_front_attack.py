"""Bake the sword lift in front of the face, preserving the existing mesh and rig."""
import bpy, math, sys
from pathlib import Path
from mathutils import Vector, Quaternion

project = Path(r'C:\Users\mbc\Desktop\NC_AI\DogLife')
sys.path.insert(0, str(project/'Tools'))
from monster_pose_utils import capture_feet, plant_feet
scene = bpy.context.scene
rig = next(o for o in scene.objects if o.type == 'ARMATURE')
mesh = next(o for o in scene.objects if o.type == 'MESH')
rig.animation_data_create()
rig.animation_data.action = None
# Reuse the existing rig-space rotation and two-bone arm solver.
helpers = (project/'Tools/monster_animation_templates/BlossomSkeleton.py').read_text(encoding='utf-8').split('import sys')[0]
helpers = helpers.replace('y=18','y=0').replace('y=-15','y=0').replace('z=-35','z=0')
exec(helpers)
reset()
feet = capture_feet(rig)
hands = {s:rig.pose.bones[s+'Hand'].matrix.translation.copy() for s in ['Left','Right']}
wrist = rig.data.bones['RightHand'].head_local.copy()
sword = [v.co for v in mesh.data.vertices if v.co.x < -.24 and v.co.y < -.12 and .15 < v.co.z < .40]
tip = sum(sorted(sword,key=lambda v:(v-wrist).length,reverse=True)[:8],Vector())/8
axis = (tip-wrist).normalized()
action = bpy.data.actions.new('FrontOverheadAttack')
rig.animation_data.action = action
scene.render.fps = 60
scene.frame_start, scene.frame_end = 1, 100
ready = Vector((-.075,-.20,.64))
high = Vector((-.06,-.13,.91))
low = Vector((-.055,-.255,.48))
def smooth(a,b,t):
    return a.lerp(b,ease(max(0,min(1,t))))
def blade_rotation(angle):
    # Rotate only through the frontal sagittal arc; no 285-degree backswing.
    direction = Vector((0,-math.cos(angle),math.sin(angle)))
    return axis.rotation_difference(direction) @ rest['RightHand'], direction
for frame in range(100):
    t = frame/60
    reset()
    gather = ease(min(1,t/.25))
    lift = ease(max(0,min(1,(t-.25)/.40)))
    strike = ease(max(0,min(1,(t-.95)/.30)))
    release = ease(max(0,min(1,(t-1.28)/.37)))
    weight = 1-release
    pose('Hips',x=5*strike*weight)
    pose('Spine',x=4*strike*weight)
    pose('Spine1',x=5*strike*weight)
    pose('Head',x=-3*lift*weight)
    shift('Root',(0,-.015*strike*weight,-.008*strike*weight))
    plant_feet(rig,world_rotation,feet)
    # Hands pass chest -> forehead -> forward/down, always in front of the torso.
    target = hands['Right'].lerp(ready,gather).lerp(high,lift).lerp(low,strike).lerp(hands['Right'],release)
    angle = math.radians(50+28*lift-140*strike)
    desired, direction = blade_rotation(angle)
    desired = rest['RightHand'].slerp(desired,gather*weight)
    reach('Right',target,Vector((-.36,-.22,.67)))
    world_rotation('RightHand',desired)
    actual = rig.pose.bones['RightHand'].matrix.translation.copy()
    # The support hand follows the same handle; keep its own original grip basis.
    left_target = hands['Left'].lerp(actual-direction*.045,gather*weight)
    reach('Left',left_target,Vector((.32,-.20,.66)))
    left_rotation = axis.rotation_difference(direction) @ rest['LeftHand']
    world_rotation('LeftHand',rest['LeftHand'].slerp(left_rotation,gather*weight))
    key(frame+1)
action.use_fake_user = True
scene.frame_set(40)
bpy.data.libraries.write(str(project/'ArtSources/BlossomSkeleton/BlossomSkeleton_FrontAttack.blend'),{scene},fake_user=True)
for o in scene.objects: o.select_set(o.type in {'ARMATURE','MESH'})
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(project/'Assets/Art/Enemies/BlossomSkeleton/FrontAttack_Source.fbx'),
    use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,
    bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
    axis_forward='-Z',axis_up='Y',path_mode='STRIP')
print('Front attack baked: 1.65 seconds; lift ends .65, strike .95-1.25, recover 1.28-1.65')
