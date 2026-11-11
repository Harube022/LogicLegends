OpeningRemakeV2.Bind();
var s=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Cinematics/OpeningRemakeV2/LogicLegends_Opening_Remake.unity");
var cam=s.GetRootGameObjects().Single(r=>r.name=="Opening Review Camera").GetComponent<UnityEngine.Camera>();
cam.useOcclusionCulling=false;cam.ResetCullingMatrix();
OpeningRemakeV2.Preview(24,"realm-no-occlusion",1280,720);
return cam.useOcclusionCulling;
