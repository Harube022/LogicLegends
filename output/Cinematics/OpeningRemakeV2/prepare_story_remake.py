"""Author the Movie_002-faithful version using saved Unity assets and original narration."""
from pathlib import Path
import json,shutil,hashlib

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'output/Cinematics/OpeningRemakeV2/EditSource'
FOLDER=ROOT/'Assets/Cinematics/OpeningRemakeV2'
PREV=ROOT/'output/Cinematics/OpeningRemake/EditSource'
OUT.mkdir(parents=True,exist_ok=True);(FOLDER/'Editor').mkdir(parents=True,exist_ok=True)
for name in ['Narration_Edit.wav','Music_Ambience_SFX.wav','Review_Mix_Master.wav','caption-cues.json','audio-edl.json','source_transcripts_small.en.json','tools.json','fracture-control-points.json']:
    shutil.copy2(PREV/name,OUT/name)
shutil.copytree(PREV/'Fonts',OUT/'Fonts',dirs_exist_ok=True)
t=json.loads((PREV/'timeline.json').read_text())
def vec(v):return dict(zip('xyz',v))
def pose(s,a,b,c,d,fov=50,blend=.7):
    s.update({'from':vec(a),'to':vec(b),'aimFrom':vec(c),'aimTo':vec(d),'fov0':fov,'fov1':fov,'continuous':False,'curved':False,'dissolve':blend})
    return s
s=t['shots']
pose(s[0],(-1300,88,-1120),(-2140,48,-1120),(-2847,0,-1173),(-2847,12,-1135),54,0)
pose(s[1],(-2140,48,-1120),(-2668,31,-1097),(-2847,12,-1135),(-2847,25,-1096),54,0)
s[0].update(label='Smoke and symbols - Logic Realm reveal',continuous=True,fadeIn=1.3)
s[1].update(label='World map - approach the Pillar',continuous=True)
for i in range(2):
    row=s[i];span=row['end']-row['start']
    for start,end,tan0,tan1 in [('from','to','tangentFrom','tangentTo'),('aimFrom','aimTo','aimTangentFrom','aimTangentTo')]:
        if i==0:
            v0=[(row[end][k]-row[start][k])/span*.5 for k in 'xyz']
            nxt=s[1];v1=[(nxt[end][k]-row[start][k])/(nxt['end']-row['start']) for k in 'xyz']
        else:
            prev=s[0];v0=[(row[end][k]-prev[start][k])/(row['end']-prev['start']) for k in 'xyz'];v1=[(row[end][k]-row[start][k])/span*.35 for k in 'xyz']
        row[tan0]=vec(v0);row[tan1]=vec(v1)
# The atlas approaches the actual Pillar; a match dissolve takes us into its sanctuary.
pose(s[2],(-234,3986.6,-292),(-234,3992.5,-309),(-233.81,3990,-321),(-233.81,3994,-320.99),49,1.1)
s[2]['label']='Pillar sanctuary - match reveal from map'
pose(s[7],(-622.5,22.4,413),(-624,21.7,409),(-640.5,17.4,397),(-641,18.1,393),53,.65)
s[7]['label']='Classroom - understanding fades'
pose(s[8],(-234,3997,-300),(-234,4010,-260),(-233.81,3990,-321),(-233.81,3989,-321),52,.65)
s[8]['label']='Sanctuary - the realm begins to collapse'
pose(s[9],(-634,23,401),(-637,24,398),(-640.4,24,384.8),(-640.4,24,384.8),47,.65)
s[9]['label']='Classroom - Discrete Mathematics forgotten'
guardian=s[10]
pose(guardian,(-226.5,3980.6,-268),(-226,3980,-278),(-236.1,3978.4,-281),(-236.1,3978.4,-281),52,.65)
guardian['label']='Guardian - gathering the summoning magic';guardian['end']=103.5
school=pose(dict(guardian),(-622.5,23,412),(-624,24,409),(-640.5,17.5,400),(-640.5,18,400),54,.45)
school.update(start=103.5,end=105.8,label='Classroom - chosen students are summoned')
cast=pose(dict(guardian),(-226,3980,-278),(-226.5,3979.8,-281),(-236.1,3978.4,-281),(-236.1,3978.4,-281),52,.45)
cast.update(start=105.8,end=109.06733333333334,label='Guardian - spell and open portal')
arrival=s[11]
pose(arrival,(-7,5.2,-58),(-5.5,5.6,-57),(0,3.2,-45),(0,3.3,-46),50,.55)
arrival.update(label='Logic Garden - students walk out of the portal',end=115.8)
wide=s[12]
pose(wide,(-4,7.7,-61),(-2,7.2,-59),(0,3,-45),(0,3,-45),49,.65)
wide.update(start=115.8,end=120.293,label='Welcome - four chosen students and their Guardian')
close=s[13]
pose(close,(-1.5,5.2,-56),(1,5.3,-55),(0,3.5,-45),(0,3.5,-45),48,.65)
close['label']='Restore balance - Guardian welcomes the students'
final=s[14]
pose(final,(0,8,-61),(0,7,-59),(0,3,-45),(0,3,-45),49,.65)
final.update(label='The journey begins - chosen minds',fadeOut=2)
t['shots']=s[:10]+[guardian,school,cast,arrival,wide,close,final]
if not (OUT/'timeline.json').exists():
    (OUT/'timeline.json').write_text(json.dumps(t,indent=2))

code=(ROOT/'Assets/Cinematics/OpeningRemake/Editor/OpeningRemake.cs').read_text(encoding='utf8').replace('OpeningRemake','OpeningRemakeV2')
code=code.replace('public static class OpeningRemakeV2','public static partial class OpeningRemakeV2')
code=code.replace('p.useAutoRandomSeed=false;p.randomSeed=42;p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);','p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);p.useAutoRandomSeed=false;p.randomSeed=42;')
code=code.replace('Build Fresh-Asset Opening Remake','Build Movie 002 Story Remake')
code=code.replace('RenderSettings.skybox=sky;RenderSettings.fog=false;','BuildStorySets(film);RenderSettings.skybox=sky;RenderSettings.fog=false;')
code=code.replace('if(File.Exists(Output+"/EditSource/timeline.json"))','BindStorySets();if(File.Exists(Output+"/EditSource/timeline.json"))')
beg=code.index('    static void EvaluateShot(Shot shot,float time)')
end=code.index('    public static double Benchmark',beg)
code=code[:beg]+'    static void EvaluateShot(Shot shot,float time){EvaluateStory(shot,time);}\n'+code[end:]
code=code.replace('float e=Smooth(u);root.transform.position=Vector3.Lerp(from,to,e);','float e=Mathf.Clamp01(u);root.transform.position=Vector3.Lerp(from,to,e);')
code=code.replace('float phase=length*e/natural;','float phase=length*e/natural*1.18f;')
code=code.replace('Mathf.Clamp01(u*8),Mathf.Clamp01((1-u)*8)','Mathf.Clamp01(u*18),Mathf.Clamp01((1-u)*18)')
if not (FOLDER/'Editor/OpeningRemakeV2.cs').exists():
    (FOLDER/'Editor/OpeningRemakeV2.cs').write_text(code,encoding='utf8')

finish=(ROOT/'output/Cinematics/OpeningRemake/finish_remake.py').read_text(encoding='utf8')
finish=finish.replace("'LogicLegends_Opening_Remastered.mp4'","'LogicLegends_Opening_Story_Remake.mp4'")
finish=finish.replace("'LogicLegends_Opening_Remastered.srt'","'LogicLegends_Opening_Story_Remake.srt'")
finish=finish.replace("'Remastered-Final.tmp.mp4'","'Story-Remake-Final.tmp.mp4'")
finish=finish.replace('Remastered Opening','Story Opening').replace('Remastered opening','Story opening')
finish=finish.replace("'-preset','medium','-crf','18'","'-preset','fast','-crf','17'")
finish=finish.replace("important=[", "important=[5.5,12.5,20.5,28.5,74.5,91.5,101.5,103.9,106.5,111.5,114.5,")
if not (OUT.parent/'finish_story_remake.py').exists():
    (OUT.parent/'finish_story_remake.py').write_text(finish,encoding='utf8')
integrity=json.loads((PREV/'source-assets-verified.json').read_text())
for file in ['Assets/Assets/Video_Cutscene/Movie_002.mp4','Assets/Scenes/CINEMATICS.unity','Assets/FINAL_ASSETS/MAPS/CINEMATIC/LOGICREALM3.png']:
    integrity.append(dict(file=file,sha256=hashlib.sha256((ROOT/file).read_bytes()).hexdigest()))
(OUT/'source-assets-verified.json').write_text(json.dumps(integrity,indent=2))
print(json.dumps({'visual_reference':'Movie_002.mp4','voice':'20 original recordings, unchanged at 1x','duration':t['duration'],'shots':len(t['shots'])},indent=2))
