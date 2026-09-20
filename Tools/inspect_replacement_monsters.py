import bpy, pathlib, json
from mathutils import Vector
files = {
 'BlossomSkeleton':'Sakura Skeleton Warrior-158dd326bb68b857d1cbed20bd03219a.fbx',
 'FloralDeer':'Floral Skeleton Deer-eb89c790496ecd4448f3df7c325baae0.fbx',
 'SakuraRabbit':'Blossom Fantasy Hare-f44860c91a1959951e554e0b2d3fa240.fbx',
 'BlossomSpirit':'Blossom Antler Dryad-18e1a19e8c56daacfe880343ea488541.fbx'}
out=pathlib.Path('Temp/MonsterReplacement');out.mkdir(parents=True,exist_ok=True)
report={}
for name,file in files.items():
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(pathlib.Path('C:/Users/kking/Downloads')/file))
 report[name]=[{'name':o.name,'type':o.type,'dimensions':list(o.dimensions),'scale':list(o.scale),'vertices':len(o.data.vertices) if o.type=='MESH' else 0,'bones':[b.name for b in o.data.bones] if o.type=='ARMATURE' else [],'groups':[g.name for g in o.vertex_groups] if o.type=='MESH' else []} for o in bpy.context.scene.objects]
 bpy.ops.wm.save_as_mainfile(filepath=str((out/(name+'.blend')).resolve()))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 pts=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
 lo=Vector([min(p[i] for p in pts) for i in range(3)]);hi=Vector([max(p[i] for p in pts) for i in range(3)]);center=(lo+hi)/2;size=max(hi-lo)
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8
 scene.world=bpy.data.worlds.new('World');scene.world.color=(.22,.22,.22)
 scene.render.resolution_x=600;scene.render.resolution_y=700;scene.render.resolution_percentage=100
 for offset,power in [((2,-3,4),450),((-2,-1,2),250),((0,3,3),400)]:
  data=bpy.data.lights.new('Area','AREA');data.energy=power*size*size;data.size=size*3
  light=bpy.data.objects.new('Area',data);scene.collection.objects.link(light);light.location=center+Vector(offset)*size;light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
 data=bpy.data.cameras.new('Review');cam=bpy.data.objects.new('Review',data);scene.collection.objects.link(cam);scene.camera=cam;data.type='ORTHO';data.ortho_scale=size*1.2
 for view,offset in [('front',(0,-3,.2)),('side',(3,0,.2))]:
  cam.location=center+Vector(offset)*size;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
  scene.render.filepath=str((out/(name+'_'+view+'.png')).resolve());bpy.ops.render.render(write_still=True)
(out/'inspection.json').write_text(json.dumps(report,indent=2))
