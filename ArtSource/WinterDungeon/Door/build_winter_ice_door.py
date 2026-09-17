import bpy,bmesh,math,random,json,os,shutil
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
base=r"C:/Users/mbc/Desktop/NC_AI/DogLife";out=base+"/Assets/MapDesigne/Winter/Door";archive=base+"/ArtSource/WinterDungeon/Door"
s=bpy.data.scenes["Winter_Ice_Door"];bpy.context.window.scene=s
source=s.objects["WinterDoor_Source_Preserved"]
for old in list(s.objects):
 if old.name.startswith("Frame_") or old.name.startswith("IceShard_"):bpy.data.objects.remove(old,do_unlink=True)
work=source.copy();work.data=source.data.copy();s.collection.objects.link(work);work.name="Optimization_Work"
bpy.context.view_layer.objects.active=work;work.select_set(True)
# Moderate reduction before fracturing preserves UVs, snow edges and the carved columns.
weld=bmesh.new();weld.from_mesh(work.data);bmesh.ops.remove_doubles(weld,verts=list(weld.verts),dist=.00001);weld.to_mesh(work.data);weld.free()
mod=work.modifiers.new("Silhouette_preserving_reduction",'DECIMATE');mod.ratio=.55
bpy.ops.object.modifier_apply(modifier=mod.name)
tree=BVHTree.FromPolygons([v.co for v in work.data.vertices],[list(p.vertices) for p in work.data.polygons])
dist=sorted(tree.find_nearest(v.co)[3] for v in source.data.vertices)
report={"sourceTriangles":sum(len(p.vertices)-2 for p in source.data.polygons),"optimizedBeforeFracture":sum(len(p.vertices)-2 for p in work.data.polygons),"surfaceDeviationMax":max(dist),"surfaceDeviation95":dist[int(len(dist)*.95)]}
inner=bpy.data.materials.new("Ice_Fracture_Interior");inner.use_nodes=True
bsdf=next(n for n in inner.node_tree.nodes if n.type=="BSDF_PRINCIPLED")
bsdf.inputs["Base Color"].default_value=(.17,.40,.53,1);bsdf.inputs["Roughness"].default_value=.3
parts=[]
def cutmesh(name,planes,cap=False):
 mesh=work.data.copy();mesh.materials.append(inner);bm=bmesh.new();bm.from_mesh(mesh)
 bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
 for point,normal in planes:
  result=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),plane_co=point,plane_no=normal,dist=.000001,clear_outer=True,clear_inner=False)
  if cap:
   edges=[e for e in bm.edges if e.is_boundary and all(abs((v.co-point).dot(normal))<.00001 for v in e.verts)]
   if edges:
    filled=bmesh.ops.holes_fill(bm,edges=edges,sides=0)
    for f in filled["faces"]:f.material_index=1
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 bm.to_mesh(mesh);bm.free()
 obj=bpy.data.objects.new(name,mesh);s.collection.objects.link(obj)
 mesh.transform(Matrix.Rotation(math.pi/2,4,"X"));obj.matrix_world=Matrix.Identity(4)
 parts.append(obj);return obj
# Leave the two original pillars and the attached edge icicles completely fixed.
cutmesh("Frame_Left",[(Vector((-.29,0,0)),Vector((1,0,0)))])
cutmesh("Frame_Right",[(Vector((.29,0,0)),Vector((-1,0,0)))])
rng=random.Random(47)
seeds=[Vector((-.24+col*.16+rng.uniform(-.035,.035),.07+row*.17+rng.uniform(-.025,.025),0)) for row in range(4) for col in range(4)]
for i,seed in enumerate(seeds):
 planes=[(Vector((-.29,0,0)),Vector((-1,0,0))),(Vector((.29,0,0)),Vector((1,0,0)))]
 for j,other in enumerate(seeds):
  if i!=j:planes.append(((seed+other)*.5,(other-seed).normalized()))
 obj=cutmesh("IceShard_%02d"%(i+1),planes,True)
 # Each solid chunk has its own pivot and a short outward impulse followed by a fall.
 center=sum((v.co for v in obj.data.vertices),Vector())/max(1,len(obj.data.vertices))
 obj.data.transform(Matrix.Translation(-center));obj.location=center
 delay=7+int(abs(seed.x)*12)+rng.randrange(0,4)
 push=Vector((seed.x*.48,rng.uniform(-.22,.22),rng.uniform(.025,.11)))
 spin=Vector((rng.uniform(-1.4,1.4),rng.uniform(-1.2,1.2),rng.uniform(-1.5,1.5)))
 keys=[(1,center,Vector(),1),(delay,center,Vector(),1),(delay+9,center+push,spin*.28,1),(delay+25,Vector((center.x+push.x*2,center.y+push.y*2,-.15)),spin,1),(delay+40,Vector((center.x+push.x*2.2,center.y+push.y*2.2,-.45)),spin*1.3,0)]
 for frame,loc,rot,scale in keys:
  obj.location=loc;obj.rotation_euler=rot;obj.scale=(scale,)*3
  for channel in ["location","rotation_euler","scale"]:obj.keyframe_insert(data_path=channel,frame=frame)
source.hide_render=True;source.hide_set(True);bpy.data.objects.remove(work,do_unlink=True)
s.frame_start=1;s.frame_end=61;s.render.fps=30
report["finalTriangles"]=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts);report["shards"]=16;report["duration"]=2.0
for frame,label in [(1,"Closed"),(24,"Breaking"),(61,"Open")]:
 s.frame_set(frame);s.render.filepath=archive+"/Door_"+label+".png";bpy.ops.render.render(write_still=True)
s.frame_set(1);bpy.ops.object.select_all(action="DESELECT")
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.export_scene.fbx(filepath=out+"/Models/WinterIceDoor.fbx",use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='RELATIVE')
bpy.ops.wm.save_as_mainfile(filepath=archive+"/WinterIceDoor.blend")
open(archive+"/Optimization_Report.json","w").write(json.dumps(report,indent=2))
backup=archive+"/Stage4_Before_IceDoors.unity.backup"
if not os.path.exists(backup):shutil.copy2(base+"/Assets/Scenes/Stage4_Winter.unity",backup)
print(json.dumps(report))

