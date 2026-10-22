import bpy,json,math
from mathutils import Quaternion,Vector
BASE='C:/unity/DogLife/'
frame=json.load(open(BASE+'ArtSource/PlayerAxe/model-frame.json'));q=frame['q'];oq=Quaternion((q[3],q[0],q[1],q[2]));op=Vector(frame['p']);C=Quaternion((1,0,0),math.pi/2);undo=oq@C.inverted()
rig=bpy.data.objects['DogLife_AxeRig'];scene=bpy.context.scene;clips=[]
for name,length in [('BoneAxe_1',52/60),('BoneAxe_2',59/60),('BoneAxe_3',83/60),('AxeWhirlwind_Enter',.3),('AxeWhirlwind_Loop',.5),('AxeWhirlwind_Exit',.3)]:
    rig.animation_data.action=bpy.data.actions[name];frames=[]
    for f in range(round(length*60)+1):
        scene.frame_set(f);bpy.context.view_layer.update();values=[]
        for b in rig.pose.bones:
            p=undo@b.matrix.translation+op;q=undo@b.matrix.to_quaternion();values.append({'name':b.name,'p':list(p),'q':[q.x,q.y,q.z,q.w]})
        frames.append({'t':f/60,'bones':values})
    clips.append({'name':name,'loop':name.endswith('_Loop'),'frames':frames})
json.dump({'clips':clips},open(BASE+'ArtSource/PlayerAxe/animation-bake.json','w'))
rig.animation_data.action=bpy.data.actions['BoneAxe_1'];scene.frame_end=52;scene.frame_set(34)
bpy.ops.wm.save_as_mainfile(filepath=BASE+'ArtSource/PlayerAxe/DogLife_Axe_Authoring.blend')
print('Baked',len(clips),'actions at 60 fps')
