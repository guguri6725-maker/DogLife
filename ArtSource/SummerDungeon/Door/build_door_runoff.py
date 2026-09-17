import bpy,math,os,json
from mathutils import Matrix,Vector
base=r"C:/Users/mbc/Desktop/NC_AI/DogLife";out=base+"/Assets/MapDesigne/Summer/Door/Models";archive=base+"/ArtSource/SummerDungeon/Door"
s=bpy.data.scenes["Summer_Door_Runoff"];bpy.context.window.scene=s
for old in list(s.objects):bpy.data.objects.remove(old,do_unlink=True)
report=[]
def export(name,verts,faces,colors=None):
 mesh=bpy.data.meshes.new(name+"_Mesh");mesh.from_pydata(verts,[],faces);mesh.update();mesh.transform(Matrix.Rotation(math.pi/2,4,'X'))
 if colors:
  col=mesh.color_attributes.new(name="Col",type='FLOAT_COLOR',domain='POINT')
  for i,c in enumerate(colors):col.data[i].color=c if isinstance(c,tuple) else (c,c,c,1)
 obj=bpy.data.objects.new(name,mesh);s.collection.objects.link(obj)
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.export_scene.fbx(filepath=out+"/"+name+".fbx",use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_UNITS')
 return obj
def smoothpath(points):
 result=[]
 for k in range(len(points)-1):
  p0=Vector(points[max(0,k-1)]);p1=Vector(points[k]);p2=Vector(points[k+1]);p3=Vector(points[min(len(points)-1,k+2)])
  for j in range(10):
   t=j/10
   result.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
 result.append(Vector(points[-1]));return result
for name,L,Z,off,radius in [("CoastalCave",38.4,22,0,3.8),("TropicalForest",38.4,22,44.4,2.3),("Waterfall",38.4,22,88.8,5.2),("CoastalCliff",46.08,26,133.2,5.8)]:
 paths=[]
 for side in [0,1]:
  targetX=7 if side==0 else L-7
  mouth=12+math.sin((L-targetX+off)*.15)-radius+.6
  points=[(.12,-2.5),(.12,0),(.6,2.1),(3.7,2.8),(6.9,4.0),(7,mouth)]
  if side:points=[(L-x,z) for x,z in points]
  path=smoothpath(points);paths.append((path,mouth))
  verts=[];faces=[];colors=[]
  for i,p in enumerate(path):
   tangent=(path[min(i+1,len(path)-1)]-path[max(0,i-1)]).normalized();normal=Vector((-tangent.y,tangent.x))
   width=.38+.12*max(0,min(1,p.y/mouth))
   y=.11-.355*max(0,min(1,(p.y-2.3)/(mouth-2.3)))
   for j in range(5):
    u=-1+2*j/4;q=p+normal*(u*width)
    verts.append((L-q.x,y,q.y));colors.append((abs(u)**4,0,0,1-abs(u)**6))
  for i in range(len(path)-1):
   for j in range(4):
    a=i*5+j;faces.append((a,a+1,a+6,a+5))
  export(name+("_Entrance_Runoff" if side==0 else "_Exit_Runoff"),verts,faces,colors)
  report.append({"room":name,"side":side,"mouthLocalX":targetX,"mouthZ":mouth,"waterLevelStart":.11,"waterLevelEnd":-.245,"triangles":len(faces)*2})
 def originalheight(x,z):
  if x<0 or x>L:return .08
  center=12+math.sin((x+off)*.15)
  r=radius*math.sqrt(max(0,min(1,x/4,(L-x)/4)))
  a=max(0,min(1,(abs(z-center)-r+1)/2));a=a*a*(3-2*a)
  return .08-1.1*(1-a)
 nx=math.ceil((L+4)/.55);nz=math.ceil(2*Z/.55)
 xs=sorted(set([-2+(L+4)*i/nx for i in range(nx+1)]+[i*.18 for i in range(49)]+[L-i*.18 for i in range(49)]))
 zs=sorted(set([-Z+2*Z*j/nz for j in range(nz+1)]+[2+i*.18 for i in range(64)]))
 nx=len(xs)-1;nz=len(zs)-1
 verts=[];faces=[];changed=0
 for i in range(nx+1):
  x=xs[i];wx=L-x
  for j in range(nz+1):
   z=zs[j];h=originalheight(x,z);old=h
   if -.8<z<14 and (wx<8.5 or wx>L-8.5):
    for path,mouth in paths:
     p=Vector((wx,z));nearest=min(path,key=lambda q:(q-p).length_squared);d=(nearest-p).length
     width=.38+.12*max(0,min(1,nearest.y/mouth))
     water=.11-.355*max(0,min(1,(nearest.y-2.3)/(mouth-2.3)))
     if nearest.y>1.5 and d<width+.75:
      blend=max(0,min(1,(width+.75-d)/.75));blend=blend*blend*(3-2*blend)
      blend*=max(0,min(1,(nearest.y-1.5)/.7))
      h=min(h,old+(water-.12-old)*blend)
   if h<old-.00001:changed+=1
   verts.append((x,h,z))
 for i in range(nx):
  for j in range(nz):
   a=i*(nz+1)+j;b=a+nz+1;faces.append((a,a+1,b+1,b))
 export(name+"_DoorChannelBank",verts,faces)
 report.append({"room":name,"modifiedBankVertices":changed})
open(archive+"/Runoff_Report.json","w").write(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=archive+"/SummerDoor_Runoff.blend")
print(json.dumps(report))

