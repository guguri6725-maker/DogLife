"""여름용 장식 모델. 기존 Blender 오브젝트는 보존하며 전용 컬렉션만 사용한다."""
import bpy, bmesh, math, random, os
from mathutils import Vector

ROOT = 'C:/Users/mbc/Desktop/NC_AI/DogLife'
OUT = ROOT + '/Assets/Art/SummerPolish/Models'
os.makedirs(OUT, exist_ok=True)
collection = bpy.data.collections.new('SummerPolish_Authored')
bpy.context.scene.collection.children.link(collection)
atlas = bpy.data.images.load(ROOT + '/Assets/Art/SeasonCrafted/Textures/SeasonAtlas.png', check_existing=True)
material = bpy.data.materials.new('SummerPolish_Atlas')
material.use_nodes = True
bs = material.node_tree.nodes.get('Principled BSDF')
bs.inputs['Roughness'].default_value = .83
tex = material.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = atlas
material.node_tree.links.new(tex.outputs['Color'], bs.inputs['Base Color'])
rng = random.Random(922)
LOW = False

class Mesh:
    # 부위별로 기존 계절 아틀라스의 돌/나무/잎 영역을 사용한다.
    def __init__(self): self.v=[]; self.f=[]; self.uv=[]
    def face(self, points, tile='stone', uv=None, double=False):
        base=len(self.v); self.v.extend([tuple(p) for p in points]); self.f.append(tuple(range(base,base+len(points))))
        if uv is None: uv=[(p[0]*.23%1,p[2]*.23%1) for p in points]
        ox,oy={'stone':(0,0),'bark':(0,.5),'leaf':(.5,0),'sand':(.5,.5)}[tile]
        coords=[(ox+.008+u*.484,oy+.008+v*.484) for u,v in uv]
        self.uv.append(coords)
        if double: self.f.append(tuple(reversed(range(base,base+len(points))))); self.uv.append(list(reversed(coords)))
    def tube(self, pts, radii, sides=10, tile='bark'):
        if LOW: sides=max(4,sides//2)
        rings=[]
        for i,p in enumerate(pts):
            direction=Vector(pts[min(i+1,len(pts)-1)])-Vector(pts[max(0,i-1)])
            q=Vector((0,0,1)).rotation_difference(direction.normalized())
            rings.append([Vector(p)+q@Vector((math.cos(j*math.tau/sides)*radii[i],math.sin(j*math.tau/sides)*radii[i],0)) for j in range(sides)])
        for i in range(len(rings)-1):
            for j in range(sides):
                k=(j+1)%sides
                self.face([rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]],tile,[(j/sides,i/(len(rings)-1)),((j+1)/sides,i/(len(rings)-1)),((j+1)/sides,(i+1)/(len(rings)-1)),(j/sides,(i+1)/(len(rings)-1))])
        self.face(list(reversed(rings[0])),tile)
        self.face(rings[-1],tile)
    def leaf(self, start, end, width, tile='leaf', segments=6):
        if LOW: segments=max(2,segments//2)
        a,b=Vector(start),Vector(end); d=b-a
        side=d.cross(Vector((0,0,1))).normalized()
        if side.length<.1: side=Vector((1,0,0))
        rows=[]
        for i in range(segments+1):
            t=i/segments; center=a+d*t+Vector((0,0,math.sin(math.pi*t)*width*.35)); w=math.sin(math.pi*t)**.7*width
            rows.append([center-side*w,center+Vector((0,0,w*.16)),center+side*w])
        for i in range(segments):
            for j in range(2):self.face([rows[i][j],rows[i+1][j],rows[i+1][j+1],rows[i][j+1]],tile,[(j*.5,i/segments),(j*.5,(i+1)/segments),((j+1)*.5,(i+1)/segments),((j+1)*.5,i/segments)],True)
    def rock(self, p, scale, rings=7, sides=14, tile='stone'):
        if LOW: rings=max(3,rings//2); sides=max(6,sides//2)
        p=Vector(p); rows=[]
        for i in range(rings+1):
            t=math.pi*i/rings; row=[]
            for j in range(sides):
                a=j*math.tau/sides; irregular=1+rng.uniform(-.10,.10)
                row.append(p+Vector((math.sin(t)*math.cos(a)*scale[0]*irregular,math.sin(t)*math.sin(a)*scale[1]*irregular,math.cos(t)*scale[2]*irregular)))
            rows.append(row)
        for i in range(rings):
            for j in range(sides):self.face([rows[i][j],rows[i+1][j],rows[i+1][(j+1)%sides],rows[i][(j+1)%sides]],tile,[(j/sides,i/rings),(j/sides,(i+1)/rings),((j+1)/sides,(i+1)/rings),((j+1)/sides,i/rings)])
    def box(self,p,s):
        x,y,z=p; a,b,c=[v*.5 for v in s]
        pts=[(x+i*a,y+j*b,z+k*c) for k in [-1,1] for j in [-1,1] for i in [-1,1]]
        for ids in [(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]:self.face([pts[i] for i in ids],'stone',[(0,0),(1,0),(1,1),(0,1)])
    def object(self,name):
        mesh=bpy.data.meshes.new(name); mesh.from_pydata(self.v,[],self.f); mesh.update()
        layer=mesh.uv_layers.new(name='UVMap')
        for poly,coords in zip(mesh.polygons,self.uv):
            for li,uv in zip(poly.loop_indices,coords):layer.data[li].uv=uv
        # 면마다 중복된 꼭짓점을 연결하여 분리된 조각처럼 보이지 않게 한다.
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001);bm.to_mesh(mesh);bm.free()
        obj=bpy.data.objects.new(name,mesh); collection.objects.link(obj); mesh.materials.append(material)
        return obj

def fern(m,p,size=1,fronds=9):
    p=Vector(p)
    for j in range(fronds):
        a=j*math.tau/fronds+rng.random()*.25; d=Vector((math.cos(a),math.sin(a),0))
        pts=[p+d*(t*size)+Vector((0,0,math.sin(t*2.2)*size*.75)) for t in [i/12 for i in range(13)]]
        m.tube(pts,[.018*(1-i/14)*size for i in range(13)],5,'leaf')
        side=Vector((-d.y,d.x,0))
        for k in range(2,12):
            t=k/12; w=math.sin(t*math.pi)*size*.32
            for sign in [-1,1]:m.leaf(pts[k],pts[k]+side*w*sign+d*size*.14+Vector((0,0,-.07)),w*.23,segments=3)

def palm():
    m=Mesh(); h=6.4
    pts=[(.8*(i/34)**2,.18*math.sin(i/34*math.pi),h*i/34) for i in range(35)]
    m.tube(pts,[.28*(1-i/48)*(1.13 if i%2==0 else 1) for i in range(35)],14)
    for i in range(3,33,2):
        p=Vector(pts[i]);m.tube([p+Vector((0,0,-.035)),p+Vector((0,0,.035))],[.28*(1-i/48)*1.19]*2,14)
    top=Vector(pts[-1])
    for j in range(14):
        a=j*math.tau/14;d=Vector((math.cos(a),math.sin(a),0));side=Vector((-d.y,d.x,0));length=3.5+rng.random()*.5
        path=[top+d*(t*length)+Vector((0,0,1.15*math.sin(t*math.pi)-t*t*1.4)) for t in [i/22 for i in range(23)]]
        m.tube(path,[.045*(1-i/24) for i in range(23)],6,'leaf')
        for k in range(2,22):
            t=k/22;w=math.sin(math.pi*t)**.65*.86
            for sign in [-1,1]:m.leaf(path[k],path[k]+side*w*sign+d*.45+Vector((0,0,-.24-t*.26)),.105*(1-t*.55),segments=4)
    for j in range(6):
        a=j*math.tau/6;m.rock(top+Vector((math.cos(a)*.28,math.sin(a)*.28,-.22)),(.16,.16,.23),5,9,'bark')
    return m

def garden():
    m=Mesh()
    for p,s in [((-.85,-.3,.08),1.15),((.7,.45,.06),.95),((.5,-.65,.08),.7)]:fern(m,p,s)
    for j in range(14):
        a=j*2.399; p=Vector((math.cos(a)*.75,math.sin(a)*.55,.04));end=p+Vector((math.cos(a)*.65,math.sin(a)*.65,.55+rng.random()*.45));m.leaf(p,end,.25,segments=8)
    for j in range(9):
        a=j*2.399;m.rock((math.cos(a)*1.3,math.sin(a)*.8,.08),(.22,.17,.12),4,9)
    return m

def bluff():
    m=Mesh()
    for j in range(8):
        x=(j%3-1)*1.35;y=(j//3-1)*.6;h=.8+(j%3)*.45
        m.rock((x,y,h*.75),(1,.85,h),9,18)
    fern(m,(-1.25,-.6,.7),.9);fern(m,(1.3,.1,1.3),.75)
    for j in range(12):
        a=j*2.399;m.rock((math.cos(a)*2.3,math.sin(a)*1.1,.12),(.3,.2,.15),4,8)
    return m

def arch():
    m=Mesh()
    for sign in [-1,1]:
        for k in range(6):m.box((sign*2.55,0,.45+k*.68),(1.2+(k%2)*.16,1.25,.64))
        m.box((sign*2.55,0,.18),(1.65,1.65,.36));m.box((sign*2.55,0,4.1),(1.6,1.5,.3))
    # 잘린 쐐기돌로 된 아치와 이중 테두리.
    for j in range(15):
        a=j*math.pi/15+.016;b=(j+1)*math.pi/15-.016
        pts=[(math.cos(t)*r,y,4.1+math.sin(t)*r) for y in [-.65,.65] for r in [2.05,3.05] for t in [a,b]]
        for ids in [(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)]:m.face([pts[i] for i in reversed(ids)],'stone',[(0,0),(1,0),(1,1),(0,1)])
    for sign in [-1,1]:
        fern(m,(sign*2.4,-.6,.12),1.1)
        for j in range(3):m.rock((sign*(2.5+j*.33),-.5,.2),(.5,.4,.26),5,10)
    for j in range(7):
        x=-2.5+j*.7;z=4.1+math.sqrt(max(0,2.9**2-x*x));path=[(x+.1*math.sin(k),-.75,z-k*.26) for k in range(9+j%3)]
        m.tube(path,[.026]*len(path),5,'bark')
        for k,p in enumerate(path):m.leaf(p,Vector(p)+Vector(((-1)**k*.32,-.07,-.15)),.16,segments=4)
    return m

def relic():
    m=Mesh()
    for h,r in [(0,.95),(.22,.84),(.4,.65),(3.4,.72),(3.65,.9)]:m.tube([(0,0,h),(0,0,h+.18)],[r,r],16,'stone')
    m.tube([(0,0,.52),(0,0,3.4)],[.55,.48],16,'stone')
    for j in range(12):
        a=j*math.tau/12;m.tube([(math.cos(a)*.51,math.sin(a)*.51,.65),(math.cos(a)*.48,math.sin(a)*.48,3.2)],[.06,.045],7,'stone')
    m.rock((0,0,4.08),(.55,.55,.48),9,16)
    fern(m,(-.7,-.4,.05),.85)
    return m

def wall_vines():
    m=Mesh()
    # 벽 윗부분에서 늘어지는 덩굴과 넓은 잎으로 평평한 벽의 윤곽을 부드럽게 한다.
    for j in range(9):
        x=-2.3+j*.57;length=2.2+(j%4)*.65
        path=[(x+math.sin(k*.52+j)*.15,.16+k*.006,5.7-length*k/18) for k in range(19)]
        m.tube(path,[.038*(1-k/24) for k in range(19)],6,'bark')
        for k in range(1,18):
            p=Vector(path[k]);sign=(-1)**k
            m.leaf(p,p+Vector((sign*.43,.16,-.3)),.23,segments=5)
    for j in range(16):
        a=j*2.399;p=Vector((-2.1+(j%8)*.6,0,5.65));m.leaf(p,p+Vector((math.cos(a)*.65,math.sin(a)*.4,.45)),.26,segments=5)
    return m

authored=[]
for name,fn in [('CoastalPalm',palm),('TropicalUnderstory',garden),('LimestoneGarden',bluff),('TidalSanctuaryArch',arch),('TideRelicPillar',relic),('HangingCoastalVines',wall_vines)]:
    LOW=False
    seed=rng.getstate()
    high=fn().object(name+'_LOD0')
    parent=bpy.data.objects.new(name,None);collection.objects.link(parent);high.parent=parent
    # 잎을 삭제하지 않고 곡면의 분할 수를 낮춰 원거리 실루엣을 보존한다.
    LOW=True;rng.setstate(seed)
    low=fn().object(name+'_LOD1');low.parent=parent
    bpy.ops.object.select_all(action='DESELECT')
    for o in [parent,high,low]:o.select_set(True)
    bpy.context.view_layer.objects.active=high
    bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,bake_anim=False,use_mesh_modifiers=True,path_mode='STRIP',add_leaf_bones=False)
    authored.append((name,len(high.data.polygons)))
    parent.hide_set(True)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/SummerPolish/SummerPolish.blend',copy=True)
print(authored)
