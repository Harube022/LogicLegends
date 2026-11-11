if(OpeningRemakeV2.Rendering)return "A cinematic render is still active.";
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerGameplay.unity",UnityEditor.SceneManagement.OpenSceneMode.Single);
return UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+" | Play="+UnityEditor.EditorApplication.isPlaying;
