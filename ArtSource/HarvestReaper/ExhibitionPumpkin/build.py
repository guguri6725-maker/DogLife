import bpy, math, os
import numpy as np
from mathutils import Vector

root = 'C:/unity/DogLife'
out = root + '/Assets/Art/ExhibitionPolish/Pumpkin'
os.makedirs(out, exist_ok=True)
scene = bpy.data.scenes.new('Harvest Pumpkin Studio')
bpy.context.window.scene = scene
parts = []

def image_file(name, rgb):
    h,w,_=rgb.shape
    image=bpy.data.images.new(name,width=w,height=h,alpha=True)
    rgba=np.concatenate([np.clip(rgb,0,1),np.ones((h,w,1))],axis=2).astype(np.float32)
    image.pixels.foreach_set(rgba.ravel());image.filepath_raw=out+'/'+name+'.png';image.file_format='PNG';image.save()
    return image

size=1024
v,u=np.mgrid[0:1:complex(size),0:1:complex(size)]
grain=(np.sin(u*423+np.sin(v*59)*2)*np.sin(v*353+u*91)+np.sin(u*917+v*673))*.5
lobes=(.5+.5*np.cos(u*math.tau*10))**.5
streak=np.sin(u*183+np.sin(v*21))*.035
tone=.72+.2*lobes+.06*np.sin(v*math.pi)+streak+grain*.022
dirt=np.clip((np.abs(v-.5)-.28)*2.5,0,.5)
rgb=np.stack([tone*.76-dirt*.22,tone*(.25+.035*lobes)-dirt*.11,tone*.052],axis=2)
skin_image=image_file('HarvestPumpkin_Albedo',rgb)
height=grain*.15+np.sin(u*183+np.sin(v*21))*.1
dy,dx=np.gradient(height);normal=np.stack([-dx*6,-dy*6,np.ones_like(dx)],axis=2);normal/=np.linalg.norm(normal,axis=2,keepdims=True)
image_file('HarvestPumpkin_Normal',normal*.5+.5)

def material(name,col,rough=.6):
    m=bpy.data.materials.new(name);m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(*col,1);p.inputs['Roughness'].default_value=rough
    m.diffuse_color=(*col,1)
    return m,p
skin,p=material('PumpkinRind',(.7,.23,.04))
tex=skin.node_tree.nodes.new('ShaderNodeTexImage');tex.image=skin_image;skin.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
stem,sp=material('PumpkinStem',(.13,.085,.035),.85)
glow,gp=material('PumpkinEmber',(.65,.16,.01),.35);gp.inputs['Emission Color'].default_value=(1,.32,.025,1);gp.inputs['Emission Strength'].default_value=2

verts=[];faces=[];uvs=[]
N,M=80,40
for j in range(M+1):
    t=math.pi*j/M
    for i in range(N+1):
        a=math.tau*i/N
        r=math.sin(t)*(.76+.075*math.cos(a*10))*(1+.025*math.sin(a*3+t*2))
        z=.67+.62*math.cos(t)-.13*math.exp(-(t/.28)**2)+.035*math.cos(a*2)*math.sin(t)
        verts.append((r*math.cos(a),r*math.sin(a),z));uvs.append((i/N,1-j/M))
for j in range(M):
    for i in range(N):
        a=j*(N+1)+i;faces.append((a,a+1,a+N+2,a+N+1))
mesh=bpy.data.meshes.new('Lobed rind mesh');mesh.from_pydata(verts,[],faces);mesh.update()
body=bpy.data.objects.new('HarvestPumpkin_Rind',mesh);scene.collection.objects.link(body);parts.append(body)
mesh.materials.append(skin)
uv=mesh.uv_layers.new(name='UVMap')
for poly in mesh.polygons:
    poly.use_smooth=True
    for k in poly.loop_indices:uv.data[k].uv=uvs[mesh.loops[k].vertex_index]
# Close the pole seam before carving through the front rind.
bpy.context.view_layer.objects.active=body;body.select_set(True)
bm=__import__('bmesh').new();bm.from_mesh(mesh);__import__('bmesh').ops.remove_doubles(bm,verts=list(bm.verts),dist=.0001);__import__('bmesh').ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()

def prism(name,outline,front,back):
    vs=[(x,y,z) for y in [front,back] for x,z in outline];n=len(outline)
    fs=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.update()
    bm=__import__('bmesh').new();bm.from_mesh(me);__import__('bmesh').ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob)
    return ob

outlines=[ [(-.49,.85),(-.13,.78),(-.26,1.02)],[(.13,.78),(.49,.85),(.26,1.02)],
 [(-.4,.57),(-.24,.53),(-.18,.61),(-.07,.51),(.05,.58),(.19,.51),(.29,.57),(.41,.61),(.32,.36),(.15,.32),(0,.35),(-.17,.32),(-.32,.39)] ]
for index,outline in enumerate(outlines):
    cutter=prism('Carve',outline,-1.2,-.38)
    mod=body.modifiers.new('Carved lantern opening','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cutter;mod.solver='EXACT'
    bpy.context.view_layer.objects.active=body;bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter,do_unlink=True)
    inset=prism('HarvestPumpkin_Ember_'+str(index),outline,-.43,-.40);inset.data.materials.append(glow);parts.append(inset)
bevel=body.modifiers.new('Soft cut edges','BEVEL');bevel.width=.012;bevel.segments=2
bpy.ops.object.modifier_apply(modifier=bevel.name)

# Crooked, ribbed stem with a tapered end.
vs=[];fs=[]
for j in range(13):
    t=j/12;center=Vector((.17*t*t,.07*math.sin(t*2),1.13+.48*t))
    for i in range(12):
        a=i*math.tau/12;radius=(.105*(1-t)+.045*t)*(1+.12*math.cos(a*5))
        vs.append(center+Vector((math.cos(a)*radius,math.sin(a)*radius,0)))
for j in range(12):
    for i in range(12):fs.append((j*12+i,j*12+(i+1)%12,(j+1)*12+(i+1)%12,(j+1)*12+i))
fs.extend([tuple(range(11,-1,-1)),tuple(range(144,156))])
me=bpy.data.meshes.new('Curved stem');me.from_pydata(vs,[],fs);me.materials.append(stem);me.update()
ob=bpy.data.objects.new('HarvestPumpkin_Stem',me);scene.collection.objects.link(ob);parts.append(ob)
for p in me.polygons:p.use_smooth=True

bpy.ops.object.select_all(action='DESELECT')
for ob in parts:ob.select_set(True)
bpy.context.view_layer.objects.active=body
bpy.ops.export_scene.fbx(filepath=out+'/HarvestPumpkin.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
scene.world=bpy.data.worlds.new('Pumpkin Studio World');scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.12,.09,.07,1)
for name,loc,power,sz in [('Key',(-3,-4,5),350,4),('Rim',(2,1,3),300,3)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=sz
    light=bpy.data.objects.new(name,data);scene.collection.objects.link(light);light.location=loc;light.rotation_euler=(Vector((0,0,.7))-light.location).to_track_quat('-Z','Y').to_euler()
camera=bpy.data.objects.new('Pumpkin Camera',bpy.data.cameras.new('Pumpkin Camera'));scene.collection.objects.link(camera);camera.location=(2,-3.6,2);camera.rotation_euler=(Vector((0,0,.75))-camera.location).to_track_quat('-Z','Y').to_euler();scene.camera=camera;camera.data.lens=58
scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=root+'/outputs/exhibition-review/pumpkin-blender.png'
os.makedirs(os.path.dirname(scene.render.filepath),exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=root+'/ArtSource/HarvestReaper/ExhibitionPumpkin/HarvestPumpkin.blend',copy=True)
bpy.ops.render.render(write_still=True)
print({'export':out,'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts)})
