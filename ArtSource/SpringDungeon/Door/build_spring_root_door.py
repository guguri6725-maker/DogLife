import bpy,bmesh,math,json,os
from mathutils import Matrix,Vector
base=r"C:/Users/mbc/Desktop/NC_AI/DogLife";out=base+"/Assets/MapDesigne/Spring/Door";archive=base+"/ArtSource/SpringDungeon/Door"
s=bpy.data.scenes["Spring_Root_Door"];bpy.context.window.scene=s
source=next(o for o in s.objects if o.type=="MESH");source.name="SpringDoor_Source_Preserved"
parts=[]
# Cut along the inner sides of the columns; UVs on the original surfaces are retained.
for label,lo,hi in [("Frame_Left",-2,-.265),("Roots_Left",-.265,0),("Roots_Right",0,.265),("Frame_Right",.265,2)]:
    mesh=source.data.copy();bm=bmesh.new();bm.from_mesh(mesh)
    for plane,normal in [(lo,Vector((1,0,0))),(hi,Vector((-1,0,0)))]:
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),plane_co=(plane,0,0),plane_no=normal,dist=.000001,clear_inner=True,clear_outer=False)
    bm.to_mesh(mesh);bm.free()
    obj=bpy.data.objects.new(label,mesh);s.collection.objects.link(obj);obj.matrix_world=Matrix.Identity(4)
    if label.startswith("Roots"):
        obj.shape_key_add(name="Basis");key=obj.shape_key_add(name="Open");side=-1 if label.endswith("Left") else 1
        for v in key.data:
            x,y,z=v.co;f=max(0,min(1,(.265-abs(x))/.265))
            # Curl toward the anchored columns. The center seam separates; the columns never move.
            v.co.x=side*(.265-.028*math.sin(math.pi*f))
            v.co.z=z+.11*math.sin(math.pi*f)+.03*f
        for frame,value in [(1,0),(10 if side<0 else 15,0),(42,.25),(76,.88),(97,1)]:
            key.value=value;key.keyframe_insert(data_path="value",frame=frame)
        obj.data.shape_keys.animation_data.action.name=label+"_Open"
    mesh.transform(Matrix.Rotation(math.pi/2,4,"X"),shape_keys=True)
    parts.append(obj)
source.hide_render=True;source.hide_set(True)
s.frame_start=1;s.frame_end=97;s.render.fps=30
s.frame_set(1);s.render.filepath=archive+"/Door_Closed.png";bpy.ops.render.render(write_still=True)
s.frame_set(97);s.render.filepath=archive+"/Door_Open.png";bpy.ops.render.render(write_still=True)
s.frame_set(1)
bpy.ops.object.select_all(action="DESELECT")
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.export_scene.fbx(filepath=out+"/Models/SpringRootDoor.fbx",use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='RELATIVE')
bpy.ops.wm.save_as_mainfile(filepath=archive+"/SpringRootDoor.blend")
print(json.dumps([{"name":o.name,"triangles":sum(len(p.vertices)-2 for p in o.data.polygons),"shapeKeys":list(o.data.shape_keys.key_blocks.keys()) if o.data.shape_keys else []} for o in parts]))

