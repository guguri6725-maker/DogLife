"""Bake distinct grounded combat motions on the existing Harvest Reaper rig."""
import bpy, math, os, json
from mathutils import Vector, Matrix, Euler

ROOT = 'C:/Users/mbc/Desktop/NC_AI/DogLife'
rig = bpy.data.objects['HarvestReaper_Rig']
# FBX imported Hips as connected: release only that link so pelvis offsets evaluate.
bpy.context.view_layer.objects.active=rig
if bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.mode_set(mode='EDIT')
rig.data.edit_bones['Hips'].use_connect=False
bpy.ops.object.mode_set(mode='OBJECT')
scene = bpy.context.scene
rig.animation_data_clear()
rig.animation_data_create()
# Rerunning this baker replaces only its own generated actions.
for old in list(bpy.data.actions):
    if old.name.startswith('HarvestReaper_') and '|' not in old.name:
        bpy.data.actions.remove(old)
rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
feet = {s: rest[s+'Foot'].translation.copy() for s in ('L','R')}
hand_rest = {s: rest[s+'Hand'].translation.copy() for s in ('L','R')}
scene.render.fps = 60
scene.frame_start, scene.frame_end = 1, 61
report = {'max_foot_error': 0.0, 'max_grip_error': 0.0, 'clips': []}

def ease(t):
    t=max(0,min(1,t)); return t*t*(3-2*t)

def ramp(t,a,b): return ease((t-a)/(b-a))

def reset():
    for p in rig.pose.bones:
        p.rotation_mode='QUATERNION'; p.matrix_basis.identity()

def rot(name,x=0,y=0,z=0):
    q=rest[name].to_quaternion()
    rig.pose.bones[name].rotation_quaternion=q.inverted()@Euler((x,y,z)).to_quaternion()@q

def move_hips(x=0,y=0,z=0):
    p=rig.pose.bones['Hips']
    p.location=rest['Hips'].to_3x3().inverted()@Vector((x,y,z))

def limb(a,b,c,target,pole):
    # Use joint-to-joint lengths, because FBX leaf-bone tails are reconstructed.
    upper,lower,end=[rig.pose.bones[n] for n in (a,b,c)]
    start=upper.head.copy(); target=Vector(target)
    l1=(rest[b].translation-rest[a].translation).length
    l2=(rest[c].translation-rest[b].translation).length
    axis=(target-start).normalized()
    d=max(abs(l1-l2)+.0001,min((target-start).length,l1+l2-.0001))
    bend=Vector(pole); bend=(bend-axis*bend.dot(axis)).normalized()
    along=(l1*l1-l2*l2+d*d)/(2*d)
    knee=start+axis*along+bend*math.sqrt(max(0,l1*l1-along*along))
    goal=start+axis*d
    for p,s,e,nextname in ((upper,start,knee,b),(lower,knee,goal,c)):
        direction=rest[nextname].translation-rest[p.name].translation
        q=direction.rotation_difference(e-s)@rest[p.name].to_quaternion()
        p.matrix=Matrix.Translation(s)@q.to_matrix().to_4x4()
        bpy.context.view_layer.update()
    return goal

def hand(side,target,delta):
    goal=limb(side+'Arm',side+'Forearm',side+'Hand',target,(1 if side=='R' else -1,.35,0))
    rig.pose.bones[side+'Hand'].matrix=Matrix.Translation(goal)@(delta@rest[side+'Hand'].to_quaternion()).to_matrix().to_4x4()
    bpy.context.view_layer.update()
    return goal

def planted_legs(targets, turns):
    for side in ('L','R'):
        goal=limb(side+'Thigh',side+'Shin',side+'Foot',targets[side],(0,-1,0))
        q=Euler((0,0,turns[side])).to_quaternion()@rest[side+'Foot'].to_quaternion()
        rig.pose.bones[side+'Foot'].matrix=Matrix.Translation(goal)@q.to_matrix().to_4x4()
        report['max_foot_error']=max(report['max_foot_error'],(goal-targets[side]).length)
    bpy.context.view_layer.update()

def pose(kind,phase,t,direction=0):
    reset()
    # Shared progression keeps the end of each preparation continuous with its strike.
    loading=ease(t/.82) if phase=='Ready' else 1
    attack=ramp(t,.04,.66) if phase=='Attack' else 0
    recovery=ease(t) if phase=='Recover' else 0
    if phase=='Recover': attack=1; loading=1
    decay=1-recovery
    delta=Euler((0,0,0)).to_quaternion()
    grip=hand_rest['R'].copy()
    left=hand_rest['L'].copy()
    two_hands=False
    targets={s: feet[s].copy() for s in ('L','R')}
    turns={'L':0,'R':0}
    # A lifted arc makes intentional steps visible without dragging either foot.
    step=ramp(t,0,.65) if phase=='Ready' else (1-ramp(t,.25,.9) if phase=='Recover' else 1)
    arc=math.sin(math.pi*step)*.18
    if kind=='Sweep':
        stroke=ramp(t,.06,.64) if phase=='Attack' else (1 if phase=='Recover' else 0)
        angle=(-1.65*loading+3.3*stroke)*decay
        turn=(-.45*loading+.9*stroke)*decay
        rot('Hips',z=turn*.24); rot('Spine',z=turn*.55,x=.07*loading*decay)
        rot('Chest',z=turn*.45,x=.08*loading*decay); rot('Head',z=-turn*.6)
        move_hips(x=.18*math.sin(angle)*decay,z=-.34*loading*decay)
        targets['L']+=Vector((-.32*step,-.20*step,arc))
        turns['L']=-.22*step; turns['R']=.30*stroke*decay
        delta=Euler((math.pi*.5*loading*decay,0,angle)).to_quaternion()
        loaded=Vector((.2+1.0*math.sin(angle),-1.25-.15*math.cos(angle),3.8))
        grip=hand_rest['R'].lerp(loaded,loading*decay); two_hands=True
    elif kind=='Cleave':
        stroke=ramp(t,.04,.35) if phase=='Attack' else (1 if phase=='Recover' else 0)
        up=loading*(1-stroke)*decay; down=stroke*decay
        rot('Hips',x=.07*down); rot('Spine',x=-.12*up+.18*down)
        rot('Chest',x=-.16*up+.24*down); rot('Head',x=.08*up-.16*down)
        stride=ramp(t,.01,.27) if phase=='Attack' else (1-ramp(t,.25,.9) if phase=='Recover' else 0)
        targets['L']+=Vector((-.08*stride,-.58*stride,.23*math.sin(math.pi*stride)))
        move_hips(x=-.12*down,y=-.24*down,z=-.12*loading*decay-.28*down)
        delta=Euler((-.45*up+1.78*down,0,0)).to_quaternion()@Euler((0,0,-math.pi*.5*loading*decay)).to_quaternion()
        target=Vector((.1,-.7-1.15*stroke,4.9-2.0*stroke))
        grip=hand_rest['R'].lerp(target,loading*decay); two_hands=True
    elif kind=='Slam':
        strike=ramp(t,.03,.35) if phase=='Attack' else (1 if phase=='Recover' else 0)
        lift=loading*(1-strike)*decay
        rot('Spine',x=-.08*lift+.14*strike*decay)
        rot('Chest',x=-.07*lift+.12*strike*decay);rot('Head',x=-.12*strike*decay)
        targets['L']+=Vector((-.24*step,0,arc))
        turns['L']=-.15*step
        move_hips(y=-.10*strike*decay,z=-.13*loading*decay-.38*strike*decay)
        grip=hand_rest['R']+Vector((-.15*loading*decay,-.4*loading*decay,1.0*lift-.05*strike*decay))
        two_hands=True
    elif kind=='Summon':
        rot('Chest',z=-.10*loading*decay,x=-.06*loading*decay)
        rot('Head',z=-.15*loading*decay,x=-.08*loading*decay)
        move_hips(x=.20*loading*decay,z=-.24*loading*decay)
        targets['L']+=Vector((-.16*step,0,arc*.6))
        # Free hand gathers near the chest, then casts seeds outward at activation.
        cast=ease(t/.48) if phase=='Attack' else (1 if phase=='Recover' else 0)
        target=Vector((-1.25-1.0*cast,-1.0+.45*cast,4.5-.3*cast))
        left=hand_rest['L'].lerp(target,loading*decay)
    elif kind in ('Storm','Gather'):
        hold=phase=='Attack' or phase=='Hold'
        u=loading*decay
        rot('Spine',x=-.06*u);rot('Chest',x=-.08*u)
        rot('Head',x=-.12*u,z=.035*math.sin(t*math.tau)*u if hold else 0)
        move_hips(x=.08*u,z=-.32*u)
        targets['L']+=Vector((-.26*step,-.12*step,arc))
        turns['L']=-.18*step
        spread=1 if hold or phase=='Recover' else 0
        left=hand_rest['L'].lerp(Vector((-2.0 if spread else -1.0,-.45,5.0 if spread else 5.65)),u)
        # The rigid weapon stays planted; off-hand and torso carry the storm silhouette.
        grip=hand_rest['R'].copy()
    elif kind=='Wedge':
        angle=direction*math.pi/4
        # Turn upper torso toward rear sectors without twisting the feet or full body.
        signed=(angle+math.pi)%(2*math.pi)-math.pi
        turn=max(-.7,min(.7,-signed*.5))
        rot('Spine',z=turn*.45);rot('Chest',z=turn*.55)
        rot('Head',z=turn*.4)
        pulse=math.sin(math.pi*t) if phase=='Attack' else 0
        move_hips(x=.12*math.cos(angle),z=-.26-.12*pulse)
        targets['L']+=Vector((-.26,-.12,0))
        turns['L']=-.18
        drop=.5*ramp(t,0,.16) if phase=='Attack' else (.5*(1-ease(t)) if phase=='Hold' else 0)
        target=Vector((-math.sin(angle)*2.0,-math.cos(angle)*2.0,4.6-drop))
        left=target
    bpy.context.view_layer.update()
    if kind=='Wedge':
        # Aim from the shoulder, not the body centre, so every safe direction is exact.
        angle=direction*math.pi/4
        left=rig.pose.bones['LArm'].head+Vector((-math.sin(angle)*1.1,-math.cos(angle)*1.1,-drop))
    planted_legs(targets,turns)
    motion=report.setdefault('lower_body',{}).setdefault(kind,{'foot_travel':0,'hip_drop':0})
    motion['foot_travel']=max(motion['foot_travel'],max((targets[s]-feet[s]).length for s in ('L','R')))
    motion['hip_drop']=max(motion['hip_drop'],rest['Hips'].translation.z-rig.pose.bones['Hips'].head.z)
    right=hand('R',grip,delta)
    if two_hands:
        shaft=delta@Vector((-.09,0,.335)).normalized()
        left=hand_rest['L'].lerp(right+shaft*.5,loading*decay)
    hand('L',left,delta if two_hands else Euler((-.15,0,.12*loading*decay)).to_quaternion())
    rig.pose.bones['Scythe'].matrix_basis.identity()
    bpy.context.view_layer.update()
    report['max_grip_error']=max(report['max_grip_error'],(rig.pose.bones['Scythe'].head-rig.pose.bones['RHand'].head).length)

spec=[]
for kind,prefix in [('Sweep','Sweep'),('Cleave','Cleave'),('Slam','Slam'),('Summon','Summon'),('Storm','Special'),('Gather','Gather')]:
    for phase,suffix in [('Ready','Ready'),('Attack','Attack'),('Recover','Recovery')]:spec.append((prefix+suffix,kind,phase,0))
spec.append(('SpecialHold','Storm','Hold',0))
for direction in range(8):
    for phase in ('Ready','Attack','Hold'):spec.append(('Wedge'+phase+str(direction),'Wedge',phase,direction))

for name,kind,phase,direction in spec:
    action=bpy.data.actions.new('HarvestReaper_'+name)
    rig.animation_data.action=action; action.use_fake_user=True
    for frame in range(1,62):
        scene.frame_set(frame);pose(kind,phase,(frame-1)/60,direction)
        for p in rig.pose.bones:
            for prop in ('location','rotation_quaternion','scale'):p.keyframe_insert(data_path=prop,frame=frame,group=p.name)
    track=rig.animation_data.nla_tracks.new();track.name=name
    track.strips.new(action.name,1,action);track.mute=True
    report['clips'].append(name)
rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=False
reset()
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
out=ROOT+'/Assets/Art/HarvestReaper/Animations/HarvestReaper_PatternMotion.fbx'
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
for track in rig.animation_data.nla_tracks:track.mute=True
os.makedirs(ROOT+'/Documentation/HarvestReaper',exist_ok=True)
with open(ROOT+'/Documentation/HarvestReaper/motion_bake_report.json','w') as f:json.dump(report,f,indent=2)
print(json.dumps(report))
