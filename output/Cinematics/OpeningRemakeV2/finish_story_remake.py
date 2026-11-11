"""Finish the fresh Unity render with the user's original voice and readable subtitles."""
from pathlib import Path
import json, subprocess, re, hashlib, shutil, sys, math, wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont

EDIT=Path(__file__).resolve().parent/'EditSource'
ROOT=EDIT.parents[3]
OUT=ROOT/'output/Cinematics'
FF=json.loads((EDIT/'tools.json').read_text())['ffmpeg']
REALM=len(sys.argv)>1 and sys.argv[1] in ['realm','realm-inspect']
PILLAR=len(sys.argv)>1 and sys.argv[1] in ['pillar','pillar-inspect']
POLISH=REALM or PILLAR or len(sys.argv)>1 and sys.argv[1] in ['polish','polish-inspect']
TARGET=OUT/('LogicLegends_Opening_Story_Remake_v4.mp4' if PILLAR else 'LogicLegends_Opening_Story_Remake_v3.mp4' if REALM else 'LogicLegends_Opening_Story_Remake_v2.mp4' if POLISH else 'LogicLegends_Opening_Story_Remake.mp4')
DURATION=json.loads((EDIT/'timeline.json').read_text())['duration']

def run(args,log,check=True):
    result=subprocess.run([FF,'-hide_banner','-y','-filter_threads','1','-threads','4',*args],cwd=EDIT,capture_output=True,text=True,encoding='utf8',errors='replace')
    (EDIT/log).write_text(result.stdout+'\n'+result.stderr,encoding='utf8')
    if check and result.returncode:raise RuntimeError(result.stderr[-4000:])
    return result.stdout+'\n'+result.stderr

def stamp(seconds,ass=False):
    if ass:
        n=round(seconds*100);h,n=divmod(n,360000);m,n=divmod(n,6000);s,n=divmod(n,100)
        return f'{h}:{m:02d}:{s:02d}.{n:02d}'
    n=round(seconds*1000);h,n=divmod(n,3600000);m,n=divmod(n,60000);s,n=divmod(n,1000)
    return f'{h:02d}:{m:02d}:{s:02d},{n:03d}'

def prepare():
    cues=json.loads((EDIT/'caption-cues.json').read_text())
    # Retain all spoken phrases and the existing word-aligned original-recording timings.
    for c in cues:c['text']=c['text'].replace('...','…')
    font=ImageFont.truetype(str(EDIT/'Fonts/OpenSans-Semibold.ttf'),60)
    caption_checks=[]
    for i,c in enumerate(cues):
        width=font.getlength(c['text']);cps=len(c['text'])/(c['end']-c['start'])
        assert c['start']<c['end']<=DURATION
        assert width<1640
        if i:assert cues[i-1]['end']<=c['start']
        caption_checks.append(dict(text=c['text'],width=width,seconds=c['end']-c['start'],characters_per_second=cps))
    # Georgia is the installed title typeface; Open Sans is the existing licensed subtitle font.
    shutil.copy2('C:/Windows/Fonts/georgiab.ttf',EDIT/'Fonts/georgiab.ttf')
    header='''[Script Info]
Title: Logic Legends - Story Opening
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080
WrapStyle: 0
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Subtitle,Open Sans SemiBold,60,&H00F2FAFF,&H00F2FAFF,&H001B1210,&H80000000,0,0,0,0,100,100,0,0,1,3.2,1.5,2,130,130,72,1
Style: EndTitle,Georgia,68,&H0088D9FF,&H0088D9FF,&H001B1210,&H80000000,-1,0,0,0,100,100,5,0,1,2,2,8,80,80,84,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
'''
    lines=[f'Dialogue: 0,{stamp(c["start"],True)},{stamp(c["end"],True)},Subtitle,,0,0,0,,{{\\fad(90,120)}}{c["text"]}' for c in cues]
    lines.append(f'Dialogue: 1,{stamp(130.85,True)},{stamp(DURATION-.1,True)},EndTitle,,0,0,0,,{{\\fad(650,600)}}LOGIC LEGENDS')
    (EDIT/'Captions.ass').write_text(header+'\n'.join(lines)+'\n',encoding='utf8')
    TARGET.with_suffix('.srt').write_text('\n\n'.join(f'{i+1}\n{stamp(c["start"])} --> {stamp(c["end"])}\n{c["text"]}' for i,c in enumerate(cues))+'\n',encoding='utf8')
    (EDIT/'subtitle-verification.json').write_text(json.dumps(caption_checks,indent=2))
    edl=json.loads((EDIT/'audio-edl.json').read_text())
    integrity=[]
    for row in edl:
        digest=hashlib.sha256((ROOT/row['file']).read_bytes()).hexdigest()
        assert digest==row['sha256'] and row['speed']==1
        integrity.append(dict(file=row['file'],unmodified=True,playback_speed=1))
    (EDIT/'voice-verification.json').write_text(json.dumps(integrity,indent=2))
    # Reuse the original mastered narration/music mix; no voice, words or timings are regenerated.
    assert hashlib.sha256((EDIT/'Review_Mix_Master.wav').read_bytes()).digest()==hashlib.sha256((OUT/'EditSource/Review_Mix_Master.wav').read_bytes()).digest()
    print(json.dumps(dict(original_voice_files=len(edl),subtitle_cues=len(cues),font_px=60,voice_speed=1),indent=2),flush=True)

def encode():
    status=json.loads((EDIT/'unity-render-status.json').read_text())
    if PILLAR:
        assert not status['rendering'] and status['frame']==status['total']==3345 and not status['error'],status
        for name,frames in [('unity-polished-render-status.json',4024),('unity-realm3d-render-status.json',1020)]:
            previous=json.loads((EDIT/name).read_text())
            assert not previous['rendering'] and previous['frame']==previous['total']==frames and not previous['error'],previous
        assert (EDIT/'UnityPillarOrbitFootage.mp4').exists()
    elif REALM:
        assert not status['rendering'] and status['frame']==status['total']==1020 and not status['error'],status
        polished=json.loads((EDIT/'unity-polished-render-status.json').read_text())
        assert not polished['rendering'] and polished['frame']==polished['total']==4024 and not polished['error'],polished
        assert (EDIT/'UnityRealm3DFootage.mp4').exists()
    elif POLISH:
        assert not status['rendering'] and status['frame']==status['total']==4024 and not status['error'],status
        assert (EDIT/'UnityPolishedFootage.mp4').exists()
        segment_probe=run(['-i','UnityPolishedFootage.mp4','-map','0:v:0','-c','copy','-f','null','-'],'polished-segment-probe.txt')
        assert int(re.findall(r'frame=\s*(\d+)',segment_probe)[-1])==4024-2166
    casting_refined=not REALM and not PILLAR and status['total']==3273 and (EDIT/'UnityCastingRefined.mp4').exists()
    refined=(status['total']==1020 or casting_refined) and (EDIT/'UnityOpeningRefined.mp4').exists()
    if refined:
        opening=json.loads((EDIT/'unity-opening-render-status.json').read_text()) if casting_refined else status
        assert not opening['rendering'] and opening['frame']==opening['total']==1020 and not opening['error'],opening
        if casting_refined:
            assert not status['rendering'] and status['frame']==status['total']==3273 and not status['error'],status
        complete=json.loads((EDIT/'unity-full-render-status.json').read_text())
        assert not complete['rendering'] and complete['frame']==complete['total']==round(DURATION*30) and not complete['error'],complete
    elif not PILLAR:
        assert not status['rendering'] and status['frame']==status['total']==round(DURATION*30) and not status['error'],status
    temporary=EDIT/'Story-Remake-Final.tmp.mp4'
    if PILLAR:
        inputs=['-i','UnityRealm3DFootage.mp4','-i','UnityPillarOrbitFootage.mp4','-i','UnityPolishedFootage.mp4','-i','Review_Mix_Master.wav','-filter_complex_threads','1',
                '-filter_complex','[0:v]trim=end_frame=952,setpts=PTS-STARTPTS[opening];[1:v]trim=end_frame=2393,setpts=PTS-STARTPTS[pillar];[2:v]trim=start_frame=1179:end_frame=1858,setpts=PTS-STARTPTS[ending];[opening][pillar][ending]concat=n=3:v=1:a=0,settb=AVTB,setpts=N/(30*TB),scale=in_color_matrix=bt601:out_color_matrix=bt709,ass=filename=Captions.ass:fontsdir=Fonts[finished]',
                '-map','[finished]','-map','3:a:0']
    elif POLISH:
        inputs=['-i','UnityRealm3DFootage.mp4' if REALM else 'UnityOpeningRefined.mp4','-i','UnityFootage.mp4','-i','UnityPolishedFootage.mp4','-i','Review_Mix_Master.wav','-filter_complex_threads','1',
                '-filter_complex','[0:v]trim=end_frame=1020,setpts=PTS-STARTPTS[opening];[1:v]trim=start_frame=1020:end_frame=2166,setpts=PTS-STARTPTS[middle];[2:v]trim=end_frame=1858,setpts=PTS-STARTPTS[polish];[opening][middle][polish]concat=n=3:v=1:a=0,settb=AVTB,setpts=N/(30*TB),scale=in_color_matrix=bt601:out_color_matrix=bt709,ass=filename=Captions.ass:fontsdir=Fonts[finished]',
                '-map','[finished]','-map','3:a:0']
    elif casting_refined:
        inputs=['-i','UnityOpeningRefined.mp4','-i','UnityFootage.mp4','-i','UnityCastingRefined.mp4','-i','Review_Mix_Master.wav','-filter_complex_threads','1',
                '-filter_complex','[0:v]trim=end_frame=1020,setpts=PTS-STARTPTS[opening];[1:v]split=2[base1][base2];[base1]trim=start_frame=1020:end_frame=3096,setpts=PTS-STARTPTS[middle];[2:v]trim=end_frame=177,setpts=PTS-STARTPTS[casting];[base2]trim=start_frame=3273,setpts=PTS-STARTPTS[ending];[opening][middle][casting][ending]concat=n=4:v=1:a=0,settb=AVTB,setpts=N/(30*TB),scale=in_color_matrix=bt601:out_color_matrix=bt709,ass=filename=Captions.ass:fontsdir=Fonts[finished]',
                '-map','[finished]','-map','3:a:0']
    elif refined:
        inputs=['-i','UnityOpeningRefined.mp4','-i','UnityFootage.mp4','-i','Review_Mix_Master.wav','-filter_complex_threads','1',
                '-filter_complex','[0:v]trim=end_frame=1020,setpts=PTS-STARTPTS[opening];[1:v]trim=start_frame=1020,setpts=PTS-STARTPTS[rest];[opening][rest]concat=n=2:v=1:a=0,settb=AVTB,setpts=N/(30*TB),scale=in_color_matrix=bt601:out_color_matrix=bt709,ass=filename=Captions.ass:fontsdir=Fonts[finished]',
                '-map','[finished]','-map','2:a:0']
    else:
        inputs=['-i','UnityFootage.mp4','-i','Review_Mix_Master.wav','-map','0:v:0','-map','1:a:0',
                '-vf','scale=in_color_matrix=bt601:out_color_matrix=bt709,ass=filename=Captions.ass:fontsdir=Fonts']
    run([*inputs,
         '-c:v','libx264','-preset','fast','-crf','17','-threads','4','-pix_fmt','yuv420p','-r','30','-fps_mode','cfr',
         '-color_primaries','bt709','-color_trc','bt709','-colorspace','bt709',
         '-c:a','aac','-b:a','192k','-ar','48000','-ac','2','-movflags','+faststart',
         '-metadata','title=Logic Legends - Story Opening','-progress','encode-progress.txt','-stats_period','2',str(temporary)],'encode-log.txt')
    temporary.replace(TARGET)
    print('Exported '+str(TARGET),flush=True)

def inspect():
    errors=run(['-v','error','-i',str(TARGET),'-map','0:v:0','-map','0:a:0','-f','null','-'],'full-decode.txt')
    assert not errors.strip(),errors
    probe=run(['-i',str(TARGET),'-map','0:v:0','-c','copy','-f','null','-'],'final-probe.txt')
    counted_frames=int(re.findall(r'frame=\s*(\d+)',probe)[-1])
    assert counted_frames==round(DURATION*30),counted_frames
    assert '1920x1080' in probe and '30 fps' in probe
    qa=EDIT/('QA-v4' if PILLAR else 'QA-v3' if REALM else 'QA-v2' if POLISH else 'QA');qa.mkdir(exist_ok=True)
    run(['-i',str(TARGET),'-vf','fps=1/4,scale=640:-1','-q:v','3',str(qa/'frame-%03d.jpg')],'contact-extract.txt')
    files=sorted(qa.glob('frame-*.jpg'))
    font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
    for page in range(math.ceil(len(files)/12)):
        part=files[page*12:(page+1)*12]
        board=Image.new('RGB',(1920,4*390),(16,21,27));draw=ImageDraw.Draw(board)
        for i,f in enumerate(part):
            x=i%3*640;y=i//3*390;board.paste(Image.open(f),(x,y+30))
            draw.text((x+10,y+4),f'{(page*12+i)*4+2:.1f}s',font=font,fill='white')
        board.save(qa/f'contact-{page+1}.jpg',quality=94)
    important=[5.5,12.5,20.5,28.5,33.9,34,34.1,74.5,77.8,89.8,91.5,96.7,97.8,101.5,101.9,103.1,103.2,103.9,106.5,111.5,114.5,116.2,117.2,15.45,15.55,31.7,31.8,49.8,50.8,51.8,54.8,72.15,72.3,80.6,80.8,96.2,96.8,104.3,108.8,109.1,109.3,113.8,114.1,120.4,124.9,125.6,130.9,132.5]
    if PILLAR:important += [40,41,53.6,54.0,54.65,54.75,56,60]
    for t in important:
        run(['-ss',str(t),'-i',str(TARGET),'-frames:v','1','-q:v','2',str(qa/f'boundary-{t:05.1f}.jpg')],f'extract-{t}.txt')
    unchanged=[]
    for row in json.loads((EDIT/'source-assets-verified.json').read_text()):
        ok=hashlib.sha256((ROOT/row['file']).read_bytes()).hexdigest()==row['sha256'];unchanged.append(dict(file=row['file'],unchanged=ok));assert ok,row['file']
    report=dict(file=str(TARGET),duration=DURATION,resolution='1920x1080',fps=30,frames=counted_frames,
        full_audio_video_decode='passed',original_voice_speed=1,original_voice_mix='byte-identical source WAV',
        subtitles='36 burned-in timed captions plus separate SRT',sources_unchanged=unchanged,
        manual_visual_qa='pending',actual_new_user_device_playback='not tested; no playback trigger changed')
    (EDIT/('Verification-v4.json' if PILLAR else 'Verification-v3.json' if REALM else 'Verification-v2.json' if POLISH else 'Verification.json')).write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2),flush=True)

if __name__=='__main__':
    mode=sys.argv[1] if len(sys.argv)>1 else 'all'
    if mode in ['all','prepare','polish','realm','pillar']:prepare()
    if mode in ['all','encode','polish','realm','pillar']:encode()
    if mode in ['all','inspect','polish','polish-inspect','realm','realm-inspect','pillar','pillar-inspect']:inspect()
