import bpy,json,math
from mathutils import Vector,Quaternion,Matrix
BASE='C:/unity/DogLife/'
rig=bpy.data.objects['DogLife_AxeRig'];scene=bpy.context.scene
bones=rig.pose.bones
rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
restp={n:m.translation.copy() for n,m in rest.items()}
axe=bpy.data.objects['Held Bone Axe'];hand=restp['RightHand'];tip=max([v.co for v in axe.data.vertices],key=lambda v:(v-hand).length)
axeaxis=(tip-hand).normalized()
def update():bpy.context.view_layer.update()
def rotate(n,axis,angle):
    b=bones[n];m=b.matrix.copy();m=Matrix.LocRotScale(m.translation,Quaternion(axis,math.radians(angle))@m.to_quaternion(),Vector((1,1,1)));b.matrix=m;update()
def solve(up,el,hand,target,pole):
    a=bones[up].head.copy();e=bones[el].head.copy();h=bones[hand].head.copy();l1=(e-a).length;l2=(h-e).length
    delta=target-a;d=max(abs(l1-l2)+.001,min(delta.length,l1+l2-.002));direction=delta.normalized();side=pole-a;side-=direction*side.dot(direction);side.normalize()
    along=(l1*l1+d*d-l2*l2)/(2*d);joint=a+direction*along+side*math.sqrt(max(0,l1*l1-along*along))
    m=bones[up].matrix.copy();q=(e-a).rotation_difference(joint-a);bones[up].matrix=Matrix.LocRotScale(a,q@m.to_quaternion(),Vector((1,1,1)));update()
    e=bones[el].head.copy();h=bones[hand].head.copy();q=(h-e).rotation_difference(a+direction*d-e);m=bones[el].matrix.copy();bones[el].matrix=Matrix.LocRotScale(e,q@m.to_quaternion(),Vector((1,1,1)));update()
def smooth(x):x=max(0,min(1,x));return x*x*(3-2*x)
def interpolate(keys,t):
    for i in range(len(keys)-1):
        a,b=keys[i],keys[i+1]
        if t<=b[0]:
            f=smooth((t-a[0])/(b[0]-a[0]));return [a[j]+(b[j]-a[j])*f for j in range(1,len(a))]
    return keys[-1][1:]
def pose(weight,lean,crouch,yaw,h,axis,spin=0,step_phase=None):
    for b in bones:b.matrix_basis=Matrix.Identity(4)
    update();hip=bones['Hips'];m=hip.matrix.copy();m.translation.z-=crouch;m.translation.y-=crouch*.2;hip.matrix=m;update()
    for name,f in [('Hips',.22),('Spine',.28),('Spine1',.25),('Spine2',.25)]:rotate(name,Vector((1,0,0)),lean*f);rotate(name,Vector((0,0,1)),yaw*f)
    rotate('Head',Vector((1,0,0)),-lean*.5)
    for prefix in ['Left','Right']:
        plant=restp[prefix+'Foot'].copy()
        if step_phase is not None:plant.z+=max(0,math.sin(step_phase+(0 if prefix=='Left' else math.pi)))*.025
        solve(prefix+'UpLeg',prefix+'Leg',prefix+'Foot',plant,restp[prefix+'UpLeg']+Vector((0,-1,0)))
        m=bones[prefix+'Foot'].matrix.copy();bones[prefix+'Foot'].matrix=Matrix.LocRotScale(m.translation,rest[prefix+'Foot'].to_quaternion(),Vector((1,1,1)));update()
    axis=Vector(axis).normalized();h=Vector(h)
    desired=axeaxis.rotation_difference(axis)@rest['RightHand'].to_quaternion()
    for prefix,target in [('Right',h),('Left',h-axis*.13)]:
        target=restp[prefix+'Hand'].lerp(target,weight)
        pole=bones[prefix+'Arm'].head+Vector((-.6 if prefix=='Left' else .6,.12,-.12))
        solve(prefix+'Arm',prefix+'ForeArm',prefix+'Hand',target,pole)
        m=bones[prefix+'Hand'].matrix.copy();q=rest[prefix+'Hand'].to_quaternion().slerp(desired,weight);bones[prefix+'Hand'].matrix=Matrix.LocRotScale(m.translation,q,Vector((1,1,1)));update()
    if spin:
        pivot=Vector((-.17,-.09,0));rotation=Matrix.Translation(pivot)@Quaternion((0,0,1),math.radians(spin)).to_matrix().to_4x4()@Matrix.Translation(-pivot)
        bones['Root'].matrix=rotation@bones['Root'].matrix;update()
def author(name,length,kind):
    rig.animation_data_create();action=bpy.data.actions.new(name);rig.animation_data.action=action;action.use_fake_user=True
    frames=round(length*60)
    hit=[.56,.68,1.1][min(kind,2)] if kind<3 else 0
    for f in range(frames+1):
        t=f/60
        if kind<3:
            dur=length;keys=[(0,0,0,0,0),(.14,1,-4,.015,-4),(hit-.17,1,-9 if kind<2 else -12,.025,-12 if kind==0 else 0),(hit,1,28 if kind<2 else 34,.13 if kind==0 else .08 if kind==1 else .10,8 if kind==0 else 0),(hit+.045,1,30 if kind<2 else 35,.14 if kind==0 else .09 if kind==1 else .105,8 if kind==0 else 0),(dur-.08,.6,12,.035,0),(dur,0,0,0,0)]
            weight,lean,crouch,yaw=interpolate(keys,t)
            strike=smooth((t-(hit-.17))/.17)
            h=Vector((-.10,-.12,1.16)).lerp(Vector((-.19 if kind==0 else -.10,-.35,.49)),strike)
            axis=Vector((.05,.15,1)).lerp(Vector((-.22 if kind==0 else .03,-.58,-.82)),strike).normalized()
            pose(weight,lean,crouch,yaw,h,axis)
        else:
            w=smooth(t/length) if kind==3 else 1-smooth(t/length) if kind==5 else 1
            pose(w,8*w,.045*w,0,(-.10,-.30,.79),(1,-.25,-.02),spin=360*t/length if kind==4 else 0,step_phase=t/length*math.pi*2 if kind==4 else None)
        for b in bones:
            b.rotation_mode='QUATERNION';b.keyframe_insert('location',frame=f,group=b.name);b.keyframe_insert('rotation_quaternion',frame=f,group=b.name)
    for fc in action.fcurves:
        for key in fc.keyframe_points:key.interpolation='LINEAR'
    return action
specs=[('BoneAxe_1',52/60,0),('BoneAxe_2',59/60,1),('BoneAxe_3',83/60,2),('AxeWhirlwind_Enter',.3,3),('AxeWhirlwind_Loop',.5,4),('AxeWhirlwind_Exit',.3,5)]
actions=[author(*s) for s in specs]
rig.animation_data.action=actions[0];scene.frame_end=52;scene.frame_set(34)
bpy.ops.wm.save_as_mainfile(filepath=BASE+'ArtSource/PlayerAxe/DogLife_Axe_Authoring.blend')
print('Created six Blender actions')
