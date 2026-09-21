import bpy,bmesh,math,random
from mathutils import Vector,Quaternion
ROOT='C:/unity/DogLife/Assets/Art/SeasonCrafted'
collection=bpy.data.collections.get('DogLife_SeasonCrafted')
if not collection:
 collection=bpy.data.collections.new('DogLife_SeasonCrafted');bpy.context.scene.collection.children.link(collection)
else:
 for ob in list(collection.objects):bpy.data.objects.remove(ob,do_unlink=True)
# Shared closed tube and UV helpers from the repaired autumn model source.
src=open('C:/unity/DogLife/ArtSource/AutumnCrafted/build_autumn.py',encoding='utf-8-sig').read()
exec(src[src.index('class Mesh:'):src.index('def tree(')])
def mat(name,color):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.diffuse_color=(*color,1);return m
bark=mat('Season_Bark',(.28,.15,.065));stone=mat('Season_Stone',(.81,.73,.56));leaf=mat('Season_Leaf',(.2,.5,.06))
bronze=mat('Season_Bronze',(.34,.2,.065));blue=mat('Season_Blue',(.1,.61,.73));white=mat('Season_Canvas',(.96,.88,.7))
def primitive(name,p,size,material,kind='cube'):
 if kind=='rock':bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=p)
 else:bpy.ops.mesh.primitive_cube_add(size=1,location=p)
 o=bpy.context.object;o.name=name;o.scale=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 for c in list(o.users_collection):c.objects.unlink(o)
 collection.objects.link(o);o.data.materials.append(material)
 if kind=='cube':
  mod=o.modifiers.new('Worn edges','BEVEL');mod.width=.045;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
 uv=o.data.uv_layers.active or o.data.uv_layers.new()
 for f in o.data.polygons:
  ax=max(range(3),key=lambda a:abs(f.normal[a]));axes=[a for a in range(3) if a!=ax]
  for li in f.loop_indices:
   v=o.data.vertices[o.data.loops[li].vertex_index].co;uv.data[li].uv=(v[axes[0]]*.6,v[axes[1]]*.6)
 return o
wood=Mesh();wood.tube([(0,0,.12),(.2,0,1),(-.1,.1,2.2),(.3,.1,3.5),(.8,.2,4.3)],[.65,.53,.39,.22,.03],16)
for i in range(7):
 a=i*math.tau/7
 wood.tube([(0,0,.65),(math.cos(a)*1.1,math.sin(a)*1.1,.22),(math.cos(a)*2,math.sin(a)*2,.015)],[.35,.19,.018],10)
for a,z in [(0,1.6),(2,2.6),(4,2)]:
 wood.tube([(0,0,z),(math.cos(a)*1.1,math.sin(a)*1.1,z+.7),(math.cos(a)*1.9,math.sin(a)*1.9,z+1.2)],[.22,.12,.012],10)
export('AncientSnag',[wood.object('AncientSnag_Wood',bark)])
wood=Mesh()
for i in range(6):
 a=i*math.tau/6
 pts=[(0,0,.18),(math.cos(a)*.7,math.sin(a)*.7,.26),(math.cos(a+.2)*1.5,math.sin(a+.2)*1.5,.16),(math.cos(a+.4)*2.2,math.sin(a+.4)*2.2,.025)]
 wood.tube(pts,[.2,.16,.1,.012],10)
export('ExposedRoots',[wood.object('ExposedRoots_Wood',bark)])
wood=Mesh();wood.tube([(-2.4,0,.27),(-1,.12,.35),(0,0,.45),(1.6,-.2,.36),(2.7,-.1,.15)],[.11,.35,.45,.28,.018],14)
for i in range(3):wood.tube([(i-.7,0,.3),(i-.9,.6,.6),(i-1.2,.9,.7)],[.14,.07,.012],8)
export('CoastalDriftwood',[wood.object('Driftwood_Wood',bark)])
obs=[primitive('Handle_Backplate',(0,.04,0),(.23,.07,.43),bronze)]
bpy.ops.mesh.primitive_torus_add(major_radius=.20,minor_radius=.045,major_segments=24,minor_segments=8,location=(0,-.07,-.08),rotation=(math.pi/2,0,0))
o=bpy.context.object;o.name='Handle_Ring'
for c in list(o.users_collection):c.objects.unlink(o)
collection.objects.link(o);o.data.materials.append(bronze);obs.append(o)
export('BronzeDoorRing',obs)
for variant in range(2):
 wood=Mesh();fol=Mesh();lean=1 if variant==0 else -1
 pts=[(lean*.025*i*i,0,i*.8) for i in range(9)]
 wood.tube(pts,[.32-i*.022 for i in range(9)],14)
 crown=Vector(pts[-1])
 for i in range(11):
  a=i*math.tau/11
  center=[]
  for j in range(9):
   t=j/8;center.append(crown+Vector((math.cos(a)*t*3.1,math.sin(a)*t*3.1,math.sin(t*math.pi)*.7-t*t*1.7)))
  wood.tube(center,[.055*(1-j/9) for j in range(9)],6)
  for j in range(8):
   v=center[j];n=center[j+1];perp=Vector((-math.sin(a),math.cos(a),0))
   w=.52*math.sin(math.pi*(j+.25)/8);wn=.52*math.sin(math.pi*min(1,(j+1.25)/8))
   for side in [-1,1]:
    edge=v+perp*side*w-Vector((0,0,.08));edge2=n+perp*side*wn-Vector((0,0,.08))
    fol.face([v,n,edge2,edge],[(.5,j/8),(.5,(j+1)/8),(1 if side>0 else 0,(j+1)/8),(1 if side>0 else 0,j/8)])
 export('CoastalPalm_'+str(variant+1),[wood.object('Palm_Wood',bark),fol.object('Palm_Leaf',leaf)])
obs=[]
rng=random.Random(92)
for i in range(5):
 obs.append(primitive('Limestone_Rock',(rng.uniform(-1.5,1.5),rng.uniform(-.9,.9),.35+rng.random()*.4),(.7+rng.random()*.6,.5+rng.random()*.5,.5+rng.random()*.7),stone,'rock'))
export('LimestoneCluster',obs)
obs=[]
for x in [-1.7,1.7]:
 obs.append(primitive('Ruin_Foot',(x,0,.18),(1.2,1.2,.36),stone))
 for i in range(5):obs.append(primitive('Ruin_Block',(x,0,.66+i*.62),(.85,.85,.64),stone))
obs.append(primitive('Ruin_Lintel',(0,0,3.85),(4.5,1,.55),stone))
export('CoastalRuinArch',obs)
wood=Mesh();wood.tube([(0,0,0),(0,0,2.7)],[.045,.045],10);obs=[wood.object('Parasol_Wood',bark)]
for i in range(8):
 canopy=Mesh();a=i*math.tau/8;b=(i+1)*math.tau/8
 canopy.face([(0,0,3),(.9*math.cos(a),.9*math.sin(a),2.8),(.9*math.cos(b),.9*math.sin(b),2.8)],[(.5,1),(0,.5),(1,.5)])
 canopy.face([(.9*math.cos(a),.9*math.sin(a),2.8),(1.7*math.cos(a),1.7*math.sin(a),2.45),(1.7*math.cos(b),1.7*math.sin(b),2.45),(.9*math.cos(b),.9*math.sin(b),2.8)],[(0,.5),(0,0),(1,0),(1,.5)])
 obs.append(canopy.object('Parasol_Canvas' if i%2 else 'Parasol_Blue',white if i%2 else blue))
export('BeachParasol',obs)
obs=[]
for j in range(7):obs.append(primitive('Lounger_Slat',(0,j*.17,.5),(.8,.14,.08),bark))
for x in [-.3,.3]:
 for y in [0,.95]:obs.append(primitive('Lounger_Leg',(x,y,.24),(.10,.10,.48),bark))
export('BeachLounger',obs)
shell=Mesh()
for i in range(16):
 a=math.pi*i/16;b=math.pi*(i+1)/16
 shell.face([(0,0,.04),(math.cos(a)*.4,math.sin(a)*.5,.10+(i%2)*.03),(math.cos(b)*.4,math.sin(b)*.5,.10+((i+1)%2)*.03)],[(.5,0),(i/16,1),((i+1)/16,1)])
export('Seashell',[shell.object('Shell_Stone',stone)])
exec(open('C:/unity/DogLife/ArtSource/SeasonCrafted/apply_materials.py',encoding='utf-8-sig').read())
bpy.ops.wm.save_as_mainfile(filepath='C:/unity/DogLife/ArtSource/SeasonCrafted/SeasonCrafted.blend',copy=True)
print('Created 11 authored seasonal models.')


