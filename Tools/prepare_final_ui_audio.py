from pathlib import Path
import wave,json,shutil
import numpy as np
from prepare_season_door_audio import stretch

report=[]
for name,folder,seconds in [('Memory_Object_Loop','UI',None),('Player_Death','Player',None),('Death_Result_Show','UI',1.4),('Memory_Page_Turn','UI',.45)]:
    p=Path('C:/Users/kking/Downloads')/(name+'.wav')
    with wave.open(str(p),'rb') as w:
        assert w.getsampwidth()==2
        rate=w.getframerate();x=np.frombuffer(w.readframes(w.getnframes()),'<i2').astype(float).reshape(-1,w.getnchannels()).mean(1)/32768
    original=len(x)/rate;x-=x.mean();hop=int(rate*.005)
    rms=np.sqrt(np.mean(x[:len(x)//hop*hop].reshape(-1,hop)**2,axis=1));active=np.flatnonzero(rms>max(.0004,rms.max()*.04))
    start=max(0,int(active[0]*hop-rate*.004));end=min(len(x),int((active[-1]+1)*hop+rate*.1));x=x[start:end]
    loop='Loop' in name
    if loop:
        k=min(int(rate*.15),len(x)//5);f=np.linspace(0,1,k);x=np.concatenate((x[-k:]*(1-f)+x[:k]*f,x[k:-k]))
    else:
        if seconds:x=stretch(x,round(seconds*rate),rate)
        k=int(rate*.003);x[:k]*=np.linspace(0,1,k);k=min(int(rate*.05),len(x)//6);x[-k:]*=np.linspace(1,0,k)
    target=.04 if loop else .065 if name=='Memory_Page_Turn' else .085
    gain=min(4,target/max(1e-8,np.sqrt(np.mean(x*x))),.78/max(1e-8,abs(x).max()));x*=gain
    dest=Path('Assets/Resources/Audio')/folder/(name+'.wav');dest.parent.mkdir(parents=True,exist_ok=True)
    with wave.open(str(dest),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes((x*32767).astype('<i2').tobytes())
    report.append(dict(name=name,original=original,leadTrim=start/rate,duration=len(x)/rate,peak=float(abs(x).max()),gain=gain))
shutil.copy2('C:/Users/kking/Downloads/Title_BGM.mp3','Assets/Resources/Audio/Music/Title_BGM.mp3')
Path('Builds/AudioValidation/final-ui-edit-report.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report,indent=2))
