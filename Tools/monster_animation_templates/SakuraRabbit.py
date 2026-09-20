rest={b.name:b.matrix_local.to_quaternion() for b in arm.bones}
def pose(name,x=0,y=0,z=0):
    # Larger readable poses, preserving the fixed forward-facing correction.
    x*=1.22; y*=1.18
    if abs(z)<30:z*=1.18
    q=Quaternion((0,0,1),math.radians(z))@Quaternion((0,1,0),math.radians(y))@Quaternion((1,0,0),math.radians(x))
    rig.pose.bones[name].rotation_quaternion=rest[name].inverted()@q@rest[name]
def reset():
    for b in rig.pose.bones:b.rotation_mode='QUATERNION';b.rotation_quaternion=Quaternion();b.location=(0,0,0);b.scale=(1,1,1)
def ease(t):t=clamp(t);return t*t*(3-2*t)
def squat(amount):
    rig.pose.bones['Root'].scale=(1+.04*amount,1-.22*amount,1+.04*amount)
    pose('Head',x=9*amount)
    for side,s in [('L',1),('R',-1)]:pose('Ear'+side,x=-18*amount,y=s*4*amount)
bpy.context.scene.render.fps=60
for name,duration in [('Idle',2.4),('Move',.6),('Crouch',.7),('Jump',.72),('Land',.3),('Retreat',.6)]:
    rig.animation_data_create();action=bpy.data.actions.new(name);rig.animation_data.action=action
    for frame in range(round(duration*60)+1):
        reset();t=frame/60;u=min(1,t/duration);phase=u*math.tau
        if name=='Idle':
            pose('Head',x=.8*math.sin(phase));pose('EarL',x=2*math.sin(phase));pose('EarR',x=-2*math.sin(phase));pose('Tail',z=3*math.sin(phase))
        elif name=='Retreat':
            hop=math.sin(math.pi*u)**2
            rig.pose.bones['Root'].location=rest['Root'].inverted()@Vector((0,0,.085*hop))
            squat(.35*(1-hop));pose('Head',x=-6*hop)
            pose('Body',x=-10*hop);pose('Tail',x=18*hop)
            for side in ['L','R']:
                pose('Front'+side+'Upper',x=24*hop);pose('Front'+side+'Foot',x=-18*hop)
                pose('Hind'+side+'Upper',x=-27*hop);pose('Hind'+side+'Foot',x=20*hop);pose('Ear'+side,x=-15*hop)
        elif name=='Move':
            hop=.025*(1-math.cos(phase));rig.pose.bones['Root'].location=rest['Root'].inverted()@Vector((0,0,hop))
            pose('Body',x=9*math.sin(phase));pose('Head',x=-6*math.sin(phase-.3));pose('Tail',x=15*math.sin(phase-.5))
            for side in ['L','R']:
                pose('Front'+side+'Upper',x=23*math.sin(phase));pose('Front'+side+'Foot',x=-16*math.sin(phase))
                pose('Hind'+side+'Upper',x=-26*math.sin(phase));pose('Hind'+side+'Foot',x=18*math.sin(phase));pose('Ear'+side,x=-14*math.sin(phase-.3))
        elif name=='Crouch':
            load=ease(u/.65);squat(load);pose('Body',x=-7*load);pose('Tail',x=-12*load)
            for side in ['L','R']:
                pose('Front'+side+'Upper',x=15*load);pose('Front'+side+'Foot',x=-12*load)
                pose('Hind'+side+'Upper',x=-22*load);pose('Hind'+side+'Foot',x=18*load)
        elif name=='Jump':
            lift=math.sin(math.pi*u);squat((1-ease(u/.2))*.9)
            pose('Head',x=8*(1-lift)-5*lift)
            pose('Body',x=-12*math.sin(math.tau*u));pose('Tail',x=22*lift)
            for side in ['L','R']:
                pose('Front'+side+'Upper',x=-24*lift);pose('Front'+side+'Foot',x=18*lift)
                pose('Hind'+side+'Upper',x=24*lift);pose('Hind'+side+'Foot',x=-18*lift);pose('Ear'+side,x=-28*lift)
        elif name=='Land':
            squash=ease(u/.22) if u<.22 else 1-ease((u-.22)/.78)
            squat(squash*1.15)
            pose('Body',x=12*squash);pose('Head',x=-5*squash);pose('Tail',x=-18*squash)
            for side in ['L','R']:
                pose('Front'+side+'Upper',x=18*squash);pose('Hind'+side+'Upper',x=-20*squash)
        for b in rig.pose.bones:
            b.keyframe_insert('rotation_quaternion',frame=frame+1,group=b.name);b.keyframe_insert('location',frame=frame+1,group=b.name);b.keyframe_insert('scale',frame=frame+1,group=b.name)
    action.use_fake_user=True
    for curve in action.fcurves:
        for key in curve.keyframe_points:key.interpolation='LINEAR'
rig.animation_data.action=None;reset()
