OpeningRemakeV2.Bind();OpeningRemakeV2.Evaluate(0);
var review=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Cinematics/OpeningRemakeV2/LogicLegends_Opening_Remake.unity");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(review);
var gameplay=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/MultiplayerGameplay.unity");
if(gameplay.IsValid()&&gameplay.isLoaded)UnityEngine.SceneManagement.SceneManager.SetActiveScene(gameplay);
UnityEditor.SceneManagement.EditorSceneManager.CloseScene(review,true);
return new {active=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,gameplayDirty=gameplay.isDirty,reviewClosed=true};
