import bpy,bmesh,json,math,pathlib
from mathutils import Vector,kdtree
ROOT=pathlib.Path('C:/Users/mbc/Desktop/NC_AI/DogLife/Temp/SpringVegetationOptimizationWork')

def optimize(path):
    data=json.loads(path.read_text(encoding='utf-8-sig'));bm=bmesh.new()
    verts=[bm.verts.new(p) for p in data['vertices']]
    uv=bm.loops.layers.uv.new('UVMap');normal=bm.loops.layers.float_vector.new('OriginalNormal')
    before=0
    for slot,indices in enumerate(data['parts']):
        for i in range(0,len(indices),3):
            ids=indices[i:i+3];before+=1
            try:f=bm.faces.new([verts[j] for j in ids])
            except ValueError:continue
            f.material_index=slot
            for loop,j in zip(f.loops,ids):
                loop[uv].uv=data['uv'][j];loop[normal]=data['normals'][j]
    # UVと法線はループに保持し、同一位置の頂点だけ接続します。
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001);bm.normal_update();bm.verts.ensure_lookup_table()
    seen=set();islands=[]
    for v in bm.verts:
        if v in seen:continue
        stack=[v];seen.add(v);vs=[];fs=set()
        while stack:
            q=stack.pop();vs.append(q);fs.update(q.link_faces)
            for e in q.link_edges:
                w=e.other_vert(q)
                if w not in seen:seen.add(w);stack.append(w)
        if not fs:continue
        lo=Vector([min(v.co[a] for v in vs) for a in range(3)]);hi=Vector([max(v.co[a] for v in vs) for a in range(3)])
        closed=all(e.is_manifold for v in vs for e in v.link_edges)
        planes=[(f.normal.copy(),f.verts[0].co.copy()) for f in fs]
        convex=closed and len(vs)<=64 and all(n.dot(v.co-p)<.00001 for n,p in planes for v in vs)
        islands.append(dict(vs=vs,fs=fs,lo=lo,hi=hi,center=(lo+hi)/2,radius=(hi-lo).length/2,convex=convex,planes=planes))
    tree=kdtree.KDTree(len(islands));maxradius=max((g['radius'] for g in islands),default=0)
    for i,g in enumerate(islands):tree.insert(g['center'],i)
    tree.balance();remove=set();hidden=0
    # 別の閉じた凸形状の内部に完全に入る島だけ削除します。部分的な重なりは残します。
    for i,g in enumerate(islands):
        for _,j,_ in tree.find_range(g['center'],maxradius+g['radius']):
            if i==j:continue
            h=islands[j]
            if not h['convex'] or any(g['lo'][a]<h['lo'][a]+.00001 or g['hi'][a]>h['hi'][a]-.00001 for a in range(3)):continue
            if all(n.dot(v.co-p)<-.00001 for n,p in h['planes'] for v in g['vs']):
                remove.update(g['fs']);hidden+=1;break
    hidden_tris=sum(len(f.verts)-2 for f in remove)
    if remove:bmesh.ops.delete(bm,geom=list(remove),context='FACES')
    # 花弁材質だけを対象に、頂点を移動せず小さい面角の分割を統合します。
    edges=[e for e in bm.edges if len(e.link_faces)==2 and all(f.material_index in (3,4) for f in e.link_faces)]
    bmesh.ops.dissolve_limit(bm,angle_limit=math.radians(8),use_dissolve_boundaries=False,verts=[],edges=edges,delimit={'MATERIAL','UV'})
    bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>3])
    out={'path':data['path'],'name':data['name'],'vertices':[],'normals':[],'uv':[],'parts':[[] for p in data['parts']]};unique={}
    for f in bm.faces:
        for loop in f.loops:
            pos=tuple(loop.vert.co);n=loop[normal].normalized();tex=tuple(loop[uv].uv)
            if n.length_squared<.1:n=f.normal
            key=(pos,tuple(n),tex)
            if key not in unique:
                unique[key]=len(out['vertices']);out['vertices'].append(pos);out['normals'].append(tuple(n));out['uv'].append(tex)
            out['parts'][f.material_index].append(unique[key])
    after=sum(len(p)//3 for p in out['parts']);bm.free()
    (ROOT/'Optimized'/path.name).write_text(json.dumps(out,separators=(',',':')),encoding='utf-8')
    return dict(file=path.name,before=before,after=after,hiddenIslands=hidden,hiddenTriangles=hidden_tris)

def run(prefix):
    stats=[optimize(p) for p in sorted((ROOT/'Original').glob(prefix+'*.json'))]
    (ROOT/('report_'+prefix+'.json')).write_text(json.dumps(stats,indent=2),encoding='utf-8')
    print(dict(chunks=len(stats),before=sum(s['before'] for s in stats),after=sum(s['after'] for s in stats),hiddenIslands=sum(s['hiddenIslands'] for s in stats),hiddenTriangles=sum(s['hiddenTriangles'] for s in stats)))




