import bpy, bmesh, json, math, pathlib
from mathutils import Vector, Quaternion
from mathutils.kdtree import KDTree

base=pathlib.Path('Assets/Art/Enemies/BlossomSkeleton');base.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(pathlib.Path('Library/BlossomSkeleton/source.blend').resolve()))
for name in ['Cube','Camera','Light']:
    if name in bpy.data.objects:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH');rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
components=json.loads(pathlib.Path('Library/BlossomSkeleton/components.json').read_text())
tree=KDTree(len(components[0]['vertices']))
for i in components[0]['vertices']:tree.insert(obj.data.vertices[i].co,i)
tree.balance();remove=[];report=[]
for c in components[1:]:
    distance=min(tree.find(obj.data.vertices[i].co)[2] for i in c['vertices'])
    # 몸체와 분리된 바닥 꽃잎 및 공중 꽃잎만 제거합니다.
    discard=c['max'][2]<.05 or distance>.007
    if discard:remove.extend(c['vertices'])
    report.append({'component':c['id'],'gap':distance,'removed':discard,'vertices':c['count']})
bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm,geom=[bm.verts[i] for i in remove],context='VERTS');bm.to_mesh(obj.data);bm.free();obj.data.update()
# 원본 자동 리깅이 칼날을 다리에 연결한 부분을 손의 강체 가중치로 보정합니다.
hand=obj.vertex_groups['RightHand']
sword=[]
for v in obj.data.vertices:
    x,y,z=v.co
    if x<-.19 and y<-.025 and .095<z<.55:sword.append(v.index)
for group in obj.vertex_groups:group.remove(sword)
hand.add(sword,1.0,'REPLACE')
pathlib.Path('Library/BlossomSkeleton/cleanup.json').write_text(json.dumps(report,indent=2))
for image in bpy.data.images:
    if image.packed_file:
        image.filepath_raw=str((base/(image.name+'.png')).resolve());image.file_format='PNG';image.save()

scene=bpy.context.scene;scene.render.fps=60
rest={b.name:b.matrix_local.to_quaternion() for b in rig.data.bones}
def pose(name,x=0,y=0,z=0):
    q=Quaternion(Vector((0,0,1)),math.radians(z)) @ Quaternion(Vector((0,1,0)),math.radians(y)) @ Quaternion(Vector((1,0,0)),math.radians(x))
    rig.pose.bones[name].rotation_quaternion=rest[name].inverted() @ q @ rest[name]
def reset():
    for b in rig.pose.bones:b.rotation_mode='QUATERNION';b.rotation_quaternion=Quaternion();b.location=(0,0,0);b.scale=(1,1,1)
    pose('LeftUpLeg',y=18);pose('RightUpLeg',y=-15)
def shift(name,v):rig.pose.bones[name].location=rest[name].inverted() @ Vector(v)
def ease(t):return t*t*(3-2*t)
def key(frame):
    for b in rig.pose.bones:
        b.keyframe_insert('rotation_quaternion',frame=frame,group=b.name);b.keyframe_insert('location',frame=frame,group=b.name)
for name,duration in [('Idle',2.4),('Walk',1.0),('Attack',1.05)]:
    rig.animation_data_create();action=bpy.data.actions.new(name);rig.animation_data.action=action
    frames=round(duration*60)
    for f in range(frames+1):
        reset();t=f/60;cycle=2*math.pi*t/duration
        if name=='Idle':
            pose('Spine1',x=1.5*math.sin(cycle));pose('Head',z=1.5*math.sin(cycle+.3))
            pose('LeftArm',x=2*math.sin(cycle));pose('RightArm',x=1.2*math.sin(cycle))
        elif name=='Walk':
            for side,phase,adduct in [('Left',0,18),('Right',math.pi,-15)]:
                s=math.sin(cycle+phase);knee=24*max(0,-s)
                pose(side+'UpLeg',x=24*s,y=adduct);pose(side+'Leg',x=knee)
                pose(side+'Foot',x=-18*s-knee*.7)
                pose(side+'Arm',x=-s*(13 if side=='Left' else 7))
                pose(side+'ForeArm',x=-5)
            pose('Hips',z=3*math.sin(cycle));pose('Spine1',x=3,z=-4*math.sin(cycle))
            shift('Root',(0,0,.007*(1-math.cos(cycle*2))))
        else:
            # 0–0.40 준비, 0.40–0.47 유지, 0.47–0.63 단일 베기, 이후 복귀/후딜.
            if t<.4:w=ease(t/.4);s=0
            elif t<.47:w=1;s=0
            elif t<.63:w=1;s=ease((t-.47)/.16)
            else:w=1-ease(min(1,(t-.63)/.42));s=1
            # 뒤쪽 머리 위에서 전방으로 이어지는 한 방향의 큰 베기입니다.
            arm_angle=(155+155*s)*w if t<.63 else 310+50*(1-w)
            pose('RightArm',x=arm_angle,y=-8*w,z=(-10+25*s)*w)
            pose('RightForeArm',x=(15-10*s)*w);pose('RightHand',x=(15-35*s)*w)
            pose('Spine1',x=(-8+20*s)*w,z=(-22+44*s)*w)
            pose('Spine2',z=(-10+20*s)*w);pose('Head',z=(10-20*s)*w)
            pose('LeftArm',x=-20*w,y=12*w,z=15*w)
            pose('LeftForeArm',x=-20*w);shift('Root',(0,-.025*s*w,-.012*w))
        key(f+1)
    action.use_fake_user=True
    for curve in action.fcurves:
        for k in curve.keyframe_points:k.interpolation='LINEAR'
rig.animation_data.action=None;reset()
# Export only the imported model and its existing armature.
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(pathlib.Path('Library/BlossomSkeleton/animated.blend').resolve()))
bpy.ops.export_scene.fbx(filepath=str((base/'BlossomSkeleton.fbx').resolve()),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
print('BUILD_DONE removed vertices='+str(len(remove)))
