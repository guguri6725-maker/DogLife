import bpy,math,os,json,shutil
from mathutils import Matrix,Vector
base=r"C:/Users/mbc/Desktop/NC_AI/DogLife";out=base+"/Assets/MapDesigne/Summer/Door/Models";archive=base+"/ArtSource/SummerDungeon/Door"
backup=archive+"/Stage3_Before_WideRiver.unity.backup"
if not os.path.exists(backup):shutil.copy2(base+"/Assets/Scenes/Stage3_Summer.unity",backup)
s=bpy.data.scenes.new("Summer_Grounded_Rivers");bpy.context.window.scene=s
report=[]
def smooth(t):
 t=max(0,min(1,t));return t*t*(3-2*t)
def export(name,verts,faces,colors=None):
 # Keep exported object/mesh names stable so Unity retains sub-asset references on reimport.
 for collection,key in [(bpy.data.objects,name),(bpy.data.meshes,name+"_Mesh")]:
  old=collection.get(key)
  if old:old.name="Archived_"+key
 # Make every generated surface face upwards before converting Unity Y-up to Blender Z-up.
 faces=[tuple(reversed(f)) if (Vector(verts[f[1]])-Vector(verts[f[0]])).cross(Vector(verts[f[2]])-Vector(verts[f[0]])).y<0 else f for f in faces]
 mesh=bpy.data.meshes.new(name+"_Mesh");mesh.from_pydata(verts,[],faces);mesh.update()
 for p in mesh.polygons:p.use_smooth=True
 mesh.transform(Matrix.Rotation(math.pi/2,4,'X'))
 if colors:
  c=mesh.color_attributes.new(name="Col",type='FLOAT_COLOR',domain='POINT')
  for i,v in enumerate(colors):c.data[i].color=v
 obj=bpy.data.objects.new(name,mesh);s.collection.objects.link(obj)
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.export_scene.fbx(filepath=out+"/"+name+".fbx",use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_UNITS')
 return sum(len(f)-2 for f in faces)
for ri,(name,L,Z,off,radius) in enumerate([("CoastalCave",38.4,22,0,3.8),("TropicalForest",38.4,22,44.4,2.3),("Waterfall",38.4,22,88.8,5.2),("CoastalCliff",46.08,26,133.2,5.8)]):
 def river(x,z):
  if x<0 or x>L:return -10,.32
  r=radius*math.sqrt(max(0,min(1,x/4,(L-x)/4)))+.32
  center=12+math.sin((L-x+off)*.15)
  return r-abs(z-center),r
 def branch(x,z,side):
  center=12+math.sin(((L-6.5 if side==0 else 6.5)+off)*.15)
  t=max(0,min(1,(z+.5)/(center+.5)))
  cx=.18+6.3*(t**.8)+.5*math.sin(z*.65+ri*.7)*math.sin(math.pi*t)
  cx=cx if side==0 else L-cx
  w=(1.6+.3*math.sin(z*.87+ri+side)+.85*smooth(t)) * math.sqrt(max(0,min(1,(z+2.7)/2.2)))
  d=min(w-abs(x-cx),z+2.7,center+.8-z)
  end=max(3.4,center-radius-.5)
  # Start below the surrounding floor; descend continuously into the river.
  y=.045-.295*smooth((z-1)/(end-1))
  return d,w,y
 def field(x,z):
  dr,wr=river(x,z);choices=[(dr,wr,-.25),branch(x,z,0),branch(x,z,1)]
  best=max(choices,key=lambda a:a[0])
  d,w,y=best
  if dr>0:y=-.25
  bank=max(0,min(1,1-d/max(w,.1)))**4
  alpha=smooth(d/.22)
  return d,y,bank,alpha
 # Clip a shared grid against the union of the river and the two tributaries: no overlapping sheets.
 nx=math.ceil((L+3)/.27);nz=math.ceil(24/.27)
 samples={}
 for i in range(nx+1):
  x=-1.5+(L+3)*i/nx
  for j in range(nz+1):
   z=-3+24*j/nz;d,y,b,a=field(x,z);samples[(i,j)]=(x,z,d)
 verts=[];faces=[];colors=[];lookup={}
 def add(p):
  x,z=p[0],p[1];key=(round(x,6),round(z,6))
  if key in lookup:return lookup[key]
  d,y,b,a=field(x,z);idx=len(verts);lookup[key]=idx
  verts.append((L-x,y,z));colors.append((b,b,b,a));return idx
 for i in range(nx):
  for j in range(nz):
   q=[samples[(i,j)],samples[(i+1,j)],samples[(i+1,j+1)],samples[(i,j+1)]]
   for tri in [(q[0],q[1],q[2]),(q[0],q[2],q[3])]:
    poly=[]
    for a,b in zip(tri,tri[1:]+tri[:1]):
     if a[2]>=0:poly.append(a)
     if (a[2]>=0)!=(b[2]>=0):
      t=a[2]/(a[2]-b[2]);poly.append((a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t,0))
    if len(poly)>=3:
     ids=[add(p) for p in poly]
     for k in range(1,len(ids)-1):faces.append((ids[0],ids[k],ids[k+1]))
 watertris=export(name+"_UnifiedDoorRiver",verts,faces,colors)
 # Keep the original landscape, with shallow irregular banks only around the enlarged tributaries.
 nx=math.ceil((L+4)/.4);nz=math.ceil(2*Z/.4)
 xs=sorted(set([-2+(L+4)*i/nx for i in range(nx+1)]+[i*.22 for i in range(48)]+[L-i*.22 for i in range(48)]))
 zs=sorted(set([-Z+2*Z*j/nz for j in range(nz+1)]+[1+i*.22 for i in range(61)]))
 verts=[];faces=[]
 for x in xs:
  wx=L-x
  for z in zs:
   if x<0 or x>L:h=.08
   else:
    r=radius*math.sqrt(max(0,min(1,x/4,(L-x)/4)));c=12+math.sin((x+off)*.15)
    h=.08-1.1*(1-smooth((abs(z-c)-r+1)/2))
   # Match the bank to the SAME union boundary as the water, including junctions.
   # At the transparent edge, soil reaches the surface. Only the interior is submerged.
   d,y,_,_=field(wx,z)
   if d>-.8:
    inside=smooth(max(0,d)/.75)
    bed=min(h,y-.19)
    target=(y+.025)*(1-inside)+bed*inside
    mix=smooth((d+.8)/.8)
    h=h*(1-mix)+target*mix
   verts.append((x,h,z))
 n=len(zs)
 for i in range(len(xs)-1):
  for j in range(n-1):
   a=i*n+j;faces.append((a,a+1,a+n+1,a+n))
 banktris=export(name+"_WideChannelBank",verts,faces)
 report.append({"room":name,"waterTriangles":watertris,"bankTriangles":banktris,"branchApproxWidth":[2.6,5.5]})
open(archive+"/WideRiver_Report.json","w").write(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=archive+"/Summer_Grounded_Rivers.blend")
print(json.dumps(report))
