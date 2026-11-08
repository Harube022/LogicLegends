"""Repair the Sanctuary pullback with a clean, actual Unity camera take.

Uses natural-speed reverse of the already rendered environment-only approach.
No generated pictures, freezes, repeated stills, narration retiming or asset edits.
Stages PNG frames on disk to avoid a large in-memory reverse-video buffer.
"""
from pathlib import Path
import sys,json,subprocess,math
ROOT=Path(__file__).resolve().parents[3]
EDIT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'Library/CinematicTools/python'))
import av,numpy as np
from PIL import Image
FF=json.loads((EDIT/'tools.json').read_text())['ffmpeg']
timeline=json.loads((EDIT/'timeline.json').read_text())
shot=next(s for s in timeline['shots'] if s['label']=='A fading world — Sanctuary')
first=math.ceil(shot['start']*30);end=math.ceil(shot['end']*30);length=end-first
source_end=456;source_start=source_end-length;fade=20
folder=EDIT/'PullbackRepair';folder.mkdir(exist_ok=True)
handles={}

def extract(start,stop,kind):
    count=0
    with av.open(str(EDIT/'UnityFootage.mp4')) as movie:
        video=movie.streams.video[0];video.codec_context.thread_count=2
        movie.seek(int((start/30)/video.time_base),stream=video,backward=True)
        for frame in movie.decode(video=0):
            index=round(float(frame.time)*30)
            if index<start:continue
            if index>=stop:break
            image=frame.to_image()
            if kind=='approach':image.save(folder/f'source_{source_end-1-index:04}.png',compress_level=1)
            else:handles[index]=np.asarray(image).astype(np.float32)
            count+=1
    assert count==stop-start,(kind,count,start,stop)
    print(kind,count,flush=True)

extract(source_start,source_end,'approach')
extract(first-fade,first,'outgoing')
extract(end-fade,end,'return')
smooth=lambda t:t*t*(3-2*t)
for i in range(length):
    a=np.asarray(Image.open(folder/f'source_{i:04}.png')).astype(np.float32)
    u=smooth(i/(length-1))
    gray=(a[:,:,0]*.2126+a[:,:,1]*.7152+a[:,:,2]*.0722)[:,:,None]
    a=(gray+(a-gray)*(.92-.22*u))*(.92-.2*u)
    if i<fade:
        weight=smooth(i/(fade-1));a=handles[first-fade+i]*(1-weight)+a*weight
    if i>=length-fade:
        weight=smooth((i-(length-fade))/(fade-1));a=a*(1-weight)+handles[first+i]*weight
    Image.fromarray(np.clip(a,0,255).astype('uint8')).save(folder/f'frame_{i:04}.png',compress_level=1)
    if i%60==0:print('composite',i,flush=True)
command=[FF,'-hide_banner','-y','-filter_threads','1','-framerate','30','-i',str(folder/'frame_%04d.png'),
    '-frames:v',str(length),'-an','-c:v','libx264','-threads','2','-preset','veryfast','-crf','12','-pix_fmt','yuv420p',str(EDIT/'Sanctuary_Pullback_Repair.mp4')]
result=subprocess.run(command,capture_output=True,text=True)
(folder/'encode.txt').write_text(result.stderr)
assert result.returncode==0,result.stderr[-2000:]
recipe=dict(first_frame=first,end_frame=end,frames=length,file='Sanctuary_Pullback_Repair.mp4',
    source='UnityFootage.mp4',source_first_frame=source_start,source_end_frame=source_end,
    playback='reverse at 1.0x; environment-only actual Unity footage',dissolve_frames=fade,
    purpose='Remove column obstruction; keep the original narration and total frame count unchanged')
(EDIT/'camera-patch.json').write_text(json.dumps(recipe,indent=2))
# The editable scene retraces the same safe camera path on subsequent renders.
shot.update(cameraSourceScene=1,cameraSourceFrom=(source_end-1)/30,cameraSourceTo=source_start/30)
(EDIT/'timeline.json').write_text(json.dumps(timeline,indent=2))
print(json.dumps(recipe),flush=True)
