"""발로 땅을 파는 준비 동작을 먼저 보여 주고 머리부터 잠복한다."""
import bpy,math
from mathutils import Vector,Quaternion
obj=bpy.data.objects['LeafHedgehog_Body'];rig=obj.parent
for state,frames in [('AttackReady',49),('Attack',25),('Recovery',33)]:
    action=bpy.data.actions['LeafHedgehog_'+state]
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in list(bag.fcurves):bag.fcurves.remove(curve)
    rig.animation_data.action=action
    for frame in range(1,frames+1):
        t=(frame-1)/(frames-1)
        for p in rig.pose.bones:p.rotation_mode='XYZ';p.rotation_euler=(0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
        if state=='AttackReady':pitch=.20*min(1,t*5)+.065*math.sin(t*math.tau*3);bob=-.045*(1-math.cos(t*math.tau*3))
        elif state=='Attack':pitch=.20+.35*math.sin(t*math.pi/2);bob=0
        else:pitch=.32*(1-t);bob=0
        body=rig.pose.bones['Body'];inv=body.bone.matrix_local.to_3x3().inverted()
        body.rotation_euler=Quaternion(inv@Vector((1,0,0)),pitch).to_euler()
        body.location=inv@Vector((0,0,bob))
        for p in rig.pose.bones:
            if not p.name.endswith('Leg'):continue
            inv=p.bone.matrix_local.to_3x3().inverted()
            if state=='AttackReady':
                phase=t*math.tau*3+(math.pi if p.name.startswith('Right') else 0)
                front='Front' in p.name
                p.rotation_euler=Quaternion(inv@Vector((1,0,0)),(.9 if front else .2)*math.sin(phase)).to_euler()
                p.location=inv@Vector((0,(-.16 if front else -.035)*math.sin(phase),(.14 if front else .035)*max(0,math.cos(phase))))
            elif state=='Attack':p.rotation_euler=Quaternion(inv@Vector((1,0,0)),.6*(1-t)).to_euler()
        for p in rig.pose.bones:
            for prop in ('rotation_euler','location','scale'):p.keyframe_insert(data_path=prop,frame=frame,group=p.name)
rig.animation_data.action=None
for p in rig.pose.bones:p.matrix_basis.identity()
for o in bpy.context.scene.objects:o.select_set(False)
obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
pos=rig.location.copy();rig.location=(0,0,0);bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath='C:/Users/mbc/Desktop/NC_AI/DogLife/Assets/Models/Monster/Autumn/LeafHedgehog/LeafHedgehog.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,axis_forward='-Z',axis_up='Y')
rig.location=pos;bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath='C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/FallMonsters/FallMonsters.blend')
print('Dig preparation, dive and emergence clips saved')
