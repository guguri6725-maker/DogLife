import math
import bpy
from mathutils import Vector

def capture_feet(rig):
    bpy.context.view_layer.update()
    return {s:(rig.pose.bones[s+'Foot'].matrix.translation.copy(),rig.pose.bones[s+'Foot'].matrix.to_quaternion().copy()) for s in ['Left','Right']}

def plant_feet(rig,world_rotation,targets):
    """Keep the planted feet stable while pelvis/spine drive attack weight shifts."""
    bpy.context.view_layer.update()
    for side,(target,rotation) in targets.items():
        upper=rig.pose.bones[side+'UpLeg'];lower=rig.pose.bones[side+'Leg'];foot=rig.pose.bones[side+'Foot']
        a=upper.matrix.translation.copy();b=lower.matrix.translation.copy();c=foot.matrix.translation.copy()
        l1=(b-a).length;l2=(c-b).length;direction=target-a
        distance=max(.001,min(direction.length,l1+l2-.0001));direction.normalize()
        pole=Vector((.12 if side=='Left' else -.12,-1,0));pole-=direction*pole.dot(direction);pole.normalize()
        along=(l1*l1-l2*l2+distance*distance)/(2*distance)
        knee=a+direction*along+pole*math.sqrt(max(0,l1*l1-along*along))
        world_rotation(upper.name,(b-a).rotation_difference(knee-a)@upper.matrix.to_quaternion())
        b=lower.matrix.translation.copy();c=foot.matrix.translation.copy()
        world_rotation(lower.name,(c-b).rotation_difference(target-b)@lower.matrix.to_quaternion())
        world_rotation(foot.name,rotation)
