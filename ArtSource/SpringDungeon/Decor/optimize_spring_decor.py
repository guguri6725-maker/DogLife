import bpy,bmesh,os,json,math
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
base=r'C:\Users\mbc\Desktop\NC_AI\DogLife'
out=os.path.join(base,'Assets','MapDesigne','Spring')
sets={
'FirstRoom':['VineRuins','FlowerRock','FallenTree','GardenRing','GardenPillar','FlowerPatch','ForestPlant'],
'BossRoom':['BlossomWall','FlowerAltar','RaisedFlowerBed','BlossomPillar','FlowerMound','FlowerSprout','SmallFlowers','BlossomArch']}
report=[]
for tag,labels in sets.items():
 s=bpy.data.scenes['Spring_'+tag+'_Decor'];bpy.context.window.scene=s;source=s.objects['Spring_'+tag+'_Original']
 archive=os.path.join(base,'ArtSource','SpringDungeon','Decor',tag)
 for sub in ['Models','Prefabs','Materials']:os.makedirs(os.path.join(out,sub,'Decor',tag),exist_ok=True)
 bm=bmesh.new();bm.from_mesh(source.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
 seen=set();groups=[]
 for v in bm.verts:
  if v in seen:continue
  g=[];todo=[v];seen.add(v)
  while todo:
   t=todo.pop();g.append(t)
   for e in t.link_edges:
    n=e.other_vert(t)
    if n not in seen:seen.add(n);todo.append(n)
  groups.append(g)
 groups.sort(key=len,reverse=True)
 main_count=7
 mains=groups[:main_count]
 boxes=[(Vector([min(v.co[k] for v in g) for k in range(3)]),Vector([max(v.co[k] for v in g) for k in range(3)])) for g in mains]
 key=lambda co:tuple(round(c,6) for c in co)
 lookup={}
 for i,g in enumerate(groups):
  if i<main_count:idx=i
  else:
   c=sum((v.co for v in g),Vector())/len(g)
   idx=min(range(len(boxes)),key=lambda j:sum(max(boxes[j][0][k]-c[k],0,c[k]-boxes[j][1][k])**2 for k in range(3)))
  for v in g:lookup[key(v.co)]=(7 if tag=='BossRoom' and idx==0 and v.co.x>.235 else idx)
 bm.free()
 pieces=[]
 for idx,label in enumerate(labels):
  name='SpringDecor_'+tag+'_'+label;mesh=source.data.copy();bm=bmesh.new();bm.from_mesh(mesh)
  bmesh.ops.delete(bm,geom=[f for f in bm.faces if lookup.get(key(f.verts[0].co))!=idx],context='FACES')
  bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
  bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001);bm.to_mesh(mesh);bm.free()
  obj=bpy.data.objects.new(name,mesh);s.collection.objects.link(obj);obj.matrix_world=source.matrix_world.copy();bpy.context.view_layer.objects.active=obj
  original=mesh.copy();verts=[v.co.copy() for v in original.vertices];polys=[list(p.vertices) for p in original.polygons]
  bvh=BVHTree.FromPolygons(verts,polys);extent=max(max(v[k] for v in verts)-min(v[k] for v in verts) for k in range(3));before=sum(len(p.vertices)-2 for p in original.polygons)
  accepted=1;maxerr=0
  # 형태 오차가 허용 범위를 넘으면 감소 비율을 완화합니다.
  for ratio in [.35,.5,.65,.8,.9,.97]:
   obj.data=original.copy();mod=obj.modifiers.new('PreserveShape','DECIMATE');mod.decimate_type='COLLAPSE';mod.ratio=ratio;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
   obvh=BVHTree.FromPolygons([v.co for v in obj.data.vertices],[list(p.vertices) for p in obj.data.polygons])
   err=max([obvh.find_nearest(v)[3] for v in verts]+[bvh.find_nearest(v.co)[3] for v in obj.data.vertices])
   if err<=extent*.006:accepted=ratio;maxerr=err;break
  if accepted==1:obj.data=original.copy()
  after=sum(len(p.vertices)-2 for p in obj.data.polygons);pieces.append(obj)
  # 실사용 복사본만 중심/바닥 원점과 최대 길이 6단위로 정리합니다.
  exp=obj.copy();exp.data=obj.data.copy();s.collection.objects.link(exp)
  coords=[v.co for v in exp.data.vertices];lo=Vector([min(v[k] for v in coords) for k in range(3)]);hi=Vector([max(v[k] for v in coords) for k in range(3)])
  center=Vector(((lo.x+hi.x)/2,lo.y,(lo.z+hi.z)/2));factor=6/max(hi-lo)
  for v in exp.data.vertices:v.co=(v.co-center)*factor
  dims=list((hi-lo)*factor)
  exp.data.transform(Matrix.Rotation(math.pi/2,4,'X'));exp.matrix_world=Matrix.Identity(4)
  wrapper=bpy.data.objects.new(name+'_Root',None);s.collection.objects.link(wrapper);exp.parent=wrapper;exp.name=name+'_Mesh'
  bpy.ops.object.select_all(action='DESELECT');exp.select_set(True);wrapper.select_set(True);bpy.context.view_layer.objects.active=wrapper
  bpy.ops.export_scene.fbx(filepath=os.path.join(out,'Models','Decor',tag,name+'.fbx'),use_selection=True,object_types={'MESH','EMPTY'},bake_anim=False,axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_UNITS',path_mode='RELATIVE')
  report.append({'room':tag,'name':name,'beforeTriangles':before,'afterTriangles':after,'retainedRatio':accepted,'sampleMaxErrorPercent':round(maxerr/extent*100,4),'unityDimensions':dims})
  bpy.data.objects.remove(exp,do_unlink=True);bpy.data.objects.remove(wrapper,do_unlink=True)
 source.hide_render=True;source.hide_set(True)
 s.render.filepath=os.path.join(archive,'Optimized_Review.png');bpy.ops.render.render(write_still=True)
folder=os.path.join(base,'ArtSource','SpringDungeon','Decor')
with open(os.path.join(folder,'Optimization_Report.json'),'w',encoding='utf-8') as f:json.dump(report,f,indent=2)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(folder,'Spring_Decor_Optimized.blend'))
print(json.dumps(report))

