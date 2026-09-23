"""뼈·낙엽 디자인을 보존하면서 수평 비행 체형과 날개 관절을 다시 제작한다."""
import bpy, math
from mathutils import Vector, Matrix, Quaternion
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def rot(deg,axis):return Matrix.Rotation(math.radians(deg),3,axis)
obj=bpy.data.objects['BoneCrow_Body'];rig=obj.parent
rig.animation_data.action=None
for p in rig.pose.bones:p.matrix_basis.identity()
source=bpy.data.meshes.get('BoneCrow_StandingSource')
if source is None:source=obj.data.copy();source.name='BoneCrow_StandingSource';source.use_fake_user=True
obj.data=source.copy();obj.data.name='BoneCrow_HorizontalFlight'
for g in list(obj.vertex_groups):obj.vertex_groups.remove(g)
weight_sets=[]
for vertex,original in zip(obj.data.vertices,source.vertices):
    p=original.co.copy();x,y,z=p;side=1 if x>=0 else -1;side_name='Left' if side>0 else 'Right'
    body=rot(62,'X')@(p-Vector((0,0,.70)))+Vector((0,0,.68))
    # 두개골과 부리는 전방을 보도록 유지하고 가슴을 수평으로 눕힌다.
    head_weight=smooth(.55,.70,z)*(1-smooth(.24,.36,abs(x)))*(1-smooth(-.32,-.15,y))
    head=rot(-35,'X')@(p-Vector((0,-.40,1.02)))+Vector((0,-.58,.87))
    wing_weight=smooth(.26,.43,abs(x))*(1-(1-smooth(.20,.34,z))*(1-smooth(-.20,0,y)))
    wing=rot(-side*58,'Y')@(p-Vector((side*.32,0,1.02)))+Vector((side*.25,-.04,.78))
    leg_weight=(1-smooth(.32,.58,z))*(1-smooth(.17,.32,y))*(1-smooth(.34,.46,abs(x)))
    leg=rot(105,'X')@(p-Vector((side*.22,0,.53)))*.60+Vector((side*.17,.12,.49))
    tail_weight=smooth(.18,.40,y)*(1-smooth(.65,.83,z))
    tail=Vector((x*.78,.25+y*.95,.59+(z-.28)*.27))
    q=body.lerp(tail,tail_weight).lerp(leg,leg_weight).lerp(wing,wing_weight).lerp(head,head_weight)
    vertex.co=q
    w={'Body':1.0}
    def mix(groups,factor):
        for k in list(w):w[k]*=1-factor
        for k,v in groups.items():w[k]=w.get(k,0)+v*factor
    mix({'Tail':1},tail_weight);mix({side_name+'Leg':1},leg_weight)
    tip=smooth(.57,.95,abs(q.x));mix({side_name+'Wing':1-tip,side_name+'WingTip':tip},wing_weight)
    mix({'Head':1},head_weight);weight_sets.append(w)
# 몸통과 날개 뼈를 새 체형에 맞추고 기존 경로 이름은 유지한다.
for o in bpy.context.scene.objects:o.hide_set(False);o.select_set(False)
rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
bones=rig.data.edit_bones
spec={'Root':((0,0,0),(0,0,.15),None),'Body':((0,.30,.65),(0,-.26,.72),'Root'),
      'Head':((0,-.26,.74),(0,-.72,.81),'Body'),'Tail':((0,.30,.62),(0,.94,.60),'Body')}
for side,sgn in [('Left',1),('Right',-1)]:
    spec[side+'Wing']=((sgn*.22,-.04,.78),(sgn*.63,-.04,.88),'Body')
    spec[side+'WingTip']=((sgn*.63,-.04,.88),(sgn*1.18,.08,.91),side+'Wing')
    spec[side+'Leg']=((sgn*.17,.12,.49),(sgn*.17,.42,.39),'Body')
for name,(head,tail,parent) in spec.items():
    b=bones.get(name) or bones.new(name);b.head=head;b.tail=tail;b.roll=0;b.use_connect=False
for name,(_,_,parent) in spec.items():bones[name].parent=bones.get(parent) if parent else None
bpy.ops.object.mode_set(mode='OBJECT')
groups={name:obj.vertex_groups.new(name=name) for name in spec}
for i,weights in enumerate(weight_sets):
    for name,w in weights.items():
        if w>.00001:groups[name].add([i],w,'REPLACE')

def clear(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in list(bag.fcurves):bag.fcurves.remove(curve)
def axis_rotation(pb,axis,angle):
    local=pb.bone.matrix_local.to_3x3().inverted()@Vector(axis)
    pb.rotation_quaternion=Quaternion(local,angle)

for state,length,cycles in [('Idle',33,2),('Walk',25,2),('AttackReady',25,1),('Attack',25,1),('Recovery',33,2)]:
    action=bpy.data.actions['BoneCrow_'+state];clear(action);rig.animation_data.action=action
    for frame in range(1,length+1):
        t=(frame-1)/(length-1);phase=t*math.tau*cycles
        # 빠른 내리치기와 접힌 날개 끝의 지연으로 양력을 만드는 날갯짓을 표현한다.
        flap=math.sin(phase)+.12*math.sin(2*phase)
        for p in rig.pose.bones:p.rotation_mode='QUATERNION';p.rotation_quaternion=(1,0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
        for side,sgn in [('Left',1),('Right',-1)]:
            wing=rig.pose.bones[side+'Wing'];tip=rig.pose.bones[side+'WingTip']
            lift=(.78*flap+.08) if state in ('Idle','Walk','Recovery') else .65*math.sin(t*math.pi*.5) if state=='AttackReady' else .06
            axis_rotation(wing,(0,1,0),-sgn*lift)
            flex=.28*math.sin(phase-.65) if state in ('Idle','Walk','Recovery') else -.12
            axis_rotation(tip,(0,1,0),-sgn*flex)
        body=rig.pose.bones['Body'];axis_rotation(body,(1,0,0),.025*math.sin(phase-.5))
        body.location=body.bone.matrix_local.to_3x3().inverted()@Vector((0,0,.025*math.sin(phase-.4)))
        axis_rotation(rig.pose.bones['Tail'],(1,0,0),.05*math.sin(phase-.8))
        for p in rig.pose.bones:
            for prop in ('rotation_quaternion','location','scale'):p.keyframe_insert(data_path=prop,frame=frame,group=p.name)
    # 반복 구간 끝에서 멈칫하지 않도록 선형 보간을 사용한다.
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for k in curve.keyframe_points:k.interpolation='LINEAR'
rig.animation_data.action=None
for p in rig.pose.bones:p.matrix_basis.identity()

# 고슴도치 보행: 다리 가중치 영역과 보폭을 늘리고 발을 번갈아 들어 올린다.
h=bpy.data.objects['LeafHedgehog_Body'];hr=h.parent
for v in h.data.vertices:
    for g in list(v.groups):h.vertex_groups[g.group].remove([v.index])
    foot=(1-smooth(.10,.28,v.co.z))*smooth(.12,.29,abs(v.co.x))
    side='Left' if v.co.x>0 else 'Right';part='Front' if v.co.y<0 else 'Back'
    h.vertex_groups['Body'].add([v.index],1-foot,'REPLACE')
    if foot>0:h.vertex_groups[side+part+'Leg'].add([v.index],foot,'REPLACE')
action=bpy.data.actions['LeafHedgehog_Walk'];clear(action);hr.animation_data.action=action
for frame in range(1,26):
    t=(frame-1)/24;phase=t*math.tau
    for p in hr.pose.bones:p.rotation_mode='XYZ';p.rotation_euler=(0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
    for p in hr.pose.bones:
        if p.name.endswith('Leg'):
            a=phase+(math.pi if ('Left' in p.name)==('Front' in p.name) else 0)
            p.rotation_euler.x=.42*math.sin(a)
            p.location=p.bone.matrix_local.to_3x3().inverted()@Vector((0,-.055*math.sin(a),.075*max(0,math.cos(a))))
    hr.pose.bones['Body'].rotation_euler.z=.035*math.sin(phase)
    for p in hr.pose.bones:
        for prop in ('rotation_euler','location','scale'):p.keyframe_insert(data_path=prop,frame=frame,group=p.name)
hr.animation_data.action=None
for p in hr.pose.bones:p.matrix_basis.identity()

for name in ['BoneCrow','LeafHedgehog']:
    o=bpy.data.objects[name+'_Body'];r=o.parent
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);r.select_set(True);bpy.context.view_layer.objects.active=r
    loc=r.location.copy();r.location=(0,0,0);bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Art/FallMonsters/Models/'+name+'.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,axis_forward='-Z',axis_up='Y')
    r.location=loc
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/FallMonsters/FallMonsters.blend')
print('Horizontal flight rig and visible hedgehog steps exported')
