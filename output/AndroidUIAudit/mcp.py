import json, sys, urllib.request, urllib.error, time
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
root=Path(__file__).resolve().parent
session_file=root/'mcp_session.txt'
def rpc(method,params,session=None):
    h={'Content-Type':'application/json','Accept':'application/json, text/event-stream'}
    if session:h['mcp-session-id']=session
    req=urllib.request.Request('http://127.0.0.1:8080/mcp',json.dumps({'jsonrpc':'2.0','id':time.time_ns()%2147483647,'method':method,'params':params}).encode(),h)
    with urllib.request.urlopen(req,timeout=55) as response:
        body=response.read().decode();sid=response.headers.get('mcp-session-id')
    lines=[l[6:] for l in body.splitlines() if l.startswith('data: ')]
    return json.loads(lines[-1] if lines else body),sid
if not session_file.exists():
    result,sid=rpc('initialize',{'protocolVersion':'2024-11-05','capabilities':{},'clientInfo':{'name':'AndroidUIAudit','version':'1.0'}});session_file.write_text(sid)
method=sys.argv[1]
if method=='code':
    method='tools/call';params={'name':'execute_code','arguments':{'action':'execute','code':Path(sys.argv[2]).read_text(encoding='utf-8-sig')}}
else:params=json.loads(Path(sys.argv[2][1:]).read_text(encoding='utf-8-sig') if sys.argv[2].startswith('@') else sys.argv[2]) if len(sys.argv)>2 else {}
while isinstance(params,str):params=json.loads(params)
try:result,_=rpc(method,params,session_file.read_text().strip())
except urllib.error.HTTPError as e:
    print(e.read().decode());raise
if method=='tools/list':
    (root/'tools.json').write_text(json.dumps(result,indent=2));result={'names':[t['name'] for t in result['result']['tools']]}
print(json.dumps(result,ensure_ascii=False,indent=2))
