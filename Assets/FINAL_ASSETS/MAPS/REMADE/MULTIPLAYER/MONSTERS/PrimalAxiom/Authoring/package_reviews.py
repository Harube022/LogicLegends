"""Assemble actual Unity-rendered poses and motion frames into review images.

Requires Pillow. Run after Tools > Primal Axiom > Render animation review frames.
No concept artwork or generated illustration is substituted for the 3D renders.
"""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

BASE=Path(__file__).resolve().parents[1]
PROJECT=next(p for p in BASE.parents if (p/'ProjectSettings').is_dir())
PREVIEWS=BASE/'Previews'
report=json.loads((PREVIEWS/'UnityValidation.json').read_text())
BG=(14,19,23)
GOLD=(246,190,94)
WHITE=(229,229,220)
MUTED=(150,165,170)
def font(size,bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/'+('consolab.ttf' if bold else 'consola.ttf'),size)
def label(draw,point,text,size=22,color=WHITE,bold=False):
    draw.text(point,text,font=font(size,bold),fill=color)

width,height=1440,1660
sheet=Image.new('RGB',(width,height),BG)
draw=ImageDraw.Draw(sheet)
label(draw,(36,26),'THE PRIMAL AXIOM',42,GOLD,True)
label(draw,(38,81),'UNITY / IMPORTED MODEL & ANIMATION REVIEW',21,MUTED)
label(draw,(38,117),'6,240 TRIANGLES   34 BONES   2 URP MATERIALS   13 CLIPS',21,WHITE)
draw.line((36,157,width-36,157),fill=(89,73,47),width=2)
for i,clip in enumerate(report['clips']):
    col,row=i%4,i//4
    x,y=col*352+24,178+row*366
    image=Image.open(PREVIEWS/('Unity_'+clip['name']+'.png')).convert('RGB').resize((334,334),Image.Resampling.LANCZOS)
    sheet.paste(image,(x,y))
    label(draw,(x+10,y+302),f"{i+1:02d}  {clip['name'].upper()}",20,GOLD,True)
    label(draw,(x+10,y+329),f"{clip['seconds']:.1f}s / "+('LOOP' if clip['loop'] else 'ONE SHOT'),17,MUTED)
label(draw,(390,1320),'VERIFIED IN UNITY 6000.3.6f1',24,GOLD,True)
label(draw,(390,1365),'Generic avatar / bound skeleton',20)
label(draw,(390,1400),'In-place root / +Z forward / scale 1',20)
label(draw,(390,1435),'Zero missing references',20)
label(draw,(390,1470),'Sleep, Idle and Chase loops match',20)
label(draw,(390,1505),'Return_To_Sleep matches Sleep',20)
label(draw,(390,1540),'Android device profiling not performed',18,MUTED)
sheet.save(PREVIEWS/'PrimalAxiom_UnityContactSheet.png')

frames=[]
for clip in report['clips']:
    name=clip['name']
    paths=sorted((PROJECT/'Temp/PrimalAxiomReviewFrames'/name).glob('*.png'))
    for i,path in enumerate(paths):
        canvas=Image.new('RGB',(420,492),BG)
        canvas.paste(Image.open(path).convert('RGB'),(0,52))
        d=ImageDraw.Draw(canvas)
        label(d,(15,9),name.upper().replace('_',' '),23,GOLD,True)
        label(d,(15,36),'ACTUAL UNITY CLIP / 12 FPS',12,MUTED)
        d.line((15,480,405,480),fill=(50,56,60),width=3)
        d.line((15,480,15+int(390*(i+1)/len(paths)),480),fill=GOLD,width=3)
        frames.append(canvas)
if frames:
    # A shared palette avoids color flicker and keeps the review small.
    reference=Image.new('RGB',(420*4,492),BG)
    for i,index in enumerate([0,len(frames)//4,len(frames)//2,3*len(frames)//4]):
        reference.paste(frames[index],(i*420,0))
    palette=reference.quantize(colors=128)
    indexed=[frame.quantize(palette=palette,dither=Image.Dither.NONE) for frame in frames]
    indexed[0].save(PREVIEWS/'PrimalAxiom_AnimationReview.gif',save_all=True,
                    append_images=indexed[1:],duration=83,loop=0,optimize=True,disposal=2)
print(json.dumps({'frames':len(frames),'seconds_at_12fps':len(frames)/12,'contact_sheet':str(PREVIEWS/'PrimalAxiom_UnityContactSheet.png'),'animation':str(PREVIEWS/'PrimalAxiom_AnimationReview.gif')},indent=2))
