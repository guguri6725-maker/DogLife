"""제공된 기립/발도 동작을 보스 골격에 맞추고 접지를 보정합니다."""
import bpy, json, math, copy
from mathutils import Vector, Quaternion, Matrix
ROOT=r'C:/Users/mbc/Desktop/NC_AI/DogLife'
data=json.load(open(ROOT+'/Temp/FrozenKnight/cinematic_input.json',encoding='utf-8'))
bones=data['bones']; n=len(bones); ids={b['name']:i for i,b in enumerate(bones)}
def v(d):return Vector((d['x'],d['y'],d['z']))
def q(d):return Quaternion((d['w'],d['x'],d['y'],d['z']))
def vd(x):return dict(zip(('x','y','z'),x))
def qd(x):return dict(x=x.x,y=x.y,z=x.z,w=x.w)
def fk(ps,qs):
 wp=[];wq=[]
 for i,b in enumerate(bones):
  p=b['parent'];wp.append(ps[i] if p<0 else wp[p]+wq[p]@ps[i]);wq.append(qs[i] if p<0 else wq[p]@qs[i])
 return wp,wq
def pose(m,f):return [v(x) for x in m['positions'][f*n:(f+1)*n]],[q(x) for x in m['rotations'][f*n:(f+1)*n]]
def write(m,f,ps,qs):
 m['positions'][f*n:(f+1)*n]=[vd(x) for x in ps];m['rotations'][f*n:(f+1)*n]=[qd(x.normalized()) for x in qs]
motions={m['name']:m for m in data['motions']};idle=motions['Idle'];ip,iq=pose(idle,0);iw,ir=fk(ip,iq)
feet=[ids['LeftFoot'],ids['RightFoot']];anchor=(iw[feet[0]]+iw[feet[1]])*.5
rise=copy.deepcopy(motions['SitUp']);rise['name']='ThroneRise'
for f in range(rise['frames']):
 ps,qs=pose(rise,f);wp,wq=fk(ps,qs);foot=(wp[feet[0]]+wp[feet[1]])*.5
 # 원본의 떠오르는 루트 이동을 제거해 양발이 같은 지면에 남도록 합니다.
 ps[0]+=anchor-foot
 write(rise,f,ps,qs)
sit=copy.deepcopy(rise);sit['name']='ThroneIdle';sit['duration']=3; sit['frames']=91
sp,sq=pose(rise,0);sit['positions']=[vd(x) for _ in range(91) for x in sp];sit['rotations']=[qd(x) for _ in range(91) for x in sq];sit['scales']=[vd(Vector((1,1,1))) for _ in range(91*n)]
for f in range(91):
 ps,qs=pose(sit,f);qs[ids['Spine1']]=qs[ids['Spine1']]@Quaternion((1,0,0),math.sin(f/90*math.tau)*.008);write(sit,f,ps,qs)
out=[sit,rise]
# FBX의 실제 팔/몸통 동작을 뼈 길이는 보존하면서 새 골격으로 옮깁니다.
reflect=Matrix.Diagonal(Vector((1,1,-1)))
restp=[v(b['p']) for b in bones];restq=[q(b['q']) for b in bones];rw,rq=fk(restp,restq)
children={i:next((j for j,b in enumerate(bones) if b['parent']==i),None) for i in range(n)}
for num,filename in [(1,'칼 뒤에서 뽑기1.fbx'),(2,'칼 뒤에서 뽑기 2.fbx')]:
 src=bpy.data.objects.get('Source_Draw'+str(num))
 if not src:
  before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath='C:/Users/mbc/Downloads/'+filename)
  src=next(o for o in bpy.data.objects if o not in before and o.type=='ARMATURE');src.name='Source_Draw'+str(num)
 first,last=src.animation_data.action.frame_range;frames=int(last-first)+1
 m=dict(name='ThroneDraw'+str(num),duration=(frames-1)/30,frames=frames,positions=[],rotations=[],scales=[])
 for f in range(frames):
  bpy.context.scene.frame_set(int(first)+f);ps=[x.copy() for x in ip];qs=[x.copy() for x in iq]
  for i,b in enumerate(bones):
   sb=src.pose.bones.get('mixamorig:'+b['name'])
   if not sb:continue
   delta=reflect@(sb.matrix.to_3x3()@sb.bone.matrix_local.to_3x3().inverted())@reflect
   desired=delta.to_quaternion()@rq[i]
   child=children[i]
   sc=src.pose.bones.get('mixamorig:'+bones[child]['name']) if child is not None else None
   if sc:
    direction=reflect@(sc.head-sb.head)
    current=desired@(rq[i].inverted()@(rw[child]-rw[i]))
    if current.length>.001 and direction.length>.001:desired=current.rotation_difference(direction)@desired
   wp,wq=fk(ps,qs);p=b['parent'];qs[i]=wq[p].inverted()@desired if p>=0 else desired
  wp,wq=fk(ps,qs);ps[0]+=anchor-(wp[feet[0]]+wp[feet[1]])*.5
  m['positions'] += [vd(x) for x in ps];m['rotations'] += [qd(x.normalized()) for x in qs];m['scales'] += [vd(Vector((1,1,1))) for _ in ps]
 out.append(m);src.hide_render=True;src.hide_set(True)
# 기존 탑승 동작의 시작/끝 자세는 보존하고 실제 길이만 여유 있게 사용합니다.
mount=copy.deepcopy(motions['Mount']);mount['name']='CinematicMount';mount['duration']=2.8;out.append(mount)
data['motions']=out
for key in ('skin','sword','swordTriangles'):data.pop(key,None)
json.dump(data,open(ROOT+'/Temp/FrozenKnight/cinematic_output.json','w',encoding='utf-8'))
# Blender에서도 동일한 결과를 재생하고 수정할 수 있도록 액션을 보관합니다.
rig=bpy.data.objects['FrozenKnight_Rig'];arm=rig.data
for m in out:
 action=bpy.data.actions.new(m['name']+'_Cinematic');action.use_fake_user=True;rig.animation_data.action=action
 for f in range(m['frames']):
  ps,qs=pose(m,f);wp,wq=fk(ps,qs);matrices=[Matrix.Translation(wp[i])@wq[i].to_matrix().to_4x4() for i in range(n)]
  for i,b in enumerate(bones):
   pb=rig.pose.bones.get(b['name'])
   if not pb:continue
   pb.rotation_mode='QUATERNION';rest=arm.bones[b['name']].matrix_local;p=b['parent'];basis=rest.inverted()@matrices[i]
   if p>=0:basis=rest.inverted()@arm.bones[bones[p]['name']].matrix_local@matrices[p].inverted()@matrices[i]
   pb.matrix_basis=basis;frame=1+f*m['duration']*30/(m['frames']-1)
   for prop in ('location','rotation_quaternion','scale'):pb.keyframe_insert(prop,frame=frame)
rig.animation_data.action=bpy.data.actions.get('ThroneRise_Cinematic');bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/FrozenKnight/WinterKnight_Cinematics.blend')
print('Created',[(m['name'],m['duration']) for m in out])
