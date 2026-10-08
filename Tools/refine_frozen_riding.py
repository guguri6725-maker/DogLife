"""기존 메시/바인드 뼈대를 유지하며 승마 자세를 Blender에서 검토하고 클립으로 굽습니다."""
import bpy, json, math, copy, os
from mathutils import Vector, Quaternion, Matrix

BASE='C:/Users/mbc/Desktop/NC_AI/DogLife'
EX=BASE+'/Temp/FrozenKnight/'
knight=json.load(open(EX+'riding_input.json',encoding='utf-8'))
horse=json.load(open(EX+'horse_riding_input.json',encoding='utf-8'))
C=Matrix(((1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)))
def vec(v): return Vector((v['x'],v['y'],v['z']))
def quat(q): return Quaternion((q['w'],q['x'],q['y'],q['z'])).normalized()
def jv(v): return dict(zip('xyz',v))
def jq(q): return dict(x=q.x,y=q.y,z=q.z,w=q.w)
def smooth(t): return max(0,min(1,t))**2*(3-2*max(0,min(1,t)))
def ease(a,b,t): return smooth((t-a)/(b-a))
def sample(data,m,f):
 n=len(data['bones']);k=f*n
 return [[vec(v) for v in m['positions'][k:k+n]], [quat(q) for q in m['rotations'][k:k+n]], [vec(v) for v in m['scales'][k:k+n]]]
def rest(data):return [[vec(b['p']) for b in data['bones']],[quat(b['q']) for b in data['bones']],[vec(b['s']) for b in data['bones']]]
def worlds(data,pose):
 p,q,s=pose;out=[]
 for i,b in enumerate(data['bones']):
  m=Matrix.LocRotScale(p[i],q[i],s[i]);out.append(out[b['parent']]@m if b['parent']>=0 else m)
 return out
def world_rotation(data,pose,index,q):
 parent=data['bones'][index]['parent'];w=worlds(data,pose)
 pose[1][index]=(w[parent].to_quaternion().inverted()@q).normalized() if parent>=0 else q.normalized()
def solve(data,pose,upper,lower,end,target,bend):
 w=worlds(data,pose);a0=w[upper].translation;b0=w[lower].translation;c0=w[end].translation
 a=(b0-a0).length;b=(c0-b0).length;axis=(target-a0).normalized();d=min(a+b-.00001,max(abs(a-b)+.00001,(target-a0).length))
 along=(a*a-b*b+d*d)/(2*d);plane=(bend-axis*axis.dot(bend)).normalized()
 knee=a0+axis*along+plane*math.sqrt(max(0,a*a-along*along))
 world_rotation(data,pose,upper,(b0-a0).rotation_difference(knee-a0)@w[upper].to_quaternion())
 w=worlds(data,pose);world_rotation(data,pose,lower,(w[end].translation-w[lower].translation).rotation_difference(a0+axis*d-w[lower].translation)@w[lower].to_quaternion())

names={b['name']:i for i,b in enumerate(knight['bones'])}
original={m['name']:m for m in knight['motions']}
horse_motions={m['name']:m for m in horse['motions']}
bind=rest(knight);bind_world=worlds(knight,bind)
base=sample(knight,original['MountedIdle'],0)
# 골반을 기준으로 다리 길이를 보존하며 무릎을 말 바깥으로 벌립니다.
base[0][names['Root']]=Vector((0,0,0))
base[1][names['Root']]=Quaternion()
base[0][names['Hips']]=Vector((0,.012,0))
base[1][names['Hips']]=Quaternion()
for name,deg in [('Spine',-5),('Spine1',-2),('Spine2',0),('Neck',4),('Head',3)]:
 base[1][names[name]]=bind[1][names[name]]@Quaternion((1,0,0),math.radians(deg))
for side,sign in [('Left',1),('Right',-1)]:
 solve(knight,base,names[side+'UpLeg'],names[side+'Leg'],names[side+'Foot'],Vector((sign*.228,-.300,-.025)),Vector((sign*.62,0,-1)))
 foot=names[side+'Foot'];toe=names[side+'ToeBase'];q=bind_world[foot].to_quaternion()
 q=(bind_world[toe].translation-bind_world[foot].translation).rotation_difference(Vector((sign*.012,-.008,-.11)))@q
 world_rotation(knight,base,foot,q)
 # 양손은 목 앞쪽에서 편안히 모으고 팔꿈치는 바깥으로 살짝 벌립니다.
 solve(knight,base,names[side+'Arm'],names[side+'ForeArm'],names[side+'Hand'],Vector((sign*.125,.235,-.235)),Vector((sign,0,.25)))

def refined(name,source,f):
 old=sample(knight,source,f);t=f/(source['frames']-1);phase=math.tau*t
 p=copy.deepcopy(old)
 mounting=name in ('Mount','CinematicMount');weight=ease(.40,.96,t) if mounting else 1
 # 시전/사망의 상체 연기는 보존하고 하체만 같은 승마 기준으로 맞춥니다.
 for n in ['Root','Hips','LeftUpLeg','LeftLeg','LeftFoot','LeftToeBase','RightUpLeg','RightLeg','RightFoot','RightToeBase']:
  i=names[n];p[1][i]=old[1][i].slerp(base[1][i],weight);p[0][i]=old[0][i].lerp(base[0][i],weight)
 if name in ('MountedReady','MountedStompReady','MountedStompImpact','MountedGroggy','MountedCommand'):
  for n in ['Spine','Spine1','Spine2','Neck','Head']:
   p[1][names[n]]=base[1][names[n]].copy()
  lean=-18 if name=='MountedGroggy' else -6*math.sin(math.pi*t) if name=='MountedStompImpact' else -6
  p[1][names['Spine']] @= Quaternion((1,0,0),math.radians(lean))
  if name in ('MountedReady','MountedStompReady','MountedStompImpact'):
   for n in ['LeftShoulder','LeftArm','LeftForeArm','LeftHand','RightShoulder','RightArm','RightForeArm','RightHand']:
    p[1][names[n]]=base[1][names[n]].copy()
 if name in ('MountedIdle','MountedWalk','MountedCharge','MountedBackDash'):
  for n in ['Spine','Spine1','Spine2','Neck','Head','LeftShoulder','LeftArm','LeftForeArm','LeftHand','RightShoulder','RightArm','RightForeArm','RightHand']:
   p[1][names[n]]=base[1][names[n]].copy()
  amplitude={'MountedIdle':.2,'MountedWalk':1.0,'MountedCharge':2.0,'MountedBackDash':1.2}[name]
  # 골반은 접촉을 유지하고 흉곽/머리로 갈수록 반동을 흡수합니다.
  pitch=amplitude*math.sin(phase-.25);roll=amplitude*.4*math.sin(phase)
  p[1][names['Hips']] @= Quaternion((0,0,1),math.radians(roll))
  for n,factor,lag in [('Spine',1,.2),('Spine1',-.55,.35),('Spine2',-.25,.5),('Neck',-.14,.6),('Head',-.06,.7)]:
   lean=-9 if name=='MountedCharge' and n=='Spine' else 0
   p[1][names[n]] @= Quaternion((1,0,0),math.radians(lean+factor*amplitude*math.sin(phase-lag)))
  for side,sign in [('Left',1),('Right',-1)]:
   p[1][names[side+'Shoulder']] @= Quaternion((1,0,0),math.radians(amplitude*.3*math.sin(phase-.45)))
   p[1][names[side+'ForeArm']] @= Quaternion((0,0,1),math.radians(sign*amplitude*.6*math.sin(phase-.5)))
  # 몸통 반동이 있어도 발 위치가 말 안쪽으로 밀리지 않도록 다시 풉니다.
  for side,sign in [('Left',1),('Right',-1)]:
   target=Vector((sign*.228,-.300,-.025));target.y+=.002*amplitude*math.sin(phase-.25)
   solve(knight,p,names[side+'UpLeg'],names[side+'Leg'],names[side+'Foot'],target,Vector((sign*.62,0,-1)))
   world_rotation(knight,p,names[side+'Foot'],worlds(knight,base)[names[side+'Foot']].to_quaternion())
 # 안장이 기울어질 때 골반은 따라가고 흉곽과 머리는 반대 방향으로 조금씩 받아냅니다.
 gait={'MountedIdle':'Idle','MountedWalk':'Walk','MountedCharge':'Gallop','MountedStompReady':'Rear','MountedReady':'Rear','MountedStompImpact':'Stomp','MountedGroggy':'Groggy','MountedBackDash':'BackDash'}.get(name)
 if gait:
  hm=horse_motions[gait];hi=round(t*(hm['frames']-1));hw=worlds(horse,sample(horse,hm,hi));seat=next(i for i,b in enumerate(horse['bones']) if b['name']=='MountSeat')
  yaw=Quaternion((0,1,0),math.pi);counter=(yaw.inverted()@hw[seat].to_quaternion()@yaw).inverted()
  for n,factor in [('Spine',.45),('Spine1',.25),('Spine2',.15),('Neck',.10),('Head',.05)]:
   p[1][names[n]] @= Quaternion().slerp(counter,factor)
  if name in ('MountedWalk','MountedCharge'):
   height0=worlds(horse,sample(horse,hm,0))[seat].translation.y
   absorb=(hw[seat].translation.y-height0)*3.6/3.4
   p[0][names['Spine']].y-=.30*absorb
   p[0][names['Spine1']].y-=.15*absorb
 if mounting:
  for n in ['Spine','Spine1','Spine2','Neck','Head','LeftShoulder','LeftArm','LeftForeArm','LeftHand','RightShoulder','RightArm','RightForeArm','RightHand']:
   p[1][names[n]]=old[1][names[n]].slerp(base[1][names[n]],ease(.75,1,t))
 return p

def build_output():
 output=copy.deepcopy(knight);walk=copy.deepcopy(original['MountedIdle']);walk['name']='MountedWalk';walk['duration']=horse_motions['Walk']['duration'];output['motions'].append(walk)
 for m in output['motions']:
  source=original.get(m['name'],original['MountedIdle'])
  if m['name']=='MountedCharge':m['duration']=horse_motions['Gallop']['duration']
  if m['name']=='MountedIdle':m['duration']=horse_motions['Idle']['duration']
  for f in range(m['frames']):
   pose=refined(m['name'],source,f);a=f*len(knight['bones']);b=a+len(knight['bones'])
   # 그로기 반복 끝은 시작 자세로 부드럽게 돌아와 경계에서 튀지 않습니다.
   if m['name']=='MountedGroggy':
    first=refined(m['name'],source,0);blend=ease(.75,1,f/(m['frames']-1))
    for i in range(len(knight['bones'])):
     pose[0][i]=pose[0][i].lerp(first[0][i],blend);pose[1][i]=pose[1][i].slerp(first[1][i],blend)
   m['positions'][a:b]=[jv(v) for v in pose[0]];m['rotations'][a:b]=[jq(q) for q in pose[1]];m['scales'][a:b]=[jv(v) for v in pose[2]]
 json.dump(output,open(EX+'riding_output.json','w'),separators=(',',':'));return output

def make_rig(data,label):
 pose=rest(data);w=worlds(data,pose)
 arm=bpy.data.armatures.new(label);rig=bpy.data.objects.new(label,arm);bpy.context.collection.objects.link(rig)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
 for i,b in enumerate(data['bones']):
  bone=arm.edit_bones.new(b['name']);m=C@w[i]@C.inverted();bone.matrix=Matrix.LocRotScale(m.translation,m.to_quaternion(),Vector((1,1,1)));bone.length=.04
  if b['parent']>=0:bone.parent=arm.edit_bones[data['bones'][b['parent']]['name']]
 bpy.ops.object.mode_set(mode='OBJECT')
 mesh=bpy.data.meshes.new(label+'Mesh');vs=[C@vec(v) for v in data['skin']['vertices']];tri=data['skin']['triangles'];mesh.from_pydata(vs,[],[tri[i:i+3] for i in range(0,len(tri),3)]);mesh.update()
 obj=bpy.data.objects.new(label+'Body',mesh);bpy.context.collection.objects.link(obj);obj.parent=rig
 for b in data['bones']:obj.vertex_groups.new(name=b['name'])
 for i in range(len(vs)):
  for j in range(4):
   k=i*4+j;idx=data['skin']['indices'][k];weight=data['skin']['weights'][k]
   if idx>=0 and weight>0:obj.vertex_groups[idx].add([i],weight,'REPLACE')
 mod=obj.modifiers.new('Original skin weights','ARMATURE');mod.object=rig
 return rig,obj

def show_pose(rig,data,pose):
 w=worlds(data,pose)
 desired=[]
 for i,b in enumerate(data['bones']):
  m=C@w[i]@C.inverted();desired.append(Matrix.LocRotScale(m.translation,m.to_quaternion(),Vector((1,1,1))))
 for i,b in enumerate(data['bones']):
  pb=rig.pose.bones[b['name']];parent=b['parent']
  args=dict(parent_matrix=desired[parent],parent_matrix_local=pb.parent.bone.matrix_local) if parent>=0 else {}
  pb.matrix_basis=pb.bone.convert_local_to_pose(desired[i],pb.bone.matrix_local,invert=True,**args)
 bpy.context.view_layer.update()
 return w

def preview():
 global kr,hr,kmesh,hmesh,outdata
 for o in bpy.context.scene.objects:o.hide_set(True)
 outdata=build_output();kr,kmesh=make_rig(knight,'RidingKnightReview');hr,hmesh=make_rig(horse,'RidingHorseReview')
 kr.scale=(3.4,)*3;kr.rotation_euler.z=math.pi;hr.scale=(3.6,)*3
 show_pose(hr,horse,sample(horse,horse_motions['Idle'],0));show_pose(kr,knight,refined('MountedIdle',original['MountedIdle'],0))
 seat=next(i for i,b in enumerate(horse['bones']) if b['name']=='MountSeat');kr.location=hr.matrix_world@(C@worlds(horse,sample(horse,horse_motions['Idle'],0))[seat].translation)
 for obj,color in [(kmesh,(.25,.38,.5,1)),(hmesh,(.32,.5,.57,1))]:
  mat=bpy.data.materials.new(obj.name+'Preview');mat.diffuse_color=color;obj.data.materials.append(mat)
 print('Preview ready; bone topology and weights unchanged')

if __name__=='__main__':preview()
