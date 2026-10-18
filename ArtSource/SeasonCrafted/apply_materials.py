import bpy
atlas=bpy.data.images.load('C:/unity/DogLife/Assets/Textures/Environment/Summer/SeasonCrafted/SeasonAtlas.png',check_existing=True)
for name,offset in [('Season_Bark',(0,.5,0)),('Season_Stone',(0,0,0)),('Season_Leaf',(.5,0,0))]:
 m=bpy.data.materials.get(name)
 if not m:continue
 m.use_nodes=True;n=m.node_tree.nodes;links=m.node_tree.links
 bs=next(x for x in n if x.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.8
 tex=n.new('ShaderNodeTexImage');tex.image=atlas
 uv=n.new('ShaderNodeTexCoord');fract=n.new('ShaderNodeVectorMath');fract.operation='FRACTION'
 scale=n.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=.492
 add=n.new('ShaderNodeVectorMath');add.operation='ADD';add.inputs[1].default_value=(offset[0]+.004,offset[1]+.004,0)
 links.new(uv.outputs['UV'],fract.inputs[0]);links.new(fract.outputs[0],scale.inputs[0]);links.new(scale.outputs[0],add.inputs[0]);links.new(add.outputs[0],tex.inputs['Vector']);links.new(tex.outputs['Color'],bs.inputs['Base Color'])
