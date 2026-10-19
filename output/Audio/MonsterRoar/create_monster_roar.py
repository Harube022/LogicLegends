"""Local, non-destructive 3-second creature sound design using supplied clips.
Run with Blender's bundled Python for aud decode/MP3 encode, numpy for DSP.
"""
import aud, numpy as np, json, math, wave
from pathlib import Path

ROOT=Path(__file__).resolve().parent
SR=44100;DURATION=3.;N=int(SR*DURATION);rng=np.random.default_rng(191026)
data=np.load(ROOT/'decoded_sources.npz')

def mono_resample(a,old_sr):
    x=a.mean(axis=1).astype(np.float64)
    if old_sr!=SR:x=np.interp(np.arange(round(len(x)*SR/old_sr))*old_sr/SR,np.arange(len(x)),x)
    return x
def fit(x,n):return np.pad(x[:n],(0,max(0,n-len(x))))
def fades(x,attack=.015,release=.12):
    x=x.copy();a=min(int(attack*SR),len(x));r=min(int(release*SR),len(x))
    if a:x[:a]*=np.sin(np.linspace(0,np.pi/2,a))**2
    if r:x[-r:]*=np.cos(np.linspace(0,np.pi/2,r))**2
    return x
def rms(x):return float(np.sqrt(np.mean(x*x)+1e-15))
def normalize(x,level=.18):return x*(level/max(rms(x),1e-8))
def eq(x,hp=30,lp=8000,bass=0):
    # Zero-phase smooth frequency response with generous padding avoids wraparound.
    pad=4096;v=np.pad(x,(pad,pad));f=np.fft.rfftfreq(len(v),1/SR)
    h=(1-np.exp(-(f/max(hp,1))**4))*np.exp(-(f/lp)**6)
    if bass:h*=10**((bass*np.exp(-.5*(np.log2((f+1)/125)/.85)**2))/20)
    return np.fft.irfft(np.fft.rfft(v)*h,len(v))[pad:pad+len(x)]
def stretch(x,rate):
    # Phase-vocoder time compression lets the roar retain a lowered pitch.
    fft=2048;hop=256;win=np.hanning(fft);v=np.pad(x,(fft//2,fft//2+fft))
    frames=np.lib.stride_tricks.sliding_window_view(v,fft)[::hop]
    spec=np.fft.rfft(frames*win,axis=1).T
    times=np.arange(0,spec.shape[1]-2,rate);phase=np.angle(spec[:,0]);advance=2*np.pi*hop*np.arange(spec.shape[0])/fft
    out=np.zeros((fft//2+1,len(times)),complex)
    for j,t in enumerate(times):
        k=int(t);alpha=t-k
        mag=(1-alpha)*np.abs(spec[:,k])+alpha*np.abs(spec[:,k+1])
        out[:,j]=mag*np.exp(1j*phase)
        d=np.angle(spec[:,k+1])-np.angle(spec[:,k])-advance;d-=2*np.pi*np.round(d/(2*np.pi));phase+=advance+d
    n=fft+hop*max(0,len(times)-1);audio=np.zeros(n);weight=np.zeros(n)
    for j,frame in enumerate(np.fft.irfft(out.T,n=fft,axis=1)):
        offset=j*hop;audio[offset:offset+fft]+=frame*win;weight[offset:offset+fft]+=win*win
    audio/=np.maximum(weight,1e-9)
    return fit(audio[fft//2:],round(len(x)/rate))
def pitch_to_duration(x,ratio,n):
    slow=np.interp(np.arange(0,len(x)-1,ratio),np.arange(len(x)),x)
    return fit(stretch(slow,len(slow)/n),n)
def write_wav(path,a):
    if a.ndim==1:a=np.column_stack((a,a))
    with wave.open(str(path),'wb') as f:
        f.setnchannels(a.shape[1]);f.setsampwidth(2);f.setframerate(SR);f.writeframes((np.clip(a,-1,1)*32767).astype('<i2').tobytes())

mama=mono_resample(data['mama'],int(data['mama_rate']))
fatalis=mono_resample(data['fatalis'],int(data['fatalis_rate']))
crop_start=.32;mama_crop=fit(mama[int(crop_start*SR):int((crop_start+3)*SR)],N)
write_wav(ROOT/'Mama_Cropped_3s.wav',mama_crop)
vocal=eq(mama_crop,hp=160,lp=3100)

def creature_grains(ratio,seed):
    random=np.random.default_rng(seed);out=np.zeros(N);weights=np.zeros(N)
    grain_n=round(.16*SR);step=round(.034*SR);window=np.hanning(grain_n)
    # Reorder/reverse short loud vowel grains, eliminating the original word rhythm.
    centers=np.array([.33,.48,.65,.80,.96,1.11,1.29,1.47,1.65,1.83,2.02])
    for k,offset in enumerate(range(0,N-grain_n,step)):
        center=float(random.choice(centers))+random.uniform(-.022,.022)
        indices=center*SR+np.arange(grain_n)*ratio
        grain=np.interp(indices,np.arange(len(vocal)),vocal)
        if k%3!=0:grain=grain[::-1]
        out[offset:offset+grain_n]+=grain*window;weights[offset:offset+grain_n]+=window
    out/=np.maximum(weights,.18)
    return out

throat=.72*normalize(creature_grains(.49,11),.18)+.28*normalize(creature_grains(.64,34),.18)
t=np.arange(N)/SR
throat*=.76+.24*np.sin(2*np.pi*(32*t+.65*np.sin(2*np.pi*1.7*t)))
throat=np.tanh(2.6*normalize(throat,.22))/2.2
throat=eq(throat,60,4100,bass=3)
vocal_env=(1-np.exp(-t/.06))*np.exp(-np.maximum(t-1.75,0)/.40)
throat=fades(throat*vocal_env,.03,.32)

start=round(.18*SR);roar_n=round(2.79*SR)
roar_source=fatalis[round(1.90*SR):round(6.55*SR)]
roar=pitch_to_duration(roar_source,.82,roar_n)
roar=normalize(eq(roar,42,9000,bass=3),.23)
roar=fades(roar,.025,.24)
roar_layer=np.zeros(N);roar_layer[start:start+min(roar_n,N-start)]=roar[:N-start]
vocal_layer=np.zeros(N);vocal_layer[start:]=throat[:N-start]*.77

# A rising sub-rumble comes before the heavy chesty impact at 0.18 seconds.
rumble=normalize(eq(rng.normal(0,1,N),25,155),.055)
rumble_env=np.where(t<.18,(t/.18)**1.2,np.exp(-(t-.18)/.52))
rumble*=rumble_env
u=np.maximum(0,t-.18);active=t>=.18
phase=2*np.pi*(34*u+43*.17*(1-np.exp(-u/.17)))
sub=np.sin(phase)*np.exp(-u/.44)*(1-np.exp(-u/.005))*active*.38
chest=normalize(eq(rng.normal(0,1,N),70,1550),.3)*np.exp(-u/.09)*(1-np.exp(-u/.002))*active*.34
impact=sub+chest

dry=.88*roar_layer+.63*vocal_layer+rumble+impact
dry=eq(dry,27,10500,bass=1)
dry=np.tanh(dry*1.45)/1.35
left=dry.copy();right=dry.copy()
for i,(delay,gain) in enumerate([(.037,.095),(.061,.073),(.099,.052),(.151,.041),(.223,.029),(.317,.016)]):
    dl=round((delay+(.003 if i%2 else 0))*SR);dr=round((delay+.007)*SR)
    reflection=eq(dry,110,4100)
    left[dl:]+=reflection[:-dl]*gain;right[dr:]+=reflection[:-dr]*gain*.96
stereo=np.column_stack((left,right));stereo-=stereo.mean(axis=0)
for c in range(2):stereo[:,c]=fades(stereo[:,c],.008,.20)
stereo*=.865/max(np.max(np.abs(stereo)),1e-9)

final=ROOT/'Monster_Roar_MAMA_Effect_3s.mp3'
def encode(a):
    # A PCM file supplies stable, padded decoder buffers to the MP3 encoder.
    encode_source=ROOT/'Monster_Roar_Encoding_Source.wav'
    write_wav(encode_source,a)
    sound=aud.Sound.file(str(encode_source)).cache()
    sound.write(str(final),SR,aud.CHANNELS_STEREO,aud.FORMAT_S16,aud.CONTAINER_MP3,aud.CODEC_MP3,320000,1152)
encode(stereo)
decoded=aud.Sound.file(str(final)).data()
decoded_peak=float(np.max(np.abs(decoded)))
if decoded_peak>.965:
    stereo*=.94/decoded_peak;encode(stereo);decoded=aud.Sound.file(str(final)).data();decoded_peak=float(np.max(np.abs(decoded)))
write_wav(ROOT/'Monster_Roar_Master_3s.wav',stereo)
# Keep a processed-only vocal layer available for checking that language was removed.
write_wav(ROOT/'Mama_Processed_Creature_Layer_3s.wav',normalize(throat,.18))
report=dict(output=final.name,source_crop_start_seconds=crop_start,source_crop_length_seconds=3,final_master_duration_seconds=len(stereo)/SR,
    mp3_decoded_duration_seconds=len(decoded)/SR,sample_rate=SR,channels=2,bitrate_requested_kbps=320,
    decoded_peak_dbfs=round(20*np.log10(decoded_peak+1e-12),2),master_rms_dbfs=round(20*np.log10(rms(stereo)),2),
    peak_clipping_samples=int(np.sum(np.abs(decoded)>=.999)),sources_unchanged=True,
    treatment=['3-second crop from the first supplied clip','short reversed/reordered vowel grains at lowered pitches; no intact MAMA phrase',
    'lower-pitched Fatalis roar compressed to the short effect duration','bass rumble then chesty sub impact at 0.18 seconds','throat modulation, saturation, short stereo reflections and fade-out'])
assert 2.98<=len(decoded)/SR<=3.08,report
assert report['peak_clipping_samples']==0,report
assert final.stat().st_size>10000
(ROOT/'audio-processing-report.json').write_text(json.dumps(report,indent=2))
print('MONSTER_ROAR_COMPLETE '+json.dumps(report))
