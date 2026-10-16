"""Bake supplied summon, eight-direction gesture and death onto the existing Reaper rigs."""
import bpy,math,json,numpy as np
from mathutils import Vector,Matrix,Euler
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
exec(open(ROOT+'/ArtSource/HarvestReaper/retarget_user_motions.py',encoding='utf-8').read().split('for prefix,label,ready_end,impact,attack_end,end in specs:')[0])
main_rig=rig
report={'clips':[],'death_ground_lift':0}

def key_pose(frame):
    for p in rig.pose.bones:
        for prop in ['location','rotation_quaternion','scale']:p.keyframe_insert(data_path=prop,frame=frame,group=p.name)

def action_begin(name):
    action=bpy.data.actions.new('HarvestReaper_'+name);action.use_fake_user=True
    rig.animation_data.action=action
    return action

def action_end(action):
    track=rig.animation_data.nla_tracks.new();track.name=action.name
    track.strips.new(action.name,1,action);track.mute=True
    report['clips'].append(action.name)

def export(path):
    rig.animation_data.action=None
    for track in rig.animation_data.nla_tracks:track.mute=False
    reset();bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=ROOT+path,use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
    for track in rig.animation_data.nla_tracks:track.mute=True

prefix='Summon';src=bpy.data.objects['UserMotion_호박소환']
scene.frame_set(25);grip_correction=delta(src,'RightHand').inverted()
for phase in ['Ready','Attack','Recovery']:
    action=action_begin('Summon'+phase)
    for frame in range(1,62):
        t=(frame-1)/60;sf=1+24*t if phase=='Ready' else (25+24*t if phase=='Attack' else 49)
        scene.frame_set(int(sf),subframe=sf-int(sf));sample(src)
        blend=1-ease(t/.25) if phase=='Ready' else (ease(t) if phase=='Recovery' else 0)
        for p in rig.pose.bones:
            loc,q=neutral[p.name];p.location=p.location.lerp(loc,blend);p.rotation_quaternion=p.rotation_quaternion.slerp(q,blend)
        key_pose(frame)
    action_end(action)

prefix='Wedge';src=bpy.data.objects['UserMotion_안전지대 회전']
scene.frame_set(23);grip_correction=delta(src,'RightHand').inverted();sample(src)
d=rig.pose.bones['LHand'].head-rig.pose.bones['LArm'].head
base_angle=-math.pi/2-math.atan2(d.y,d.x)
for sector in range(8):
    for phase,start,end in [('Ready',1,23),('Attack',23,45),('Hold',45,68)]:
        action=action_begin('Wedge'+phase+str(sector))
        for frame in range(1,62):
            t=(frame-1)/60;sf=start+(end-start)*t
            scene.frame_set(int(sf),subframe=sf-int(sf));sample(src)
            # Rotate only the visual rig so the gesture follows the actual safe sector.
            rot('Root',z=base_angle-sector*math.pi/4);bpy.context.view_layer.update();key_pose(frame)
        action_end(action)
for phase,start,end in [('Ready',1,23),('Attack',23,45),('Recovery',45,68)]:
    action=action_begin('Gather'+phase)
    for frame in range(1,62):
        t=(frame-1)/60;sf=start+(end-start)*t
        scene.frame_set(int(sf),subframe=sf-int(sf));sample(src)
        blend=1-ease(t/.2) if phase=='Ready' else (ease((t-.7)/.3) if phase=='Recovery' else 0)
        for p in rig.pose.bones:
            loc,q=neutral[p.name];p.location=p.location.lerp(loc,blend);p.rotation_quaternion=p.rotation_quaternion.slerp(q,blend)
        key_pose(frame)
    action_end(action)
export('/Assets/Art/HarvestReaper/Animations/HarvestReaper_UserExtraMotion.fbx')

# The existing death skin includes a separate hem control to protect the folded legs.
rig=bpy.data.objects['HarvestReaper_DeathBakeRig'];rig.animation_data_clear();rig.animation_data_create()
bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for b in rig.data.edit_bones:b.use_connect=False
bpy.ops.object.mode_set(mode='OBJECT')
rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
feet={s:rest[s+'Foot'].translation.copy() for s in ['L','R']}
prefix='Death';src=bpy.data.objects['UserMotion_Death'];scene.frame_set(1);grip_correction=delta(src,'RightHand').inverted()
body=next(o for o in bpy.data.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers))
torso_ids=[v.index for v in body.data.vertices if any(body.vertex_groups[g.group].name in ['Hips','Spine','Chest','Head'] and g.weight>.7 for g in v.groups)]
action=action_begin('Death');scene.render.fps=60
for frame in range(1,236):
    scene.frame_set(frame);sample(src)
    pelvis=rig.pose.bones['Hips'];robe=rig.pose.bones['DeathRobe']
    # Shorten the hanging hem during kneeling, then restore its length as the body falls flat.
    m=pelvis.matrix@rest['Hips'].inverted()@rest['DeathRobe']
    vertical=abs((m.to_quaternion()@Vector((0,1,0))).z)
    compression=max(.18,min(1,pelvis.head.z/max(.001,2.9*vertical)))
    robe.matrix=m@Matrix.Diagonal((1,compression,1,1));bpy.context.view_layer.update()
    # Let the weapon settle flat with the fallen body instead of lifting the whole corpse.
    fall=ease((frame-160)/40)
    weapon=rig.pose.bones['Scythe'];wm=weapon.matrix.copy()
    flat=Euler((math.pi/2,0,0)).to_quaternion()@rest['Scythe'].to_quaternion()
    wq=wm.to_quaternion().slerp(flat,fall)
    wp=wm.translation.lerp(rig.pose.bones['RHand'].head+(wq@Vector((0,1,0)))*1.2,fall)
    weapon.matrix=Matrix.Translation(wp)@wq.to_matrix().to_4x4()
    bpy.context.view_layer.update()
    foot_targets={s:rig.pose.bones[s+'Foot'].head.copy() for s in ['L','R']}
    foot_rotations={s:rig.pose.bones[s+'Foot'].matrix.to_quaternion() for s in ['L','R']}
    ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
    coords=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',coords)
    coords=coords.reshape((-1,3));world=np.array(ev.matrix_world);low=float(np.min(coords[torso_ids]@world[2,:3]+world[2,3]));ev.to_mesh_clear()
    lift=max(0,.02-low);report['death_ground_lift']=max(report['death_ground_lift'],lift)
    rig.pose.bones['Root'].location=rest['Root'].to_3x3().inverted()@Vector((0,0,lift));bpy.context.view_layer.update()
    # Keep the feet on the floor while making room for the thicker target torso.
    for side in ['L','R']:
        goal=limb(side+'Thigh',side+'Shin',side+'Foot',foot_targets[side],(0,-1,0))
        rig.pose.bones[side+'Foot'].matrix=Matrix.Translation(goal)@foot_rotations[side].to_matrix().to_4x4()
    bpy.context.view_layer.update()
    # Loose hem and rigid weapon have their own contact correction; they cannot float the torso.
    for name in ['DeathRobe','Scythe']:
        ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();gi=body.vertex_groups[name].index
        low=min((ev.matrix_world@mesh.vertices[v.index].co).z for v in body.data.vertices if any(g.group==gi and g.weight>.8 for g in v.groups));ev.to_mesh_clear()
        if low<.02:
            p=rig.pose.bones[name];m=p.matrix.copy();m.translation.z+=.02-low;p.matrix=m;bpy.context.view_layer.update()
    key_pose(frame)
action_end(action);scene.frame_end=235
export('/Assets/Art/HarvestReaper/Animations/HarvestReaper_UserDeath.fbx')
with open(ROOT+'/Documentation/HarvestReaper/user_extra_motion_report.json','w',encoding='utf-8') as f:json.dump(report,f,indent=2)
print(json.dumps(report))
