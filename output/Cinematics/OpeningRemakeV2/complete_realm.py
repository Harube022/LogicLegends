from pathlib import Path
import json, time, subprocess, sys

EDIT = Path(__file__).parent / 'EditSource'
deadline = time.monotonic() + 600
while True:
    try:
        status = json.loads((EDIT / 'unity-render-status.json').read_text())
    except json.JSONDecodeError:
        time.sleep(1)
        continue
    if not status['rendering']:
        break
    if time.monotonic() > deadline:
        raise TimeoutError('The 3D realm render did not complete within ten minutes.')
    time.sleep(2)
assert status['frame'] == status['total'] == 1020 and not status['error'], status
(EDIT / 'unity-realm3d-render-status.json').write_text(json.dumps(status, indent=2))
print('The 1020-frame 3D opening render is complete. Encoding V3.', flush=True)
subprocess.run([sys.executable, str(Path(__file__).parent / 'finish_story_remake.py'), 'realm'], check=True)
