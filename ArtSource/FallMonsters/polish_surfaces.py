"""가을 조명에서 읽히도록 잎맥·나무결을 만들고 기존 가시를 입체 잎 모양으로 다듬는다."""
import bpy, math, numpy as np
from mathutils import Vector
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife/'
N=512
y,x=np.mgrid[0:N,0:N].astype(float)/(N-1)
rng=np.random.default_rng(47)
noise=rng.random((N,N))-.5
# 중앙 잎맥과 비스듬한 측맥, 얼룩을 한 장에 그립니다.
vein=np.exp(-((x-.5)/.014)**2)
branch=np.exp(-(np.sin((y-np.abs(x-.5)*.65)*math.pi*12)/.14)**2)
mottle=np.sin(x*31+y*19)*np.sin(y*43-x*17)*.045+noise*.035
tone=np.clip(.67+.20*y-.20*np.abs(x-.5)+mottle+.14*vein+.07*branch,0,1)
def texture(name,arr):
 im=bpy.data.images.get(name) or bpy.data.images.new(name,N,N,alpha=True)
 rgba=np.ones((N,N,4),dtype=np.float32);rgba[:,:,:3]=arr
 im.pixels.foreach_set(rgba.ravel());im.filepath_raw=ROOT+'Assets/Art/FallMonsters/Textures/'+name+'.png';im.file_format='PNG';im.save()
 return im
leaf=texture('Autumn_LeafGrain',np.stack([tone,tone*.71,tone*.39],axis=-1))
grain=np.sin(x*math.pi*34+np.sin(y*14)*.7)+.4*np.sin(x*math.pi*94+y*3)
woodtone=np.clip(.58+.12*grain+.09*np.sin(y*20+x*6)+noise*.05,0,1)
wood=texture('Autumn_ExtensionBark',np.stack([woodtone,woodtone*.70,woodtone*.43],axis=-1))
def textured(mat,im,color):
 mat.use_nodes=True;p=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
 node=next((n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.label=='Fall polish'),None)
 if not node:node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.label='Fall polish'
 node.image=im;mat.node_tree.links.new(node.outputs['Color'],p.inputs['Base Color']);p.inputs['Roughness'].default_value=.86
 mat.diffuse_color=(*color,1)
for mat in bpy.data.materials:
 if mat.name.startswith('BoneCrow_FlightLeaf_'):textured(mat,leaf,(.7,.5,.25))
 if mat.name=='Scarecrow_ExtensionWood':textured(mat,wood,(.65,.45,.26))
# 기존 날개 각 조각의 UV를 잎 전체 좌표로 연결합니다. 기존 뼈/웨이트는 유지합니다.
mesh=bpy.data.objects['BoneCrow_Body'].data
leaf_faces=[p for p in mesh.polygons if mesh.materials[p.material_index].name.startswith('BoneCrow_FlightLeaf_')]
for k,p in enumerate(leaf_faces):
 local=k%24;j=local//4;side=(local//2)%2;back=local%2
 coords=[(side*.5,j/6),(side*.5,(j+1)/6),((side+1)*.5,(j+1)/6),((side+1)*.5,j/6)]
 if back:coords=list(reversed(coords))
 for li,uv in zip(p.loop_indices,coords):mesh.uv_layers.active.data[li].uv=uv
# 48개의 가시 배치는 그대로 두고 단면이 있는 뾰족한 잎으로 교체합니다.
obj=bpy.data.objects['LeafSpike'];v=[];f=[];uv=[]
for j in range(10):
 t=j/9;width=.25*math.sin(math.pi*(.09+.91*t))*(1-.35*t)
 z=t*1.8;curve=.10*t*t;thick=.055*math.sin(math.pi*t)+.008
 v.extend([(-width,curve,z),(0,curve-thick,z),(width,curve,z),(0,curve+thick,z)])
for j in range(9):
 for k in range(4):
  f.append((j*4+k,(j+1)*4+k,(j+1)*4+(k+1)%4,j*4+(k+1)%4))
  a=[0,.5,1,.5][k];b=[0,.5,1,.5][(k+1)%4]
  uv.append([(a,j/9),(a,(j+1)/9),(b,(j+1)/9),(b,j/9)])
f.extend([(3,2,1,0),(36,37,38,39)]);uv.extend([[(0,0)]*4,[(.5,1)]*4])
new=bpy.data.meshes.new('LeafSpike_Ridged');new.from_pydata(v,[],f);new.update();layer=new.uv_layers.new(name='UVMap')
for p,coords in zip(new.polygons,uv):
 for li,c in zip(p.loop_indices,coords):layer.data[li].uv=c
obj.data=new
for name in ['Scarecrow','BoneCrow','LeafSpike']:
 obj=bpy.data.objects[name if name=='LeafSpike' else name+'_Body'];rig=obj.parent
 for o in bpy.context.scene.objects:o.select_set(False)
 obj.select_set(True);bpy.context.view_layer.objects.active=obj
 if rig:
  rig.animation_data.action=None
  for p in rig.pose.bones:p.matrix_basis.identity()
  rig.select_set(True);pos=rig.location.copy();rig.location=(0,0,0)
 bpy.context.view_layer.update()
 bpy.ops.export_scene.fbx(filepath=ROOT+'Assets/Art/FallMonsters/Models/'+name+'.fbx',use_selection=True,add_leaf_bones=False,bake_anim=bool(rig),bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,axis_forward='-Z',axis_up='Y',path_mode='RELATIVE')
 if rig:rig.location=pos
bpy.context.view_layer.update();bpy.ops.wm.save_as_mainfile(filepath=ROOT+'ArtSource/FallMonsters/FallMonsters.blend')
print('Leaf and bark textures, wing UVs and ridged spike exported')
