import bpy,math,os,json
from mathutils import Matrix,Vector,Quaternion
base=r"C:/Users/mbc/Desktop/NC_AI/DogLife"
out=base+"/Assets/MapDesigne/Summer/Models/ShallowRooms"
archive=base+"/ArtSource/SummerDungeon/Door"
os.makedirs(out,exist_ok=True)
s=bpy.data.scenes.new("Summer_Shallow_Rooms");bpy.context.window.scene=s
report=[]
# Build a solid walking bed and a separate non-blocking flat water surface.
for name,L,Z in [("CoastalCave",38.4,22),("TropicalForest",38.4,22),("Waterfall",38.4,22),("CoastalCliff",46.08,26),("Passage",6,3.5)]:
 for wet in [False,True]:
  suffix="_ShallowWater" if wet else "_ShallowBed"
  key=name+suffix
  for collection,k in [(bpy.data.objects,key),(bpy.data.meshes,key+"_Mesh")]:
   old=collection.get(k)
   if old:old.name="Archived_"+k
  # Room floors extend under walls; passage seams meet at the exact water level.
  margin=0 if wet or name=="Passage" else 2
  nx=math.ceil((L+margin*2)/.8);nz=math.ceil(2*Z/.8)
  vs=[];cols=[];faces=[]
  for i in range(nx+1):
   x=-margin+(L+2*margin)*i/nx
   for j in range(nz+1):
    z=-Z+2*Z*j/nz
    y=-.03 if wet else -.18+.01*math.sin(x*.5)*math.sin(z*.6)
    vs.append((x,y,z));cols.append((.65,.65,.65,1))
  for i in range(nx):
   for j in range(nz):
    a=i*(nz+1)+j;faces.append((a,a+1,a+nz+2,a+nz+1))
  mesh=bpy.data.meshes.new(key+"_Mesh");mesh.from_pydata(vs,[],faces);mesh.update()
  mesh.transform(Matrix.Rotation(math.pi/2,4,'X'))
  for p in mesh.polygons:p.use_smooth=True
  c=mesh.color_attributes.new(name="Col",type='FLOAT_COLOR',domain='POINT')
  for i,v in enumerate(cols):c.data[i].color=v
  obj=bpy.data.objects.new(key,mesh);s.collection.objects.link(obj)
  bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
  bpy.ops.export_scene.fbx(filepath=out+"/"+key+".fbx",use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_UNITS')
  report.append({"mesh":key,"triangles":len(faces)*2,"waterY":-.03,"bedY":-.18,"depthRange":[.14,.16]})
# A top view makes the layered floor/water construction inspectable in Blender.
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':
  area.spaces.active.region_3d.view_rotation=Quaternion((1,0,0,0))
  area.spaces.active.region_3d.view_location=Vector((23,0,0))
  area.spaces.active.region_3d.view_distance=65
bpy.ops.wm.save_as_mainfile(filepath=archive+"/Summer_Shallow_Rooms.blend")
open(archive+"/ShallowRooms_Report.json","w").write(json.dumps(report,indent=2))
print(json.dumps(report))

