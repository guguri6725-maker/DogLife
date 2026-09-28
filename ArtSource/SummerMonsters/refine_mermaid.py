import bpy, math

# Articulation only; the AI supplies the dive arc so collision stays on the
# navigation surface. Preserve earlier actions under Previous_ names.
rig=bpy.data.objects['CoralMermaid_Rig']
def curve(t,keys):
    for i in range(1,len(keys)):
        if t<=keys[i][0]:
            a,x=keys[i-1];b,y=keys[i];u=(t-a)/(b-a);u=u*u*(3-2*u)
            return x+(y-x)*u
    return keys[-1][1]
for state in ['Dive','Burrow','Emerge','Recovery']:
    name='CoralMermaid_'+state
    old=bpy.data.actions.get(name)
    if old:old.name='Previous_'+name
    action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action
    for track in rig.animation_data.nla_tracks:track.mute=True
    for frame in range(1,49):
        t=(frame-1)/47;wave=math.sin(t*math.tau)
        for pb in rig.pose.bones:pb.rotation_mode='XYZ';pb.rotation_euler=(0,0,0);pb.location=(0,0,0);pb.scale=(1,1,1)
        if state=='Dive':
            fold=curve(t,[(0,0),(.23,.3),(.65,1),(1,.7)])
            sweep=curve(t,[(0,0),(.25,-.35),(.6,.8),(1,.45)])
            tail=curve(t,[(0,0),(.28,-.2),(.65,.8),(1,.35)])
        elif state=='Burrow':fold=.65;sweep=.4+.12*wave;tail=.3*wave
        elif state=='Emerge':
            fold=curve(t,[(0,.7),(.2,.3),(.38,-.7),(.7,-.2),(1,0)])
            sweep=curve(t,[(0,.45),(.22,-.65),(.45,-1),(.72,-.2),(1,0)])
            tail=curve(t,[(0,.35),(.18,-.6),(.4,.65),(.7,-.35),(1,0)])
        else:fold=.09*math.sin(t*math.pi)*(1-t);sweep=.12*math.sin(t*math.pi)*(1-t);tail=.16*wave*(1-t)
        rig.pose.bones['Spine'].rotation_euler.x=-.35*fold
        rig.pose.bones['Chest'].rotation_euler.x=-.18*fold
        rig.pose.bones['Head'].rotation_euler.x=.18*fold
        rig.pose.bones['TailA'].rotation_euler.x=.55*tail
        rig.pose.bones['TailB'].rotation_euler.x=-.65*tail
        rig.pose.bones['TailFin'].rotation_euler.x=.4*tail
        rig.pose.bones['TailFin'].rotation_euler.z=.18*wave
        rig.pose.bones['Hair'].rotation_euler.x=-.12*sweep
        for side,sign in [('L',1),('R',-1)]:
            rig.pose.bones[side+'Arm'].rotation_euler=(sweep*.65,0,sign*.25*abs(sweep))
            rig.pose.bones[side+'Hand'].rotation_euler.x=-sweep*.25
        for pb in rig.pose.bones:
            for prop in ('rotation_euler','location','scale'):pb.keyframe_insert(data_path=prop,frame=frame,group=pb.name)
    track=next((tr for tr in rig.animation_data.nla_tracks if tr.name==name),None)
    if not track:track=rig.animation_data.nla_tracks.new();track.name=name
    for strip in list(track.strips):track.strips.remove(strip)
    track.strips.new(name,1,action)
rig.animation_data.action=None
for ob in bpy.context.selected_objects:ob.select_set(False)
obj=bpy.data.objects['CoralMermaid_Body'];obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
old_position=rig.location.copy();rig.location=(0,0,0)
for track in rig.animation_data.nla_tracks:track.mute=False
bpy.ops.export_scene.fbx(filepath='C:/unity/DogLife/Assets/Art/SummerMonsters/Models/CoralMermaid.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,axis_forward='-Z',axis_up='Y')
for track in rig.animation_data.nla_tracks:track.mute=True
rig.location=old_position
