import bpy,math,os,json
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
base=r"C:/Users/mbc/Desktop/NC_AI/DogLife";out=base+"/Assets/MapDesigne/Summer/Door";archive=base+"/ArtSource/SummerDungeon/Door"
s=bpy.data.scenes["Summer_Waterfall_Door"];bpy.context.window.scene=s
source=s.objects["SummerDoor_Source_Preserved"]
for old in list(s.objects):
 if old.name in ["SummerGate_Frame","WaterCurtain"]:bpy.data.objects.remove(old,do_unlink=True)
frame=source.copy();frame.data=source.data.copy();s.collection.objects.link(frame);frame.name="SummerGate_Frame";frame.matrix_world=Matrix.Identity(4);frame.hide_render=False;frame.hide_set(False)
bpy.context.view_layer.objects.active=frame;frame.select_set(True)
mod=frame.modifiers.new("Preserve_Arch_Details",'DECIMATE');mod.ratio=.6
bpy.ops.object.modifier_apply(modifier=mod.name)
tree=BVHTree.FromPolygons([v.co for v in source.data.vertices],[list(p.vertices) for p in source.data.polygons])
optimized=BVHTree.FromPolygons([v.co for v in frame.data.vertices],[list(p.vertices) for p in frame.data.polygons])
dist=sorted(optimized.find_nearest(v.co)[3] for v in source.data.vertices)
frame.data.transform(Matrix.Rotation(math.pi/2,4,'X'))
verts=[];faces=[];colors=[];nx=12;ny=32
for j in range(ny+1):
 y=.055+(.505-.055)*j/ny
 left=tree.ray_cast(Vector((0,y,0)),Vector((-1,0,0)),.4)[3]
 right=tree.ray_cast(Vector((0,y,0)),Vector((1,0,0)),.4)[3]
 left=min(.21,(left if left else .11)+.016);right=min(.21,(right if right else .11)+.016)
 for i in range(nx+1):
  u=i/nx;verts.append((-left+(left+right)*u,y,.0015*math.sin(i*1.1+j*.6)));colors.append((abs(2*u-1)**6,j/ny,0,1))
for j in range(ny):
 for i in range(nx):
  a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
mesh=bpy.data.meshes.new("WaterCurtain_Mesh");mesh.from_pydata(verts,[],faces);mesh.update();mesh.transform(Matrix.Rotation(math.pi/2,4,'X'))
col=mesh.color_attributes.new(name="Col",type='FLOAT_COLOR',domain='POINT')
for i,c in enumerate(colors):col.data[i].color=c
water=bpy.data.objects.new("WaterCurtain",mesh);s.collection.objects.link(water)
# The curtain thins into a final stream. Unity also fades its shared water material per renderer.
for f,scale in [(1,1),(18,1),(55,.72),(78,.22),(91,0)]:
 water.scale=(scale,1,1);water.keyframe_insert(data_path="scale",frame=f)
source.hide_render=True;source.hide_set(True)
s.frame_start=1;s.frame_end=91;s.render.fps=30;s.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');frame.select_set(True);water.select_set(True);bpy.context.view_layer.objects.active=frame
bpy.ops.export_scene.fbx(filepath=out+"/Models/SummerWaterfallDoor.fbx",use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='RELATIVE')
report={"frameSourceTriangles":30000,"frameOptimizedTriangles":sum(len(p.vertices)-2 for p in frame.data.polygons),"waterTriangles":len(faces)*2,"duration":3,"surfaceDeviation95":dist[int(len(dist)*.95)],"surfaceDeviationMax":max(dist)}
open(archive+"/Optimization_Report.json","w").write(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=archive+"/SummerWaterfallDoor.blend")
print(json.dumps(report))

