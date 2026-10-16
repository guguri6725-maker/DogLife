"""Conservative part-level sculpt, preserving topology and exact shared boundaries."""
import bpy, json
import numpy as np
from pathlib import Path
ROOT=Path(r'C:\Users\mbc\Desktop\NC_AI\DogLife')
FOLDER=ROOT/'ArtSources/SpringBoar'
data=json.loads((FOLDER/'part-refinement-input.json').read_text())
p=np.asarray(data['vertices'],float);normal=np.asarray(data['normals'],float)
faces=np.asarray(data['triangles'],int).reshape(-1,3)[:,[0,2,1]]
collection=bpy.data.collections['ForestBoar_PartRefinement']
# Weld only coincident positions in the calculation; exported vertex/UV identities stay separate.
_,first,inverse=np.unique(np.round(p,7),axis=0,return_index=True,return_inverse=True)
q=p[first].copy();n=normal[first].copy();f=inverse[faces]
edges=np.concatenate([f[:,[0,1]],f[:,[1,2]],f[:,[2,0]]]);edges=np.unique(np.sort(edges,axis=1),axis=0)
a=np.concatenate([edges[:,0],edges[:,1]]);b=np.concatenate([edges[:,1],edges[:,0]])
degree=np.bincount(a,minlength=len(q)).clip(1)
def mean_neighbours(values):
    total=np.zeros_like(values);np.add.at(total,a,values[b]);return total/degree.reshape((-1,)+(1,)*(values.ndim-1))
regions=['01_Eyes_Lids','02_Tusks','03_Leaves','04_Roots_Bark','07_Head_Snout','08_Torso_Hips','Leg']
mask=np.zeros((len(q),len(regions)))
for ob in collection.objects:
    ids=np.empty(len(ob.data.vertices),dtype=np.int32);ob.data.attributes['unity_vertex_id'].data.foreach_get('value',ids)
    region=next((i for i,name in enumerate(regions[:-1]) if ob.name.startswith(name)),6)
    mask[inverse[ids],region]=1
mask/=mask.sum(axis=1).clip(1)[:,None]
for _ in range(5):mask=mask*.55+mean_neighbours(mask)*.45

# Remove small melted lumps on eyes/tusks and lightly fair anatomy, without changing proportions.
fair=q.copy()
strength=mask@np.array([.28,.42,.02,.08,.12,.045,.075])
for _ in range(5):
    lap=mean_neighbours(fair)-fair
    fair+=lap*strength[:,None]
    # Reverse part of the shrinkage so the silhouette and mass are retained.
    fair-=(mean_neighbours(fair)-fair)*strength[:,None]*.45
delta=fair-q
length=np.linalg.norm(delta,axis=1);delta*=np.minimum(1,.0018/np.maximum(length,1e-12))[:,None]
# Give existing vine and leaf surfaces a subtle raised profile, with a soft shared boundary.
lift=mask[:,3]*.00135+mask[:,2]*.00065
delta+=n*lift[:,None]
result=q+delta
# Use the original normals plus a conservative correction from the sculpted surface.
def surface_normals(points):
    face_normal=np.cross(points[f[:,1]]-points[f[:,0]],points[f[:,2]]-points[f[:,0]])
    sums=np.zeros_like(points)
    for j in range(3):np.add.at(sums,f[:,j],face_normal)
    return sums/np.maximum(np.linalg.norm(sums,axis=1)[:,None],1e-12)
before_n=surface_normals(q);after_n=surface_normals(result)
new_normals=normal+(after_n-before_n)[inverse]
new_normals/=np.maximum(np.linalg.norm(new_normals,axis=1)[:,None],1e-12)
new_positions=p+delta[inverse]
for ob in collection.objects:
    ids=np.empty(len(ob.data.vertices),dtype=np.int32);ob.data.attributes['unity_vertex_id'].data.foreach_get('value',ids)
    ob.data.vertices.foreach_set('co',new_positions[ids].ravel());ob.data.update()
    ob['refinement']='Local fairing, shallow relief, welded shared boundaries; original UV retained'

# Reassemble using the original face list, rather than joining and changing vertex ordering.
mesh=bpy.data.meshes.new('ForestBoar_Refined_Assembled')
mesh.from_pydata(new_positions.tolist(),[],faces.tolist());mesh.update()
uv=np.asarray(data['uv']);layer=mesh.uv_layers.new(name='UVMap')
layer.data.foreach_set('uv',uv[np.asarray([l.vertex_index for l in mesh.loops])].ravel())
mesh.materials.append(next(iter(collection.objects)).data.materials[0])
for poly in mesh.polygons:poly.use_smooth=True
assembled=bpy.data.objects.new('ForestBoar_Refined_Assembled',mesh);bpy.context.scene.collection.objects.link(assembled)
for ob in collection.objects:ob.hide_render=True;ob.hide_set(True)
# Restore the exact corrected vertex groups for the editable Blender deliverable.
names=['Root','Hips','Spine','Chest','Neck','Head','Jaw','LFrontUpper','LFrontLower','LFrontFoot','RFrontUpper','RFrontLower','RFrontFoot','Tail','LRearUpper','LRearLower','LRearFoot','RRearUpper','RRearLower','RRearFoot']
for name in names:assembled.vertex_groups.new(name=name)
for i,row in enumerate(data['weights']):
    for bone,weight in zip(row['indices'],row['weights']):
        if weight>0:assembled.vertex_groups[bone].add([i],weight,'REPLACE')
unity_positions=new_positions*np.array([-1,1,1])/100
unity_normals=new_normals*np.array([-1,1,1])
(FOLDER/'part-refinement-output.json').write_text(json.dumps({'vertices':unity_positions.tolist(),'normals':unity_normals.tolist()}))
bpy.data.libraries.write(str(FOLDER/'ForestBoar_Refined_Parts.blend'),set(collection.objects)|{assembled},fake_user=True)
print(json.dumps({'vertices':len(p),'triangles':len(faces),'maxMovementModelUnits':float(np.linalg.norm(delta,axis=1).max()),'parts':len(collection.objects)}))
