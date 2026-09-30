"""Unity 본 좌표를 보존하며 Blender에서 대검을 든 포즈를 편집하고 굽습니다."""
import bpy, json, math, os
from mathutils import Vector, Quaternion, Matrix
ROOT = r'C:/Users/mbc/Desktop/NC_AI/DogLife'
with open(ROOT+'/Temp/FrozenKnight/blender_input.json', encoding='utf-8') as f:
    data=json.load(f)
bones=data['bones']; count=len(bones); ids={b['name']:i for i,b in enumerate(bones)}
def vec(v): return Vector((v['x'],v['y'],v['z']))
def quat(q): return Quaternion((q['w'],q['x'],q['y'],q['z']))
def qdict(q): return dict(x=q.x,y=q.y,z=q.z,w=q.w)
def fk(ps,qs):
    wp=[];wq=[]
    for i,b in enumerate(bones):
        parent=b['parent']
        wp.append(ps[i].copy() if parent<0 else wp[parent]+wq[parent]@ps[i])
        wq.append(qs[i].copy() if parent<0 else wq[parent]@qs[i])
    return wp,wq
def solve(ps,qs,side,goal):
    wp,wq=fk(ps,qs);a=ids[side+'Arm'];b=ids[side+'ForeArm'];h=ids[side+'Hand'];origin=wp[a]
    l1=(wp[b]-origin).length;l2=(wp[h]-wp[b]).length;axis=(goal-origin).normalized()
    distance=min(max((goal-origin).length,abs(l1-l2)+.001),l1+l2-.004)
    goal=origin+axis*distance;along=(l1*l1-l2*l2+distance*distance)/(2*distance)
    pole=Vector((-1 if side=='Right' else 1,-.45,.12));pole=(pole-axis*pole.dot(axis)).normalized()
    elbow=origin+axis*along+pole*math.sqrt(max(0,l1*l1-along*along))
    turn=(wp[b]-origin).rotation_difference(elbow-origin)
    qs[a]=wq[bones[a]['parent']].inverted()@turn@wq[a]
    wp,wq=fk(ps,qs);turn=(wp[h]-wp[b]).rotation_difference(goal-wp[b])
    qs[b]=wq[bones[b]['parent']].inverted()@turn@wq[b]

grip=vec(data['grip']);axis=(vec(data['tip'])-grip).normalized();worst=0;errors={}
for motion in data['motions']:
    name=motion['name']
    for f in range(motion['frames']):
        start=f*count;ps=[vec(v) for v in motion['positions'][start:start+count]];qs=[quat(v) for v in motion['rotations'][start:start+count]]
        wp,wq=fk(ps,qs);rh=ids['RightHand'];lh=ids['LeftHand']
        # 이동 중 팔을 흔드는 대신 대검 무게를 두 손으로 지탱합니다.
        guard=name in ('Idle','Walk','PushUpper','Roar','FrostChannel')
        if guard:
            goal=wp[ids['RightArm']]+Vector((.09,-.19,-.17))
            solve(ps,qs,'Right',goal);wp,wq=fk(ps,qs)
            desired=Vector((.2,.60,-.78)).normalized()
            wrist=(wq[rh]@axis).rotation_difference(desired)@wq[rh]
            qs[rh]=wq[bones[rh]['parent']].inverted()@wrist
        # 전신 소환/회전 베기는 원본 팔 궤적을 유지하고 보조 손을 손잡이에 맞춥니다.
        wp,wq=fk(ps,qs)
        if name in ('Idle','Walk','IceWallFull','Spin3Sword'):
            # 넓게 벌린 원본 포즈에서도 보조 손이 닿도록 검을 몸통 쪽으로 조금 가져옵니다.
            for attempt in range(5):
                target=wp[rh]+wq[rh]@(grip-axis*.085);shoulder=wp[ids['LeftArm']]
                reach=(wp[ids['LeftForeArm']]-shoulder).length+(wp[lh]-wp[ids['LeftForeArm']]).length-.008
                excess=(target-shoulder).length-reach
                if excess<=0:break
                wrist=wq[rh].copy();solve(ps,qs,'Right',wp[rh]+(shoulder-target).normalized()*(excess+.004))
                wp,wq=fk(ps,qs);qs[rh]=wq[bones[rh]['parent']].inverted()@wrist;wp,wq=fk(ps,qs)
            target=wp[rh]+wq[rh]@(grip-axis*.085)
            solve(ps,qs,'Left',target);wp,wq=fk(ps,qs)
            error=(wp[lh]-target).length;worst=max(worst,error);errors[name]=max(errors.get(name,0),error)
        for i,q in enumerate(qs):motion['rotations'][start+i]=qdict(q.normalized())

# 전용 씬을 사용하므로 사용자가 열어 둔 기본 씬은 지우지 않습니다.
scene=bpy.data.scenes.get('FrozenKnight_SwordMotion') or bpy.data.scenes.new('FrozenKnight_SwordMotion')
bpy.context.window.scene=scene
for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
arm=bpy.data.armatures.new('FrozenKnight_Rig');rig=bpy.data.objects.new('FrozenKnight_Rig',arm);scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
restp,restq=fk([vec(b['p']) for b in bones],[quat(b['q']) for b in bones])
for i,b in enumerate(bones):
    bone=arm.edit_bones.new(b['name']);bone.head=restp[i];bone.tail=restp[i]+restq[i]@Vector((0,.08,0));bone.matrix=Matrix.Translation(restp[i])@restq[i].to_matrix().to_4x4()
    if b['parent']>=0:bone.parent=arm.edit_bones[bones[b['parent']]['name']]
bpy.ops.object.mode_set(mode='OBJECT')
def mesh_obj(name,vertices,triangles):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],[triangles[i:i+3] for i in range(0,len(triangles),3)]);mesh.update()
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj);return obj
skin=data['skin'];body=mesh_obj('Knight_Armor',[vec(v) for v in skin['vertices']],skin['triangles'])
for b in bones:body.vertex_groups.new(name=b['name'])
for i in range(len(skin['vertices'])):
    for k in range(4):
        bone=skin['indices'][i*4+k];weight=skin['weights'][i*4+k]
        if bone>=0 and weight>0:body.vertex_groups[bone].add([i],weight,'REPLACE')
modifier=body.modifiers.new('Rig','ARMATURE');modifier.object=rig
rh=ids['RightHand'];sword=mesh_obj('Greatsword',[restp[rh]+restq[rh]@vec(v) for v in data['sword']],data['swordTriangles'])
group=sword.vertex_groups.new(name='RightHand');group.add(list(range(len(data['sword']))),1,'REPLACE');modifier=sword.modifiers.new('HandGrip','ARMATURE');modifier.object=rig
for obj,color in [(body,(.16,.25,.34,1)),(sword,(.4,.72,.88,1))]:
    material=bpy.data.materials.new(obj.name+'_Preview');material.diffuse_color=color;obj.data.materials.append(material)
rig.animation_data_create();scene.render.fps=30
for action in list(bpy.data.actions):
    if any(action.name.startswith(m['name']+'_SwordRefined') for m in data['motions']):bpy.data.actions.remove(action)
for motion in data['motions']:
    action=bpy.data.actions.new(motion['name']+'_SwordRefined');action.use_fake_user=True;rig.animation_data.action=action
    for f in range(motion['frames']):
        start=f*count;wp,wq=fk([vec(v) for v in motion['positions'][start:start+count]],[quat(v) for v in motion['rotations'][start:start+count]])
        matrices=[Matrix.Translation(wp[i])@wq[i].to_matrix().to_4x4() for i in range(count)]
        for i,b in enumerate(bones):
            pb=rig.pose.bones[b['name']];pb.rotation_mode='QUATERNION';rest=arm.bones[b['name']].matrix_local
            parent=b['parent'];basis=rest.inverted()@matrices[i]
            if parent>=0:basis=rest.inverted()@arm.bones[bones[parent]['name']].matrix_local@matrices[parent].inverted()@matrices[i]
            pb.matrix_basis=basis
            pb.keyframe_insert('location',frame=f+1);pb.keyframe_insert('rotation_quaternion',frame=f+1);pb.keyframe_insert('scale',frame=f+1)
rig.animation_data.action=bpy.data.actions.get('Idle_SwordRefined');scene.frame_start=1;scene.frame_end=data['motions'][0]['frames'];scene.frame_set(1)
# Unity Y-up 좌표의 결과를 편집 화면에서만 Blender Z-up으로 돌립니다.
display=Matrix.Rotation(math.pi/2,4,'X')
for obj in (rig,body,sword):obj.matrix_world=display
data.pop('skin');data.pop('sword');data.pop('swordTriangles')
with open(ROOT+'/Temp/FrozenKnight/blender_output.json','w',encoding='utf-8') as f:json.dump(data,f)
os.makedirs(ROOT+'/ArtSource/FrozenKnight',exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/FrozenKnight/WinterKnight_SwordMotion.blend')
print('Edited 7 actions; off-hand grip errors:',errors)
