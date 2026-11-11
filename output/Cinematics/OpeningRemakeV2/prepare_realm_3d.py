from pathlib import Path
import json,copy
EDIT=Path(__file__).parent/'EditSource'
path=EDIT/'timeline.json';backup=EDIT/'timeline-before-3d-realm.json'
if not backup.exists():backup.write_bytes(path.read_bytes())
t=json.loads(backup.read_text(encoding='utf-8-sig'));s=t['shots']
def v(*xyz):return dict(zip('xyz',xyz))
s[0].update(label='Smoke and symbols - reveal the sculpted Logic Realm',to=v(-2300,-470,-1405),aimTo=v(-2820,0,-1173),fov0=54,fov1=54)
s[1].update(label='Three dimensional Logic Realm - orbit toward the Pillar',**{'from':v(-2300,-470,-1405),'to':v(-2650,-135,-1064),'aimFrom':v(-2820,0,-1173),'aimTo':v(-2813,24,-1093),'fov0':54,'fov1':54})
for i in (0,1):
    row=s[i];span=row['end']-row['start']
    for start,end,tan0,tan1 in [('from','to','tangentFrom','tangentTo'),('aimFrom','aimTo','aimTangentFrom','aimTangentTo')]:
        if i==0:
            a=[(row[end][k]-row[start][k])/span*.5 for k in 'xyz'];nxt=s[1]
            b=[(nxt[end][k]-row[start][k])/(nxt['end']-row['start']) for k in 'xyz']
        else:
            prev=s[0];a=[(row[end][k]-prev[start][k])/(row['end']-prev['start']) for k in 'xyz']
            b=[(row[end][k]-row[start][k])/span*.35 for k in 'xyz']
        row[tan0]=v(*a);row[tan1]=v(*b)
assert all(abs(a['end']-b['start'])<.0001 for a,b in zip(s,s[1:]))
path.write_text(json.dumps(t,indent=2));print('Updated the smoke-to-world orbit while preserving all narration and later scenes.')
