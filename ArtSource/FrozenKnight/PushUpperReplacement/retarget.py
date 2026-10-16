"""Retarget the supplied shockwave to the existing knight without changing its rig."""
import bpy,json,csv,math
from mathutils import Vector,Quaternion,Matrix
W='C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/FrozenKnight/PushUpperReplacement'
bones=json.load(open(W+'/rig.json'));idle=json.load(open(W+'/idle.json'))
src=bpy.data.objects['KnightPush_Source'];ids={b['name']:i for i,b in enumerate(bones)}
rp=[Vector(b['p']) for b in bones];rq=[Quaternion(b['q']) for b in bones]
ip=[p.copy() for p in rp];iq=[q.copy() for q in rq];sc=[Vector(b['s']) for b in bones]
for row in idle:
 i=next((i for i,b in enumerate(bones) if b['path']==row['path']),None)
 if i is None:continue
 prop=row['property'];axis=prop[-1]
 if 'Position' in prop:ip[i]['xyz'.index(axis)]=row['value']
 elif 'Rotation' in prop:iq[i]['wxyz'.index(axis)]=row['value']
 elif 'Scale' in prop:sc[i]['xyz'.index(axis)]=row['value']
def fk(ps,qs):
 wp=[];wq=[]
 for i,b in enumerate(bones):
  p=b['parent'];wp.append(ps[i] if p<0 else wp[p]+wq[p]@ps[i]);wq.append(qs[i] if p<0 else wq[p]@qs[i])
 return wp,wq
rw,rwq=fk(rp,rq);iw,iwq=fk(ip,iq);feet=[ids['LeftFoot'],ids['RightFoot']]
reflect=Matrix.Diagonal((1,1,-1));first,last=src.animation_data.action.frame_range
fps=bpy.context.scene.render.fps/bpy.context.scene.render.fps_base;duration=(last-first)/fps
rows=[]
for frame in range(int(first),int(last)+1):
 bpy.context.scene.frame_set(frame);ps=[p.copy() for p in ip];qs=[q.copy() for q in iq]
 for i,b in enumerate(bones):
  sb=src.pose.bones.get('mixamorig:'+b['name'])
  if not sb:continue
  delta=reflect@sb.matrix.to_3x3()@sb.bone.matrix_local.to_3x3().inverted()@reflect
  desired=delta.to_quaternion()@rwq[i]
  child=next((j for j,x in enumerate(bones) if x['parent']==i and src.pose.bones.get('mixamorig:'+x['name'])),None)
  if child is not None:
   target=reflect@(src.pose.bones['mixamorig:'+bones[child]['name']].head-sb.head)
   current=desired@(rwq[i].inverted()@(rw[child]-rw[i]))
   if target.length>.0001 and current.length>.0001:desired=current.rotation_difference(target)@desired
  wp,wq=fk(ps,qs);parent=b['parent'];qs[i]=wq[parent].inverted()@desired if parent>=0 else desired
 # Ground the feet and ease the clip into/out of the existing idle pose.
 wp,wq=fk(ps,qs);ps[ids['Root']].y+=min(iw[i].y for i in feet)-min(wp[i].y for i in feet)
 t=(frame-first)/fps;blend=min(1,t/.20,(duration-t)/.25);blend=max(0,blend);blend=blend*blend*(3-2*blend)
 for i,b in enumerate(bones):
  p=ip[i].lerp(ps[i],blend);q=iq[i].slerp(qs[i],blend)
  rows.append([t,b['path'],*p,q.x,q.y,q.z,q.w,*sc[i]])
with open(W+'/motion.csv','w',newline='') as f:csv.writer(f).writerows(rows)
print('Retargeted',len(rows),'samples; duration',duration,'seconds at',fps,'fps')
