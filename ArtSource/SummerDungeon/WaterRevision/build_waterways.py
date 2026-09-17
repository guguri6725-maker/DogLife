import bpy, math, os, json
from mathutils import Matrix
base=r"C:/Users/mbc/Desktop/NC_AI/DogLife"
out=base+"/Assets/MapDesigne/Summer/Models/WaterRevision"
os.makedirs(out,exist_ok=True)
s=bpy.data.scenes.new("Summer_Connected_Waterways")
bpy.context.window.scene=s
report=[]
# Model coordinates are authored in Unity's X/Y/Z, then converted for FBX export.
def export_mesh(name,verts,faces,colors=None):
    mesh=bpy.data.meshes.new(name+"_Mesh");mesh.from_pydata(verts,[],faces);mesh.update()
    mesh.transform(Matrix.Rotation(math.pi/2,4,'X'))
    obj=bpy.data.objects.new(name+"_Mesh",mesh);s.collection.objects.link(obj)
    for p in mesh.polygons:p.use_smooth=True
    if colors:
        c=mesh.color_attributes.new(name="Col",type="FLOAT_COLOR",domain="POINT")
        for i,v in enumerate(colors):c.data[i].color=(v,v,v,1)
    bpy.ops.object.select_all(action="DESELECT");obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.fbx(filepath=out+"/"+name+".fbx",use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_UNITS')
    report.append({"name":name,"triangles":sum(len(p.vertices)-2 for p in mesh.polygons)})
rooms=[("CoastalCave",38.4,22,0,3.8),("TropicalForest",38.4,22,44.4,2.3),("Waterfall",38.4,22,88.8,5.2),("CoastalCliff",46.08,26,133.2,5.8)]
for name,L,Z,off,width in rooms:
    def center(x):return 12+math.sin((x+off)*.15)*1.0
    def radius(x):return width*math.sqrt(max(0,min(1,x/4,(L-x)/4)))
    def height(x,z):
        if x<0 or x>L:return .08
        dist=abs(z-center(x));r=radius(x)
        a=max(0,min(1,(dist-r+1)/2));a=a*a*(3-2*a)
        return .08-1.1*(1-a)
    nx=math.ceil((L+4)/.55);nz=math.ceil(2*Z/.55)
    verts=[];faces=[]
    for i in range(nx+1):
        x=-2+(L+4)*i/nx
        for j in range(nz+1):
            z=-Z+2*Z*j/nz
            verts.append((x,height(x,z),z))
    for i in range(nx):
        for j in range(nz):
            a=i*(nz+1)+j;b=a+nz+1;faces.append((a,a+1,b+1,b))
    export_mesh(name+"_Riverbank",verts,faces)
    verts=[];faces=[];colors=[];steps=160;cross=10
    for i in range(steps+1):
        x=L*i/steps;r=radius(x)+.32
        for j in range(cross+1):
            v=-1+2*j/cross
            verts.append((x,-.25,center(x)+v*r));colors.append(abs(v)**4)
    for i in range(steps):
        for j in range(cross):
            a=i*(cross+1)+j;b=a+cross+1;faces.append((a,a+1,b+1,b))
    export_mesh(name+"_ContinuousWater",verts,faces,colors)
    report[-1].update({"worldX":off,"length":L})
bpy.ops.wm.save_as_mainfile(filepath=base+"/ArtSource/SummerDungeon/WaterRevision/Summer_Connected_Waterways.blend")
print(json.dumps(report))

