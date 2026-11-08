"""Read-only, sanitized Unity licensing diagnostics; never modifies licensing."""
from pathlib import Path
import os,re,json,shutil
ROOT=Path(__file__).resolve().parents[3]
REC=ROOT/'output/Cinematics/Recovery';REC.mkdir(parents=True,exist_ok=True)
backup=ROOT/'Library/OpeningReview/Original_Unsaved_Backup.unity'
if backup.exists():
    target=REC/'LogicGarden_Unsaved_BeforeCinematic.unity'
    if not target.exists():shutil.copy2(backup,target)
    print('SCENE BACKUP',str(target),'bytes',target.stat().st_size,flush=True)
local=Path(os.environ.get('LOCALAPPDATA','C:/Users/harub/AppData/Local'))
roam=Path(os.environ.get('APPDATA','C:/Users/harub/AppData/Roaming'))
paths=[local/'Unity/Editor/Editor.log',local/'Unity/Unity.Licensing.Client.log',local/'Unity/Unity.Licensing.Client.log.1',local/'Unity/Unity.Entitlements.Audit.log']
for root in [local/'Unity',roam/'UnityHub/logs']:
    if root.exists():
        paths.extend(p for p in root.glob('**/*') if p.is_file() and ('licens' in p.name.lower() or p.name=='info-log.json') and p.suffix in ['.log','.json','.1'])
seen=set();output=[]
for path in paths:
    if str(path) in seen or not path.exists():continue
    seen.add(str(path))
    try:
        with path.open('rb') as f:
            f.seek(max(0,path.stat().st_size-2000000));raw=f.read().decode('utf8','replace')
        lines=[s for s in raw.splitlines() if re.search(r'licens|entitle|activation|401|403|certificate|expired|timed? ?out|connection',s,re.I)]
        clean=[]
        for s in lines[-80:]:
            s=re.sub(r'[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}','[EMAIL REDACTED]',s)
            s=re.sub(r'[A-Za-z0-9+/=_-]{24,}','[IDENTIFIER REDACTED]',s)
            s=re.sub(r'(?i)(access[_ -]?token|refresh[_ -]?token|serial|license[_ -]?key|password)(["\s]*[:=]["\s]*)([^\s,;"}]+)',r'\1\2[REDACTED]',s)
            clean.append(s[:1800])
        item={'file':str(path),'modified':path.stat().st_mtime,'lines':clean}
        output.append(item);print(json.dumps(item,ensure_ascii=False),flush=True)
    except Exception as e:print('READ ERROR',str(path),type(e).__name__,flush=True)
(REC/'licensing-diagnostics-sanitized.json').write_text(json.dumps(output,indent=2),encoding='utf8')
