"""제공된 사망 FBX의 상체 연기를 기마 하체에 결합합니다."""
import bpy,json,math
from mathutils import Vector,Quaternion,Matrix
ROOT=r'C:/Users/mbc/Desktop/NC_AI/DogLife'
# 기존 왕좌 제작 파일의 좌표 변환/골격 함수만 재사용합니다.
source=open(ROOT+'/ArtSource/FrozenKnight/author_cinematics.py',encoding='utf-8').read()
exec(compile(source[:source.index('for num,filename')],'cinematic_helpers','exec'))
src=bpy.data.objects.get('Source_Death')
if not src:
 before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath='C:/Users/mbc/Downloads/Two Handed Sword Death.fbx')
 src=next(o for o in bpy.data.objects if o not in before and o.type=='ARMATURE');src.name='Source_Death'
mp,mq=pose(motions['MountedIdle'],0)
first,last=src.animation_data.action.frame_range;frames=int(last-first)+1
m=dict(name='MountedDeath',duration=(frames-1)/30,frames=frames,positions=[],rotations=[],scales=[])
for f in range(frames):
 bpy.context.scene.frame_set(int(first)+f);ps=[x.copy() for x in mp];qs=[x.copy() for x in mq]
 for i,b in enumerate(bones):
  if '/Spine' not in b['path']:continue
  sb=src.pose.bones.get('mixamorig:'+b['name'])
  if not sb:continue
  delta=reflect@(sb.matrix.to_3x3()@sb.bone.matrix_local.to_3x3().inverted())@reflect
  desired=delta.to_quaternion()@rq[i];child=children[i]
  sc=src.pose.bones.get('mixamorig:'+bones[child]['name']) if child is not None else None
  if sc:
   direction=reflect@(sc.head-sb.head);current=desired@(rq[i].inverted()@(rw[child]-rw[i]))
   if current.length>.001 and direction.length>.001:desired=current.rotation_difference(direction)@desired
  wp,wq=fk(ps,qs);p=b['parent'];qs[i]=wq[p].inverted()@desired
  # 안장 위에서는 지면에 눕는 만큼 허리를 꺾지 않습니다.
  if b['name']=='Spine':
   angle=mq[i].rotation_difference(qs[i]).angle
   if angle>math.radians(65):qs[i]=mq[i].slerp(qs[i],math.radians(65)/angle)
  blend=min(1,f/12);blend=blend*blend*(3-2*blend);qs[i]=mq[i].slerp(qs[i],blend)
 m['positions'] += [vd(x) for x in ps];m['rotations'] += [qd(x.normalized()) for x in qs];m['scales'] += [vd(Vector((1,1,1))) for _ in ps]
data['motions']=[m]
for key in ('skin','sword','swordTriangles'):data.pop(key,None)
json.dump(data,open(ROOT+'/Temp/FrozenKnight/cinematic_output.json','w',encoding='utf-8'))
rig=bpy.data.objects['FrozenKnight_Rig'];arm=rig.data
action=bpy.data.actions.new('MountedDeath_Cinematic');action.use_fake_user=True;rig.animation_data.action=action
for f in range(frames):
 ps,qs=pose(m,f);wp,wq=fk(ps,qs);matrices=[Matrix.Translation(wp[i])@wq[i].to_matrix().to_4x4() for i in range(n)]
 for i,b in enumerate(bones):
  pb=rig.pose.bones.get(b['name'])
  if not pb:continue
  pb.rotation_mode='QUATERNION';rest=arm.bones[b['name']].matrix_local;p=b['parent'];basis=rest.inverted()@matrices[i]
  if p>=0:basis=rest.inverted()@arm.bones[bones[p]['name']].matrix_local@matrices[p].inverted()@matrices[i]
  pb.matrix_basis=basis
  for prop in ('location','rotation_quaternion','scale'):pb.keyframe_insert(prop,frame=f+1)
src.hide_render=True;src.hide_set(True);bpy.data.objects['Greatsword'].hide_render=True
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=frames;bpy.context.scene.frame_set(120)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/FrozenKnight/WinterKnight_Death.blend')
print('MountedDeath authored from supplied FBX:',m['duration'],'seconds')
