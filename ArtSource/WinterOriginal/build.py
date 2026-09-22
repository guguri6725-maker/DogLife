import bpy, math, random, json, os
import numpy as np
from mathutils import Vector

# 기존 모델을 읽지 않고 기본 정점으로 새 겨울 에셋을 제작한다.
ROOT = r'C:/Users/mbc/Desktop/NC_AI/DogLife'
OUT = ROOT + '/Assets/Art/WinterOriginal'
CFG = json.load(open(ROOT + '/ArtSource/WinterOriginal/config.json', encoding='utf-8-sig'))
KINDS = ['Snow', 'Slate', 'Ice', 'Bark', 'Needles']
COL = bpy.data.collections.get('WinterOriginal')
if COL is None:
    COL = bpy.data.collections.new('WinterOriginal')
    bpy.context.scene.collection.children.link(COL)

def textures():
    # 주기적 노이즈를 사용하여 텍스처 경계가 이어지도록 만든다.
    n = 1024
    y, x = np.mgrid[0:n,0:n].astype(np.float32) / n
    rng = np.random.default_rng(924)
    noise = np.zeros((n,n), np.float32)
    for k in range(1,70):
        a,b = rng.integers(-k,k+1,2)
        noise += np.sin(2*np.pi*(a*x+b*y)+rng.random()*6.28)/(k**.85)
    noise = (noise-noise.mean())/(noise.std()+1e-5)
    # 주기적인 직조무늬 대신 무작위 광물 입자의 미세한 표면을 만든다.
    freq=np.fft.fftfreq(n)
    fy,fx=np.meshgrid(freq,freq)
    spectrum=np.fft.fft2(rng.normal(size=(n,n)))
    fine=np.fft.ifft2(spectrum/(1+np.sqrt(fx*fx+fy*fy)*90)).real
    fine=(fine-fine.mean())/fine.std()
    for kind in KINDS:
        if kind == 'Snow':
            h=.5+.10*noise+.018*fine
            base=np.array([.86,.93,.97]); c=base[None,None,:]+(h-.5)[...,None]*.24
        elif kind == 'Slate':
            strata=np.sin(2*np.pi*(9*y+.16*np.sin(2*np.pi*x))+.75*noise)
            veins=np.exp(-np.abs(strata)*18)
            h=.48+.12*noise+.025*fine-.13*veins
            c=np.array([.25,.32,.39])[None,None,:]+(h-.45)[...,None]*.48
        elif kind == 'Ice':
            vein=np.abs(np.sin(2*np.pi*(3*x+2*y)+.25*np.sin(2*np.pi*5*y)))
            vein2=np.abs(np.sin(2*np.pi*(x-4*y)+.3*np.sin(2*np.pi*3*x)))
            cracks=np.maximum(np.exp(-vein*90),np.exp(-vein2*100))
            h=.5+.025*noise+.12*cracks
            c=np.array([.16,.43,.58])[None,None,:]+noise[...,None]*.04+cracks[...,None]*.48
        elif kind == 'Bark':
            ridges=np.sin(2*np.pi*(17*x+.13*np.sin(2*np.pi*3*y)))
            h=.5+.17*ridges+.08*noise
            c=np.array([.25,.19,.15])[None,None,:]+(h-.5)[...,None]*.30
        else:
            needles=np.sin(2*np.pi*(45*x+9*y))*np.sin(2*np.pi*(45*x-9*y))
            h=.5+.09*noise+.12*needles
            c=np.array([.075,.19,.18])[None,None,:]+(h-.5)[...,None]*.23
        dx=(np.roll(h,-1,1)-np.roll(h,1,1))*5
        dy=(np.roll(h,-1,0)-np.roll(h,1,0))*5
        normals=np.stack([-dx,-dy,np.ones_like(h)],axis=-1)
        normals/=np.linalg.norm(normals,axis=-1)[...,None]
        for suffix,pixels in [('Color',np.clip(c,.015,1)),('Normal',normals*.5+.5)]:
            img=bpy.data.images.new('WinterOriginal_'+kind+'_'+suffix,n,n,alpha=True)
            if suffix=='Normal': img.colorspace_settings.name='Non-Color'
            rgba=np.concatenate([pixels,np.ones((n,n,1),np.float32)],axis=-1).astype(np.float32)
            img.pixels.foreach_set(rgba.ravel())
            img.filepath_raw=OUT+'/Textures/'+kind+'_'+suffix+'.png'
            img.file_format='PNG'; img.save()
        mat=bpy.data.materials.get('WO_'+kind) or bpy.data.materials.new('WO_'+kind)
        mat.use_nodes=True
        nt=mat.node_tree; nt.nodes.clear()
        bs=nt.nodes.new('ShaderNodeBsdfPrincipled'); dest=nt.nodes.new('ShaderNodeOutputMaterial')
        tex=nt.nodes.new('ShaderNodeTexImage'); tex.image=bpy.data.images.get('WinterOriginal_'+kind+'_Color')
        nm=nt.nodes.new('ShaderNodeTexImage'); nm.image=bpy.data.images.get('WinterOriginal_'+kind+'_Normal')
        normal=nt.nodes.new('ShaderNodeNormalMap'); normal.inputs['Strength'].default_value=.5
        nt.links.new(tex.outputs['Color'],bs.inputs['Base Color']); nt.links.new(nm.outputs['Color'],normal.inputs['Color'])
        nt.links.new(normal.outputs['Normal'],bs.inputs['Normal']); nt.links.new(bs.outputs['BSDF'],dest.inputs['Surface'])
        bs.inputs['Roughness'].default_value=.24 if kind=='Ice' else .82

class Geo:
    def __init__(self): self.v=[]; self.f=[]; self.m=[]
    def face(self,points,mat):
        start=len(self.v); self.v.extend(points); self.f.append(tuple(range(start,start+len(points)))); self.m.append(mat)
    def box(self,c,s,mat,steps=1,relief=0):
        # 분할 표면의 실제 요철과 둥근 모서리가 암석의 빛 반사를 만든다.
        c=np.array(c); s=np.array(s)/2
        for axis in range(3):
            u=(axis+1)%3; v=(axis+2)%3
            for sign in [-1,1]:
                grid=[]
                for j in range(steps+1):
                    row=[]
                    for i in range(steps+1):
                        p=c.copy(); p[axis]+=sign*s[axis]
                        a=i/steps*2-1; b=j/steps*2-1
                        p[u]+=a*s[u]; p[v]+=b*s[v]
                        radius=min(s)*.24
                        delta=p-c; nearest=np.clip(delta,-s+radius,s-radius)
                        edge=delta-nearest
                        if np.linalg.norm(edge)>0:p=c+nearest+edge/np.linalg.norm(edge)*radius
                        bump=math.sin(i*1.91+j*2.17+c[0]*4)*math.sin(i*.47-j*.8)
                        p[axis]+=sign*relief*bump*math.sin(math.pi*i/steps)*math.sin(math.pi*j/steps)
                        row.append(tuple(p))
                    grid.append(row)
                for j in range(steps):
                    for i in range(steps):
                        pts=[grid[j][i],grid[j][i+1],grid[j+1][i+1],grid[j+1][i]]
                        self.face(pts if sign>0 else pts[::-1],mat)
    def rock(self,c,s,mat,seed=1,segments=20,rings=12,snow=True):
        rng=random.Random(seed); phase=rng.random()*6.28
        coords=[]
        for j in range(rings+1):
            phi=math.pi*j/rings
            row=[]
            for i in range(segments):
                t=i*2*math.pi/segments
                r=1+.065*math.sin(5*t+phase)*math.sin(phi)+.04*math.cos(9*t-3*phi+phase)
                p=(c[0]+s[0]*math.sin(phi)*math.cos(t)*r/2,c[1]+s[1]*math.sin(phi)*math.sin(t)*r/2,c[2]+s[2]*math.cos(phi)/2)
                row.append(p)
            coords.append(row)
        for j in range(rings):
            for i in range(segments):
                k=(i+1)%segments
                self.face([coords[j][i],coords[j+1][i],coords[j+1][k],coords[j][k]],0 if snow and j<rings*.3 else mat)
    def tube(self,a,b,r1,r2,mat,sides=12,rings=6,ripple=0):
        a=Vector(a); b=Vector(b); axis=(b-a).normalized()
        ref=Vector((0,0,1)) if abs(axis.z)<.9 else Vector((1,0,0))
        u=axis.cross(ref).normalized(); v=axis.cross(u)
        rows=[]
        for j in range(rings+1):
            t=j/rings; r=r1+(r2-r1)*t
            rows.append([tuple(a+(b-a)*t+(u*math.cos(i*math.tau/sides)+v*math.sin(i*math.tau/sides))*r*(1+ripple*math.sin(i*3.1+j*1.4))) for i in range(sides)])
        for j in range(rings):
            for i in range(sides):
                k=(i+1)%sides; self.face([rows[j][i],rows[j][k],rows[j+1][k],rows[j+1][i]],mat)
        self.face(rows[0][::-1],mat); self.face(rows[-1],mat)

def make_shape(name,high=True):
    g=Geo(); rng=random.Random(sum(ord(c) for c in name)); n=16 if high else 8
    # 모든 형태는 새로 만든 기본 메시를 조합한다. 좌표는 Z-up, 전체 크기는 마지막에 맞춘다.
    if name == 'WinterMerchantStall':
        for x in [-3.1,3.1]:
            for y in [-1.6,1.6]:g.tube((x,y,0),(x,y,3.6),.13,.1,3,12,8)
        for i in range(12):
            x=-3.3+i*.6
            g.box((x,0,3.65),(.63,4.2,.18),3,4,.025)
            g.rock((x,0,3.83),(.75,4.25,.30),0,seed=i,segments=12,rings=8)
        g.box((0,-1.35,1),(6.2,.7,1.8),3,8,.025)
        g.box((0,-1.35,1.96),(6.5,1,.17),3,8,.025)
        for i in range(7):
            x=-2.5+i*.8
            g.tube((x,-1.35,2.08),(x,-1.35,2.55),.14,.09,2,12,6)
            g.tube((x,-1.35,2.55),(x,-1.35,2.7),.07,.07,3,8,2)
        for x in [-2.5,2.5]:
            g.box((x,.7,.55),(1.1,1.2,1.1),3,6,.03)
            for z in [.2,.85]:g.box((x,.7,z),(1.15,1.25,.08),1,2,0)
    elif name == 'WinterSynthesisForge':
        g.box((0,1,1.1),(4.5,2,2.2),1,12,.07)
        g.box((0,.7,2.35),(4.8,2.5,.35),1,8,.03)
        for x in [-1.8,1.8]:g.box((x,.6,3.2),(.6,1.8,1.5),1,6,.03)
        g.box((0,.6,4),(4.4,2,.5),1,8,.03)
        g.box((.9,1,4.8),(1,1,1.3),1,6,.03)
        g.rock((0,.6,4.3),(4.6,2.2,.25),0,segments=24,rings=8)
        g.box((0,-1.4,.6),(1.7,1.5,1.2),3,6,.02)
        g.box((0,-1.4,1.5),(2.8,.8,.6),1,8,.02)
        g.tube((1.3,-1.4,1.5),(2.4,-1.4,1.6),.3,.03,1,12,5)
        for i in range(9):
            a=i*2.4;g.tube((math.cos(a),.4+math.sin(a)*.4,2.5),(math.cos(a),.4+math.sin(a)*.4,3+rng.random()*.5),.18,0,2,7,6)
    elif name.endswith('_Floor'):
        h=1; step=1
        g.box((0,0,.5),(6,7,h),2 if 'Cave' in name or 'IceRegion' in name or 'Glacier' in name else 0,step,0)
    elif name.endswith('_Wall'):
        icy='Cave' in name or 'IceRegion' in name or 'Glacier' in name
        for row in range(4):
            for col in range(5):
                x=-2.4+col*1.2; z=.8+row*1.6
                if row==3:z+=rng.uniform(-.25,.30)
                depth=1.35+rng.random()*.25
                detail=(7 if name.startswith(('SnowCanyon','IceRegion')) else 6) if high else 3
                g.box((x,rng.uniform(-.12,.12),z),(1.3,depth,1.73),2 if icy and (row+col)%3==0 else 1,detail,.13)
                if row==3 or rng.random()<.2:
                    g.rock((x,-.02,z+.81),(1.32,depth+.10,.25),0,seed=row*10+col,segments=12 if high else 8,rings=4 if high else 3)
        for i in range(20 if high else 8):
            x=-2.9+5.8*i/(19 if high else 7)
            z=6.35; length=.2+rng.random()*.65
            g.tube((x,-.79,z),(x,-.8,z-length),.04+rng.random()*.04,.003,2,7 if high else 5,3)
    elif 'Pine' in name:
        g.tube((0,0,0),(.09,.04,6),.22,.035,3,n,14,.06)
        for layer in range(6):
            z=.8+layer*.85; radius=1.75*(1-layer/7)
            g.tube((0,0,z),(.04,.02,z+1.45),radius,.02,4,28 if high else 16,12 if high else 6,.055)
            g.tube((0,0,z+.16),(.04,.02,z+1.51),radius*.94,.016,0,28 if high else 16,12 if high else 6,.05)
            for k in range(7):
                a=k*math.tau/7+layer*.55
                end=(math.cos(a)*radius*.85,math.sin(a)*radius*.85,z+.03)
                g.tube((0,0,z+.2),end,.055,.009,3,7,3)
    elif 'Grass' in name:
        for i in range(35 if high else 18):
            a=i*2.399; r=.3+(i%7)*.2
            x=math.cos(a)*r; y=math.sin(a)*r; h=.8+rng.random()*1.6
            g.tube((x,y,0),(x*1.3,y*1.3,h),.035,.006,4,6,5)
            g.rock((x*1.3,y*1.3,h),(.14,.12,.4),0,seed=i,segments=8,rings=6)
    elif 'Tree' in name or 'Shrub' in name:
        shrub='Shrub' in name or 'Grass' in name
        g.tube((0,0,0),(.12,0,5.6),.35 if not shrub else .12,.04,3,n,18,.08)
        for i in range(16 if high else 10):
            a=i*2.399; z=.8+(i%6)*.65; r=1.1+(i%3)*.25
            tip=(math.cos(a)*r,math.sin(a)*r,z+1.2)
            g.tube((.05,0,z),tip,.12,.015,3,10 if high else 6,5)
            g.tube((.05,0,z+.09),(tip[0]*.85,tip[1]*.85,tip[2]+.06),.085,.009,0,8,4)
            g.tube((tip[0]*.55,tip[1]*.55,z+.6),(tip[0]*.7+.3,tip[1]*.7-.25,z+1.4),.04,.004,3,7,3)
    elif 'Log' in name:
        g.tube((-2.8,0,.7),(2.8,.2,.8),.7,.55,3,32 if high else 12,24 if high else 6,.09)
        for i in range(6):g.rock((-2.4+i*.9,.05,1.31),(.99,1.1,.3),0,seed=i,segments=n,rings=8)
        g.tube((.8,0,.8),(1.4,.5,1.7),.18,.06,3,12,5)
    elif 'Throne' in name:
        g.box((0,0,.35),(4,3.8,.7),1,n//2,.05)
        g.box((0,.7,2.7),(3,.6,4.7),2,n//2,.03)
        g.box((0,-.3,1.3),(2.7,2.3,.65),2,n//2,.04)
        for x in [-1.55,1.55]:
            g.box((x,-.2,1.9),(.5,2.7,1.3),1,n//2,.04)
            g.tube((x,.8,1),(x,.8,5.6),.28,0,2,8,14)
        g.rock((0,.7,5),(3.3,.8,.22),0,segments=n,rings=6)
    elif 'Arch' in name:
        for i in range(11):
            t=math.pi*i/10
            g.rock((math.cos(t)*2.4,0,.45+math.sin(t)*2),(1.05,2,1.3),1,seed=i,segments=n,rings=14 if high else 6)
    elif 'Canopy' in name:
        g.rock((0,0,4.8),(4.3,3.2,1.4),1,segments=32 if high else 16,rings=18 if high else 8)
        for i in range(22 if high else 12):
            a=i*2.399;r=.3+(i%5)*.35;x=math.cos(a)*r;y=math.sin(a)*r
            g.tube((x,y,4.4),(x+.1,y,1+rng.random()*2.1),.13+rng.random()*.09,.002,2,10 if high else 6,12 if high else 4)
    elif 'Ramp' in name:
        # 경사면과 바닥의 면 방향을 바깥쪽으로 통일해 양쪽 충돌을 보장한다.
        g.face([(2,-3,0),(2,3,3),(-2,3,3),(-2,-3,0)],0)
        g.face([(-2,3,0),(2,3,0),(2,-3,0),(-2,-3,0)],1)
        g.face([(-2,3,3),(2,3,3),(2,3,0),(-2,3,0)],1)
        g.face([(-2,3,3),(-2,3,0),(-2,-3,0)],1)
        g.face([(2,3,0),(2,3,3),(2,-3,0)],1)
        for i in range(7):
            y=-2.5+i*.8;z=(y+3)*.5
            for x in [-1.9,1.9]:g.rock((x,y,z),(.4,.7,.35),1,seed=i,segments=n,rings=8)
    elif any(k in name for k in ['Crystal','Spire','Column','Pillar']):
        count=9 if 'Cluster' in name or 'Rubble' in name else 5
        for i in range(count):
            a=i*2.399; r=0 if i==0 else .9+rng.random()*.65
            h=5.6 if i==0 else 1.8+rng.random()*2.7
            p=(math.cos(a)*r,math.sin(a)*r,0)
            g.tube(p,(p[0]*1.25,p[1]*1.25,h*.78),.45 if i else .72,.34 if i else .53,2,8,20 if high else 6,.015)
            g.tube((p[0]*1.25,p[1]*1.25,h*.78),(p[0]*1.35+.12,p[1]*1.35,h),.34 if i else .53,0,2,8,6 if high else 2)
            g.rock((p[0],p[1],.12),(.85,.85,.35),0,seed=i,segments=n,rings=6)
    elif any(k in name for k in ['Pool','Sheet','Platform']):
        g.rock((0,0,.23),(5,5.7,.46),2,segments=64 if high else 24,rings=22 if high else 8,snow=False)
        for i in range(12):
            a=i*math.tau/12
            g.rock((math.cos(a)*2.35,math.sin(a)*2.7,.21),(.7,.65,.38),0,seed=i,segments=n,rings=8)
    else:
        tall='Cliff' in name; count=5 if 'Rubble' in name else 3
        for i in range(count):
            z=1.8+i*1.3 if tall else .7+i*.3
            g.rock((math.sin(i*2)*.65,math.cos(i*2)*.55,z),(3.5-i*.35,3.2-i*.25,3.3 if tall else 2),2 if 'Ice' in name else 1,seed=i+37,segments=40 if high else 16,rings=26 if high else 10)
    return g

def make_asset(index):
    p=CFG[index]; name=p['name']
    for o in list(COL.objects):
        if o.name.startswith(name+'_') or o.name.startswith('WO_'+name+'_'): bpy.data.objects.remove(o,do_unlink=True)
    objects=[]
    for lod in [0,1]:
        g=make_shape(name,lod==0); vs=np.array(g.v,dtype=float)
        lo=vs.min(axis=0); hi=vs.max(axis=0)
        # Unity X/Y/Z를 Blender X/-Y/Z로 변환. 기존 크기만 참고하고 형상은 새로 생성한다.
        target=np.array([p['max'][0]-p['min'][0],p['max'][2]-p['min'][2],p['max'][1]-p['min'][1]])
        if name.endswith('_Wall'):target[1]=min(target[1],2.2)
        vs=(vs-lo)/(hi-lo+1e-9)*target
        vs[:,0]+=p['min'][0];vs[:,1]-=target[1]/2;vs[:,2]+=p['min'][1]
        mesh=bpy.data.meshes.new(name+'_Original_LOD'+str(lod));mesh.from_pydata(vs.tolist(),[],g.f);mesh.update()
        ob=bpy.data.objects.new('WO_'+name+'_LOD'+str(lod),mesh);COL.objects.link(ob)
        for k in KINDS:mesh.materials.append(bpy.data.materials['WO_'+k])
        for poly,mat in zip(mesh.polygons,g.m):poly.material_index=mat
        uv=mesh.uv_layers.new(name='SurfaceUV')
        for poly in mesh.polygons:
            axis=max(range(3),key=lambda a:abs(poly.normal[a]))
            a,b=[i for i in range(3) if i!=axis]
            for li in poly.loop_indices:
                co=mesh.vertices[mesh.loops[li].vertex_index].co
                uv_scale=2 if name.endswith('_Floor') else .5
                uv.data[li].uv=(co[a]*uv_scale,co[b]*uv_scale)
        objects.append(ob)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=OUT+'/Models/'+name+'.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,bake_anim=False,add_leaf_bones=False,use_mesh_modifiers=True)
    return {'name':name,'triangles':[sum(len(f.vertices)-2 for f in ob.data.polygons) for ob in objects]}

def build(start,end):
    result=[]
    for i in range(start,min(end,len(CFG))):result.append(make_asset(i))
    print(json.dumps(result))


