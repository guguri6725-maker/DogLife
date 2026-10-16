"""Polish the existing rise and author a continuous, planted back-sword draw."""
import json,math,copy
from mathutils import Vector,Quaternion,Matrix
W='C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/FrozenKnight/IntroRefinement'
data=json.load(open(W+'/input.json',encoding='utf-8'))
bones=data['bones'];N=len(bones);ids={b['name']:i for i,b in enumerate(bones)}
def v(x):return Vector((x['x'],x['y'],x['z']))
def q(x):return Quaternion((x['w'],x['x'],x['y'],x['z'])).normalized()
def vd(x):return dict(zip('xyz',x))
def qd(x):return dict(x=x.x,y=x.y,z=x.z,w=x.w)
def smooth(t):t=max(0,min(1,t));return t*t*t*(10+t*(-15+6*t))
def pose(m,f):return [v(x) for x in m['positions'][f*N:(f+1)*N]],[q(x) for x in m['rotations'][f*N:(f+1)*N]]
def mix(a,b,t):return [x.lerp(y,t) for x,y in zip(a[0],b[0])],[x.slerp(y,t) for x,y in zip(a[1],b[1])]
def sample(m,u):
 x=max(0,min(1,u))*(m['frames']-1);f=int(x);return mix(pose(m,f),pose(m,min(f+1,m['frames']-1)),x-f)
def fk(p,r):
 wp=[];wr=[]
 for i,b in enumerate(bones):
  j=b['parent'];wp.append(p[i] if j<0 else wp[j]+wr[j]@p[i]);wr.append(r[i] if j<0 else wr[j]@r[i])
 return wp,wr
motions={m['name']:m for m in data['motions']}
idle=pose(motions['Idle'],0);rise=motions['ThroneRise'];stand=copy.deepcopy(idle)
for side in ['Left','Right']:
 for part in ['Shoulder','Arm','ForeArm','Hand']:
  i=ids[side+part];stand[1][i]=pose(rise,rise['frames']-1)[1][i].copy()
sw,sq=fk(*stand);iw,iq=fk(*idle)
axis=(v(data['tip'])-v(data['grip'])).normalized()
# Actor faces local -Z. The handle sits above the right shoulder and the blade hangs diagonally behind.
socket=Vector((-.135,.13,.145))
blade_back=Vector((.81,-.585,.015)).normalized()
rh=ids['RightHand'];lh=ids['LeftHand'];ch=ids['Spine2']
socket_local=sq[ch].inverted()@socket
ref=sample(motions['ThroneDraw1'],.5);rw,rq=fk(*ref)
back_q=Quaternion((.30722463,.66545945,.66196251,-.15678762)).normalized()
def set_world(r,i,target,wr):r[i]=wr[bones[i]['parent']].inverted()@target
transport={}
def ik(p,r,side,target,pole,hand_q=None,leg=False):
 names=['UpLeg','Leg','Foot'] if leg else ['Arm','ForeArm','Hand']
 ai,ei,hi=[ids[side+s] for s in names];wp,wr=fk(p,r);a=wp[ai]
 l1=p[ei].length;l2=p[hi].length;delta=target-a;dist=max(abs(l1-l2)+.00001,min(delta.length,(l1+l2)*.998))
 direction=delta.normalized();bend=pole-direction*pole.dot(direction)
 if bend.length<.0001:bend=Vector((1,0,0))-direction*direction.x
 bend.normalize();along=(l1*l1-l2*l2+dist*dist)/(2*dist)
 elbow=a+direction*along+bend*math.sqrt(max(0,l1*l1-along*along));goal=a+direction*dist
 if side=='Right' and not leg:
  # Preserve the upper-arm path, remove accumulated roll from the elbow joint.
  base=transport.get(ai,wr[ai]);upper=(base@p[ei]).rotation_difference(elbow-a)@base
  set_world(r,ai,upper,wr);transport[ai]=upper.copy();wp,wr=fk(p,r)
  lower_ref=upper@q(bones[ei]['q'])
  lower_q=(lower_ref@p[hi]).rotation_difference(goal-wp[ei])@lower_ref
  set_world(r,ei,lower_q,wr)
 else:
  for i,j,dest in [(ai,ei,elbow),(ei,hi,goal)]:
   wp,wr=fk(p,r)
   base=wr[i] if leg else transport.get(i,wr[i])
   turn=(base@p[j]).rotation_difference(dest-wp[i]);result=turn@base
   set_world(r,i,result,wr)
   if not leg:transport[i]=result.copy()
 if hand_q is not None:
  wp,wr=fk(p,r)
  if side=='Right' and not leg and name=='ThroneDraw2':
   # Keep the blade trajectory, choosing the wrist roll closest to a neutral grip.
   neutral=wr[ei]@q(bones[hi]['q']);desired=hand_q@axis
   hand_q=(neutral@axis).rotation_difference(desired)@neutral
  set_world(r,hi,hand_q,wr)
 return (goal-target).length
def feet(p,r,targets,rotations):
 for side in ['Left','Right']:ik(p,r,side,targets[side],Vector((0,0,-1)),rotations[side],True)
def motion(name,duration,poses):
 return dict(name=name,duration=duration,frames=len(poses),positions=[vd(x) for p,r in poses for x in p],rotations=[qd(x.normalized()) for p,r in poses for x in r],scales=[b['s'] for _ in poses for b in bones])
# Filter small retargeting vibration while retaining the original body-weight shift.
def smooth_source(m,u):
 center=sample(m,u);poses=[sample(m,u+dt/m['duration']) for dt in [-.12,-.09,-.06,-.03,.03,.06,.09,.12]]
 out=center
 for j,other in enumerate(poses):out=mix(out,other,1/(j+2))
 return out
out=[];rp0,rq0=fk(*pose(rise,0));foot_start={s:rp0[ids[s+'Foot']] for s in ['Left','Right']};foot_end={s:iw[ids[s+'Foot']] for s in ['Left','Right']}
for name,duration in [('ThroneRise',4.6)]:
 poses=[]
 for f in range(round(duration*60)+1):
  t=f/60;u=t/duration
  # Slow preparation, confident lift, then controlled settling.
  u0=.2*u+.8*smooth(u)
  p,r=smooth_source(rise,u0)
  end=smooth((u-.8)/.2);p,r=mix((p,r),stand,end)
  start=smooth(t/.18);p,r=mix(pose(rise,0),(p,r),start)
  # Fix each ankle rather than averaging them, preserving the silhouette and bone lengths.
  goals={s:foot_start[s].lerp(foot_end[s],smooth(u)) for s in ['Left','Right']}
  rots={s:rq0[ids[s+'Foot']].slerp(iq[ids[s+'Foot']],smooth(u)) for s in ['Left','Right']}
  feet(p,r,goals,rots);p,r=mix(pose(rise,0),(p,r),smooth(t/.4));p,r=mix((p,r),stand,end);poses.append((p,r))
 poses[0]=pose(rise,0);poses[-1]=copy.deepcopy(stand);out.append(motion(name,duration,poses))
# A calm unarmed stand avoids briefly assuming the armed guard before the reach.
out.append(motion('ThroneStand',1.2,[copy.deepcopy(stand) for _ in range(73)]))
def path(keys,u):
 # Cubic Hermite path keeps velocity continuous across the overhead extraction arc.
 k=next((i for i in range(len(keys)-1) if u<=keys[i+1][0]),len(keys)-2)
 t0,a=keys[k];t1,b=keys[k+1];dt=t1-t0;x=max(0,min(1,(u-t0)/dt))
 def tangent(j):
  if j==0 or j==len(keys)-1:return Vector((0,0,0))
  return (keys[j+1][1]-keys[j-1][1])/(keys[j+1][0]-keys[j-1][0])
 return (2*x**3-3*x*x+1)*a+(x**3-2*x*x+x)*dt*tangent(k)+(-2*x**3+3*x*x)*b+(x**3-x*x)*dt*tangent(k+1)
def qr(keys,u):
 k=next((i for i in range(len(keys)-1) if u<=keys[i+1][0]),len(keys)-2)
 return keys[k][1].slerp(keys[k+1][1],smooth((u-keys[k][0])/(keys[k+1][0]-keys[k][0])))
def orient(ref,desired):return (ref@axis).rotation_difference(desired.normalized())@ref
max_reach=0;max_reach_at=None;grip_pose=None
for name,duration in [('ThroneDraw1',1.65),('ThroneDraw2',3.2)]:
 poses=[];transport={} if name=='ThroneDraw1' else {i:x.copy() for i,x in enumerate(fk(*grip_pose)[1])}
 for f in range(round(duration*60)+1):
  t=f/60;u=t/duration;p,r=copy.deepcopy(stand)
  anticipation=math.sin(math.pi*.5*smooth(u)) if name=='ThroneDraw1' else 1-smooth(u)
  # Share the reach with the torso and shoulders instead of folding only the wrist.
  r[ids['Spine1']]=r[ids['Spine1']]@Quaternion((0,1,0),math.radians(-6)*anticipation)
  r[ch]=r[ch]@Quaternion((1,0,0),math.radians(-3)*anticipation)
  wp,wr=fk(p,r)
  # Raise the clavicle with the reach so the upper-arm armor is not driven into the chest.
  si=ids['RightShoulder'];ai=ids['RightArm'];lift=smooth(u/.7) if name=='ThroneDraw1' else 1-smooth((u-.6)/.4)
  ray=wp[ai]-wp[si];desired_ray=ray+Vector((0,.015,0))*lift
  set_world(r,si,ray.rotation_difference(desired_ray)@wr[si],wr);wp,wr=fk(p,r)
  if name=='ThroneDraw1':
   # The reachable handle is fixed to the chest, including its animated lean and turn.
   contact=wr[ch]@socket_local
   keys=[(0,sw[rh]-sw[ch]),(.18,Vector((-.23,-.18,-.025))),(.50,Vector((-.27,.12,.025))),(.76,contact)]
   offset=path(keys,u);wrist=qr([(0,sq[rh]),(.25,sq[rh]),(.78,back_q),(1,back_q)],u)
   left=(sw[lh]-sw[ch]).lerp(Vector((.2,-.17,-.10)),smooth(u))
   lq=sq[lh];blend=smooth(t/.28)
  else:
   # First extract along the blade, then turn it over the outer shoulder into the guard.
   contact=grip_world[rh]-grip_world[ch]
   extract=contact-(back_q@axis)*.11
   keys=[(0,contact),(.22,extract),(.46,Vector((-.215,.255,.14))),(.69,Vector((-.21,.23,-.13))),(1,iw[rh]-iw[ch])]
   offset=path(keys,u)
   overhead=orient(back_q,Vector((-.15,.86,.48)))
   outer=orient(overhead,Vector((-.3,.85,-.44)))
   wrist=qr([(0,back_q),(.22,back_q),(.50,overhead),(.70,outer),(1,iq[rh])],u)
   left=Vector((.2,-.17,-.10)).lerp(iw[lh]-iw[ch],smooth((u-.35)/.65));lq=sq[lh].slerp(iq[lh],smooth((u-.35)/.65));blend=1
  error=ik(p,r,'Right',wp[ch]+offset,Vector((-.7,-.35,-1)),wrist)
  if error>max_reach:max_reach=error;max_reach_at=(name,t)
  wp,wr=fk(p,r);ik(p,r,'Left',wp[ch]+left,Vector((1,-.15,-.3)),lq)
  if name=='ThroneDraw1':p,r=mix(stand,(p,r),blend)
  # Both ankles remain in place throughout the draw.
  feet(p,r,foot_end,{s:iq[ids[s+'Foot']] for s in ['Left','Right']})
  if name=='ThroneDraw2':
   if u<.04:p,r=mix(grip_pose,(p,r),smooth(u/.04))
   if u>.68:p,r=mix((p,r),idle,smooth((u-.68)/.32))
  poses.append((p,r))
 if name=='ThroneDraw1':
  # Choose a neutral grip at contact, then approach it without a wrist roll flip.
  gp,grot=fk(*poses[-1]);neutral=grot[ids['RightForeArm']]@q(bones[rh]['q'])
  contact_blade=(grot[ch]@sq[ch].inverted())@blade_back
  back_q=(neutral@axis).rotation_difference(contact_blade)@neutral
  socket_rotation=grot[ch].inverted()@back_q
  for j,(p,r) in enumerate(poses):
   u=j/(len(poses)-1);wp,wr=fk(p,r)
   wrist=sq[rh].slerp(wr[ch]@socket_rotation,smooth((u-.15)/.63));set_world(r,rh,wrist,wr)
  poses[0]=copy.deepcopy(stand);grip_pose=copy.deepcopy(poses[-1]);grip_world=fk(*grip_pose)[0]
 else:poses[0]=copy.deepcopy(grip_pose);poses[-1]=copy.deepcopy(idle)
 out.append(motion(name,duration,poses))
# Continuous quaternion signs avoid interpolation spins.
for m in out:
 for b in range(N):
  prev=None
  for f in range(m['frames']):
   i=f*N+b;rot=q(m['rotations'][i])
   if prev is not None and prev.dot(rot)<0:rot.negate()
   m['rotations'][i]=qd(rot);prev=rot
gp,gq=fk(*grip_pose)
report={'sheathedHandQuaternion':qd(gq[rh]),'socketLocalPosition':vd(socket_local),'socketLocalRotation':qd(socket_rotation),'motions':{},'max_unreachable_hand_model_units':max_reach,'maxReachAt':max_reach_at,'sheathBladeDirectionBoss':[-blade_back.x,blade_back.y,-blade_back.z],'gripNormalizedTime':.78}
for m in out:
 max_step=0;max_foot=0;max_bone=0;prev=None
 for f in range(m['frames']):
  p,r=pose(m,f);wp,wr=fk(p,r)
  if prev:
   for a,b in zip(prev,r):
    ang=math.degrees(a.rotation_difference(b).angle);max_step=max(max_step,min(ang,360-ang))
  if m['name'].startswith('ThroneDraw'):
   max_foot=max(max_foot,*[(wp[ids[s+'Foot']]-foot_end[s]).length for s in ['Left','Right']])
  for i,b in enumerate(bones):
   if b['parent']>=0:max_bone=max(max_bone,abs(p[i].length-v(b['p']).length))
  prev=r
 report['motions'][m['name']]={'duration':m['duration'],'maxRotationStep60fps':max_step,'maxFootDriftWorld':max_foot*5.1,'maxBoneLengthChange':max_bone}
data['motions']=out
for key in ['sword','swordTriangles']:data.pop(key,None)
json.dump(data,open(W+'/output.json','w'),separators=(',',':'))
json.dump(report,open(W+'/report.json','w'),indent=2)
print(json.dumps(report,indent=2))
