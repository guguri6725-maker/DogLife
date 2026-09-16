import bpy
from pathlib import Path
root=Path(__file__).resolve().parent
for p in sorted((root/'Candidates').glob('*.fbx')):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(p))
 bpy.ops.export_scene.fbx(filepath=str(p),use_selection=False,object_types={'MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',mesh_smooth_type='OFF',path_mode='AUTO')
