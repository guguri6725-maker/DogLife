import bpy, json, pathlib
bpy.ops.import_scene.fbx(filepath=r'C:/Users/kking/Downloads/Blossom Armored Skeleton-89bb2f1403686a3a88c2f6d9c00bcfc4.fbx')
out=[]
for o in bpy.context.scene.objects:
    item={'name':o.name,'type':o.type,'location':list(o.location),'scale':list(o.scale),'dimensions':list(o.dimensions)}
    if o.type=='MESH':
        item.update(vertices=len(o.data.vertices),faces=len(o.data.polygons),materials=[m.name if m else '' for m in o.data.materials],groups=[g.name for g in o.vertex_groups])
    if o.type=='ARMATURE':item['bones']=[{'name':b.name,'head':list(b.head_local),'tail':list(b.tail_local),'parent':b.parent.name if b.parent else ''} for b in o.data.bones]
    out.append(item)
p=pathlib.Path('Library/BlossomSkeleton');p.mkdir(parents=True,exist_ok=True)
(p/'inspection.json').write_text(json.dumps({'objects':out,'images':[{'name':i.name,'path':i.filepath,'packed':bool(i.packed_file)} for i in bpy.data.images]},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str((p/'source.blend').resolve()))
print('INSPECTION_DONE')
