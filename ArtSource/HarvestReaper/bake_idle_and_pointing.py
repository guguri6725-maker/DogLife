"""Import the supplied idle and constrain both pointing arm and palm throughout each safe-sector gesture."""
import bpy,math
from mathutils import Vector,Matrix
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
exec(open(ROOT+'/ArtSource/HarvestReaper/retarget_extra_user_motions.py',encoding='utf-8').read().split("prefix='Summon';src=")[0])
prefix='Idle';src=bpy.data.objects['UserMotion_Idle'];scene.frame_set(1);grip_correction=delta(src,'RightHand').inverted()
sample(src);idle_start={p.name:(p.location.copy(),p.rotation_quaternion.copy(),p.scale.copy()) for p in rig.pose.bones}
action=action_begin('Idle')
for frame in range(1,62):
    t=(frame-1)/60;sf=1+55*t;scene.frame_set(int(sf),subframe=sf-int(sf));sample(src)
    # Close the breathing loop without a visible reset.
    u=ease((t-.8)/.2)
    for p in rig.pose.bones:
        loc,q,sc=idle_start[p.name];p.location=p.location.lerp(loc,u);p.rotation_quaternion=p.rotation_quaternion.slerp(q,u);p.scale=p.scale.lerp(sc,u)
    key_pose(frame)
action_end(action)
prefix='Wedge';src=bpy.data.objects['UserMotion_안전지대 회전'];scene.frame_set(23);grip_correction=delta(src,'RightHand').inverted();sample(src)
d=rig.pose.bones['LHand'].head-rig.pose.bones['LArm'].head;base_angle=-math.pi/2-math.atan2(d.y,d.x)
for sector in range(8):
    theta=sector*math.pi/4;direction=Vector((-math.sin(theta),-math.cos(theta),0))
    for phase,start,end in [('Ready',1,23),('Attack',23,45),('Hold',45,68)]:
        action=action_begin('Wedge'+phase+str(sector))
        for frame in range(1,62):
            t=(frame-1)/60;sf=start+(end-start)*t;scene.frame_set(int(sf),subframe=sf-int(sf));sample(src)
            rot('Root',z=base_angle-theta);bpy.context.view_layer.update()
            # Aim the entire arm and the actual hand forward axis, not only one endpoint frame.
            target=rig.pose.bones['LArm'].head+direction*1.2
            goal=limb('LArm','LForearm','LHand',target,(0,0,-1))
            # The model's fingers point mostly along -Y, unlike the imported bone's -X tail.
            finger_axis=Vector((-.1015728,-.9765251,.1899517)).normalized()
            q=finger_axis.rotation_difference(direction)@rest['LHand'].to_quaternion()
            rig.pose.bones['LHand'].matrix=Matrix.Translation(goal)@q.to_matrix().to_4x4()
            bpy.context.view_layer.update();key_pose(frame)
        action_end(action)
export('/Assets/Art/HarvestReaper/Animations/HarvestReaper_IdlePointing.fbx')
print('Baked new looped Idle and 24 continuously aimed sector clips.')
