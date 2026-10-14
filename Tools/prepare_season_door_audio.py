"""Create pitch-preserving door cues; leave supplied recordings untouched."""
from pathlib import Path
import json
import wave
import numpy as np

def stretch(x, size, rate):
    # WSOLA: align overlapping waveforms rather than raising pitch by resampling.
    window=int(rate*.05);hop=window//2;search=int(rate*.015)
    out=np.zeros(size+window);weights=np.zeros_like(out)
    fade=np.hanning(window);previous=None
    for dest in range(0,size,hop):
        expected=int(dest*max(0,len(x)-window)/max(1,size-window))
        lo=max(0,expected-search);hi=min(len(x)-window,expected+search)
        if previous is None:chosen=0
        elif hi>=lo:
            target=previous[hop:];candidate=x[lo:hi+hop]
            corr=np.correlate(candidate,target,'valid')
            squares=np.concatenate(([0],np.cumsum(candidate*candidate)))
            energy=np.maximum(1e-12,squares[hop:]-squares[:-hop])
            chosen=lo+int(np.argmax(corr/np.sqrt(energy)))
        else:chosen=max(0,len(x)-window)
        frame=x[chosen:chosen+window];frame=np.pad(frame,(0,window-len(frame)))
        out[dest:dest+window]+=frame*fade;weights[dest:dest+window]+=fade
        previous=frame
    return (out/np.maximum(weights,1e-8))[:size]

if __name__ == '__main__':
    report=[]
    for season,folder,seconds in [('Summer','Summer',3.),('Autumn','Fall',3.2)]:
        for action in ['Open','Close']:
            name=f'{season}_Door_{action}';src=Path('C:/Users/kking/Downloads')/(name+'.wav')
            with wave.open(str(src),'rb') as w:
                assert w.getsampwidth()==2
                rate=w.getframerate();x=np.frombuffer(w.readframes(w.getnframes()),'<i2').astype(float).reshape(-1,w.getnchannels()).mean(axis=1)/32768
            original=len(x)/rate;x-=x.mean();hop=int(rate*.005)
            rms=np.sqrt(np.mean(x[:len(x)//hop*hop].reshape(-1,hop)**2,axis=1))
            active=np.flatnonzero(rms>max(.0005,rms.max()*.04))
            start=max(0,int(active[0]*hop-rate*.005));end=min(len(x),int((active[-1]+1)*hop+rate*.1))
            x=stretch(x[start:end],round(seconds*rate),rate)
            k=int(rate*.008);x[:k]*=np.linspace(0,1,k)
            k=int(rate*.035);x[-k:]*=np.linspace(1,0,k)
            gain=min(3,.085/max(1e-8,np.sqrt(np.mean(x*x))),.78/max(1e-8,abs(x).max()));x*=gain
            dest=Path('Assets/Resources/Audio')/folder/(name+'.wav');dest.parent.mkdir(parents=True,exist_ok=True)
            with wave.open(str(dest),'wb') as w:
                w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes((x*32767).astype('<i2').tobytes())
            report.append(dict(name=name,original=original,trimmedLead=start/rate,duration=seconds,gain=gain,peak=float(abs(x).max()),method='pitch-preserving WSOLA'))
    path=Path('Builds/AudioValidation/season-door-edit-report.json');path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report,indent=2))
