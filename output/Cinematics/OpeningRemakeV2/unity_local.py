"""Local MCP transport fallback for the already running Unity editor service."""
from pathlib import Path
import json, urllib.request, sys
URL='http://127.0.0.1:8080/mcp'
STATE=Path(__file__).parent/'EditSource/local-mcp-session.json'

def request(method,params=None,session=None,notification=False):
    data={'jsonrpc':'2.0','method':method}
    if not notification:data['id']=1
    if params is not None:data['params']=params
    headers={'Content-Type':'application/json','Accept':'application/json, text/event-stream'}
    if session:headers['Mcp-Session-Id']=session
    req=urllib.request.Request(URL,json.dumps(data).encode(),headers=headers,method='POST')
    with urllib.request.urlopen(req,timeout=180) as response:
        raw=response.read().decode();sid=response.headers.get('Mcp-Session-Id',session)
    if not raw:return {},sid
    messages=[json.loads(line[5:].strip()) for line in raw.splitlines() if line.startswith('data:')]
    result=messages[-1] if messages else json.loads(raw)
    if 'error' in result:raise RuntimeError(result['error'])
    return result.get('result',result),sid

def initialize():
    _,sid=request('initialize',{'protocolVersion':'2024-11-05','capabilities':{},'clientInfo':{'name':'LogicLegendsCinematicReview','version':'1.0'}})
    request('notifications/initialized',session=sid,notification=True)
    STATE.write_text(json.dumps({'session':sid}))
    return sid

def call(name,args):
    sid=json.loads(STATE.read_text())['session'] if STATE.exists() else initialize()
    return request('tools/call',{'name':name,'arguments':args},sid)[0]

if __name__=='__main__':
    mode=sys.argv[1]
    if mode=='list':
        sid=initialize();result,_=request('tools/list',session=sid)
        print(json.dumps([t for t in result['tools'] if t['name'] in ['execute_code','read_console','refresh_unity','set_active_instance']],indent=2))
    elif mode=='call':
        print(json.dumps(call(sys.argv[2],json.loads(Path(sys.argv[3]).read_text(encoding='utf-8-sig')))))
    elif mode=='resource':
        sid=json.loads(STATE.read_text())['session'] if STATE.exists() else initialize()
        print(json.dumps(request('resources/read',{'uri':sys.argv[2]},sid)[0]))
    elif mode=='exec':
        result=call('execute_code',{'action':'execute','code':Path(sys.argv[2]).read_text(encoding='utf-8-sig')})
        if len(sys.argv)>3:Path(sys.argv[3]).write_text(json.dumps(result,indent=2))
        print(json.dumps(result.get('structuredContent',result)))
    elif mode=='console':
        result=call('read_console',{'action':'get','types':['error'],'count':'5','format':'detailed'})
        print(json.dumps(result.get('structuredContent',result)))
