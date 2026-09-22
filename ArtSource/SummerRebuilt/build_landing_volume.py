"""접점 거품의 중앙은 부풀리고 외곽과 바닥은 수면 아래에 묻는 닫힌 메시입니다."""
import bpy, math
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
nx,ny=40,32
verts=[];uvs=[];faces=[]
for j in range(ny+1):
    v=j/ny;z=abs(v*2-1)
    for i in range(nx+1):
        u=i/nx;x=abs(u*2-1)
        taper=max(0,1-x*x)**.6
        crest=(.23*math.exp(-(z/.25)**2)+.065*math.exp(-((z-.47)/.14)**2))*taper
        height=-.065+crest+.024*math.sin(u*27+v*8)*max(0,1-z)**2*taper
        verts.append((u-.5,v-.5,height));uvs.append((u,v))
for j in range(ny):
    for i in range(nx):
        a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
# 외곽과 바닥을 닫아 옆에서 보아도 뚫린 평면이 남지 않게 합니다.
rim=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,ny+1)]+[ny*(nx+1)+i for i in range(nx-1,-1,-1)]+[j*(nx+1) for j in range(ny-1,0,-1)]
bottom=[]
for a in rim:
    bottom.append(len(verts));verts.append((verts[a][0],verts[a][1],-.13));uvs.append(uvs[a])
for i in range(len(rim)):
    k=(i+1)%len(rim);faces.append((rim[i],bottom[i],bottom[k],rim[k]))
faces.append(tuple(reversed(bottom)))
mesh=bpy.data.meshes.new('CoastalLandingVolume');mesh.from_pydata(verts,[],faces);mesh.update()
uv=mesh.uv_layers.new(name='UVMap')
for p in mesh.polygons:
    p.use_smooth=True
    for li in p.loop_indices:uv.data[li].uv=uvs[mesh.loops[li].vertex_index]
collection=bpy.data.collections.new('CoastalLandingVolume_Source');bpy.context.scene.collection.children.link(collection)
obj=bpy.data.objects.new('CoastalLandingVolume',mesh);collection.objects.link(obj)
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
bpy.ops.export_scene.fbx(filepath=ROOT+'/Assets/Art/SummerRebuilt/Models/CoastalLandingVolume.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/SummerRebuilt/CoastalLandingVolume.blend',copy=True)
print('landing volume exported',len(verts),len(faces))
