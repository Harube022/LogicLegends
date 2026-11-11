OpeningRemakeV2.Bind();
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(OpeningRemakeV2.ScenePath);
var sanctuary=scene.GetRootGameObjects().Single(r=>r.name=="Sanctuary");
return sanctuary.GetComponentsInChildren<UnityEngine.Transform>(true).Where(t=>t.parent==sanctuary.transform||t.GetComponent<UnityEngine.Renderer>()!=null).Select(t=>new {path=UnityEditor.AnimationUtility.CalculateTransformPath(t,sanctuary.transform),position=t.position.ToString(),rotation=t.localEulerAngles.ToString(),active=t.gameObject.activeSelf,type=t.GetComponent<UnityEngine.MeshFilter>()==null?"":t.GetComponent<UnityEngine.MeshFilter>().sharedMesh.name}).ToArray();
