"""계절별 상점/합성대를 Blender에서 제작하고 텍스처와 FBX를 내보냅니다."""
import bpy, math, random, json
import numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(r'C:/Users/mbc/Desktop/NC_AI/DogLife')
OUT=ROOT/'Assets/Models/Environment/SeasonStations2026'
TEX=ROOT/'Assets/Textures/Environment/SeasonStations2026'
SOURCE=ROOT/'ArtSource/SeasonStations2026'
SEASONS=['Spring','Summer','Autumn','Winter']
PALETTES={
 'Spring':[(.28,.17,.10),(.46,.48,.35),(.62,.32,.40),(.54,.38,.16),(.12,.15,.13),(.26,.39,.16),(.78,.70,.49),(.95,.62,.26)],
 'Summer':[(.39,.31,.20),(.43,.48,.45),(.18,.43,.48),(.44,.36,.19),(.10,.21,.23),(.66,.48,.35),(.77,.74,.58),(.36,.77,.75)],
 'Autumn':[(.26,.13,.075),(.33,.29,.23),(.53,.25,.12),(.58,.29,.12),(.16,.12,.10),(.56,.24,.075),(.74,.61,.37),(.98,.43,.12)],
 'Winter':[(.19,.23,.24),(.31,.37,.43),(.23,.34,.46),(.51,.56,.59),(.12,.17,.22),(.43,.66,.74),(.79,.82,.78),(.46,.76,.92)]}
KINDS=['Wood','Stone','Cloth','Metal','Iron','Accent','Ivory','Light']
scene=bpy.data.scenes.get('DogLife_SeasonStations_Art') or bpy.data.scenes.new('DogLife_SeasonStations_Art')
bpy.context.window.scene=scene
# 이 작업 전용 씬의 임포트 참고본만 비우고 다른 Blender 씬은 보존합니다.
for ob in list(scene.objects):bpy.data.objects.remove(ob,do_unlink=True)
scene.world=bpy.data.worlds.new('Stations_World')
scene.world.use_nodes=True
next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(.12,.16,.20,1)
MATS={};manifest={};objects=[];rng=random.Random(4721)

def texture(season,kind,color,seed):
    """단순 단색 대신 원본 나뭇결/짜임/석재 높이와 색상 맵을 직접 제작합니다."""
    n=1024;y,x=np.mgrid[0:n,0:n].astype(np.float32)/n
    rand=np.random.default_rng(seed);noise=np.zeros_like(x)
    for freq,amp in [(2,.22),(5,.11),(13,.07),(31,.035),(79,.018)]:
        for j in range(3):
            ax=int(rand.integers(1,freq+1));ay=int(rand.integers(1,freq+1))
            noise+=np.sin(2*np.pi*(ax*x+ay*y)+rand.random()*6.28)*amp/3
    fine=rand.random((n,n),dtype=np.float32)-.5
    if kind=='Wood':
        warp=x+.026*np.sin(y*math.tau*2)+.012*np.sin(y*math.tau*7)
        grain=(.5+.5*np.sin(warp*math.tau*37+noise*5))**12
        grain+=.28*(.5+.5*np.sin(warp*math.tau*131))**20
        knots=np.zeros_like(x)
        for kx,ky in [(.22,.28),(.76,.71)]:
            d=np.sqrt(((x-kx)*3)**2+((y-ky)*.8)**2)
            knots+=np.exp(-d*20)*np.sin(d*180)
        h=.5+noise*.7-grain*.17+knots*.13+fine*.025
        value=.93+noise*.9-grain*.24+knots*.14+fine*.035
    elif kind=='Cloth':
        weave=np.sin(x*math.tau*256)*np.sin(y*math.tau*256)
        threads=np.sin(x*math.tau*32)*.025+np.sin(y*math.tau*32)*.02
        h=.5+weave*.08+noise*.12+threads;value=.97+noise*.35+weave*.045+threads
    elif kind in ['Stone','Accent']:
        veins=np.exp(-np.abs(np.sin(x*15+y*8+noise*10))*35)
        pores=(fine>.46).astype(np.float32)
        h=.5+noise*.8-veins*.09-pores*.09;value=.97+noise*.8-veins*.16-pores*.14
        if kind=='Accent' and season=='Winter':value=1+noise*.55+veins*.18
    else:
        scratches=(.5+.5*np.sin(y*math.tau*160+noise*2))**24
        h=.5+noise*.18+fine*.03-scratches*.02;value=1+noise*.35+fine*.03-scratches*.04
    arr=np.ones((n,n,4),dtype=np.float32)
    for k in range(3):arr[:,:,k]=np.clip(color[k]*value,0,1)
    name=f'{season}_{kind}'
    def save(a,suffix,colorspace):
        im=bpy.data.images.new(name+suffix,width=n,height=n,alpha=True)
        im.colorspace_settings.name=colorspace;im.pixels.foreach_set(a.ravel());im.filepath_raw=str(TEX/(name+suffix+'.png'));im.file_format='PNG';im.save();return im
    albedo=save(arr,'_BaseColor','sRGB')
    dx=(np.roll(h,-1,1)-np.roll(h,1,1))*7;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*7
    normals=np.stack([-dx,-dy,np.ones_like(h)],axis=-1);normals/=np.linalg.norm(normals,axis=-1,keepdims=True)
    arr[:,:,:3]=normals*.5+.5;normal=save(arr,'_Normal','Non-Color')
    mat=bpy.data.materials.new('SS_'+name);mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    im=nodes.new('ShaderNodeTexImage');im.image=albedo;links.new(im.outputs['Color'],bs.inputs['Base Color'])
    nm=nodes.new('ShaderNodeTexImage');nm.image=normal;normalnode=nodes.new('ShaderNodeNormalMap');normalnode.inputs['Strength'].default_value=.45
    links.new(nm.outputs['Color'],normalnode.inputs['Color']);links.new(normalnode.outputs['Normal'],bs.inputs['Normal'])
    bs.inputs['Roughness'].default_value=.68 if kind in ['Stone','Wood','Cloth'] else .38
    bs.inputs['Metallic'].default_value=.65 if kind in ['Metal','Iron'] else 0
    if kind=='Light':bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=.5
    mat.diffuse_color=(*color,1)
    return mat

def uvmesh(ob):
    me=ob.data;uv=me.uv_layers.new(name='CraftUV') if not me.uv_layers else me.uv_layers[0]
    for poly in me.polygons:
        axis=max(range(3),key=lambda a:abs(poly.normal[a]));a,b=[i for i in range(3) if i!=axis]
        for li in poly.loop_indices:
            v=me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv=(v[a]*.7,v[b]*.7)
def finish(ob,kind,smooth=False):
    ob.name=station+'_'+kind;ob.data.materials.append(MATS[kind]);objects.append(ob)
    if smooth:
        for p in ob.data.polygons:p.use_smooth=True
    return ob
def custom(verts,faces,kind,smooth=False):
    me=bpy.data.meshes.new('ArtisanMesh');me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(station+'_'+kind,me);scene.collection.objects.link(ob);uvmesh(ob);return finish(ob,kind,smooth)
def box(loc,size,kind,bevel=.08,rot=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);ob=bpy.context.object;ob.scale=size;ob.rotation_euler.z=rot
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=ob.modifiers.new('Hand finished edges','BEVEL');mod.width=min(bevel,min(size)*.24);mod.segments=3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    uvmesh(ob);return finish(ob,kind)
def ellipsoid(loc,scale,kind):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,location=loc);o=bpy.context.object;o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,kind,True)
def tube(points,radius,kind,sides=10):
    verts=[];faces=[]
    for i,p in enumerate(points):
        p=Vector(p);t=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        t.normalize();u=t.cross(Vector((0,0,1)))
        if u.length<.01:u=t.cross(Vector((0,1,0)))
        u.normalize();v=t.cross(u).normalized();r=radius[i] if isinstance(radius,list) else radius
        for j in range(sides):verts.append(p+r*(u*math.cos(j*math.tau/sides)+v*math.sin(j*math.tau/sides)))
        if i:
            for j in range(sides):faces.append(((i-1)*sides+j,(i-1)*sides+(j+1)%sides,i*sides+(j+1)%sides,i*sides+j))
    faces.extend([tuple(reversed(range(sides))),tuple((len(points)-1)*sides+j for j in range(sides))]);return custom(verts,faces,kind,True)
def ring(center,radius,minor,kind,plane='xy'):
    pts=[]
    for i in range(49):
        a=i*math.tau/48;v=(math.cos(a)*radius,math.sin(a)*radius,0) if plane=='xy' else (math.cos(a)*radius,0,math.sin(a)*radius)
        pts.append(tuple(center[k]+v[k] for k in range(3)))
    return tube(pts,minor,kind,8)
def lathe(center,profile,kind,segments=36):
    verts=[];faces=[]
    for r,z in profile:
        for j in range(segments):
            a=j*math.tau/segments;verts.append((center[0]+r*math.cos(a),center[1]+r*math.sin(a),center[2]+z))
    for i in range(len(profile)-1):
        for j in range(segments):a=i*segments+j;b=i*segments+(j+1)%segments;faces.append((a,b,b+segments,a+segments))
    return custom(verts,faces,kind,True)
def leaf(loc,angle,length=.5,kind='Accent'):
    u=Vector((math.cos(angle),math.sin(angle),.35));v=Vector((-math.sin(angle),math.cos(angle),0));p=Vector(loc)
    verts=[p,p+u*length*.45+v*length*.24,p+u*length,p+u*length*.45-v*length*.24,p+u*length*.48+Vector((0,0,.07))]
    return custom(verts,[(0,1,4),(1,2,4),(2,3,4),(3,0,4)],kind)
def blossom(loc,size=.22):
    for k in range(5):
        a=k*math.tau/5;o=ellipsoid((loc[0]+math.cos(a)*size*.65,loc[1]+math.sin(a)*size*.65,loc[2]),(size*.68,size*.38,size*.12),'Cloth');o.rotation_euler.z=a
    ellipsoid(loc,(size*.18,)*3,'Metal')
def lantern(x,y,z,scale=1):
    lathe((x,y,z),[(0,0),(.28*scale,0),(.32*scale,.1*scale),(.22*scale,.13*scale),(.22*scale,.65*scale),(.32*scale,.68*scale),(0,.85*scale)],'Iron',16)
    ellipsoid((x,y,z+.39*scale),(.24*scale,.24*scale,.28*scale),'Light')
    for a in range(4):
        d=a*math.pi/2;tube([(x+math.cos(d)*.25*scale,y+math.sin(d)*.25*scale,z+.09*scale),(x+math.cos(d)*.25*scale,y+math.sin(d)*.25*scale,z+.68*scale)],.025*scale,'Metal',6)
    ring((x,y,z+.94*scale),.13*scale,.025*scale,'Metal','xz')
def rope(a,b,thick=.035):
    tube([Vector(a).lerp(Vector(b),i/16)+Vector((0,0,-.15*math.sin(i*math.pi/16))) for i in range(17)],thick,'Ivory',6)
def bowl(x,y,z,r=.65):
    lathe((x,y,z),[(0,0),(r*.6,0),(r*.95,.18),(r,.3),(r*.9,.33),(r*.6,.12),(0,.10)],'Stone')
    ring((x,y,z+.30),r*.95,.035,'Metal')
def curved_counter():
    # 独立した板材と曲面の前板で、長方形一塊の印象を避けます。
    for j in range(17):
        x=(j-8)*.49;y=-.65-.28*(1-(x/4.4)**2)
        box((x,y,1.04),(.46,.16,1.38),'Wood',.045,rot=x*.018)
        for z in [.53,1.5]:ellipsoid((x,y-.11,z),(.035,.02,.035),'Metal')
    for y in [-.65,.65]:tube([(x,y-.22*(1-(x/4.5)**2),z) for x in np.linspace(-4.5,4.5,36) for z in [1.78]],.11,'Wood')
    for i in range(5):box((0,-.65+i*.32,1.8),(9,.30,.14),'Wood',.045)
    for x in [-4.2,4.2]:box((x,0,1),(.28,1.8,1.7),'Wood',.07)
    for x in [-3.25,0,3.25]:bowl(x,0,1.9,.58)
def drape(season):
    verts=[];faces=[];nx=72;ny=32
    def pos(u,v):
        x=(u-.5)*10.9;y=-2.55+v*6.6
        if season=='Spring':z=4.65+.88*math.sin(math.pi*v)+.20*math.cos(x*.48)+.07*math.cos(x*5.4)
        elif season=='Summer':z=4.55+1.1*v+.5*(x/5.45)**2-.40*math.sin(math.pi*v)+.055*math.sin(x*8+v*3)
        elif season=='Autumn':z=4.65+1.1*math.sin(math.pi*v)**.72+.07*math.cos(x*6.5)
        else:z=4.5+1.4*(1-abs(v*2-1))+.04*math.cos(x*5)
        return (x,y,z)
    for j in range(ny+1):
        for i in range(nx+1):verts.append(pos(i/nx,j/ny))
    for j in range(ny):
        for i in range(nx):a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
    custom(verts,faces,'Cloth',True)
    for j in [0,ny]:
        pts=[pos(i/nx,j/ny) for i in range(nx+1)];tube(pts,.045,'Ivory',6)
        for k in range(12):
            a=k/12;b=(k+1)/12;vs=[]
            for q in range(9):
                t=q/8;u=a+(b-a)*t;p=Vector(pos(u,j/ny));vs.extend([p,p-Vector((0,0,.22+.25*math.sin(math.pi*t)))])
            custom(vs,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(8)],'Cloth',True)
    for i in range(0,nx+1,12):tube([pos(i/nx,j/ny) for j in range(ny+1)],.018,'Ivory',5)
def shop(season):
    curved_counter();drape(season)
    for x in [-4.8,4.8]:
        for y in [-2.1,3.6]:
            pts=[(x+.13*math.sin(i*.65),y+.10*math.sin(i*.8),.25+i*.59) for i in range(9)]
            tube(pts,[.23-i*.008 for i in range(9)],'Wood',12)
            for z in [.45,3.8,4.35]:ring((x,y,z),.24,.048,'Metal')
            if season=='Spring':
                branch=[(x,y,2.9),(x*.91,y,3.6),(x*.8,y,4.05),(x*.7,y,4.4)];tube(branch,[.11,.085,.05,.015],'Wood')
                for i,p in enumerate(branch[1:]):leaf(p,i*.7,.55);blossom((p[0],p[1],p[2]+.1),.2)
        tube([(x,-2.1,4.7),(x,0,5.15),(x,3.6,5.25)],.15,'Wood')
        rope((x,-2.1,4.5),(x*.65,-2.0,3.6));lantern(x*.84,-2.0,3.05,.85)
    # 階層のついた後方棚・器・編籠で商店として読める密度を作ります。
    for z in [.7,1.6,2.5]:
        box((0,3.2,z),(8.5,.75,.15),'Wood')
        for k in range(7):
            x=-3.6+k*1.15
            lathe((x,3.2,z+.1),[(.18,0),(.27,.12),(.24,.45),(.12,.57),(.13,.65)],'Stone' if k%2 else 'Accent',20)
            ring((x,3.2,z+.67),.13,.025,'Metal')
    for x in [-4,4]:
        for z in [.3,2.5]:box((x,3.2,z+.4),(.16,.16,.9),'Wood')
    for x in [-3.8,3.8]:
        lathe((x,-1.8,.05),[(.45,0),(.65,.2),(.73,.8),(.7,.95),(.6,.95),(.56,.2)],'Wood',24)
        for z in [.25,.45,.65,.85]:ring((x,-1.8,z),.6+z*.13,.035,'Ivory')
        if season=='Autumn':
            for j in range(3):
                p=(x+(j-1)*.32,-1.8,.95);ellipsoid(p,(.29,.27,.25),'Accent');tube([p,(p[0]+.05,p[1],p[2]+.39)],.035,'Wood',6)
        elif season=='Winter':
            for j in range(4):tube([(x+(j-1.5)*.2,-1.8,.8),(x+(j-1.5)*.2,-1.8,1.25+j*.04)],[.10,.015],'Accent',6)
        else:
            for j in range(4):leaf((x,-1.8,.9),j*1.6,.65)
    if season=='Summer':
        for x in [-4.8,4.8]:
            for k in range(7):rope((x,-2.1,1.3+k*.28),(x,1.6,1.3+k*.28))
            for k in range(9):rope((x,-2.1+k*.45,1.3),(x,-2.1+k*.45,3.0))
        # 帆の中央に貝の扇形レリーフを置きます。
        for j in range(11):
            a=(j-5)*.14;tube([(0,-2.65,4.55),(math.sin(a)*.65,-2.7,4.65+math.cos(a)*.5),(math.sin(a),-2.65,4.7+math.cos(a)*.8)],[.06,.10,.03],'Ivory',8)
    if season=='Autumn':
        for x in [-4.8,4.8]:
            for j in range(8):leaf((x,-2.15,3.0+j*.2),j*.9,.45,'Accent')
    if season=='Winter':
        # 傾斜屋根の板金リブと不揃いな氷柱。
        for i in range(15):
            x=-5.25+i*.75;tube([(x,-2.55,4.53),(x,.75,5.94),(x,4.05,4.53)],.07,'Metal',8)
            tube([(x,-2.55,4.48),(x+.04,-2.56,4.05-rng.random()*.25)],[.07,.005],'Accent',6)
        for x in [-4.6,4.6]:
            pts=[(x+math.cos(a)*.32,-2.16,3.9+math.sin(a)*.35) for a in np.linspace(0,math.pi*1.8,22)];tube(pts,.045,'Iron',8)

def fusion(season):
    # 元の祭壇中心と背面アーチ位置を保ち、操作スペースを空けます。
    cy=-2.6
    lathe((0,cy,.12),[(0,0),(3.45,0),(3.65,.18),(3.5,.38),(3.2,.5),(2.9,.63),(0,.63)],'Stone',64)
    lathe((0,cy,.7),[(2.3,0),(2.05,.25),(1.8,.9),(2.2,1.05),(2.65,1.15),(2.55,1.4),(2.15,1.4),(1.9,1.15),(0,1.1)],'Stone',48)
    for z,r in [(1,2.05),(1.8,2.55),(2.1,2.5)]:ring((0,cy,z),r,.065,'Metal')
    for a in range(0,360,30):
        t=math.radians(a);x=math.cos(t)*2.15;y=cy+math.sin(t)*2.15
        tube([(x,y,.6),(x*.87,cy+(y-cy)*.87,1.2),(x,y,1.8)],.09,'Metal')
        ellipsoid((math.cos(t)*2.57,cy+math.sin(t)*2.57,2.02),(.08,.08,.08),'Light')
    bowl(0,cy,1.85,1.1)
    # 合成用の二つの供物皿。
    for x in [-2.15,2.15]:bowl(x,cy,2.12,.48)
    for x in [-3.15,3.15]:
        lathe((x,cy,.2),[(.5,0),(.42,.2),(.23,.3),(.19,1.3),(.38,1.5),(.45,1.6)],'Stone',24);lantern(x,cy,1.8,1.05)
    archY=-9.6
    if season=='Spring':
        for sign in [-1,1]:
            pts=[(sign*(3.3-.4*t-.9*t*t),archY+.15*math.sin(t*5),.3+t*7.5) for t in np.linspace(0,1,30)]
            tube(pts,[.43*(1-i/38) for i in range(30)],'Wood',14)
            for j in range(5):
                p=pts[5+j*4];tube([p,(p[0]-sign*.7,archY-.1,p[2]+.4),(p[0]-sign*1.15,archY-.15,p[2]+.8)],[.16,.08,.02],'Wood')
                leaf((p[0]-sign*.8,archY-.2,p[2]+.5),j,.8);blossom((p[0]-sign*.8,archY-.3,p[2]+.6),.3)
            for j in range(4):tube([(sign*3.2,archY,.45),(sign*(3.1+j*.22),archY+.9,.18),(sign*(3.5+j*.2),archY+1.8,.15)],[.2,.14,.025],'Wood')
        ring((0,archY,6.25),1.15,.13,'Metal','xz')
        for j in range(8):blossom((math.cos(j*.785)*1.15,archY-.08,6.25+math.sin(j*.785)*1.15),.24)
    elif season=='Summer':
        # 리브 사이에 굴곡진 조개 면을 채워 뼈대처럼 보이지 않게 합니다.
        verts=[];faces=[];na=96;nr=24
        for j in range(na+1):
            a=(j/na-.5)*1.38
            for k in range(nr+1):
                t=k/nr;r=.3+t*(7.8-.12*(1-math.cos(j/na*24*math.pi)))
                verts.append((math.sin(a)*r,archY-.12+.18*math.sin(t*math.pi)+.08*math.cos(j/na*24*math.pi)*t,.35+math.cos(a)*r))
        for j in range(na):
            for k in range(nr):
                a=j*(nr+1)+k;faces.append((a,a+1,a+nr+2,a+nr+1))
        custom(verts,faces,'Ivory')
        # 서로 다른 높이의 조개 리브가 뒤쪽 성소의 실루엣을 만듭니다.
        for j in range(13):
            a=(j-6)*.115
            pts=[(math.sin(a)*r,archY+.3*math.sin(t*math.pi),.35+math.cos(a)*r) for t,r in [(k/24,.3+k/24*7.8) for k in range(25)]]
            tube(pts,[.12+.10*math.sin(i/24*math.pi) for i in range(25)],'Ivory',10)
        for sign in [-1,1]:
            for k in range(4):tube([(sign*3,archY,.2),(sign*(3+k*.18),archY,1.4),(sign*(2.8+k*.3),archY,2.8+k*.2)],[.27,.18,.04],'Accent',10)
        ring((0,archY-.2,4.6),1.0,.16,'Metal','xz')
    elif season=='Autumn':
        for sign in [-1,1]:
            pts=[(sign*3.25,archY,.3),(sign*3.0,archY,3.2),(sign*2.3,archY,5.9),(sign*.4,archY,8.0)]
            tube(pts,[.48,.36,.24,.08],'Wood',14)
            for k in range(3):tube([(sign*(2.8-k*.5),archY,4.2+k),(sign*(3.4-k*.45),archY,5.1+k),(sign*(3.2-k*.5),archY,6.1+k)],[.15,.1,.02],'Wood')
        for j in range(9):leaf(((j-4)*.6,archY-.2,7.1-abs(j-4)*.35),j*.3,.7,'Accent')
        ring((0,archY,5.9),1.35,.17,'Metal','xz');ring((0,archY-.1,5.9),1.05,.06,'Iron','xz')
        for x in [-3.4,3.4]:
            for j in range(3):ellipsoid((x+(j-1)*.48,archY+1,.55),(.4,.35,.46),'Accent')
    else:
        for sign in [-1,1]:
            for z in range(6):box((sign*3.0,archY,.55+z*.9),(.8,.8,.85),'Stone',.12)
            pts=[(sign*3.0,archY,5.2),(sign*2.65,archY,6.1),(sign*1.75,archY,7.2),(0,archY,8.45)]
            tube(pts,.30,'Stone',8);tube([(p[0],p[1]-.34,p[2]) for p in pts],.075,'Metal',8)
            for j in range(4):tube([(sign*(3.25+j*.18),archY,.25),(sign*(3.25+j*.15),archY,1.4+j*.4)],[.18,.01],'Accent',6)
        ring((0,archY,6.0),1.08,.11,'Metal','xz')
        for j in range(6):
            a=j*math.pi/3;tube([(0,archY-.15,6),(math.cos(a)*1.2,archY-.15,6+math.sin(a)*1.2)],.045,'Ivory',6)
    # 紋章の中心は骨のモチーフ。ゲームの素材テーマを共有します。
    z=6.0 if season!='Summer' else 4.6
    tube([(-.5,archY-.25,z-.35),(.5,archY-.25,z+.35)],.12,'Ivory',10)
    for sign in [-1,1]:
        for dy in [-.1,.1]:ellipsoid((sign*.5,archY-.25+dy,z+sign*.35),(.18,.15,.16),'Ivory')

def export_station():
    bpy.ops.object.select_all(action='DESELECT')
    merged=[]
    groups={kind:[o for o in objects if o.data.materials and o.data.materials[0]==MATS[kind]] for kind in KINDS}
    for kind in KINDS:
        group=groups[kind]
        if not group:continue
        for o in group:o.select_set(True)
        bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();o=bpy.context.object;o.name=station+'_'+kind
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);merged.append(o)
        bpy.ops.object.select_all(action='DESELECT')
    for o in merged:o.select_set(True)
    bpy.context.view_layer.objects.active=merged[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/(station+'.fbx')),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_anim=False,use_mesh_modifiers=True,path_mode='AUTO')
    tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in merged)
    manifest[station]={'triangles':tris,'objects':[o.name for o in merged],'materials':[m.name for m in MATS.values()]}
    # 検査用のシーンでは八台を並べ、FBX は各々原点で書き出します。
    x=SEASONS.index(season)*17;y=0 if 'Shop' in station else 19
    for o in merged:o.location=(x,y,0)

def build_one(season_arg):
    global season,station,objects,MATS
    season=season_arg;MATS={k:texture(season,k,c,400+SEASONS.index(season)*20+i) for i,(k,c) in enumerate(zip(KINDS,PALETTES[season]))}
    for typ in ['Shop','Fusion']:
        station=season+'_'+typ;objects=[];rng.seed(451+SEASONS.index(season));shop(season) if typ=='Shop' else fusion(season);export_station()
    (SOURCE/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print('BUILT',season,manifest[season+'_Shop']['triangles'],manifest[season+'_Fusion']['triangles'])
