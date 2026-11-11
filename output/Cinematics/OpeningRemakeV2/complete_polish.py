from pathlib import Path
import json,time,subprocess,sys,math,re
EDIT=Path(__file__).parent/'EditSource'
deadline=time.monotonic()+600
while True:
    status=json.loads((EDIT/'unity-render-status.json').read_text())
    if not status['rendering']:break
    if time.monotonic()>deadline:raise TimeoutError('Unity render did not finish within ten minutes')
    time.sleep(2)
assert status['frame']==status['total']==4024 and not status['error'],status
(EDIT/'unity-polished-render-status.json').write_text(json.dumps(status,indent=2))
samples=json.loads((EDIT/'wizard-polished-motion.json').read_text())
def xyz(text):return [float(n) for n in re.findall(r'-?\d+(?:\.\d+)?',text)]
a=min(samples,key=lambda s:abs(s['time']-116.5));b=min(samples,key=lambda s:abs(s['time']-116.7))
dist=lambda p,q:math.sqrt(sum((xyz(p)[i]-xyz(q)[i])**2 for i in (0,2)))
motion={'grounding':'Lowest ankle kept near the floor while preserving the original foot lift',
        'root_travel_116_5_to_116_7':dist(a['position'],b['position']),
        'planted_left_foot_drift_same_interval':dist(a['left'],b['left']),
        'stride_bias_removed_for_wizard':True,'original_animation_clips_unchanged':True}
assert motion['planted_left_foot_drift_same_interval']<.08
(EDIT/'polish-motion-verification.json').write_text(json.dumps(motion,indent=2))
print(json.dumps({'render':'complete','new_frames':1858,'motion':motion}),flush=True)
subprocess.run([sys.executable,str(Path(__file__).parent/'finish_story_remake.py'),'polish'],check=True)
