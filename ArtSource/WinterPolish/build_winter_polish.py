import bpy, bmesh, math, random, os, json
from mathutils import Vector, Matrix, noise

ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
OUT=ROOT+'/Assets/Art/WinterPolish/Models'
os.makedirs(OUT,exist_ok=True)
COL=bpy.data.collections.get('DogLife_WinterPolish')
if not COL:
    COL=bpy.data.collections.new('DogLife_WinterPolish');bpy.context.scene.collection.children.link(COL)
CONFIG=json.load(open(ROOT+'/ArtSource/WinterPolish/assets.json',encoding='utf-8'))

def material(name,color):
    mat=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes=True
    bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=color;bs.inputs['Roughness'].default_value=.6
    return mat
BASE=material('WinterPolish_Base',(.35,.48,.52,1))
SNOW=material('WinterPolish_Snow',(.83,.93,1,1))
ICE=material('WinterPolish_Ice',(.22,.55,.72,1))

def owned(o):
    for c in list(o.users_collection):c.objects.unlink(o)
    COL.objects.link(o)

def icicle(vertices,faces,p,length,radius,sides,rings,rng):
    # 높이마다 굵기를 바꾸어 얼어붙은 물방울 형태의 고드름을 만든다.
    start=len(vertices)
    for j in range(rings+1):
        t=j/rings;r=radius*((1-t)**.78+.015)
        for k in range(sides):
            a=k*math.tau/sides
            vertices.append(tuple(p+Vector((math.cos(a)*r,math.sin(a)*r,-t*length))))
    for j in range(rings):
        for k in range(sides):
            a=start+j*sides+k;b=start+j*sides+(k+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.append(tuple(start+k for k in reversed(range(sides))))
    faces.append(tuple(start+rings*sides+k for k in range(sides)))

def snow_and_ice(base,name,low):
    rng=random.Random(sum(map(ord,name)));verts=[];faces=[];icev=[];icef=[]
    source=base.data;source.update()
    # 기존 상단 면 위에 두께를 가진 적설층을 얹어 구조물의 원래 윤곽을 유지한다.
    tree=any(t in name for t in ['Pine','Tree','Shrub','Grass','Log'])
    threshold=.72 if tree else .48
    selected=[]
    for poly in source.polygons:
        if poly.normal.z<threshold:continue
        if tree and rng.random()<.55:continue
        if low and rng.random()<.38:continue
        selected.append(poly)
    # 모서리를 공유시켜 눈 표면의 균열을 막는다.
    used=sorted(set(v for p in selected for v in p.vertices));lookup={v:i for i,v in enumerate(used)}
    for v in used:
        co=source.vertices[v].co.copy()
        depth=.035+.035*(.5+.5*math.sin(co.x*2.7+co.y*3.2))
        verts.append(tuple(co+Vector((0,0,depth))))
    for poly in selected:faces.append(tuple(lookup[v] for v in poly.vertices))
    edge_count={}
    for poly in selected:
        pv=list(poly.vertices)
        for a,b in zip(pv,pv[1:]+pv[:1]):
            key=tuple(sorted((a,b)));edge_count[key]=edge_count.get(key,0)+1
    edges=[edge for edge,count in edge_count.items() if count==1]
    for a,b in edges:
        n=len(verts);verts.extend([tuple(source.vertices[a].co-Vector((0,0,.018))),tuple(source.vertices[b].co-Vector((0,0,.018)))])
        faces.append((lookup[a],n,n+1,lookup[b]))
    # 선반/절벽 가장자리에서만 고드름이 내려오도록 한다.
    rng.shuffle(edges)
    limit=(8 if tree else 32 if 'Wall' in name else 14)
    for a,b in edges[:limit]:
        p=(source.vertices[a].co+source.vertices[b].co)*.5
        if p.z<.45:continue
        length=rng.uniform(.18,.65)*(1.35 if 'Wall' in name else 1)
        radius=rng.uniform(.025,.065)
        icicle(icev,icef,p+Vector((0,0,.015)),length,radius,5 if low else 7,3 if low else 5,rng)
    result=[]
    for suffix,v,f,mat in [('Snow',verts,faces,SNOW),('Icicles',icev,icef,ICE)]:
        mesh=bpy.data.meshes.new(name+suffix);mesh.from_pydata(v,[],f);mesh.update()
        uv=mesh.uv_layers.new(name='UVMap')
        for poly in mesh.polygons:
            poly.use_smooth=True
            for li in poly.loop_indices:
                co=mesh.vertices[mesh.loops[li].vertex_index].co;uv.data[li].uv=(co.x*.5,co.y*.5)
        obj=bpy.data.objects.new(name+suffix,mesh);COL.objects.link(obj);mesh.materials.append(mat);result.append(obj)
    return result

def build(index):
    item=CONFIG[index];name=item['name']
    # 같은 작업 에셋을 재생성할 때 사용자 오브젝트는 건드리지 않는다.
    for o in list(COL.objects):
        if o.name in [name+'_LOD0',name+'_LOD1',name+'_Polished']:bpy.data.objects.remove(o,do_unlink=True)
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=ROOT+'/'+item['mesh'])
    imported=[o for o in bpy.data.objects if o not in before]
    original=next(o for o in imported if o.type=='MESH')
    # FBX의 축 변환을 메시로 굽는다. Unity 오브젝트의 위치/크기는 바꾸지 않는다.
    original.data.transform(original.matrix_world);original.parent=None;original.matrix_world=Matrix.Identity(4);owned(original)
    original.data.materials.clear();original.data.materials.append(BASE)
    outputs=[];counts=[]
    for low in [False,True]:
        obj=original.copy();obj.data=original.data.copy();COL.objects.link(obj)
        obj.hide_set(False);bpy.context.view_layer.objects.active=obj;obj.select_set(True)
        if not low and not name.startswith('FirstRoom_'):
            bm=bmesh.new();bm.from_mesh(obj.data)
            bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=1,use_grid_fill=True)
            bm.normal_update()
            # 추가된 점으로 암석 표면과 얼음 단면의 작은 굴곡을 실제로 조형한다.
            for v in bm.verts:
                strength=.012 if 'Crystal' in name else .025
                v.co+=v.normal*(noise.noise_vector(v.co*3.7).x*strength)
            bm.to_mesh(obj.data);bm.free()
        if low:
            mod=obj.modifiers.new('Distance simplification','DECIMATE');mod.ratio=.55
            bpy.ops.object.modifier_apply(modifier=mod.name)
        extras=snow_and_ice(obj,name,low)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        for o in extras:o.select_set(True)
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.join()
        obj.name=name+('_LOD1' if low else '_LOD0');outputs.append(obj)
        counts.append(sum(len(p.vertices)-2 for p in obj.data.polygons))
    for o in imported:
        if o.name in bpy.data.objects:bpy.data.objects.remove(o,do_unlink=True)
    parent=bpy.data.objects.new(name+'_Polished',None);COL.objects.link(parent)
    for o in outputs:o.parent=parent
    bpy.ops.object.select_all(action='DESELECT')
    for o in [parent]+outputs:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=parent
    bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,bake_anim=False,path_mode='STRIP',add_leaf_bones=False)
    for o in [parent]+outputs:o.hide_set(True)
    print(name,counts)
    return counts

print('Winter asset authoring ready:',len(CONFIG))
