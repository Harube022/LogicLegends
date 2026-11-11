OpeningRemakeV2.Bind();
var film=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(OpeningRemakeV2.ScenePath);
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/FINAL_ASSETS/MAPS/CINEMATIC/Tests.fbx");
var paper=UnityEngine.Object.Instantiate(prefab);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(paper,film);paper.transform.position=UnityEngine.Vector3.zero;
var tests=paper.GetComponentsInChildren<UnityEngine.Renderer>(true).Select(r=>new{name=r.name,center=r.bounds.center.ToString(),size=r.bounds.size.ToString(),rotation=r.transform.rotation.eulerAngles.ToString(),materials=r.sharedMaterials.Select(m=>m.name).ToArray()}).ToArray();
UnityEngine.Object.DestroyImmediate(paper);
var wizard=film.GetRootGameObjects().Single(r=>r.name=="Cinematic Lefyahj");var anim=wizard.GetComponentInChildren<UnityEngine.Animator>(true);var hips=anim.GetComponentsInChildren<UnityEngine.Transform>(true).Single(t=>t.name.EndsWith(":Hips"));
var clips=new[]{"Wizard_Walking","Wizard_Walking1"};var samples=new System.Collections.Generic.List<object>();
var pos=wizard.transform.position;var rot=wizard.transform.rotation;wizard.SetActive(true);wizard.transform.SetPositionAndRotation(UnityEngine.Vector3.zero,UnityEngine.Quaternion.identity);
foreach(var name in clips){var clip=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/Characters_NEW/LATEST_SKINS/Wizard/Animations/"+name+".anim");foreach(var time in new[]{0f,.25f,.5f,.75f,clip.length}){clip.SampleAnimation(anim.gameObject,time);samples.Add(new{clip=name,time=time,hips=hips.position.ToString("F5"),left=anim.GetComponentsInChildren<UnityEngine.Transform>(true).Single(t=>t.name.EndsWith(":LeftFoot")).position.ToString("F5"),right=anim.GetComponentsInChildren<UnityEngine.Transform>(true).Single(t=>t.name.EndsWith(":RightFoot")).position.ToString("F5")});}}
wizard.transform.SetPositionAndRotation(pos,rot);OpeningRemakeV2.Evaluate(0);
System.IO.File.WriteAllText("output/Cinematics/OpeningRemakeV2/EditSource/papers-walk-audit.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{tests=tests,rootScale=wizard.transform.lossyScale.ToString(),modelScale=anim.transform.lossyScale.ToString(),hipsParentScale=hips.parent.lossyScale.ToString(),modelRotation=anim.transform.localEulerAngles.ToString(),samples=samples},Newtonsoft.Json.Formatting.Indented));
return "Audited original Tests.fbx and Wizard foot motion";
