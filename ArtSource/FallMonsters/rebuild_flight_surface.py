"""기존 두개골·갈비뼈는 보존하고 펼친 날개와 꼬리를 비행 체형으로 제작한다."""
import bpy,math,bmesh
from mathutils import Vector,Matrix
obj=bpy.data.objects['BoneCrow_Body'];source=bpy.data.meshes['BoneCrow_StandingSource'];rig=obj.parent
verts=[];faces=[];uvs=[];weights=[];materials=[]
def add_face(points,uv,weight,mat):
    base=len(verts);verts.extend(points);faces.append(list(range(base,base+len(points))));uvs.append(uv);materials.append(mat)
    weights.extend([weight.copy() for _ in points])
def rotate(deg):return Matrix.Rotation(math.radians(deg),3,'X')
for polygon in source.polygons:
    center=sum((source.vertices[i].co for i in polygon.vertices),Vector())/len(polygon.vertices);x,y,z=center
    # 원래 손발과 접힌 날개를 제외하고 해골·가슴의 디테일만 보존한다.
    if abs(x)<.31 and y<-.23 and z>.73:
        fn=lambda p:rotate(-35)@(p-Vector((0,-.40,1.02)))+Vector((0,-.59,.86));bone='Head'
    elif abs(x)<.31 and -.24<=y<.28 and .44<z<1.02:
        fn=lambda p:rotate(62)@(p-Vector((0,0,.70)))+Vector((0,0,.68));bone='Body'
    else:continue
    pts=[fn(source.vertices[source.loops[i].vertex_index].co) for i in polygon.loop_indices]
    tex=[tuple(source.uv_layers.active.data[i].uv) for i in polygon.loop_indices]
    add_face(pts,tex,{bone:1},polygon.material_index)
original_materials=list(source.materials)
def material(name,color,roughness=.8):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=roughness
    original_materials.append(m);return len(original_materials)-1
bone_mat=material('BoneCrow_FlightBone',(.64,.51,.34))
leaf_mats=[material('BoneCrow_FlightLeaf_'+str(i),c) for i,c in enumerate([(.22,.08,.025),(.32,.12,.035),(.43,.19,.055),(.16,.055,.025),(.36,.15,.065)])]
vein_mat=material('BoneCrow_LeafVein',(.095,.035,.012))
def tube(a,b,r,bone,mat,segments=9):
    a=Vector(a);b=Vector(b);axis=(b-a).normalized();u=axis.cross(Vector((0,0,1)))
    if u.length<.01:u=axis.cross(Vector((0,1,0)))
    u.normalize();v=axis.cross(u)
    for i in range(segments):
        p=i*math.tau/segments;q=(i+1)*math.tau/segments
        add_face([a+r*(u*math.cos(p)+v*math.sin(p)),a+r*(u*math.cos(q)+v*math.sin(q)),b+r*.82*(u*math.cos(q)+v*math.sin(q)),b+r*.82*(u*math.cos(p)+v*math.sin(p))],[(0,0),(1,0),(1,1),(0,1)],{bone:1},mat)
def leaf(a,b,width,bone,index):
    a=Vector(a);b=Vector(b);axis=(b-a).normalized();side=Vector((-axis.y,axis.x,0)).normalized()
    # 양면 입체 잎깃: 가운데 능선과 볼록한 단면이 단순 평면처럼 보이지 않게 한다.
    rows=[]
    for j in range(7):
        t=j/6;center=a.lerp(b,t)+Vector((0,0,.04*math.sin(t*math.pi)))
        w=width*math.sin(math.pi*(.08+.92*t))*(1-.32*t)
        rows.append([center-side*w,center+Vector((0,0,.025*math.sin(t*math.pi))),center+side*w])
    for j in range(6):
        for k in range(2):
            pts=[rows[j][k],rows[j+1][k],rows[j+1][k+1],rows[j][k+1]]
            add_face(pts,[(0,0),(0,1),(1,1),(1,0)],{bone:1},leaf_mats[index%len(leaf_mats)])
            add_face([p-Vector((0,0,.015)) for p in reversed(pts)],[(0,0),(0,1),(1,1),(1,0)],{bone:1},leaf_mats[(index+3)%len(leaf_mats)])
    tube(a,b,.009,bone,vein_mat,5)
for side,sgn in [('Left',1),('Right',-1)]:
    shoulder=(sgn*.21,-.04,.78);elbow=(sgn*.63,-.04,.88);wrist=(sgn*1.0,.02,.84)
    tube(shoulder,elbow,.045,side+'Wing',bone_mat)
    tube(elbow,wrist,.036,side+'WingTip',bone_mat)
    for j in range(11):
        t=j/10
        a=(sgn*(.63+.35*t),-.015+.075*t,.87-.03*t)
        b=(sgn*(.93+.68*t),.68-.43*t,.77+.015*math.sin(j))
        leaf(a,b,.14,side+'WingTip',j)
    for j in range(9):
        t=j/8;a=(sgn*(.22+.42*t),-.06,.81+.06*t);b=(sgn*(.27+.55*t),.57+.08*t,.72+.03*t)
        leaf(a,b,.105,side+'Wing',j+2)
    for j in range(7):
        t=j/6;leaf((sgn*(.26+.70*t),-.07,.86),(sgn*(.36+.71*t),.20,.89),.07,side+'Wing' if t<.5 else side+'WingTip',j+1)
    # 다리는 배 아래로 짧게 접고 발톱은 뒤쪽으로 둔다.
    hip=(sgn*.14,.14,.50);knee=(sgn*.18,.30,.37);foot=(sgn*.16,.46,.46)
    tube(hip,knee,.026,side+'Leg',bone_mat);tube(knee,foot,.022,side+'Leg',bone_mat)
    for j in range(3):tube(foot,(sgn*(.12+j*.035),.56,.39),.010,side+'Leg',bone_mat,7)
for j in range(9):
    t=(j-4)/4;leaf((t*.07,.27,.63),(t*.30,1.11-.13*abs(t),.59),.105,'Tail',j+1)
tube((0,-.19,.71),(0,-.39,.76),.06,'Head',bone_mat)
mesh=bpy.data.meshes.new('BoneCrow_FlightSurface');mesh.from_pydata(verts,[],faces);mesh.update()
for m in original_materials:mesh.materials.append(m)
uv=mesh.uv_layers.new(name='UVMap')
for p,coords,mat in zip(mesh.polygons,uvs,materials):
    p.material_index=mat;p.use_smooth=True
    for li,c in zip(p.loop_indices,coords):uv.data[li].uv=c
obj.data=mesh
groups={p.name:obj.vertex_groups.new(name=p.name) for p in rig.pose.bones}
for i,w in enumerate(weights):
    for bone,amount in w.items():groups[bone].add([i],amount,'REPLACE')
# 같은 위치의 정점을 합쳐 뼈와 잎의 표면 음영을 부드럽게 연결한다.
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bm.normal_update();bm.to_mesh(mesh);bm.free()
rig.animation_data.action=None
for p in rig.pose.bones:p.matrix_basis.identity()
for o in bpy.context.scene.objects:o.hide_set(False);o.select_set(False)
obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
pos=rig.location.copy();rig.location=(0,0,0);bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath='C:/Users/mbc/Desktop/NC_AI/DogLife/Assets/Models/Monster/Autumn/BoneCrow/BoneCrow.fbx',use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,axis_forward='-Z',axis_up='Y')
rig.location=pos;bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath='C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/FallMonsters/FallMonsters.blend')
print('Flight surface',len(verts),'vertices')
