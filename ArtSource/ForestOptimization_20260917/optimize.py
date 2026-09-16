"""Staged FBX simplification. Originals are never modified; run with Blender 4.3."""
import bpy, bmesh, json, math
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parent
SKIP={'04','07','12','17','21'}
def geometry(mesh):
    mesh.calc_loop_triangles()
    vs=[v.co.copy() for v in mesh.vertices]
    ts=[tuple(t.vertices) for t in mesh.loop_triangles]
    # FBX source contains loose vertices; they have no rendered surface.
    used=set(i for t in ts for i in t)
    samples=[vs[i] for i in used]+[(vs[a]+vs[b]+vs[c])/3 for a,b,c in ts]
    return vs,ts,samples,BVHTree.FromPolygons(vs,ts,all_triangles=True)
def error(samples,bvh,diag):
    d=np.array([bvh.find_nearest(v)[3] for v in samples])/diag
    return float(np.percentile(d,95)),float(d.max()),float(np.sqrt(np.mean(d*d)))
results=[]
for src in sorted((ROOT/'Originals/Models').glob('*.fbx')):
    if src.stem.split('_')[1] in SKIP: continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(src))
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True)
    original=obj.data.copy()
    ov,ot,os,ob=geometry(original)
    diag=(Vector(tuple(max(v[i] for v in ov) for i in range(3)))-Vector(tuple(min(v[i] for v in ov) for i in range(3)))).length
    base=obj.data
    bm=bmesh.new(); bm.from_mesh(base)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=diag*1e-7)
    bm.to_mesh(base); bm.free(); base.update()
    # Retain small disconnected leaves/details exactly: global decimation otherwise
    # deletes entire islands even at 95%, causing large silhouette outliers.
    bm=bmesh.new();bm.from_mesh(base)
    unseen=set(bm.verts);protected=set()
    while unseen:
        seed=unseen.pop();component={seed};stack=[seed]
        while stack:
            v=stack.pop()
            for edge in v.link_edges:
                w=edge.other_vert(v)
                if w in unseen: unseen.remove(w);component.add(w);stack.append(w)
        faces={f for v in component for f in v.link_faces}
        if len(faces)<=4: protected.update(v.index for v in component)
    bm.free()
    detail=base.copy()
    bm=bmesh.new();bm.from_mesh(detail)
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index not in protected],context='VERTS')
    bm.to_mesh(detail);bm.free()
    bm=bmesh.new();bm.from_mesh(base)
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index in protected],context='VERTS')
    bm.to_mesh(base);bm.free()
    floor='ForestFloor' in src.stem
    thin=src.stem.split('_')[1] in {'01','02','03','06','11','13','14'}
    # Floors use world-unit limits (2 cm typical, 5 cm worst), props relative limits.
    limits=(.02/diag,.05/diag) if floor else ((.0015,.006) if thin else (.0025,.008))
    attempts=[]; best=None
    for ratio in [.95,.9,.85,.8,.7,.6,.5,.4,.35,.3,.25,.2,.18,.15,.12,.1,.08,.05]:
        obj.data=base.copy()
        mod=obj.modifiers.new('ShapePreservingReduction','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bm=bmesh.new();bm.from_mesh(obj.data);bm.from_mesh(detail);bm.to_mesh(obj.data);bm.free();obj.data.update()
        cv,ct,cs,cb=geometry(obj.data)
        e1=error(os,cb,diag);e2=error(cs,ob,diag)
        p95=max(e1[0],e2[0]); worst=max(e1[1],e2[1])
        passed=p95<=limits[0] and worst<=limits[1]
        attempts.append(dict(ratio=ratio,triangles=len(ct),p95=p95,max=worst,passed=passed))
        print('CANDIDATE',src.stem,attempts[-1],flush=True)
        if passed: best=obj.data.copy()
        else: break
    obj.data=best if best else original.copy()
    obj.data.name=original.name
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'Candidates'/src.name),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',mesh_smooth_type='OFF',use_custom_props=False,path_mode='AUTO')
    _,finaltris,_,_=geometry(obj.data)
    results.append(dict(name=src.stem,before=len(ot),after=len(finaltris),limits=limits,attempts=attempts,materials=[m.name for m in obj.data.materials],uv_layers=len(obj.data.uv_layers)))
    (ROOT/'reduction_metrics.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
    print('FINAL',src.stem,len(ot),len(finaltris),flush=True)
