from pathlib import Path
import json,copy,hashlib
EDIT=Path(__file__).parent/'EditSource';ROOT=EDIT.parents[3]
path=EDIT/'timeline.json'
backup=EDIT/'timeline-before-walk-paper-circle-polish.json'
if not backup.exists():backup.write_bytes(path.read_bytes())
t=json.loads(backup.read_text(encoding='utf-8-sig'))
def vec(*v):return dict(zip('xyz',v))
shots=[]
for s in t['shots']:
    if s['label']=='Classroom - understanding fades':
        s['end']=75.5;shots.append(s)
        p=copy.deepcopy(s);p.update(label='Classroom - original failed exam close-up',start=75.5,end=80.69133333333333,dissolve=.55,continuous=False,curved=False,
            **{'from':vec(-637.6,21,407),'to':vec(-638.1,20.5,406.6),'aimFrom':vec(-639.5,16.69,405.2),'aimTo':vec(-639.5,16.69,405.2),'fov0':43,'fov1':40})
        shots.append(p);continue
    if s['label']=='Classroom - Discrete Mathematics forgotten':
        p=copy.deepcopy(s);p.update(label='Classroom - F grade and forgotten foundations',end=91.5,dissolve=.65,
            **{'from':vec(-639,23.2,408),'to':vec(-639.5,22.2,407.2),'aimFrom':vec(-639.5,16.69,405.2),'aimTo':vec(-639.5,16.69,405.2),'fov0':43,'fov1':43})
        shots.append(p);s['start']=91.5;shots.append(s);continue
    if s['label']=='Guardian - gathering the summoning magic':s['end']=100.4
    if s['label']=='Classroom - chosen students are summoned':
        s.update(start=100.4,**{'from':vec(-623.5,26.6,414),'to':vec(-625.2,26.8,413.3),'aimFrom':vec(-640,15.4,401),'aimTo':vec(-640,15.4,401),'fov0':58,'fov1':58})
    shots.append(s)
t['shots']=shots
assert all(abs(a['end']-b['start'])<.0001 for a,b in zip(shots,shots[1:]))
path.write_text(json.dumps(t,indent=2))
sources=json.loads((EDIT/'source-assets-verified.json').read_text())
for file in ['Assets/FINAL_ASSETS/MAPS/CINEMATIC/Tests.fbx','Assets/Characters_NEW/LATEST_SKINS/Wizard/Animations/Wizard_Idle1.anim','Assets/Characters_NEW/LATEST_SKINS/Wizard/Animations/Wizard_Walking.anim']:
    if not any(s['file']==file for s in sources):sources.append(dict(file=file,sha256=hashlib.sha256((ROOT/file).read_bytes()).hexdigest()))
(EDIT/'source-assets-verified.json').write_text(json.dumps(sources,indent=2))
print(json.dumps({'shots':len(shots),'duration':t['duration'],'original_failed_papers':'Tests.fbx','render_start_frame':2166},indent=2))
