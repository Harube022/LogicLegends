"""Finish the real Unity footage with original narration and timed subtitles.

Run after OpeningReview.StartRender has finished. No gameplay or source asset writes.
"""
from pathlib import Path
import json, subprocess, shutil, sys, re, hashlib, wave
ROOT=Path(__file__).resolve().parents[3]
EDIT=Path(__file__).resolve().parent
OUT=EDIT.parent
sys.path.insert(0,str(ROOT/'Library/CinematicTools/python'))
import av, numpy as np
from PIL import Image, ImageDraw, ImageFont
FF=json.loads((EDIT/'tools.json').read_text())['ffmpeg']

def run(args,log):
    result=subprocess.run([FF,'-hide_banner','-y','-filter_threads','1','-threads','2',*args],cwd=EDIT,capture_output=True,text=True,encoding='utf8',errors='replace')
    (EDIT/log).write_text(result.stdout+'\n'+result.stderr,encoding='utf8')
    if result.returncode:raise RuntimeError(result.stderr[-4000:])
    return result.stderr

def ass_time(seconds):
    cs=round(seconds*100); h,cs=divmod(cs,360000); m,cs=divmod(cs,6000); s,cs=divmod(cs,100)
    return f'{h}:{m:02}:{s:02}.{cs:02}'

def prepare_captions():
    fonts=EDIT/'Fonts';fonts.mkdir(exist_ok=True)
    source=ROOT/'Assets/Photon/PhotonChat/Demos/Demo Chat/Resources/OpenSans'
    for name in ['OpenSans-Semibold.ttf','Apache License.txt']:shutil.copy2(source/name,fonts/name)
    cues=json.loads((EDIT/'caption-cues.json').read_text(encoding='utf8'))
    font=ImageFont.truetype(str(fonts/'OpenSans-Semibold.ttf'),54)
    checks=[]
    header='''[Script Info]
Title: Logic Legends Opening Review
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080
WrapStyle: 0
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Subtitle,Open Sans SemiBold,54,&H00FFFFFF,&H00FFFFFF,&H18090B11,&H60000000,0,0,0,0,100,100,0,0,1,3,1.5,2,120,120,78,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
'''
    lines=[]
    for i,c in enumerate(cues):
        assert 0<=c['start']<c['end']
        if i:assert cues[i-1]['end']<=c['start']
        text=c['text'].replace('...','…')
        bounds=font.getbbox(text,stroke_width=3)
        checks.append(dict(cue=i+1,text=text,width=bounds[2]-bounds[0],seconds=c['end']-c['start']))
        assert bounds[2]-bounds[0]<1680,(i,text,bounds)
        lines.append(f"Dialogue: 0,{ass_time(c['start'])},{ass_time(c['end'])},Subtitle,,0,0,0,,{{\\fad(100,120)}}{text}")
    (EDIT/'Captions.ass').write_text(header+'\n'.join(lines)+'\n',encoding='utf8')
    (EDIT/'caption-layout-check.json').write_text(json.dumps(checks,indent=2),encoding='utf8')
    return cues

def master_audio():
    stats=run(['-i','Review_Mix_PreMaster.wav','-af','loudnorm=I=-18:TP=-1.5:LRA=11:print_format=json','-f','null','-'],'loudness-pass1.txt')
    match=re.findall(r'\{\s*"input_i".*?\}',stats,re.S); values=json.loads(match[-1])
    effect=('loudnorm=I=-18:TP=-1.5:LRA=11:linear=true:print_format=json'
      +':measured_I='+values['input_i']+':measured_TP='+values['input_tp']
      +':measured_LRA='+values['input_lra']+':measured_thresh='+values['input_thresh']+':offset='+values['target_offset'])
    run(['-i','Review_Mix_PreMaster.wav','-af',effect,'-ar','48000','-ac','2','-c:a','pcm_s24le','Review_Mix_Master.wav'],'loudness-pass2.txt')
    return values

def encode():
    status=json.loads((EDIT/'unity-render-status.json').read_text())
    expected=round(json.loads((EDIT/'timeline.json').read_text())['duration']*30)
    assert not status['rendering'] and status['frame']==status['total']==expected and not status['error'],status
    with av.open(str(EDIT/'UnityFootage.mp4')) as c:
        v=c.streams.video[0]
        assert (v.width,v.height)==(1920,1080)
        assert v.average_rate==30 and v.frames==expected
    run(['-i','UnityFootage.mp4','-i','Review_Mix_Master.wav',
      '-map','0:v:0','-map','1:a:0',
      '-vf','scale=in_color_matrix=bt601:out_color_matrix=bt709,ass=filename=Captions.ass:fontsdir=Fonts',
      '-c:v','libx264','-threads','2','-preset','veryfast','-crf','17','-pix_fmt','yuv420p',
      '-color_primaries','bt709','-color_trc','bt709','-colorspace','bt709',
      '-c:a','aac','-b:a','192k','-ar','48000','-ac','2',
      '-movflags','+faststart','-metadata','title=Logic Legends — Opening Cinematic Review',
      '-progress','encode-progress.txt','-stats_period','2',
      'Finished_Review.tmp.mp4'],'final-encode.txt')
    (EDIT/'Finished_Review.tmp.mp4').replace(OUT/'LogicLegends_Opening_Review.mp4')

def inspect():
    target=OUT/'LogicLegends_Opening_Review.mp4'
    timeline=json.loads((EDIT/'timeline.json').read_text())
    cues=json.loads((EDIT/'caption-cues.json').read_text())
    expected=round(timeline['duration']*30)
    samples=set(range(2,int(timeline['duration']),3))
    for s in timeline['shots']:
        for t in [s['start']+.07,s['start']+.4,(s['start']+s['end'])/2,s['end']-.07]:samples.add(max(0,min(expected-1,round(t*30)))/30)
    for c in cues:samples.add(round((c['start']+c['end'])*15)/30)
    indices={round(t*30) for t in samples}
    qa=EDIT/'QA';qa.mkdir(exist_ok=True)
    decoded=0; thumbs=[];means=[]; motion=[];last=None;pts=[]
    with av.open(str(target)) as container:
        video=container.streams.video[0];audio=container.streams.audio[0]
        metadata=dict(width=video.width,height=video.height,fps=str(video.average_rate),video_codec=video.codec_context.name,audio_codec=audio.codec_context.name,
          audio_rate=audio.codec_context.sample_rate,audio_layout=audio.codec_context.layout.name,duration=float(container.duration/1e6),stream_frames=video.frames)
        for frame in container.decode(video=0):
            stamp=float(frame.time);pts.append(stamp)
            image=frame.to_image() if decoded%30==0 or decoded in indices else None
            small=image.resize((320,180)) if image is not None else None
            if decoded%30==0:
                a=np.asarray(small);means.append(dict(time=stamp,mean=float(a.mean()),dark_fraction=float((a.max(axis=2)<8).mean())))
                if last is not None:motion.append(dict(time=stamp,difference=float(np.abs(a.astype(float)-last).mean())))
                last=a.astype(float)
            if decoded in indices:
                image.save(qa/f'frame_{decoded:05}.jpg',quality=91)
                thumbs.append((stamp,small.copy()))
            decoded+=1
    assert decoded==expected,decoded
    assert all(abs((b-a)-1/30)<.002 for a,b in zip(pts,pts[1:])), 'nonuniform video timestamps'
    assert metadata['video_codec']=='h264' and metadata['audio_codec']=='aac'
    assert (metadata['width'],metadata['height'])==(1920,1080)
    for sheet_index,start in enumerate(range(0,len(thumbs),24)):
        part=thumbs[start:start+24];sheet=Image.new('RGB',(1280,6*204),(15,18,24));draw=ImageDraw.Draw(sheet)
        for i,(stamp,img) in enumerate(part):
            x=(i%4)*320;y=(i//4)*204;sheet.paste(img,(x,y));draw.text((x+9,y+183),f'{stamp:.2f} seconds',fill='white')
        sheet.save(qa/f'contact_{sheet_index+1:02}.jpg',quality=94)
    run(['-v','error','-i',str(target),'-f','null','-'],'full-decode-check.txt')
    measured=run(['-i',str(target),'-af','loudnorm=I=-18:TP=-1.5:LRA=11:print_format=json','-vn','-f','null','-'],'final-loudness.txt')
    values=json.loads(re.findall(r'\{\s*"input_i".*?\}',measured,re.S)[-1])
    sources=json.loads((EDIT/'source_transcripts_small.en.json').read_text())
    integrity=[dict(file=s['file'],unchanged=hashlib.sha256((ROOT/s['file']).read_bytes()).hexdigest()==s['sha256']) for s in sources]
    assert all(s['unchanged'] for s in integrity)
    metadata.update(decoded_frames=decoded,audio_loudness=values,original_narration_files_unchanged=integrity,
      sample_frames=len(thumbs),sample_sheets=(len(thumbs)+23)//24,video_timestamp_check='passed',full_decode='passed',
      direct_audio_audition='unavailable in this tool session',manual_visual_review='pending',
      source_scene_hash=hashlib.sha256((ROOT/'Assets/Scenes/LogicGarden.unity').read_bytes()).hexdigest())
    (EDIT/'qa-report.json').write_text(json.dumps(metadata,indent=2),encoding='utf8')
    (qa/'frame-statistics.json').write_text(json.dumps(dict(brightness=means,motion=motion),indent=2))
    print(json.dumps(metadata,indent=2),flush=True)

if __name__=='__main__':
    mode=sys.argv[1] if len(sys.argv)>1 else 'all'
    if mode in ['all','prepare']:prepare_captions();master_audio()
    if mode in ['all','encode']:encode()
    if mode in ['all','inspect']:inspect()
