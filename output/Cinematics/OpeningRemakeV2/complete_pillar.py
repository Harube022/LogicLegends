from pathlib import Path
import json, time, subprocess, sys

EDIT=Path(__file__).parent/'EditSource'
deadline=time.monotonic()+900
while True:
    try:
        status=json.loads((EDIT/'unity-render-status.json').read_text())
    except json.JSONDecodeError:
        time.sleep(1)
        continue
    if not status['rendering']:break
    if time.monotonic()>deadline:raise TimeoutError('Pillar render did not finish within fifteen minutes.')
    time.sleep(2)
assert status['frame']==status['total']==3345 and not status['error'],status
(EDIT/'unity-pillar-orbit-render-status.json').write_text(json.dumps(status,indent=2))
print('Pillar orbit render completed. Encoding and verifying V4.',flush=True)
subprocess.run([sys.executable,str(Path(__file__).parent/'finish_story_remake.py'),'pillar'],check=True)
