"""Read-only local transcription of supplied audio; never synthesizes speech."""
import sys, os, json, hashlib, re, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'Library/CinematicTools/python'))
os.environ['HF_HUB_DISABLE_XET']='1'
os.environ['HF_HOME']=str(ROOT/'Library/CinematicTools/huggingface')
from faster_whisper import WhisperModel
import imageio_ffmpeg, av, numpy as np
OUT=ROOT/'output/Cinematics/EditSource'
OUT.mkdir(parents=True,exist_ok=True)
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
(OUT/'tools.json').write_text(json.dumps({'ffmpeg':ffmpeg,'python':sys.executable},indent=2))
model_name=sys.argv[1] if len(sys.argv)>1 else 'base.en'
model=WhisperModel(model_name,device='cpu',compute_type='int8',cpu_threads=6,download_root=str(ROOT/'Library/CinematicTools/models'),local_files_only='--download' not in sys.argv)
folder=ROOT/'Assets/FINAL_ASSETS/MAPS/CINEMATIC/AUDIO/VOICEOVER'
result=[]
for p in sorted(folder.glob('*.mp3'),key=lambda p:int(re.search(r'(\d+)\.mp3',p.name)[1])):
    container=av.open(str(p)); stream=container.streams.audio[0]
    duration=float(stream.duration*stream.time_base) if stream.duration else container.duration/1e6
    container.close()
    pcm=subprocess.check_output([ffmpeg,'-v','error','-i',str(p),'-f','f32le','-ar','16000','-ac','1','pipe:1'])
    segments,info=model.transcribe(np.frombuffer(pcm,dtype=np.float32),language='en',beam_size=5,word_timestamps=True,vad_filter=True,condition_on_previous_text=False,initial_prompt='Logic Legends. Logic Realm. Pillar of Logic. Discrete Mathematics. Logic Guardian. Chosen minds.')
    segments=list(segments)
    entry={'file':p.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'duration':duration,'text':' '.join(s.text.strip() for s in segments),'segments':[{'start':s.start,'end':s.end,'text':s.text,'words':[{'word':w.word,'start':w.start,'end':w.end,'probability':w.probability} for w in s.words]} for s in segments]}
    result.append(entry)
    (OUT/('source_transcripts_'+model_name+'.json')).write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
    print(p.name,round(duration,3),entry['text'],flush=True)
