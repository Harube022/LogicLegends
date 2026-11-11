OpeningRemakeV2.Bind();OpeningRemakeV2.ReframePaperLayout();
var film=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(OpeningRemakeV2.ScenePath);
var root=film.GetRootGameObjects().Single(r=>r.name=="Cinematic Lefyahj").transform;
var feet=root.GetComponentsInChildren<UnityEngine.Transform>(true).Where(t=>t.name.EndsWith(":LeftFoot")||t.name.EndsWith(":RightFoot")).ToArray();
var samples=new System.Collections.Generic.List<object>();
foreach(var time in new[]{96.3f,96.5f,96.7f,96.9f,97.1f,97.3f,97.5f,97.7f,97.9f,98.1f,98.3f,115.9f,116.1f,116.3f,116.5f,116.7f,116.9f,117.1f,117.3f,117.5f})
{
    OpeningRemakeV2.Evaluate(time);samples.Add(new{time=time,position=root.position.ToString("F4"),yaw=root.eulerAngles.y,left=feet[0].position.ToString("F4"),right=feet[1].position.ToString("F4")});
}
System.IO.File.WriteAllText("output/Cinematics/OpeningRemakeV2/EditSource/wizard-polished-motion.json",Newtonsoft.Json.JsonConvert.SerializeObject(samples,Newtonsoft.Json.Formatting.Indented));
foreach(var time in new[]{77.8f,89.8f,96.7f,97.3f,97.8f,101.9f,116.2f,116.7f,117.2f})OpeningRemakeV2.Preview(time,"polished-"+time.ToString("F1",System.Globalization.CultureInfo.InvariantCulture),1280,720);
return "Previewed corrected F-grade framing and recorded Wizard foot/root/turn samples.";
