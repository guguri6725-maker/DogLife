"""Non-destructive VARCO edit: remove pre-roll, balance attacks, soften cut tails."""
import pathlib, wave, json
import numpy as np

source=pathlib.Path('C:/Users/kking/Desktop/VARCO_Sound/Spring')
dest=pathlib.Path('Assets/Resources/Audio/Spring');dest.mkdir(parents=True,exist_ok=True)
report_dir=pathlib.Path('Builds/AudioValidation');report_dir.mkdir(parents=True,exist_ok=True)
limits={'Rabbit_Leap':.6,'Rabbit_Bite':.35,'Deer_Charge_Ready':1.25,'Deer_Charge':1.2,
 'Blossom_Charge':1.1,'Blossom_Launch':.6,'Blossom_Burst':.8,'Boar_Footstep':.42,
 'Boar_Scrape':.7,'Boar_Charge':1.5,'Boar_Tusk_Uppercut':.75,'Boar_Ground_Slam':1.5,
 'Boar_Wall_Impact':1.5,'Boar_Bone_Spike':1.1,'Boar_Root_Warning':1.2,'Boar_Root_Eruption':1.6,
 'Boar_Roar':2.6,'Boar_Phase_Transform':5.5,'Boar_Death':2.4,'Spring_Gate_Open':2.4,'Spring_Gate_Close':2.4}
report=[]
paths=list(sorted(source.rglob('*.wav')))
ambient=report_dir/'spring-ambience-decoded.wav'
if ambient.exists():paths.append(ambient)
for path in paths:
    with wave.open(str(path),'rb') as w:
        rate,channels,width=w.getframerate(),w.getnchannels(),w.getsampwidth();raw=w.readframes(w.getnframes())
    if width!=2:raise ValueError((path,width))
    x=np.frombuffer(raw,'<i2').astype(np.float32).reshape(-1,channels)/32768
    key='Spring_Ambience' if path==ambient else path.stem
    original=len(x)/rate;x-=x.mean(axis=0)
    if key!='Spring_Ambience':x=x.mean(axis=1,keepdims=True)
    hop=max(1,int(rate*.005));n=len(x)//hop
    energy=np.sqrt(np.mean(x[:n*hop].reshape(n,hop,-1)**2,axis=(1,2)))
    active=np.flatnonzero(energy>max(.0005,energy.max()*.055))
    start=max(0,int(active[0]*hop-rate*.004)) if len(active) else 0
    end=min(len(x),int((active[-1]+1)*hop+rate*.12)) if len(active) else len(x)
    x=x[start:end]
    if key=='Spring_Ambience':
        # Keep the complete soundscape; a circular overlap makes the wrap seamless.
        count=min(int(rate*2),len(x)//8);f=np.linspace(0,1,count)[:,None]
        x=np.concatenate((x[-count:]*(1-f)+x[:count]*f,x[count:-count]))
        rms_target=.038
    else:
        x=x[:int(rate*limits[key])]
        count=min(int(rate*.002),len(x)//8);x[:count]*=np.linspace(0,1,count)[:,None]
        count=min(int(rate*(.10 if key.startswith('Boar_') else .045)),len(x)//5);x[-count:]*=np.linspace(1,0,count)[:,None]
        rms_target=.12 if key.startswith('Boar_') else .095
    peak=float(np.max(np.abs(x)));rms=float(np.sqrt(np.mean(x*x)))
    gain=min(rms_target/max(rms,1e-8),.78/max(peak,1e-8),4);x*=gain
    with wave.open(str(dest/(key+'.wav')),'wb') as w:
        w.setnchannels(x.shape[1]);w.setsampwidth(2);w.setframerate(rate);w.writeframes((np.clip(x,-1,1)*32767).astype('<i2').tobytes())
    report.append(dict(clip=key,originalSeconds=round(original,3),removedLead=round(start/rate,3),seconds=round(len(x)/rate,3),peak=round(float(np.max(np.abs(x))),4),rms=round(float(np.sqrt(np.mean(x*x))),4)))
(report_dir/'spring-edit-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
