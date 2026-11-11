"""Original, seamless Open Archive score family. No external music or runtime synthesis."""
from pathlib import Path
import json, wave
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/LightingAudio/Music'
OUT.mkdir(parents=True,exist_ok=True)
SR=24000;BEAT=.625;BAR=BEAT*4;DURATION=BAR*32;N=int(SR*DURATION)
rng=np.random.default_rng(111126)

def hz(note):return 440*2**((note-69)/12)
def add(mix,signal,start,gain,pan=0):
    signal=np.asarray(signal,np.float32)
    if signal.ndim==1:signal=signal[:,None]*np.array([np.sqrt((1-pan)/2),np.sqrt((1+pan)/2)],np.float32)
    index=int(round(start*SR))%N
    size=min(len(signal),N-index);mix[index:index+size]+=signal[:size]*gain
    if size<len(signal):mix[:len(signal)-size]+=signal[size:]*gain

def pad(note,length,shade=0):
    t=np.arange(int(length*SR))/SR;f=hz(note)
    phase=f*t+.012*np.sin(2*np.pi*.23*t)
    x=sum(np.sin(2*np.pi*phase*k+.06*k)/k**(2.1-shade*.22) for k in range(1,8))
    env=np.minimum(1,t/.75)*np.minimum(1,(length-t)/1.25)
    return x*env*.4

def piano(note,length=2.6):
    t=np.arange(int(length*SR))/SR;f=hz(note)
    x=sum(np.sin(2*np.pi*f*k*(1+.0002*k)*t)*np.exp(-t*(1.5+k*.38))/k**1.9 for k in range(1,7))
    return x*np.minimum(1,t/.009)*np.minimum(1,(length-t)/.07)

def strings(note,length=.55):
    t=np.arange(int(length*SR))/SR;f=hz(note)
    x=sum(np.sin(2*np.pi*f*k*t+.02*k*np.sin(2*np.pi*4.7*t))/k**1.8 for k in range(1,8))
    env=np.minimum(1,t/.065)*np.minimum(1,(length-t)/.12)*np.exp(-t*1.3)
    return x*env*.45

def bass(note,length=.75):
    t=np.arange(int(length*SR))/SR;f=hz(note)
    return (np.sin(2*np.pi*f*t)+.12*np.sin(2*np.pi*f*2*t))*np.minimum(1,t/.03)*np.minimum(1,(length-t)/.10)*np.exp(-t*2.2)

def drum():
    t=np.arange(int(SR*.7))/SR
    body=np.sin(2*np.pi*(62*t+42*.025*(1-np.exp(-t/.025))))*np.exp(-t*8)
    noise=rng.normal(0,1,len(t));noise=np.convolve(noise,np.ones(18)/18,mode='same')
    return body+.28*noise*np.exp(-t*18)

def reverberant(mix,sig,at,gain,pan=0):
    add(mix,sig,at,gain,pan)
    for delay,level in [(.31,.20),(.67,.10),(1.13,.045)]:add(mix,sig,at+delay,gain*level,-pan)

# Shared 8-bar harmonic arc. New moods retain the same motif and tempo, so
# phase changes and the two-stream crossfader remain musically coherent.
chords=[[50,57,62,65,69],[46,53,58,62,65],[41,53,57,60,65],[48,55,60,64,67],
        [43,50,55,58,62],[46,53,58,62,65],[45,52,57,60,64],[45,52,57,61,64]]
melody=[74,77,76,69,72,74,77,81,79,77,74,72,69,72,73,69]
reports=[]
for mood,name in enumerate(['Daylight','Afternoon','Evening','Night','Hunt']):
    mix=np.zeros((N,2),np.float32)
    for bar in range(32):
        at=bar*BAR;chord=chords[bar%8]
        if mood>=3:chord=[x-12 if i<2 else x for i,x in enumerate(chord)]
        for i,note in enumerate(chord):
            weight=[.19,.21,.24,.27,.20][mood]
            reverberant(mix,pad(note,4.5,mood*.2),at,weight,(i-2)*.32)
        if mood<=2:
            for n in range(4):
                note=chord[2+n%3]+(12 if mood==0 and n==3 else 0)
                if mood==2:note-=12 if n%2 else 0
                reverberant(mix,piano(note),at+n*.625,[.11,.09,.055][mood],(-1 if n%2 else 1)*.45)
        # A short recognizable phrase appears only every other bar, leaving room
        # for the student's thought process and the game's informational sounds.
        if mood<=1 and bar%2==0:
            for n in range(4):
                note=melody[((bar%8)//2)*4+n]
                reverberant(mix,piano(note,2.4),at+n*.5+.18,.075 if mood==0 else .085,.15)
        if mood==1 or mood>=2:
            count=4 if mood<4 else 8
            for n in range(count):add(mix,bass(chord[0]-12 if mood<3 else chord[0]),at+n*BAR/count,.105 if mood<4 else .18)
        if mood==1:
            for n in [1,3]:add(mix,strings(chord[2+n%2],.6),at+n*.625,.06,(-1 if n==1 else 1)*.4)
        if mood==2:
            for n in [0,2,5]:reverberant(mix,piano(chord[2]+12,3),at+n*.3125,.045,(-1 if n%2 else 1)*.55)
        if mood==3:
            # Sparse low pulse and distant, restrained upper-register echoes.
            for n in [0,2.5]:add(mix,drum(),at+n*.625,.07)
            if bar%2==0:reverberant(mix,piano(chord[2]+12,3.5),at+.4,.045,.55)
        if mood==4:
            for n in range(8):add(mix,strings(chord[n%3]+12,.35),at+n*.3125,.18,(-1 if n%2 else 1)*.35)
            for n in [0,1.5,2,3.5]:add(mix,drum(),at+n*.625,.20 if n in [0,2] else .12)
            for n in range(16):
                t=np.arange(int(SR*.065))/SR;noise=rng.normal(0,1,len(t));noise-=np.convolve(noise,np.ones(8)/8,mode='same')
                add(mix,noise*np.exp(-t*70),at+n*.15625,.013,(-1 if n%2 else 1)*.6)
    mix=np.tanh(mix*.85)
    # Consistent perceived level across moods, without flattening their natural
    # phrasing. Circular note tails make the 80-second boundary seamless.
    rms=float(np.sqrt(np.mean(mix**2)));mix*=10**(-19/20)/max(rms,.00001)
    peak=float(np.max(np.abs(mix)))
    if peak>.76:mix*=.76/peak
    path=OUT/f'Archive_Score_{name}.wav'
    with wave.open(str(path),'wb') as f:
        f.setnchannels(2);f.setsampwidth(2);f.setframerate(SR);f.writeframes((mix*32767).astype('<i2').tobytes())
    reports.append(dict(mood=name,path=path.relative_to(ROOT).as_posix(),duration=DURATION,bpm=96,sample_rate=SR,channels=2,rms_db=float(20*np.log10(np.sqrt(np.mean(mix**2)))),peak_db=float(20*np.log10(np.max(np.abs(mix)))),boundary_delta=float(np.max(np.abs(mix[0]-mix[-1])))))
    print(name,'complete',flush=True)
Path(__file__).with_name('score-provenance.json').write_text(json.dumps(dict(authorship='Original procedural composition for Logic Legends. No external samples, voice-over or third-party songs.',loop_method='Circular wrapped instrument/reverb tails; shared tempo and harmony across all moods.',tracks=reports),indent=2))
