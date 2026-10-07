import bpy, math, os
from mathutils import Vector

ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
scene=bpy.context.scene
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def edit(rig):
 bpy.ops.object.select_all(action='DESELECT');rig.hide_set(False);rig.select_set(True)
 bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
def reset(rig):
 rig.animation_data_clear()
 for p in rig.pose.bones:p.matrix_basis.identity()

# 기존 애니메이션 경로는 보존하고 실제 피부를 움직일 보조 관절을 추가한다.
rig=bpy.data.objects['Kraken_Tentacle_Rig'];obj=bpy.data.objects['SummerBoss_Tentacle_Skin'];reset(rig);edit(rig)
for j in range(24):
 name='Flex_%02d'%j
 b=rig.data.edit_bones.get(name) or rig.data.edit_bones.new(name)
 b.head=(0,0,j*8/24);b.tail=(0,0,(j+1)*8/24)
 b.parent=rig.data.edit_bones['Tendril%d'%(j//2)];b.use_connect=False
bpy.ops.object.mode_set(mode='OBJECT');obj.vertex_groups.clear()
groups=[obj.vertex_groups.new(name='Flex_%02d'%j) for j in range(24)]
for v in obj.data.vertices:
 q=max(0,min(23,v.co.z*3-.5));lo=int(q);hi=min(23,lo+1);f=q-lo
 groups[lo].add([v.index],1-f,'REPLACE')
 if hi!=lo:groups[hi].add([v.index],f,'REPLACE')

rig=bpy.data.objects['Kraken_Body_Rig'];obj=bpy.data.objects['SummerBoss_Body_Skin'];reset(rig);edit(rig)
for arm in range(8):
 angle=arm*math.tau/8;direction=Vector((math.sin(angle),math.cos(angle),0))
 for j in range(6):
  name='Mantle_%02d_%02d'%(arm,j);b=rig.data.edit_bones.get(name) or rig.data.edit_bones.new(name)
  b.head=direction*(1.35+j*.72)+Vector((0,0,1.9-j*.22))
  b.tail=direction*(1.35+(j+1)*.72)+Vector((0,0,1.9-(j+1)*.22))
  b.parent=rig.data.edit_bones['Body0'] if j==0 else rig.data.edit_bones['Mantle_%02d_%02d'%(arm,j-1)]
  b.use_connect=j>0
bpy.ops.object.mode_set(mode='OBJECT');obj.vertex_groups.clear()
groups={n:obj.vertex_groups.new(name=n) for n in [b.name for b in rig.data.bones if b.name!='Root']}
for v in obj.data.vertices:
 x,y,z=v.co;r=math.hypot(x,y)
 mix=smooth(1.35,2.8,r)*(1-smooth(1.55,3.2,z))*.9
 weights={};q=max(0,min(4,z/1.6-.5));lo=int(q);f=q-lo
 weights['Body%d'%lo]=(1-mix)*(1-f)
 if lo<4:weights['Body%d'%(lo+1)]=(1-mix)*f
 aq=(math.atan2(x,y)%math.tau)/math.tau*8;a0=int(aq)%8;af=aq-int(aq)
 jq=max(0,min(5,(r-1.35)/.72-.5));j0=int(jq);jf=jq-j0
 for arm,aw in [(a0,1-af),((a0+1)%8,af)]:
  for j,jw in [(j0,1-jf),(min(5,j0+1),jf)]:
   n='Mantle_%02d_%02d'%(arm,j);weights[n]=weights.get(n,0)+mix*aw*jw
 # Unity의 네 가중치 설정에서도 경계가 벌어지지 않도록 정규화한다.
 selected=sorted(weights.items(),key=lambda p:p[1],reverse=True)[:4];total=sum(w for _,w in selected)
 for n,w in selected:
  if w>0:groups[n].add([v.index],w/total,'REPLACE')

for kind in ['Body','Tentacle']:
 rig=bpy.data.objects['Kraken_'+kind+'_Rig'];obj=bpy.data.objects['SummerBoss_'+kind+'_Skin'];reset(rig)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);obj.hide_set(False);obj.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Art/SummerBossAssembly/Models/SummerBoss_'+kind+'_Rigged.fbx',use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
 print(kind,'bones',len(rig.data.bones),'vertices',len(obj.data.vertices),'unweighted',sum(not v.groups for v in obj.data.vertices))
bpy.data.libraries.write(ROOT+'/ArtSource/SummerBoss_RefinedRig.blend',{scene},fake_user=True)
