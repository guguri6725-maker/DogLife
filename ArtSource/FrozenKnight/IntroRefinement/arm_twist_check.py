exec(open('C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/FrozenKnight/IntroRefinement/inspect.py').read().split('for m in d')[0])
for fn in ['before_arm_polish','output']:
 data=json.load(open('C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/FrozenKnight/IntroRefinement/'+fn+'.json'))
 for m in data['motions']:
  if not m['name'].startswith('ThroneDraw'):continue
  vals=[];angles=[]
  for f in range(m['frames']):
   p,r=fk(m,f);i=ids['RightForeArm'];parent=b[i]['parent'];rel=q(b[i]['q']).inverted()@(r[parent].inverted()@r[i]);twist=Quaternion((rel.w,0,rel.y,0)).normalized();a=math.degrees(twist.angle);vals.append(min(a,360-a))
   arm=p[ids['RightForeArm']]-p[ids['RightArm']];fore=p[ids['RightHand']]-p[ids['RightForeArm']];angles.append(math.degrees(arm.angle(fore)))
  print(fn,m['name'],'elbow_twist_max',max(vals),'bend_max',max(angles))
