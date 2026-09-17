import bpy,bmesh,math,json,os,shutil
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
base=r"C:/Users/mbc/Desktop/NC_AI/DogLife";out=base+"/Assets/MapDesigne/Fall/Door";archive=base+"/ArtSource/FallDungeon/Door"
s=bpy.data.scenes["Fall_Sinking_Door"];bpy.context.window.scene=s
source=s.objects["FallDoor_Source_Preserved"]
for old in list(s.objects):
 if old.name in ["SinkingPanel","FallDoorRig"]:bpy.data.objects.remove(old,do_unlink=True)
obj=source.copy();obj.data=source.data.copy();s.collection.objects.link(obj);obj.name="SinkingPanel";obj.matrix_world=Matrix.Identity(4)
# Reduce only depth; the front outline, UVs and original surface textures remain.
for v in obj.data.vertices:v.co.z*=.18
reference=[v.co.copy() for v in obj.data.vertices]
bpy.context.view_layer.objects.active=obj;obj.select_set(True)
mod=obj.modifiers.new("Conservative_Optimization",'DECIMATE');mod.ratio=.55
bpy.ops.object.modifier_apply(modifier=mod.name)
tree=BVHTree.FromPolygons([v.co for v in obj.data.vertices],[list(p.vertices) for p in obj.data.polygons])
dist=sorted(tree.find_nearest(v)[3] for v in reference)
obj.data.transform(Matrix.Rotation(math.pi/2,4,"X"))
# Move as one solid door, slowly accelerating and settling below the ground.
for frame,z in [(1,0),(7,0),(22,-.08),(48,-.72),(73,-1.05)]:
 obj.location=(0,0,z);obj.keyframe_insert(data_path="location",frame=frame)
source.hide_render=True;source.hide_set(True)
s.frame_start=1;s.frame_end=73;s.render.fps=30
for frame,label in [(1,"Thin_Closed"),(38,"Sinking"),(73,"Open")]:
 s.frame_set(frame);s.render.filepath=archive+"/"+label+".png";bpy.ops.render.render(write_still=True)
s.frame_set(1)
root=bpy.data.objects.new("FallDoorRig",None);s.collection.objects.link(root);obj.parent=root
bpy.ops.object.select_all(action='DESELECT');root.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=obj
bpy.ops.export_scene.fbx(filepath=out+"/Models/FallSinkingDoor.fbx",use_selection=True,object_types={'MESH','EMPTY'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='RELATIVE')
bpy.ops.wm.save_as_mainfile(filepath=archive+"/FallSinkingDoor.blend")
report={"sourceTriangles":30000,"finalTriangles":sum(len(p.vertices)-2 for p in obj.data.polygons),"depthRatio":.18,"durationSeconds":2.4,"surfaceDeviation95":dist[int(len(dist)*.95)],"surfaceDeviationMax":max(dist)}
open(archive+"/Optimization_Report.json","w").write(json.dumps(report,indent=2))
backup=archive+"/Stage3_Before_FallDoors.unity.backup"
if not os.path.exists(backup):shutil.copy2(base+"/Assets/Scenes/Stage3_Fall.unity",backup)
print(json.dumps(report))

