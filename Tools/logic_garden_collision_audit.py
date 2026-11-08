"""Read-only serialized collision inventory, including prefab sources."""
import re,json,subprocess,collections
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
scene=ROOT/'Assets/Scenes/LogicGarden.unity'
assets={}
for name in subprocess.check_output(['rg','--files','--null','Assets','-g','*.meta'],cwd=ROOT).decode('utf8').split('\0'):
    if not name:continue
    p=ROOT/name
    s=p.read_text(encoding='utf8',errors='replace')[:250]
    guid=re.search(r'^guid: (\w+)',s,re.M)
    if guid:assets[guid[1]]=name[:-5].replace('\\','/')
def docs(text):
    return {int(m[2]):(int(m[1]),m[3]) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
def field(s,key):
    m=re.search(r'^  '+re.escape(key)+r': (.*)',s,re.M);return m[1] if m else None
d=docs(scene.read_text(encoding='utf8'));entries=[]
for ident,(kind,s) in d.items():
    if kind not in [64,65,135,136,143,154]:continue
    go=field(s,'m_GameObject');gid=int(re.search(r'fileID: (-?\d+)',go)[1]) if go else 0
    g=d.get(gid,(0,''))[1];name=field(g,'m_Name');ref=field(g,'m_CorrespondingSourceObject') or ''
    source=re.search(r'guid: (\w+)',ref);source=assets.get(source[1],source[1]) if source else ''
    mesh=field(s,'m_Mesh') or '';mg=re.search(r'guid: (\w+)',mesh)
    mesh_source=assets.get(mg[1],mg[1]) if mg else ''
    entries.append(dict(id=ident,kind=kind,object_id=gid,name=name,source=source,mesh_source=mesh_source,enabled=field(s,'m_Enabled'),trigger=field(s,'m_IsTrigger'),convex=field(s,'m_Convex'),size=field(s,'m_Size'),center=field(s,'m_Center')))
summary=collections.Counter((e['mesh_source'] or e['source'] or e['name'] or 'unknown') for e in entries)
out=ROOT/'output/CollisionRepairs';out.mkdir(parents=True,exist_ok=True)
(out/'LogicGarden-collider-inventory.json').write_text(json.dumps(dict(scene=str(scene),colliders=entries,counts=dict(summary)),indent=2))
print(json.dumps(dict(total=len(entries),counts=dict(summary)),indent=2))
