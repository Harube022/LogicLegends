using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OpenArchiveAuthoring
{
    public static class OpenArchivePreview
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Render Review Views")]
        public static void RenderAll()
        {
            // Render only the authored environment. Never unload another scene with unsaved work.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits before rendering review views.");
            EditorSceneManager.OpenScene(OpenArchiveBuilder.ScenePath, OpenSceneMode.Single);
            var go = new GameObject("Temporary_Review_Camera");
            var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.farClipPlane = 700;
            camera.nearClipPlane = .3f; camera.allowHDR = false; camera.allowMSAA = true;
            bool asynchronous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            // Preview the authored daylight even when the Editor is on the Very Low quality level.
            var previousShadows = QualitySettings.shadows;
            QualitySettings.shadows = ShadowQuality.All;
            try
            {
                Capture(camera, "OpenArchive_Aerial", new Vector3(85, 230, -160), new Vector3(0, -1, 0), true, 107, 1600, 1200);
                Capture(camera, "OpenArchive_Layout", new Vector3(0, 210, -1), Vector3.zero, true, 108, 1400, 1400);
                Capture(camera, "OpenArchive_FromSpawn", new Vector3(0, 7.5f, -82), new Vector3(0, 5, -3), false, 65, 1600, 900);
                Capture(camera, "OpenArchive_Nest", new Vector3(34, 34, -42), new Vector3(0, 2, 0), false, 62, 1600, 1100);
                Capture(camera, "OpenArchive_Nest_PlayerEye", new Vector3(-1.3f, 5.1f, -12.1f), new Vector3(.4f, 1.1f, 2), false, 78, 1800, 1100);
                Capture(camera, "OpenArchive_Nest_Detail", new Vector3(21, 17, -24), new Vector3(0, 1.1f, 0), false, 66, 1800, 1200);
                Capture(camera, "OpenArchive_Bridge", new Vector3(60, 13, -45), new Vector3(23, 1, -27), false, 65, 1600, 900);
            }
            finally { Object.DestroyImmediate(go); ShaderUtil.allowAsyncCompilation = asynchronous; QualitySettings.shadows = previousShadows; }
            AssetDatabase.Refresh();
        }

        static void Capture(Camera camera, string name, Vector3 position, Vector3 target, bool orthographic, float size, int width, int height)
        {
            camera.transform.position = position; camera.transform.LookAt(target);
            camera.orthographic = orthographic; camera.orthographicSize = size; camera.fieldOfView = size;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            var prior = RenderTexture.active; Texture2D pixels = null;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(OpenArchiveBuilder.Folder + "/Previews/" + name + ".png", pixels.EncodeToPNG());
            }
            finally { camera.targetTexture = null; RenderTexture.active = prior; if (pixels != null) Object.DestroyImmediate(pixels); rt.Release(); Object.DestroyImmediate(rt); }
        }
    }
}
