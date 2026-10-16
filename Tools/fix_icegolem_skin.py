"""Refine left-arm skinning while preserving the source mesh and animation."""
import bpy,json,heapq,math
from pathlib import Path
from mathutils.kdtree import KDTree
project=Path(r'C:\Users\mbc\Desktop\NC_AI\DogLife')
obj=bpy.data.objects['IceGolem_Body'];rig=obj.find_armature()
folder=project/'ArtSources/IceGolem'
backup=folder/'BeforeSkinRefinement/weights.json'
if not backup.exists():
    backup.write_text(json.dumps([{obj.vertex_groups[g.group].name:g.weight for g in v.groups} for v in obj.data.vertices]))
original=json.loads(backup.read_text())
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def distance(v,name):
    bone=rig.data.bones[name];a=bone.head_local;b=bone.tail_local;axis=b-a
    t=max(0,min(1,(v-a).dot(axis)/axis.length_squared))
    return (v-a-axis*t).length
arm=['LShoulder','LUpperArm','LForearm','LHand']
# Keep the left arm on its adjacent arm joints at every graphics quality level.
fixed=[];changed=0;masks=[]
for v,old in zip(obj.data.vertices,original):
    arm_total=sum(old.get(n,0) for n in arm)
    mask=1.0 if v.co.x>.25 and arm_total>.48 else 0.0
    masks.append(mask)
    if mask:
        fore=(old.get('LForearm',0)+old.get('LHand',0))/arm_total
        new={'LUpperArm':1-fore,'LForearm':fore}
        new=dict(sorted(new.items(),key=lambda p:p[1],reverse=True))
        changed+=1
    else:new=old.copy()
    fixed.append(new)
for g in obj.vertex_groups:g.remove(list(range(len(obj.data.vertices))))
for i,ws in enumerate(fixed):
    for n,w in ws.items():obj.vertex_groups[n].add([i],w,'REPLACE')
# Match Unity's split vertices to unchanged source coordinates.
tree=KDTree(len(obj.data.vertices))
for v in obj.data.vertices:tree.insert(v.co,v.index)
tree.balance()
unity=json.loads((folder/'unity-vertices.json').read_text())
trials=[]
for sx in [-1,1]:
    for sy in [-1,1]:
        trials.append((sum(tree.find((p[0]*100*sx,p[1]*100*sy,p[2]*100))[2] for p in unity['vertices'][::40]),sx,sy))
_,sx,sy=min(trials);rows=[];error=0
for p in unity['vertices']:
    _,i,d=tree.find((p[0]*100*sx,p[1]*100*sy,p[2]*100));error=max(error,d)
    rows.append({'apply':masks[i]>.00001,'indices':[unity['bones'].index(n) for n in fixed[i]],'weights':list(fixed[i].values())})
if error>.0001:raise RuntimeError(('Vertex mismatch',error))
(folder/'corrected-weights.json').write_text(json.dumps(rows))
bpy.data.libraries.write(str(folder/'IceGolem_SkinRefined.blend'),{obj,rig,*[a for a in bpy.data.actions if 'IceGolem' in a.name]},fake_user=True)
print(json.dumps({'changed':changed,'maxMappingError':error,'axisSigns':[sx,sy]}))

