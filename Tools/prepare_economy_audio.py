import pathlib, wave, json
import numpy as np

source=pathlib.Path('C:/Users/kking/Desktop/VARCO_Sound/UI')
dest=pathlib.Path('Assets/Resources/Audio/UI'); dest.mkdir(parents=True,exist_ok=True)
limits={'UI_Button_Click':.22,'UI_Action_Denied':.45,'UI_Window_Open':.45,'UI_Window_Close':.4,'Shop_Item_Select':.25,'Shop_Purchase':.8,'Shop_Sell':.65,'Synthesis_Material_Add':.4,'Synthesis_Material_Remove':.35,'Synthesis_Start':.65,'Synthesis_Result':1.2,'Synthesis_Rare_Result':1.7}
report=[]
for path in sorted(source.glob('*.wav')):
    with wave.open(str(path),'rb') as w:
        rate,channels,width=w.getframerate(),w.getnchannels(),w.getsampwidth();raw=w.readframes(w.getnframes())
    if width==2:x=np.frombuffer(raw,'<i2').astype(float)/32768
    elif width==3:
        b=np.frombuffer(raw,np.uint8).reshape(-1,3);v=b[:,0].astype(np.int32)|(b[:,1].astype(np.int32)<<8)|(b[:,2].astype(np.int32)<<16);x=((v^8388608)-8388608)/8388608.
    elif width==4:x=np.frombuffer(raw,'<i4').astype(float)/2147483648
    else:raise ValueError(width)
    x=x.reshape(-1,channels);x-=x.mean(axis=0);original=len(x)/rate
    win=max(1,int(rate*.005));n=len(x)//win
    env=np.sqrt(np.mean(x[:n*win].reshape(n,win,channels)**2,axis=(1,2)))
    active=np.flatnonzero(env>max(.0008,env.max()*.07));start=max(0,int(active[0]*win-rate*.005));end=min(len(x),int((active[-1]+1)*win+rate*.06))
    x=x[start:end];loop=path.stem=='Synthesis_Loop'
    if loop:
        # Circular overlap-add: both sides of the wrap follow the same continuous waveform.
        n=min(int(rate*.09),len(x)//6);f=np.linspace(0,1,n)[:,None];cross=x[-n:]*(1-f)+x[:n]*f;x=np.concatenate((cross,x[n:-n]))
    else:
        x=x[:int(rate*limits[path.stem])]
        n=min(int(rate*.002),len(x)//8);x[:n]*=np.linspace(0,1,n)[:,None]
        n=min(int(rate*.05),len(x)//5);x[-n:]*=np.linspace(1,0,n)[:,None]
    rms=np.sqrt(np.mean(x*x));peak=np.max(np.abs(x));gain=min((.085 if loop else .12)/max(rms,1e-9),.8/max(peak,1e-9),5)
    x*=gain
    with wave.open(str(dest/path.name),'wb') as w:
        w.setnchannels(channels);w.setsampwidth(2);w.setframerate(rate);w.writeframes((np.clip(x,-1,1)*32767).astype('<i2').tobytes())
    report.append(dict(clip=path.stem,originalSeconds=round(original,3),trimmedStartSeconds=round(start/rate,3),editedSeconds=round(len(x)/rate,3),peak=round(float(np.max(np.abs(x))),3),loop=loop))
out=pathlib.Path('Builds/AudioValidation');out.mkdir(parents=True,exist_ok=True)
(out/'economy-edit-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
