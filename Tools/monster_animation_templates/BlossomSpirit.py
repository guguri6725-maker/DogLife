rest={b.name:b.matrix_local.to_quaternion() for b in rig.data.bones}
def pose(name,x=0,y=0,z=0):
    q=Quaternion((0,0,1),math.radians(z))@Quaternion((0,1,0),math.radians(y))@Quaternion((1,0,0),math.radians(x))
    rig.pose.bones[name].rotation_quaternion=rest[name].inverted()@q@rest[name]
def reset():
    for b in rig.pose.bones:b.rotation_mode='QUATERNION';b.rotation_quaternion=Quaternion();b.location=(0,0,0);b.scale=(1,1,1)
def ease(t):t=max(0,min(1,t));return t*t*(3-2*t)
def worldrot(name,q):
    b=rig.pose.bones[name];p=b.parent.matrix.to_quaternion() if b.parent else Quaternion();pr=rest[b.parent.name] if b.parent else Quaternion()
    b.rotation_quaternion=(pr.inverted()@rest[name]).inverted()@p.inverted()@q;bpy.context.view_layer.update()
def reach(side,target):
    bpy.context.view_layer.update();u=rig.pose.bones[side+'Arm'];l=rig.pose.bones[side+'ForeArm'];h=rig.pose.bones[side+'Hand']
    a=u.matrix.translation.copy();b=l.matrix.translation.copy();c=h.matrix.translation.copy();l1=(b-a).length;l2=(c-b).length
    d=target-a;length=min(d.length,l1+l2-.001);d.normalize();bend=Vector((1 if side=='Left' else -1,.1,-.3));bend-=d*bend.dot(d);bend.normalize()
    along=(l1*l1-l2*l2+length*length)/(2*length);elbow=a+d*along+bend*math.sqrt(max(0,l1*l1-along*along))
    worldrot(u.name,(b-a).rotation_difference(elbow-a)@u.matrix.to_quaternion());b=l.matrix.translation.copy();c=h.matrix.translation.copy()
    worldrot(l.name,(c-b).rotation_difference(target-b)@l.matrix.to_quaternion())

# Floating dryad: no alternating leg gait, pelvis yaw or planted-foot IK.
# Root offsets are visual only; the NavMeshAgent and damage collider stay grounded.
rig.animation_data_clear();reset();bpy.context.scene.render.fps=60
for name,duration in [('Idle',2.4),('Walk',2.4),('Attack',1.8),('Dash',.38)]:
    rig.animation_data_create();action=bpy.data.actions.new(name);rig.animation_data.action=action
    for frame in range(round(duration*60)+1):
        reset();t=frame/60;u=t/duration;cycle=u*math.tau
        wave=math.sin(cycle);breathe=.5-.5*math.cos(cycle)
        height=.045+.014*breathe
        # Keep the sculpted legs aligned with the body, with only a small ankle point.
        pose('LeftFoot',x=5);pose('RightFoot',x=5)
        targets={}
        if name in ('Idle','Walk'):
            moving=name=='Walk'
            pose('Spine',x=(7 if moving else 2)+2*wave)
            pose('Spine1',x=-3 if moving else -1)
            pose('Head',x=-4 if moving else -2)
            # Both arms open and close together, tracing wide, slow arcs around the petals.
            width=(.23 if moving else .20)+.025*wave
            for side,sign in [('Left',1),('Right',-1)]:
                targets[side]=Vector((sign*width,-.105-.035*breathe,.57+.045*breathe))
            if moving:height+=.007*breathe
        elif name=='Dash':
            recoil=math.sin(math.pi*u)
            pose('Spine',x=-14*recoil);pose('Spine1',x=-6*recoil);pose('Head',x=10*recoil)
            height=.045+.045*recoil
            for side,sign in [('Left',1),('Right',-1)]:
                targets[side]=Vector((sign*(.20+.045*recoil),-.105+.07*recoil,.60+.08*recoil))
        else:
            gather=ease(t/.55);release=ease((t-1.1)/.16);recover=ease((t-1.4)/.4)
            weight=gather*(1-recover)
            pose('Spine',x=(-10+23*release)*weight)
            pose('Spine1',x=(-5+10*release)*weight)
            pose('Head',x=(7-15*release)*weight)
            height=.045+.03*weight
            for side,sign in [('Left',1),('Right',-1)]:
                opened=Vector((sign*.20,-.105,.60))
                casting=Vector((sign*(.065+.035*release),-.18-.14*release,.69+.035*release))
                targets[side]=opened.lerp(casting,weight)
        for side,target in targets.items():reach(side,target)
        rig.pose.bones['Root'].location=rest['Root'].inverted()@Vector((0,0,height))
        for bone in rig.pose.bones:
            bone.keyframe_insert('rotation_quaternion',frame=frame+1,group=bone.name)
            bone.keyframe_insert('location',frame=frame+1,group=bone.name)
    action.use_fake_user=True
    for curve in action.fcurves:
        for key in curve.keyframe_points:key.interpolation='LINEAR'
rig.animation_data.action=None;reset()
