import bpy,random,os
random.seed(17)
scene=bpy.data.scenes["Forest_VineGate_Design"];bpy.context.window.scene=scene
out=r"C:/Users/mbc/Desktop/NC_AI/DogLife/Assets/Art/ForestVineGates/Models"
source=r"C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/ForestVineGates"
gate=[o for o in scene.objects if o.type=="MESH" and o.name!="Root_Thicket"]
hedgeob=scene.objects["Root_Thicket"]
palette=bpy.data.materials.get("ForestVinePalette") or bpy.data.materials.new("ForestVinePalette")
palette.use_nodes=True;nt=palette.node_tree;bs=nt.nodes.get("Principled BSDF")
vc=nt.nodes.new("ShaderNodeVertexColor");vc.layer_name="VineColor";nt.links.new(vc.outputs["Color"],bs.inputs["Base Color"]);bs.inputs["Roughness"].default_value=.87
for ob in gate+[hedgeob]:
    mesh=ob.data
    attr=mesh.color_attributes.get("VineColor") or mesh.color_attributes.new(name="VineColor",type="FLOAT_COLOR",domain="CORNER")
    for poly in mesh.polygons:
        col=mesh.materials[poly.material_index].diffuse_color
        shade=random.uniform(.88,1.12)
        for li in poly.loop_indices:attr.data[li].color=(col[0]*shade,col[1]*shade,col[2]*shade,1)
        poly.material_index=0
    mesh.materials.clear();mesh.materials.append(palette)
def export(obs,name,animated):
    bpy.ops.object.select_all(action="DESELECT")
    for ob in obs:ob.hide_set(False);ob.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.export_scene.fbx(filepath=out+"/"+name+".fbx",use_selection=True,object_types={"MESH"},add_leaf_bones=False,axis_forward="-Z",axis_up="Y",apply_unit_scale=True,bake_anim=animated,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0.0)
scene.frame_set(1)
export(gate,"ForestVineGate",True)
export([hedgeob],"ForestVineThicket",False)
hedgeob.hide_set(True);hedgeob.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=source+"/ForestVineGate.blend")
print("Vine palette complete")

