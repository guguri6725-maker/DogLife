import bpy, math, os
from mathutils import Vector, Quaternion, Matrix, Euler
ROOT='C:/unity/DogLife'
rig=bpy.data.objects['HarvestReaper_Rig']; obj=bpy.data.objects['HarvestReaper_Body']; scene=bpy.context.scene
rig.animation_data_clear()
for pb in rig.pose.bones:
 pb.rotation_mode='QUATERNION';pb.matrix_basis.identity()
bpy.context.view_layer.update()
def ease(a,b,t):
 u=max(0,min(1,(t-a)/(b-a)));return u*u*(3-2*u)
# A separate hem control prevents the rigid hip-weighted robe from going underground.
bpy.context.view_layer.objects.active=rig
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for b in rig.data.edit_bones:b.use_connect=False
if 'DeathRobe' not in rig.data.edit_bones:
 b=rig.data.edit_bones.new('DeathRobe');b.head=(0,0,2.94);b.tail=(0,0,3.7);b.parent=rig.data.edit_bones['Root']
bpy.ops.object.mode_set(mode='OBJECT')
group=obj.vertex_groups.get('DeathRobe') or obj.vertex_groups.new(name='DeathRobe')
hipsidx=obj.vertex_groups['Hips'].index
if obj.get('death_skin_revision',0)<2:
 # Restore original hip mass so rerunning the bake never loses influence.
 for v in obj.data.vertices:
  weights={g.group:g.weight for g in v.groups}
  original_hip=weights.get(hipsidx,0)+weights.get(group.index,0)+max(0,1-sum(weights.values()))
  world=obj.matrix_world@v.co
  strength=ease(2.95,2.4,world.z) if world.z<2.95 else 0
  obj.vertex_groups['Hips'].add([v.index],original_hip*(1-strength),'REPLACE')
  group.add([v.index],original_hip*strength,'REPLACE')
  # Preserve rigid detail outside the knee and ankle hinge regions.
  for side in ('L','R'):
   names=[side+n for n in ('Thigh','Shin','Foot')]
   total=sum(weights.get(obj.vertex_groups[n].index,0) for n in names)
   if total<.001:continue
   # Loose outer robe panels must not follow the folding shins.
   a=rig.data.bones[side+'Shin'].head_local;b=rig.data.bones[side+'Shin'].tail_local
   u=max(0,min(1,(world-a).dot(b-a)/(b-a).length_squared))
   distance=(world-(a+(b-a)*u)).length
   cloth=ease(.45,.8,distance)*ease(.42,.85,world.z)
   if cloth>0:
    existing=next((g.weight for g in v.groups if g.group==group.index),0)
    group.add([v.index],existing+total*cloth,'REPLACE');total*=1-cloth
   ankle=ease(.27,.53,world.z);knee=ease(1.43,1.78,world.z)
   values=(knee,(1-knee)*ankle,(1-knee)*(1-ankle))
   for name,value in zip(names,values):obj.vertex_groups[name].add([v.index],total*value,'REPLACE')
 
 obj['death_skin_revision']=2

# Match Unity's four-weight skinning before measuring contact.
for v in obj.data.vertices:
 weighted=sorted([(g.group,g.weight) for g in v.groups if g.weight>0],key=lambda item:item[1],reverse=True)
 keep=weighted[:4];total=sum(w for i,w in keep)
 for i,w in weighted:obj.vertex_groups[i].remove([v.index])
 for i,w in keep:obj.vertex_groups[i].add([v.index],w/total,'REPLACE')

action=bpy.data.actions.new('HarvestReaper_Death_Blender');rig.animation_data_create();rig.animation_data.action=action
scene.render.fps=30;scene.frame_start=1;scene.frame_end=151
rest={p.name:p.bone.matrix_local.copy() for p in rig.pose.bones}
def setrot(name,x=0,y=0,z=0):
 p=rig.pose.bones[name];q=rest[name].to_quaternion()
 p.rotation_quaternion=q.inverted()@Euler((math.radians(-x),math.radians(y),math.radians(z))).to_quaternion()@q
def setpos(name,target):
 p=rig.pose.bones[name];m=p.matrix.copy();m.translation=Vector(target);p.matrix=m;bpy.context.view_layer.update()
def limb(first,middle,end,target,pole):
 a,b,c=[rig.pose.bones[n] for n in (first,middle,end)];start=a.head.copy();target=Vector(target)
 l1=a.bone.length;l2=b.bone.length;axis=(target-start).normalized();d=max(abs(l1-l2)+.001,min((target-start).length,l1+l2-.001))
 along=(l1*l1-l2*l2+d*d)/(2*d);bend=Vector(pole);bend=(bend-axis*bend.dot(axis)).normalized()
 knee=start+axis*along+bend*math.sqrt(max(0,l1*l1-along*along));goal=start+axis*d
 for p,s,e in ((a,start,knee),(b,knee,goal)):
  direction=p.bone.tail_local-p.bone.head_local
  q=direction.rotation_difference(e-s)@rest[p.name].to_quaternion()
  p.matrix=Matrix.Translation(s)@q.to_matrix().to_4x4();bpy.context.view_layer.update()
 return goal
feet={n:rest[n].translation.copy() for n in ['LFoot','RFoot']}
for frame in range(1,152):
 scene.frame_set(frame);t=(frame-1)/30
 for p in rig.pose.bones:p.matrix_basis.identity();p.rotation_mode='QUATERNION'
 brace=ease(.3,1.1,t);r=ease(1.1,1.8,t);l=ease(1.8,2.5,t);fall=ease(2.7,4,t)
 recoil=math.sin(ease(0,.4,t)*math.pi)
 setrot('Hips',x=-70*fall,y=5*fall)
 setrot('Spine',x=4*recoil-10*brace-6*fall)
 setrot('Chest',x=7*recoil-8*brace-5*fall)
 setrot('Head',x=10*recoil-15*brace-8*l+10*fall)
 bpy.context.view_layer.update()
 pelvis=Vector((.12*r*(1-l),-.35*fall,2.94-.8*r-.65*l-.44*fall));setpos('Hips',pelvis)
 for side,kneel in [('R',r),('L',l)]:
  sign=1 if side=='R' else -1
  target=feet[side+'Foot'].lerp(Vector((sign*.68,1.15,.48)),kneel)
  goal=limb(side+'Thigh',side+'Shin',side+'Foot',target,(0,-1,1.1*kneel))
  p=rig.pose.bones[side+'Foot'];p.matrix=Matrix.Translation(goal)@Euler((math.radians(5*kneel),0,0)).to_matrix().to_4x4()@rest[p.name].to_quaternion().to_matrix().to_4x4()
 # Hem spreads slightly while gathering beneath the lowered pelvis.
 robe=rig.pose.bones['DeathRobe'];height=pelvis.z
 robe.matrix=Matrix.Translation((pelvis.x,pelvis.y,height))@Matrix.Diagonal((1+.12*l,1+.18*l,max(.15,(height-.04)/2.9),1))@rest['DeathRobe'].to_quaternion().to_matrix().to_4x4()
 bpy.context.view_layer.update()
 left=rest['LHand'].translation.lerp(Vector((-1.35,-.7,3.5)),brace).lerp(Vector((-1.2,-2.7,.38)),fall)
 limb('LArm','LForearm','LHand',left,(-1,.2,0))
 right=rest['RHand'].translation.lerp(Vector((1.5,-.3,2.8)),r).lerp(Vector((1.1,-2,.4)),fall)
 limb('RArm','RForearm','RHand',right,(1,.2,0))
 # Keep the weapon rigid: it remains planted, then tips onto the floor.
 sc=rig.pose.bones['Scythe'];q=Euler((math.radians(83*fall),math.radians(8*fall),math.radians(-12*brace))).to_quaternion()
 sc.matrix=Matrix.Translation((1.89-.25*brace,-.28-.65*fall,4.095-3.6*fall))@q.to_matrix().to_4x4()@rest['Scythe'].to_quaternion().to_matrix().to_4x4()
 bpy.context.view_layer.update()
 # Measure the evaluated weapon; raise only its rigid bone if its shaft touches the floor.
 deps=bpy.context.evaluated_depsgraph_get();ev=obj.evaluated_get(deps);me=ev.to_mesh()
 wi=obj.vertex_groups['Scythe'].index
 ids=[v.index for v in obj.data.vertices if any(g.group==wi and g.weight>.9 for g in v.groups)]
 low=min((ev.matrix_world@me.vertices[i].co).z for i in ids);ev.to_mesh_clear()
 if low<.025:
  m=sc.matrix.copy();m.translation.z+=.025-low;sc.matrix=m
 if t>4:setrot('Head',x=-23+math.sin((t-4)*18)*math.exp(-(t-4)*6))
 bpy.context.view_layer.update()
 for p in rig.pose.bones:
  for prop in ('location','rotation_quaternion','scale'):p.keyframe_insert(data_path=prop,frame=frame,group=p.name)
for fc in action.fcurves:
 for k in fc.keyframe_points:k.interpolation='LINEAR'
# Textures use the same original image maps.
for mat in obj.data.materials:
 if not mat or not mat.use_nodes:continue
 for n in mat.node_tree.nodes:
  if n.type=='TEX_IMAGE' and n.image:
   suffix='Normal' if 'normal' in n.image.name.lower() else 'BaseColor'
   n.image.filepath=ROOT+'/Assets/Art/HarvestReaper/Textures/HarvestReaper_'+suffix+'.png';n.image.reload()
scene.frame_set(151)
os.makedirs(ROOT+'/ArtSource/HarvestReaper',exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/HarvestReaper/HarvestReaper_Death.blend')
print('Blender death action authored; ready for pose review')
# Final mesh contact bake includes the long hem and the rigid scythe.
for f in range(1,152):
 scene.frame_set(f);root=rig.pose.bones['Root'];root.location=(0,0,0);bpy.context.view_layer.update()
 ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());me=ev.to_mesh()
 low=min((ev.matrix_world@v.co).z for v in me.vertices);ev.to_mesh_clear()
 root.location=root.bone.matrix_local.to_3x3().inverted()@Vector((0,0,max(0,.015-low)))
 root.keyframe_insert(data_path='location',frame=f)
for fc in action.fcurves:
 for k in fc.keyframe_points:k.interpolation='LINEAR'
scene.frame_set(151)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/HarvestReaper/HarvestReaper_Death.blend')
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Models/Monster/Autumn/HarvestReaper/Death.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
