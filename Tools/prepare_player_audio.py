import pathlib, wave, json
import numpy as np

source = pathlib.Path('C:/Users/kking/Desktop/VARCO_Sound')
dest = pathlib.Path('Assets/Resources/Audio/Player')
dest.mkdir(parents=True, exist_ok=True)
mapping = {'가방 열기':'BagOpen','가방 닫기':'BagClose','구르기':'Roll','점프':'Jump','착지':'Land','발걸음':'Step','피격':'Hurt'}
weapons = {'검':'Sword','단검':'Dagger','창':'Spear','도끼':'Axe','활':'Bow','부메랑':'Boomerang'}
report=[]
for path in sorted(source.rglob('*.wav')):
    rel=path.relative_to(source); parts=rel.parts
    if parts[0]=='무기':
        prefix=weapons[parts[1]]; name=path.stem
        suffix='Hit' if name in ('명중음','타격음') else 'Attack' if name=='공격음' else 'Final' if '마지막' in name else 'Ground' if '지면' in name else 'Thrust' if '찌르기용' in name else 'Charge' if '차징' in name else 'Loop' if '반복' in name else 'Return'
        key=prefix+suffix
    else:
        key=mapping[parts[0]]
        if key in ('Step','Hurt'): key+=path.stem.rsplit('_',1)[-1]
    with wave.open(str(path),'rb') as w:
        rate=w.getframerate(); channels=w.getnchannels(); width=w.getsampwidth(); raw=w.readframes(w.getnframes())
    if width==2: x=np.frombuffer(raw,'<i2').astype(np.float64)/32768
    elif width==3:
        b=np.frombuffer(raw,np.uint8).reshape(-1,3); v=b[:,0].astype(np.int32)|(b[:,1].astype(np.int32)<<8)|(b[:,2].astype(np.int32)<<16); v=(v^8388608)-8388608; x=v/8388608.
    elif width==4: x=np.frombuffer(raw,'<i4').astype(np.float64)/2147483648
    else: raise ValueError((path,width))
    x=x.reshape(-1,channels).mean(axis=1); original=len(x)/rate
    x-=x.mean()
    window=max(1,int(rate*.01)); count=len(x)//window
    energy=np.sqrt(np.mean(x[:count*window].reshape(count,window)**2,axis=1))
    active=np.flatnonzero(energy>max(.0015,energy.max()*.04))
    if len(active): x=x[max(0,int(active[0]*window-rate*.015)):min(len(x),int((active[-1]+1)*window+rate*.04))]
    # Walking recordings contain multiple steps; retain a single short contact for each variant.
    limits={'BagOpen':.45,'BagClose':.4,'Roll':.45,'Jump':.3,'Land':.45,'SwordAttack':.4,'SwordFinal':.8,'DaggerAttack':.2,'DaggerFinal':.38,'AxeAttack':.65,'AxeGround':.85,'BoomerangAttack':.4,'BoomerangReturn':.3,'SpearAttack':.45,'SpearThrust':.13,'BowCharge':1.2}
    limit=.24 if key.startswith('Step') else .4 if key.startswith('Hurt') else .32 if key.endswith('Hit') else limits.get(key)
    if limit and len(x)>int(rate*limit):
        count=len(x)//window; energy=np.mean(x[:count*window].reshape(count,window)**2,axis=1)
        peak=int(np.argmax(energy))*window
        lead=.025 if key.startswith('Step') or key.endswith('Hit') or key.startswith('Hurt') else min(.12,limit*.25)
        start=max(0,peak-int(rate*lead));x=x[start:start+int(rate*limit)]
    # Keep the audible onset close to the event, even when the recording ends soon after it.
    # Charge and continuous flight deliberately retain their envelopes.
    if key not in ('BowCharge','BoomerangLoop'):
        win=max(1,int(rate*.005)); count=len(x)//win
        env=np.sqrt(np.mean(x[:count*win].reshape(count,win)**2,axis=1))
        audible=np.flatnonzero(env>env.max()*.25)
        lead=.04 if key=='SwordFinal' else .025 if key.endswith(('Attack','Final')) else .005
        if len(audible): x=x[max(0,int(audible[0]*win-rate*lead)):]
    if key.startswith('Step'): x=x[:int(rate*.17)]
    if key=='BoomerangLoop':
        n=min(int(rate*.08),len(x)//8)
        f=np.linspace(0,1,n); x[:n]=x[-n:]*(1-f)+x[:n]*f; x=x[:-n]
    else:
        n=min(int(rate*.005),len(x)//4); x[:n]*=np.linspace(0,1,n)
        n=min(int(rate*.035),len(x)//4); x[-n:]*=np.linspace(1,0,n)
    rms=np.sqrt(np.mean(x*x)); peak=max(abs(x).max(),1e-8)
    target=.07 if key.startswith('Step') else .13
    gain=min(target/max(rms,1e-8),.84/peak,16.0 if key.startswith('Step') else 3.0); x*=gain
    with wave.open(str(dest/(key+'.wav')),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes((np.clip(x,-1,1)*32767).astype('<i2').tobytes())
    report.append(dict(source=str(rel),clip=key,originalSeconds=round(original,3),editedSeconds=round(len(x)/rate,3),gain=round(gain,3),peak=round(float(abs(x).max()),3)))
pathlib.Path('Builds/AudioValidation').mkdir(parents=True,exist_ok=True)
pathlib.Path('Builds/AudioValidation/edit-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
