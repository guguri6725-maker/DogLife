"""Keep source recordings intact; create game-ready mono effects and loop seams."""
from pathlib import Path
import wave, json, shutil
import numpy as np

src=Path('C:/Users/kking/Desktop/VARCO_Sound/Summer')
dst=Path('Assets/Resources/Audio/Summer'); dst.mkdir(parents=True,exist_ok=True)
report=[]
limits={'Tortoise_Footstep':.5,'Tortoise_Bite':.65,'Tortoise_Shell_Block':.65,
        'Crab_Bubble_Hit':.7,'Crab_Bubble_Expire':.4,'Crab_Debuff_End':.85}
names=set()
for p in sorted(src.rglob('*')):
    if p.suffix.lower() not in ('.wav','.mp3'): continue
    name=p.stem
    while name.lower().endswith('.wav'): name=name[:-4]
    assert name not in names, name
    names.add(name)
    if p.suffix.lower()=='.mp3':
        shutil.copy2(p,dst/(name+'.mp3'))
        report.append(dict(name=name,processing='original stereo ambience; runtime crossfade and effects gain'))
        continue
    with wave.open(str(p),'rb') as w:
        rate,ch,width=w.getframerate(),w.getnchannels(),w.getsampwidth()
        assert width==2,(p,width)
        x=np.frombuffer(w.readframes(w.getnframes()),'<i2').astype(np.float32).reshape(-1,ch)/32768
    original=len(x)/rate; x=x.mean(axis=1); x-=x.mean()
    hop=max(1,int(rate*.005)); n=len(x)//hop
    rms=np.sqrt(np.mean(x[:n*hop].reshape(n,hop)**2,axis=1))
    active=np.flatnonzero(rms>max(.0004,float(rms.max())*.04))
    assert len(active),p
    start=max(0,int(active[0]*hop-rate*.004)); end=min(len(x),int((active[-1]+1)*hop+rate*.16))
    x=x[start:end]; loop='Loop' in name
    x=x[:int(rate*limits.get(name,100))]
    if loop:
        k=min(int(rate*.15),len(x)//5); f=np.linspace(0,1,k)
        x=np.concatenate((x[-k:]*(1-f)+x[:k]*f,x[k:-k]))
    else:
        k=min(int(rate*.003),len(x)//8); x[:k]*=np.linspace(0,1,k)
        k=min(int(rate*.07),len(x)//6); x[-k:]*=np.linspace(1,0,k)
    gain=min(4,(.045 if loop else .085)/max(1e-8,float(np.sqrt(np.mean(x*x)))),.78/max(1e-8,float(abs(x).max())))
    x*=gain
    with wave.open(str(dst/(name+'.wav')),'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(rate); w.writeframes((x*32767).astype('<i2').tobytes())
    report.append(dict(name=name,source=str(p),original=original,trimmedLead=start/rate,duration=len(x)/rate,gain=gain,peak=float(abs(x).max())))
out=Path('Builds/AudioValidation');out.mkdir(parents=True,exist_ok=True)
(out/'summer-edit-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({'files':len(report),'waveFiles':sum('peak' in r for r in report),'maxTrimmedLead':max(r.get('trimmedLead',0) for r in report),'maxPeak':max(r.get('peak',0) for r in report)}))
