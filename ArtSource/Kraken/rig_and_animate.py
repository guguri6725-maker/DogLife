import bpy,math,os,shutil
from mathutils import Vector,Euler
ROOT='C:/unity/DogLife';OUT=ROOT+'/Assets/Art/Kraken'
for folder in ['Models','Textures','Materials','Animations']:os.makedirs(OUT+'/'+folder,exist_ok=True)
for kind,height in [('Body',8),('Tentacle',8)]:
 src=bpy.data.objects['Kraken_'+kind+'_Source'];obj=src.copy();obj.data=src.data.copy();bpy.context.scene.collection.objects.link(obj);obj.name='Kraken_'+kind;obj.hide_set(False);obj.hide_render=False
 mat=src.matrix_world.copy()
 for v in obj.data.vertices:v.co=mat@v.co*height
 obj.matrix_world.identity()
 # The supplied surface is preserved, including UVs and its silhouette.
 data=bpy.data.armatures.new('Kraken_'+kind+'_Skeleton');rig=bpy.data.objects.new('Kraken_'+kind+'_Rig',data);bpy.context.scene.collection.objects.link(rig)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
 root=data.edit_bones.new('Root');root.head=(0,0,0);root.tail=(0,0,.3)
 count=12 if kind=='Tentacle' else 5
 for i in range(count):
  b=data.edit_bones.new(('Tendril' if kind=='Tentacle' else 'Body')+str(i));b.head=(0,0,height*i/count);b.tail=(0,0,height*(i+1)/count);b.parent=root if i==0 else data.edit_bones[('Tendril' if kind=='Tentacle' else 'Body')+str(i-1)]
 bpy.ops.object.mode_set(mode='OBJECT');data.bones['Root'].use_deform=False
 groups=[obj.vertex_groups.new(name=('Tendril' if kind=='Tentacle' else 'Body')+str(i)) for i in range(count)]
 for v in obj.data.vertices:
  u=max(0,min(count-1,v.co.z/height*count-.5));a=int(u);b=min(count-1,a+1);w=u-a
  groups[a].add([v.index],1-w,'REPLACE')
  if b!=a:groups[b].add([v.index],w,'REPLACE')
 mod=obj.modifiers.new('Kraken skin','ARMATURE');mod.object=rig;obj.parent=rig;rig.animation_data_create()
 states=['Idle','Ready','Strike','Ground','Retract','Dive','Emerge','Bind','Throw'] if kind=='Tentacle' else ['Idle','Command','Roar','Wave','Groggy']
 for state in states:
  act=bpy.data.actions.new(kind+'_'+state);act.use_fake_user=True;rig.animation_data.action=act
  for frame in range(1,41):
   t=(frame-1)/39;e=t*t*(3-2*t)
   for pb in rig.pose.bones:pb.rotation_mode='XYZ';pb.rotation_euler=(0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
   for i in range(count):
    pb=rig.pose.bones[('Tendril' if kind=='Tentacle' else 'Body')+str(i)]
    if kind=='Tentacle':
     bend=0;side=0
     if state=='Idle':bend=.022*math.sin(t*math.tau-i*.4);side=.018*math.cos(t*math.tau-i*.3)
     elif state=='Ready':bend=-.06*e;side=.025*math.sin(i*.5)*e
     elif state=='Strike':bend=(-.06*(1-min(1,t/.3))+.13*min(1,t/.3));rig.pose.bones['Tendril0'].location.z=0
     elif state=='Ground':bend=.13
     elif state=='Retract':bend=.13*(1-e)-.025*e
     elif state=='Dive':bend=.05*e;rig.pose.bones['Root'].location.z=-height*e
     elif state=='Emerge':bend=.045*math.sin(i*.6)*e;rig.pose.bones['Root'].location.z=-height*(1-min(1,t/.3))
     elif state=='Bind':bend=.16;side=.07*math.sin(i*.6+t*math.tau)
     elif state=='Throw':bend=.16-.26*min(1,t/.4)
     pb.rotation_euler=(bend,side,0)
    else:
     amp=.018 if state=='Idle' else .075
     pb.rotation_euler.x=amp*math.sin(t*math.tau-i*.5)
     if state=='Roar':pb.rotation_euler.x=-.1*math.sin(t*math.pi);pb.scale=(1+.025*math.sin(t*math.pi),)*3
     if state=='Command':pb.rotation_euler.x=.06*math.sin(t*math.pi)
     if state=='Wave':pb.rotation_euler.x=-.09*math.cos(t*math.pi)
     if state=='Groggy':pb.rotation_euler.x=.065+.008*math.sin(t*math.tau)
   for pb in rig.pose.bones:
    for prop in ['rotation_euler','location','scale']:pb.keyframe_insert(data_path=prop,frame=frame)
  for fc in act.fcurves:
   for k in fc.keyframe_points:k.interpolation='LINEAR'
  tr=rig.animation_data.nla_tracks.new();tr.strips.new(act.name,1,act)
 rig.animation_data.action=None
 for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=OUT+'/Models/Kraken_'+kind+'.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,axis_forward='-Z',axis_up='Y')
 for tr in rig.animation_data.nla_tracks:tr.mute=True
 for suffix,source in [('BaseColor','base_color'),('Normal','normal')]:shutil.copy2(ROOT+'/ArtSource/Kraken/Original/'+kind+'/model-material_0-'+source+'.png',OUT+'/Textures/'+kind+'_'+suffix+'.png')
 obj.location.x=0 if kind=='Body' else 10;rig.location.x=obj.location.x;obj.location.x=0
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/Kraken/Kraken.blend')
print('Body 6 bones / 5 animations; Tentacle 13 bones / 9 animations exported')
