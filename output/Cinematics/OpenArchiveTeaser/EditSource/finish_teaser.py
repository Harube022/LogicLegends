"""Original cinematic score, project-owned SFX, captions and delivery encoding."""
from pathlib import Path
import json, subprocess, wave
import numpy as np
def butter(order, cutoff, fs, btype='lowpass', output=None):
    return order, cutoff, fs, btype

def sosfilt(spec, x):
    order, cutoff, fs, kind = spec
    freq=np.fft.rfftfreq(len(x),1/fs)
    if kind=='bandpass':
        lo,hi=cutoff
        response=(1-1/(1+(freq/max(1,lo))**(2*order)))/(1+(freq/hi)**(2*order))
    else:response=1/(1+(freq/cutoff)**(2*order))
    return np.fft.irfft(np.fft.rfft(x)*np.sqrt(response),n=len(x))

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
FF=Path(json.loads((HERE/'tools.json').read_text())['ffmpeg'])
SR=48000; DURATION=57; N=int(SR*DURATION)
rng=np.random.default_rng(190126)
score=np.zeros((N,2),np.float32); fx=np.zeros_like(score)

def add(dst,signal,start,gain=1,pan=0):
    a=int(start*SR);signal=np.asarray(signal,dtype=np.float32)
    if signal.ndim==1: signal=signal[:,None]*np.array([np.sqrt((1-pan)/2),np.sqrt((1+pan)/2)],np.float32)
    end=min(N,a+len(signal))
    if end>a:dst[max(0,a):end]+=signal[max(0,-a):end-a]*gain

def hz(note):return 440*2**((note-69)/12)
def synth(note,duration,kind='pad'):
    t=np.arange(int(SR*duration))/SR;f=hz(note)
    if kind=='pad':
        x=sum(np.sin(2*np.pi*(f*k*t+.002*k*np.sin(2*np.pi*(.21+k*.013)*t))+.11*k)/k**1.85 for k in range(1,9))
        env=np.minimum(1,t/.55)*np.minimum(1,(duration-t)/1.15)
        x*=env*.32
    elif kind=='bell':
        x=(np.sin(2*np.pi*f*t)+.32*np.sin(2*np.pi*f*2.003*t)+.12*np.sin(2*np.pi*f*3.98*t))*np.exp(-t*3.1)*np.minimum(1,t/.006)
    elif kind=='bass':
        x=(np.sin(2*np.pi*f*t)+.16*np.sin(2*np.pi*f*2*t)+.06*np.sin(2*np.pi*f*3*t))*np.exp(-t*2.4)*np.minimum(1,t/.015)*np.minimum(1,(duration-t)/.05)
    else:
        x=sum(np.sin(2*np.pi*f*k*t)/k**1.5 for k in range(1,7))*np.minimum(1,t/.08)*np.minimum(1,(duration-t)/.13)*np.exp(-t*.65)*.3
    return x.astype(np.float32)

def echo(sig,delay=.27,amount=.24):
    shift=int(SR*delay);out=np.zeros(len(sig)+shift*3,np.float32);out[:len(sig)]=sig
    for i in range(1,4):out[shift*i:shift*i+len(sig)]+=sig*amount**i
    return out

def whoosh(start,duration,gain=.12,down=False):
    t=np.arange(int(duration*SR))/SR;noise=rng.normal(0,1,len(t));noise=sosfilt(butter(2,[250,3800],fs=SR,btype='bandpass',output='sos'),noise)
    env=np.sin(np.pi*t/duration)**1.8
    phase=2*np.pi*(90*t+(220 if not down else -30)*t*t/duration)
    x=noise*env*.4+np.sin(phase)*env*.1
    add(fx,x,start,gain,-.2);add(fx,x,start+.025,gain*.7,.3)

def impact(start,gain=.22):
    t=np.arange(int(SR*1.8))/SR
    phase=2*np.pi*(45*t+70*.035*(1-np.exp(-t/.035)))
    x=np.sin(phase)*np.exp(-t*3.7)+rng.normal(0,1,len(t))*np.exp(-t*22)*.18
    add(fx,x,start,gain)

# D minor, B-flat, F and C: curiosity -> jeopardy -> a hopeful return.
chords=[[50,57,62,65,69],[46,53,58,62,65],[41,53,57,60,65],[48,55,60,64,67]]
for bar in range(21):
    start=bar*2.5
    chord=chords[bar%4]
    if 25<=start<36:chord=[38,45,50,53,56] if bar%2==0 else [39,46,51,55,58]
    if start>=42:chord=chords[(bar+1)%4]
    for k,note in enumerate(chord):add(score,synth(note,4,'pad'),start,.19 if start<25 else .23,(k-2)*.29)
    if start>=6 and start<25 or start>=42:
        for step in range(8):
            note=chord[2+step%3]+(12 if step%4==3 else 0)
            add(score,echo(synth(note,1.4,'bell')),start+step*.3125,.055 if start<15 else .075,(-1 if step%2 else 1)*.48)
    if start>=12:
        for beat in range(4):add(score,synth(chord[0]-12,.58,'bass'),start+beat*.625,.15)
    if start>=20 and start<25 or start>=36 and start<52:
        for step in range(8):add(score,synth(chord[step%3],.38,'strings'),start+step*.3125,.12,(-1 if step%2 else 1)*.2)
        for beat in [0,2]:
            t=np.arange(int(SR*.6))/SR
            drum=np.sin(2*np.pi*(64*t+55*.025*(1-np.exp(-t/.025))))*np.exp(-t*8)+sosfilt(butter(2,800,fs=SR,output='sos'),rng.normal(0,1,len(t)))*np.exp(-t*18)*.5
            add(score,drum,start+beat*.625,.14)

for i,note in enumerate([38,50,57,62,64,69]):add(score,synth(note,6.0,'pad'),51+i*.02,.27,(i-2.5)*.22)
for i,note in enumerate([74,77,81,86]):add(score,echo(synth(note,2.5,'bell'),.38,.32),52+i*.24,.10,(i-1.5)*.25)

def asset(filename,start,gain=.4,pan=0):
    files=list((ROOT/'Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE').rglob(filename))
    if not files:print('Missing optional SFX:',filename);return
    data=subprocess.check_output([str(FF),'-v','error','-i',str(files[0]),'-f','f32le','-ar',str(SR),'-ac','2','pipe:1'])
    x=np.frombuffer(data,np.float32).reshape(-1,2)
    add(fx,x,start,gain)

for start,dur,gain in [(0,2,.06),(5.7,1.8,.16),(10.7,.7,.08),(14.7,.9,.11),(19.6,1.6,.12),(27.1,1.1,.16),(31.4,.8,.2),(35.2,1,.18),(41.3,1.4,.17),(46.6,1.1,.12),(51.5,1.3,.17)]:whoosh(start,dur,gain)
for at,g in [(6.1,.18),(20,.13),(28,.28),(32,.28),(36,.2),(42,.16),(47,.17),(52,.24)]:impact(at,g)
asset('Archive_Cue_TopicStarted.wav',15.2,.3)
asset('Archive_Cue_TopicComplete.wav',23.5,.38)
asset('Archive_Primal_Growl.wav',25.3,.7)
asset('Archive_Primal_Roar.wav',28.9,.8)
asset('Archive_Proof_Roar.wav',33,.85)
asset('Archive_Night_Ominous_Wind_Loop.wav',31.5,.4)
for i in range(12):asset('Archive_Primal_Footstep.wav',36.2+i*.43,.12 if i%2 else .18)
asset('Archive_Item_Revival.wav',43.3,.23)
asset('Archive_Cue_TopicComplete.wav',46.8,.36)
music_envelope=np.minimum(1,np.arange(N)/SR/2)*np.minimum(1,(DURATION-np.arange(N)/SR)/2)
duck=np.ones(N,np.float32)
for at in [25.3,28.9,33]:
    tt=np.arange(N)/SR;duck*=1-.45*np.exp(-((tt-at-.35)/.65)**2)
mix=(score*duck[:,None]+fx)*music_envelope[:,None]
# Peak headroom and a soft, rare transient limiter. Final delivery also measures loudness.
mix=np.tanh(mix*.9);mix*=.86/max(.001,np.max(np.abs(mix)))
with wave.open(str(HERE/'Archive_Score_and_SFX.wav'),'wb') as f:
    f.setnchannels(2);f.setsampwidth(2);f.setframerate(SR);f.writeframes((mix*32767).astype('<i2').tobytes())

captions=[
(.9,5.6,'The Pillar of Logic stands restored.\\NBut the Open Archive remains fractured.'),
(6.5,10.7,'Lefyahj calls upon four chosen minds.'),
(11.4,14.7,'Together, reclaim the knowledge left behind.'),
(15.4,19.7,'Ten topics. Ten crystals to restore.'),
(20.5,24.7,'Every answer rebuilds it.\\NEvery teammate makes it whole.'),
(25.2,28.1,'Its guardians no longer know whom to protect.'),
(28.5,31.7,'THE PRIMAL AXIOM'),
(32.4,35.7,'THE PROOF DEVOURER'),
(36.4,41.7,'When night falls, face the hunt together.'),
(42.4,46.6,'Restore the Archive. Open the way home.'),
(47.3,51.7,'Leave no one behind.'),
]
def stamp(t):return f'{int(t//3600)}:{int(t//60)%60:02}:{int(t)%60:02}.{int(round(t%1*100)):02}'
ass='''[Script Info]
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080
WrapStyle: 0

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Story,Georgia,39,&H00EAF2F5,&H000000FF,&H00131518,&H90000000,0,0,0,0,100,100,0,0,1,1.2,1,2,120,120,55,1
Style: Brand,Georgia,32,&H008DD7EA,&H000000FF,&H00201B15,&HA0000000,0,0,0,0,100,100,5,0,1,1.2,1,5,80,80,0,1
Style: Title,Georgia,83,&H00EAF2F5,&H000000FF,&H00201B15,&HA0000000,-1,0,0,0,100,100,4,0,1,1.5,2,5,80,80,0,1
Style: Tagline,Georgia,29,&H00D5E8A2,&H000000FF,&H00201B15,&HA0000000,0,0,0,0,100,100,3,0,1,1,1,5,80,80,0,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
'''
for start,end,text in captions:ass+=f'Dialogue: 0,{stamp(start)},{stamp(end)},Story,,0,0,0,,{{\\fad(230,230)}}{text}\n'
ass+='Dialogue: 1,0:00:52.70,0:00:56.60,Brand,,0,0,0,,{\\pos(960,405)\\fad(700,650)}LOGIC LEGENDS\n'
ass+='Dialogue: 1,0:00:53.00,0:00:56.60,Title,,0,0,0,,{\\pos(960,525)\\fad(700,650)}THE OPEN ARCHIVE\n'
ass+='Dialogue: 1,0:00:53.30,0:00:56.60,Tagline,,0,0,0,,{\\pos(960,639)\\fad(700,650)}THINK TOGETHER. SURVIVE TOGETHER.\n'
(HERE/'story-captions.ass').write_text(ass,encoding='utf-8-sig')
srt=''
for i,(start,end,text) in enumerate(captions,1):
    def ts(x):return f'00:{int(x//60):02}:{int(x)%60:02},{int(round(x%1*1000)):03}'
    srt+=f'{i}\n{ts(start)} --> {ts(end)}\n{text.replace(chr(92)+"N",chr(10))}\n\n'
(HERE.parent/'LogicLegends_OpenArchive_Story.srt').write_text(srt,encoding='utf-8-sig')
(HERE/'audio-provenance.json').write_text(json.dumps({'music':'Original score synthesized for this teaser by finish_teaser.py; no voice-over','sfx':'Existing Open Archive project sound effects, plus original whooshes and impacts','duration':57,'sample_rate':SR,'peak_before_loudness':float(np.max(np.abs(mix)))},indent=2))
print('Original stereo score and captions ready.',flush=True)

if '--encode' in __import__('sys').argv:
    output=HERE.parent/'LogicLegends_OpenArchive_Story_1080p.mp4'
    filters="drawbox=x=0:y=0:w=iw:h=95:color=black:t=fill,drawbox=x=0:y=ih-95:w=iw:h=95:color=black:t=fill,ass=story-captions.ass"
    subprocess.run([str(FF),'-hide_banner','-y','-filter_threads','1','-i',str(HERE/'Archive_Unity_Footage.mp4'),'-i',str(HERE/'Archive_Score_and_SFX.wav'),'-vf',filters,'-af','highpass=f=32,loudnorm=I=-16:TP=-1.5:LRA=11','-c:v','libx264','-threads','3','-preset','slow','-crf','18','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-ar','48000','-t','57','-movflags','+faststart',str(output)],cwd=HERE,check=True)
    print(output,flush=True)
