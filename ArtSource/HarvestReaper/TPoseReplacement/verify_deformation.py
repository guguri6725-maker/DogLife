"""Sample every state and measure skin-edge stretch and foot-floor contact."""
import bpy,numpy as np,json
ob=bpy.data.objects['NewReaper_Body'];cache=bpy.app.driver_namespace['new_reaper_poses'];rest=bpy.app.driver_namespace['new_reaper_rest']
co=np.array([list(v.co)+[1] for v in ob.data.vertices]);groups={g.index:g.name for g in ob.vertex_groups};weights={}
for v in ob.data.vertices:
    for g in v.groups:weights.setdefault(groups[g.group],[]).append((v.index,g.weight))
weights={n:(np.array([x[0] for x in rows]),np.array([x[1] for x in rows])) for n,rows in weights.items()}
edges=np.array([list(e.vertices) for e in ob.data.edges]);base=np.linalg.norm(co[edges[:,0],:3]-co[edges[:,1],:3],axis=1);valid=base>.012
footids=[v.index for v in ob.data.vertices if any(groups[g.group].endswith('Foot') and g.weight>.95 for g in v.groups)]
report=[]
for name,frames in cache.items():
    times=sorted(frames);worst=0;count=0;low=999;wi=0
    for t in times[::max(1,len(times)//7)]+[times[-1]]:
        pose=frames[t];out=np.zeros((len(co),3))
        for n,(ids,ws) in weights.items():
            mat=np.array(pose[n]@rest[n].inverted());out[ids]+=(co[ids]@mat.T)[:,:3]*ws[:,None]
        ratio=np.linalg.norm(out[edges[:,0]]-out[edges[:,1]],axis=1)/np.maximum(base,1e-9)
        maxr=float(np.max(ratio[valid]));count=max(count,int(np.sum((ratio>2)&valid)));low=min(low,float(out[footids,2].min()))
        if maxr>worst:worst=maxr;wi=int(np.argmax(np.where(valid,ratio,0)))
    report.append({'clip':name,'max_edge_ratio':worst,'edges_over_2x':count,'foot_min_z':low,'worst_edge':edges[wi].tolist()})
with open('C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/HarvestReaper/TPoseReplacement/deformation_check.json','w') as f:json.dump(report,f,indent=2)
print(json.dumps(sorted(report,key=lambda r:r['max_edge_ratio'],reverse=True)[:5]))
print('Lowest foot vertex',min(r['foot_min_z'] for r in report))
