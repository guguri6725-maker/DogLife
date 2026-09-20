import bpy,pathlib
from mathutils import Vector
out=pathlib.Path('Temp/MonsterReplacement').resolve()
for name,attack,frame in [('BlossomSkeleton','Attack',58),('FloralDeer','Windup',76),('SakuraRabbit','Jump',20),('BlossomSpirit','Attack',60)]:
 bpy.ops.wm.open_mainfile(filepath=str(out/name/(name+'_Rigged.blend')))
 rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=12
 scene.world=bpy.data.worlds.new('ReviewWorld');scene.world.color=(.2,.2,.2)
 scene.render.resolution_x=650;scene.render.resolution_y=750;scene.render.resolution_percentage=100
 for pos,power in [((2,-3,4),450),((-2,-1,2),250),((0,3,3),400)]:
  data=bpy.data.lights.new('Area','AREA');data.energy=power;data.size=3
  o=bpy.data.objects.new('Area',data);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,.5))-o.location).to_track_quat('-Z','Y').to_euler()
 data=bpy.data.cameras.new('Review');cam=bpy.data.objects.new('Review',data);scene.collection.objects.link(cam);scene.camera=cam;data.type='ORTHO';data.ortho_scale=1.55
 cam.location=(1.5,-3,1.1);cam.rotation_euler=(Vector((0,0,.5))-cam.location).to_track_quat('-Z','Y').to_euler()
 for state,f in [('Idle',1),(attack,frame)]:
  rig.animation_data.action=bpy.data.actions[state];scene.frame_set(f)
  scene.render.filepath=str(out/(name+'_'+state+'_rigged.png'));bpy.ops.render.render(write_still=True)
