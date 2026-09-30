"""제공된 맨손 보행을 보스 골격에 옮기고 지면 높이를 보정합니다."""
import bpy,json,math
from mathutils import Vector,Quaternion,Matrix
ROOT=r'C:/Users/mbc/Desktop/NC_AI/DogLife'
source=open(ROOT+'/ArtSource/FrozenKnight/author_cinematics.py',encoding='utf-8').read()
exec(compile(source[:source.index('for num,filename')],'cinematic_helpers','exec'))
src=bpy.data.objects['Source_UnarmedWalk']
first,last=src.animation_data.action.frame_range;frames=int(last-first)+1
m=dict(name='UnarmedWalk',duration=(frames-1)/30,frames=frames,positions=[],rotations=[],scales=[])
for f in range(frames):
 bpy.context.scene.frame_set(int(first)+f);ps=[x.copy() for x in ip];qs=[x.copy() for x in iq]
 for i,b in enumerate(bones):
  sb=src.pose.bones.get('mixamorig:'+b['name'])
  if not sb:continue
  delta=reflect@(sb.matrix.to_3x3()@sb.bone.matrix_local.to_3x3().inverted())@reflect
  desired=delta.to_quaternion()@rq[i];child=children[i]
  sc=src.pose.bones.get('mixamorig:'+bones[child]['name']) if child is not None else None
  if sc:
   direction=reflect@(sc.head-sb.head);current=desired@(rq[i].inverted()@(rw[child]-rw[i]))
   if current.length>.001 and direction.length>.001:desired=current.rotation_difference(direction)@desired
  wp,wq=fk(ps,qs);parent=b['parent'];qs[i]=wq[parent].inverted()@desired if parent>=0 else desired
 wp,wq=fk(ps,qs)
 ps[0].y+=min(iw[i].y for i in feet)-min(wp[i].y for i in feet)
 m['positions'] += [vd(x) for x in ps];m['rotations'] += [qd(x.normalized()) for x in qs];m['scales'] += [vd(Vector((1,1,1))) for _ in ps]
data['motions']=[m]
for key in ('skin','sword','swordTriangles'):data.pop(key,None)
json.dump(data,open(ROOT+'/Temp/FrozenKnight/cinematic_output.json','w',encoding='utf-8'))
rig=bpy.data.objects['FrozenKnight_Rig'];arm=rig.data
act=bpy.data.actions.new('UnarmedWalk_Retargeted');act.use_fake_user=True;rig.animation_data.action=act
for f in range(frames):
 ps,qs=pose(m,f);wp,wq=fk(ps,qs);matrices=[Matrix.Translation(wp[i])@wq[i].to_matrix().to_4x4() for i in range(n)]
 for i,b in enumerate(bones):
  pb=rig.pose.bones.get(b['name'])
  if not pb:continue
  pb.rotation_mode='QUATERNION';rest=arm.bones[b['name']].matrix_local;parent=b['parent'];basis=rest.inverted()@matrices[i]
  if parent>=0:basis=rest.inverted()@arm.bones[bones[parent]['name']].matrix_local@matrices[parent].inverted()@matrices[i]
  pb.matrix_basis=basis
  for prop in ('location','rotation_quaternion','scale'):pb.keyframe_insert(prop,frame=f+1)
src.hide_render=True;src.hide_set(True)
bpy.data.objects['Greatsword'].hide_render=True
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=frames;bpy.context.scene.frame_set(20)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/FrozenKnight/WinterKnight_UnarmedWalk.blend')
print('Retargeted UnarmedWalk',m['duration'])
