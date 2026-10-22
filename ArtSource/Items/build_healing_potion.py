# Dedicated Blender scene; preserve the existing training dummy scene.
import bpy,math,os
from mathutils import Vector
base=r'C:/Users/mbc/Desktop/NC_AI/DogLife'
s=bpy.data.scenes.new('Bonebound Healing Potion');bpy.context.window.scene=s
root=bpy.data.objects.new('BoneboundHealingPotion',None);s.collection.objects.link(root)
def material(name,col,metal=0,rough=.4):
 m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);m.use_nodes=True;p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(*col,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough;return m
ruby=material('Potion_RubyGlaze',(.23,.012,.022),.28,.19)
brass=material('Potion_AgedBrass',(.29,.16,.055),.78,.32)
bone=material('Potion_OldIvory',(.76,.62,.39),.05,.42)
leather=material('Potion_Leather',(.065,.025,.013),0,.85)
cork=material('Potion_Cork',(.18,.085,.032),0,.9)
# A closed lathed bottle with tapered shoulders, inset neck, and heavy base.
def lathe(name,profile,mat,n=48):
 verts=[];faces=[]
 for z,r in profile:
  for i in range(n):
   a=i*math.tau/n;verts.append((r*math.cos(a),r*math.sin(a),z))
 for j in range(len(profile)-1):
  for i in range(n):a=j*n+i;b=j*n+(i+1)%n;faces.append((a,b,b+n,a+n))
 faces.append(tuple(reversed(range(n))));faces.append(tuple((len(profile)-1)*n+i for i in range(n)))
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new(name,mesh);s.collection.objects.link(o);o.parent=root;o.data.materials.append(mat)
 for p in mesh.polygons:p.use_smooth=len(p.vertices)==4
 return o
lathe('Ruby ceramic flask',[(.025,.11),(.04,.15),(.09,.175),(.21,.19),(.31,.175),(.37,.13),(.405,.068),(.47,.065)],ruby)
lathe('Brass pedestal',[(0,.105),(.012,.135),(.025,.145),(.042,.148),(.052,.135)],brass)
lathe('Raised neck collar',[(.409,.073),(.419,.082),(.442,.082),(.453,.075)],brass)
lathe('Cork stopper',[(.451,.06),(.50,.064),(.523,.061)],cork,32)
lathe('Stopper crown',[(.51,.061),(.52,.069),(.53,.065)],brass,32)
# Thin metal waist bands and leather straps bind the bottle like a small relic.
for z,r in [(.09,.177),(.31,.178)]:lathe('Engraved brass band',[(z-.009,r),(z+.009,r)],brass)
def tube(name,points,r,mat):
 c=bpy.data.curves.new(name,'CURVE');c.dimensions='3D';c.bevel_depth=r;c.bevel_resolution=2;sp=c.splines.new('POLY');sp.points.add(len(points)-1)
 for p,co in zip(sp.points,points):p.co=(*co,1)
 o=bpy.data.objects.new(name,c);s.collection.objects.link(o);o.parent=root;o.data.materials.append(mat);return o
for a in [math.radians(20),math.radians(160),math.radians(260)]:
 points=[(r*math.cos(a),r*math.sin(a),z) for z,r in [(.05,.14),(.12,.182),(.24,.193),(.32,.175),(.38,.115),(.415,.078)]];tube('Leather binding',points,.009,leather)
# Ivory dog-bone charm is readable at inventory icon size.
def sphere(name,loc,scale,mat):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,location=loc);o=bpy.context.object;o.name=name;o.scale=scale;o.parent=root;o.data.materials.append(mat)
 for p in o.data.polygons:p.use_smooth=True
 return o
tube('Ivory charm', [(-.048,-.194,.205),(.048,-.194,.245)],.012,bone)
for x,z in [(-.054,.203),(.054,.247)]:
 for dz in [-.009,.009]:sphere('Carved bone ends',(x,-.194,z+dz),(.017,.011,.012),bone)
tube('Pendant cord',[(0,-.075,.444),(-.02,-.135,.37),(-.025,-.186,.28),(0,-.199,.248)],.0035,brass)
for z,r in [(.09,.18),(.31,.18)]:
 for i in range(16):
  a=i*math.tau/16;sphere('Band rivet',(r*math.cos(a),r*math.sin(a),z),(.004,.004,.004),bone)
# Convert and export only the prop, leaving studio lighting out of the FBX.
bpy.ops.object.select_all(action='DESELECT')
for o in root.children_recursive:o.select_set(True)
bpy.context.view_layer.objects.active=next(o for o in root.children if o.type=='MESH');bpy.ops.object.convert(target='MESH');bpy.ops.object.join();mesh=bpy.context.object;mesh.name='HealingPotion_Mesh'
root.select_set(True)
bpy.ops.export_scene.fbx(filepath=base+'/Assets/Models/Items/HealingPotion/HealingPotion.fbx',use_selection=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
# Render the actual Blender model as the shop and HUD icon.
bpy.ops.object.camera_add(location=(.68,-1.6,.85));cam=bpy.context.object;cam.name='PotionIconCamera';cam.rotation_euler=(Vector((0,0,.26))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=.68;s.camera=cam
for name,loc,power,size in [('WarmKey',(-.7,-1,1.3),27,1),('CoolRim',(.5,.5,.8),39,.7),('SoftFill',(.7,-.6,.3),13.5,.6)]:
 bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.23))-o.location).to_track_quat('-Z','Y').to_euler()
s.world=bpy.data.worlds.new('PotionStudio');s.world.use_nodes=True;next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(.17,.2,.24,1)
s.render.resolution_x=512;s.render.resolution_y=512;s.render.resolution_percentage=100;s.render.film_transparent=True;s.render.image_settings.file_format='PNG';s.render.image_settings.color_mode='RGBA';s.render.filepath=base+'/Assets/Models/Items/HealingPotion/HealingPotionIcon.png'
bpy.ops.wm.save_as_mainfile(filepath=base+'/ArtSource/Items/HealingPotion.blend');bpy.ops.render.render(write_still=True)
print('Potion exported, icon rendered',len(mesh.data.polygons))
