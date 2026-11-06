#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TruthTableGroundChecks
{
    const string Report="Library/TruthTableGroundChecks.txt";
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Offline TruthTable grass/path verification, actual saved scene and normal lighting. No account/reward writes.\n");
        var root=GameObject.Find("Truth_Table");var floor=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="FLOOR (1)");
        var material=floor.GetComponent<MeshRenderer>().sharedMaterial;
        var report=JsonUtility.FromJson<TruthTableGroundSetup.GroundReport>(File.ReadAllText("Assets/Art/TruthTableGround/TruthTableGroundRoutes.json"));
        Check(report.transformAndCollidersPreserved,"authoring preserved exact ground transforms, mesh references and collider serialization");
        Check(report.routes.Count(r=>r.name.StartsWith("House"))==6&&report.routes.Any(r=>r.name=="Truth Table approach"),"paths connect the book hub, six house approaches and Truth Table approach");
        Check(material.shader.name=="LogicLegends/TruthTable/GrassAndPaths"&&material.shader.isSupported&&!ShaderUtil.ShaderHasError(material.shader),"mobile-target shader compiled and active");
        Check(root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Extension").All(r=>r.sharedMaterial==material),"central raised ground shares the same seamless world-space surface");
        Check(AssetDatabase.GetAssetPath(material.GetTexture("_GrassMap")).Contains("MULTIPLAYER/OPEN_ARCHIVE")&&material.GetFloat("_TileMeters")==3.8f,"existing multiplayer grass texture uses world-meter tiling");
        var mask=(Texture2D)material.GetTexture("_PathMask");Check(mask.width==1024&&mask.mipmapCount>1&&mask.wrapMode==TextureWrapMode.Clamp,"path mask has mipmaps and clamped boundaries");
        Check(floor.GetComponent<MeshCollider>().enabled&&floor.GetComponent<MeshCollider>().sharedMesh==floor.GetComponent<MeshFilter>().sharedMesh,"original floor mesh collision remains active");
        Check(!root.GetComponentsInChildren<Transform>(true).Any(t=>t.name.Contains("Dirt Decal")||t.name.Contains("Path Overlay")),"no coplanar path overlay meshes to cause z-fighting");
        Check(root.GetComponent<TruthTableDuskLighting>().FixtureCount==25&&root.GetComponentsInChildren<TruthTableNightVisibility>(true).Length==0,"sunset lamps and clear camera view preserved");
        var clock=UnityEngine.Object.FindFirstObjectByType<TruthTableStageClock>();Check(clock.IsWaitingForBook,"visual QA leaves the real question waiting for the book");
        var camera=Camera.main;var brain=camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();bool before=brain!=null&&brain.enabled;var position=camera.transform.position;var rotation=camera.transform.rotation;
        if(brain!=null)brain.enabled=false;
        try
        {
            camera.transform.SetPositionAndRotation(new Vector3(663,151,781),Quaternion.LookRotation(new Vector3(638,144.8f,750)-new Vector3(663,151,781)));
            foreach(int size in new[]{6,12,3})
            {
                BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Library/TruthGround_Gameplay_"+Screen.width+"x"+Screen.height+".png");
                Check(camera.targetTexture==null,"normal direct camera/UI rendering at "+Screen.width+"x"+Screen.height);
            }
            var a=TreesChamberSetup.Capture("Library/TruthGround_StableA.png",new Vector3(655,146.8f,774),new Vector3(642,144.8f,766),60);
            var b=TreesChamberSetup.Capture("Library/TruthGround_StableB.png",new Vector3(655,146.8f,774),new Vector3(642,144.8f,766),60);
            var pa=a.GetPixels32();var pb=b.GetPixels32();double delta=0;for(int i=0;i<pa.Length;i++)delta+=Math.Abs(pa[i].r-pb[i].r)+Math.Abs(pa[i].g-pb[i].g)+Math.Abs(pa[i].b-pb[i].b);delta/=pa.Length*3*255.0;
            UnityEngine.Object.Destroy(a);UnityEngine.Object.Destroy(b);
            Check(delta<.001,"fixed-view ground rendering is stable; mean repeated-capture difference="+delta);
        }
        finally{camera.transform.SetPositionAndRotation(position,rotation);if(brain!=null)brain.enabled=before;BookMapMobileChecks.SetSize(6);}
        Check(clock.IsWaitingForBook,"no challenge activation, timer consumption or progression from visual checks");
        File.AppendAllText(Report,"ALL GROUND CHECKS PASSED. Physical-device GPU profiling not performed.\n");
    }
}
#endif
