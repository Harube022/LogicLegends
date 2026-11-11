"""Prepare an isolated fresh-asset Unity remake; preserve the original recording/edit."""
from pathlib import Path
import json, shutil, hashlib

ROOT=Path(__file__).resolve().parents[2]
EDIT=ROOT/'output/Cinematics/OpeningRemake/EditSource'
FOLDER=ROOT/'Assets/Cinematics/OpeningRemake'
EDIT.mkdir(parents=True,exist_ok=True)
(FOLDER/'Editor').mkdir(parents=True,exist_ok=True)
OLD=ROOT/'output/Cinematics/EditSource'
for name in ['Narration_Edit.wav','Music_Ambience_SFX.wav','Review_Mix_Master.wav','caption-cues.json','audio-edl.json','source_transcripts_small.en.json']:
    shutil.copy2(OLD/name,EDIT/name)
shutil.copytree(OLD/'Fonts',EDIT/'Fonts',dirs_exist_ok=True)
(EDIT/'tools.json').write_text(json.dumps(dict(ffmpeg=r'C:/Users/jhayf/.codex/visualizations/2026/10/16/01a1436d-771e-7190-abf8-1ab636024fb9/expert-gameplay-2026-10-23/ffmpeg.exe'),indent=2))
t=json.loads((OLD/'timeline.json').read_text())
def v(a):return dict(zip(('x','y','z'),a))
def shot(s,a,b,aim0,aim1,fov=48,blend=.65,flow=False,c1=None,c2=None,fadein=0,fadeout=0):
    s.update({'from':v(a),'to':v(b),'aimFrom':v(aim0),'aimTo':v(aim1),'fov0':fov,'fov1':fov,'dissolve':blend,'continuous':flow,'fadeIn':fadein,'fadeOut':fadeout})
    s.pop('cameraSourceScene',None);s.pop('cameraSourceFrom',None);s.pop('cameraSourceTo',None)
    if c1:s['control1']=v(c1);s['control2']=v(c2);s['curved']=True
    return s
ss=t['shots']
shot(ss[0],(-269,4010,-239),(-237,3993,-271),(-238,3986,-319),(-234,3989,-321),52,0,True,fadein=1.4)
shot(ss[1],(-237,3993,-271),(-234,3989.5,-298),(-234,3989,-321),(-233.81,3991.54,-320.99),50,0,True)
shot(ss[2],(-234,3989.5,-298),(-233.81,3992.5,-309),(-233.81,3991.54,-320.99),(-233.81,3994,-320.99),48,0,True)
ss[0]['fov1']=50
ss[1]['fov1']=48
shot(ss[3],(-233.81,3992.5,-309),(-233.81,3993,-316.4),(-233.81,3994,-320.99),(-233.81,3993,-320.99),48,0,True,fadeout=1.5)
shot(ss[4],(-234.8,3994,-314.4),(-232.6,3993.2,-314.7),(-233.81,3993,-318.2),(-233.81,3993,-318.2),43,0,fadein=1.1)
shot(ss[5],(-230.8,3994,-306),(-240.3,3995,-306),(-233.81,3992,-321),(-233.81,3992,-321),48,.55,c1=(-233,3995,-304.5),c2=(-238,3995.4,-304.7))
shot(ss[6],(-234.8,3993.2,-314.7),(-234,3993.6,-313.8),(-233.81,3993,-319),(-233.81,3993,-319),43,.35)
shot(ss[7],(-234,3997,-300),(-234,4014,-248),(-233.81,3990,-321),(-233.81,3989,-321),52,.6,c1=(-234,4000,-287),c2=(-234,4011,-265))
shot(ss[8],(-8,12,-63),(-2,13,-59),(0,4,-27),(0,4,-27),52,.8,c1=(-6,12.2,-61),c2=(-4,12.7,-59))
shot(ss[9],(-129,29,673),(-123,29.5,676),(-126,29,696),(-126,29,696),54,.8)
shot(ss[10],(-227.8,3980.5,-267),(-231,3981,-268.8),(-236.1,3978.4,-281),(-236.1,3978.4,-281),54,.8,c1=(-228.6,3980.8,-267.3),c2=(-230,3981,-268.3))
shot(ss[11],(-6,5.8,-60),(-4,7.8,-58),(0,3.2,-45),(0,3.6,-43),49,.55,c1=(-5.6,6,-59),c2=(-4.6,7,-58.3))
end=ss[12]
wide=shot(dict(end),(-2,14,-68),(0,11,-65),(0,3.3,-41),(0,3.3,-41),48,.75)
wide['end']=120.293;wide['label']='The journey begins - chosen minds'
close=shot(dict(end),(-1.8,5.5,-56),(1.2,5.4,-55),(0,3.7,-43),(0,3.7,-43),48,.65)
close.update(start=120.293,end=124.910,label='Restore balance - Guardian and companions')
final=shot(dict(end),(0,11,-65),(0,8.5,-61),(0,3.3,-41),(0,3.3,-41),48,.75,fadeout=2.0)
final.update(start=124.910,label='Reclaim the lost logic')
t['shots']=ss[:12]+[wide,close,final]
# Hermite position and target velocities keep the opening flight continuous at every join.
for i in range(4):
    s=t['shots'][i];duration=s['end']-s['start']
    for field,out in [('from','tangentFrom'),('aimFrom','aimTangentFrom')]:
        endfield='to' if field=='from' else 'aimTo'
        if i==0:vel=[(s[endfield][k]-s[field][k])/duration*.65 for k in 'xyz']
        else:
            prev=t['shots'][i-1];span=s['end']-prev['start'];vel=[(s[endfield][k]-prev[field][k])/span for k in 'xyz']
        s[out]=v(vel)
    for field,out in [('to','tangentTo'),('aimTo','aimTangentTo')]:
        startfield='from' if field=='to' else 'aimFrom'
        if i==3:vel=[0,0,0]
        else:
            nxt=t['shots'][i+1];span=nxt['end']-s['start'];vel=[(nxt[field][k]-s[startfield][k])/span for k in 'xyz']
        s[out]=v(vel)
(EDIT/'timeline.json').write_text(json.dumps(t,indent=2),encoding='utf8')

code=(ROOT/'Assets/Cinematics/OpeningReview/Editor/OpeningReview.cs').read_text(encoding='utf8')
code=code.replace('OpeningReview','OpeningRemake').replace('output/Cinematics"','output/Cinematics/OpeningRemake"')
code=code.replace('LogicLegends_Opening_Review','LogicLegends_Opening_Remake')
code=code.replace('Build Standalone Opening Review','Build Fresh-Asset Opening Remake')
code=code.replace('public Vector3 from,to,aimFrom,aimTo;','public Vector3 from,to,aimFrom,aimTo,control1,control2,tangentFrom,tangentTo,aimTangentFrom,aimTangentTo;public bool continuous,curved;')
old='original=SceneManager.GetActiveScene();if(original.path!="Assets/Scenes/LogicGarden.unity")throw new Exception("Select the current LogicGarden scene first.");'
new='''original=SceneManager.GetActiveScene();
        var source=SceneManager.GetSceneByPath("Assets/Scenes/LogicGarden.unity");
        if(!source.isLoaded)source=EditorSceneManager.OpenScene("Assets/Scenes/LogicGarden.unity",OpenSceneMode.Additive);
        if(source.isDirty)throw new Exception("Fresh source scene must be saved before cloning.");
        var sanctuarySource=SceneManager.GetSceneByPath("Assets/Cinematics/OpeningReview/LogicLegends_Opening_Review.unity");
        if(!sanctuarySource.isLoaded)sanctuarySource=EditorSceneManager.OpenScene("Assets/Cinematics/OpeningReview/LogicLegends_Opening_Review.unity",OpenSceneMode.Additive);'''
assert old in code;code=code.replace(old,new)
code=code.replace('EditorSceneManager.SaveScene(original,"Library/OpeningRemake/Original_Unsaved_Backup.unity",true);','')
code=code.replace('var roots=original.GetRootGameObjects();var sky=RenderSettings.skybox;','''SceneManager.SetActiveScene(source);var sky=RenderSettings.skybox;
        var roots=source.GetRootGameObjects().Concat(sanctuarySource.GetRootGameObjects().Where(r=>r.name=="Sanctuary"||r.name.StartsWith("CLOUDS_PS"))).ToArray();''')
code=code.replace('"LogicGarden_Assets","background_0"','"LogicGarden_Assets","LogicGarden Study Library","background_0"')
code=code.replace('source=original.path,workingScene=ScenePath,sourceWasDirty=original.isDirty','source=source.path,workingScene=ScenePath,sourceWasDirty=source.isDirty')
code=code.replace('profile.Add<ColorAdjustments>(true);color.contrast.Override(9);color.saturation.Override(2);','profile.Add<ColorAdjustments>(true);color.contrast.Override(7);color.saturation.Override(1);var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.13f);vignette.smoothness.Override(.5f);')
code=code.replace('Asset(profile,Folder+"/ReviewVolume.asset");volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/ReviewVolume.asset");','''Asset(profile,Folder+"/ReviewVolume.asset");volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/ReviewVolume.asset");
        foreach(var component in volume.sharedProfile.components)if(component!=null&&!EditorUtility.IsPersistent(component))AssetDatabase.AddObjectToAsset(component,volume.sharedProfile);
        EditorUtility.SetDirty(volume.sharedProfile);''')
old='''float u=Mathf.Clamp01((time-shot.start)/(shot.end-shot.start)),e=Smooth(shot.scene==11?Mathf.Clamp01(u/.82f):u);
        camera.transform.position=Lerp(shot.from,shot.to,e);camera.transform.rotation=Quaternion.LookRotation(Lerp(shot.aimFrom,shot.aimTo,e)-camera.transform.position,Vector3.up);camera.fieldOfView=Mathf.Lerp(shot.fov0,shot.fov1,e);camera.aspect=16f/9;'''
new='''float duration=shot.end-shot.start,u=Mathf.Clamp01((time-shot.start)/duration),e=Smooth(u);
        Vector3 position=shot.continuous?Hermite(shot.from,shot.to,shot.tangentFrom*duration,shot.tangentTo*duration,u):shot.curved?Bezier(shot.from,shot.control1,shot.control2,shot.to,e):Lerp(shot.from,shot.to,e);
        Vector3 aim=shot.continuous?Hermite(shot.aimFrom,shot.aimTo,shot.aimTangentFrom*duration,shot.aimTangentTo*duration,u):Lerp(shot.aimFrom,shot.aimTo,e);
        camera.transform.position=position;camera.transform.rotation=Quaternion.LookRotation(aim-position,Vector3.up);camera.fieldOfView=Mathf.Lerp(shot.fov0,shot.fov1,e);camera.aspect=16f/9;'''
assert old in code;code=code.replace(old,new)
code=code.replace('static float Smooth(float x){return x*x*(3-2*x);}','''static Vector3 Hermite(Vector3 a,Vector3 b,Vector3 va,Vector3 vb,float u){float v=u*u,w=v*u;return (2*w-3*v+1)*a+(w-2*v+u)*va+(-2*w+3*v)*b+(w-v)*vb;}
    static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float u){float v=1-u;return v*v*v*a+3*v*v*u*b+3*v*u*u*c+u*u*u*d;}
    static float Smooth(float x){return x*x*(3-2*x);}''')
code=code.replace('float local=time-shot.start;','float local=time-(shot.scene==11?timeline.shots.First(s=>s.scene==11).start:shot.start);')
old='''wizard.root.transform.SetPositionAndRotation(GardenWizard+new Vector3(-5,0,5),Quaternion.Euler(0,180,0));wizard.Pose(wizard.idle,local);
                portal.position=GardenPortal;portal.rotation=Quaternion.identity;portal.gameObject.SetActive(false);
                for(int i=0;i<cast.Count;i++){cast[i].root.SetActive(i==6);if(i==6)cast[i].Walk(GardenPortal+Vector3.forward*1.8f,new Vector3(0,.02f,-40),u,shot.end-shot.start,local);}'''
new='''wizard.root.transform.SetPositionAndRotation(GardenWizard+new Vector3(-3.5f,0,2),Quaternion.Euler(0,28,0));wizard.Pose(wizard.idle,local);
                portal.position=new Vector3(0,.02f,-40);portal.rotation=Quaternion.identity;portal.gameObject.SetActive(true);
                for(int i=0;i<cast.Count;i++)
                {
                    int order=Array.IndexOf(new[]{6,0,7,2},i);if(order<0){cast[i].root.SetActive(false);continue;}
                    float progress=Mathf.Clamp01((local-order*.46f)/3.25f);cast[i].root.SetActive(local>=order*.46f);
                    Vector3 from=new Vector3((order%2-.5f)*1.7f,.02f,-40.4f),to=new Vector3((order-1.5f)*2.1f,.02f,-46.4f+order*.35f);
                    cast[i].Walk(from,to,progress,3.25f,local);
                }'''
assert old in code;code=code.replace(old,new)
code=code.replace(':Mathf.SmoothStep(1,0,u);',':1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,1,u));')
code=code.replace('for(int i=0;i<2&&frame<total;i++)','for(int i=0;i<4&&frame<total;i++)')
code=code.replace('RenderTexture.active=null;if(frame>=total)Finish();','RenderTexture.active=null;if(frame%60<4)File.WriteAllText(Output+"/EditSource/unity-render-status.json",Status);if(frame>=total)Finish();')
code=code.replace('if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);','if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);')
# Reference existing tested shaders by their original identifiers; material assets are remake-local.
code=code.replace('Hidden/LogicLegends/OpeningRemakeBlend','Hidden/LogicLegends/OpeningReviewBlend')
code=code.replace('''SceneManager.SetActiveScene(original.IsValid()?original:SceneManager.GetSceneByPath("Assets/Scenes/LogicGarden.unity"));''','''if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);''')
code=code.replace('public static string Preview(float seconds','''public static double Benchmark(int count=12)
    {
        Bind();CreateBuffers(1920,1080);double beganAt=EditorApplication.timeSinceStartup;
        for(int i=0;i<count;i++){RenderFrame(100+i/30f);RenderTexture.active=composite;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0,false);}
        double elapsed=EditorApplication.timeSinceStartup-beganAt;RenderTexture.active=null;camera.targetTexture=null;ReleaseBuffers();return elapsed;
    }
    public static string Preview(float seconds''')
code=code.replace('static Dictionary<int,float> lightLevels=new Dictionary<int,float>();','static Dictionary<int,float> lightLevels=new Dictionary<int,float>();static Dictionary<int,float> simulatedAt=new Dictionary<int,float>();')
code=code.replace('ToArray();foreach(var p in particles){','ToArray();simulatedAt.Clear();foreach(var p in particles){')
code=code.replace('foreach(var p in particles)if(p.gameObject.activeInHierarchy)p.Simulate(time%35,false,true,false);','''foreach(var p in particles)
        {
            if(!p.gameObject.activeInHierarchy){simulatedAt.Remove(p.GetInstanceID());continue;}
            float previousTime;bool advance=simulatedAt.TryGetValue(p.GetInstanceID(),out previousTime)&&time>=previousTime&&time-previousTime<.15f;
            p.Simulate(advance?time-previousTime:time,false,!advance,false);simulatedAt[p.GetInstanceID()]=time;
        }''')
(FOLDER/'Editor/OpeningRemake.cs').write_text(code,encoding='utf8')
sources=[]
for row in json.loads((EDIT/'audio-edl.json').read_text()):
    path=ROOT/row['file'];digest=hashlib.sha256(path.read_bytes()).hexdigest()
    assert digest==row['sha256'],path
    assert row['speed']==1
    sources.append(dict(file=row['file'],sha256=digest,speed=1))
(EDIT/'original-voice-integrity.json').write_text(json.dumps(sources,indent=2))
print(json.dumps(dict(renderer=str(FOLDER/'Editor/OpeningRemake.cs'),shots=len(t['shots']),original_recordings_verified=len(sources),duration=t['duration']),indent=2))
