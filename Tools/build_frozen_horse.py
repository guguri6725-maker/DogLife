"""Blender에서 원본 USDZ를 보존하며 얼음 군마 리그와 큰 전투 동작을 제작합니다."""
import bpy, math, os
from mathutils import Vector
ROOT = 'C:/Users/mbc/Desktop/NC_AI/DogLife'
out = ROOT + '/Assets/Art/FrozenKnight'
os.makedirs(out+'/Models',exist_ok=True)
os.makedirs(ROOT+'/ArtSource/FrozenKnight',exist_ok=True)
horse=bpy.data.objects.get('mesh_0')
if horse is None:
    before=set(bpy.data.objects)
    bpy.ops.wm.usd_import(filepath=ROOT+'/Assets/Models/Monster/Winter/FrozenKnight/_Source/WinterBoss_horse.usdz')
    horse=next(o for o in bpy.data.objects if o not in before and o.type=='MESH')
bpy.ops.object.select_all(action='DESELECT');horse.select_set(True);bpy.context.view_layer.objects.active=horse
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
horse.name='IceHorseBody'
arm=bpy.data.armatures.new('IceHorseRig');rig=bpy.data.objects.new('IceHorseRig',arm);bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;horse.select_set(False);rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
spec={
 'Root':((0,.16,.52),(0,0,.58),None),
 'Pelvis':((0,.2,.52),(0,.05,.57),'Root'),
 'Spine':((0,.05,.57),(0,-.16,.61),'Root'),
 'Chest':((0,-.16,.61),(0,-.27,.64),'Spine'),
 'Neck':((0,-.27,.64),(0,-.33,.78),'Chest'),
 'Head':((0,-.33,.78),(0,-.46,.8),'Neck'),
 'Tail':((0,.31,.6),(0,.47,.43),'Pelvis'),
}
for side,x in [('L',.085),('R',-.085)]:
 for end,y,parent in [('Front',-.23,'Chest'),('Hind',.24,'Pelvis')]:
  prefix=end+side
  spec[prefix+'Upper']=((x,y,.53),(x,y+.035,.3),parent)
  spec[prefix+'Lower']=((x,y+.035,.3),(x,y,.085),prefix+'Upper')
  spec[prefix+'Hoof']=((x,y,.085),(x,y-.06,.03),prefix+'Lower')
for name,(head,tail,parent) in spec.items():
 b=arm.edit_bones.new(name);b.head=head;b.tail=tail
 if parent:b.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
for name in spec:horse.vertex_groups.new(name=name)
def distance(p,a,b):
 d=b-a;return (p-(a+d*max(0,min(1,(p-a).dot(d)/d.length_squared)))).length
for v in horse.data.vertices:
 p=v.co
 if p.z<.48 and abs(p.x)>.025:
  prefix=('Front' if p.y<0 else 'Hind')+('L' if p.x>0 else 'R')
  names=[prefix+'Upper',prefix+'Lower',prefix+'Hoof']
  if p.z>.4:names += ['Chest' if p.y<0 else 'Pelvis']
 elif p.y<-.27 and p.z>.58:names=['Chest','Neck','Head']
 elif p.y>.33:names=['Pelvis','Tail']
 else:names=['Pelvis','Spine','Chest','Neck']
 nearest=sorted((distance(p,Vector(spec[n][0]),Vector(spec[n][1])),n) for n in names)[:2]
 weights=[1/max(.012,d)**4 for d,n in nearest];total=sum(weights)
 for (d,n),w in zip(nearest,weights):horse.vertex_groups[n].add([v.index],w/total,'REPLACE')
mod=horse.modifiers.new('IceHorse Skin','ARMATURE');mod.object=rig;horse.parent=rig
rig.animation_data_create()
actions=[('Idle',48),('Gallop',30),('Rear',45),('Stomp',30),('Groggy',48),('Summon',48)]
for name,frames in actions:
 action=bpy.data.actions.new(name);rig.animation_data.action=action
 for f in range(1,frames+1):
  t=(f-1)/(frames-1);phase=t*math.tau
  for p in rig.pose.bones:p.rotation_mode='XYZ';p.rotation_euler=(0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
  if name=='Gallop':
   rig.pose.bones['Root'].location.z=.035*math.sin(phase*2)
   rig.pose.bones['Spine'].rotation_euler.x=.07*math.sin(phase)
   for end,offset in [('Front',0),('Hind',math.pi)]:
    for side,delay in [('L',0),('R',.45)]:
     a=phase+offset+delay
     rig.pose.bones[end+side+'Upper'].rotation_euler.x=.75*math.sin(a)
     rig.pose.bones[end+side+'Lower'].rotation_euler.x=-.85*max(0,math.sin(a+.5))
     rig.pose.bones[end+side+'Hoof'].rotation_euler.x=.3*math.sin(a-.3)
  elif name in ('Rear','Stomp','Summon'):
   amount=(math.sin(t*math.pi*.5) if name=='Rear' else max(0,1-t*2.3) if name=='Stomp' else math.sin(t*math.pi))
   rig.pose.bones['Root'].rotation_euler.x=-.48*amount
   rig.pose.bones['Neck'].rotation_euler.x=.18*amount
   for side in ['L','R']:
    rig.pose.bones['Front'+side+'Upper'].rotation_euler.x=-.8*amount
    rig.pose.bones['Front'+side+'Lower'].rotation_euler.x=-.65*amount
    rig.pose.bones['Hind'+side+'Upper'].rotation_euler.x=.3*amount
  else:
   rig.pose.bones['Spine'].rotation_euler.x=.035*math.sin(phase)
   rig.pose.bones['Neck'].rotation_euler.x=(.22 if name=='Groggy' else 0)+.06*math.sin(phase)
  rig.pose.bones['Tail'].rotation_euler.z=.2*math.sin(phase)
  for p in rig.pose.bones:
   p.keyframe_insert('rotation_euler',frame=f,group=p.name);p.keyframe_insert('location',frame=f,group=p.name)
 action.use_fake_user=True
bpy.context.scene.render.fps=30
rig.animation_data.action=bpy.data.actions.get('Idle');bpy.context.scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');horse.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=out+'/Models/IceHorse_Rigged.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
# VFX용 낮은 폴리곤의 뾰족한 얼음 결정을 별도 모델로 보관합니다.
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.mesh.primitive_cone_add(vertices=6,radius1=.5,radius2=0,depth=1,location=(0,0,.5))
crystal=bpy.context.object;crystal.name='FrostCrystal';bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.export_scene.fbx(filepath=out+'/Models/FrostCrystal.fbx',use_selection=True,bake_anim=False,axis_forward='-Z',axis_up='Y')
crystal.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/FrozenKnight/IceHorse_Rigged.blend')
print('Created',len(spec),'bones and',len(actions),'actions; vertices',len(horse.data.vertices))
