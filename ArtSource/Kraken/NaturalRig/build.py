import bpy, math, os, statistics, json
from mathutils import Vector
ROOT='C:/unity/DogLife'
scene=bpy.context.scene
obj=next(o for o in scene.objects if o.type=='MESH')
old=next(o for o in scene.objects if o.type=='ARMATURE')
original=[tuple(v.co) for v in obj.data.vertices]
uv=[tuple(x.uv) for x in obj.data.uv_layers.active.data]
obj.parent=None
for mod in list(obj.modifiers):
 if mod.type=='ARMATURE':obj.modifiers.remove(mod)
bpy.data.objects.remove(old,do_unlink=True)
obj.vertex_groups.clear()
data=bpy.data.armatures.new('Tentacle_NaturalSkeleton');rig=bpy.data.objects.new('Kraken_Tentacle_Rig',data);scene.collection.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
root=data.edit_bones.new('Root');root.head=(0,0,0);root.tail=(0,0,.3);root.use_deform=False
count=18;points=[]
for i in range(count+1):
 z=8*i/count;vs=[v.co for v in obj.data.vertices if abs(v.co.z-z)<.34]
 points.append(Vector((statistics.median(v.x for v in vs),statistics.median(v.y for v in vs),z)))
for _ in range(3):
 points=[points[0]]+[(points[i-1]+points[i]*2+points[i+1])/4 for i in range(1,count)]+[points[-1]]
for i in range(count):
 b=data.edit_bones.new('Tendril'+str(i));b.head=points[i];b.tail=points[i+1];b.parent=root if i==0 else data.edit_bones['Tendril'+str(i-1)];b.use_connect=i>0;b.align_roll(Vector((0,1,0)))
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True
groups=[obj.vertex_groups.new(name='Tendril'+str(i)) for i in range(count)]
for v in obj.data.vertices:
 q=max(0,min(count-1,v.co.z/8*count-.5));weights=[]
 for i in range(max(0,int(q)-1),min(count,int(q)+3)):
  d=abs(q-i);w=(2/3-d*d+.5*d*d*d) if d<1 else ((2-d)**3/6 if d<2 else 0)
  if w>0:weights.append((i,w))
 total=sum(w for i,w in weights)
 for i,w in weights:groups[i].add([v.index],w/total,'REPLACE')
mod=obj.modifiers.new('Continuous four-weight skin','ARMATURE');mod.object=rig;mod.use_deform_preserve_volume=False;obj.parent=rig
# Material image paths are repaired to existing project files; UVs and pixels remain unchanged.
for mat in obj.data.materials:
 if mat and mat.use_nodes:
  for node in mat.node_tree.nodes:
   if node.type=='TEX_IMAGE' and node.image:
    suffix='Normal' if 'normal' in node.image.name.lower() else 'BaseColor'
    node.image.filepath=ROOT+'/Assets/Art/SummerBossAssembly/Textures/AttackTentacle_'+suffix+'.png';node.image.reload()
rig.animation_data_create()
for name,duration in [('Idle',6),('Flow',8),('Settle',3)]:
 action=bpy.data.actions.new('NaturalTentacle_'+name);action.use_fake_user=True;rig.animation_data.action=action
 for f in range(int(duration*30)+1):
  t=f/(duration*30)
  for i in range(count):
   p=rig.pose.bones['Tendril'+str(i)];p.rotation_mode='XYZ';s=i/(count-1);amp=(.10+.90*s*s)*(0 if i==0 else 1)
   phase=t*math.tau-s*3.8
   if name=='Idle':x=math.radians(2.6)*amp*math.sin(phase);z=math.radians(1.3)*amp*math.cos(phase)
   elif name=='Flow':x=math.radians(6)*amp*math.sin(phase);z=math.radians(3)*amp*math.sin(phase-.7)
   else:x=math.radians(4)*amp*math.exp(-5*t)*math.sin(t*math.pi*4-s*2);z=0
   p.rotation_euler=(x,0,z);p.keyframe_insert('rotation_euler',frame=f+1)
 for fc in action.fcurves:
  for k in fc.keyframe_points:k.interpolation='LINEAR'
 track=rig.animation_data.nla_tracks.new();track.strips.new(action.name,1,action);track.mute=True
rig.animation_data.action=None
for p in rig.pose.bones:p.matrix_basis.identity()
scene.frame_start=1;scene.frame_end=181;scene.render.fps=30
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=rig
out=ROOT+'/Assets/Art/Kraken/Models/Kraken_Tentacle_Natural.fbx'
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,axis_forward='-Z',axis_up='Y')
rig.animation_data.action=bpy.data.actions['NaturalTentacle_Idle']
scene.frame_set(45)
bpy.data.libraries.write(ROOT+'/ArtSource/Kraken/NaturalRig/Tentacle_Natural.blend',{scene},fake_user=True)
report={'deform_bones':count,'vertices_unchanged':original==[tuple(v.co) for v in obj.data.vertices],'uv_unchanged':uv==[tuple(x.uv) for x in obj.data.uv_layers.active.data],'unweighted':sum(not v.groups for v in obj.data.vertices),'max_weights':max(len(v.groups) for v in obj.data.vertices)}
open(ROOT+'/ArtSource/Kraken/NaturalRig/validation.json','w').write(json.dumps(report,indent=2))
print(report)
