"""Build the sample-accurate narration edit, subtitles, mix and camera cue sheet."""
import sys,json,subprocess,math,wave,re,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'Library/CinematicTools/python'))
import numpy as np
OUT=ROOT/'output/Cinematics'; EDIT=OUT/'EditSource'
FF=json.loads((EDIT/'tools.json').read_text())['ffmpeg'];SR=48000
source=json.loads((EDIT/'source_transcripts_small.en.json').read_text(encoding='utf8'))
data={int(re.search(r'recording (\d+)',s['file'])[1]):s for s in source}
def pcm(path):return np.frombuffer(subprocess.check_output([FF,'-v','error','-i',str(path),'-f','f32le','-ar',str(SR),'-ac','1','pipe:1']),dtype=np.float32).copy()
edl=[];position=round(.5*SR)
for number in sorted(data):
    d=data[number];samples=pcm(ROOT/d['file']);duration=len(samples)/SR
    edl.append(dict(number=number,file=d['file'],sha256=d['sha256'],source_in=0,source_out=duration,start=position/SR,end=(position+len(samples))/SR,samples=len(samples),track='narration',speed=1))
    d['pcm']=samples;position+=len(samples)+round(.15*SR)
    if number==11:position+=round(3.5*SR)
    if number==14:position+=round(2.4*SR)
    if number==19:position+=round(4.8*SR)
duration=math.ceil((position/SR+2.35)*30)/30
by={e['number']:e for e in edl}
def v(x,y,z):return dict(x=x,y=y,z=z)
def shot(scene,label,start,end,a,b,c,d,fov=48,dissolve=0,fadein=0,fadeout=0):return dict(scene=scene,label=label,start=start,end=end,**{'from':v(*a),'to':v(*b),'aimFrom':v(*c),'aimTo':v(*d),'fov0':fov,'fov1':fov,'dissolve':dissolve,'fadeIn':fadein,'fadeOut':fadeout})
s1=0;s2=by[4]['start'];s3=by[8]['start'];s4=by[11]['end']+.15;s5=by[12]['start'];s6=by[13]['start'];s7=by[14]['end']+.15;s8=by[15]['start'];s8b=by[16]['start'];s8c=by[17]['start'];s9=by[18]['start'];s10=by[19]['end']+.15;s11=by[20]['start']
shots=[
shot(1,'An invitation',s1,s2,(-281,4018,-228),(-234,3993,-257),(-238,3984,-318),(-234,3987,-321),52,fadein=1.5),
shot(2,'The Logic Realm',s2,s3,(-234,3993,-257),(-234,3983.5,-294),(-234,3987,-321),(-234,3989,-321),52),
shot(3,'The Pillar of Logic',s3,s4,(-234,3986,-294),(-234,3990,-307),(-234,3989,-321),(-234,3994.8,-321),46,dissolve=.65),
shot(4,'Into darkness',s4,s5,(-234,3990,-307),(-234.3,3993,-317.2),(-234,3994.8,-321),(-234.2,3993.8,-319),46,fadeout=1.8),
shot(5,'The first fracture',s5,s6,(-234.4,3991.94,-313.99),(-233.1,3993.14,-314.69),(-233.81,3992.54,-319.39),(-233.81,3992.54,-319.39),44,fadein=1.0),
shot(6,'The Pillar falters',s6,s7,(-234.61,3993.54,-307.99),(-241.81,3994.54,-308.99),(-233.81,3992.54,-320.99),(-233.81,3992.54,-320.99),46),
shot(7,'Breaking balance',s7,s8,(-232.41,3991.54,-315.49),(-230.81,3992.54,-308.99),(-233.81,3991.54,-319.5),(-233.81,3991.54,-320.99),44),
shot(8,'A fading world — Sanctuary',s8,s8b,(-241.81,3999.54,-287.99),(-261.81,4011.54,-248.99),(-233.81,3988.54,-320.99),(-233.81,3988.54,-320.99),52,dissolve=.65),
shot(8,'A fading world — Garden',s8b,s8c,(0,11,-58),(-4,13,-62),(0,5,-27),(0,5,-27),52,dissolve=.65),
shot(8,'A fading world — Library',s8c,s9,(-126,29,674),(-126,28.5,678),(-126,29,697),(-126,29,697),57,dissolve=.65),
shot(9,'The Guardian\'s call',s9,s10,(-234,3981.4,-263),(-231.5,3983,-266),(-234.2,3980,-282),(-234.2,3980,-282),48,dissolve=.8),
shot(10,'Arrival in Logic Garden',s10,s11,(-.8,5.5,-54),(0,12,-58),(0,3.2,-44),(0,5.5,-29),49),
shot(11,'The journey begins',s11,duration,(0,19,-71),(0,9.7,-67),(0,3.0,-42),(0,3,-42),45,dissolve=.6,fadeout=2),
]
words={n:[w for s in d['segments'] for w in s['words']] for n,d in data.items()}
timeline=dict(duration=duration,firstCrack=by[12]['start']+words[12][-1]['start']-.25,spreadCrack=by[13]['start']+words[13][-1]['start'],portalCall=by[19]['start']+words[19][3]['start'],shots=shots)
(EDIT/'timeline.json').write_text(json.dumps(timeline,indent=2),encoding='utf8')
# Phrase boundaries are actual recognition/alignment intervals, not guessed word durations.
groups={
1:[(0,2,'Stay awhile...'),(3,4,'and listen...')],2:[(0,4,'Beyond the world you know...')],3:[(0,2,'lies a realm...'),(3,5,'shaped by logic...')],
4:[(0,2,'A world governed'),(3,5,'not by chaos...')],6:[(0,2,'but by truth...'),(3,5,'reasoning and structure...')],7:[(0,2,'The Logic Realm...')],
8:[(0,2,'At its core...'),(3,7,'stands the Pillar of Logic...')],10:[(0,3,'The force that binds'),(4,5,'all knowledge...')],11:[(0,0,'into balance...')],
12:[(0,1,'But now...'),(2,4,'something is wrong...')],13:[(0,2,'The Pillar falters...')],14:[(0,3,'And the very logic'),(4,7,'that shapes this realm...'),(8,10,'begins to fade...')],
15:[(0,2,'As understanding fades...'),(3,5,'in your world...')],16:[(0,1,'So too...'),(2,7,'does this realm begin to collapse...')],
17:[(0,2,'The foundation of'),(3,4,'Discrete Mathematics...'),(5,5,'forgotten...')],18:[(0,5,'But all is not yet lost...')],
19:[(0,2,'The Logic Guardian'),(3,6,'calls upon chosen minds...')],20:[(0,1,'And here...'),(2,4,'your journey begins...')],
21:[(0,2,'To restore balance...')],22:[(0,3,'and reclaim the logic'),(4,7,'that has been lost...')],}
cues=[]
for n,parts in groups.items():
    for first,last,text in parts:
        cues.append(dict(start=by[n]['start']+words[n][first]['start'],end=min(by[n]['start']+words[n][last]['end']+.18,by[n]['end']),text=text,source_number=n,first_word=first,last_word=last))
for i in range(len(cues)-1):cues[i]['end']=min(cues[i]['end'],cues[i+1]['start']-.02)
def stamp(t):
    ms=round(t*1000);h,ms=divmod(ms,3600000);m,ms=divmod(ms,60000);s,ms=divmod(ms,1000);return f'{h:02}:{m:02}:{s:02},{ms:03}'
(OUT/'LogicLegends_Opening_Review.srt').write_text('\n\n'.join(f'{i+1}\n{stamp(c["start"])} --> {stamp(c["end"])}\n{c["text"]}' for i,c in enumerate(cues))+'\n',encoding='utf8')
(EDIT/'caption-cues.json').write_text(json.dumps(cues,indent=2),encoding='utf8')
(EDIT/'audio-edl.json').write_text(json.dumps(edl,indent=2),encoding='utf8')
total=round(duration*SR);narration=np.zeros(total,dtype=np.float32)
for e in edl:
    s=data[e['number']]['pcm'];rms=float(np.sqrt(np.mean(s*s)));gain=min(3,.13/max(.001,rms),.92/max(.001,float(np.max(np.abs(s)))));s=s*gain
    # Only the original near-silent source edges receive 10 ms anti-click fades.
    fade=min(480,len(s)//2);s[:fade]*=np.linspace(0,1,fade);s[-fade:]*=np.linspace(1,0,fade)
    at=round(e['start']*SR);narration[at:at+len(s)]+=s
def write_wav(path,values):
    with wave.open(str(path),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(SR);w.writeframes((np.clip(values,-.999,.999)*32767).astype('<i2').tobytes())
write_wav(EDIT/'Narration_Edit.wav',narration)
# The project's supplied introduction music remains subordinate to speech.
music_path=ROOT/'Assets/FINAL_ASSETS/MAPS/CINEMATIC/AUDIO/BGMUSICS/INTRODUCTIONBGMUSIC1.mp3'
music=pcm(music_path);music/=max(.0001,float(np.sqrt(np.mean(music*music))))
bed=np.zeros(total,dtype=np.float32);overlap=round(2*SR);offset=0
while offset<total:
    size=min(len(music),total-offset);piece=music[:size].copy();f=min(overlap,size//2)
    piece[:f]*=np.linspace(0,1,f);piece[-f:]*=np.linspace(1,0,f);bed[offset:offset+size]+=piece;offset+=max(1,len(music)-overlap)
t=np.arange(total,dtype=np.float32)/SR
activity=np.abs(narration).reshape(-1) # duck gain uses the real speech envelope in 100 ms blocks.
block=4800;power=np.array([np.sqrt(np.mean(activity[i:i+block]**2)) for i in range(0,total,block)])
duck=np.interp(t,np.arange(len(power))*.1,np.where(power>.02,.009,.018)).astype(np.float32)
bed*=duck;bed*=np.minimum(np.clip(t/2,0,1),np.clip((duration-t)/2.2,0,1))
rng=np.random.default_rng(105);rumble=np.zeros(total,dtype=np.float32)
for begin,end in [(s5,s9)]:
    mask=np.clip((t-begin)/2,0,1)*np.clip((end-t)/2,0,1)
    rumble+=(np.sin(2*np.pi*43*t)+.45*np.sin(2*np.pi*61.3*t))*.009*mask
sfx=np.zeros(total,dtype=np.float32)
for at,file,gain in [(timeline['firstCrack'],'Mgc_Ice_Ball_Cast_01.wav',.035),(s7,'Mgc_Ice_Arrow_Hit_01.wav',.045),(timeline['portalCall'],'Mgc_Water_Cast_01.wav',.035)]:
    path=ROOT/'Assets/Assets/Sounds Effects/Small Sound Kit/Magic'/file
    if path.exists():
        value=pcm(path);value*=gain/max(.001,float(np.sqrt(np.mean(value*value))));i=round(at*SR);length=min(len(value),total-i);sfx[i:i+length]+=value[:length]
write_wav(EDIT/'Music_Ambience_SFX.wav',bed+rumble+sfx)
mix=narration+bed+rumble+sfx
mix*=min(1,.97/max(.001,float(np.max(np.abs(mix)))))
write_wav(EDIT/'Review_Mix_PreMaster.wav',mix)
(EDIT/'edit-report.json').write_text(json.dumps(dict(runtime=duration,frames=round(duration*30),source_recordings=len(edl),subtitle_cues=len(cues),narration_unchanged_speed=True,listening_review='not_performed',transcription='local faster-whisper small.en, word-aligned; proper terms checked against supplied brief',uncertain_spans=['recording 11: into balance','recording 12: now','recording 13: falters','recording 16: So too','recording 20: And here'],source_recordings_unmodified=True),indent=2))
print(json.dumps({'runtime':duration,'frames':round(duration*30),'shots':[(s['scene'],s['label'],round(s['start'],2),round(s['end'],2)) for s in shots],'events':{k:timeline[k] for k in ['firstCrack','spreadCrack','portalCall']}},indent=2))
