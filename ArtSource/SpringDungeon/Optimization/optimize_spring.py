import bpy,bmesh,os,json,math
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
base=r'C:\Users\mbc\Desktop\NC_AI\DogLife'
out=os.path.join(base,'Assets','MapDesigne','Spring')
archive=os.path.join(base,'ArtSource','SpringDungeon','Optimization')
for folder in ['Models','Textures','Materials','Prefabs']:os.makedirs(os.path.join(out,folder),exist_ok=True)
s=bpy.data.scenes['Spring_Optimization'];bpy.context.window.scene=s
source=s.objects['Spring_Original']
source.name='Spring_Original'
mat=source.data.materials[0];mat.name='Spring_Original_Surface'
# 원본 텍스처와 UV를 그대로 보존합니다.
for node in mat.node_tree.nodes:
 if node.type=='TEX_IMAGE' and node.image:
  img=node.image
  dest=os.path.join(out,'Textures','Spring_'+('BaseColor.png' if 'base_color' in img.name else 'Normal.png'))
  img.filepath_raw=dest;img.save()
report=[];pieces=[]
labels=['FlowerForest','FullBloom']
# 연결된 큰 두 그룹은 벽이며, 바닥은 접점 기준으로 분리합니다.
tmp=bmesh.new();tmp.from_mesh(source.data);bmesh.ops.remove_doubles(tmp,verts=list(tmp.verts),dist=.000001)
seen=set();groups=[]
for v in tmp.verts:
 if v in seen:continue
 g=[];todo=[v];seen.add(v)
 while todo:
  t=todo.pop();g.append(t)
  for e in t.link_edges:
   n=e.other_vert(t)
   if n not in seen:seen.add(n);todo.append(n)
 groups.append(g)
groups.sort(key=len,reverse=True)
wall_coords=[set(tuple(round(c,6) for c in v.co) for v in g) for g in groups[:2]]
tmp.free()
wall_thresholds=[-.302,-.106,.090,.286]
floor_thresholds=[-.287,-.145,.014,.177,.335]
for row in range(2):
 for col,label in enumerate(labels):
  name=label+('_Wall' if row==0 else '_Floor')
  mesh=source.data.copy();bm=bmesh.new();bm.from_mesh(mesh)
  def belongs(f):
   p=f.calc_center_median()
   ids=[i for i,coords in enumerate(wall_coords) if any(tuple(round(c,6) for c in v.co) in coords for v in f.verts)]
   if row==0:return col in ids
   return not ids and int(p.x-.273*p.z>0)==col
  bmesh.ops.delete(bm,geom=[f for f in bm.faces if not belongs(f)],context='FACES')
  bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
  bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
  bm.to_mesh(mesh);bm.free()
  obj=bpy.data.objects.new(name,mesh);s.collection.objects.link(obj);obj.matrix_world=source.matrix_world.copy()
  bpy.context.view_layer.objects.active=obj
  before=sum(len(p.vertices)-2 for p in mesh.polygons)
  original=mesh.copy()
  # 감소량보다 외형 보존을 우선하며 표면 오차가 크면 완화합니다.
  verts=[v.co.copy() for v in original.vertices];polys=[list(p.vertices) for p in original.polygons]
  bvh=BVHTree.FromPolygons(verts,polys)
  extent=max(max(v[k] for v in verts)-min(v[k] for v in verts) for k in range(3))
  accepted=None
  for ratio in [.75,.85,.93,.97]:
   obj.data=original.copy()
   mod=obj.modifiers.new('Preserve_Silhouette','DECIMATE');mod.decimate_type='COLLAPSE';mod.ratio=ratio;mod.use_collapse_triangulate=True
   bpy.ops.object.modifier_apply(modifier=mod.name)
   obvh=BVHTree.FromPolygons([v.co for v in obj.data.vertices],[list(p.vertices) for p in obj.data.polygons])
   errors=[obvh.find_nearest(v)[3] for v in verts]
   reverse=[bvh.find_nearest(v.co)[3] for v in obj.data.vertices]
   maxerr=max(errors+reverse)
   if maxerr<=extent*.008:accepted=ratio;break
  if accepted is None:
   obj.data=original.copy();accepted=1;maxerr=0
  after=sum(len(p.vertices)-2 for p in obj.data.polygons)
  report.append({'name':name,'beforeTriangles':before,'afterTriangles':after,'ratio':accepted,'maxSurfaceDeviationPercent':round(maxerr/extent*100,4)})
  pieces.append(obj)
source.hide_render=True;source.hide_set(True)
# 같은 시점의 비교 이미지를 보관합니다.
s.render.filepath=os.path.join(archive,'Optimized_Review.png');bpy.ops.render.render(write_still=True)
for obj in pieces:
 # 복사본만 실사용 크기로 정규화합니다. 원본/비교용 배치는 Blender에 남깁니다.
 exp=obj.copy();exp.data=obj.data.copy();s.collection.objects.link(exp)
 coords=[v.co for v in exp.data.vertices]
 lo=Vector([min(v[k] for v in coords) for k in range(3)]);hi=Vector([max(v[k] for v in coords) for k in range(3)])
 center=Vector(((lo.x+hi.x)/2,lo.y,(lo.z+hi.z)/2))
 factor=6/(hi.x-lo.x)
 for v in exp.data.vertices:v.co=(v.co-center)*factor
 # 원본의 Y-up 좌표를 Blender Z-up으로 변환하고 내보냅니다.
 exp.data.transform(Matrix.Rotation(math.pi/2,4,'X'));exp.matrix_world=Matrix.Identity(4)
 wrapper=bpy.data.objects.new(obj.name+'_Root',None);s.collection.objects.link(wrapper);exp.parent=wrapper;exp.name=obj.name+'_Mesh'
 bpy.ops.object.select_all(action='DESELECT');exp.select_set(True);wrapper.select_set(True);bpy.context.view_layer.objects.active=wrapper
 bpy.ops.export_scene.fbx(filepath=os.path.join(out,'Models',obj.name+'.fbx'),use_selection=True,object_types={'MESH','EMPTY'},bake_anim=False,axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_UNITS',path_mode='RELATIVE')
 dims=list(exp.dimensions)
 report[[r['name'] for r in report].index(obj.name)]['blenderDimensions']=dims
 bpy.data.objects.remove(exp,do_unlink=True);bpy.data.objects.remove(wrapper,do_unlink=True)
with open(os.path.join(archive,'Optimization_Report.json'),'w',encoding='utf-8') as f:json.dump(report,f,indent=2)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(archive,'Spring_Optimized.blend'))
print(json.dumps(report))


