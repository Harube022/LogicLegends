#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Library-only presentation; never edits the garden's environment or shared source materials.</summary>
public static class StudyLibraryPresentationSetup
{
    const string Folder = "Assets/Resources/StudyLibrary/";
    [MenuItem("Logic Legends/Library/Update Library Presentation")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "LogicGarden" || Application.isPlaying) throw new System.InvalidOperationException("Open LogicGarden outside Play Mode.");
        var library = Object.FindFirstObjectByType<StudyLibraryController>();
        var room = GameObject.Find("LogicGarden_Assets/FLOOR (2)");
        if (library == null || room == null) throw new System.InvalidOperationException("Study library or room is missing.");
        Undo.RecordObject(library.gameObject, "Improve Study Library presentation");
        var lighting = library.GetComponent<StudyLibraryLighting>();
        if (lighting == null) lighting = Undo.AddComponent<StudyLibraryLighting>(library.gameObject);
        Undo.RecordObject(lighting, "Scope dim library atmosphere");
        lighting.sunlight = RenderSettings.sun;
        lighting.chamberBounds = new[] { new Bounds(new Vector3(-125.84f, 35, 669.6f), new Vector3(67, 36, 68)) };
        lighting.chamberNames = new[] { "Study Library" };
        lighting.ambientColor = new Color(.36f, .325f, .28f);
        lighting.sunlightIntensity = .16f; lighting.reflectionIntensity = .24f;
        var lamps = room.GetComponentsInChildren<Light>(true);
        lighting.torches = lamps;
        foreach (var light in lamps)
        {
            Undo.RecordObject(light, "Balance warm library lamps");
            bool low = light.transform.position.y < 33;
            light.intensity = low ? 40 : 85; light.range = low ? 22 : 32;
            light.color = new Color(1, .71f, .40f);
            light.shadows = LightShadows.None; light.renderMode = LightRenderMode.Auto;
            EditorUtility.SetDirty(light);
        }
        // Chandelier candles remain local glows, rather than expensive overlapping room lights.
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(l => l.transform.parent != null && l.transform.parent.name.StartsWith("Candle Flame") && lighting.Contains(l.transform.position)))
        {
            Undo.RecordObject(light, "Balance library candle glow");
            light.intensity = 2.2f; light.range = 6; light.color = new Color(1,.66f,.30f); light.shadows = LightShadows.None;
        }
        AddSoftFill(library.transform);
        var glass = AssetDatabase.LoadAssetAtPath<Material>(Folder + "LibraryTintedGlass.mat");
        if (glass == null)
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/CHEATCODE/DUNGEON/LowPolyDungeon/Materials/Glass.mat");
            glass = new Material(original) { name = "Library Black Tinted Glass" };
            AssetDatabase.CreateAsset(glass, Folder + "LibraryTintedGlass.mat");
        }
        glass.SetColor("_BaseColor", new Color(.018f,.022f,.026f,.48f));
        glass.SetTexture("_BaseMap", null);
        glass.SetFloat("_Blend", 0); glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        glass.SetFloat("_Metallic", 0); glass.SetFloat("_Smoothness", .35f);
        glass.SetFloat("_Cull", 0); glass.SetFloat("_ZWrite", 0); glass.renderQueue = 3000;
        glass.SetFloat("_EnvironmentReflections", 0); glass.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        glass.SetFloat("_SpecularHighlights", 0); glass.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "LibraryArchedGlass.asset");
        if (mesh == null)
        {
            // A pointed arch follows the imported opening. Its edges sit inside
            // the opaque reveal, behind the original tracery on both faces.
            mesh = new Mesh { name = "Library pointed window glass" };
            var outline = new[] { new Vector2(-5.3f,0),new Vector2(5.3f,0),new Vector2(5.3f,10.1f),
                new Vector2(4.8f,15.3f),new Vector2(3,17.2f),new Vector2(0,19.5f),
                new Vector2(-3,17.2f),new Vector2(-4.8f,15.3f),new Vector2(-5.3f,10.1f) };
            mesh.vertices = outline.Select(p => new Vector3(p.x,p.y,0)).ToArray();
            mesh.uv = outline.Select(p => new Vector2((p.x+4.3f)/8.6f,p.y/16.1f)).ToArray();
            var triangles = new int[(outline.Length-2)*3];
            for (int i=0;i<outline.Length-2;i++) { triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2; }
            mesh.triangles=triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Folder + "LibraryArchedGlass.asset");
        }
        var parent = library.transform.Find("Library Window Glass");
        if (parent == null) { var go=new GameObject("Library Window Glass");Undo.RegisterCreatedObjectUndo(go,"Create library glazing");go.transform.SetParent(library.transform,false);parent=go.transform; }
        foreach (var z in new[] { 700.3f,638.92f })
        {
            string name = z > 670 ? "North Tinted Window" : "South Tinted Window";
            var pane=parent.Find(name);
            if (pane==null) { var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));Undo.RegisterCreatedObjectUndo(go,"Fit window glass");go.transform.SetParent(parent,false);pane=go.transform; }
            pane.position=new Vector3(-125.84f,24.35f,z);pane.rotation=Quaternion.identity;pane.localScale=Vector3.one;
            pane.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=pane.GetComponent<MeshRenderer>();renderer.sharedMaterial=glass;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        EditorUtility.SetDirty(glass);AssetDatabase.SaveAssetIfDirty(glass);
        EditorUtility.SetDirty(lighting);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Study Library: scoped atmosphere, six balanced lamps, chandelier glows and two pointed tinted windows.");
    }

    static void AddSoftFill(Transform library)
    {
        var parent=library.Find("Library Fill Lights");
        if(parent==null)
        {
            var go=new GameObject("Library Fill Lights");Undo.RegisterCreatedObjectUndo(go,"Add soft library fill");
            go.transform.SetParent(library,false);parent=go.transform;
        }
        for(int i=0;i<2;i++)
        {
            string name=i==0?"South Aisle Fill":"North Aisle Fill";
            var child=parent.Find(name);
            if(child==null)
            {
                var go=new GameObject(name,typeof(Light));Undo.RegisterCreatedObjectUndo(go,"Add library aisle fill");
                go.transform.SetParent(parent,false);child=go.transform;
            }
            var light=child.GetComponent<Light>();Undo.RecordObject(light,"Balance soft aisle fill");
            child.position=new Vector3(-125.84f,29,i==0?658:684);
            light.type=LightType.Point;light.color=new Color(1,.86f,.69f);
            light.intensity=18;light.range=23;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.Auto;
        }
    }

    public static string VerifyWindowsAndLighting()
    {
        var lighting = Object.FindFirstObjectByType<StudyLibraryLighting>();
        var panes = lighting.transform.Find("Library Window Glass").GetComponentsInChildren<MeshRenderer>();
        if (panes.Length != 2 || panes.Any(p => p.GetComponent<Collider>() != null)) throw new System.InvalidOperationException("Expected two decorative glass panes without collision changes.");
        var sun = lighting.sunlight; var ambient = RenderSettings.ambientSkyColor;
        float intensity = sun.intensity;
        var go = new GameObject("Library window QA camera"); var camera = go.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 13; camera.nearClipPlane = .1f; camera.farClipPlane = 200;
        var rt = new RenderTexture(512,512,24); camera.targetTexture = rt;
        var readback = new Texture2D(512,512,TextureFormat.RGB24,false);
        var previous = RenderTexture.active; var report = new System.Collections.Generic.List<string>();
        try
        {
            foreach (var pane in panes)
            {
                int direction = pane.transform.position.z > 670 ? 1 : -1;
                camera.transform.SetPositionAndRotation(new Vector3(-125.84f,33,670),Quaternion.LookRotation(Vector3.forward*direction));
                pane.enabled=false; camera.Render(); RenderTexture.active=rt;
                readback.ReadPixels(new Rect(0,0,512,512),0,0);readback.Apply();var clear=readback.GetPixels();
                pane.enabled=true;camera.Render();readback.ReadPixels(new Rect(0,0,512,512),0,0);readback.Apply();var tinted=readback.GetPixels();
                int scenery=0, darkened=0;
                for(int y=90;y<425;y++)for(int x=160;x<352;x++)
                {
                    int pixel=y*512+x;
                    if(clear[pixel].b>.25f && clear[pixel].b>clear[pixel].r*1.3f)
                    { scenery++;if(tinted[pixel].grayscale<clear[pixel].grayscale*.92f)darkened++; }
                }
                if(scenery<200 || darkened<scenery*.95f)throw new System.InvalidOperationException(pane.name+": incomplete tint coverage "+darkened+"/"+scenery);
                System.IO.File.WriteAllBytes("Library/"+pane.name.Replace(" ","")+".png",readback.EncodeToPNG());
                report.Add(pane.name+": visible scenery remains behind uniform tint ("+darkened+"/"+scenery+" pixels).");
                if(RenderSettings.ambientSkyColor!=ambient || sun.intensity!=intensity)throw new System.InvalidOperationException("Camera left global outdoor lighting changed.");
            }
            camera.transform.position=new Vector3(0,15,-20);camera.transform.rotation=Quaternion.identity;
            int count=lighting.ScopedRenderCount;camera.Render();
            if(lighting.ScopedRenderCount!=count || sun.intensity!=intensity || RenderSettings.ambientSkyColor!=ambient)
                throw new System.InvalidOperationException("Library atmosphere leaked into the garden camera.");
            report.Add("Outdoor garden camera keeps its original sunlight and ambient light. All library render scopes restore global lighting.");
            return string.Join("\n",report);
        }
        finally
        {
            foreach(var pane in panes)pane.enabled=true;
            RenderTexture.active=previous;camera.targetTexture=null;
            Object.DestroyImmediate(go);Object.DestroyImmediate(rt);Object.DestroyImmediate(readback);
        }
    }
}
#endif
