"""사용자가 제공한 OBJ에 변형 리그와 크게 읽히는 전투 동작을 제작한다."""
import bpy, math, os, json
from mathutils import Vector
ROOT=r'C:/Users/mbc/Desktop/NC_AI/DogLife'
OUT=ROOT+'/Assets/Art/FallMonsters'
os.makedirs(OUT+'/Models',exist_ok=True)
os.makedirs(OUT+'/Textures',exist_ok=True)

def build(name,height):
    obj=bpy.data.objects[name+'_Source']
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    # 배치용 X 오프셋을 제거하고 실제 정점 좌표를 기준으로 정규화한다.
    obj.location.x-= ['MyceliumSkeleton','BoneCrow','LeafHedgehog','Scarecrow'].index(name)*4
    mat=obj.matrix_world.copy()
    for v in obj.data.vertices:v.co=mat@v.co
    obj.matrix_world.identity()
    lo=min(v.co.z for v in obj.data.vertices);hi=max(v.co.z for v in obj.data.vertices)
    factor=height/(hi-lo)
    cx=(min(v.co.x for v in obj.data.vertices)+max(v.co.x for v in obj.data.vertices))*.5
    cy=(min(v.co.y for v in obj.data.vertices)+max(v.co.y for v in obj.data.vertices))*.5
    for v in obj.data.vertices:v.co=Vector(((v.co.x-cx)*factor,(v.co.y-cy)*factor,(v.co.z-lo)*factor))
    bones=[]
    def bone(n,a,b,p=None):bones.append((n,Vector(a)*height,Vector(b)*height,p))
    bone('Root',(0,0,0),(0,0,.12))
    if name in ('MyceliumSkeleton','Scarecrow'):
        bone('Hips',(0,0,.34),(0,0,.46),'Root')
        bone('Spine',(0,0,.46),(0,0,.55),'Hips');bone('Spine1',(0,0,.55),(0,0,.63),'Spine');bone('Spine2',(0,0,.63),(0,0,.70),'Spine1')
        bone('Neck',(0,0,.70),(0,0,.76),'Spine2');bone('Head',(0,0,.76),(0,0,.98),'Neck')
        for side,sgn in [('Left',1),('Right',-1)]:
            bone(side+'Shoulder',(0,0,.66),(sgn*.16,0,.65),'Spine2')
            bone(side+'Arm',(sgn*.16,0,.65),(sgn*.23,0,.48),side+'Shoulder')
            bone(side+'ForeArm',(sgn*.23,0,.48),(sgn*.29,-.01,.33),side+'Arm')
            bone(side+'Hand',(sgn*.29,-.01,.33),(sgn*.30,-.01,.22),side+'ForeArm')
            bone(side+'UpLeg',(sgn*.085,0,.38),(sgn*.10,0,.21),'Hips')
            bone(side+'Leg',(sgn*.10,0,.21),(sgn*.11,0,.055),side+'UpLeg')
            bone(side+'Foot',(sgn*.11,0,.055),(sgn*.11,-.11,.025),side+'Leg')
    else:
        bone('Body',(0,0,.18),(0,0,.53),'Root')
        bone('Head',(0,-.15,.38),(0,-.40,.42),'Body')
        if name=='BoneCrow':
            for side,sgn in [('Left',1),('Right',-1)]:
                bone(side+'Wing',(sgn*.10,0,.54),(sgn*.34,0,.60),'Body')
                bone(side+'WingTip',(sgn*.34,0,.60),(sgn*.57,0,.42),side+'Wing')
                bone(side+'Leg',(sgn*.12,0,.27),(sgn*.14,-.04,.03),'Body')
        else:
            bone('Spines',(0,.05,.44),(0,.07,.89),'Body')
            for side,sgn in [('Left',1),('Right',-1)]:
                for part,y in [('Front',-.24),('Back',.24)]:bone(side+part+'Leg',(sgn*.26,y,.25),(sgn*.32,y,.03),'Body')
    armdata=bpy.data.armatures.new(name+'_Rig');rig=bpy.data.objects.new('skel',armdata);bpy.context.collection.objects.link(rig)
    obj.select_set(False);rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
    for n,a,b,p in bones:
        e=armdata.edit_bones.new(n);e.head=a;e.tail=b
        if p:e.parent=armdata.edit_bones[p]
    bpy.ops.object.mode_set(mode='OBJECT')
    candidates=[b for b in bones if b[0]!='Root']
    groups={n:obj.vertex_groups.new(name=n) for n,_,_,_ in candidates}
    # 가까운 뼈까지의 거리로 가중치를 부드럽게 나누어 손과 날개도 따라가게 한다.
    for v in obj.data.vertices:
        if name=='Scarecrow' and abs(v.co.x)>height*.235 and v.co.z<height*.40:
            groups[('Left' if v.co.x>0 else 'Right')+'Hand'].add([v.index],1,'REPLACE')
            continue
        distances=[]
        for n,a,b,p in candidates:
            ab=b-a;t=max(0,min(1,(v.co-a).dot(ab)/ab.length_squared));d=(v.co-(a+ab*t)).length
            distances.append((d,n))
        near=sorted(distances)[:3];weights=[math.exp(-d*35/height) for d,n in near];total=sum(weights)
        for (d,n),w in zip(near,weights):groups[n].add([v.index],w/total,'REPLACE')
    mod=obj.modifiers.new('FallRig','ARMATURE');mod.object=rig;obj.parent=rig
    obj.name=name+'_Body'
    for m in obj.data.materials:
        if not m:continue
        m.name=name+'_Material'
        for node in m.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image:
                im=node.image;src=bpy.path.abspath(im.filepath);suffix='Normal' if 'normal' in src.lower() else 'BaseColor'
                dst=f'{OUT}/Textures/{name}_{suffix}.png'
                import shutil
                if os.path.exists(src):shutil.copy2(src,dst);im.filepath=dst
    rig.animation_data_create()
    for pb in rig.pose.bones:pb.rotation_mode='XYZ'
    for action_name,length in [('Idle',48),('Walk',24),('AttackReady',24),('Attack',20),('Recovery',24)]:
        action=bpy.data.actions.new(name+'_'+action_name);rig.animation_data.action=action;action.use_fake_user=True
        for frame in range(1,length+1,2):
            t=(frame-1)/(length-1);wave=math.sin(t*math.tau)
            for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
            body=rig.pose.bones.get('Spine') or rig.pose.bones.get('Body')
            if action_name=='Idle':body.rotation_euler.x=.035*wave
            if action_name=='Walk':
                body.rotation_euler.z=.08*wave
                for pb in rig.pose.bones:
                    if 'Leg' in pb.name or pb.name.endswith('Arm'):pb.rotation_euler.x=.45*wave*(1 if pb.name.startswith('Left') else -1)
            if name=='BoneCrow':
                for side,sgn in [('Left',1),('Right',-1)]:
                    pb=rig.pose.bones[side+'Wing'];pb.rotation_euler.x=sgn*(.55*wave if action_name in ('Idle','Walk') else -.75*math.sin(t*math.pi/2) if action_name=='AttackReady' else .35)
                if action_name=='Attack':body.rotation_euler.x=.65
            elif action_name=='AttackReady':
                body.rotation_euler.x=-.30*math.sin(t*math.pi/2)
                if name=='MyceliumSkeleton':
                    body.scale=(1+.16*t,1+.10*t,1+.16*t)
                    for side,sgn in [('Left',1),('Right',-1)]:rig.pose.bones[side+'Arm'].rotation_euler.z=sgn*.65*t
                if name=='Scarecrow':rig.pose.bones['RightArm'].rotation_euler.x=-1.1*t;body.rotation_euler.z=-.45*t
                if name=='LeafHedgehog':body.scale=(1.12, .78, 1.12);rig.pose.bones['Head'].rotation_euler.x=.5*t
            elif action_name=='Attack':
                body.rotation_euler.x=.35*math.sin(t*math.pi)
                if name=='MyceliumSkeleton':
                    for side,sgn in [('Left',1),('Right',-1)]:rig.pose.bones[side+'Arm'].rotation_euler.z=sgn*.9;body.scale=(1.18,1.1,1.18)
                if name=='Scarecrow':body.rotation_euler.z=-.45+1.25*t;rig.pose.bones['RightArm'].rotation_euler.x=-1.1
                if name=='LeafHedgehog':rig.pose.bones['Spines'].scale=(1.08,1.08,1.08)
            elif action_name=='Recovery':body.rotation_euler.x=.3*(1-t)
            for pb in rig.pose.bones:
                for prop in ('rotation_euler','location','scale'):pb.keyframe_insert(data_path=prop,frame=frame,group=pb.name)
        track=rig.animation_data.nla_tracks.new();track.name=action.name;track.strips.new(action.name,1,action)
        track.mute=True
    rig.animation_data.action=None
    for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=f'{OUT}/Models/{name}.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,axis_forward='-Z',axis_up='Y')
    return {'name':name,'bones':len(bones),'height':height,'vertices':len(obj.data.vertices)}

if __name__=='__main__':
    name=globals().get('BUILD_NAME','MyceliumSkeleton')
    print(json.dumps(build(name,{'MyceliumSkeleton':2.7,'Scarecrow':2.8,'BoneCrow':1.35,'LeafHedgehog':1.15}[name])))
