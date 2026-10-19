"""Package actual Unity renders. Requires Pillow; no concept art is reused.

Run after Tools > Proof Devourer > Render Unity review / animation review frames.
"""
from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFont

BASE=Path(__file__).resolve().parents[1]
PROJECT=next(p for p in BASE.parents if (p/'ProjectSettings').is_dir())
OUT=BASE/'Previews'
report=json.loads((OUT/'UnityValidation.json').read_text())
BG=(14,19,23);RED=(255,115,131);WHITE=(235,224,223);MUTED=(158,167,176)
def font(size,bold=False):return ImageFont.truetype('C:/Windows/Fonts/'+('consolab.ttf' if bold else 'consola.ttf'),size)
def label(draw,point,text,size=22,color=WHITE,bold=False):draw.text(point,text,font=font(size,bold),fill=color)

sheet=Image.new('RGB',(1440,1660),BG);d=ImageDraw.Draw(sheet)
label(d,(36,26),'THE PROOF DEVOURER',42,RED,True)
label(d,(38,81),'UNITY / IMPORTED MODEL & ANIMATION REVIEW',21,MUTED)
label(d,(38,117),f"{report['triangles']:,} TRIANGLES   {report['bones']} BONES   {report['materials']} URP MATERIALS   13 CLIPS",21)
d.line((36,157,1404,157),fill=(110,65,75),width=2)
for i,c in enumerate(report['clips']):
    x,y=i%4*352+24,178+i//4*366
    image=Image.open(OUT/('Unity_'+c['name']+'.png')).convert('RGB').resize((334,334),Image.Resampling.LANCZOS)
    sheet.paste(image,(x,y))
    label(d,(x+10,y+302),f"{i+1:02d}  {c['name'].upper()}",20,RED,True)
    label(d,(x+10,y+329),f"{c['seconds']:.1f}s / "+('LOOP' if c['loop'] else 'ONE SHOT'),17,MUTED)
label(d,(390,1320),'VERIFIED IN UNITY '+report['unityVersion'],24,RED,True)
for y,text in [(1365,'Blind quadruped / no wings / no tail'),(1400,'33 bones / four radial mouth controls'),(1435,'Generic avatar / stationary root'),(1470,'Thirteen moving clips / no missing refs'),(1505,'Staged awakening poses match')]:label(d,(390,y),text,20)
label(d,(390,1540),'Android device profiling not performed',18,MUTED)
sheet.save(OUT/'ProofDevourer_UnityContactSheet.png')

model=Image.new('RGB',(1440,1580),BG);d=ImageDraw.Draw(model)
label(d,(36,26),'THE PROOF DEVOURER / MODEL SHEET',36,RED,True)
label(d,(38,80),'ACTUAL UNITY MESH / FRONT, SIDE, BACK AND TOP',20,MUTED)
for i,name in enumerate(['Front','Side','Back','Top']):
    x,y=24+i%2*708,130+i//2*710
    view=Image.open(OUT/('UnityModel_'+name+'.png')).convert('RGB').resize((690,690),Image.Resampling.LANCZOS)
    model.paste(view,(x,y));label(d,(x+22,y+655),name.upper(),24,RED,True)
model.save(OUT/'ProofDevourer_ModelSheet.png')

frames=[]
for clip in report['clips']:
    paths=sorted((PROJECT/'Temp/ProofDevourerReviewFrames'/clip['name']).glob('*.png'))
    for i,path in enumerate(paths):
        frame=Image.new('RGB',(420,492),BG)
        frame.paste(Image.open(path).convert('RGB'),(0,52));d=ImageDraw.Draw(frame)
        label(d,(15,9),clip['name'].upper().replace('_',' '),23,RED,True)
        label(d,(15,36),'ACTUAL UNITY CLIP / 12 FPS',12,MUTED)
        d.line((15,480,405,480),fill=(55,55,64),width=3)
        d.line((15,480,15+int(390*(i+1)/len(paths)),480),fill=RED,width=3)
        frames.append(frame)
if frames:
    reference=Image.new('RGB',(1680,492),BG)
    for i,index in enumerate([0,len(frames)//4,len(frames)//2,3*len(frames)//4]):reference.paste(frames[index],(420*i,0))
    palette=reference.quantize(colors=96)
    indexed=[f.quantize(palette=palette,dither=Image.Dither.NONE) for f in frames]
    indexed[0].save(OUT/'ProofDevourer_AnimationReview.gif',save_all=True,append_images=indexed[1:],duration=83,loop=0,optimize=True,disposal=2)
print(json.dumps({'frames':len(frames),'seconds_at_12fps':len(frames)/12,'output':str(OUT)},indent=2))
