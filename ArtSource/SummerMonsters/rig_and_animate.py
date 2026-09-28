import bpy, math, os, shutil, json
from mathutils import Vector
ROOT='C:/unity/DogLife'
OUT=ROOT+'/Assets/Art/SummerMonsters'
NAMES=['HermitCrab','BoneTortoise','BlueJellyfish','CoralMermaid']

def smooth_weights(obj,height):
    # Smooth the regional skin assignments through mesh topology, preserving
    # rigid shell centers while avoiding stretched seams at limb attachments.
    import numpy as np
    n=len(obj.data.vertices);g=len(obj.vertex_groups)
    weights=np.zeros((n,g),dtype=np.float64)
    for vertex in obj.data.vertices:
        for element in vertex.groups:weights[vertex.index,element.group]=element.weight
    edges=np.array([edge.vertices[:] for edge in obj.data.edges]);a=edges[:,0];b=edges[:,1]
    degree=np.bincount(np.concatenate([a,b]),minlength=n).clip(1)[:,None]
    for iteration in range(18):
        totals=np.zeros_like(weights);np.add.at(totals,a,weights[b]);np.add.at(totals,b,weights[a])
        weights=weights*.35+(totals/degree)*.65
    from mathutils.kdtree import KDTree
    kd=KDTree(n)
    for vertex in obj.data.vertices:kd.insert(vertex.co,vertex.index)
    kd.balance()
    neighbors=[[index for co,index,distance in kd.find_n(vertex.co,16) if distance<height*.04] for vertex in obj.data.vertices]
    for iteration in range(4):weights=np.array([weights[i]*.3+weights[near].mean(axis=0)*.7 for i,near in enumerate(neighbors)])
    weights[weights<.003]=0;weights/=weights.sum(axis=1)[:,None]
    for group in obj.vertex_groups:group.remove(list(range(n)))
    for i in range(n):
        for k in np.nonzero(weights[i])[0]:obj.vertex_groups[int(k)].add([i],float(weights[i,k]),'REPLACE')

def build(name):
    height={'HermitCrab':1.6,'BoneTortoise':1.55,'BlueJellyfish':2.05,'CoralMermaid':2.45}[name]
    src=bpy.data.objects[name+'_Source'];obj=src.copy();obj.data=src.data.copy();bpy.context.collection.objects.link(obj);obj.location=(0,0,0);obj.name=name+'_Body'
    lo=min(v.co.z for v in obj.data.vertices);hi=max(v.co.z for v in obj.data.vertices)
    for v in obj.data.vertices:v.co=(v.co-Vector((0,0,lo)))*(height/(hi-lo))
    # This source faces +Y, unlike the other three sources. Normalize the mesh
    # before creating Head/Jaw weights, not merely the prefab's visible yaw.
    if name=='BoneTortoise':
        for v in obj.data.vertices:v.co.x=-v.co.x;v.co.y=-v.co.y
    for p in obj.data.polygons:p.use_smooth=True
    bones=[]
    def B(n,a,b,p='Root'):bones.append((n,Vector(a)*height,Vector(b)*height,p))
    B('Root',(0,0,0),(0,0,.1),None)
    if name=='CoralMermaid':
        B('Hips',(.02,0,.45),(.02,0,.55));B('Spine',(.02,0,.55),(.02,0,.68),'Hips');B('Chest',(.02,0,.68),(.02,0,.77),'Spine');B('Head',(.02,0,.77),(.02,0,.96),'Chest')
        B('TailA',(.02,0,.46),(.11,0,.29),'Hips');B('TailB',(.11,0,.29),(.12,0,.14),'TailA');B('TailFin',(.12,0,.14),(-.19,0,.07),'TailB')
        for side,s in [('L',1),('R',-1)]:
            B(side+'Arm',(.02+s*.10,0,.71),(.02+s*.18,0,.58),'Chest');B(side+'Hand',(.02+s*.18,0,.58),(.02+s*.24,-.025,.45),side+'Arm')
        B('Hair',(0,.06,.82),(-.10,.15,.54),'Head')
    elif name=='BlueJellyfish':
        B('Body',(0,0,.68),(0,0,.89));B('Head',(0,0,.89),(0,0,.99),'Body')
        for k in range(8):
            a=k*math.tau/8
            for j in range(3):
                r=.13+j*.014
                B('T%d_%d'%(k,j),(r*math.cos(a),r*math.sin(a),.68-j*.21),(r*math.cos(a),r*math.sin(a),.47-j*.21),'Body' if j==0 else 'T%d_%d'%(k,j-1))
    else:
        B('Body',(0,.04,.30),(0,.04,.65));B('Head',(0,-.18,.35),(0,-.43,.30),'Body')
        if name=='BoneTortoise':B('Jaw',(0,-.35,.27),(0,-.54,.23),'Head')
        for side,s in [('L',1),('R',-1)]:
            for k,y in enumerate([-.20,.02,.23] if name=='HermitCrab' else [-.20,.25]):
                B(side+'Leg'+str(k),(s*.20,y,.29),(s*.43,y-.03,.05),'Body')
            if name=='HermitCrab':B(side+'Claw',(s*.18,-.24,.33),(s*.32,-.44,.16),'Body')
    data=bpy.data.armatures.new(name+'_Rig');rig=bpy.data.objects.new(name+'_Rig',data);bpy.context.collection.objects.link(rig)
    for o in bpy.context.selected_objects:o.select_set(False)
    rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
    for n,a,b,p in bones:
        eb=data.edit_bones.new(n);eb.head=a;eb.tail=b
        if p:eb.parent=data.edit_bones[p]
    bpy.ops.object.mode_set(mode='OBJECT')
    groups={n:obj.vertex_groups.new(name=n) for n,a,b,p in bones if n!='Root'}
    for v in obj.data.vertices:
        x,y,z=v.co/height;candidates=[b for b in bones if b[0]!='Root']
        if name in ('BoneTortoise','HermitCrab'):
            if z>.50:candidates=[b for b in candidates if b[0]=='Body']
            elif name=='HermitCrab' and y<-.23 and abs(x)>.15:candidates=[b for b in candidates if b[0]==('L' if x>0 else 'R')+'Claw']
            elif y<-.28 and abs(x)<.22:candidates=[b for b in candidates if b[0] in ('Head','Jaw')]
            elif abs(x)>.23 and z<.32:candidates=[b for b in candidates if 'Leg' in b[0] and b[0].startswith('L' if x>0 else 'R')]
            else:candidates=[b for b in candidates if b[0] in ('Body','Head')]
        elif name=='BlueJellyfish':
            if z>.66:candidates=[b for b in candidates if b[0] in ('Body','Head')]
            else:
                k=round(math.atan2(y,x)/math.tau*8)%8;candidates=[b for b in candidates if b[0].startswith('T%d_'%k)]
        elif name=='CoralMermaid':
            if z<.43:candidates=[b for b in candidates if b[0].startswith('Tail') or b[0]=='Hips']
            elif y>.08 and z>.5:candidates=[b for b in candidates if b[0] in ('Hair','Head')]
            elif abs(x-.02)>.13 and .43<z<.74:candidates=[b for b in candidates if b[0].startswith('L' if x>.02 else 'R')]
            else:candidates=[b for b in candidates if b[0] in ('Hips','Spine','Chest','Head')]
        near=[]
        for n,a,b,p in candidates:
            ab=b-a;t=max(0,min(1,(v.co-a).dot(ab)/ab.length_squared));near.append(((v.co-a-ab*t).length,n))
        near=sorted(near)[:2];ws=[math.exp(-d*45/height) for d,n in near];total=sum(ws)
        for (d,n),w in zip(near,ws):groups[n].add([v.index],w/total,'REPLACE')
    smooth_weights(obj,height)
    mod=obj.modifiers.new('Summer skeleton','ARMATURE');mod.object=rig;obj.parent=rig
    os.makedirs(OUT+'/Models',exist_ok=True);os.makedirs(OUT+'/Textures',exist_ok=True)
    for i,m in enumerate(obj.data.materials):
        if not m:continue
        m=m.copy();obj.data.materials[i]=m;m.name=name+'_Material'
        for node in m.node_tree.nodes:
            if node.type=='NORMAL_MAP':node.inputs['Strength'].default_value=0
            if node.type=='TEX_IMAGE' and node.image:
                im=node.image.copy();node.image=im;suffix='Normal' if 'normal' in im.filepath.lower() else 'BaseColor';dst=OUT+'/Textures/'+name+'_'+suffix+'.png';shutil.copy2(bpy.path.abspath(im.filepath),dst);im.filepath=dst
    rig.animation_data_create()
    states=['Idle','Walk','AttackReady','Attack','Recovery']
    if name=='CoralMermaid':states+=['Dive','Burrow','Emerge']
    if name=='BoneTortoise':states+=['GuardIn','GuardLoop','GuardOut']
    for state in states:
        action=bpy.data.actions.new(name+'_'+state);rig.animation_data.action=action;action.use_fake_user=True
        for frame in range(1,32):
            t=(frame-1)/30;wave=math.sin(t*math.tau);s=math.sin(t*math.pi/2);strike=math.sin(t*math.pi)
            for pb in rig.pose.bones:pb.rotation_mode='XYZ';pb.rotation_euler=(0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
            def rot(n,x=0,y=0,z=0):
                if n in rig.pose.bones:rig.pose.bones[n].rotation_euler=(x,y,z)
            body=rig.pose.bones.get('Body') or rig.pose.bones.get('Hips')
            if state in ('Idle','Walk'):body.rotation_euler.z=(.05 if state=='Idle' else .13)*wave
            if name=='CoralMermaid':
                for j,n in enumerate(['TailA','TailB','TailFin']):rot(n,x=(.16 if state=='Idle' else .32)*math.sin(t*math.tau-j*.7),z=.13*wave)
                rot('Hair',x=.09*wave)
                if state in ('Dive','AttackReady'):rot('Spine',x=-.65*s);rot('TailA',x=.75*s);rot('TailB',x=.65*s)
                if state=='Burrow':rot('Spine',x=-.7);rot('TailA',x=.45*wave)
                if state in ('Attack','Emerge'):rot('Spine',x=.65*strike);rot('Chest',x=-.3*strike);rot('TailA',x=-.65*strike);rot('TailB',x=.9*strike)
                for side,k in [('L',1),('R',-1)]:
                    a=.15*wave if state in ('Idle','Walk','Burrow') else .9*s if state in ('Dive','AttackReady') else -1.05*strike if state in ('Attack','Emerge') else -.3*(1-t)
                    rot(side+'Arm',x=a,z=k*.35*abs(a));rot(side+'Hand',x=-a*.5)
            elif name=='BlueJellyfish':
                pulse=.08*wave if state in ('Idle','Walk') else -.22*s if state=='AttackReady' else .27*strike if state=='Attack' else .12*(1-t)
                body.scale=(1+pulse,1-pulse*.65,1+pulse)
                for k in range(8):
                    for j in range(3):rot('T%d_%d'%(k,j),x=.24*math.sin(t*math.tau+k*.5-j*.7)+(s*.4 if state=='AttackReady' else -.65*strike if state=='Attack' else 0),z=.12*math.cos(t*math.tau+k))
            elif name=='BoneTortoise':
                guard=s if state=='GuardIn' else 1 if state=='GuardLoop' else 1-s if state=='GuardOut' else 0
                rig.pose.bones['Head'].location.y=-height*.20*guard
                rig.pose.bones['Head'].scale=(1,.40 if guard>.99 else 1-.6*guard,1)
                rot('Head',x=-.35*s if state=='AttackReady' else .55*strike if state=='Attack' else -.2*guard)
                rot('Jaw',x=.65*s if state=='AttackReady' else .65*(1-t) if state=='Attack' else 0)
                if state=='Attack':rig.pose.bones['Head'].location.y=height*.16*strike;body.rotation_euler.x=.18*strike
                for k in range(2):
                    for side,d in [('L',1),('R',-1)]:
                        pb=rig.pose.bones[side+'Leg'+str(k)];pb.scale=(1,1-.65*guard,1);rot(pb.name,x=.38*wave*d*((-1)**k) if state=='Walk' else .45*guard)
            else:
                for side,d in [('L',1),('R',-1)]:
                    for k in range(3):rot(side+'Leg'+str(k),x=.40*wave*d*((-1)**k) if state=='Walk' else .1*wave)
                    rot(side+'Claw',x=-.65*s if state=='AttackReady' else 1.0*strike if state=='Attack' else .1*wave,z=d*(.4*s if state=='AttackReady' else -.65*strike if state=='Attack' else 0))
                body.rotation_euler.x=-.2*s if state=='AttackReady' else .3*strike if state=='Attack' else .06*wave
            for pb in rig.pose.bones:
                if name=='HermitCrab' and 'Claw' in pb.name:pb.rotation_euler*=.6
                if name=='CoralMermaid' and any(part in pb.name for part in ['Arm','Hand','Spine']):pb.rotation_euler*=.65
                if name=='BoneTortoise' and 'Leg' in pb.name:pb.location.y=-height*.15*guard
                for prop in ('rotation_euler','location','scale'):pb.keyframe_insert(data_path=prop,frame=frame,group=pb.name)
        track=rig.animation_data.nla_tracks.new();track.name=action.name;track.strips.new(action.name,1,action)
    rig.animation_data.action=None
    for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
    for o in bpy.context.selected_objects:o.select_set(False)
    obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=OUT+'/Models/'+name+'.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,axis_forward='-Z',axis_up='Y')
    for tr in rig.animation_data.nla_tracks:tr.mute=True
    src.hide_set(True);src.hide_render=True;rig.location.x=NAMES.index(name)*4
    print(json.dumps({'name':name,'bones':len(bones),'clips':states,'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons)}))

if __name__=='__main__':build(globals().get('BUILD_NAME','HermitCrab'))
