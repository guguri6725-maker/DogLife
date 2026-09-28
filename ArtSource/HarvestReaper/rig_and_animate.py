import bpy,math,os,shutil
from mathutils import Vector,Euler,Matrix
ROOT='C:/unity/DogLife';OUT=ROOT+'/Assets/Art/HarvestReaper';h=7
src=bpy.data.objects['HarvestReaper_Source']
for name in ['HarvestReaper_Body','HarvestReaper_Rig']:
 old=bpy.data.objects.get(name)
 if old:bpy.data.objects.remove(old,do_unlink=True)
for action in list(bpy.data.actions):
 if action.name.startswith('HarvestReaper_'):bpy.data.actions.remove(action)
obj=src.copy();obj.data=src.data.copy();bpy.context.scene.collection.objects.link(obj);obj.name='HarvestReaper_Body';obj.hide_render=False;obj.hide_set(False)
mat=src.matrix_world.copy()
for v in obj.data.vertices:v.co=(mat@v.co+Vector((.10,0,0)))*h
obj.matrix_world.identity()
bones=[]
def B(n,a,b,p='Root'):bones.append((n,(Vector(a)+Vector((.10,0,0)))*h,(Vector(b)+Vector((.10,0,0)))*h,p))
B('Root',(-.10,0,0),(-.10,0,.1),None)
B('Hips',(-.10,0,.42),(-.10,0,.53));B('Spine',(-.10,0,.53),(-.10,0,.64),'Hips');B('Chest',(-.10,0,.64),(-.10,0,.73),'Spine');B('Head',(-.10,0,.73),(-.10,0,.87),'Chest')
for side,sign in [('L',-1),('R',1)]:
 x=-.10+sign*.08;ex=-.10+sign*.15;hx=-.10+sign*.23
 if side=='R':hx=.17
 B(side+'Arm',(x,0,.70),(ex,-.02,.60),'Chest');B(side+'Forearm',(ex,-.02,.60),(hx,-.04,.585),side+'Arm');B(side+'Hand',(hx,-.04,.585),(hx+sign*.05,-.04,.585),side+'Forearm')
 B(side+'Thigh',(-.10+sign*.065,0,.43),(-.10+sign*.10,0,.23),'Hips');B(side+'Shin',(-.10+sign*.10,0,.23),(-.10+sign*.14,-.02,.05),side+'Thigh');B(side+'Foot',(-.10+sign*.14,-.02,.05),(-.10+sign*.15,-.10,.035),side+'Shin')
B('Scythe',(.17,-.04,.585),(.08,-.04,.92),'RHand')
data=bpy.data.armatures.new('HarvestReaper_Skeleton');rig=bpy.data.objects.new('HarvestReaper_Rig',data);bpy.context.scene.collection.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for n,a,b,p in bones:
 e=data.edit_bones.new(n);e.head=a;e.tail=b
 if p:e.parent=data.edit_bones[p]
bpy.ops.object.mode_set(mode='OBJECT');data.bones['Root'].use_deform=False
groups={n:obj.vertex_groups.new(name=n) for n,a,b,p in bones if n!='Root'}
weapon=[]
for v in obj.data.vertices:
 x,y,z=v.co/h;x-=.10
 # Rigid scythe shaft, blade and lantern; preserve their shape through all poses.
 isweapon=(z>.88 and x>-.075) or (z>.68 and x>.07) or (z>.55 and x>.29) or (abs(x-(.325-.27*z))<.052 and abs(y+.035)<.065)
 if isweapon:groups['Scythe'].add([v.index],1,'REPLACE');weapon.append(v.index);continue
 if z>.73:candidates=['Head']
 elif z<.46:
  if z<.25 and abs(x+.10)>.065:candidates=[('L' if x<-.10 else 'R')+n for n in ['Thigh','Shin','Foot']]
  else:candidates=['Hips']
 elif x<-.23 and z<.72:candidates=['LArm','LForearm','LHand']
 elif x>.045 and z<.73:candidates=['RArm','RForearm','RHand']
 else:candidates=['Hips','Spine','Chest']
 near=[]
 for n,a,b,p in bones:
  if n not in candidates:continue
  ab=b-a;t=max(0,min(1,(v.co-a).dot(ab)/ab.length_squared));near.append(((v.co-a-ab*t).length,n))
 near=sorted(near)[:2];weights=[math.exp(-d*25/h) for d,n in near];total=sum(weights)
 for (d,n),w in zip(near,weights):groups[n].add([v.index],w/total,'REPLACE')
scope={'__name__':'helpers'};exec(compile(open(ROOT+'/ArtSource/SummerMonsters/rig_and_animate.py',encoding='utf-8-sig').read(),'helpers','exec'),scope);scope['smooth_weights'](obj,h)
# Rigid weapon surfaces never interpolate with cape vertices. Split the
# geometry at the weapon/body boundary while retaining original UV loops.
import bmesh
bm=bmesh.new();bm.from_mesh(obj.data)
layer=bm.verts.layers.deform.verify()
scythe_index=groups['Scythe'].index
selected=[]
for face in bm.faces:
 c=face.calc_center_median()/h;x=c.x-.10;y=c.y;z=c.z
 if (z>.88 and x>-.075) or (z>.68 and x>.07) or (z>.55 and x>.29) or (abs(x-(.325-.27*z))<.052 and abs(y+.035)<.065):selected.append(face)
selected=set(selected)
boundary=set(e for f in selected for e in f.edges if any(other not in selected for other in e.link_faces))
bmesh.ops.split_edges(bm,edges=list(boundary))
weaponverts=set(v for f in selected for v in f.verts)
for v in bm.verts:
 weights=v[layer]
 if v in weaponverts:weights.clear();weights[scythe_index]=1
 else:
  if scythe_index in weights:del weights[scythe_index]
  total=sum(weights.values())
  if total<.0001:weights[groups['Hips'].index]=1
  else:
   for k in list(weights.keys()):weights[k]/=total
bm.to_mesh(obj.data);bm.free()
mod=obj.modifiers.new('Harvest skin','ARMATURE');mod.object=rig;obj.parent=rig
rig.animation_data_create()
def grip_pose(target,delta,side="R"):
 # Bake an arm IK solution. Weapon and palm share the same grip pivot;
 # the scythe never rotates independently inside the fingers.
 bpy.context.view_layer.update()
 upper=rig.pose.bones[side+'Arm'];lower=rig.pose.bones[side+'Forearm'];hand=rig.pose.bones[side+'Hand']
 a=upper.head.copy();d=target-a;l1=upper.bone.length;l2=lower.bone.length
 distance=max(.001,min(d.length,(l1+l2)*.995));direction=d.normalized()
 bend=Vector((1 if side=="R" else -1,.5,0));bend=(bend-direction*bend.dot(direction)).normalized()
 along=(l1*l1-l2*l2+distance*distance)/(2*distance)
 elbow=a+direction*along+bend*math.sqrt(max(0,l1*l1-along*along));wrist=a+direction*distance
 for pb,start,end in [(upper,a,elbow),(lower,elbow,wrist)]:
  rest=pb.bone.tail_local-pb.bone.head_local
  q=rest.rotation_difference(end-start)@pb.bone.matrix_local.to_quaternion()
  pb.matrix=Matrix.Translation(start)@q.to_matrix().to_4x4();bpy.context.view_layer.update()
 hand.matrix=Matrix.Translation(wrist)@(delta@hand.bone.matrix_local.to_quaternion()).to_matrix().to_4x4()
 rig.pose.bones['Scythe'].rotation_quaternion=(1,0,0,0)
 bpy.context.view_layer.update()
def extend_grip(delta,amount):
 # Slide the existing rigid weapon through the palm: the palm now holds the lower shaft.
 weapon=rig.pose.bones['Scythe'];m=weapon.matrix.copy()
 m.translation += delta@Vector((-.09,0,.335)).normalized()*amount
 weapon.matrix=m;bpy.context.view_layer.update()

states=['Idle','SweepReady','SweepAttack','CleaveReady','CleaveAttack','SlamReady','SlamAttack','SummonReady','SummonAttack','SpecialReady','SpecialAttack','SpecialHold','Recovery']
for state in states:
 action=bpy.data.actions.new('HarvestReaper_'+state);rig.animation_data.action=action;action.use_fake_user=True
 for frame in range(1,41):
  t=(frame-1)/39;e=t*t*(3-2*t);pulse=math.sin(t*math.pi)
  for p in rig.pose.bones:p.rotation_mode='QUATERNION';p.rotation_quaternion=(1,0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
  def rot(n,x=0,y=0,z=0):
   p=rig.pose.bones[n];q=p.bone.matrix_local.to_quaternion();p.rotation_quaternion=q.inverted()@Euler((x,y,z)).to_quaternion()@q
  def loc(n,z):
   p=rig.pose.bones[n];p.location=p.bone.matrix_local.to_3x3().inverted()@Vector((0,0,z*h))
  rot('Chest',x=.015*math.sin(t*math.tau));rot('Head',z=.025*math.sin(t*math.tau))
  if state.startswith('Sweep'):
   swing=e if state.endswith('Ready') else 1-2*min(1,t/.7)
   rot('Spine',z=-.25*swing);rot('Chest',z=-.45*swing);rot('Head',z=.2*swing)
   rot('RArm',x=-.7,z=-.7*swing);rot('RForearm',x=-.35,z=-.3*swing);rot('Scythe',y=-1.05,z=-.25*swing);rot('LArm',x=-.5,z=.3*swing)
  elif state.startswith('Cleave'):
   raiseup=e if state.endswith('Ready') else max(0,1-t/.45);smash=0 if state.endswith('Ready') else math.sin(math.pi*min(1,t/.7))
   rot('Spine',x=-.15*raiseup+.25*smash);rot('Chest',x=-.18*raiseup+.3*smash);rot('RArm',x=-2.2*raiseup-1.0*smash);rot('RForearm',x=-.4*raiseup);rot('Scythe',x=1.6*raiseup-.4*smash);rot('LArm',x=-1.4*raiseup-.6*smash)
  elif state.startswith('Slam'):
   up=e if state.endswith('Ready') else max(0,1-t/.35);down=0 if state.endswith('Ready') else pulse
   loc('Hips',.02*up-.05*down);rot('Chest',x=-.12*up+.35*down);rot('RArm',x=-1.4*up-.7*down);rot('Scythe',x=-.3*down);rot('LArm',x=-.6*up,z=.3*down)
  elif state.startswith('Summon'):
   u=e if state.endswith('Ready') else 1-.25*e
   rot('Chest',x=-.15*u);rot('Head',x=-.15*u);rot('LArm',x=-1.35*u,z=.7*u);rot('LForearm',x=-.35*u);rot('RArm',x=-.3*u);rot('Scythe',z=.15*u)
  elif state.startswith('Special'):
   u=e if state=='SpecialReady' else 1
   loc('Hips',-.045*u);rot('Spine',x=.2*u);rot('Chest',x=.3*u);rot('Head',x=-.25*u);rot('RArm',x=-.9*u);rot('RForearm',x=-.3*u);rot('Scythe',x=-.25*u);rot('LArm',x=-.9*u,z=.25*u)
  elif state=='Recovery':rot('Chest',x=.15*(1-e));rot('RArm',x=-.4*(1-e))
  # Author the weapon path in armature/world axes rather than accumulating
  # shoulder + elbow + wrist + scythe Euler rotations.
  if state.startswith('Sweep'):
   # Slow loaded anticipation, fast cutting stroke through the .35 damage frame,
   # then a weighted follow-through. Spine, hips and off-hand drive the scythe.
   if state.endswith('Ready'):
    angle=math.radians(-135)*e;load=-e;extension=2.0*e
   else:
    u=max(0,min(1,(t-.08)/.54));u=u*u*(3-2*u)
    angle=math.radians(-135+220*u);load=-1+2*u;extension=2.0
   rot('Hips',z=.20*load);rot('Spine',z=.38*load,x=.06)
   rot('Chest',z=.36*load,x=.09);rot('Head',z=-.35*load)
   loc('Hips',-.025*(1-abs(load)))
   delta=Euler((math.pi*.5,0,angle)).to_quaternion()
   target=Vector((.04+.18*math.sin(angle),-.25-.05*math.cos(angle),.56+.03*abs(load)))*h
   grip_pose(target,delta)
   shaft=delta@Vector((-.09,0,.335)).normalized()
   grip_pose(rig.pose.bones['RHand'].head+shaft*.48,delta,'L')
   extend_grip(delta,extension)
  elif state.startswith('Cleave'):
   if state.endswith('Ready'):
    u=e;strike=0;extension=2.0*e
   else:
    v=max(0,min(1,(t-.05)/.30));v=v*v*(3-2*v);u=1-v;strike=v;extension=2.0
   rot('Hips',x=.10*strike);rot('Spine',x=-.18*u+.24*strike)
   rot('Chest',x=-.22*u+.48*strike);rot('Head',x=.12*u-.16*strike)
   loc('Hips',.025*u-.10*strike)
   # Align the shaft vertically, then turn the blade into the sagittal cutting plane.
   # Previously the blade face stayed broadside to the downward stroke.
   pitch=-.45*u+1.98*strike
   roll=1 if state.endswith('Attack') else e
   delta=Euler((pitch,0,0)).to_quaternion()@Euler((0,0,-math.pi*.5*roll)).to_quaternion()@Euler((0,math.atan2(.09,.335)*roll,0)).to_quaternion()
   target=Vector((-.04,-.14-.23*strike,.36+.42*u))*h
   grip_pose(target,delta)
   shaft=delta@Vector((-.09,0,.335)).normalized()
   grip_pose(rig.pose.bones['RHand'].head+shaft*.48,delta,'L')
   extend_grip(delta,extension)
  elif state.startswith('Slam'):
   u=e if state.endswith('Ready') else max(0,1-t/.35)
   grip_pose(Vector((.20,-.16,.53+.20*u))*h,Euler((.12,0,0)).to_quaternion())
  elif state.startswith('Special'):
   u=e if state=='SpecialReady' else 1
   grip_pose(Vector((.20,-.16,.585-.07*u))*h,Euler((.10*u,0,0)).to_quaternion())
  else:
   rig.pose.bones['Scythe'].rotation_quaternion=(1,0,0,0)
  for p in rig.pose.bones:
   for prop in ('rotation_quaternion','location','scale'):p.keyframe_insert(data_path=prop,frame=frame,group=p.name)
 for fc in action.fcurves:
  for key in fc.keyframe_points:key.interpolation='LINEAR'
 track=rig.animation_data.nla_tracks.new();track.name=action.name;track.strips.new(action.name,1,action)
rig.animation_data.action=None
for p in rig.pose.bones:p.rotation_quaternion=(1,0,0,0);p.location=(0,0,0)
for folder in ['Models','Textures','Animations','Materials']:os.makedirs(OUT+'/'+folder,exist_ok=True)
for suffix,source in [('BaseColor','base_color'),('Normal','normal')]:shutil.copy2(ROOT+'/ArtSource/HarvestReaper/Original/model-material_0-'+source+'.png',OUT+'/Textures/HarvestReaper_'+suffix+'.png')
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=OUT+'/Models/HarvestReaper.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,axis_forward='-Z',axis_up='Y')
for track in rig.animation_data.nla_tracks:track.mute=True
src.hide_set(True);src.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/HarvestReaper/HarvestReaper.blend')
print('Rig ready',len(bones),'bones',len(obj.data.polygons),'polygons',states)


