"""Retarget saved pattern directions onto fixed-length anatomical limbs and a rigid palm grip."""
import bpy,csv,json,math
from mathutils import Vector,Matrix,Quaternion
ROOT='C:/Users/mbc/Desktop/NC_AI/DogLife';WORK=ROOT+'/ArtSource/HarvestReaper/TPoseReplacement'
rig=bpy.data.objects['NewReaper_Rig'];rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
body=bpy.data.objects['NewReaper_Body']
foot_points={}
weapon_points=[v.co.copy()-rest['Scythe'].translation for v in bpy.data.objects['NewReaper_Scythe'].data.vertices]
for side in ['L','R']:
    gi=body.vertex_groups[side+'Foot'].index
    points=[v.co.copy()-rest[side+'Foot'].translation for v in body.data.vertices if any(g.group==gi and g.weight>.95 for g in v.groups)]
    foot_points[side]=points
oldjson=json.load(open(WORK+'/old_bind.json'));old={n:Matrix(v['matrix']) for n,v in oldjson.items()}
S=Matrix(((-1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)));Si=S.inverted()
clips={}
with open(WORK+'/existing_motion.csv') as f:
    for row in csv.reader(f):
        name,t,bone=row[:3];t=float(t)
        if t<0:continue
        vals=list(map(float,row[3:]));m=Matrix([vals[i:i+4] for i in range(0,12,4)]+[[0,0,0,1]])
        clips.setdefault(name,{}).setdefault(t,{})[bone]=Si@m@S
def trs(p,q):return Matrix.Translation(p)@q.to_matrix().to_4x4()
def point(pose,n):
    b=rig.data.bones[n]
    return pose[b.parent.name]@rest[b.parent.name].inverted()@rest[n].translation if b.parent else rest[n].translation.copy()
def direction(d,a,b):return (d[a].to_3x3()@(old[b].translation-old[a].translation)).normalized()
def orient(a,b,axis):return (rest[b].translation-rest[a].translation).rotation_difference(axis)@rest[a].to_quaternion()
def solve(start,target,l1,l2,pole):
    delta=target-start;axis=delta.normalized();dist=max(abs(l1-l2)+.001,min(delta.length,l1+l2-.001))
    bend=pole-axis*pole.dot(axis)
    if bend.length<.001:bend=Vector((0,-1,0))-axis*axis.dot(Vector((0,-1,0)))
    bend.normalize();along=(l1*l1-l2*l2+dist*dist)/(2*dist)
    return start+axis*along+bend*math.sqrt(max(0,l1*l1-along*along)),start+axis*dist
def sample(name,d,time):
    pose={};pose['Root']=d['Root']@rest['Root'];pose['Hips']=d['Hips']@rest['Hips']
    for n in ['Spine','Chest','Head']:pose[n]=trs(point(pose,n),d[n].to_quaternion()@rest[n].to_quaternion())
    for side in ['L','R']:
        a,b,c=[side+n for n in ['Arm','Forearm','Hand']]
        u=direction(d,a,b);v=direction(d,b,c)
        pose[a]=trs(point(pose,a),orient(a,b,u));pose[b]=trs(point(pose,b),orient(b,c,v))
        # Keep hand surfaces attached to the wrist while preserving safe-zone pointing.
        original_finger=Vector((-.10,-.977,.19)) if side=='L' else Vector((.28,0,0))
        target_finger=d[c].to_3x3()@original_finger
        q=(rest[c].to_3x3()@Vector((0,1,0))).rotation_difference(target_finger)@rest[c].to_quaternion()
        pose[c]=trs(point(pose,c),q)
        sl=side+'Sleeve';pose[sl]=pose[a]@rest[a].inverted()@rest[sl]
        a,b,c=[side+n for n in ['Thigh','Shin','Foot']]
        start=point(pose,a);u=direction(d,a,b);v=direction(d,b,c)
        l1=(rest[b].translation-rest[a].translation).length;l2=(rest[c].translation-rest[b].translation).length
        target=start+u*l1+v*l2
        fq=d[c].to_quaternion()@rest[c].to_quaternion()
        if 'Death' not in name and target.z<rest[c].translation.z+.20:
            heading=d[c].to_quaternion().to_euler('XYZ').z
            fq=Quaternion(Vector((0,0,1)),heading)@rest[c].to_quaternion()
        delta=fq@rest[c].to_quaternion().inverted()
        floor_height=.025-min((delta@p).z for p in foot_points[side])
        target.z=max(floor_height,target.z)
        knee,ankle=solve(start,target,l1,l2,u)
        pose[a]=trs(start,orient(a,b,knee-start));pose[b]=trs(knee,orient(b,c,ankle-knee))
        pose[c]=trs(ankle,fq)
    # Follow the existing blade plane while the entire weapon remains rigid.
    correction=Vector((0,0,1)).rotation_difference(Vector((-.09,0,.335)))@Quaternion(Vector((1,0,0)),math.pi/2)
    wq=d['Scythe'].to_quaternion()@correction@rest['Scythe'].to_quaternion()
    hq=wq@rest['Scythe'].to_quaternion().inverted()@rest['RHand'].to_quaternion()
    pose['RHand']=trs(pose['RHand'].translation,hq)
    pose['Scythe']=trs(point(pose,'Scythe'),wq)
    if 'Death' in name:
        delta=wq@rest['Scythe'].to_quaternion().inverted()
        lowest=pose['Scythe'].translation.z+min((delta@p).z for p in weapon_points)
        settle=max(0,min(1,(time-3.0)/.7));settle=settle*settle*(3-2*settle)
        lift=max(0,.025-lowest)+min(0,.025-lowest)*settle
        start=pose['RArm'].translation;goal=pose['RHand'].translation+Vector((0,0,lift))
        upper=(rest['RForearm'].translation-rest['RArm'].translation).length
        lower=(rest['RHand'].translation-rest['RForearm'].translation).length
        elbow,wrist=solve(start,goal,upper,lower,pose['RForearm'].translation-start)
        pose['RArm']=trs(start,orient('RArm','RForearm',elbow-start))
        pose['RForearm']=trs(elbow,orient('RForearm','RHand',wrist-elbow))
        pose['RHand']=trs(wrist,hq);pose['Scythe']=trs(point(pose,'Scythe'),wq)
        pose['RSleeve']=pose['RArm']@rest['RArm'].inverted()@rest['RSleeve']
    pose['DeathRobe']=pose['Hips']@rest['Hips'].inverted()@rest['DeathRobe']
    if 'Death' in name:
        m=pose['DeathRobe'];vertical=abs((m.to_quaternion()@Vector((0,1,0))).z)
        compression=max(.22,min(1,pose['Hips'].translation.z/max(.001,2.9*vertical)))
        pose['DeathRobe']=m@Matrix.Diagonal((1,compression,1,1))
    for n in ['RobeFrontLeft','RobeFrontRight','RobeBackLeft','RobeBackRight']:
        pose[n]=pose['DeathRobe']@rest['DeathRobe'].inverted()@rest[n]
    return pose
out=open(WORK+'/retargeted_motion.csv','w',newline='');writer=csv.writer(out)
cache={};lengths={};worst=0
for name,frames in clips.items():
    times=sorted(frames);lengths[name]=times[-1];cache[name]={}
    # Dense enough for fast strikes; retain the exact first and last pose.
    for i,t in enumerate(times):
        pose=sample(name,frames[t],t);cache[name][t]=pose
        for n,m in pose.items():
            delta=S@m@rest[n].inverted()@Si
            writer.writerow([name,t,n]+[delta[r][c] for r in range(3) for c in range(4)])
        for side in ['L','R']:
            for a,b in [(side+'Thigh',side+'Shin'),(side+'Shin',side+'Foot'),(side+'Arm',side+'Forearm'),(side+'Forearm',side+'Hand')]:
                worst=max(worst,abs((pose[a].translation-pose[b].translation).length-(rest[a].translation-rest[b].translation).length))
out.close()
bpy.app.driver_namespace['new_reaper_poses']=cache
bpy.app.driver_namespace['new_reaper_rest']=rest
with open(WORK+'/retarget_report.json','w') as f:json.dump({'clips':lengths,'max_limb_length_error_m':worst},f,indent=2)
print(json.dumps({'clips':len(cache),'max_limb_length_error_m':worst}))
