"""Separate editable regions without changing Unity vertex identities, UVs or skin."""
import bpy, json, math
import numpy as np
from pathlib import Path
from mathutils import Vector

ROOT=Path(r'C:\Users\mbc\Desktop\NC_AI\DogLife')
FOLDER=ROOT/'ArtSources/SpringBoar'
data=json.loads((FOLDER/'part-refinement-input.json').read_text())
positions=np.asarray(data['vertices'],dtype=float)
normals=np.asarray(data['normals'],dtype=float)
uv=np.asarray(data['uv'],dtype=float)
faces=np.asarray(data['triangles'],dtype=int).reshape(-1,3)[:,[0,2,1]]
image=bpy.data.images.load(str(ROOT/'Assets/Art/SpringBoar/Phase1/Textures/SpringBoarPhase1_BaseColor.png'),check_existing=True)
pixels=np.empty(len(image.pixels),dtype=np.float32);image.pixels.foreach_get(pixels)
pixels=pixels.reshape(image.size[1],image.size[0],4)
samples=pixels[(uv[:,1]*(image.size[1]-1)).astype(int).clip(0,image.size[1]-1),(uv[:,0]*(image.size[0]-1)).astype(int).clip(0,image.size[0]-1),:3]
centres=positions[faces].mean(axis=1);colours=samples[faces].mean(axis=1)
groups={}
eye_centres=[np.array([.057455,-.409757,.414399]),np.array([-.073567,-.407568,.413275])]
for fi,(p,c) in enumerate(zip(centres,colours)):
    x,y,z=p;r,g,b=c;ax=abs(x)
    if min(np.linalg.norm(p-e) for e in eye_centres)<.031:region='01_Eyes_Lids'
    elif y<-.26 and z<.45 and ax>.065 and min(c)>.38 and r>b*.99:region='02_Tusks'
    elif g>r*1.035 and g>b*1.18:region='03_Leaves'
    elif r>b*2.4 and r>g*1.5 and max(c)<.60:region='04_Roots_Bark'
    elif z<.315 and ax>.105:region=('05_FrontLeg_' if y<0 else '06_RearLeg_')+('L' if x>0 else 'R')
    elif y<-.255:region='07_Head_Snout'
    else:region='08_Torso_Hips'
    groups.setdefault(region,[]).append(fi)

collection=bpy.data.collections.new('ForestBoar_PartRefinement');bpy.context.scene.collection.children.link(collection)
original=bpy.data.objects.get('SpringBoarPhase1_Body')
material=original.data.materials[0]
for ob in list(bpy.context.scene.objects):
    if ob.type=='MESH':ob.hide_render=True;ob.hide_set(True)
for name,face_ids in groups.items():
    selected=faces[face_ids];ids=np.unique(selected);remap=np.full(len(positions),-1,dtype=int);remap[ids]=np.arange(len(ids))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(positions[ids].tolist(),[],remap[selected].tolist());mesh.update()
    attr=mesh.attributes.new('unity_vertex_id','INT','POINT');attr.data.foreach_set('value',ids.tolist())
    layer=mesh.uv_layers.new(name='UVMap')
    layer.data.foreach_set('uv',uv[ids][np.asarray([l.vertex_index for l in mesh.loops])].ravel().tolist())
    mesh.materials.append(material)
    for poly in mesh.polygons:poly.use_smooth=True
    ob=bpy.data.objects.new(name,mesh);collection.objects.link(ob)
    ob['source_vertex_count']=len(ids);ob['source_face_count']=len(face_ids)
print(json.dumps({name:len(ids) for name,ids in groups.items()}))
bpy.data.libraries.write(str(FOLDER/'ForestBoar_Parts_Before.blend'),set(collection.objects),fake_user=True)
