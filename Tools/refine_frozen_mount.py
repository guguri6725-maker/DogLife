"""손으로 말을 짚고 오른다리를 넘기는 탑승 동작. 기존 메시와 뼈 길이를 보존합니다."""
exec(compile(open('C:/Users/mbc/Desktop/NC_AI/DogLife/Tools/refine_frozen_riding.py',encoding='utf-8').read(),'refine_frozen_riding.py','exec'))
seated_source=sample(knight,next(m for m in json.load(open(EX+'riding_output.json'))['motions'] if m['name']=='MountedIdle'),0)

def track(points,t):
 for (a,va),(b,vb) in zip(points,points[1:]):
  if t<=b:return Vector(va).lerp(Vector(vb),ease(a,b,t))
 return Vector(points[-1][1])

def root_offset(t):
 # Unity 탑승 이동과 동일한 안장 기준 좌표입니다.
 lateral=.26*ease(0,.22,t)+.74*ease(.42,.80,t);rise=ease(.28,.84,t)
 return Vector((2.4/5.1*(1-lateral),-3.225/5.1*(1-rise)+.25/5.1*math.sin(math.pi*rise),0))

def mount_pose(t):
 start=sample(knight,original['CinematicMount'],0);p=copy.deepcopy(start)
 seated=seated_source
 sw=worlds(knight,start);bw=worlds(knight,seated);offset=root_offset(t);offset0=root_offset(0)
 # 원본 Root 높이를 골반에 포함했으므로 중복으로 더해지지 않게 합니다.
 p[0][names['Root']]=Vector((0,0,0));p[1][names['Root']]=Quaternion()
 hip=track([(0,tuple(sw[names['Hips']].translation+offset0)),(.24,(.32,-.09,-.04)),(.48,(.26,.15,-.015)),(.68,(.09,.18,.02)),(.9,(0,.012,0)),(1,(0,.012,0))],t)
 p[0][names['Hips']]=hip-offset
 p[1][names['Hips']]=start[1][names['Hips']].slerp(seated[1][names['Hips']],ease(.12,.85,t))
 for n in ['Spine','Spine1','Spine2','Neck','Head','LeftShoulder','RightShoulder']:
  p[1][names[n]]=start[1][names[n]].slerp(seated[1][names[n]],ease(0,.8,t))
 lean=math.sin(math.pi*ease(.06,.94,t))
 p[1][names['Spine']] @= Quaternion((0,0,1),math.radians(12*lean))
 p[1][names['Spine1']] @= Quaternion((1,0,0),math.radians(-10*lean))
 # 왼발로 지탱하고 오른발은 말 뒤쪽 위로 크게 넘겨 반대편에 내립니다.
 for side,sign in [('Left',1),('Right',-1)]:
  initial=sw[names[side+'Foot']].translation+offset0;final=bw[names[side+'Foot']].translation
  points=[(0,tuple(initial)),(.30,(.245,-.30,-.015)),(.72,(.24,-.30,-.025)),(1,tuple(final))] if side=='Left' else [(0,tuple(initial)),(.28,tuple(initial)),(.48,(.35,-.08,.20)),(.63,(.13,.21,.23)),(.78,(-.25,.10,.16)),(.94,tuple(final)),(1,tuple(final))]
  target=track(points,t)-offset
  solve(knight,p,names[side+'UpLeg'],names[side+'Leg'],names[side+'Foot'],target,Vector((sign*.7,0,-1)))
  world_rotation(knight,p,names[side+'Foot'],sw[names[side+'Foot']].to_quaternion().slerp(bw[names[side+'Foot']].to_quaternion(),ease(.05,.94,t)))
 # 안장 앞뒤를 짚은 두 손을 유지하다가 앉은 뒤 승마 대기 자세로 놓습니다.
 for side,sign in [('Left',1),('Right',-1)]:
  initial=sw[names[side+'Hand']].translation+offset0;final=bw[names[side+'Hand']].translation
  grip=(.19,.10,-.15) if side=='Left' else (.20,.08,.10)
  target=track([(0,tuple(initial)),(.22,grip),(.69,grip),(.94,tuple(final)),(1,tuple(final))],t)-offset
  solve(knight,p,names[side+'Arm'],names[side+'ForeArm'],names[side+'Hand'],target,Vector((sign,.15,.3)))
  world_rotation(knight,p,names[side+'Hand'],sw[names[side+'Hand']].to_quaternion().slerp(bw[names[side+'Hand']].to_quaternion(),ease(.05,.94,t)))
 # 마지막 연결은 승마 대기 첫 프레임과 정확히 일치시킵니다.
 blend=ease(.94,1,t)
 for i in range(len(knight['bones'])):
  p[0][i]=p[0][i].lerp(seated[0][i],blend);p[1][i]=p[1][i].slerp(seated[1][i],blend)
 return p

def build_mount():
 output=copy.deepcopy(knight);output['motions']=[]
 for name in ['Mount','CinematicMount']:
  m=dict(name=name,duration=3.6,frames=109,positions=[],rotations=[],scales=[])
  for f in range(m['frames']):
   p=mount_pose(f/108);m['positions'] += [jv(v) for v in p[0]];m['rotations'] += [jq(q) for q in p[1]];m['scales'] += [jv(v) for v in p[2]]
  output['motions'].append(m)
 json.dump(output,open(EX+'mount_grip_output.json','w'),separators=(',',':'))
 return output
