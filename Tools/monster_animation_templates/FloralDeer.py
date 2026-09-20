rest={b.name:b.matrix_local.to_quaternion() for b in arm.bones}
def pose(name,x=0,y=0,z=0):
    # Larger readable poses, preserving the fixed forward-facing correction.
    x*=1.22; y*=1.18
    if abs(z)<30:z*=1.18
    q=Quaternion((0,0,1),math.radians(z))@Quaternion((0,1,0),math.radians(y))@Quaternion((1,0,0),math.radians(x))
    rig.pose.bones[name].rotation_quaternion=rest[name].inverted()@q@rest[name]
def reset():
    for b in rig.pose.bones:b.rotation_mode='QUATERNION';b.rotation_quaternion=Quaternion();b.location=(0,0,0)
    # Sculpted skull looks sideways; align the skull and antlers with the body.
    pose('Head',z=-80)
def ease(t):t=max(0,min(1,t));return t*t*(3-2*t)
def gait(phase,amount):
    for side in ['L','R']:
        for front in [True,False]:
            p=('Front' if front else 'Hind')+side;offset=0 if (side=='L')==front else math.pi
            s=math.sin(phase+offset);pose(p+'Upper',x=amount*s);pose(p+'Lower',x=-amount*.65*max(0,s));pose(p+'Cannon',x=-amount*.4*s);pose(p+'Hoof',x=amount*.15*s)
bpy.context.scene.render.fps=60
durations={'Idle':2.4,'Walk':1.0,'Windup':1.35,'Charge':.5,'Impact':.28,'Recover':1.0}
for name,duration in durations.items():
    rig.animation_data_create();action=bpy.data.actions.new(name);rig.animation_data.action=action
    for frame in range(round(duration*60)+1):
        reset();t=frame/60;phase=t/duration*math.tau
        if name=='Idle':pose('Neck',x=1.2*math.sin(phase));pose('Tail',x=4*math.sin(phase))
        elif name=='Walk':
            gait(phase,21);pose('Spine',x=3*math.sin(phase*2),y=3*math.sin(phase),z=3*math.sin(phase))
            pose('Neck',x=-3*math.sin(phase*2),z=-2*math.sin(phase));pose('Head',x=2*math.sin(phase*2),z=-80)
            pose('Tail',x=6*math.sin(phase),z=8*math.sin(phase-.5))
            rig.pose.bones['Root'].location=rest['Root'].inverted()@Vector((.006*math.sin(phase),0,.008*(1-math.cos(phase*2))))
        elif name=='Windup':
            # 두 번 발을 들고 뒤로 긁은 뒤, 마지막 0.3초는 뿔을 낮추고 버팁니다.
            scrape=math.sin(math.pi*((t% .45)/.45)) if t<.9 else 0
            pose('FrontRUpper',x=-38*scrape);pose('FrontRLower',x=52*scrape);pose('FrontRCannon',x=-20*scrape)
            rig.pose.bones['FrontRUpper'].location=rest['FrontRUpper'].inverted()@Vector((0,-.025*scrape,.035*scrape))
            brace=ease((t-.85)/.35)
            pose('Spine',x=-5*scrape+5*brace,y=4*scrape);pose('Tail',x=12*brace,z=8*scrape)
            for side in ['L','R']:
                pose('Hind'+side+'Upper',x=-10*brace);pose('Hind'+side+'Lower',x=14*brace);pose('Hind'+side+'Cannon',x=-4*brace)
            rig.pose.bones['Root'].location=rest['Root'].inverted()@Vector((-.008*scrape,.014*brace,-.012*brace))
            pose('Neck',x=48*ease((t-.85)/.35));pose('Head',x=10*ease((t-.85)/.35),z=-80)
        elif name=='Charge':
            gait(phase,36);pose('Spine',x=7*math.sin(phase),y=3*math.sin(phase));pose('Neck',x=48-4*math.sin(phase));pose('Head',x=10+2*math.sin(phase),z=-80)
            pose('Tail',x=18+8*math.sin(phase-.6))
            rig.pose.bones['Root'].location=rest['Root'].inverted()@Vector((0,0,.018*(1-math.cos(phase*2))))
        elif name=='Impact':
            ram=math.sin(math.pi*t/duration)
            pose('Neck',x=48+14*ram);pose('Head',x=10-12*ram,z=-80);pose('Spine',x=7*ram)
            gait(phase,12)
            pose('Tail',x=20*ram);rig.pose.bones['Root'].location=rest['Root'].inverted()@Vector((0,-.025*ram,-.012*ram))
        elif name=='Recover':
            settle=math.sin(math.pi*min(1,t/.6))
            pose('Neck',x=48*(1-ease(t/.85)));pose('Head',x=10*(1-ease(t/.85)),z=-80)
            pose('Spine',x=-5*settle);pose('Tail',x=12*settle)
            for side in ['L','R']:
                pose('Front'+side+'Upper',x=-10*settle);pose('Front'+side+'Lower',x=16*settle)
        for b in rig.pose.bones:b.keyframe_insert('rotation_quaternion',frame=frame+1,group=b.name);b.keyframe_insert('location',frame=frame+1,group=b.name)
    action.use_fake_user=True
    for curve in action.fcurves:
        for key in curve.keyframe_points:key.interpolation='LINEAR'
rig.animation_data.action=None;reset()
