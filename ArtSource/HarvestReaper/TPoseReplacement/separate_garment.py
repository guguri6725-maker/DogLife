"""Detach fused robe/leg surfaces at their hidden interfaces, preserving face UVs."""
import bpy,bmesh
ob=bpy.data.objects['NewReaper_Body'];rig=bpy.data.objects['NewReaper_Rig']
for p in rig.pose.bones:p.matrix_basis.identity()
bm=bmesh.new();bm.from_mesh(ob.data);weights=bm.verts.layers.deform.verify()
ids={g.name:g.index for g in ob.vertex_groups}
legs={s:{ids[s+n] for n in ['Thigh','Shin','Foot']} for s in ['L','R']}
region=bm.faces.layers.int.new('AnatomicalSurface')
for f in bm.faces:
    score={s:sum(sum(v[weights].get(i,0) for i in gs) for v in f.verts)/len(f.verts) for s,gs in legs.items()}
    f[region]=1 if score['L']>.5 else 2 if score['R']>.5 else 0
boundary=[e for e in bm.edges if len({f[region] for f in e.link_faces})>1]
bmesh.ops.split_edges(bm,edges=boundary)
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
for v in bm.verts:
    regions={f[region] for f in v.link_faces}
    if not regions:continue
    r=next(iter(regions));z=v.co.z/6.25
    if r:
        side='L' if r==1 else 'R'
        if z<.115:a,b,t=side+'Foot',side+'Shin',smooth(.055,.105,z)
        elif z<.335:a,b,t=side+'Shin',side+'Thigh',smooth(.255,.315,z)
        else:a,b,t=side+'Thigh','Hips',smooth(.43,.49,z)
        v[weights].clear();v[weights][ids[a]]=1-t;v[weights][ids[b]]=t
    elif any(i in v[weights] for i in legs['L']|legs['R']):
        x,y,z=v.co/6.25
        panel='Robe'+('Back' if y>.025 else 'Front')+('Left' if x<0 else 'Right')
        t=smooth(.43,.49,z);v[weights].clear();v[weights][ids[panel]]=1-t;v[weights][ids['Hips']]=t
bm.to_mesh(ob.data);bm.free();bpy.context.view_layer.update()
print('Detached fused garment interfaces:',len(boundary),'edges; vertices:',len(ob.data.vertices))
