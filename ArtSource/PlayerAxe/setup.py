import bpy,json,math
from mathutils import Matrix,Vector,Quaternion
BASE='C:/unity/DogLife/'
data=json.load(open(BASE+'ArtSource/PlayerAxe/rig-reference.json'))
frame=json.load(open(BASE+'ArtSource/PlayerAxe/model-frame.json'))
q=frame['q']; original_q=Quaternion((q[3],q[0],q[1],q[2])); original_p=Vector(frame['p'])
C=Matrix(((1,0,0),(0,0,-1),(0,1,0))).to_quaternion()
norm=C@original_q.inverted()
def point(v):return norm@(Vector(v)-original_p)
def rotation(v):return norm@Quaternion((v[3],v[0],v[1],v[2]))
scene=bpy.data.scenes.new('DogLife Axe Authoring');bpy.context.window.scene=scene
armdata=bpy.data.armatures.new('DogLife axe source rig');rig=bpy.data.objects.new('DogLife_AxeRig',armdata);scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
valid=set(data['meshes'][0]['bones']);records=[b for b in data['bones'] if b['name'] in valid]
for b in records:
    bone=armdata.edit_bones.new(b['name']);bone.head=point(b['p']);bone.tail=bone.head+rotation(b['q'])@Vector((0,.08,0));bone.matrix=Matrix.LocRotScale(point(b['p']),rotation(b['q']),Vector((1,1,1)));bone.length=.08
for b in records:
    if b['parent'] in valid:armdata.edit_bones[b['name']].parent=armdata.edit_bones[b['parent']]
bpy.ops.object.mode_set(mode='OBJECT')
for record in data['meshes']:
    mesh=bpy.data.meshes.new(record['name']);faces=[];matindices=[]
    for i,tris in enumerate(record['triangles']):
        faces.extend([tuple(tris[j:j+3]) for j in range(0,len(tris),3)]);matindices.extend([i]*(len(tris)//3))
    mesh.from_pydata([point(v) for v in record['vertices']],[],faces);mesh.update()
    obj=bpy.data.objects.new(record['name'],mesh);scene.collection.objects.link(obj)
    uv=mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:uv.data[loop.index].uv=record['uv'][loop.vertex_index]
    for path in record['textures']:
        mat=bpy.data.materials.new(record['name']+' Surface');mat.use_nodes=True;node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');node.inputs['Roughness'].default_value=.8
        if path:
            tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(BASE+path,check_existing=True);mat.node_tree.links.new(tex.outputs['Color'],node.inputs['Base Color'])
        mesh.materials.append(mat)
    for poly,mi in zip(mesh.polygons,matindices):poly.material_index=mi;poly.use_smooth=True
    for name in record['bones']:obj.vertex_groups.new(name=name)
    for i,(weights,indices) in enumerate(zip(record['weights'],record['indices'])):
        for w,idx in zip(weights,indices):
            if w>0:obj.vertex_groups[idx].add([i],w,'REPLACE')
    modifier=obj.modifiers.new('Original skin weights','ARMATURE');modifier.object=rig
    # Linear skinning matches Unity, so volume defects remain visible during QA.
    modifier.use_deform_preserve_volume=False
for record in data['weapon']:
    mesh=bpy.data.meshes.new('Axe reference');mesh.from_pydata([point(v) for v in record['vertices']],[],[tuple(record['triangles'][j:j+3]) for j in range(0,len(record['triangles']),3)]);mesh.update()
    axe=bpy.data.objects.new('Held Bone Axe',mesh);scene.collection.objects.link(axe)
    g=axe.vertex_groups.new(name='RightHand');g.add(list(range(len(mesh.vertices))),1,'REPLACE');mod=axe.modifiers.new('Hand attachment','ARMATURE');mod.object=rig
    mat=bpy.data.materials.new('Ivory axe');mat.diffuse_color=(.8,.7,.5,1);mesh.materials.append(mat)
rig.show_in_front=True
scene.render.fps=60;scene.frame_start=0;scene.frame_end=84
bpy.ops.wm.save_as_mainfile(filepath=BASE+'ArtSource/PlayerAxe/DogLife_Axe_Authoring.blend')
print('Rig ready',[(b.name,tuple(round(x,3) for x in b.head_local)) for b in armdata.bones])
