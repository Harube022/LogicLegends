if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("This isolated decode check uses Edit Mode.");
string output="output/Cinematics/OpeningRemakeV2/EditSource/NewUserIntegration/";
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var go=new UnityEngine.GameObject("Temporary V4 Video Decode Check");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
var video=go.AddComponent<UnityEngine.Video.VideoPlayer>();
video.playOnAwake=false;video.clip=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>("Assets/Assets/Video_Cutscene/LogicLegends_Opening_V4.mp4");video.audioOutputMode=UnityEngine.Video.VideoAudioOutputMode.None;
var texture=new UnityEngine.RenderTexture(960,540,0,UnityEngine.RenderTextureFormat.ARGB32);texture.Create();video.renderMode=UnityEngine.Video.VideoRenderMode.RenderTexture;video.targetTexture=texture;video.sendFrameReadyEvents=true;
long decoded=-1;string error=null;int stage=0;double began=UnityEditor.EditorApplication.timeSinceStartup;var samples=new System.Collections.Generic.List<object>();
video.frameReady+=(player,index)=>decoded=index;video.errorReceived+=(player,message)=>error=message;
UnityEditor.EditorApplication.CallbackFunction tick=null;
System.Action<bool> finish=passed=>
{
    UnityEditor.EditorApplication.update-=tick;
    var report=new{passed,scope="Native Unity VideoPlayer decoding in isolated Edit Mode; no authentication or account writes",error,samples};
    System.IO.File.WriteAllText(output+"video-decoding.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
    video.Stop();UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);texture.Release();UnityEngine.Object.DestroyImmediate(texture);
};
tick=()=>
{
    try
    {
        if(error!=null){finish(false);return;}
        if(UnityEditor.EditorApplication.timeSinceStartup-began>45){error="Video decoding check timed out in Edit Mode.";finish(false);return;}
        if(stage==0&&video.isPrepared){video.time=20;video.Play();stage=1;decoded=-1;}
        else if((stage==1&&decoded>=600)||(stage==2&&decoded>=1800))
        {
            video.Pause();var previous=UnityEngine.RenderTexture.active;UnityEngine.RenderTexture.active=texture;var frame=new UnityEngine.Texture2D(960,540,UnityEngine.TextureFormat.RGB24,false);frame.ReadPixels(new UnityEngine.Rect(0,0,960,540),0,0);frame.Apply();
            string file=output+(stage==1?"native-v4-map.png":"native-v4-pillar.png");System.IO.File.WriteAllBytes(file,frame.EncodeToPNG());UnityEngine.Object.DestroyImmediate(frame);UnityEngine.RenderTexture.active=previous;
            samples.Add(new{frame=decoded,time=video.time,screenshot=file});
            if(stage==2){finish(true);return;}
            stage=2;decoded=-1;video.time=60;video.Play();
        }
    }
    catch(Exception e){error=e.ToString();finish(false);}
};
UnityEditor.EditorApplication.update+=tick;video.Prepare();
return "Isolated Unity VideoPlayer decode check started for map and cracked-Pillar frames.";
