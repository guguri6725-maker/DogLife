"""Retarget the supplied Mixamo attacks to the existing Reaper and rigid right-hand scythe."""
import bpy, math, json
from mathutils import Vector, Matrix, Euler
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
# Reuse the tested two-bone solver; this does not rebuild the mesh or skin weights.
exec(open(ROOT+'/ArtSource/HarvestReaper/refine_pattern_motion.py',encoding='utf-8').read().split('spec=[]')[0])
C=Matrix(((-1,0,0),(0,0,-1),(0,1,0)))
mapping={'Hips':'Hips','Spine':'Spine','Chest':'Spine2','Head':'Head'}
specs=[('Slam','충격파',24,36,50,171),('Sweep','휩쓸기',53,63,83,191),('Cleave','직선베기',56,66,76,191)]
summary={}
idle=bpy.data.actions.get('HarvestReaper_Rig|HarvestReaper_Idle')
if idle:
    rig.animation_data.action=idle; scene.frame_set(int(idle.frame_range[0]));bpy.context.view_layer.update()
    neutral={p.name:(p.location.copy(),p.rotation_quaternion.copy()) for p in rig.pose.bones}
else:
    reset();neutral={p.name:(p.location.copy(),p.rotation_quaternion.copy()) for p in rig.pose.bones}
rig.animation_data.action=None

def delta(src,name):
    p=src.pose.bones['mixamorig:'+name];r=src.data.bones[p.name]
    return (C@p.matrix.to_3x3().normalized()@r.matrix_local.to_3x3().inverted()@C.inverted()).to_quaternion()

def sample(src):
    reset()
    sb=src.data.bones;pb=src.pose.bones
    scale=(rest['Hips'].translation.z-rest['LFoot'].translation.z)/(sb['mixamorig:Hips'].head_local.y-sb['mixamorig:LeftFoot'].head_local.y)
    shift=C@(pb['mixamorig:Hips'].head-sb['mixamorig:Hips'].head_local)*scale
    move_hips(*shift)
    for target,source in mapping.items():
        bpy.context.view_layer.update();p=rig.pose.bones[target]
        p.matrix=Matrix.Translation(p.head)@(delta(src,source)@rest[target].to_quaternion()).to_matrix().to_4x4()
    bpy.context.view_layer.update()
    for side,word in [('L','Left'),('R','Right')]:
        # Feet inherit source steps while the target keeps its own limb lengths.
        source=word+'Foot';target=feet[side]+C@(pb['mixamorig:'+source].head-sb['mixamorig:'+source].head_local)*scale
        target.z=max(feet[side].z,target.z)
        pole=C@(pb['mixamorig:'+word+'Leg'].head-pb['mixamorig:'+word+'UpLeg'].head)
        goal=limb(side+'Thigh',side+'Shin',side+'Foot',target,pole)
        q=delta(src,source)@rest[side+'Foot'].to_quaternion()
        rig.pose.bones[side+'Foot'].matrix=Matrix.Translation(goal)@q.to_matrix().to_4x4()
        bpy.context.view_layer.update()
        source_a=pb['mixamorig:'+word+'Arm'];source_b=pb['mixamorig:'+word+'ForeArm'];source_c=pb['mixamorig:'+word+'Hand']
        arm_length=(rest[side+'Forearm'].translation-rest[side+'Arm'].translation).length+(rest[side+'Hand'].translation-rest[side+'Forearm'].translation).length
        source_length=(sb[source_b.name].head_local-sb[source_a.name].head_local).length+(sb[source_c.name].head_local-sb[source_b.name].head_local).length
        target=rig.pose.bones[side+'Arm'].head+C@(source_c.head-source_a.head)*(arm_length/source_length)
        pole=C@(source_b.head-source_a.head)
        goal=limb(side+'Arm',side+'Forearm',side+'Hand',target,pole)
        q=delta(src,word+'Hand')@rest[side+'Hand'].to_quaternion()
        rig.pose.bones[side+'Hand'].matrix=Matrix.Translation(goal)@q.to_matrix().to_4x4()
        bpy.context.view_layer.update()
    # Keep one rigid grip: shift along the shaft, never scale or detach the weapon.
    weapon=rig.pose.bones['Scythe'];weapon.matrix_basis.identity();bpy.context.view_layer.update()
    # One constant grip correction per source clip keeps the cutting plane aligned with its hit area.
    hand_delta=delta(src,'RightHand')
    wq=hand_delta@grip_correction@rest['Scythe'].to_quaternion()
    shaft=(wq@Vector((0,1,0))).normalized()
    palm=rig.pose.bones['RHand'].head+hand_delta@Vector((-.188,.553,.046))
    weapon.matrix=Matrix.Translation(palm+shaft*1.20)@wq.to_matrix().to_4x4()
    bpy.context.view_layer.update()

for prefix,label,ready_end,impact,attack_end,end in specs:
    src=bpy.data.objects['UserMotion_'+label]
    scene.frame_set(impact)
    # Local Y follows the shaft; local Z is normal to the original blade plane.
    shaft=Vector((0,-1,0)) if prefix=='Sweep' else (Vector((0,-.70710678,-.70710678)) if prefix=='Cleave' else Vector((0,0,1)))
    normal=Vector((0,0,1)) if prefix=='Sweep' else Vector((1,0,0))
    across=shaft.cross(normal).normalized()
    desired=Matrix((across,shaft,normal)).transposed().to_quaternion()
    if prefix=='Cleave':
        # Preserve the supplied uppercut and make the hooked edge lead upward at impact.
        shaft=Vector((0,-.98,.20)).normalized()
        edge=Vector((0,.20,.98)).normalized()
        rest_shaft=Vector((-.09,0,.335)).normalized()
        rest_edge=Vector((1,0,0));rest_edge=(rest_edge-rest_shaft*rest_edge.dot(rest_shaft)).normalized()
        source_basis=Matrix((rest_edge,rest_shaft,rest_edge.cross(rest_shaft))).transposed()
        target_basis=Matrix((edge,shaft,edge.cross(shaft))).transposed()
        desired=(target_basis@source_basis.inverted()@rest['Scythe'].to_3x3()).to_quaternion()
    grip_correction=delta(src,'RightHand').inverted()@desired@rest['Scythe'].to_quaternion().inverted()
    summary[prefix]={'source':label+'.fbx','prepare_end':ready_end,'impact':impact,'attack_end':attack_end,'end':end}
    for phase in ['Ready','Attack','Recovery']:
        action=bpy.data.actions.new('HarvestReaper_'+prefix+phase);rig.animation_data.action=action;action.use_fake_user=True
        for frame in range(1,62):
            t=(frame-1)/60
            if phase=='Ready': sf=1+(ready_end-1)*min(1,t/.95)
            elif phase=='Attack': sf=ready_end+(impact-ready_end)*t/.35 if t<=.35 else impact+(attack_end-impact)*(t-.35)/.65
            else: sf=attack_end+(end-attack_end)*min(1,t/.85)
            scene.frame_set(int(sf),subframe=sf-int(sf));sample(src)
            # Blend the clip boundaries to the existing idle, keeping the supplied strike intact.
            blend=1-ease(t/.12) if phase=='Ready' else (ease((t-.85)/.15) if phase=='Recovery' else 0)
            if blend:
                for p in rig.pose.bones:
                    loc,q=neutral[p.name];p.location=p.location.lerp(loc,blend);p.rotation_quaternion=p.rotation_quaternion.slerp(q,blend)
            for p in rig.pose.bones:
                for prop in ['location','rotation_quaternion','scale']:p.keyframe_insert(data_path=prop,frame=frame,group=p.name)
        track=rig.animation_data.nla_tracks.new();track.name=prefix+phase;track.strips.new(action.name,1,action);track.mute=True
rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=False
reset();bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
scene.frame_start=1;scene.frame_end=61;scene.render.fps=60
bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Art/HarvestReaper/Animations/HarvestReaper_UserMotion.fbx',use_selection=True,object_types={'ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
for track in rig.animation_data.nla_tracks:track.mute=True
with open(ROOT+'/Documentation/HarvestReaper/user_motion_retarget.json','w',encoding='utf-8') as f:json.dump(summary,f,ensure_ascii=False,indent=2)
print(json.dumps(summary,ensure_ascii=False))
