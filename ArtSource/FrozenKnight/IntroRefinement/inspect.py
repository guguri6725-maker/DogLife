import json,math
from mathutils import Vector,Quaternion
d=json.load(open('C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/FrozenKnight/IntroRefinement/input.json'))
b=d['bones'];n=len(b);ids={x['name']:i for i,x in enumerate(b)}
def v(x):return Vector((x['x'],x['y'],x['z']))
def q(x):return Quaternion((x['w'],x['x'],x['y'],x['z']))
def fk(m,f):
 ps=list(map(v,m['positions'][f*n:(f+1)*n]));qs=list(map(q,m['rotations'][f*n:(f+1)*n]));wp=[];wq=[]
 for i,x in enumerate(b):
  j=x['parent'];wp.append(ps[i] if j<0 else wp[j]+wq[j]@ps[i]);wq.append(qs[i] if j<0 else wq[j]@qs[i])
 return wp,wq
axis=(v(d['tip'])-v(d['grip'])).normalized()
for m in d['motions']:
 print(m['name'])
 for f in [0,(m['frames']-1)//2,m['frames']-1]:
  p,r=fk(m,f);print(f, {s:tuple(round(z,3) for z in p[ids[s]]) for s in ['Root','Spine2','RightHand','LeftHand','RightFoot','LeftFoot']},'blade',tuple(round(z,3) for z in r[ids['RightHand']]@axis))
