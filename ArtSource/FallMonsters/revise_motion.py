"""기존 리그를 유지하고 옷의 연장 변형, 보행 가중치와 비행 동작을 수정한다."""
import bpy, math
from mathutils import Vector
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'

def repair_arm():
    obj=bpy.data.objects['Scarecrow_Body'];old=obj.data
    if old.get('extension_repaired'):return
    # 손과 소매의 삼각형을 분리해 옷이 긴 삼각형으로 늘어나지 않게 한다.
    verts=[];faces=[];weights=[];uvs=[];lookup={};mats=[]
    group_names=[g.name for g in obj.vertex_groups]
    hand=obj.vertex_groups['RightHand'].index
    fore=obj.vertex_groups['RightForeArm'].index
    uv=old.uv_layers.active
    for p in old.polygons:
        center=sum((old.vertices[i].co for i in p.vertices),Vector())/len(p.vertices)
        detached=center.x<-.60 and center.z<1.23
        face=[];faceuv=[]
        for li in p.loop_indices:
            vi=old.loops[li].vertex_index;k=(vi,detached)
            if k not in lookup:
                lookup[k]=len(verts);v=old.vertices[vi];verts.append(v.co.copy())
                w={g.group:g.weight for g in v.groups}
                if detached:w={hand:1.0}
                elif hand in w:w[fore]=w.get(fore,0)+w.pop(hand)
                weights.append(w)
            face.append(lookup[k]);faceuv.append(tuple(uv.data[li].uv) if uv else (0,0))
        faces.append(face);uvs.append(faceuv);mats.append(p.material_index)
    # 소매와 손 사이에는 나무줄기 다섯 가닥을 연결한다. 천과 손은 원래 크기를 유지한다.
    rig=obj.parent;a=rig.data.bones['RightForeArm'].head_local;b=rig.data.bones['RightHand'].head_local
    axis=(b-a).normalized();u=axis.cross(Vector((0,1,0))).normalized();v=axis.cross(u)
    for strand in range(5):
        start=len(verts);theta=strand*math.tau/5
        for ring in range(9):
            t=ring/8;center=a.lerp(b,t)+(u*math.cos(theta+t*.4)+v*math.sin(theta+t*.4))*.065
            for j in range(7):
                angle=j*math.tau/7;verts.append(center+(u*math.cos(angle)+v*math.sin(angle))*.048)
                weights.append({fore:1-t,hand:t})
        for ring in range(8):
            for j in range(7):
                faces.append([start+ring*7+j,start+ring*7+(j+1)%7,start+(ring+1)*7+(j+1)%7,start+(ring+1)*7+j])
                uvs.append([(j/7,ring/8),((j+1)/7,ring/8),((j+1)/7,(ring+1)/8),(j/7,(ring+1)/8)])
                mats.append(len(old.materials))
    mesh=bpy.data.meshes.new('Scarecrow_RepairedSurface');mesh.from_pydata(verts,[],faces);mesh.update()
    for m in old.materials:mesh.materials.append(m)
    wood=bpy.data.materials.get('Scarecrow_ExtensionWood') or bpy.data.materials.new('Scarecrow_ExtensionWood');wood.use_nodes=True
    bsdf=next(n for n in wood.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value=(.19,.075,.024,1);bsdf.inputs['Roughness'].default_value=.86
    mesh.materials.append(wood);layer=mesh.uv_layers.new(name='UVMap')
    for p,coords,mi in zip(mesh.polygons,uvs,mats):
        p.material_index=mi;p.use_smooth=True
        for li,co in zip(p.loop_indices,coords):layer.data[li].uv=co
    obj.data=mesh
    for name in group_names:obj.vertex_groups.new(name=name)
    for i,w in enumerate(weights):
        for gi,value in w.items():
            if value>0:obj.vertex_groups[gi].add([i],value,'REPLACE')
    mesh['extension_repaired']=True

def repair_hedgehog():
    obj=bpy.data.objects['LeafHedgehog_Body']
    for v in obj.data.vertices:
        for g in list(v.groups):obj.vertex_groups[g.group].remove([v.index])
        # 등과 얼굴은 몸통에 고정하고, 바닥 가까운 발에만 보행 영향을 준다.
        foot=max(0,min(1,(.20-v.co.z)/.14))*max(0,min(1,(abs(v.co.x)-.16)/.13))
        side='Left' if v.co.x>0 else 'Right';part='Front' if v.co.y<0 else 'Back'
        obj.vertex_groups['Body'].add([v.index],1-foot,'REPLACE')
        if foot>0:obj.vertex_groups[side+part+'Leg'].add([v.index],foot,'REPLACE')

def animate(name,states):
    rig=bpy.data.objects[name+'_Body'].parent
    for state,length in states:
        action=bpy.data.actions[name+'_'+state]
        # 기존 액션 데이터와 슬롯을 유지하면서 키만 다시 작성한다.
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in list(bag.fcurves):bag.fcurves.remove(curve)
        rig.animation_data.action=action
        for frame in range(1,length+1):
            t=(frame-1)/(length-1);wave=math.sin(t*math.tau)
            for p in rig.pose.bones:p.rotation_mode='XYZ';p.rotation_euler=(0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
            body=rig.pose.bones.get('Body') or rig.pose.bones.get('Spine')
            if name=='MyceliumSkeleton':
                body.rotation_euler.z=.045*wave;body.rotation_euler.x=-.07
                rig.pose.bones['Root'].location.z=.045*(1-math.cos(t*math.tau))
                for side,sgn in [('Left',1),('Right',-1)]:rig.pose.bones[side+'Arm'].rotation_euler.z=sgn*.20+.035*wave
            elif name=='LeafHedgehog':
                body.location.y=.016*(1-math.cos(t*math.tau*2));body.rotation_euler.z=.018*wave
                if state=='Walk':
                    for p in rig.pose.bones:
                        if p.name.endswith('Leg'):p.rotation_euler.x=.16*math.sin(t*math.tau+(math.pi if ('Left' in p.name)==('Front' in p.name) else 0))
            elif name=='BoneCrow':
                flap=math.sin(t*math.tau*(2 if state=='Walk' else 1))
                for side,sgn in [('Left',1),('Right',-1)]:
                    wing=rig.pose.bones[side+'Wing'];tip=rig.pose.bones[side+'WingTip']
                    # 날개 길이 방향에 수직인 로컬 X축으로 크게 상하 운동한다.
                    wing.rotation_euler.x=sgn*(.85*flap if state in ('Idle','Walk','Recovery') else -.5 if state=='AttackReady' else .15)
                    tip.rotation_euler.x=sgn*(.36*math.sin(t*math.tau-.6) if state in ('Idle','Walk','Recovery') else .2)
                body.location.y=.035*wave
            for p in rig.pose.bones:
                for prop in ('rotation_euler','location','scale'):p.keyframe_insert(data_path=prop,frame=frame,group=p.name)
    rig.animation_data.action=None
    for p in rig.pose.bones:p.rotation_euler=(0,0,0);p.location=(0,0,0);p.scale=(1,1,1)

def export(name):
    obj=bpy.data.objects[name+'_Body'];rig=obj.parent
    for o in bpy.context.scene.objects:o.hide_set(False)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
    location=rig.location.copy();rig.location=(0,0,0);bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Art/FallMonsters/Models/'+name+'.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,axis_forward='-Z',axis_up='Y')
    rig.location=location

repair_arm();repair_hedgehog()
animate('MyceliumSkeleton',[('Walk',49)])
animate('LeafHedgehog',[('Idle',49),('Walk',25)])
animate('BoneCrow',[(s,49) for s in ['Idle','Walk','AttackReady','Attack','Recovery']])
for name in ['MyceliumSkeleton','Scarecrow','LeafHedgehog','BoneCrow']:export(name)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/FallMonsters/FallMonsters.blend')
print('Repaired meshes and flight/glide actions exported')
