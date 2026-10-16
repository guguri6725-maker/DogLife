"""Join pattern phases in local bone space and remove unwanted wind-up turns."""
parents={b.name:b.parent.name if b.parent else None for b in rig.data.bones}
order=[b.name for b in rig.data.bones]
def localize(p):
    return {n:(p[parents[n]].inverted()@p[n] if parents[n] else p[n].copy()) for n in order}
def mix(a,b,t):
    result={}
    for n in order:
        ap,aq,asc=a[n].decompose();bp,bq,bsc=b[n].decompose()
        result[n]=Matrix.LocRotScale(ap.lerp(bp,t),aq.slerp(bq,t),asc.lerp(bsc,t))
    return result
def ease(t):return t*t*(3-2*t)
local={name:{t:localize(p) for t,p in frames.items()} for name,frames in cache.items()}
idle_world=cache['HarvestReaper_Idle'][0.0]
# Remove the idle's sideways torso/head bias while retaining animated changes.
for n in ['Spine','Chest','Head']:
    par=parents[n]
    own=(idle_world[n].to_quaternion()@rest[n].to_quaternion().inverted()).to_euler().z
    parent_yaw=(idle_world[par].to_quaternion()@rest[par].to_quaternion().inverted()).to_euler().z
    axis=idle_world[par].to_quaternion().inverted()@Vector((0,0,1))
    correction=Quaternion(axis,parent_yaw-own)
    for name,frames in local.items():
        if 'Death' in name:continue
        for pose in frames.values():
            p,q,s=pose[n].decompose();pose[n]=Matrix.LocRotScale(p,correction@q,s)
neutral=local['HarvestReaper_Idle'][0.0]
def ramp(name,a,b):
    frames=local[name];end=max(frames)
    local[name]={t:mix(a,b,ease(t/end)) for t in frames}
# Smooth isolated samples in strikes, without shifting phase endpoints or timing.
for stem in ['Cleave','Sweep','Slam','Special']:
    name='HarvestReaper_'+stem+'Attack';frames=local[name];times=sorted(frames)
    # Repair isolated quaternion flips before smoothing the surrounding strike.
    for n in order:
        spans=[]
        for i in range(1,len(times)):
            a=frames[times[i-1]][n].to_quaternion();b=frames[times[i]][n].to_quaternion()
            angle=math.degrees(a.rotation_difference(b).angle);angle=min(angle,360-angle)
            if angle>32:
                lo=max(0,i-5);hi=min(len(times)-1,i+5)
                if spans and lo<=spans[-1][1]:spans[-1][1]=hi
                else:spans.append([lo,hi])
        for lo,hi in spans:
            ap,aq,asc=frames[times[lo]][n].decompose();bp,bq,bsc=frames[times[hi]][n].decompose()
            for j in range(lo+1,hi):
                u=ease((j-lo)/(hi-lo))
                frames[times[j]][n]=Matrix.LocRotScale(ap.lerp(bp,u),aq.slerp(bq,u),asc.lerp(bsc,u))
    filtered={}
    for i,t in enumerate(times):
        if i<2 or i>=len(times)-2:filtered[t]=frames[t];continue
        avg=mix(frames[times[i-2]],frames[times[i+2]],.5)
        avg=mix(avg,mix(frames[times[i-1]],frames[times[i+1]],.5),.5)
        filtered[t]=mix(frames[t],avg,.5)
    local[name]=filtered
    ramp('HarvestReaper_'+stem+'Ready',neutral,filtered[min(times)])
    ramp('HarvestReaper_'+stem+'Recovery',filtered[max(times)],neutral)
# Summon with the free hand; the scythe arm stays in its idle carrying pose.
src=local['HarvestReaper_SummonAttack'];gesture=src[min(src,key=lambda t:abs(t-.55))]
gesture={n:m.copy() for n,m in gesture.items()}
for n in ['Root','Hips','RArm','RForearm','RHand','RSleeve','Scythe','LThigh','LShin','LFoot','RThigh','RShin','RFoot']:
    gesture[n]=neutral[n].copy()
ramp('HarvestReaper_SummonReady',neutral,gesture)
local['HarvestReaper_SummonAttack']={t:{n:m.copy() for n,m in gesture.items()} for t in src}
ramp('HarvestReaper_SummonRecovery',gesture,neutral)
# Replace the gathering spin with a direct reaching gesture.
ramp('HarvestReaper_GatherReady',neutral,gesture)
local['HarvestReaper_GatherAttack']={t:{n:m.copy() for n,m in gesture.items()} for t in local['HarvestReaper_GatherAttack']}
ramp('HarvestReaper_GatherRecovery',gesture,neutral)
for i in range(8):
    ready='HarvestReaper_WedgeReady'+str(i);attack='HarvestReaper_WedgeAttack'+str(i)
    hold='HarvestReaper_WedgeHold'+str(i);recovery='HarvestReaper_WedgeRecovery'+str(i)
    start=gesture if i==0 else local['HarvestReaper_WedgeHold'+str(i-1)][max(local['HarvestReaper_WedgeHold'+str(i-1)])]
    end=local[attack][min(local[attack])]
    ramp(ready,start,end)
    ramp(recovery,local[hold][max(local[hold])],neutral)
# Reconstruct connected world poses; no mesh or limb length changes are introduced.
for name,frames in local.items():
    for t,p in frames.items():
        world={}
        for n in order:world[n]=world[parents[n]]@p[n] if parents[n] else p[n]
        cache[name][t]=world
print('Refined facing, phase joins, summon recovery and gathering turn.')
