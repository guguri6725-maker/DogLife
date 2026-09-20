scene=bpy.context.scene;scene.render.fps=60
rest={b.name:b.matrix_local.to_quaternion() for b in rig.data.bones}
def pose(name,x=0,y=0,z=0):
    # Larger readable poses, preserving the fixed forward-facing correction.
    x*=1.22; y*=1.18
    if abs(z)<30:z*=1.18
    q=Quaternion(Vector((0,0,1)),math.radians(z)) @ Quaternion(Vector((0,1,0)),math.radians(y)) @ Quaternion(Vector((1,0,0)),math.radians(x))
    rig.pose.bones[name].rotation_quaternion=rest[name].inverted() @ q @ rest[name]
def reset():
    for b in rig.pose.bones:b.rotation_mode='QUATERNION';b.rotation_quaternion=Quaternion();b.location=(0,0,0);b.scale=(1,1,1)
    pose('LeftUpLeg',y=18);pose('RightUpLeg',y=-15)
    # 원본 조형의 머리는 몸 정면보다 약 35도 돌아가 있습니다.
    pose('Head',z=-35)
def shift(name,v):rig.pose.bones[name].location=rest[name].inverted() @ Vector(v)
def ease(t):return t*t*(3-2*t)
def world_rotation(name,q):
    b=rig.pose.bones[name]
    parent_pose=b.parent.matrix.to_quaternion() if b.parent else Quaternion()
    parent_rest=rest[b.parent.name] if b.parent else Quaternion()
    local_rest=parent_rest.inverted() @ rest[name]
    b.rotation_quaternion=local_rest.inverted() @ parent_pose.inverted() @ q
    bpy.context.view_layer.update()
def reach(side,target,pole):
    bpy.context.view_layer.update()
    upper=rig.pose.bones[side+'Arm'];lower=rig.pose.bones[side+'ForeArm'];hand=rig.pose.bones[side+'Hand']
    origin=upper.matrix.translation.copy();elbow=lower.matrix.translation.copy();wrist=hand.matrix.translation.copy()
    a=(elbow-origin).length;b=(wrist-elbow).length
    direction=target-origin;distance=min(direction.length,a+b-.0001);direction.normalize()
    bend=pole-origin; bend-=direction*bend.dot(direction);bend.normalize()
    along=(a*a-b*b+distance*distance)/(2*distance)
    desired_elbow=origin+direction*along+bend*math.sqrt(max(0,a*a-along*along))
    world_rotation(upper.name,(elbow-origin).rotation_difference(desired_elbow-origin) @ upper.matrix.to_quaternion())
    elbow=lower.matrix.translation.copy();wrist=hand.matrix.translation.copy()
    world_rotation(lower.name,(wrist-elbow).rotation_difference(target-elbow) @ lower.matrix.to_quaternion())
def key(frame):
    for b in rig.pose.bones:
        b.keyframe_insert('rotation_quaternion',frame=frame,group=b.name);b.keyframe_insert('location',frame=frame,group=b.name)
import sys
sys.path.insert(0,str(pathlib.Path('Tools').resolve()))
from monster_pose_utils import capture_feet, plant_feet
reset();planted_feet=capture_feet(rig)
for name,duration in [('Idle',2.4),('Walk',1.0),('Attack',1.65)]:
    rig.animation_data_create();action=bpy.data.actions.new(name);rig.animation_data.action=action
    frames=round(duration*60)
    for f in range(frames+1):
        reset();t=f/60;cycle=2*math.pi*t/duration
        if name=='Idle':
            pose('Spine1',x=1.5*math.sin(cycle));pose('Head',x=-1.5*math.sin(cycle),z=-35+1.5*math.sin(cycle+.3))
            pose('LeftArm',x=2*math.sin(cycle));pose('RightArm',x=1.2*math.sin(cycle))
        elif name=='Walk':
            for side,phase,adduct in [('Left',0,18),('Right',math.pi,-15)]:
                s=math.sin(cycle+phase);knee=24*max(0,-s)
                pose(side+'UpLeg',x=24*s,y=adduct);pose(side+'Leg',x=knee)
                pose(side+'Foot',x=-18*s-knee*.7)
                pose(side+'Arm',x=-s*(13 if side=='Left' else 7))
                pose(side+'ForeArm',x=-5)
            pose('Hips',y=4*math.sin(cycle),z=7*math.sin(cycle))
            pose('Spine',x=4,y=-3*math.sin(cycle),z=-3*math.sin(cycle))
            pose('Spine1',x=4,z=-7*math.sin(cycle));pose('Spine2',x=-2,z=-2*math.sin(cycle))
            pose('LeftShoulder',x=-5*math.sin(cycle));pose('RightShoulder',x=5*math.sin(cycle))
            pose('Head',x=-5+2*math.sin(cycle*2),z=-35+2*math.sin(cycle))
            shift('Root',(.008*math.sin(cycle),0,.012*(1-math.cos(cycle*2))))
        else:
            # 양손 잡기/들기 0–0.55, 머리 뒤에서 유지 0.55–0.95, 내려찍기 0.95–1.25.
            lift=ease(min(1,t/.55));strike=ease(max(0,min(1,(t-.95)/.30)))
            release=ease(max(0,min(1,(t-1.28)/.37)))
            weight=1-release
            pose('Hips',x=(-7+17*strike)*lift*weight,z=(-6+12*strike)*lift*weight)
            pose('Spine',x=(-4+12*strike)*lift*weight,z=(4-8*strike)*lift*weight)
            pose('Spine2',x=(-3+8*strike)*lift*weight)
            for side,adduct in [('Left',18),('Right',-15)]:
                bend=(14*lift+22*strike)*weight
                pose(side+'UpLeg',x=-bend*.55,y=adduct);pose(side+'Leg',x=bend);pose(side+'Foot',x=-bend*.45)
            pose('Spine1',x=(-10+30*strike)*lift*weight)
            pose('Head',x=(8-24*strike)*lift*weight,z=-35)
            shift('Root',(.012*lift*(1-strike)*weight,-.045*strike*weight,-(.022*lift+.038*strike)*weight))
            plant_feet(rig,world_rotation,planted_feet)
            bpy.context.view_layer.update()
            original_right=rig.pose.bones['RightHand'].matrix.translation.copy()
            original_left=rig.pose.bones['LeftHand'].matrix.translation.copy()
            # 손은 정수리 위를 통과하는 곡선, 칼끝은 뒤→위→앞→바닥의 긴 호를 따라갑니다.
            back=Vector((-.025,.12,1.055));overhead=Vector((-.025,-.02,1.24))
            down=Vector((-.025,-.22,sword_length*.996+.02))
            arc=(1-strike)**2*back+2*(1-strike)*strike*overhead+strike**2*down
            grip=original_right.lerp(arc,lift).lerp(original_right,release)
            reach('Right',grip,Vector((-.38,.02,.82)))
            sword_rest=sword_axis
            angle=math.radians(-20+285*strike)
            arc_blade=Vector((0,math.cos(angle),math.sin(angle)))
            blade=sword_rest.lerp(arc_blade,lift).normalized().lerp(sword_rest,release).normalized()
            hand_rotation=sword_rest.rotation_difference(blade) @ rest['RightHand']
            world_rotation('RightHand',hand_rotation)
            # 손잡이의 칼날 반대쪽을 왼손으로 잡으며 두 손의 간격을 유지합니다.
            actual=rig.pose.bones['RightHand'].matrix.translation.copy()
            left_target=actual-blade*.055
            blend=ease(min(1,t/.25))*weight
            reach('Left',original_left.lerp(left_target,blend),Vector((.38,.02,.82)))
            left_rotation=sword_rest.rotation_difference(blade) @ rest['LeftHand']
            world_rotation('LeftHand',rig.pose.bones['LeftHand'].matrix.to_quaternion().slerp(left_rotation,blend))
            for bone_name in ['RightArm','RightForeArm','RightHand','LeftArm','LeftForeArm','LeftHand']:
                b=rig.pose.bones[bone_name]
                b.rotation_quaternion=Quaternion().slerp(b.rotation_quaternion,blend)
        key(f+1)
    action.use_fake_user=True
    for curve in action.fcurves:
        for k in curve.keyframe_points:k.interpolation='LINEAR'
rig.animation_data.action=None;reset()
