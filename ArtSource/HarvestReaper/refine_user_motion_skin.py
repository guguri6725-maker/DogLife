"""Keep the hanging robe on the torso when the supplied attacks raise the arms."""
import bpy, math
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
rig=bpy.data.objects['HarvestReaper_Rig'];body=bpy.data.objects['HarvestReaper_Body']
arm_groups={g.index for g in body.vertex_groups if g.name in ['LArm','LForearm','LHand','RArm','RForearm','RHand']}
hip=body.vertex_groups['Hips'];spine=body.vertex_groups['Spine'];changed=0
for v in body.data.vertices:
    # Preserve the hands and sleeves; only transfer the low hanging cloth's arm influence.
    blend=max(0,min(1,(4.02-v.co.z)/.45));blend=blend*blend*(3-2*blend)
    if blend<=0:continue
    weights={g.group:g.weight for g in v.groups};amount=sum(w for i,w in weights.items() if i in arm_groups)*blend
    if amount<.00001:continue
    for i,w in weights.items():
        if i in arm_groups:body.vertex_groups[i].add([v.index],w*(1-blend),'REPLACE')
    u=max(0,min(1,(v.co.z-3.0)/1.1))
    hip.add([v.index],weights.get(hip.index,0)+amount*(1-u),'REPLACE')
    spine.add([v.index],weights.get(spine.index,0)+amount*u,'REPLACE');changed+=1
rig.animation_data.action=None
for tr in rig.animation_data.nla_tracks:tr.mute=True
for p in rig.pose.bones:p.matrix_basis.identity()
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Models/Monster/Autumn/HarvestReaper/HarvestReaper.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
print('Adjusted hanging robe weights:',changed,'; vertex count unchanged:',len(body.data.vertices))
