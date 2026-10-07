import bpy,math
from mathutils import Vector
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
# 기본 촉수 가중치부터 재생성해 반복 실행해도 동일한 결과를 만든다.
exec(compile(open(ROOT+'/ArtSource/Kraken/refine_summer_rig.py',encoding='utf-8').read(),'base_rig','exec'))
rig=bpy.data.objects['Kraken_Body_Rig'];obj=bpy.data.objects['SummerBoss_Body_Skin']
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for level in range(4):
 for sector in range(4):
  name='Hull_%02d_%02d'%(sector,level);b=rig.data.edit_bones.get(name) or rig.data.edit_bones.new(name)
  angle=sector*math.tau/4;b.head=(math.sin(angle)*1.2,math.cos(angle)*1.2,.8+level*1.6);b.tail=b.head+Vector((0,0,.8))
  b.parent=rig.data.edit_bones['Body%d'%level];b.use_connect=False
bpy.ops.object.mode_set(mode='OBJECT')
names={g.index:g.name for g in obj.vertex_groups}
weights=[]
for v in obj.data.vertices:weights.append({names[g.group]:g.weight for g in v.groups})
for level in range(4):
 for sector in range(4):
  name='Hull_%02d_%02d'%(sector,level)
  if not obj.vertex_groups.get(name):obj.vertex_groups.new(name=name)
for v,old in zip(obj.data.vertices,weights):
 core=sum(w for n,w in old.items() if n.startswith('Body'));final={n:w*.2 if n.startswith('Body') else w for n,w in old.items()}
 a=(math.atan2(v.co.x,v.co.y)%math.tau)/math.tau*4;lo=int(a)%4;af=a-int(a)
 z=max(0,min(3,(v.co.z-.8)/1.6));zi=int(z);zf=z-zi
 for s,sw in [(lo,1-af),((lo+1)%4,af)]:
  for l,lw in [(zi,1-zf),(min(3,zi+1),zf)]:
   name='Hull_%02d_%02d'%(s,l);final[name]=final.get(name,0)+core*.8*sw*lw
 selected=sorted(final.items(),key=lambda p:p[1],reverse=True)[:4];total=sum(w for _,w in selected)
 for g in list(v.groups):obj.vertex_groups[g.group].remove([v.index])
 for n,w in selected:
  if w>0:obj.vertex_groups[n].add([v.index],w/total,'REPLACE')
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Art/SummerBossAssembly/Models/SummerBoss_Body_Rigged.fbx',use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
bpy.data.libraries.write(ROOT+'/ArtSource/SummerBoss_BodyCoreRig.blend',{bpy.context.scene},fake_user=True)
print('Body bones',len(rig.data.bones),'unweighted',sum(not v.groups for v in obj.data.vertices))
