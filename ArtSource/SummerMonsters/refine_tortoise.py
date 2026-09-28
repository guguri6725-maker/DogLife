import bpy, math
from mathutils import Vector
rig=bpy.data.objects['BoneTortoise_Rig']
obj=bpy.data.objects['BoneTortoise_Body']
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
# Keep the shell rigid, but bind the entire projecting skull and each foot.
groups={g.name:g for g in obj.vertex_groups}
for v in obj.data.vertices:
    x,y,z=v.co
    head=smooth(.30,.48,-y)*(1-smooth(.27,.41,abs(x)))*(1-smooth(1.13,1.25,z))
    leg=smooth(.25,.48,abs(x))*(1-smooth(.34,.66,z))*(1-head)
    jaw=head*(1-smooth(.34,.45,z))*smooth(.48,.64,-y)*.85
    weights={'Head':head-jaw,'Jaw':jaw,'Body':1-head-leg}
    weights[('L' if x>0 else 'R')+'Leg'+('0' if y<.03 else '1')]=leg
    for g in obj.vertex_groups:g.remove([v.index])
    for n,w in weights.items():
        if w>.00001:groups[n].add([v.index],w,'REPLACE')
def translate(name,delta):
    rig.pose.bones[name].location=rig.data.bones[name].matrix_local.to_3x3().inverted()@Vector(delta)
for state in ['Idle','Walk','GuardIn','GuardLoop','GuardOut','AttackReady','Attack','Recovery']:
    name='BoneTortoise_'+state
    old=bpy.data.actions.get(name)
    if old:old.name='Previous_'+name
    a=bpy.data.actions.new(name);a.use_fake_user=True;rig.animation_data.action=a
    for tr in rig.animation_data.nla_tracks:tr.mute=True
    for frame in range(1,41):
        t=(frame-1)/39
        for p in rig.pose.bones:p.rotation_mode='XYZ';p.rotation_euler=(0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
        guard=smooth(0,1,t) if state=='GuardIn' else 1-smooth(0,1,t) if state=='GuardOut' else 1 if state=='GuardLoop' else 0
        translate('Body',(0,0,-.22*guard))
        rig.pose.bones['Body'].rotation_euler.x=.32*guard
        translate('Head',(0,.90*guard,.35*guard))
        rig.pose.bones['Head'].scale=(1-.28*guard,)*3
        rig.pose.bones['Head'].rotation_euler.x=-.12*guard
        for side,sign in [('L',1),('R',-1)]:
            for k in range(2):
                n=side+'Leg'+str(k)
                if state=='Walk':
                    phase=(t+(0 if side=='L' else .5)+k*.25)%1
                    stride=math.cos(phase*math.tau)*.115
                    lift=max(0,math.sin(phase*math.tau))*.055
                    translate(n,(0,stride,lift))
                    rig.pose.bones[n].rotation_euler.x=.10*math.cos(phase*math.tau)
                else:
                    translate(n,(-sign*.30*guard,(.12 if k==0 else -.12)*guard,.16*guard))
                    rig.pose.bones[n].scale=(1-.50*guard,)*3
        if state in ['Idle','Walk']:
            translate('Body',(0,0,.006*math.sin(t*math.tau)))
            rig.pose.bones['Head'].rotation_euler.x=.012*math.sin(t*math.tau)
        if state in ['AttackReady','Attack','Recovery']:
            ready=math.sin(t*math.pi/2) if state=='AttackReady' else max(0,1-t*3) if state=='Attack' else 0
            bite=math.sin(t*math.pi) if state=='Attack' else 0
            translate('Head',(0,.07*ready-.12*bite,.02*ready))
            rig.pose.bones['Head'].rotation_euler.x=-.08*ready+.12*bite
            rig.pose.bones['Jaw'].rotation_euler.x=.20*ready+.10*bite
        for p in rig.pose.bones:
            for prop in ['location','rotation_euler','scale']:p.keyframe_insert(data_path=prop,frame=frame,group=p.name)
    tr=next((tr for tr in rig.animation_data.nla_tracks if tr.name==name),None)
    if not tr:tr=rig.animation_data.nla_tracks.new();tr.name=name
    for strip in list(tr.strips):tr.strips.remove(strip)
    tr.strips.new(name,1,a)
rig.animation_data.action=None
for o in bpy.context.selected_objects:o.select_set(False)
obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
location=rig.location.copy();rig.location=(0,0,0)
for tr in rig.animation_data.nla_tracks:tr.mute=False
bpy.ops.export_scene.fbx(filepath='C:/unity/DogLife/Assets/Art/SummerMonsters/Models/BoneTortoise.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,axis_forward='-Z',axis_up='Y')
for tr in rig.animation_data.nla_tracks:tr.mute=True
rig.location=location
print('Stable crawl and fully retracted guard exported')
