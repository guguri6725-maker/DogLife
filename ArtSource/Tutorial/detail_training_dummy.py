# Improve the existing training prop while preserving animated arm pivots.
import bpy, math, random, os, numpy as np
from mathutils import Vector
random.seed(42)
base=r'C:/Users/mbc/Desktop/NC_AI/DogLife'
bpy.ops.wm.save_as_mainfile(filepath=base+'/ArtSource/Tutorial/PracticeScarecrow_before_detail.blend')
root=bpy.data.objects['PracticeScarecrow']; body=bpy.data.objects['PracticeScarecrow_Mesh']
meshes=[body,bpy.data.objects['AttackArmL_Mesh'],bpy.data.objects['AttackArmR_Mesh']]
texdir=base+'/Assets/Models/Tutorial/Textures';os.makedirs(texdir,exist_ok=True)
# Tileable weathering and woven fibers, saved as real exportable textures.
y,x=np.mgrid[0:512,0:512]/512; rng=np.random.default_rng(52)
for name in ['Training_Linen','Training_Walnut','Training_Endgrain','Training_Rope','Training_TargetRed']:
 m=bpy.data.materials[name];m.use_nodes=True;p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');color=np.array(p.inputs['Base Color'].default_value[:3])
 if name=='Training_Linen':
  color=np.array([.40,.29,.17]);f=.85+.14*np.sin(x*math.tau*64)*np.sin(y*math.tau*64)+.08*np.cos(x*math.tau*5+y*math.tau*3)+.05*rng.normal(size=x.shape)
 elif 'Walnut' in name or 'Endgrain' in name:
  color=np.array([.24,.12,.055]);f=.82+.17*np.sin(x*math.tau*30+2*np.sin(y*math.tau*2))+.1*np.sin(x*math.tau*7+y*math.tau)+.035*rng.normal(size=x.shape)
 else:f=.8+.18*np.sin((x+y)*math.tau*40)+.08*rng.normal(size=x.shape)
 pixels=np.ones((512,512,4),dtype=np.float32);pixels[:,:,:3]=np.clip(color[None,None,:]*f[:,:,None],0,1)
 im=bpy.data.images.new(name+'_Worn',512,512);im.pixels.foreach_set(pixels.ravel());im.filepath_raw=texdir+'/'+name+'_Worn.png';im.file_format='PNG';im.save()
 node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=im;m.node_tree.links.new(node.outputs['Color'],p.inputs['Base Color']);p.inputs['Roughness'].default_value=.88
# Soften perfect sack silhouettes with compressed folds at waist and shoulders.
for obj in meshes:
 for v in obj.data.vertices:
  slots={obj.data.polygons[i].material_index for i in []}
 # Only move vertices belonging exclusively to fabric, keeping timber and rope intact.
 fabric=set();other=set()
 for poly in obj.data.polygons:
  dst=fabric if obj.data.materials[poly.material_index].name=='Training_Linen' else other
  dst.update(poly.vertices)
 for i in fabric-other:
  v=obj.data.vertices[i];w=obj.matrix_world@v.co
  v.co.x+=.008*math.sin(w.z*49+w.x*11);v.co.y+=.009*math.sin(w.x*39+w.z*22)
 obj.data.update()
leather=bpy.data.materials.new('Training_PatchedLeather');leather.diffuse_color=(.19,.095,.045,1);leather.use_nodes=True
p=next(n for n in leather.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=leather.diffuse_color;p.inputs['Roughness'].default_value=.92
parts=[]
def tube(a,b,r,mat):
 d=Vector(b)-Vector(a);bpy.ops.mesh.primitive_cylinder_add(vertices=7,radius=r,depth=d.length,location=(Vector(a)+Vector(b))/2);o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();o.data.materials.append(mat);parts.append(o);return o
# Curved leather reinforcement on the torso edges and irregular visible stitches.
for side in [-1,1]:
 verts=[];faces=[];nu=5;nv=12
 for j in range(nv):
  z=1.12+j*.045
  for i in range(nu):
   xx=side*(.255+i*.022); yy=-.265+((abs(xx)-.24)*.75)+.006*math.sin(j*2+i)
   verts.append((xx,yy,z))
 for j in range(nv-1):
  for i in range(nu-1):
   a=j*nu+i;faces.append((a,a+1,a+nu+1,a+nu))
 mesh=bpy.data.meshes.new('Worn shoulder binding');mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new('Leather side reinforcement',mesh);bpy.context.collection.objects.link(o);o.data.materials.append(leather);parts.append(o)
 for j in range(11):
  z=1.13+j*.044;tube((side*.263,-.257,z),(side*.286,-.243,z+.019),.004,bpy.data.materials['Training_Rope'])
# Uneven hanging fabric strips add a worn layered hem instead of a round balloon base.
for k in range(15):
 a=math.tau*k/15; rr=.245;z=1.03;length=random.uniform(.10,.23);width=random.uniform(.045,.075)
 center=Vector((math.cos(a)*rr,math.sin(a)*rr,z));t=Vector((-math.sin(a),math.cos(a),0))*width
 coords=[center-t,center+t,center+t*.55+Vector((0,0,-length)),center-t*.8+Vector((0,0,-length*.82))]
 mesh=bpy.data.meshes.new('Frayed fabric');mesh.from_pydata(coords,[],[(0,1,2,3)]);mesh.update();o=bpy.data.objects.new('Frayed hem',mesh);bpy.context.collection.objects.link(o);o.data.materials.append(bpy.data.materials['Training_Linen']);parts.append(o)
# Iron fasteners and carved weather cracks in the wooden plinth.
for xx in [-.47,.47]:
 for yy in [-.08,.08]:
  bpy.ops.mesh.primitive_uv_sphere_add(segments=10,ring_count=6,radius=.025,location=(xx,yy,.18));o=bpy.context.object;o.scale.z=.35;o.data.materials.append(bpy.data.materials['Training_Iron']);parts.append(o)
for k in range(10):
 xx=random.uniform(-.55,.55);zz=random.uniform(.055,.13)
 tube((xx,-.112,zz),(xx+random.uniform(.04,.14),-.113,zz+.006),.0025,bpy.data.materials['Training_Stitch'])
# Join detailing to the existing body so Unity keeps its existing object references.
bpy.ops.object.select_all(action='DESELECT');body.select_set(True)
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
bpy.ops.object.select_all(action='DESELECT')
for o in [root]+list(root.children_recursive):o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=base+'/ArtSource/Tutorial/PracticeScarecrow.blend')
bpy.ops.export_scene.fbx(filepath=base+'/Assets/Models/Tutorial/PracticeScarecrow.fbx',use_selection=True,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True,add_leaf_bones=False,bake_anim=False)
print('Exported upgraded training dummy',sum(len(o.data.polygons) for o in meshes))
