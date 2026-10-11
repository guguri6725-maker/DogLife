import bpy, os
from mathutils.kdtree import KDTree
from mathutils import Vector
base='C:/unity/DogLife'
src=bpy.data.objects['Skeleton_SpringBoar_Body']
oldrig=bpy.data.objects['Skeleton_SpringBoar_Rig']
f=bpy.data.objects['Flesh_texture_mapping']
# Match overall proportions without collapsing the flesh onto the skeleton.
for v in f.data.vertices:
    v.co.x*=.294389695/.302859545
    v.co.y*=.49911946/.49116981
    v.co.z*=.839379072/.81409657
rig=oldrig.copy();rig.data=oldrig.data.copy();bpy.context.collection.objects.link(rig)
rig.name='SpringBoar_Rig';rig.hide_set(False);rig.animation_data_clear()
rig.data.pose_position='POSE'
for p in rig.pose.bones:
    p.location=(0,0,0);p.rotation_mode='QUATERNION';p.rotation_quaternion=(1,0,0,0);p.scale=(1,1,1)
f.name='SpringBoarPhase1_Body'
for g in list(f.vertex_groups): f.vertex_groups.remove(g)
groups={g.index:f.vertex_groups.new(name=g.name) for g in src.vertex_groups}
kd=KDTree(len(src.data.vertices))
for v in src.data.vertices: kd.insert(src.matrix_world@v.co,v.index)
kd.balance()
for v in f.data.vertices:
    near=kd.find_n(f.matrix_world@v.co,4);w={}
    for co,idx,d in near:
        q=1/max(d,.001)**2
        for g in src.data.vertices[idx].groups: w[g.group]=w.get(g.group,0)+g.weight*q
    top=sorted(w.items(),key=lambda x:-x[1])[:4];total=sum(a[1] for a in top)
    if total:
        for gi,val in top:groups[gi].add([v.index],val/total,'REPLACE')
f.parent=rig
mod=f.modifiers.new('SpringBoar skeleton','ARMATURE');mod.object=rig
for poly in f.data.polygons:poly.use_smooth=True
out=base+'/Assets/Art/SpringBoar/Phase1'
os.makedirs(out+'/Textures',exist_ok=True);os.makedirs(out+'/Models',exist_ok=True)
for im,filename in [('base_color','SpringBoarPhase1_BaseColor.png'),('normal','SpringBoarPhase1_Normal.png')]:
    img=bpy.data.images.get(im)
    if img:
        img.filepath_raw=out+'/Textures/'+filename;img.file_format='PNG';img.save()
bpy.ops.object.select_all(action='DESELECT')
f.hide_set(False);f.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=out+'/Models/SpringBoarPhase1.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
os.makedirs(base+'/ArtSource/SpringBoar/Phase1',exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=base+'/ArtSource/SpringBoar/Phase1/SpringBoarPhase1.blend')
print('EXPORTED',len(f.data.vertices),'vertices;',len(rig.data.bones),'bones',len(f.vertex_groups),'groups')

