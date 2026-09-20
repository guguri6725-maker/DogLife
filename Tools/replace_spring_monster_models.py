"""Build replacement skins against the established animation hierarchies.
Outputs are staged for visual review before Unity assets are replaced.
"""
import bpy, pathlib, math, json, heapq, sys
from mathutils import Vector, Quaternion
ROOT=pathlib.Path.cwd();OUT=ROOT/'Temp/MonsterReplacement'
sources={n:'ArtSources/'+n+'/AnimationTemplate.blend' for n in ['BlossomSkeleton','BlossomSpirit','FloralDeer','SakuraRabbit']}
def clamp(x):return max(0,min(1,x))
def segment(p,a,b):
 d=b-a;return (p-a-d*clamp((p-a).dot(d)/max(d.length_squared,1e-8))).length
reports={}
for name,source in sources.items():
 if '--' in sys.argv and name not in sys.argv[sys.argv.index('--')+1:]:continue
 bpy.ops.wm.open_mainfile(filepath=str(ROOT/source))
 rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 meshname='texture_mapping'
 for o in list(bpy.data.objects):
  if o!=rig:bpy.data.objects.remove(o,do_unlink=True)
 # Remove old image/material data so the new packed textures keep their canonical names.
 for mat in list(bpy.data.materials):bpy.data.materials.remove(mat)
 for image in list(bpy.data.images):bpy.data.images.remove(image)
 with bpy.data.libraries.load(str(ROOT/'ArtSources'/name/'Source.blend'),link=False) as (src,dst):dst.objects=[n for n in src.objects if n=='texture_mapping']
 obj=dst.objects[0];bpy.context.collection.objects.link(obj);obj.name=meshname
 for v in obj.data.vertices:v.co=obj.matrix_world@v.co
 obj.matrix_world.identity()
 floor=min(v.co.z for v in obj.data.vertices)
 for v in obj.data.vertices:v.co.z-=floor
 heads={};ends={}
 if name in ('BlossomSkeleton','BlossomSpirit'):
  spirit=name=='BlossomSpirit'
  heads.update(Root=(0,0,.5),Hips=(0,.03,.51 if spirit else .46),Spine=(0,.03,.55),Spine1=(0,.025,.60),Spine2=(0,.02,.66),Neck=(0,0,.75),Head=(0,-.015,.80))
  for side,s in [('Left',1),('Right',-1)]:
   raised=spirit and s==-1
   heads.update({side+'Shoulder':(s*.05,.015,.71),side+'Arm':(s*(.10 if spirit else .145),.01,.70),side+'ForeArm':(s*(.175 if spirit else .195),-.01,.59 if spirit else .55),side+'Hand':(s*(.22 if spirit else .225),-.075,.69 if raised else (.43 if spirit else .37)),side+'UpLeg':(s*.065,.03,.51 if spirit else .46),side+'Leg':(s*.09,.015,.24),side+'Foot':(s*.105,.015,.065),side+'ToeBase':(s*.11,-.09,.025)})
   ends[side+'Hand']=(s*.255,-.10,.73 if raised else (.365 if spirit else .31))
  ends.update(Head=(0,-.015,.95),Hips=(0,.03,.53),Spine=(0,.025,.59),Spine1=(0,.02,.66),Spine2=(0,0,.75),Neck=(0,-.015,.80))
  if not spirit:
   heads.update(RightArm=(-.15,.065,.69),RightForeArm=(-.205,.105,.55),RightHand=(-.255,.055,.40),LeftForeArm=(.18,-.025,.56),LeftHand=(.195,-.105,.39))
   ends.update(RightHand=(-.26,-.02,.34),LeftHand=(.195,-.13,.32))
 elif name=='FloralDeer':
  heads.update(Root=(0,0,0),Spine=(0,.10,.46),Neck=(0,-.17,.52),Head=(0,-.19,.66),Tail=(0,.23,.48))
  ends.update(Spine=(0,-.16,.50),Neck=(0,-.19,.66),Head=(.15,-.19,.69),Tail=(0,.34,.41))
  for side,s in [('L',1),('R',-1)]:
   for front in (True,False):
    prefix=('Front' if front else 'Hind')+side;y=-.18 if front else .19
    for joint,z,dy in [('Upper',.46,0),('Lower',.32,.02),('Cannon',.19,0),('Hoof',.04,-.02)]:heads[prefix+joint]=(s*.083,y+dy,z)
    ends[prefix+'Hoof']=(s*.083,y-.045,.005)
 else:
  heads.update(Root=(0,0,0),Body=(0,.10,.29),Head=(0,-.20,.43),Tail=(0,.28,.35),EarL=(.065,-.19,.57),EarR=(-.065,-.19,.57))
  ends.update(Body=(0,-.17,.35),Head=(0,-.34,.45),Tail=(0,.43,.51),EarL=(.24,-.12,.98),EarR=(-.24,-.12,.98))
  for side,s in [('L',1),('R',-1)]:
   heads.update({'Front'+side+'Upper':(s*.10,-.17,.28),'Front'+side+'Foot':(s*.11,-.20,.075),'Hind'+side+'Upper':(s*.13,.15,.30),'Hind'+side+'Foot':(s*.15,.19,.07)})
   ends['Front'+side+'Foot']=(s*.11,-.28,.015);ends['Hind'+side+'Foot']=(s*.15,.09,.015)
 # Preserve bone axes used by the authored clips; place pivots on the new anatomy.
 rig.animation_data.action=None
 for b in rig.pose.bones:b.rotation_mode='QUATERNION';b.rotation_quaternion=Quaternion();b.location=(0,0,0);b.scale=(1,1,1)
 bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 for b in rig.data.edit_bones:b.use_connect=False
 for b in rig.data.edit_bones:
  if b.name in heads:
   delta=Vector(heads[b.name])-b.head;b.head+=delta;b.tail+=delta
 bpy.ops.object.mode_set(mode='OBJECT')
 for b in rig.data.bones:
  if b.name not in heads:heads[b.name]=tuple(b.head_local)
  if b.name not in ends:
   child=next((c for c in b.children if c.name in heads),None)
   ends[b.name]=heads[child.name] if child else tuple(b.tail_local)
 for group in list(obj.vertex_groups):obj.vertex_groups.remove(group)
 for n in heads:obj.vertex_groups.new(name=n)
 def weights(v):
  x,y,z=v.co;side='Left' if x>=0 else 'Right'
  if name in ('BlossomSkeleton','BlossomSpirit'):
   if z>.79:return {'Head':1}
   if name=='BlossomSkeleton' and x<-.14 and y<-.09 and z<.49:return {'RightHand':1}
   if z<.42 and (abs(x)<.18 or z<.2):candidates=[side+n for n in ['UpLeg','Leg','Foot','ToeBase']]
   else:
    candidates=['Hips','Spine','Spine1','Spine2','Neck']+[side+n for n in ['Shoulder','Arm','ForeArm','Hand']]
    if y>.07:candidates=['Hips','Spine','Spine1','Spine2'] # cape stays on torso
    if name=='BlossomSpirit' and z<.56 and (abs(x)<.16 or y>-.015):candidates=['Hips']
  elif name=='FloralDeer':
   if z>.68:return {'Head':1}
   if z>.53 and y<-.09:candidates=['Neck','Head']
   elif y>.26 and z>.35:return {'Tail':1}
   elif z<.40:candidates=[('Front' if y<0 else 'Hind')+('L' if x>=0 else 'R')+n for n in ['Upper','Lower','Cannon','Hoof']]+['Spine']
   else:candidates=['Spine','Neck']
  else:
   if z>.57 and y<.07:return {'Ear'+('L' if x>=0 else 'R'):clamp((z-.57)/.07),'Head':1-clamp((z-.57)/.07)}
   if y<-.16 and z>.34:candidates=['Head','Body']
   elif y>.27 and z>.29:return {'Tail':1}
   elif z<.29:candidates=[('Front' if y<0 else 'Hind')+('L' if x>=0 else 'R')+n for n in ['Upper','Foot']]+['Body']
   else:candidates=['Body','Head']
  nearest=sorted([(segment(v.co,Vector(heads[n]),Vector(ends[n])),n) for n in candidates])[:2]
  scores=[1/max(.005,d)**5 for d,n in nearest];total=sum(scores)
  return {n:w/total for (d,n),w in zip(nearest,scores)}
 for v in obj.data.vertices:
  for n,w in weights(v).items():
   if w>1e-5:obj.vertex_groups[n].add([v.index],w,'REPLACE')
 # Smooth across welded surface neighbours, including UV seams, to avoid hard weight borders.
 neighbours=[set() for v in obj.data.vertices];weld={}
 for v in obj.data.vertices:
  key=tuple(round(c,5) for c in v.co);weld.setdefault(key,[]).append(v.index)
 for e in obj.data.edges:
  a,b=e.vertices;neighbours[a].add(b);neighbours[b].add(a)
 for ids in weld.values():
  for i in ids:neighbours[i].update(ids)
 values=[{g.group:g.weight for g in v.groups} for v in obj.data.vertices]
 for iteration in range(16):
  updated=[]
  for i,ws in enumerate(values):
   adjacent=neighbours[i]
   if not adjacent:updated.append(ws);continue
   mixed={g:w*.5 for g,w in ws.items()}
   for j in adjacent:
    for g,w in values[j].items():mixed[g]=mixed.get(g,0)+w*.5/len(adjacent)
   mixed=dict(sorted(mixed.items(),key=lambda p:p[1],reverse=True)[:4]);total=sum(mixed.values());updated.append({g:w/total for g,w in mixed.items()})
  values=updated
 for group in obj.vertex_groups:group.remove(list(range(len(obj.data.vertices))))
 for i,ws in enumerate(values):
  for g,w in ws.items():obj.vertex_groups[g].add([i],w,'REPLACE')
 if name in ('BlossomSkeleton','BlossomSpirit'):
  # Surface-distance binding prevents nearby hanging cloth from following the hands.
  points=[v.co.copy() for v in obj.data.vertices]
  graph=[[(j,max(.00001,(points[i]-points[j]).length)) for j in adjacent if j!=i] for i,adjacent in enumerate(neighbours)]
  distances={}
  for bone in heads:
   if bone=='Root':continue
   a=Vector(heads[bone]);b=Vector(ends[bone]);start=a.lerp(b,.18);end=a.lerp(b,.82)
   nearest=sorted((segment(p,start,end),i) for i,p in enumerate(points))[:8]
   dist=[float('inf')]*len(points);queue=[]
   for d,i in nearest:dist[i]=d;heapq.heappush(queue,(d,i))
   while queue:
    d,i=heapq.heappop(queue)
    if d!=dist[i]:continue
    for j,length in graph[i]:
     nd=d+length
     if nd<dist[j]:dist[j]=nd;heapq.heappush(queue,(nd,j))
   distances[bone]=dist
  for group in obj.vertex_groups:group.remove(list(range(len(points))))
  for i,p in enumerate(points):
   nearest=sorted((ds[i],n) for n,ds in distances.items())[:3]
   if not math.isfinite(nearest[0][0]):ws=weights(obj.data.vertices[i])
   else:
    # Smooth only at joints; distant surfaces retain rigid decoration silhouettes.
    scores=[math.exp(-(d-nearest[0][0])/.018) for d,n in nearest];total=sum(scores)
    ws={n:w/total for (d,n),w in zip(nearest,scores)}
   for n,w in ws.items():obj.vertex_groups[n].add([i],w,'REPLACE')
 if name in ('BlossomSkeleton','BlossomSpirit'):
  image=next(i for i in bpy.data.images if i.name.startswith('base_color'));pixels=list(image.pixels);w,h=image.size;pink=set()
  for loop in obj.data.loops:
   uv=obj.data.uv_layers.active.data[loop.index].uv;offset=(min(h-1,max(0,int(uv.y*h)))*w+min(w-1,max(0,int(uv.x*w))))*4
   r,g,b=pixels[offset:offset+3]
   if r>g*1.12 and b>g*1.025 and r>.25:pink.add(loop.vertex_index)
  for v in obj.data.vertices:
   x,y,z=v.co;ws=None;side='Left' if x>=0 else 'Right'
   if name=='BlossomSkeleton':
    if (x<-.24 and .30<z<.43) or (x<-.20 and y<-.12 and .15<z<.40):ws={'RightHand':1}
    elif .42<z<.77 and x>.12 and y>.075:ws={'Spine2':1}
    elif .31<z<.50 and abs(x)<.17 and v.index in pink:ws={'Hips':1}
    elif abs(x)>.14 and (x<0 or y<.065) and .45<z<.68:
     candidates=[side+n for n in ['Arm','ForeArm','Hand']]
     nearest=sorted((segment(v.co,Vector(heads[n]),Vector(ends[n])),n) for n in candidates)[:2];scores=[1/max(.003,d)**6 for d,n in nearest];ws={n:s/sum(scores) for (d,n),s in zip(nearest,scores)}
   elif .30<z<.60 and v.index in pink:ws={'Hips':1}
   if ws:
    for g in obj.vertex_groups:g.remove([v.index])
    for n,value in ws.items():obj.vertex_groups[n].add([v.index],value,'REPLACE')
 obj.parent=rig;mod=obj.modifiers.new('ReplacementSkin','ARMATURE');mod.object=rig
 # Blend corrections across the actual surface, including duplicated UV seam vertices.
 if name in ('BlossomSkeleton','BlossomSpirit'):
  values=[{g.group:g.weight for g in v.groups} for v in obj.data.vertices]
  for iteration in range(8):
   updated=[]
   for i,ws in enumerate(values):
    adjacent=neighbours[i]
    if not adjacent:updated.append(ws);continue
    mixed={g:w*.25 for g,w in ws.items()}
    for j in adjacent:
     for g,w in values[j].items():mixed[g]=mixed.get(g,0)+w*.75/len(adjacent)
    mixed=dict(sorted(mixed.items(),key=lambda p:p[1],reverse=True)[:4]);total=sum(mixed.values());updated.append({g:w/total for g,w in mixed.items()})
   values=updated
  for g in obj.vertex_groups:g.remove(list(range(len(obj.data.vertices))))
  for i,ws in enumerate(values):
   for g,w in ws.items():obj.vertex_groups[g].add([i],w,'REPLACE')
 # Re-solve the authored hand/foot targets on the new limb lengths.
 if name in ('BlossomSkeleton','BlossomSpirit'):
  for a in list(bpy.data.actions):bpy.data.actions.remove(a)
  if name=='BlossomSkeleton':
   sword=[v.index for v in obj.data.vertices if v.co.x<-.24 and v.co.y<-.12 and .15<v.co.z<.40]
   rest_wrist=rig.data.bones['RightHand'].head_local.copy()
   tips=sorted(sword,key=lambda i:(obj.data.vertices[i].co-rest_wrist).length,reverse=True)[:8]
   sword_tip=sum((obj.data.vertices[i].co for i in tips),Vector())/len(tips)
   sword_axis=(sword_tip-rest_wrist).normalized();sword_length=(sword_tip-rest_wrist).length
   code=(ROOT/'Tools/monster_animation_templates/BlossomSkeleton.py').read_text(encoding='utf-8')
   # The template contains only animation authoring code.
   code=code.replace('y=18','y=0').replace('y=-15','y=0').replace("('Left',0,18),('Right',math.pi,-15)","('Left',0,0),('Right',math.pi,0)").replace("('Left',18),('Right',-15)","('Left',0),('Right',0)").replace('z=-35','z=0')
   code=code.replace('x=24*s','x=34*s').replace('knee=24*','knee=38*').replace('13 if side', '24 if side').replace('else 7','else 14').replace('1.5*math.sin','3*math.sin').replace('14*lift+22*strike','20*lift+30*strike').replace('-10+30*strike','-14+40*strike')
  else:
   code=(ROOT/'Tools/monster_animation_templates/BlossomSpirit.py').read_text(encoding='utf-8')
   code=code.replace('x=19*s','x=29*s').replace('x=23*','x=34*').replace('x=-10*s','x=-20*s').replace('1.2*math.sin','3*math.sin').replace('x=-12*wave','x=-20*wave').replace('x=32*wave','x=44*wave')
  exec(code,dict(globals(),arm=rig.data))
 else:
  for a in list(bpy.data.actions):bpy.data.actions.remove(a)
  code=(ROOT/'Tools/monster_animation_templates'/(name+'.py')).read_text(encoding='utf-8')
  if name=='FloralDeer':
   code=code.replace('gait(phase,21)','gait(phase,32)').replace('x=-38*scrape','x=-52*scrape').replace('x=52*scrape','x=68*scrape').replace('x=-20*scrape','x=-30*scrape').replace('z=4*','z=7*')
  else:
   code=code.replace('1-.22*amount','1-.30*amount').replace('hop=.025*','hop=.045*').replace('x=23*math.sin','x=34*math.sin').replace('x=-26*math.sin','x=-38*math.sin').replace('x=-14*math.sin','x=-24*math.sin').replace('x=-24*lift','x=-36*lift').replace('x=24*lift','x=36*lift').replace('x=-28*lift','x=-40*lift').replace('x=2*math.sin','x=6*math.sin').replace('x=-2*math.sin','x=-6*math.sin')
  exec(code,dict(globals(),arm=rig.data))
 folder=OUT/name;folder.mkdir(exist_ok=True)
 for image in bpy.data.images:
  if image.packed_file:
   image.filepath_raw=str(folder/(image.name.split('.')[0]+'.png'));image.file_format='PNG';image.save()
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.context.scene.render.fps=60
 bpy.ops.wm.save_as_mainfile(filepath=str(folder/(name+'_Rigged.blend')))
 bpy.ops.export_scene.fbx(filepath=str(folder/(name+'.fbx')),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='STRIP')
 reports[name]={'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'bones':len(rig.data.bones),'unweighted':sum(not v.groups for v in obj.data.vertices)}
(OUT/'replacement-report.json').write_text(json.dumps(reports,indent=2))
