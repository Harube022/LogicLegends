import json,re
from pathlib import Path
p=Path(__file__).resolve().parents[1]/'Recovery/licensing-diagnostics-sanitized.json'
for item in json.loads(p.read_text(encoding='utf8')):
    print('\nFILE',item['file'],flush=True)
    lines=[s for s in item['lines'] if 'particles' not in s.lower()]
    relevant=[s for s in lines if re.search(r'error|fail|invalid|expired|revok|missing|disconnect|timed? ?out|no valid|not valid|no license|not found',s,re.I)]
    for s in (relevant[-12:] if relevant else lines[-6:]):print(s[:1300],flush=True)
