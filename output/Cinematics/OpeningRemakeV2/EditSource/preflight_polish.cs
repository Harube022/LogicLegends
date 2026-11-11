OpeningRemakeV2.Bind();
var result=OpeningRemakeV2.Preview(106.5f,"polished-portal",1280,720);
var film=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(OpeningRemakeV2.ScenePath);
OpeningRemakeV2.Evaluate(0);UnityEditor.AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.SaveScene(film);
return result;
