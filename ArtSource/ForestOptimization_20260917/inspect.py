import bpy, json
from pathlib import Path
p=Path('C:/unity/DogLife/ArtSource/ForestOptimization_20260917')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(p/'Originals/Models/F3D_01_AncientTree.fbx'))
for o in bpy.context.scene.objects:
 print('INSPECT',o.name,o.type,list(o.scale),list(o.rotation_euler),list(o.location))
 if o.type=='MESH': print('MESH',o.data.name,len(o.data.vertices),len(o.data.polygons),'UV',len(o.data.uv_layers),'COLOR',len(o.data.color_attributes),'MAT',[m.name for m in o.data.materials])
import bmesh, numpy as np
from mathutils.bvhtree import BVHTree
me=o.data; me.calc_loop_triangles()
vs=[v.co for v in me.vertices];ts=[tuple(t.vertices) for t in me.loop_triangles];used=set(i for t in ts for i in t)
bvh=BVHTree.FromPolygons(vs,ts,all_triangles=True)
ds=[bvh.find_nearest(vs[i])[3] for i in used]
print('SELF',len(vs),len(used),np.percentile(ds,[50,95,100]),'area',np.percentile([t.area for t in me.loop_triangles],[0,1,10,50,100]))
for tol in [1e-7,1e-6,1e-5,.0001]:
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=tol)
 todo=set(bm.verts);counts=[]
 while todo:
  v=todo.pop();stack=[v];com={v}
  while stack:
   v=stack.pop()
   for e in v.link_edges:
    w=e.other_vert(v)
    if w in todo:todo.remove(w);com.add(w);stack.append(w)
  counts.append(len({f for v in com for f in v.link_faces}))
 print('COMP',tol,len(bm.verts),len(counts),sorted(counts,reverse=True)[:12]);bm.free()
