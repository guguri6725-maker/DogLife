"""Separate sleeve, coat and leg influences without changing topology or animation."""
import bpy, json, math
from pathlib import Path
from collections import Counter
from mathutils.kdtree import KDTree

project = Path(r'C:\Users\mbc\Desktop\NC_AI\DogLife')
obj = bpy.data.objects['Scarecrow_Body']
rig = obj.find_armature()
backup = project / 'ArtSources/Scarecrow/BeforeSkinRefinement/weights.json'
if not backup.exists():
    backup.write_text(json.dumps([{obj.vertex_groups[g.group].name:g.weight for g in v.groups} for v in obj.data.vertices]))
original = json.loads(backup.read_text())
groups = {g.name:g for g in obj.vertex_groups}
body = {'Hips','Spine','Spine1','Spine2','Neck'}
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)))
    return t*t*(3-2*t)
fixed=[]; changed=0
# Classify connected pieces together so fingers and cloth strips cannot cross limb masks.
parent=list(range(len(obj.data.vertices)))
def root(i):
    while parent[i]!=i:
        parent[i]=parent[parent[i]]; i=parent[i]
    return i
for edge in obj.data.edges:
    a,b=map(root,edge.vertices); parent[a]=b
pieces={}
for v in obj.data.vertices:pieces.setdefault(root(v.index),[]).append(v.index)
regions={}
for ids in pieces.values():
    vs=[obj.data.vertices[i] for i in ids]
    cx=sum(v.co.x for v in vs)/len(vs)
    cz=sum(v.co.z for v in vs)/len(vs)
    top=max(v.co.z for v in vs)
    if cz>2.02: region='head'
    elif top<.30 or (top<.73 and abs(cx)<.46): region='leg'
    elif abs(cx)>.47: region='arm'
    else: region='body'
    for i in ids:regions[i]=(region,'Left' if cx>=0 else 'Right')
for v, old in zip(obj.data.vertices,original):
    x,y,z=v.co; region,side=regions[v.index]
    arm={side+n for n in ('Shoulder','Arm','ForeArm','Hand')}
    leg={side+n for n in ('UpLeg','Leg','Foot')}
    # Hat/head must never be dragged by the shoulders.
    if region=='head':
        new={'Head':1.0}
    elif region=='arm':
        # Keep the authored elbow gradient, but remove all leg/coat influences.
        new={n:w for n,w in old.items() if n in arm}
        if not new:
            new={side+('Arm' if z>1.34 else 'ForeArm' if z>.92 else 'Hand'):1}
        # All fingers follow the hand even when the procedural attack extends it.
        if z<1.05:
            new={side+'Hand':1}
        elif z<1.30:
            amount=smooth(1.05,1.30,z)
            new={side+'Hand':1-amount,side+'ForeArm':amount}
    elif region=='body':
        # The hanging tabard stays attached to the pelvis/chest, not the knees.
        new={n:w for n,w in old.items() if n in body}
        if z<1.3:
            amount=smooth(1.05,1.45,z)
            new={'Hips':1-amount,'Spine':amount}
        if not new:new={'Spine2':1}
    else:
        new={n:w for n,w in old.items() if n in leg}
        if not new:new={side+('Leg' if z>.25 else 'Foot'):1}
        # Small knee transition avoids a hard seam at the hem.
        hip=smooth(.60,.72,z)
        total=sum(new.values())
        new={n:w/total*(1-hip) for n,w in new.items()}
        if hip:new['Hips']=hip
    new={n:w for n,w in new.items() if w>1e-6}
    new=dict(sorted(new.items(),key=lambda p:p[1],reverse=True)[:4])
    total=sum(new.values());new={n:w/total for n,w in new.items()}
    changed+=sum(abs(old.get(n,0)-new.get(n,0)) for n in old.keys()|new.keys())>.001
    fixed.append(new)
for g in obj.vertex_groups:g.remove(list(range(len(obj.data.vertices))))
for i,weights in enumerate(fixed):
    for name,weight in weights.items():groups[name].add([i],weight,'REPLACE')

# Unity splits additional normal/UV vertices. Match original positions, never array indices.
tree=KDTree(len(obj.data.vertices))
for v in obj.data.vertices:tree.insert(v.co,v.index)
tree.balance()
unity=json.loads((project/'ArtSources/Scarecrow/unity-vertices.json').read_text())
trials=[]
for sx in (-1,1):
    for sy in (-1,1):
        ds=[tree.find((p[0]*100*sx,p[1]*100*sy,p[2]*100))[2] for p in unity['vertices'][::40]]
        trials.append((sum(ds)/len(ds),sx,sy))
error,sx,sy=min(trials)
rows=[];max_error=0
for p in unity['vertices']:
    _,index,distance=tree.find((p[0]*100*sx,p[1]*100*sy,p[2]*100))
    max_error=max(max_error,distance)
    rows.append({'indices':[unity['bones'].index(n) for n in fixed[index]],'weights':list(fixed[index].values())})
if max_error>.0001:raise RuntimeError(('Vertex correspondence mismatch',max_error,trials))
(project/'ArtSources/Scarecrow/corrected-weights.json').write_text(json.dumps(rows))
bpy.ops.wm.save_as_mainfile(filepath=str(project/'ArtSources/Scarecrow/Scarecrow_SkinRefined.blend'))
print(json.dumps({'changed':changed,'vertices':len(fixed),'unityVertices':len(rows),'maxMappingError':max_error,'axisSigns':[sx,sy]}))

