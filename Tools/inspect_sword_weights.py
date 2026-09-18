import bpy, pathlib, json, collections
bpy.ops.wm.open_mainfile(filepath=str(pathlib.Path('Library/BlossomSkeleton/source.blend').resolve()))
obj=bpy.data.objects['texture_mapping'];out=[]
for low in [.1,.2,.3,.4,.5]:
    vs=[v for v in obj.data.vertices if low<v.co.z<low+.1 and v.co.x<-.21]
    weights=collections.Counter()
    for v in vs:
        for g in v.groups:weights[obj.vertex_groups[g.group].name]+=g.weight
    out.append({'z':low,'count':len(vs),'min':[min((v.co[i] for v in vs),default=0) for i in range(3)],'max':[max((v.co[i] for v in vs),default=0) for i in range(3)],'weights':weights.most_common(5)})
pathlib.Path('Library/BlossomSkeleton/sword.json').write_text(json.dumps(out,indent=2))
