import bpy, math, random, os
from mathutils import Vector
random.seed(17)
root=r"C:/Users/mbc/Desktop/NC_AI/DogLife"
out=root+"/Assets/Art/ForestVineGates/Models"
source=root+"/ArtSource/ForestVineGates"
# 기존 Blender 장면을 지우지 않고 새 제작 장면을 만듭니다.
scene=bpy.data.scenes.new("Forest_VineGate_Design")
bpy.context.window.scene=scene
scene.render.fps=30
scene.frame_start=1
scene.frame_end=73
mats=[]
for name,col in [("Vine_Bark",(.19,.115,.048,1)),("Vine_Moss",(.18,.235,.065,1)),("Ivy_Deep",(.075,.15,.038,1)),("Ivy_Olive",(.23,.32,.07,1)),("Ivy_Tip",(.37,.43,.12,1))]:
    mat=bpy.data.materials.new(name);mat.diffuse_color=col;mat.use_nodes=True
    bs=mat.node_tree.nodes.get("Principled BSDF");bs.inputs["Base Color"].default_value=col;bs.inputs["Roughness"].default_value=.86
    mats.append(mat)
class Geo:
    def __init__(self):self.v=[];self.f=[];self.mi=[]
    def tube(self,pts,radius,mi=0,sides=7):
        pts=[Vector(p) for p in pts];base=len(self.v)
        for i,p in enumerate(pts):
            tangent=(pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]).normalized()
            n=tangent.cross(Vector((0,1,0)))
            if n.length<.01:n=tangent.cross(Vector((1,0,0)))
            n.normalize();b=tangent.cross(n).normalized()
            r=radius*(.75+.25*math.sin(math.pi*i/(len(pts)-1)))
            for j in range(sides):
                angle=math.tau*j/sides
                self.v.append(tuple(p+r*(math.cos(angle)*n+math.sin(angle)*b)))
        for i in range(len(pts)-1):
            for j in range(sides):
                a=base+i*sides+j;b0=base+i*sides+(j+1)%sides
                self.f.append((a,b0,b0+sides,a+sides));self.mi.append(mi if random.random()>.12 else 1)
        self.f.extend([tuple(base+j for j in reversed(range(sides))),tuple(base+(len(pts)-1)*sides+j for j in range(sides))]);self.mi.extend([mi,mi])
    def leaf(self,p,size,angle,mi):
        p=Vector(p);a=Vector((math.cos(angle),0,math.sin(angle)));u=Vector((-math.sin(angle),0,math.cos(angle)))
        # 입체적인 잎맥과 두께를 가진 양면 담쟁이 잎.
        profile=[(0,-.45),(-.52,-.1),(-.58,.27),(-.25,.28),(0,.9),(.25,.28),(.58,.27),(.52,-.1)]
        base=len(self.v)
        self.v.append(tuple(p+Vector((0,-.1*size,0))))
        for x,z in profile:self.v.append(tuple(p+size*(a*x+u*z)))
        self.v.append(tuple(p+Vector((0,.035*size,0))))
        for k in range(8):
            j=1+k;j2=1+(k+1)%8
            self.f.append((base,base+j,base+j2));self.mi.append(mi)
            self.f.append((base+9,base+j2,base+j));self.mi.append(mi)
    def obj(self,name):
        mesh=bpy.data.meshes.new(name+"_Mesh");mesh.from_pydata(self.v,[],self.f);mesh.materials.clear()
        for m in mats:mesh.materials.append(m)
        ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob)
        for f,mi in zip(mesh.polygons,self.mi):f.material_index=mi;f.use_smooth=True
        return ob
def curve(fn,n=40):return [fn(i/(n-1)) for i in range(n)]
gate=[]
frame=Geo()
for side in [-1,1]:
    for strand in range(3):
        frame.tube(curve(lambda t:(side*(3.45+.16*math.sin(t*14+strand)),.12+.16*math.cos(t*14+strand),-.2+8.05*t)),.15 if strand==0 else .09)
    for j in range(55):
        z=random.uniform(.2,7.9);x=side*(3.45+random.uniform(-.25,.25))
        frame.leaf((x,random.uniform(-.36,.18),z),random.uniform(.3,.6),random.uniform(-1,1),random.choice([2,3,4]))
for k in range(3):
    frame.tube(curve(lambda t:(-3.6+7.2*t,.2*math.sin(t*13+k),7.8+.28*math.sin(t*math.pi)+.1*k)),.12)
for j in range(70):
    frame.leaf((random.uniform(-3.6,3.6),random.uniform(-.3,.25),random.uniform(7.5,8.1)),random.uniform(.3,.55),random.uniform(-1.5,1.5),random.choice([2,3,4]))
gate.append(frame.obj("Living_Frame"))
for side in [-1,1]:
    for i in range(7):
        x=side*(.18+i*.43)
        g=Geo()
        pts=curve(lambda t:(x+.16*math.sin(t*16+i),.15*math.sin(t*12+i),.1+7.65*t),54)
        g.tube(pts,.095 if i%2==0 else .068)
        for j in range(12):
            t=(j+.6)/13;z=.1+t*7.6
            reach=(-1 if j%2==0 else 1)*random.uniform(.28,.6)
            g.tube(curve(lambda u:(x+reach*u,.12*math.sin(t*12+i)-.08*u,z+.32*u),10),.025)
        for j in range(72):
            t=random.random();z=.2+t*7.35
            xx=x+.16*math.sin(t*16+i)+random.uniform(-.25,.25)
            g.leaf((xx,random.uniform(-.3,.21),z),random.uniform(.29,.47),random.uniform(-2,2),random.choice([2,3,3,4]))
        ob=g.obj(("Left" if side<0 else "Right")+"_Vine_%02d"%i);gate.append(ob)
        # 門の中央から外側へ、時間差で持ち上がりながら滑るカーテン動作。
        target=side*(3.22+i*.08)-x
        delay=i*1.5
        for fr,fac in [(1,0),(9+delay,0),(31+delay,.22),(55+delay,.87),(73,1)]:
            ob.location=(target*fac,.24*math.sin(math.pi*fac)+i*.018*fac,(.3+.07*i)*fac)
            ob.scale=(1,1,1-.07*fac)
            ob.rotation_euler[1]=side*.05*math.sin(math.pi*fac)
            ob.keyframe_insert(data_path="location",frame=fr)
            ob.keyframe_insert(data_path="scale",frame=fr)
            ob.keyframe_insert(data_path="rotation_euler",frame=fr)
        ob.animation_data.action.name=ob.name+"_Open"
# 정적인 측면 덩굴 울타리는 별도 모델로 내보냅니다.
hedge=Geo()
for i in range(14):
    x=-5+10*i/13
    hedge.tube(curve(lambda t:(x+.3*math.sin(t*10+i),.3*math.sin(t*8+i),-.35+7.2*t)),.13)
    for j in range(63):
        t=random.random()
        hedge.leaf((x+random.uniform(-.58,.58),random.uniform(-.5,.4),t*7.3),random.uniform(.38,.72),random.uniform(-2,2),random.choice([2,3,3,4]))
for i in range(5):
    hedge.tube(curve(lambda t:(-5+10*t,.1*math.sin(t*8),.6+i*1.25+.4*math.sin(t*9+i))),.16)
hedgeob=hedge.obj("Root_Thicket")
scene.frame_set(1)
def export(objs,name,animated):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objs:ob.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.export_scene.fbx(filepath=out+"/"+name+".fbx",use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,bake_anim=animated,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0.0,path_mode='AUTO')
export(gate,"ForestVineGate",True)
export([hedgeob],"ForestVineThicket",False)
# 포트폴리오용 Blender 원본에는 문과 울타리를 분리해 보관합니다.
hedgeob.hide_set(True);hedgeob.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=source+"/ForestVineGate.blend")
print({"gateObjects":len(gate),"gateVertices":sum(len(o.data.vertices) for o in gate),"thicketVertices":len(hedgeob.data.vertices),"duration":2.4,"saved":source})

