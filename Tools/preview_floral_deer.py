import bpy, math, json, pathlib
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=str(pathlib.Path('Library/FloralDeer/source.blend').resolve()))
for name in ['Cube','Camera','Light']:
    if name in bpy.data.objects:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
parent=list(range(len(mesh.data.vertices)))
def root(a):
    while parent[a]!=a:parent[a]=parent[parent[a]];a=parent[a]
    return a
def join(a,b):parent[root(a)]=root(b)
positions={}
for v in mesh.data.vertices:
    key=tuple(round(c,5) for c in v.co)
    if key in positions:join(v.index,positions[key])
    else:positions[key]=v.index
for e in mesh.data.edges:join(*e.vertices)
groups={}
for v in mesh.data.vertices:groups.setdefault(root(v.index),[]).append(v.index)
components=[]
for ids in sorted(groups.values(),key=len,reverse=True):
    coords=[mesh.data.vertices[i].co for i in ids]
    lo=[min(v[j] for v in coords) for j in range(3)];hi=[max(v[j] for v in coords) for j in range(3)]
    components.append({'id':len(components),'count':len(ids),'min':lo,'max':hi,'vertices':ids})
pathlib.Path('Library/FloralDeer/components.json').write_text(json.dumps(components))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8
scene.world.color=(.25,.25,.25);scene.render.resolution_x=800;scene.render.resolution_y=900;scene.render.resolution_percentage=100
for pos,power,size in [((2,-3,4),450,3),((-2,-1,2),250,2),((0,3,3),400,2)]:
    data=bpy.data.lights.new('Area','AREA');data.energy=power;data.shape='DISK';data.size=size
    light=bpy.data.objects.new('Area',data);scene.collection.objects.link(light);light.location=pos;light.rotation_euler=(Vector((0,0,0))-light.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('Review');cam=bpy.data.objects.new('Review',data);scene.collection.objects.link(cam);scene.camera=cam;data.type='ORTHO';data.ortho_scale=1.35
for name,pos in [('front',(0,-3,.15)),('side',(3,0,.15))]:
    cam.location=pos;cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(pathlib.Path('Library/FloralDeer/'+name+'.png').resolve());bpy.ops.render.render(write_still=True)
print('PREVIEW_DONE')
