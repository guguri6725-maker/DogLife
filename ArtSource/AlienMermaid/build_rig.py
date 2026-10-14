import bpy, math, os, json
import numpy as np
from mathutils import Vector, Euler

ROOT = 'C:/Users/mbc/Desktop/NC_AI/DogLife'
OUT = ROOT + '/Assets/Art/SummerMonsters/AlienMermaid'
os.makedirs(OUT, exist_ok=True)
obj = bpy.data.objects.get('AlienMermaid_Body') or bpy.data.objects['texture_mapping']
obj.name = 'AlienMermaid_Body'
H = 2.45
# 원본 메시/UV는 유지하고 기존 인어 높이에 맞춥니다.
hi = max(v.co.z for v in obj.data.vertices)
if abs(hi-H) > .001:
    for v in obj.data.vertices: v.co *= H/hi
for p in obj.data.polygons: p.use_smooth = True
spec = []
def bone(n,a,b,parent='Root'):
    spec.append((n, Vector(a)*H, Vector(b)*H, parent))
bone('Root',(0,0,0),(0,0,.1),None)
bone('Hips',(0,-.025,.59),(0,-.025,.66))
bone('Spine',(0,-.025,.66),(0,-.02,.735),'Hips')
bone('Chest',(0,-.02,.735),(0,-.02,.825),'Spine')
bone('Neck',(0,-.025,.825),(0,-.025,.875),'Chest')
bone('Head',(0,-.025,.875),(0,-.025,.97),'Neck')
for side,s in [('L',1),('R',-1)]:
    bone(side+'Clavicle',(0,-.02,.815),(s*.105,-.02,.815),'Chest')
    bone(side+'UpperArm',(s*.105,-.02,.815),(s*.245,-.025,.805),side+'Clavicle')
    bone(side+'Forearm',(s*.245,-.025,.805),(s*.355,-.035,.802),side+'UpperArm')
    bone(side+'Hand',(s*.355,-.035,.802),(s*.425,-.04,.788),side+'Forearm')
    # 긴 머리카락은 머리부터 내려오는 세 구간으로 따로 가중치를 줍니다.
    for j in range(3):
        bone('Hair'+side+str(j),(s*(.04+j*.025),.045+j*.015,.91-j*.09),
             (s*(.065+j*.025),.06+j*.015,.82-j*.09),'Head' if j==0 else 'Hair'+side+str(j-1))
for j in range(8):
    a=.59-j*.0425;b=.59-(j+1)*.0425
    bone('Tail%02d'%j,(0,-.027,a),(0,-.028,b),'Hips' if j==0 else 'Tail%02d'%(j-1))
for side,s in [('L',1),('R',-1)]:
    bone('Fin'+side+'0',(0,-.028,.25),(s*.095,-.028,.15),'Tail07')
    bone('Fin'+side+'1',(s*.095,-.028,.15),(s*.185,-.028,.04),'Fin'+side+'0')

old=bpy.data.objects.get('AlienMermaid_Rig')
if old: bpy.data.objects.remove(old,do_unlink=True)
data=bpy.data.armatures.new('AlienMermaid_Skeleton')
rig=bpy.data.objects.new('AlienMermaid_Rig',data);bpy.context.collection.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for n,a,b,p in spec:
    eb=data.edit_bones.new(n);eb.head=a;eb.tail=b
    if p: eb.parent=data.edit_bones[p]
    eb.use_deform=n!='Root'
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True
obj.vertex_groups.clear()
deform=[q for q in spec if q[0]!='Root'];names=[q[0] for q in deform]
groups=[obj.vertex_groups.new(name=n) for n in names]
weights=np.zeros((len(obj.data.vertices),len(names)),dtype=np.float64)
for v in obj.data.vertices:
    x,y,z=v.co/H;ax=abs(x);side='L' if x>=0 else 'R'
    if z<.265:
        candidates=[n for n in names if n.startswith('Fin'+side) or n in ('Tail06','Tail07')]
    elif z<.585:
        candidates=[n for n in names if n.startswith('Tail') or n=='Hips']
    elif z<.87 and ax>.14 and z>.745:
        candidates=[side+'UpperArm',side+'Forearm',side+'Hand',side+'Clavicle']
    elif z>.615 and z<.94 and (y>.035 or (ax>.083 and y>-.008 and z<.78)):
        candidates=['Head']+['Hair'+side+str(j) for j in range(3)]
    else:
        candidates=['Hips','Spine','Chest','Neck','Head']+[side+'Clavicle',side+'UpperArm'] if z>.77 else ['Hips','Spine','Chest','Tail00']
    dist=[]
    for k,(n,a,b,p) in enumerate(deform):
        if n not in candidates:continue
        ab=b-a;t=max(0,min(1,(v.co-a).dot(ab)/ab.length_squared))
        dist.append(((v.co-a-ab*t).length/H,k))
    dist.sort();near=dist[:3]
    # 관절 중간에서 가중치가 부드럽게 겹치도록 거리 기반으로 분배합니다.
    vals=np.exp(-np.array([d for d,k in near])*95);vals/=vals.sum()
    for (_,k),w in zip(near,vals):weights[v.index,k]=w
# 연결된 표면만 약하게 평활화하여 팔/머리카락이 몸통에 끌려가지 않게 합니다.
edges=np.array([e.vertices[:] for e in obj.data.edges]);a,b=edges[:,0],edges[:,1]
degree=np.bincount(np.concatenate([a,b]),minlength=len(weights)).clip(1)[:,None]
for _ in range(5):
    total=np.zeros_like(weights);np.add.at(total,a,weights[b]);np.add.at(total,b,weights[a])
    weights=.6*weights+.4*total/degree
for i in range(len(weights)):
    keep=np.argsort(weights[i])[-4:];vals=weights[i,keep];vals/=vals.sum()
    for k,w in zip(keep,vals):
        if w>.0001: groups[int(k)].add([i],float(w),'REPLACE')
for m in list(obj.modifiers):
    if m.type=='ARMATURE':obj.modifiers.remove(m)
mod=obj.modifiers.new('Mermaid Skin','ARMATURE');mod.object=rig;mod.use_deform_preserve_volume=False
obj.parent=rig
mat=obj.data.materials[0];mat.name='AlienMermaid_Material'
for n in mat.node_tree.nodes:
    if n.type=='TEX_IMAGE' and n.image:
        suffix='Normal' if 'normal' in n.image.name.lower() else 'BaseColor'
        n.image.filepath_raw=OUT+'/AlienMermaid_'+suffix+'.png';n.image.file_format='PNG';n.image.save()

rig.animation_data_create()
for tr in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(tr)
def rotate(name,x=0,y=0,z=0):
    p=rig.pose.bones[name];q=p.bone.matrix_local.to_quaternion()
    p.rotation_mode='QUATERNION';p.rotation_quaternion=q.inverted()@Euler((x,y,z),'XYZ').to_quaternion()@q
def smooth(t,keys):
    for j in range(1,len(keys)):
        if t<=keys[j][0]:
            a,v=keys[j-1];b,w=keys[j];u=(t-a)/(b-a);u=u*u*(3-2*u);return v+(w-v)*u
    return keys[-1][1]
fps=30;bpy.context.scene.render.fps=fps
lengths={'Idle':2,'Walk':1.2,'Dive':1.9583333,'Burrow':1.4,'Emerge':1.9583333,'Recovery':1.9583333,'AttackReady':1.25,'Attack':1.25}
for state,duration in lengths.items():
    act=bpy.data.actions.new('AlienMermaid_'+state);act.use_fake_user=True;rig.animation_data.action=act
    count=round(duration*fps)
    for f in range(count+1):
        t=f/count;phase=math.tau*t;swim=state in ('Walk','Burrow');amp=1.55 if swim else .55
        fold=0;stroke=0
        if state in ('Dive','AttackReady'):fold=smooth(t,[(0,0),(.35,.2),(.72,1),(1,.55)])
        elif state in ('Emerge','Attack'):fold=smooth(t,[(0,.55),(.25,-.65),(.55,-.3),(1,0)]);stroke=math.sin(t*math.pi)
        elif state=='Burrow':fold=.55
        elif state=='Recovery':fold=-.12*math.sin(t*math.pi)*(1-t)
        for pb in rig.pose.bones:pb.location=(0,0,0);pb.scale=(1,1,1);pb.rotation_mode='QUATERNION';pb.rotation_quaternion=(1,0,0,0)
        rotate('Hips',x=.07*math.sin(phase)*amp-.2*fold,y=.018*math.sin(phase))
        rig.pose.bones['Hips'].location.y=.025*math.sin(phase)*amp
        rotate('Spine',x=-.12*fold+.02*math.sin(phase-.3))
        rotate('Chest',x=-.13*fold-.025*math.sin(phase-.5))
        rotate('Neck',x=.11*fold);rotate('Head',x=.13*fold)
        for side,s in [('L',1),('R',-1)]:
            rotate(side+'Clavicle',y=s*.04*math.sin(phase-.3))
            rotate(side+'UpperArm',x=.07*math.sin(phase-.6),y=s*(1.03-.35*stroke+.09*math.sin(phase-.5)*amp),z=s*(-.1-.32*fold+.65*stroke))
            rotate(side+'Forearm',y=s*.16,z=s*(-.14-.25*fold+.2*stroke))
            rotate(side+'Hand',y=-s*.12,z=s*.04*math.sin(phase-1))
            for j in range(3):rotate('Hair'+side+str(j),x=.05*math.sin(phase-.7-j*.4)*amp+.035*fold,y=s*.025*math.sin(phase-j*.5))
            for j in range(2):rotate('Fin'+side+str(j),x=.12*math.sin(phase-4-j*.5)*amp,y=s*.08*math.sin(phase-3.6-j*.5))
        # 진행 파형: 골반에서 시작한 굽힘이 꼬리 끝으로 지연되어 전달됩니다.
        for j in range(8):
            rotate('Tail%02d'%j,x=.035+(.035+j*.009)*amp*math.sin(phase-j*.5)+.075*fold*math.cos(j*.45),y=(.025+j*.006)*amp*math.sin(phase-j*.55))
        for pb in rig.pose.bones:
            pb.keyframe_insert(data_path='rotation_quaternion',frame=f+1,group=pb.name)
            if pb.name=='Hips':pb.keyframe_insert(data_path='location',frame=f+1,group=pb.name)
    track=rig.animation_data.nla_tracks.new();track.name=act.name;track.strips.new(act.name,1,act);track.mute=True
rig.animation_data.action=bpy.data.actions['AlienMermaid_Idle'];bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=61;bpy.context.scene.frame_set(1)
for o in bpy.context.selected_objects:o.select_set(False)
obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
print('Rig ready:',len(data.bones),'bones;',len(obj.data.vertices),'vertices; clips',lengths)
print('Unweighted:',sum(not v.groups for v in obj.data.vertices))
