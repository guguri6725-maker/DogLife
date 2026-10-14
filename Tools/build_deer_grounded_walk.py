"""Bake a four-beat, planted-hoof walk on the existing FloralDeer bones."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector, Quaternion

project = Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(project/'ArtSources/FloralDeer/FloralDeer_Rigged.blend'))
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
scene = bpy.context.scene
rest = {b.name:b.matrix_local.to_quaternion() for b in rig.data.bones}
duration, stance, stride, lift = 1.2, .75, .28, .065
prefixes = ['HindL','FrontL','HindR','FrontR']

def reset():
    for b in rig.pose.bones:
        b.rotation_mode='QUATERNION'; b.rotation_quaternion=Quaternion()
        b.location=(0,0,0); b.scale=(1,1,1)

def pose(name,x=0,y=0,z=0):
    q=Quaternion((0,0,1),math.radians(z))@Quaternion((0,1,0),math.radians(y))@Quaternion((1,0,0),math.radians(x))
    rig.pose.bones[name].rotation_quaternion=rest[name].inverted()@q@rest[name]

def rotate_world(name,q):
    b=rig.pose.bones[name]
    parent_pose=b.parent.matrix.to_quaternion() if b.parent else Quaternion()
    parent_rest=rest[b.parent.name] if b.parent else Quaternion()
    b.rotation_quaternion=(parent_rest.inverted()@rest[name]).inverted()@parent_pose.inverted()@q
    bpy.context.view_layer.update()

def solve_leg(prefix,target,fold):
    upper,lower,cannon,hoof=[rig.pose.bones[prefix+n] for n in ['Upper','Lower','Cannon','Hoof']]
    a,b,c,d=[bone.matrix.translation.copy() for bone in [upper,lower,cannon,hoof]]
    l1,l2,l3=(b-a).length,(c-b).length,(d-c).length
    # Fold the lower leg during swing; keep the hoof level when it bears weight.
    # Let the cannon follow the body over the planted hoof instead of forcing a vertical shin.
    lean=math.atan2((a.y-target.y)*.5,l3)+fold
    ankle=target+Vector((0,math.sin(lean)*l3,math.cos(lean)*l3))
    direction=ankle-a; distance=min(direction.length,l1+l2-.00001);direction.normalize()
    pole=Vector((0,1 if prefix.startswith('Front') else -1,0))
    pole-=direction*pole.dot(direction);pole.normalize()
    along=(l1*l1-l2*l2+distance*distance)/(2*distance)
    joint=a+direction*along+pole*math.sqrt(max(0,l1*l1-along*along))
    rotate_world(upper.name,(b-a).rotation_difference(joint-a)@upper.matrix.to_quaternion())
    b=lower.matrix.translation.copy();c=cannon.matrix.translation.copy()
    rotate_world(lower.name,(c-b).rotation_difference(ankle-b)@lower.matrix.to_quaternion())
    c=cannon.matrix.translation.copy();d=hoof.matrix.translation.copy()
    rotate_world(cannon.name,(d-c).rotation_difference(target-c)@cannon.matrix.to_quaternion())
    rotate_world(hoof.name,rest[hoof.name])

rig.animation_data.action=None;reset();bpy.context.view_layer.update()
feet={p:rig.pose.bones[p+'Hoof'].matrix.translation.copy() for p in prefixes}
action=bpy.data.actions.new('GroundedWalk');rig.animation_data.action=action
scene.render.fps=60; frames=round(duration*60); samples=[];max_error=0
scene.frame_start, scene.frame_end = 1, frames+1
for frame in range(frames+1):
    reset();u=frame/frames;cycle=u*math.tau
    # Preserve the skull's established forward-facing correction and restrained body sway.
    pose('Head',z=-80);pose('Spine',y=.45*math.sin(cycle),x=.4*math.sin(cycle*2))
    pose('Neck',y=-.45*math.sin(cycle),x=-.4*math.sin(cycle*2));pose('Tail',x=3*math.sin(cycle))
    rig.pose.bones['Root'].location=rest['Root'].inverted()@Vector((.002*math.sin(cycle),0,-.03+.002*math.sin(cycle*4)))
    bpy.context.view_layer.update();row={}
    for order,prefix in enumerate(prefixes):
        phase=(u-order*.25)%1
        target=feet[prefix].copy();swing=phase>=stance;fold=0
        if not swing:
            # Linear backward travel cancels the actor's forward movement in world space.
            target.y+=-stride/2+stride*phase/stance
        else:
            s=(phase-stance)/(1-stance)
            # Hermite travel matches contact velocity at lift-off and landing.
            m=stride*(1-stance)/stance
            target.y+=(2*s**3-3*s*s+1)*stride/2+(s**3-2*s*s+s)*m+(-2*s**3+3*s*s)*(-stride/2)+(s**3-s*s)*m
            target.z+=lift*math.sin(math.pi*s)**2
            fold=math.radians(22)*math.sin(math.pi*s)**2
        solve_leg(prefix,target,fold)
        actual=rig.pose.bones[prefix+'Hoof'].matrix.translation.copy()
        max_error=max(max_error,(actual-target).length)
        row[prefix]={'target':list(target),'actual':list(actual),'stance':not swing}
    samples.append(row)
    for bone in rig.pose.bones:
        bone.keyframe_insert('rotation_quaternion',frame=frame+1,group=bone.name)
        bone.keyframe_insert('location',frame=frame+1,group=bone.name)

assert max_error<.001, f'Foot target error: {max_error}'
out=project/'Temp/DeerWalkReview';out.mkdir(parents=True,exist_ok=True)
(out/'targets.json').write_text(json.dumps({'duration':duration,'stance':stance,'stride':stride,'speed':stride/(stance*duration),'max_error':max_error,'samples':samples},indent=2))
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(project/'ArtSources/FloralDeer/FloralDeer_GroundedWalk.blend'))
for obj in scene.objects:obj.select_set(obj.type in {'ARMATURE','MESH'})
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(project/'Assets/Art/Enemies/FloralDeer/GroundedWalk_Source.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
print('GROUND_WALK',json.dumps({'duration':duration,'max_error':max_error,'stride':stride,'lift':lift}))
