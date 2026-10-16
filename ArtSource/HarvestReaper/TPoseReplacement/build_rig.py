"""Create a T-pose rig with independent rigid scythe and garment controls."""
import bpy,math,json,os
from mathutils import Vector,Matrix
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife'
WORK=ROOT+'/ArtSource/HarvestReaper/TPoseReplacement'
H=6.25
for name in ['NewReaper_Rig','NewReaper_Body','NewReaper_Scythe']:
    o=bpy.data.objects.get(name)
    if o:bpy.data.objects.remove(o,do_unlink=True)
source=bpy.data.objects['ReaperNew_Source'];body=source.copy();body.data=source.data.copy();body.name='NewReaper_Body';bpy.context.scene.collection.objects.link(body)
for v in body.data.vertices:v.co*=H
body.vertex_groups.clear();body.modifiers.clear()
data=bpy.data.armatures.new('NewReaper_Skeleton');rig=bpy.data.objects.new('NewReaper_Rig',data);bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
def bone(n,a,b,parent):
    e=data.edit_bones.new(n);e.head=Vector(a)*H;e.tail=Vector(b)*H
    if parent:e.parent=data.edit_bones[parent]
    return e
bone('Root',(0,0,0),(0,0,.1),None)
bone('Hips',(0,0,.47),(0,0,.55),'Root');bone('Spine',(0,0,.55),(0,0,.65),'Hips');bone('Chest',(0,0,.65),(0,0,.79),'Spine');bone('Head',(0,0,.79),(0,0,.94),'Chest')
for side,sign in [('L',-1),('R',1)]:
    bone(side+'Arm',(sign*.13,0,.745),(sign*.29,0,.745),'Chest')
    bone(side+'Forearm',(sign*.29,0,.745),(sign*.405,0,.745),side+'Arm')
    bone(side+'Hand',(sign*.405,0,.745),(sign*.48,0,.72),side+'Forearm')
    bone(side+'Sleeve',(sign*.25,0,.735),(sign*.25,0,.56),side+'Arm')
    bone(side+'Thigh',(sign*.055,0,.47),(sign*.083,-.018,.285),'Hips')
    bone(side+'Shin',(sign*.083,-.018,.285),(sign*.083,0,.065),side+'Thigh')
    bone(side+'Foot',(sign*.083,0,.065),(sign*.09,-.075,.025),side+'Shin')
bone('DeathRobe',(0,0,.47),(0,0,.25),'Hips')
for name,x,y in [('RobeFrontLeft',-.08,-.05),('RobeFrontRight',.08,-.05),('RobeBackLeft',-.08,.08),('RobeBackRight',.08,.08)]:
    bone(name,(x,y,.47),(x,y,.18),'DeathRobe')
# The shaft crosses the curled palm; weapon geometry has no body weights.
grip=Vector((.452,0,.725))*H
bone('Scythe',grip/H,(grip+Vector((0,0,1)))/H,'RHand')
bpy.ops.object.mode_set(mode='OBJECT')
for b in data.bones:b.use_deform=b.name!='Root'
groups={b.name:body.vertex_groups.new(name=b.name) for b in data.bones}
foot_ids=set(json.load(open(WORK+'/foot_vertex_ids.json')))
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def blend(a,b,t):return {a:1-t,b:t}
for v in body.data.vertices:
    x,y,z=v.co/H;s='L' if x<0 else 'R';ax=abs(x)
    if z>.79:w=blend('Chest','Head',smooth(.785,.82,z))
    elif ax>.15 and z>.52:
        if ax>.385:w=blend(s+'Forearm',s+'Hand',smooth(.385,.425,ax))
        elif z<.71 and ax>.185:w={s+'Sleeve':1}
        elif ax>.26:w=blend(s+'Arm',s+'Forearm',smooth(.265,.31,ax))
        else:w=blend('Chest',s+'Arm',smooth(.14,.20,ax))
    elif z<.49:
        # Confine leg influence to the anatomical tubes, keeping the back hem independent.
        center=.083 if z<.32 else .083-(z-.32)*.16
        ry=.049 if .23<z<.33 else .038
        dist=((ax-center)/(.039 if z>.075 else .065))**2+((y-(-.018 if z>.20 else 0))/ry)**2
        leg=dist<1.15 and y<.04
        if z<.12:leg=v.index in foot_ids
        if leg:
            if z<.115:w=blend(s+'Foot',s+'Shin',smooth(.055,.105,z))
            elif z<.335:w=blend(s+'Shin',s+'Thigh',smooth(.255,.315,z))
            else:w=blend(s+'Thigh','Hips',smooth(.43,.49,z))
        else:
            panel='Robe'+('Back' if y>.025 else 'Front')+('Left' if x<0 else 'Right')
            w=blend(panel,'Hips',smooth(.43,.49,z))
    elif z<.60:w=blend('Hips','Spine',smooth(.50,.59,z))
    else:w=blend('Spine','Chest',smooth(.61,.70,z))
    for n,a in w.items():
        if a>1e-6:groups[n].add([v.index],a,'REPLACE')
mod=body.modifiers.new('Anatomical Skin','ARMATURE');mod.object=rig;body.parent=rig
weapon=bpy.data.objects['ReaperNew_WeaponSource'].copy();weapon.data=weapon.data.copy();weapon.name='NewReaper_Scythe';bpy.context.scene.collection.objects.link(weapon)
# Preserve the supplied blade/lantern proportions and place the middle shaft in the palm.
shaft=Vector((-.0401,-.0052,.52));weapon_scale=6.8
for v in weapon.data.vertices:v.co=(v.co-shaft)*weapon_scale+grip
weapon.vertex_groups.clear();weapon.vertex_groups.new(name='Scythe').add(list(range(len(weapon.data.vertices))),1,'REPLACE')
weapon.modifiers.clear();mod=weapon.modifiers.new('Rigid weapon grip','ARMATURE');mod.object=rig;weapon.parent=rig
os.makedirs(WORK+'/Textures',exist_ok=True)
for ob,label in [(body,'Body'),(weapon,'Scythe')]:
    mat=ob.data.materials[0].copy();mat.name='HarvestReaper_'+label;ob.data.materials[0]=mat
    for n in mat.node_tree.nodes:
        if n.type=='TEX_IMAGE' and n.image:
            suffix='Normal' if 'normal' in n.image.name.lower() else 'BaseColor'
            n.image.filepath_raw=WORK+'/Textures/'+label+'_'+suffix+'.png';n.image.file_format='PNG';n.image.save()
for o in bpy.context.scene.objects:o.hide_set(o not in [rig,body,weapon])
bpy.context.view_layer.update()
with open(WORK+'/new_bind.json','w') as f:json.dump({b.name:{'matrix':[list(row) for row in b.matrix_local],'head':list(b.head_local),'tail':list(b.tail_local)} for b in data.bones},f)
print(json.dumps({'bones':len(data.bones),'body_vertices':len(body.data.vertices),'weapon_vertices':len(weapon.data.vertices)}))
