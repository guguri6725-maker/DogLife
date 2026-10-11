"""Preserve VARCO originals, trim silence and balance winter SFX."""
import pathlib, wave, json, shutil
import numpy as np
src=pathlib.Path('C:/Users/kking/Desktop/VARCO_Sound/Winter')
dst=pathlib.Path('Assets/Resources/Audio/Winter');dst.mkdir(parents=True,exist_ok=True)
report=[]
for path in src.rglob('*.wav'):
    with wave.open(str(path),'rb') as w:
        rate,ch,width=w.getframerate(),w.getnchannels(),w.getsampwidth()
        assert width==2
        x=np.frombuffer(w.readframes(w.getnframes()),'<i2').astype(np.float32).reshape(-1,ch)/32768
    original=len(x)/rate;x=x.mean(axis=1);x-=x.mean()
    hop=max(1,int(rate*.005));n=len(x)//hop
    rms=np.sqrt(np.mean(x[:n*hop].reshape(n,hop)**2,axis=1))
    active=np.flatnonzero(rms>max(.0005,float(rms.max())*.055))
    start=max(0,int(active[0]*hop-rate*.005)) if len(active) else 0
    end=min(len(x),int((active[-1]+1)*hop+rate*.14)) if len(active) else len(x)
    x=x[start:end]
    limits={'Mammoth_Tusk_Uppercut':1.0,'Mammoth_Tusk_Frost':1.1,'IceWolf_Claw_Swing':.65,'IceWolf_Land':.8,'IceWolf_Leap':.6,'Golem_Slam':1.2,'Golem_IceSpike_Erupt':1.1,'Golem_IceWave_Travel':1.25,'Winter_Door_Open':2.4,'Winter_Door_Close':2.4}
    limit=.55 if 'Footstep' in path.stem else limits.get(path.stem,100)
    x=x[:int(rate*limit)]
    loop='Loop' in path.stem
    if loop:
        k=min(int(rate*.15),len(x)//5);f=np.linspace(0,1,k)
        x=np.concatenate((x[-k:]*(1-f)+x[:k]*f,x[k:-k]))
    else:
        k=min(int(rate*.003),len(x)//8);x[:k]*=np.linspace(0,1,k)
        k=min(int(rate*.065),len(x)//6);x[-k:]*=np.linspace(1,0,k)
    target=.045 if loop else .085
    x*=min(4,target/max(1e-8,float(np.sqrt(np.mean(x*x)))),.78/max(1e-8,float(abs(x).max())))
    with wave.open(str(dst/path.name),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes((x*32767).astype('<i2').tobytes())
    report.append(dict(name=path.stem,original=original,lead=start/rate,duration=len(x)/rate,peak=float(abs(x).max())))
shutil.copy2(src/'MapSound/Winter_Amb_Wind_Loop.mp3',dst/'Winter_Amb_Wind_Loop.mp3')
out=pathlib.Path('Builds/AudioValidation');out.mkdir(parents=True,exist_ok=True)
(out/'winter-edit-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps(report))
