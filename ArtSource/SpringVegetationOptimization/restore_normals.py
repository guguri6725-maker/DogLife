import json,pathlib,numpy as np
root=pathlib.Path(__file__).resolve().parents[2]/'Temp/SpringVegetationOptimizationWork'
# 면 통합 과정에서 보간된 법선 대신 원본 위치의 법선을 사용합니다.
for path in (root/'Optimized').glob('*.json'):
 src=json.loads((root/'Original'/path.name).read_text(encoding='utf-8-sig'))
 out=json.loads(path.read_text(encoding='utf-8-sig'));sums={}
 for p,n in zip(src['vertices'],src['normals']):
  key=tuple(round(float(x),5) for x in p);sums[key]=sums.get(key,np.zeros(3))+np.array(n)
 for i,p in enumerate(out['vertices']):
  key=tuple(round(float(x),5) for x in p)
  n=sums.get(key,np.array(out['normals'][i]));length=np.linalg.norm(n)
  if length>1e-8:out['normals'][i]=(n/length).tolist()
 v=np.array(out['vertices']);ns=np.array(out['normals'])
 for slot,indices in enumerate(out['parts']):
  if not indices:continue
  t=np.array(indices).reshape(-1,3);cross=np.cross(v[t[:,1]]-v[t[:,0]],v[t[:,2]]-v[t[:,0]])
  wrong=(cross*ns[t].mean(axis=1)).sum(axis=1)<0
  t[wrong]=t[wrong][:,[0,2,1]];out['parts'][slot]=t.reshape(-1).tolist()
 path.write_text(json.dumps(out,separators=(',',':')),encoding='utf-8')
print('Source normals restored and triangle winding checked')

