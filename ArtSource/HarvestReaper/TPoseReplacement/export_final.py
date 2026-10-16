"""Export the approved live authoring meshes with stable Unity hierarchy names."""
import bpy,os
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
OUT=ROOT+'/ArtSource/HarvestReaper/TPoseReplacement/FinalModels'
os.makedirs(OUT,exist_ok=True)
r=bpy.data.objects['NewReaper_Rig'];b=bpy.data.objects['NewReaper_Body'];w=bpy.data.objects['NewReaper_Scythe']
if r.animation_data:r.animation_data.action=None
for p in r.pose.bones:p.matrix_basis.identity()
r.name='HarvestReaper_Rig';b.name='HarvestReaper_Body';w.name='HarvestReaper_Scythe'
try:
    bpy.ops.object.select_all(action='DESELECT')
    for o in [r,b,w]:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=r;bpy.context.view_layer.update()
    for filename in ['HarvestReaper.fbx','HarvestReaperDeath.fbx']:
        bpy.ops.export_scene.fbx(filepath=OUT+'/'+filename,use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
finally:r.name='NewReaper_Rig';b.name='NewReaper_Body';w.name='NewReaper_Scythe'
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/HarvestReaper/TPoseReplacement/HarvestReaper_TPoseRig.blend',copy=True)
print('Saved final model exports and authoritative Blender authoring file.')
