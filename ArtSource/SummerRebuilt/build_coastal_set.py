"""기존 Summer 방에 사용할 해안 절벽과 구조물. 전용 컬렉션만 생성한다."""
import bpy, bmesh, math, random, os
from mathutils import Vector
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
OUT=ROOT+'/Assets/Art/SummerRebuilt/Models'
os.makedirs(OUT,exist_ok=True)
collection=bpy.data.collections.new('SummerRebuilt_Original');bpy.context.scene.collection.children.link(collection)
material=bpy.data.materials.new('CoastalOriginalSurface');material.use_nodes=True
image=bpy.data.images.load(ROOT+'/Assets/Textures/Environment/Summer/SummerRebuilt/CoastalOriginalAtlas.png',check_existing=True)
tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
material.node_tree.links.new(tex.outputs['Color'],material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.85
rng=random.Random(3922);LOW=False
# 이전에 제작한 메시 도구만 재사용하며 모델 형태는 아래에서 새로 구성한다.
source=open(ROOT+'/ArtSource/SummerPolish/build_summer_details.py',encoding='utf-8').read()
helpers=source[source.index('class Mesh:'):source.index('authored=[]')]
helpers=helpers.replace("'stone':(0,0),'bark':(0,.5),'leaf':(.5,0),'sand':(.5,.5)","'stone':(0,.5),'bark':(.5,.5),'leaf':(0,0),'sand':(0,.5)")
exec(helpers)

def crag(m,cx,cy,width,depth,height,phase):
    # 층리와 침식 홈이 이어지는 닫힌 암벽. 높이별 단면으로 만든다.
    sides=12 if LOW else 24; levels=8 if LOW else 16; rings=[]
    for k in range(levels+1):
        t=k/levels; row=[]
        ledge=1+.075*math.sin(t*math.pi*8+phase)-.16*t*t
        for j in range(sides):
            a=j*math.tau/sides
            # 각진 절벽 단면에 작은 풍화 흔적을 준다.
            xx=math.copysign(abs(math.cos(a))**.45,math.cos(a))
            yy=math.copysign(abs(math.sin(a))**.6,math.sin(a))
            wobble=1+.04*math.sin(j*1.8+phase+k*.7)
            row.append((cx+xx*width*.5*ledge*wobble+.1*math.sin(t*7+phase),cy+yy*depth*.5*ledge*wobble,height*t-.25))
        rings.append(row)
    for k in range(levels):
        for j in range(sides):
            n=(j+1)%sides
            m.face([rings[k][j],rings[k][n],rings[k+1][n],rings[k+1][j]],'stone',[(j/sides,k/levels),((j+1)/sides,k/levels),((j+1)/sides,(k+1)/levels),(j/sides,(k+1)/levels)])
    # 수평 절단면도 별도 평면 UV로 펼쳐 윗면의 무늬가 줄처럼 늘어나지 않게 한다.
    for cap,reverse in [(rings[0],True),(rings[-1],False)]:
        points=list(reversed(cap)) if reverse else cap
        m.face(points,'stone',[((p[0]-cx)/width*.8+.5,(p[1]-cy)/depth*.8+.5) for p in points])

def wall(rooted=False):
    m=Mesh()
    for j in range(5):crag(m,-3.25+j*1.6,0,2.05,3.4,9.8+math.sin(j*1.7)*1.15,j*.83)
    for j in range(5):
        x=-3.2+j*1.6
        m.rock((x,.85,9.2),(.8,.9,.35),5,10)
        if j%2==0:fern(m,(x,.15,9.5),.9)
    if rooted:
        for j in range(3):
            x=-2.6+j*2.6
            pts=[(x+.7*math.sin(t*5+j),1.75,9.5*(1-t)) for t in [k/18 for k in range(19)]]
            m.tube(pts,[.22*(.5+k/24) for k in range(19)],10,'bark')
            for k in range(3,17,2):m.leaf(pts[k],Vector(pts[k])+Vector(((-1)**k*.7,.1,.25)),.3,segments=6)
    return m

def spire():
    m=Mesh();crag(m,0,0,3.1,2.6,5.4,.4);crag(m,1.5,-.3,1.5,1.5,2.9,2)
    fern(m,(-1.25,.4,.1),1.1)
    return m

def terrace():
    m=Mesh()
    # 계단처럼 올라갈 수 있는 평평한 암반 테라스.
    for p,s in [((0,0,.35),(4.8,3.8,.7)),((0,-.3,.95),(3.8,2.8,.5)),((0,-.65,1.4),(2.9,1.8,.4))]:m.box(p,s)
    for j in range(7):m.rock((-2.1+j*.7,-1.4,.55),(.45,.4,.55),5,9)
    fern(m,(1.3,-.55,1.65),.7)
    return m

def root_arch():
    m=Mesh()
    for offset in [0,.4,-.4]:
        pts=[(math.cos(t*math.pi)*2.8,offset+.15*math.sin(t*8),.2+math.sin(t*math.pi)*4.4) for t in [j/24 for j in range(25)]]
        m.tube(pts,[.25+.12*abs(1-2*j/24) for j in range(25)],10,'bark')
    for sign in [-1,1]:
        for j in range(4):m.tube([(sign*2.8,0,.45),(sign*(2.9+j*.2),.4+j*.3,.06)],[.24,.035],8,'bark')
        fern(m,(sign*2.5,.2,.1),1.0)
    for j in range(9):
        a=j*math.pi/8;p=Vector((math.cos(a)*2.8,0,.2+math.sin(a)*4.4));m.leaf(p,p+Vector((.7*math.cos(j*2.4),.25,.5)),.3,segments=6)
    return m

def lagoon():
    m=Mesh()
    for j in range(12):
        a=j*math.tau/12;m.rock((math.cos(a)*2.2,math.sin(a)*1.5,.4),(.7,.55,.55),7,12)
    fern(m,(1.5,-1.1,.4),.8);fern(m,(-1.6,-.6,.35),.7)
    return m

def waterfall_rock():
    m=Mesh()
    for sign in [-1,1]:
        crag(m,sign*2.6,-.25,2.1,3.4,8.7+(sign+1)*.4,sign+2)
        m.rock((sign*2.2,.8,.7),(1.2,1.1,1),6,12)
        fern(m,(sign*2.6,.5,8.8),1.05)
    crag(m,0,-1.1,3.4,1,7.9,.7)
    m.box((0,.1,7.8),(4.1,2.8,.4))
    for j in range(9):
        a=j*math.pi/8;m.rock((math.cos(a)*3.0,1.2+math.sin(a)*1.7,.23),(.65,.55,.42),6,10)
    return m

def flow_sheet():
    m=Mesh();nx=12;nz=30;grid=[]
    for j in range(nz+1):
        t=j/nz;y=.8+.55*t*t;z=7.85*(1-t)+.08;row=[]
        for i in range(nx+1):
            u=i/nx;row.append(((u-.5)*(3.2+.35*t),y+.06*math.sin(u*24+t*8),z))
        grid.append(row)
    for j in range(nz):
        for i in range(nx):m.face([grid[j][i],grid[j+1][i],grid[j+1][i+1],grid[j][i+1]],'leaf',[(i/nx,1-j/nz),(i/nx,1-(j+1)/nz),((i+1)/nx,1-(j+1)/nz),((i+1)/nx,1-j/nz)])
    o=m.object('WaterFlow')
    # 폭포 전용 셰이더가 UV를 물 텍스처 영역으로 옮기므로 0~1로 복원한다.
    for uv in o.data.uv_layers.active.data:uv.uv=((uv.uv.x-.008)/.484,(uv.uv.y-.008)/.484)
    return o

assets=[('SeaCliffWall',lambda:wall(False)),('RootCliffWall',lambda:wall(True)),('BasaltSpire',spire),('TidalTerrace',terrace),('DriftRootArch',root_arch),('LagoonBoulders',lagoon),('WaterfallCliff',waterfall_rock),('JunglePalm',palm),('FernGarden',garden),('HangingVines',wall_vines)]
summary=[]
for name,fn in assets:
    rng=random.Random(510+len(name));seed=rng.getstate();LOW=False;high=fn().object(name+'_LOD0')
    LOW=True;rng.setstate(seed);low=fn().object(name+'_LOD1')
    parent=bpy.data.objects.new(name,None);collection.objects.link(parent);high.parent=parent;low.parent=parent
    objects=[parent,high,low]
    if name=='WaterfallCliff':flow=flow_sheet();flow.parent=parent;objects.append(flow)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=parent
    bpy.ops.export_scene.fbx(filepath=OUT+'/'+name+'.fbx',use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,bake_anim=False,use_mesh_modifiers=True,path_mode='STRIP',add_leaf_bones=False)
    summary.append((name,len(high.data.polygons),len(low.data.polygons)))
    parent.hide_set(True)
os.makedirs(ROOT+'/ArtSource/SummerRebuilt',exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/SummerRebuilt/SummerRebuilt.blend',copy=True)
print(summary)
