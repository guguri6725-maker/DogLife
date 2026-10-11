import bpy, math, os
from mathutils import Vector,Euler,Matrix
rig=bpy.data.objects['SpringBoar_Rig']
mesh=bpy.data.objects['SpringBoarPhase1_Body']
source=open('C:/unity/DogLife/ArtSource/SpringBoar/rig_replacement.py',encoding='utf-8-sig').read()
exec(source[source.index('def plant_leg'):source.index('def build')])
rig.animation_data_create()
action=bpy.data.actions.new('SpringBoar_Metamorphosis');action.use_fake_user=True;rig.animation_data.action=action
h=.839379
def ease(a,b,t):
 u=max(0,min(1,(t-a)/(b-a)));return u*u*(3-2*u)
for frame in range(1,152):
 t=(frame-1)/150
 bpy.context.scene.frame_set(frame)
 for pb in rig.pose.bones:
  pb.rotation_mode='QUATERNION';pb.rotation_quaternion=(1,0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
 def rot(n,x=0,y=0,z=0):
  pb=rig.pose.bones[n];q=pb.bone.matrix_local.to_quaternion();pb.rotation_quaternion=q.inverted()@Euler((x,y,z)).to_quaternion()@q
 def loc(n,x=0,y=0,z=0):
  pb=rig.pose.bones[n];pb.location=pb.bone.matrix_local.to_3x3().inverted()@Vector((x,y,z))
 collapse=ease(0,.17,t)*(1-ease(.36,.52,t))
 rear=ease(.36,.59,t)*(1-ease(.70,.79,t))
 impact=math.sin(math.pi*max(0,min(1,(t-.79)/.13)))
 tremble=math.sin(t*math.tau*36)*ease(.20,.28,t)*(1-ease(.44,.55,t))
 roar=ease(.44,.56,t)*(1-ease(.70,.85,t))
 loc('Hips',z=h*(-.035-.18*collapse+.075*rear-.07*impact))
 rot('Spine',x=.12*collapse-.42*rear+.09*impact,z=.015*tremble)
 rot('Chest',x=.17*collapse-.32*rear+.12*impact,z=.024*tremble)
 rot('Neck',x=.27*collapse-.24*roar+.18*rear)
 rot('Head',x=.13*collapse-.20*roar-.1*impact,z=.02*tremble)
 rot('Jaw',x=.72*roar)
 rot('Tail',x=.3*rear,z=.05*math.sin(t*20)*roar)
 for side in ('L','R'):
  rot(side+'FrontUpper',x=-.85*rear);rot(side+'FrontLower',x=1.2*rear);rot(side+'FrontFoot',x=-.35*rear)
  for limb in ('Front','Rear'):
   if limb=='Front' and rear>.03:continue
   target=rig.data.bones[side+limb+'Foot'].head_local.copy()
   plant_leg(rig,side+limb+'Upper',side+limb+'Lower',side+limb+'Foot',target,h)
 for pb in rig.pose.bones:
  for prop in ('rotation_quaternion','location','scale'):pb.keyframe_insert(data_path=prop,frame=frame,group=pb.name)
for fc in action.fcurves:
 for k in fc.keyframe_points:k.interpolation='LINEAR'
bpy.context.scene.render.fps=30;bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=151
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True);mesh.select_set(True);bpy.context.view_layer.objects.active=rig
for tr in rig.animation_data.nla_tracks:tr.mute=True
bpy.ops.export_scene.fbx(filepath='C:/unity/DogLife/Assets/Art/SpringBoar/Phase1/Models/SpringBoarMetamorphosis.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,axis_forward='-Z',axis_up='Y')
bpy.context.scene.frame_set(90)
bpy.ops.wm.save_as_mainfile(filepath='C:/unity/DogLife/ArtSource/SpringBoar/Phase1/SpringBoarMetamorphosis.blend')
print('5 second transition exported')

