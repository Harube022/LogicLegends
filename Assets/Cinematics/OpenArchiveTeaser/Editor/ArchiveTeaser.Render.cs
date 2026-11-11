using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Linq;
using Process=System.Diagnostics.Process;
using ProcessStartInfo=System.Diagnostics.ProcessStartInfo;
public static partial class ArchiveTeaser
{
    public static double Benchmark(int count=12)
    {
        Bind();CreateBuffers(1920,1080);double beganAt=EditorApplication.timeSinceStartup;
        for(int i=0;i<count;i++){RenderFrame(100+i/30f);RenderTexture.active=composite;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0,false);}
        double elapsed=EditorApplication.timeSinceStartup-beganAt;RenderTexture.active=null;camera.targetTexture=null;ReleaseBuffers();return elapsed;
    }
    public static string Preview(float seconds,string name="preview",int w=1280,int h=720)
    {
        Bind();CreateBuffers(w,h);RenderFrame(seconds);RenderTexture.active=composite;pixels.ReadPixels(new Rect(0,0,w,h),0,0,false);File.WriteAllBytes(Output+"/EditSource/"+name+".png",pixels.EncodeToPNG());RenderTexture.active=null;if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);return Output+"/EditSource/"+name+".png";
    }
    static void CreateBuffers(int w,int h)
    {
        ReleaseBuffers();width=w;height=h;
        a=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);b=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);composite=new RenderTexture(w,h,0,RenderTextureFormat.ARGB32);
        a.Create();b.Create();composite.Create();pixels=new Texture2D(w,h,TextureFormat.RGB24,false);readback=new byte[w*h*3];
        blendMaterial=new Material(Shader.Find("Hidden/LogicLegends/OpeningReviewBlend"));
    }
    static void RenderFrame(float time)
    {
        var shot=timeline.shots.LastOrDefault(s=>s.start<=time)??timeline.shots[0];
        EvaluateShot(shot,time);camera.targetTexture=b;camera.Render();
        float mix=1;if(shot.dissolve>0&&time-shot.start<shot.dissolve)
        {
            int index=Array.IndexOf(timeline.shots,shot);if(index>0){var previous=timeline.shots[index-1];EvaluateShot(previous,previous.end-shot.dissolve*.5f+(time-shot.start)*.5f);camera.targetTexture=a;camera.Render();mix=Smooth((time-shot.start)/shot.dissolve);}
        }
        if(mix==1)Graphics.Blit(b,a);
        float fade=1;if(shot.fadeIn>0)fade*=Smooth(Mathf.Clamp01((time-shot.start)/shot.fadeIn));if(shot.fadeOut>0)fade*=Smooth(Mathf.Clamp01((shot.end-time)/shot.fadeOut));
        blendMaterial.SetTexture("_NextTex",b);blendMaterial.SetFloat("_Blend",mix);blendMaterial.SetFloat("_Fade",fade);Graphics.Blit(a,composite,blendMaterial);camera.targetTexture=null;
    }
    public static string StartRender(int w=1920,int h=1080,string filename="UnityFootage.mp4",int endFrame=0,int startFrame=0)
    {
        if(Application.isPlaying||Rendering)throw new Exception("Use Edit Mode with no active render.");
        original=SceneManager.GetActiveScene();Bind();if(timeline==null)throw new Exception("Prepare narration timeline first.");CreateBuffers(w,h);
        string executable=JsonUtility.FromJson<Tools>(File.ReadAllText(Output+"/EditSource/tools.json")).ffmpeg;
        string path=Path.GetFullPath(Output+"/EditSource/"+Path.GetFileName(filename));
        var info=new ProcessStartInfo(executable,"-hide_banner -loglevel error -y -filter_threads 1 -threads 2 -thread_queue_size 2 -f rawvideo -pixel_format rgb24 -video_size "+w+"x"+h+" -framerate 30 -i pipe:0 -vf vflip -an -c:v libx264 -threads 2 -preset fast -crf 12 -pix_fmt yuv420p -movflags +faststart \""+path+"\"");
        info.UseShellExecute=false;info.CreateNoWindow=true;info.RedirectStandardInput=true;info.RedirectStandardError=true;
        encoder=new Process{StartInfo=info};error="";encoder.ErrorDataReceived+=(sender,args)=>{if(!string.IsNullOrEmpty(args.Data))error+=args.Data+"\n";};encoder.Start();encoder.BeginErrorReadLine();
        total=Mathf.CeilToInt(timeline.duration*30);if(endFrame>0)total=Mathf.Min(total,endFrame);frame=Mathf.Clamp(startFrame,0,total-1);System.GC.Collect();began=EditorApplication.timeSinceStartup;EditorApplication.update-=RenderTick;EditorApplication.update+=RenderTick;return Status;
    }
    [Serializable]class Tools{public string ffmpeg;}
    static void RenderTick()
    {
        try
        {
            for(int i=0;i<4&&frame<total;i++)
            {
                RenderFrame(frame/30f);RenderTexture.active=composite;pixels.ReadPixels(new Rect(0,0,width,height),0,0,false);pixels.GetRawTextureData<byte>().CopyTo(readback);encoder.StandardInput.BaseStream.Write(readback,0,readback.Length);frame++;
            }
            RenderTexture.active=null;if(frame%60<4)File.WriteAllText(Output+"/EditSource/unity-render-status.json",Status);if(frame>=total)Finish();
        }
        catch(Exception e){error+=e.ToString();Finish();}
    }
    public static void Finish()
    {
        EditorApplication.update-=RenderTick;if(encoder!=null){try{encoder.StandardInput.Close();encoder.WaitForExit(15000);}catch(Exception e){error+=e.Message;}encoder.Dispose();encoder=null;}
        RenderTexture.active=null;if(camera!=null)camera.targetTexture=null;ReleaseBuffers();if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
        File.WriteAllText(Output+"/EditSource/unity-render-status.json",Status);
    }
    static void ReleaseBuffers(){foreach(var r in new[]{a,b,composite})if(r!=null){r.Release();UnityEngine.Object.DestroyImmediate(r);}a=b=composite=null;if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);if(blendMaterial!=null)UnityEngine.Object.DestroyImmediate(blendMaterial);}
}
