exec(open('C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/FrozenKnight/IntroRefinement/inspect.py').read().split('for m in d')[0])
d=json.load(open('C:/Users/mbc/Desktop/NC_AI/DogLife/ArtSource/FrozenKnight/IntroRefinement/output.json'))
for m in d['motions']:
 rows=[]
 for f in range(1,m['frames']):
  for i,bone in enumerate(b):
   a=q(m['rotations'][(f-1)*n+i]);c=q(m['rotations'][f*n+i]);ang=math.degrees(a.rotation_difference(c).angle);ang=min(ang,360-ang)
   rows.append((round(ang,2),round(f/60,3),bone['name']))
 print(m['name'],sorted(rows,reverse=True)[:8])
