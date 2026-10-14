import bpy, math, os, shutil
from mathutils import Euler, Vector

ROOT = 'C:/Users/mbc/Desktop/NC_AI/DogLife'
rig = bpy.data.objects['AlienMermaid_Rig']
obj = bpy.data.objects['AlienMermaid_Body']
backup = ROOT + '/ArtSource/AlienMermaid/TealMermaid_BeforeRefinement.blend'
if not os.path.exists(backup): shutil.copy2(bpy.data.filepath, backup)

def ease(x):
    x = max(0, min(1, x))
    return x*x*x*(x*(x*6-15)+10)

# 어깨와 팔의 연결부를 함께 낮춰 목 주변이 솟거나 팔이 분리되지 않게 합니다.
def relaxed_point(co):
    x,y,z = co
    influence = ease((abs(x)-.065)/.19) * math.exp(-((z-1.98)/.24)**4)
    return Vector((x-math.copysign(.038*influence,x), y, z-.043*influence))

if not obj.get('relaxed_shoulders_v1'):
    rig.animation_data.action = None
    for v in obj.data.vertices: v.co = relaxed_point(v.co)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for b in rig.data.edit_bones:
        b.head = relaxed_point(b.head); b.tail = relaxed_point(b.tail)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj['relaxed_shoulders_v1'] = True

def rotate(name,x=0,y=0,z=0):
    p=rig.pose.bones[name];q=p.bone.matrix_local.to_quaternion()
    p.rotation_mode='QUATERNION'
    p.rotation_quaternion=q.inverted() @ Euler((x,y,z),'XYZ').to_quaternion() @ q

def curve(t, keys):
    for i in range(1,len(keys)):
        if t <= keys[i][0]:
            a,v=keys[i-1];b,w=keys[i]
            return v+(w-v)*ease((t-a)/(b-a))
    return keys[-1][1]

# 준비→추진→후속 반동→안정 구간을 분리하고 말단 관절의 반응을 늦춥니다.
lengths={'Idle':2,'Walk':1.2,'Dive':1.9583333,'Burrow':1.4,'Emerge':1.9583333,'Recovery':1.9583333,'AttackReady':1.25,'Attack':1.25}
for tr in list(rig.animation_data.nla_tracks): rig.animation_data.nla_tracks.remove(tr)
rig.animation_data.action=None
for state,duration in lengths.items():
    name='AlienMermaid_'+state
    old=bpy.data.actions.get(name)
    if old: bpy.data.actions.remove(old)
    act=bpy.data.actions.new(name);act.use_fake_user=True;rig.animation_data.action=act
    count=round(duration*30)
    for f in range(count+1):
        t=f/count;p=math.tau*t
        loop=state in ('Idle','Walk','Burrow')
        envelope=1 if loop else math.sin(math.pi*t)**2
        amp=(1 if state in ('Walk','Burrow') else .42)*envelope
        fold=.45 if state=='Burrow' else 0
        stroke=0
        if state in ('Dive','AttackReady'):
            fold=curve(t,[(0,0),(.22,.08),(.52,.7),(.8,.62),(1,.45)])
        elif state in ('Emerge','Attack'):
            fold=curve(t,[(0,.45),(.16,.58),(.38,-.4),(.65,-.13),(1,0)])
            stroke=curve(t,[(0,0),(.18,.08),(.4,1),(.64,.5),(.84,.12),(1,0)])
        elif state=='Recovery':
            fold=-.07*envelope*(1-t)
        for pb in rig.pose.bones:
            pb.location=(0,0,0);pb.scale=(1,1,1);pb.rotation_mode='QUATERNION';pb.rotation_quaternion=(1,0,0,0)
        rotate('Hips',x=.042*math.sin(p)*amp-.17*fold,y=.015*math.sin(p)*amp)
        rig.pose.bones['Hips'].location.y=.016*math.sin(p)*amp
        rotate('Spine',x=-.11*fold+.023*math.sin(p-.35)*amp,z=.013*math.sin(p-.2)*amp)
        rotate('Chest',x=-.10*fold-.018*math.sin(p-.65)*amp,z=-.01*math.sin(p-.5)*amp)
        rotate('Neck',x=.12*fold-.014*math.sin(p-.6)*amp)
        rotate('Head',x=.10*fold-.018*math.sin(p)*amp,y=-.01*math.sin(p)*amp)
        for side,s in [('L',1),('R',-1)]:
            phase=p-(.12 if s>0 else -.12)
            rotate(side+'Clavicle',y=s*(.10+.012*math.sin(phase-.4)*amp))
            rotate(side+'UpperArm',x=.025*math.sin(phase-.7)*amp,y=s*(1.12-.24*stroke+.035*math.sin(phase-.65)*amp),z=s*(-.07-.22*fold+.46*stroke))
            rotate(side+'Forearm',y=s*(.13+.028*math.sin(phase-1)*amp),z=s*(-.18-.18*fold+.16*stroke))
            rotate(side+'Hand',y=-s*.10,z=s*(.025*math.sin(phase-1.4)*amp-.045*stroke))
            for j in range(3):
                rotate('Hair'+side+str(j),x=.045*math.sin(phase-.9-j*.45)*amp+.03*fold,y=s*.018*math.sin(phase-.7-j*.5)*amp)
            for j in range(2):
                rotate('Fin'+side+str(j),x=.10*math.sin(p-3.8-j*.55)*amp,y=s*.065*math.sin(p-3.6-j*.55)*amp)
        for j in range(8):
            rotate('Tail%02d'%j,x=.025+(.028+j*.009)*amp*math.sin(p-j*.46)+.065*fold*math.cos(j*.4),y=(.022+j*.006)*amp*math.sin(p-j*.5))
        for pb in rig.pose.bones:
            pb.keyframe_insert(data_path='rotation_quaternion',frame=f+1,group=pb.name)
            if pb.name=='Hips':pb.keyframe_insert(data_path='location',frame=f+1,group=pb.name)
    track=rig.animation_data.nla_tracks.new();track.name=act.name;track.strips.new(act.name,1,act);track.mute=True
rig.animation_data.action=bpy.data.actions['AlienMermaid_Idle']
bpy.context.scene.frame_set(1)
print('Relaxed shoulders and refined eight actions; original bone names preserved.')
