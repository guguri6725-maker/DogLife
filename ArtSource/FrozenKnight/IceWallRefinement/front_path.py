"""Keep the existing overhead sword cast, with both arms travelling in front."""
import bpy,csv,json,math
from mathutils import Vector,Quaternion,Matrix
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/FrozenKnight'
W=ROOT+'/IceWallRefinement'
bones=json.load(open(ROOT+'/PushUpperReplacement/rig.json'))
ids={b['name']:i for i,b in enumerate(bones)}
frames={}
for row in csv.reader(open(W+'/current_motion.csv')):
 t=float(row[0]);v=list(map(float,row[2:]));frames.setdefault(t,{})[row[1]]=v
times=sorted(frames)
def fk(local):
 world=[]
 for i,b in enumerate(bones):
  p,q,s=local[i];m=Matrix.LocRotScale(p,q,s);par=b['parent']
  world.append(world[par]@m if par>=0 else m)
 return world
def read(t):
 out=[]
 for b in bones:
  v=frames[t].get(b['path'])
  out.append((Vector(v[:3]),Quaternion((v[6],*v[3:6])).normalized(),Vector(v[7:])) if v else (Vector(b['p']),Quaternion(b['q']),Vector(b['s'])))
 return out
locals={t:read(t) for t in times};worlds={t:fk(p) for t,p in locals.items()}
chest=ids['Spine2'];start=worlds[times[0]]
front=worlds[min(times,key=lambda t:abs(t-1.25))]
raised=worlds[min(times,key=lambda t:abs(t-3.25))]
def smooth(u):u=max(0,min(1,u));return u*u*(3-2*u)
def pose_anchor(w,i):return w[i].translation-w[chest].translation,w[i].to_quaternion()
anchors={}
for side in ['Left','Right']:
 i=ids[side+'Hand'];vals=[pose_anchor(w,i) for w in [start,front,raised]]
 # Keep the overhead height and blade attitude, moving only its rearward offset forward.
 # The imported knight faces local -Z, opposite its Animator transform.forward.
 # Keep the middle and raised poses at a similar chest-front depth, avoiding retraction.
 for p,q in vals:p.z=min(-.14,p.z)
 vals[1][0].z=-.16;vals[2][0].z=-.16
 anchors[side]=vals
def target_at(side,t):
 a,b,c=anchors[side]
 if t<=1.25:x,y,u=a,b,smooth(t/1.25)
 elif t<=3.0:x,y,u=b,c,smooth((t-1.25)/1.75)
 elif t<=4.85:x,y,u=c,c,0
 else:x,y,u=c,a,smooth((t-4.85)/(times[-1]-4.85))
 return x[0].lerp(y[0],u),x[1].slerp(y[1],u)
def solve(a,target,l1,l2,pole):
 delta=target-a;axis=delta.normalized();d=max(abs(l1-l2)+.00001,min(delta.length,(l1+l2)*.995))
 bend=pole-axis*pole.dot(axis)
 if bend.length<.0001:bend=Vector((0,0,1))-axis*axis.z
 bend.normalize();along=(l1*l1-l2*l2+d*d)/(2*d)
 return a+axis*along+bend*math.sqrt(max(0,l1*l1-along*along)),a+axis*d
output=[];minimum=999;max_step=0;previous={}
transport={ids[side+n]:start[ids[side+n]].to_quaternion() for side in ['Left','Right'] for n in ['Arm','ForeArm']}
for t in times:
 local=[(p.copy(),q.copy(),s.copy()) for p,q,s in locals[t]];world=fk(local)
 for side in ['Left','Right']:
  ai,ei,hi=[ids[side+n] for n in ['Arm','ForeArm','Hand']]
  shoulder=world[ai].translation;hand_offset,hq=target_at(side,t)
  target=world[chest].translation+hand_offset
  target.z=min(target.z,shoulder.z-.12)
  l1=(start[ei].translation-start[ai].translation).length;l2=(start[hi].translation-start[ei].translation).length
  pole=Vector((.6 if side=='Left' else -.6,-.18,-1))
  elbow,hand=solve(shoulder,target,l1,l2,pole)
  for i,j,direction in [(ai,ei,elbow-shoulder),(ei,hi,hand-elbow)]:
   reference=start[j].translation-start[i].translation
   # Transport the previous orientation to avoid a 180-degree roll near vertical arms.
   previous_axis=transport[i]@start[i].to_quaternion().inverted()@reference
   q=previous_axis.rotation_difference(direction)@transport[i]
   if t>4.85:
    rest_aligned=reference.rotation_difference(direction)@start[i].to_quaternion()
    q=q.slerp(rest_aligned,smooth((t-4.85)/(times[-1]-4.85)))
   transport[i]=q.copy()
   parent=bones[i]['parent'];p,old,s=local[i];local[i]=(p,world[parent].to_quaternion().inverted()@q,s);world=fk(local)
  parent=bones[hi]['parent'];p,old,s=local[hi];local[hi]=(p,world[parent].to_quaternion().inverted()@hq,s);world=fk(local)
 # Match the true idle at both boundaries so entering/leaving the clip cannot tug the arms.
 edge=smooth(min(t/.35,(times[-1]-t)/.4))
 for side in ['Left','Right']:
  for part in ['Arm','ForeArm','Hand']:
   i=ids[side+part];p,q,s=local[i];local[i]=(p,locals[times[0]][i][1].slerp(q,edge),s)
 world=fk(local)
 for side in ['Left','Right']:
  shoulder=world[ids[side+'Arm']].translation
  for part in ['ForeArm','Hand']:minimum=min(minimum,shoulder.z-world[ids[side+part]].translation.z)
 for i,b in enumerate(bones):
  if b['path'] not in frames[t]:continue
  p,q,s=local[i]
  if i in previous:
   angle=math.degrees(previous[i].rotation_difference(q).angle);max_step=max(max_step,min(angle,360-angle))
  previous[i]=q.copy();output.append([t,b['path'],*p,q.x,q.y,q.z,q.w,*s])
with open(W+'/front_motion.csv','w',newline='') as f:csv.writer(f).writerows(output)
report={'duration':times[-1],'minimum_hand_or_elbow_forward_offset_model_units':minimum,'maximum_local_rotation_step_degrees':max_step,'cast_moment_unchanged':True}
json.dump(report,open(W+'/front_path_report.json','w'),indent=2);print(report)
