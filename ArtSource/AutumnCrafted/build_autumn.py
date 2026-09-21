import bpy, bmesh, math, random, os
from mathutils import Vector, Quaternion
ROOT='C:/unity/DogLife/Assets/Art/AutumnCrafted'
collection=bpy.data.collections.get('DogLife_AutumnCrafted')
if not collection:
    collection=bpy.data.collections.new('DogLife_AutumnCrafted');bpy.context.scene.collection.children.link(collection)
else:
    for obj in list(collection.objects):bpy.data.objects.remove(obj,do_unlink=True)
def material(name,texture,color=(1,1,1,1)):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=color;bs.inputs['Roughness'].default_value=.8
    if texture:
        node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=bpy.data.images.load(ROOT+'/Textures/'+texture,check_existing=True);m.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
        if 'Leaf' in name:m.node_tree.links.new(node.outputs['Alpha'],bs.inputs['Alpha'])
    return m
bark=material('Crafted_Bark','Maple_Bark.png');leaf=material('Crafted_Leaf','Maple_Leaf.png');stone=material('Crafted_Stone','Autumn_Stone.png');amber=material('Crafted_Amber',None,(1,.39,.045,1))
class Mesh:
    def __init__(self):self.v=[];self.f=[];self.uv=[]
    def face(self,points,uv):
        n=len(self.v);self.v.extend([tuple(p) for p in points]);self.f.append(tuple(range(n,n+len(points))));self.uv.append(uv)
    def tube(self,points,radii,sides=12):
        points=[Vector(p) for p in points];dist=0;rings=[]
        # Reuse each bend's ring on both segments, preventing open seams.
        for j,p in enumerate(points):
            direction=(points[min(j+1,len(points)-1)]-points[max(0,j-1)]).normalized()
            q=Vector((0,0,1)).rotation_difference(direction)
            rings.append([p+q@Vector((math.cos(k*math.tau/sides)*radii[j],math.sin(k*math.tau/sides)*radii[j],0)) for k in range(sides)])
        for j in range(len(points)-1):
            a,b=points[j:j+2];direction=(b-a).normalized();q=Vector((0,0,1)).rotation_difference(direction);length=(b-a).length
            for k in range(sides):
                t=k*math.tau/sides;u=(k+1)*math.tau/sides
                va=q@Vector((math.cos(t),math.sin(t),0));vb=q@Vector((math.cos(u),math.sin(u),0))
                n=(k+1)%sides
                self.face([rings[j][k],rings[j][n],rings[j+1][n],rings[j+1][k]],[(k/sides,dist/2),((k+1)/sides,dist/2),((k+1)/sides,(dist+length)/2),(k/sides,(dist+length)/2)])
            dist+=length
        for ring in [list(reversed(rings[0])),rings[-1]]:
            self.face(ring,[(.5+.5*math.cos(k*math.tau/sides),.5+.5*math.sin(k*math.tau/sides)) for k in range(sides)])
    def blade(self,p,q,size):
        # Bent six-triangle leaf: texture has the real serrated silhouette and veins.
        p=Vector(p);grid=[]
        for y in range(3):
            for x in range(3):grid.append(p+q@Vector(((x/2-.5)*size,(y/2)*size,(abs(x-1)*.055+math.sin(y*1.5)*.08)*size)))
        for y in range(2):
            for x in range(2):
                ids=[y*3+x,y*3+x+1,(y+1)*3+x+1,(y+1)*3+x]
                self.face([grid[k] for k in ids],[(x/2,y/2),((x+1)/2,y/2),((x+1)/2,(y+1)/2),(x/2,(y+1)/2)])
    def object(self,name,mat):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(self.v,[],self.f);mesh.update();uv=mesh.uv_layers.new(name='UVMap')
        for poly,coords in zip(mesh.polygons,self.uv):
            for loop,co in zip(poly.loop_indices,coords):uv.data[loop].uv=co
            poly.use_smooth='Wood' in name
        if 'Wood' in name:
            bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0001);bm.to_mesh(mesh);bm.free();mesh.update()
        ob=bpy.data.objects.new(name,mesh);collection.objects.link(ob);ob.data.materials.append(mat);return ob
def export(name,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=ROOT+'/Models/'+name+'.fbx',use_selection=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
    print(name, 'triangles',sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects))
    for o in objects:o.hide_set(True)
def tree(seed,name,density=1):
    rng=random.Random(seed);wood=Mesh();foliage=Mesh();tips=[]
    trunk=[Vector((math.sin(i*.6)*.24,math.cos(i*.7)*.19,i*.9)) for i in range(8)]
    wood.tube(trunk,[.64,.48,.39,.32,.26,.20,.13,.035],16)
    for i in range(8):
        a=i*math.tau/8;wood.tube([(math.cos(a)*2,math.sin(a)*2,.04),(math.cos(a)*.8,math.sin(a)*.8,.22),(0,0,.8)],[.05,.22,.4],10)
    for i in range(12):
        a=i*2.399+seed*.2;z=2.3+(i%4)*.9;start=Vector((0,0,z));r=2.3+rng.random()*1.5;end=Vector((math.cos(a)*r,math.sin(a)*r,z+1.8+rng.random()*.8));mid=start.lerp(end,.55)+Vector((0,0,-.28))
        wood.tube([start,mid,end],[.24,.15,.04],12)
        for j in range(4):
            b=a+(j-1.5)*.55;tip=end+Vector((math.cos(b)*(.8+rng.random()),math.sin(b)*(.8+rng.random()),(j%2)*.5))
            wood.tube([mid.lerp(end,.5+j*.1),tip],[.06,.012],7);tips.append(tip)
            for k in range(int(18*density)):
                offset=Vector((rng.uniform(-.9,.9),rng.uniform(-.9,.9),rng.uniform(-.45,.5)));p=tip+offset
                q=Quaternion(Vector((0,0,1)),rng.random()*math.tau)@Quaternion(Vector((1,0,0)),rng.uniform(-.65,.65))
                foliage.blade(p,q,rng.uniform(.35,.64))
    obs=[wood.object(name+'_Wood',bark),foliage.object(name+'_Leaves',leaf)];export(name,obs)
    return obs
trees=[]
for i in range(3):trees.extend(tree(130+i*51,'Maple_Crafted_'+str(i+1),2))
tree(130,'Maple_Crafted_LOD',.36)
def primitive(name,location,scale,mat,kind='cube',rotation=None):
    if kind=='sphere':bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,location=location)
    else:bpy.ops.mesh.primitive_cube_add(size=1,location=location)
    ob=bpy.context.object;ob.name=name;ob.scale=scale
    if rotation:ob.rotation_euler=rotation
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for c in list(ob.users_collection):c.objects.unlink(ob)
    collection.objects.link(ob);ob.data.materials.append(mat)
    bevel=ob.modifiers.new('Soft worn edges','BEVEL');bevel.width=.07;bevel.segments=2;bpy.ops.object.modifier_apply(modifier=bevel.name)
    # Explicit box projection gives new textures consistent world scale.
    uv=ob.data.uv_layers.active or ob.data.uv_layers.new()
    for poly in ob.data.polygons:
        axis=max(range(3),key=lambda a:abs(poly.normal[a]));axes=[a for a in range(3) if a!=axis]
        for loop in poly.loop_indices:
            p=ob.data.vertices[ob.data.loops[loop].vertex_index].co;uv.data[loop].uv=(p[axes[0]]*.65,p[axes[1]]*.65)
    return ob
def lantern():
    obs=[]
    for z,size in [(0.15,(1.5,1.5,.3)),(.42,(1.15,1.15,.24)),(1.15,(.48,.48,1.4)),(1.92,(1.25,1.25,.2)),(2.9,(1.8,1.8,.25)),(3.14,(1.25,1.25,.25)),(3.35,(.6,.6,.2))]:obs.append(primitive('Lantern_Stone',(0,0,z),size,stone))
    for x in [-.48,.48]:
        for y in [-.48,.48]:obs.append(primitive('Lantern_Post',(x,y,2.4),(.18,.18,.8),stone))
    obs.append(primitive('Lantern_Amber',(0,0,2.35),(.25,.25,.4),amber,'sphere'));export('Autumn_StoneLantern',obs)
lantern()
wood=Mesh();wood.tube([(-2.6,0,.5),(-1.4,.12,.55),(0,0,.65),(1.5,-.18,.58),(2.5,-.25,.45)],[.42,.5,.58,.49,.3],16)
for i in range(4):wood.tube([(i-1.5,0,.6),(i-1.5,.8,1),(i-1.8,1.3,1.1)],[.15,.08,.015],8)
export('Autumn_FallenTimber',[wood.object('FallenTimber_Wood',bark)])
wood=Mesh()
wood.tube([(0,0,.1),(.1,0,.7),(0,.05,1.4),(.1,0,1.65)],[.8,.65,.57,.5],16)
for i in range(7):
    a=i*math.tau/7;wood.tube([(math.cos(a)*1.6,math.sin(a)*1.6,.04),(math.cos(a)*.6,math.sin(a)*.6,.35),(0,0,.7)],[.04,.22,.4],9)
export('Autumn_RootStump',[wood.object('RootStump_Wood',bark)])
obs=[]
for x in [-1.6,1.6]:
    obs.append(primitive('TrailSign_Post',(x,0,1.8),(.26,.3,3.6),bark))
for z in [2.35,2.8]:obs.append(primitive('TrailSign_Board',(0,0,z),(4,.3,.38),bark))
export('Autumn_TrailMarker',obs)
obs=[]
for z,size in [(.18,(4,3,.36)),(.46,(3.5,2.6,.2)),(1,(2.7,2,1)),(1.6,(3.4,2.5,.25))]:obs.append(primitive('Shrine_Stone',(0,0,z),size,stone))
for i in range(5):
    a=i*math.tau/5;obs.append(primitive('Shrine_Crystal',(math.cos(a),math.sin(a)*.7,2.05),(.24,.24,.75),amber,rotation=(.12,.2,a)))
export('Autumn_OfferingShrine',obs)
obs=[]
for i in range(6):
    ob=primitive('Weathered_Cairn',(math.sin(i)*.3,math.cos(i)*.16,.15+i*.295),(1.4-i*.14,1.1-i*.1,.3),stone,rotation=(0,0,i*.7));obs.append(ob)
export('Autumn_Waystone',obs)
# Put one finished tree in view, keep the user's original objects untouched.
for ob in trees[:2]:ob.hide_set(False)
bpy.ops.object.select_all(action='DESELECT')
for ob in trees[:2]:ob.select_set(True)
bpy.context.view_layer.objects.active=trees[0]
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=15;area.spaces.active.region_3d.view_location=(0,0,3.5)
            area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath='C:/unity/DogLife/ArtSource/AutumnCrafted/AutumnCrafted.blend',copy=True)
print('DONE: 4 maples and 6 new authored props')

