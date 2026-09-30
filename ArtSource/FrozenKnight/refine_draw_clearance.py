"""발도 중 칼날이 몸통을 가로지르지 않도록 손목 방향만 보정합니다."""
import bpy,json,math
from mathutils import Vector,Quaternion
ROOT=r'C:/Users/mbc/Desktop/NC_AI/DogLife'
s=open(ROOT+'/ArtSource/FrozenKnight/author_cinematics.py',encoding='utf-8').read()
exec(compile(s[:s.index('# 기존 탑승 동작')],'draw_source','exec'))
hand=ids['RightHand']
original=json.load(open(ROOT+'/Temp/FrozenKnight/blender_input.json',encoding='utf-8'))
axis=(v(original['tip'])-v(original['grip'])).normalized()
for m in out:
 if m['name'] not in ('ThroneDraw1','ThroneDraw2'):continue
 for f in range(m['frames']):
  t=f/(m['frames']-1)
  u=max(0,min(1,(t-.45)/.3)) if m['name']=='ThroneDraw1' else 1-max(0,min(1,(t-.12)/.42))
  weight=u*u*(3-2*u)
  ps,qs=pose(m,f);wp,wq=fk(ps,qs)
  current=(wq[hand]@axis).normalized()
  # 보스 오른쪽 바깥으로 칼끝을 빼고, 머리 위로 올라가면 원본 동작에 복귀합니다.
  desired=Vector((-.72,-.68,-.14)).normalized()
  turn=current.rotation_difference(desired)
  angle=turn.angle
  if angle>math.radians(55):turn=Quaternion().slerp(turn,math.radians(55)/angle)
  corrected=Quaternion().slerp(turn,weight)@wq[hand]
  qs[hand]=wq[bones[hand]['parent']].inverted()@corrected
  write(m,f,ps,qs)
data['motions']=[m for m in out if m['name'] in ('ThroneDraw1','ThroneDraw2')]
for key in ('skin','sword','swordTriangles'):data.pop(key,None)
json.dump(data,open(ROOT+'/Temp/FrozenKnight/cinematic_output.json','w',encoding='utf-8'))
print('Draw wrist clearance corrected')
