import bpy, bmesh, math, os, shutil, json
from mathutils import Vector, Quaternion, Euler, Matrix
ROOT='C:/unity/DogLife'
OUT=ROOT+'/Assets/Art/SpringBoar'
NAMES=['SpringBoar']
# Reuse the proven topology smoothing utility without running the summer builder.
scope={'__name__':'weight_helpers'}
exec(compile(open(ROOT+'/ArtSource/SummerMonsters/rig_and_animate.py',encoding='utf-8-sig').read(),'weight_helpers','exec'),scope)


def plant_leg(rig, upper_name, lower_name, foot_name, target, h):
 # Solve in armature space, then bake local rotations; no runtime IK required.
 bpy.context.view_layer.update()
 upper=rig.pose.bones[upper_name];lower=rig.pose.bones[lower_name];foot=rig.pose.bones[foot_name]
 hip=upper.head.copy();delta=target-hip
 l1=upper.bone.length;l2=lower.bone.length;distance=min(delta.length,(l1+l2)*.995)
 direction=delta.normalized();bend=Vector((0,-1,0));bend=(bend-direction*bend.dot(direction)).normalized()
 along=(l1*l1-l2*l2+distance*distance)/(2*max(.001,distance));height=math.sqrt(max(0,l1*l1-along*along))
 knee=hip+direction*along+bend*height;ankle=hip+direction*distance
 for pb,a,b in [(upper,hip,knee),(lower,knee,ankle)]:
  rest=pb.bone.tail_local-pb.bone.head_local
  q=rest.rotation_difference(b-a)@pb.bone.matrix_local.to_quaternion()
  pb.matrix=Matrix.Translation(a)@q.to_matrix().to_4x4()
  bpy.context.view_layer.update()
 foot.matrix=Matrix.Translation(ankle)@foot.bone.matrix_local.to_quaternion().to_matrix().to_4x4()
 bpy.context.view_layer.update()

def build(name):
 for suffix in ('_Body','_Rig'):
  old=bpy.context.scene.objects.get(name+suffix)
  if old:bpy.data.objects.remove(old,do_unlink=True)
 for action in list(bpy.data.actions):
  if action.name.startswith(name+'_'):bpy.data.actions.remove(action)
 h=.839379
 src=bpy.data.objects['SpringBoss_Source'];obj=src.copy();obj.data=src.data.copy();bpy.context.collection.objects.link(obj);obj.name=name+'_Body'
 mat=src.matrix_world.to_3x3()
 for v in obj.data.vertices:v.co=mat@v.co
 obj.rotation_euler=(0,0,0);obj.location=(0,0,0);obj.scale=(1,1,1)
 lo=min(v.co.z for v in obj.data.vertices);hi=max(v.co.z for v in obj.data.vertices)
 for v in obj.data.vertices:
  v.co=(v.co-Vector((0,0,lo)))*(h/(hi-lo))
  if name=='FrostMammoth':v.co.x=-v.co.x;v.co.y=-v.co.y
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 # Preserve the source triangles, UV seams and authored surface normals.
 bones=[]
 def B(n,a,b,p='Root'):bones.append((n,Vector(a)*h,Vector(b)*h,p))
 B('Root',(0,0,0),(0,0,.1),None)
 human=name in ('IceGolem','SnowSpirit')
 if human:
  B('Hips',(0,0,.38),(0,0,.50));B('Spine',(0,0,.50),(0,0,.63),'Hips');B('Chest',(0,0,.63),(0,0,.74),'Spine');B('Neck',(0,0,.74),(0,0,.79),'Chest');B('Head',(0,0,.79),(0,0,.97),'Neck')
  for side,s in [('L',1),('R',-1)]:
   B(side+'Shoulder',(s*.06,0,.70),(s*.17,0,.69),'Chest')
   B(side+'UpperArm',(s*.17,0,.69),(s*.29,-.02,.59),side+'Shoulder')
   B(side+'Forearm',(s*.29,-.02,.59),(s*.34,-.09,.46),side+'UpperArm')
   B(side+'Hand',(s*.34,-.09,.46),(s*.35,-.15,.39),side+'Forearm')
   B(side+'Thigh',(s*.12,0,.41),(s*.16,-.025,.23),'Hips');B(side+'Shin',(s*.16,-.025,.23),(s*.17,0,.07),side+'Thigh');B(side+'Foot',(s*.17,0,.07),(s*.17,-.13,.04),side+'Shin')
 else:
  B('Hips',(0,.22,.44),(0,.10,.48));B('Spine',(0,.10,.48),(0,-.08,.53),'Hips');B('Chest',(0,-.08,.53),(0,-.19,.61),'Spine')
  B('Neck',(0,-.19,.61),(0,-.32,.65),'Chest');B('Head',(0,-.32,.65),(0,-.48,.52),'Neck');B('Jaw',(0,-.40,.45),(0,-.53,.38),'Head')
  B('Tail',(0,.28,.47),(0,.48,.42),'Hips')
  if name=='FrostMammoth':B('Trunk',(0,-.50,.50),(0,-.52,.23),'Head')
  for side,s in [('L',1),('R',-1)]:
   for label,y,knee_y,ankle_y in [('Front',-.16,-.22,-.34),('Rear',.30,.40,.48)]:
    B(side+label+'Upper',(s*.21,y,.52),(s*.26,knee_y,.27),'Chest' if label=='Front' else 'Hips')
    B(side+label+'Lower',(s*.26,knee_y,.27),(s*.29,ankle_y,.07),side+label+'Upper');B(side+label+'Foot',(s*.29,ankle_y,.07),(s*.29,ankle_y-.08,.025),side+label+'Lower')
 data=bpy.data.armatures.new(name+'_Skeleton');rig=bpy.data.objects.new(name+'_Rig',data);bpy.context.collection.objects.link(rig)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
 for n,a,b,p in bones:
  eb=data.edit_bones.new(n);eb.head=a;eb.tail=b
  if p:eb.parent=data.edit_bones[p]
 bpy.ops.object.mode_set(mode='OBJECT')
 groups={n:obj.vertex_groups.new(name=n) for n,a,b,p in bones if n!='Root'}
 for v in obj.data.vertices:
  x,y,z=v.co/h;side='L' if x>0 else 'R';cand=[b for b in bones if b[0]!='Root']
  if human:
   if z>.76:cand=[b for b in cand if b[0] in ('Head','Neck','Chest')]
   elif (abs(x)>.29 and z>.37) or (abs(x)>.22 and z>.54):cand=[b for b in cand if b[0].startswith(side) and any(q in b[0] for q in ['Arm','arm','Hand','Shoulder'])]
   elif z<.46:cand=[b for b in cand if b[0].startswith(side) and any(q in b[0] for q in ['Thigh','Shin','Foot'])] if name=='IceGolem' else [b for b in cand if b[0]=='Hips']
   else:cand=[b for b in cand if b[0] in ('Hips','Spine','Chest','Neck')]
  else:
   if y<(-.44+.25*z) and z>.12:cand=[b for b in cand if b[0]=='Head']
   elif z<.48 and abs(x)>.14:cand=[b for b in cand if b[0].startswith(side+('Front' if y<0 else 'Rear'))]
   elif y>.34:cand=[b for b in cand if b[0] in ('Tail','Hips')]
   else:cand=[b for b in cand if b[0] in ('Hips','Spine','Chest','Neck','Head')]
  near=[]
  for n,a,b,p in cand:
   ab=b-a;t=max(0,min(1,(v.co-a).dot(ab)/ab.length_squared));near.append(((v.co-a-ab*t).length,n))
  near=sorted(near)[:2];ws=[math.exp(-d*40/h) for d,n in near];total=sum(ws)
  for (d,n),w in zip(near,ws):groups[n].add([v.index],w/total,'REPLACE')
 scope['smooth_weights'](obj,h)
 # The entire skull/tusks move as a rigid unit. Regional nearest-bone
 # weighting otherwise stretches the tall tusks between Head and Neck/Jaw.
 for v in obj.data.vertices:
  x,y,z=v.co/h
  amount=max(0,min(1,(-y-.44+.25*z)/.035))*max(0,min(1,(z-.10)/.05))
  amount=amount*amount*(3-2*amount)
  if amount>0:
   weights={obj.vertex_groups[e.group].name:e.weight*(1-amount) for e in v.groups}
   weights['Head']=weights.get('Head',0)+amount
   for g in obj.vertex_groups:g.remove([v.index])
   weights=sorted(weights.items(),key=lambda p:p[1],reverse=True)[:4]
   total=sum(w for n,w in weights)
   for n,w in weights:
    if w>1e-6:obj.vertex_groups[n].add([v.index],w/total,'REPLACE')

 modifier=obj.modifiers.new('Winter skin','ARMATURE');modifier.object=rig;obj.parent=rig
 data.bones['Root'].use_deform=False
 for folder in ('Models','Textures'):os.makedirs(OUT+'/'+folder,exist_ok=True)
 for suffix,source in [('BaseColor','base_color'),('Normal','normal')]:shutil.copy2(ROOT+'/ArtSource/SpringBoar/ReplacementSource/mat_2_'+source+'.png',OUT+'/Textures/'+name+'_'+suffix+'.png')
 rig.animation_data_create()
 states=['Idle','Walk','Scrape','Charge','TuskReady','Tusk','SlamReady','Slam','Roar','Stunned','Recovery']
 for state in states:
  action=bpy.data.actions.new(name+'_'+state);rig.animation_data.action=action;action.use_fake_user=True
  for frame in range(1,41):
   t=(frame-1)/39;wave=math.sin(t*math.tau);ease=t*t*(3-2*t)
   for pb in rig.pose.bones:pb.rotation_mode='QUATERNION';pb.rotation_quaternion=(1,0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
   def rot(n,x=0,y=0,z=0):
    pb=rig.pose.bones[n];q=pb.bone.matrix_local.to_quaternion();pb.rotation_quaternion=q.inverted()@Euler((x,y,z)).to_quaternion()@q
   def loc(n,x=0,y=0,z=0):
    pb=rig.pose.bones[n];pb.location=pb.bone.matrix_local.to_3x3().inverted()@Vector((x,y,z))
   if state in ('Idle','Walk','Charge'):
    loc('Hips',z=-h*.035+h*(.003 if state=='Idle' else .006)*(1-math.cos(t*math.tau*2)))
    rot('Spine',x=.012*wave,z=.012*wave);rot('Head',x=-.025*wave)
    for side,s in [('L',1),('R',-1)]:
     if human:
      rot(side+'UpperArm',x=(.35 if state=='Walk' else .05)*wave*s,z=s*.04)
      rot(side+'Forearm',x=-.12-.12*max(0,wave*s))
      if name=='IceGolem':
       rot(side+'Thigh',x=.38*wave*s if state=='Walk' else 0);rot(side+'Shin',x=-.42*max(0,-wave*s) if state=='Walk' else 0);rot(side+'Foot',x=.15*wave*s if state=='Walk' else 0)
     else:
      for label,k in [('Front',1),('Rear',-1)]:
       step=wave*s*k
       rot(side+label+'Upper',x=.42*step if state=='Walk' else 0);rot(side+label+'Lower',x=-.46*max(0,-step) if state=='Walk' else 0);rot(side+label+'Foot',x=.15*step if state=='Walk' else 0)
      rot('Tail',z=.17*wave)
   elif human:
    if name=='IceGolem':
     # Anticipation ends with both fists overhead; slam starts in that exact pose.
     u=min(1,t/.28);drop=u*u*(3-2*u)
     pose=ease if state=='AttackReady' else 1-drop if state=='Attack' else 0
     settle=max(0,min(1,(t-.42)/.58));settle=settle*settle*(3-2*settle)
     slam=drop*(1-.65*settle) if state=='Attack' else (1-ease)*.35 if state=='Recovery' else 0
     rot('Spine',x=-.20*pose+.42*slam);rot('Chest',x=-.18*pose+.20*slam);rot('Head',x=.12*pose-.15*slam);loc('Hips',z=-h*(.035+.045*pose+.10*slam))
     for side,s in [('L',1),('R',-1)]:
      rot(side+'UpperArm',x=-2.45*pose-1.10*slam,z=s*.20*pose);rot(side+'Forearm',x=-.55*pose-.15*slam);rot(side+'Hand',x=.30*slam)
      rot(side+'Thigh',x=-.3*pose-.4*slam);rot(side+'Shin',x=.40*pose+.65*slam);rot(side+'Foot',x=-.10*pose-.25*slam)
    else:
     press=ease if state=='AttackReady' else 1-.55*ease if state=='Attack' else .45*(1-ease)
     rot('Spine',x=.6*press);rot('Chest',x=.18*press);rot('Head',x=-.45*press);loc('Hips',z=-h*.24*press)
     for side,s in [('L',1),('R',-1)]:rot(side+'UpperArm',x=-.9*press,z=s*.22*press);rot(side+'Forearm',x=.25*press);rot(side+'Hand',x=.4*press)
   elif name=='SnowWolf':
    crouch=ease if state in ('AttackReady','Reaim') else max(0,1-t/.3) if state in ('Attack','Pounce') else (1-ease)*.3
    extend=math.sin(t*math.pi) if state in ('Attack','Pounce') else 0
    loc('Hips',z=-h*.16*crouch);rot('Spine',x=.18*crouch-.14*extend);rot('Chest',x=.15*crouch);rot('Head',x=-.2*crouch+.2*extend);rot('Jaw',x=.32*extend);rot('Tail',x=-.3*extend)
    for side,s in [('L',1),('R',-1)]:
     for label,k in [('Front',1),('Rear',-1)]:
      rot(side+label+'Upper',x=-.48*crouch-k*.65*extend);rot(side+label+'Lower',x=.85*crouch-.28*extend);rot(side+label+'Foot',x=-.37*crouch)
    if state in ('Reaim','Claw'):
     wind=ease if state=='Reaim' else max(0,1-t/.48)
     swipe=0 if state=='Reaim' else math.sin(math.pi*min(1,t/.85))
     loc('Hips',z=-h*.025);rot('Spine',z=.12*wind-.20*swipe);rot('Chest',x=-.12*wind+.18*swipe,z=.20*wind-.35*swipe)
     rot('Head',x=-.1*wind+.10*swipe,z=-.15*wind+.20*swipe);rot('Jaw',x=.28*swipe)
     rot('LFrontUpper',x=-1.0*wind-.65*swipe,z=.65*wind-.95*swipe);rot('LFrontLower',x=.30*wind+.25*swipe);rot('LFrontFoot',x=-.3*swipe)
     rot('RFrontUpper',x=.12*swipe);rot('RFrontLower',x=-.12*swipe);rot('RFrontFoot')
     for side in ('L','R'):
      for part in ('Upper','Lower','Foot'):rot(side+'Rear'+part)
   else:
    lower=ease if state=='AttackReady' else 1-min(1,t/.4) if state=='Attack' else 0
    rise=math.sin(math.pi*max(0,min(1,(t-.12)/.88))) if state=='Attack' else .2*(1-ease) if state=='Recovery' else 0
    loc('Hips',z=-h*(.035+.045*lower)+h*.018*rise);rot('Spine',x=.10*lower-.08*rise);rot('Chest',x=.15*lower-.12*rise);rot('Neck',x=.30*lower-.36*rise);rot('Head',x=.18*lower-.16*rise);rot('Tail',x=.20*rise)
    for side,s in [('L',1),('R',-1)]:
     for label,k in [('Front',1),('Rear',-1)]:rot(side+label+'Upper',x=-.22*lower+.25*rise*k);rot(side+label+'Lower',x=.34*lower-.15*rise*k)
   if name=='SpringBoar' and state=='Charge':
    loc('Hips',z=-h*.045+h*.012*(1-math.cos(t*math.tau*2)));rot('Chest',x=.10);rot('Neck',x=.20);rot('Head',x=.22)
   if name=='SpringBoar' and state not in ('Idle','Walk','Charge'):
    for pb in rig.pose.bones:pb.rotation_quaternion=(1,0,0,0);pb.location=(0,0,0)
    crouch=0;rear=0
    if state=='Scrape':
     scrape=math.sin(t*math.tau*3);lower=max(0,(t-.65)/.35)
     loc('Hips',z=-h*.025);rot('Chest',x=.10*lower);rot('Neck',x=.20*lower);rot('Head',x=.22*lower)
     rot('LFrontUpper',x=-.55*max(0,scrape));rot('LFrontLower',x=.75*max(0,scrape));rot('LFrontFoot',x=-.20*max(0,scrape))
    elif state in ('TuskReady','Tusk'):
     lower=ease if state=='TuskReady' else max(0,1-t/.35)
     rise=0 if state=='TuskReady' else math.sin(math.pi*max(0,min(1,(t-.14)/.86)))
     loc('Hips',z=-h*(.035+.06*lower)+h*.02*rise);rot('Spine',x=.10*lower-.10*rise);rot('Chest',x=.12*lower-.18*rise);rot('Neck',x=.3*lower-.65*rise);rot('Head',x=.12*lower-.18*rise);rot('Jaw',x=.18*rise)
    elif state in ('SlamReady','Slam'):
     rear=ease if state=='SlamReady' else max(0,1-t/.30)**2
     impact=0 if state=='SlamReady' else math.sin(math.pi*max(0,min(1,(t-.3)/.7)))
     loc('Hips',z=h*.06*rear-h*.045*impact);rot('Spine',x=-.38*rear+.1*impact);rot('Chest',x=-.26*rear+.12*impact);rot('Neck',x=.22*rear);rot('Head',x=-.12*impact)
     for side in ('L','R'):rot(side+'FrontUpper',x=-.8*rear);rot(side+'FrontLower',x=1.1*rear);rot(side+'FrontFoot',x=-.3*rear)
    elif state=='Roar':
     roar=math.sin(math.pi*t);loc('Hips',z=-h*.025);rot('Chest',x=-.18*roar);rot('Neck',x=-.32*roar);rot('Head',x=-.18*roar);rot('Jaw',x=.6*roar)
    elif state=='Stunned':
     loc('Hips',z=-h*.12);rot('Spine',x=.1);rot('Neck',x=.3,z=.06*wave);rot('Head',x=.22,z=.04*wave)
    else:loc('Hips',z=-h*.035*(1-ease))
   if name!='SnowSpirit':
    # Feet remain level and planted during anticipation; mammoth uses a four-beat walk.
    limbs=[(side+'Thigh',side+'Shin',side+'Foot',offset) for side,offset in [('L',0),('R',.5)]] if human else [(side+label+'Upper',side+label+'Lower',side+label+'Foot',offset) for side,label,offset in ([('L','Rear',0),('L','Front',.25),('R','Rear',.5),('R','Front',.75)] if name in ('FrostMammoth','SpringBoar') else [('L','Front',0),('R','Rear',0),('R','Front',.5),('L','Rear',.5)])]
    for upper,lower,foot,offset in limbs:
     target=rig.data.bones[foot].head_local.copy()
     if state in ('Walk','Charge'):
      phase=(t+offset)%1;stance=.40 if state=='Charge' else .64 if name!='SnowWolf' else .58
      stride={'IceGolem':.28,'FrostMammoth':.34,'SnowWolf':.49,'SpringBoar':.32}[name]*h
      if state=='Charge':stride=.50*h
      if phase<stance:target.y+=stride*(phase/stance-.5)
      else:
       swing=(phase-stance)/(1-stance);smooth=swing*swing*(3-2*swing)
       target.y+=stride*(.5-smooth);target.z+=h*.055*math.sin(swing*math.pi)**2
     # Airborne wolf stretches naturally; the swiping paw stays free during its claw.
     if name=='SpringBoar' and ((state=='Scrape' and foot=='LFrontFoot') or (state in ('SlamReady','Slam') and 'Front' in foot and rear>.02)):continue
     if name=='SnowWolf' and (state=='Attack' or (state in ('Reaim','Claw') and foot=='LFrontFoot')):continue
     plant_leg(rig,upper,lower,foot,target,h)
   for pb in rig.pose.bones:
    for prop in ('rotation_quaternion','location','scale'):pb.keyframe_insert(data_path=prop,frame=frame,group=pb.name)
  for fc in action.fcurves:
   for key in fc.keyframe_points:key.interpolation='LINEAR'
  track=rig.animation_data.nla_tracks.new();track.name=action.name;track.strips.new(action.name,1,action)
 rig.animation_data.action=None
 for tr in rig.animation_data.nla_tracks:tr.mute=True
 for pb in rig.pose.bones:pb.rotation_quaternion=(1,0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
 for tr in rig.animation_data.nla_tracks:tr.mute=False
 bpy.ops.export_scene.fbx(filepath=OUT+'/Models/'+name+'.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,axis_forward='-Z',axis_up='Y')
 for tr in rig.animation_data.nla_tracks:tr.mute=True
 src.hide_set(True);src.hide_render=True;rig.location.x=NAMES.index(name)*4
 print(json.dumps({'name':name,'bones':len(bones),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'clips':states}))

if __name__=='__main__':build('SpringBoar')
