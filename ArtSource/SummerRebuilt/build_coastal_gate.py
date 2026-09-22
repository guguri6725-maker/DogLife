"""여름 절벽과 이어지는 돌 문틀과 중앙 개방용 UV 물막을 제작합니다."""
import bpy, math, random
from mathutils import Vector
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
source=open(ROOT+'/ArtSource/SummerRebuilt/build_coastal_set.py',encoding='utf-8').read()
# 기존 형상 함수만 불러오고 별도 문 컬렉션에 제작합니다.
exec(source[:source.index('assets=[')].replace("'SummerRebuilt_Original'","'SummerRebuilt_Gate'"))
m=Mesh()
for side in [-1,1]:
    crag(m,side*4.75,0,2.9,2.5,9.6,side+2)
    crag(m,side*6.65,.1,2.4,2.7,8.4,side+4)
    for j in range(3):
        m.rock((side*(4.1+j*.95),.2,.4),(.6,.8,.45),6,10)
    fern(m,(side*5.0,.15,9.35),.9)
    # 옆기둥에만 뿌리를 붙여 중앙 통과 공간을 비웁니다.
    pts=[(side*(4.7+.22*math.sin(k*.6)),1.35,9.1-k*.45) for k in range(19)]
    m.tube(pts,[.14]*19,8,'bark')
for j in range(9):
    x=-3.5+j*.875
    z=8.4+.75*(1-(x/4.1)**2)
    m.rock((x,0,z),(.72,1.18,.65),7,10)
frame=m.object('CoastalGateFrame')
water=Mesh()
for y in range(24):
    for x in range(16):
        a=x/16;b=(x+1)/16;c=y/24;d=(y+1)/24
        water.face([((a-.5)*6.4,0,c*7.7),((b-.5)*6.4,0,c*7.7),((b-.5)*6.4,0,d*7.7),((a-.5)*6.4,0,d*7.7)],'leaf',[(a,c),(b,c),(b,d),(a,d)])
sheet=water.object('CoastalGateWater')
for uv in sheet.data.uv_layers.active.data:uv.uv=((uv.uv.x-.008)/.484,(uv.uv.y-.008)/.484)
parent=bpy.data.objects.new('CoastalGate',None);collection.objects.link(parent)
for o in [frame,sheet]:o.parent=parent
bpy.ops.object.select_all(action='DESELECT')
for o in [parent,frame,sheet]:o.select_set(True)
bpy.context.view_layer.objects.active=parent
bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Art/SummerRebuilt/Models/CoastalGate.fbx',use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,bake_anim=False,use_mesh_modifiers=True,path_mode='STRIP',add_leaf_bones=False)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/SummerRebuilt/CoastalGate.blend',copy=True)
print('gate exported',len(frame.data.polygons),len(sheet.data.polygons))
