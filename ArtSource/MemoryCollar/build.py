import bpy, math, os, random
from mathutils import Vector
base='C:/unity/DogLife'
scene=bpy.data.scenes.new('DogLife Memory Collar')
bpy.context.window.scene=scene
random.seed(24)
def mat(name,color,metal=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=.67 if not metal else .38
 return m
leather=mat('Memory_Leather',(.18,.045,.028));brass=mat('Memory_Brass',(.52,.31,.11),.75);thread=mat('Memory_Thread',(.53,.36,.20));stone=mat('Memory_Stone',(.15,.19,.20));glow=mat('Memory_Glow',(.14,.6,.85),.2)
# Portable, tileable leather grain, authored in Blender and used by Unity too.
img=bpy.data.images.new('MemoryLeatherGrain',width=256,height=256)
pixels=[]
for y in range(256):
 for x in range(256):
  n=random.random();grain=.75+.22*n+.04*math.sin(x*.9+y*.3)
  pixels.extend([.30*grain,.095*grain,.055*grain,1])
img.pixels.foreach_set(pixels);img.filepath_raw=base+'/Assets/Models/MemoryCollar/LeatherGrain.png';img.file_format='PNG';img.save()
node=leather.node_tree.nodes.new('ShaderNodeTexImage');node.image=img;bs=next(n for n in leather.node_tree.nodes if n.type=='BSDF_PRINCIPLED');leather.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
root=bpy.data.objects.new('MemoryCollar',None);scene.collection.objects.link(root)
def finish(obj,name,material):
 obj.name=name;obj.parent=root;obj.data.materials.append(material)
 for p in obj.data.polygons:p.use_smooth=True
 return obj
def tube(name,points,radius,material,closed=False):
 curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.bevel_depth=radius;curve.bevel_resolution=2;sp=curve.splines.new('POLY');sp.points.add(len(points)-1)
 for p,v in zip(sp.points,points):p.co=(*v,1)
 sp.use_cyclic_u=closed;obj=bpy.data.objects.new(name,curve);scene.collection.objects.link(obj);obj.parent=root;obj.data.materials.append(material);return obj
# Broad oval leather band, floats above a low weathered plinth.
verts=[];faces=[];N=96
for i in range(N):
 a=i*math.tau/N
 for radius,z in [(1,.79),(1,1.00),(.92,.79),(.92,1.00)]:verts.append((.40*math.cos(a)*radius,.31*math.sin(a)*radius,z+.035*math.cos(a)))
for i in range(N):
 j=(i+1)%N
 for a,b in [(0,1),(1,3),(3,2),(2,0)]:faces.append((i*4+a,j*4+a,j*4+b,i*4+b))
mesh=bpy.data.meshes.new('LeatherBandMesh');mesh.from_pydata(verts,[],faces);mesh.update();obj=bpy.data.objects.new('LeatherBand',mesh);scene.collection.objects.link(obj);finish(obj,'LeatherBand',leather)
bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project();bpy.ops.object.mode_set(mode='OBJECT');obj.select_set(False)
bevel=obj.modifiers.new('Soft worn edges','BEVEL');bevel.width=.009;bevel.segments=2
for row in [.815,.975]:
 for i in range(60):
  a=i*math.tau/60;b=a+.045
  tube('LeatherStitch',[(.402*math.cos(t),.312*math.sin(t),row+.035*math.cos(t)) for t in [a,b]],.0028,thread)
# Buckle on the front right, and a hanging blank engraved tag.
tube('BrassBuckle',[(.18,-.303,.79),(.31,-.26,.80),(.31,-.26,1.01),(.18,-.303,1.0)],.018,brass,True)
tube('BucklePin',[(.17,-.32,.9),(.31,-.28,.9)],.009,brass)
tube('TagLink',[(.055*math.cos(a),-.334,.77+.07*math.sin(a)) for a in [i*math.tau/40 for i in range(40)]],.012,brass,True)
bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.125,depth=.025,location=(0,-.355,.58),rotation=(math.pi/2,0,0));tag=finish(bpy.context.object,'NamelessTag',brass);tag.scale=(1,1.15,1);b=tag.modifiers.new('Rounded tag rim','BEVEL');b.width=.008;b.segments=3
# A faint paw emblem instead of a readable name.
for x,z,r in [(0,.57,.035),(-.04,.62,.014),(-.014,.641,.015),(.015,.641,.015),(.04,.62,.014)]:
 bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=1,location=(x,-.374,z));o=finish(bpy.context.object,'SoulPawInlay',glow);o.scale=(r,.004,r)
for i in range(3):
 tube('WornNameScratch', [(-.055+i*.016,-.372,.52),(.034+i*.008,-.372,.525+i*.009)],.002,stone)
for radius,depth,z in [(.57,.10,.05),(.47,.09,.145)]:
 bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=radius,depth=depth,location=(0,0,z));o=finish(bpy.context.object,'WeatheredRestingStone',stone);be=o.modifiers.new('Worn edges','BEVEL');be.width=.025;be.segments=2
for i in range(8):
 a=i*math.tau/8
 tube('SoulRune',[(.35*math.cos(a),.35*math.sin(a),.195),(.40*math.cos(a+.06),.40*math.sin(a+.06),.195)],.006,glow)
# Convert only this authoring scene and export meshes.
bpy.ops.object.select_all(action='DESELECT')
for o in list(scene.objects):
 if o.type in {'MESH','CURVE'}:o.select_set(True)
bpy.context.view_layer.objects.active=tag;bpy.ops.object.convert(target='MESH')
root.select_set(True)
bpy.ops.export_scene.fbx(filepath=base+'/Assets/Models/MemoryCollar/MemoryCollar.fbx',use_selection=True,object_types={'MESH','EMPTY'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True)
bpy.ops.wm.save_as_mainfile(filepath=base+'/ArtSource/MemoryCollar/MemoryCollar.blend')
print('Exported',sum(len(o.data.polygons) for o in scene.objects if o.type=='MESH'),'faces')
