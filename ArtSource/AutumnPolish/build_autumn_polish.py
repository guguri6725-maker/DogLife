import bpy, bmesh, math, random, os, json
from mathutils import Vector, Quaternion

# 기존 방의 벽 좌표와 나무 실루엣을 유지한 채 시각 메시만 제작한다.
ROOT = 'C:/Users/mbc/Desktop/NC_AI/DogLife'
OUT = ROOT + '/Assets/Art/AutumnPolish/Models'
os.makedirs(OUT, exist_ok=True)
collection = bpy.data.collections.get('DogLife_AutumnPolish')
if not collection:
    collection = bpy.data.collections.new('DogLife_AutumnPolish')
    bpy.context.scene.collection.children.link(collection)
bark = bpy.data.materials.get('Polish_Bark') or bpy.data.materials.new('Polish_Bark')
leaf = bpy.data.materials.get('Polish_Leaf') or bpy.data.materials.new('Polish_Leaf')
stone = bpy.data.materials.get('Polish_Stone') or bpy.data.materials.new('Polish_Stone')
for mat, tex in [(bark,'Maple_Bark'),(leaf,'Maple_Leaf'),(stone,'Autumn_Stone')]:
    mat.use_nodes=True
    bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Roughness'].default_value=.9
    node=mat.node_tree.nodes.new('ShaderNodeTexImage')
    node.image=bpy.data.images.load(ROOT+'/Assets/Art/AutumnCrafted/Textures/'+tex+'.png',check_existing=True)
    mat.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
    if mat==leaf:mat.node_tree.links.new(node.outputs['Alpha'],bs.inputs['Alpha'])

# 이미 사용 중인 잎 UV와 닫힌 가지 메시 생성 규칙을 재사용한다.
source=open(ROOT+'/ArtSource/AutumnCrafted/build_autumn.py',encoding='utf-8').read()
exec(source[source.index('class Mesh:'):source.index('def export(')])

def export(name, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,bake_anim=False,path_mode='STRIP',add_leaf_bones=False)
    print(name, [(o.name,sum(len(p.vertices)-2 for p in o.data.polygons)) for o in objects if o.type=='MESH'])
    for obj in objects:obj.hide_set(True)

def maple(seed, name, low=False):
    rng=random.Random(seed); wood=Mesh(); foliage=Mesh()
    # 원래의 12개 주가지 위치를 유지하고 세갈래 잔가지와 주름진 수피를 더한다.
    trunk=[Vector((math.sin(i*.6)*.24,math.cos(i*.7)*.19,i*.9)) for i in range(8)]
    wood.tube(trunk,[.64,.48,.39,.32,.26,.20,.13,.035],12 if low else 28)
    for i in range(8):
        a=i*math.tau/8
        wood.tube([(math.cos(a)*2,math.sin(a)*2,.04),(math.cos(a)*.8,math.sin(a)*.8,.22),(0,0,.8)],[.05,.22,.4],8 if low else 16)
    for i in range(12):
        a=i*2.399+seed*.2;z=2.3+(i%4)*.9;start=Vector((0,0,z));r=2.3+rng.random()*1.5
        end=Vector((math.cos(a)*r,math.sin(a)*r,z+1.8+rng.random()*.8));mid=start.lerp(end,.55)+Vector((0,0,-.28))
        wood.tube([start,start.lerp(mid,.5),mid,mid.lerp(end,.5),end],[.24,.19,.15,.095,.04],9 if low else 18)
        for j in range(4):
            b=a+(j-1.5)*.55;tip=end+Vector((math.cos(b)*(.8+rng.random()),math.sin(b)*(.8+rng.random()),(j%2)*.5))
            base=mid.lerp(end,.5+j*.1)
            wood.tube([base,base.lerp(tip,.5),tip],[.065,.038,.009],6 if low else 10)
            for twig in range(3):
                t=tip+Vector((math.cos(b+twig*2.1)*.55,math.sin(b+twig*2.1)*.55,.15))
                wood.tube([base.lerp(tip,.65),t],[.018,.003],4 if low else 6)
            # 낮은 LOD에서도 수관을 유지하도록 잎 크기를 보정한다.
            for k in range(128):
                offset=Vector((rng.uniform(-.85,.85),rng.uniform(-.85,.85),rng.uniform(-.4,.5)))
                p=tip+offset;q=Quaternion(Vector((0,0,1)),rng.random()*math.tau)@Quaternion(Vector((1,0,0)),rng.uniform(-.8,.8))
                size=rng.uniform(.28,.49)
                if not low or k%4==0:foliage.blade(p,q,size*(1.35 if low else 1))
    if not low:
        for k in range(28):
            a=k*math.tau/28;pts=[];rr=[]
            for j in range(22):
                t=j/3.0;i=min(6,int(t));f=t-i;c=trunk[i].lerp(trunk[i+1],f);r=[.64,.48,.39,.32,.26,.20,.13,.035][i]*(1-f)+[.64,.48,.39,.32,.26,.20,.13,.035][i+1]*f
                pts.append(c+Vector((math.cos(a+.07*math.sin(t*3))*r,math.sin(a+.07*math.sin(t*3))*r,0)));rr.append(.017*(1-j/27))
            wood.tube(pts,rr,5)
    return [wood.object(name+'_Wood',bark),foliage.object(name+'_Leaves',leaf)]

def make_trees():
    for i in range(3):
        for low in [False,True]:
            name='MaplePolish_'+str(i+1)+('_Far' if low else '_Near')
            export(name,maple(130+i*51,name,low))

def stone_block(mesh, center, size, rng, transform, low):
    x,y,z=center;w,d,h=size
    # 서로 다른 모서리 파손과 볼록한 앞면을 가진 8각 돌. 뒷면까지 닫는다.
    outline=[(-.5,-.34),(-.36,-.5),(.34,-.5),(.5,-.32),(.5,.35),(.32,.5),(-.35,.5),(-.5,.33)]
    outline=[(px*rng.uniform(.94,1.0),pz*rng.uniform(.94,1.0)) for px,pz in outline]
    rings=[]
    for depth,shrink in ([(-d*.46,.96),(d*.46,.96)] if low else [(-d*.5,.84),(-d*.25,1),(d*.25,1),(d*.5,.84)]):
        rings.append([transform((x+px*w*shrink,y+depth,z+pz*h*shrink)) for px,pz in outline])
    def face(points):
        mesh.face(points,[(p[0]*.55+p[1]*.24,p[2]*.55) for p in points])
    for a,b in zip(rings,rings[1:]):
        for k in range(8):face([a[k],a[(k+1)%8],b[(k+1)%8],b[k]])
    face(list(reversed(rings[0])));face(rings[-1])

def room_detail(plan, index, low):
    rng=random.Random(810+index);walls=Mesh();litter=Mesh()
    for wall in plan['walls']:
        width,height,depth=wall['scale'];px,py,pz=wall['pos'];angle=math.radians(wall['rot'])
        # Unity X/Y/Z를 Blender X/-Z/Y로 변환해 기존 벽과 정확히 겹친다.
        def trans(p):
            x,y,z=p
            return (-(px+math.cos(angle)*x+math.sin(angle)*y),-(pz-math.sin(angle)*x+math.cos(angle)*y),py+z)
        rows=round(height/.40);cols=round(width/.72)
        for row in range(rows):
            cuts=[-width/2]+[-width/2+(j+(.5 if row%2 else 0))*width/cols for j in range(1,cols)]+[width/2]
            for start,end in zip(cuts,cuts[1:]):
                for side in [-1,1]:
                    cx=(start+end)/2;cz=-height/2+(row+.5)*height/rows
                    d=.22+rng.random()*.07
                    stone_block(walls,(cx,side*(depth*.5-d*.52),cz),(end-start-.025,d,height/rows-.028),rng,trans,low)
        # 돌 틈새 아래의 소량 낙엽은 이동 통로가 아닌 벽 밑으로 한정한다.
        for k in range(int(width*13)):
            x=rng.uniform(-width/2+.1,width/2-.1);side=(-1)**k
            p=Vector(trans((x,side*(depth*.5+rng.uniform(.02,.75)),.27-py+rng.uniform(.015,.06))))
            q=Quaternion(Vector((0,0,1)),rng.random()*math.tau)
            if not low or k%3==0:litter.blade(p,q,rng.uniform(.18,.38))
    # 나무 밑의 얇은 낙엽 더미는 기존 줄기 위치를 그대로 따른다.
    for tree in plan['trees']:
        tx,ty,tz=tree['pos']
        for k in range(260):
            a=rng.random()*math.tau;r=rng.uniform(.45,2.1)
            p=Vector((-tx+math.cos(a)*r,-tz+math.sin(a)*r,.29+.11*(1-r/2.2)+rng.random()*.03))
            q=Quaternion(Vector((0,0,1)),rng.random()*math.tau)@Quaternion(Vector((1,0,0)),rng.uniform(-.25,.25))
            if not low or k%3==0:litter.blade(p,q,rng.uniform(.19,.35))
    suffix='_LOD1' if low else '_LOD0'
    prefix='' if index==0 else 'R'+str(index+1).zfill(2)+'_'
    wall_object=walls.object(prefix+'AutumnStonework'+suffix,stone)
    bm=bmesh.new();bm.from_mesh(wall_object.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(wall_object.data);bm.free()
    return [wall_object,litter.object(prefix+'AutumnLeafDrifts'+suffix,leaf)]

def make_room(index):
    plans=json.load(open(ROOT+'/ArtSource/AutumnPolish/room_layout.json',encoding='utf-8'))
    obs=[]
    for low in [False,True]:obs+=room_detail(plans[index],index,low)
    parent=bpy.data.objects.new('AutumnDetailRoom'+str(index+1).zfill(2),None);collection.objects.link(parent)
    for ob in obs:ob.parent=parent
    export(parent.name,[parent]+obs)

def save_source():
    # 다른 작업 모델을 포함하지 않고 이 작업의 컬렉션만 압축 저장한다.
    source_scene=bpy.data.scenes.new('AutumnPolishSource')
    source_scene.collection.children.link(collection)
    bpy.data.libraries.write(ROOT+'/ArtSource/AutumnPolish/AutumnPolish.blend',{source_scene},fake_user=True,compress=True)
    bpy.data.scenes.remove(source_scene)

print('Autumn polish authoring functions ready')
