import bpy, bmesh, json
from mathutils import Matrix

# 원본 문 애니메이션을 바꾸지 않고 정적인 프레임의 열린 경계만 막는다.
root = r'C:/Users/mbc/Desktop/NC_AI/DogLife'
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=root+'/Assets/MapDesigne/Winter/Door/Models/WinterIceDoor.fbx')
created = set(bpy.data.objects)-before
report=[]
for obj in created:
    if obj.type!='MESH' or not obj.name.startswith('Frame'):continue
    bm=bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.000001)
    fill=bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    report.append({'name':obj.name,'closedLoops':len(fill['faces']),'openEdges':sum(e.is_boundary for e in bm.edges)})
    bm.to_mesh(obj.data); bm.free()
    # 기존 Unity MeshFilter의 작은 로컬 좌표와 축을 그대로 유지한다.
    for v in obj.data.vertices:
        x,y,z=v.co
        v.co=(x*.01,-z*.01,y*.01)
    obj.data.update()
    obj.parent=None; obj.matrix_world=Matrix.Identity(4)
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.fbx(filepath=root+'/Assets/Art/WinterOriginal/Models/'+obj.name.replace('.001','')+'_Closed.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,bake_anim=False)
print(json.dumps(report))
for obj in created:bpy.data.objects.remove(obj,do_unlink=True)
