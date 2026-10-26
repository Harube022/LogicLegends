#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in offline Play Mode navigation check; the existing fixture protects account data.</summary>
public static class SharedProofDecorationsChecks
{
    public const string Flag="ProofDecorations.QA";
    const string Report="Library/SharedProofDecorationsChecks.txt";
    public static void Begin()
    {
        File.WriteAllText(Report,"Shared proof chamber decoration Play Mode checks.\n");
        var shared=GameObject.Find("ChamberDecorations").transform.Find("Shared Chamber Decorations");
        Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(shared.gameObject)==ShareProofChamberDecorations.PrefabPath,"saved scene retains its shared prefab connection");
        SessionState.SetBool(Flag,true);
        DirectProofsWorldChecks.Begin();
    }
    public static IEnumerator WalkRoutes(DirectIndirectProofStageController stage,bool neutralAltar)
    {
        var root=GameObject.Find("ChamberDecorations");
        Require(root!=null,"organized decoration root exists");
        var shared=root.transform.Find("Shared Chamber Decorations");
        Require(shared!=null&&shared.GetComponentsInChildren<Light>().Count(l=>l.enabled)==8,"shared lights and art instantiated into Play Mode");
        Require(root.transform.Find("DirectAndIndirectAdditions")!=null,"stage-specific additions remain separate");
        Require(root.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"art has no gameplay scripts");
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            Require(r.sharedMaterials.All(m=>m!=null&&m.shader.isSupported),"supported material on "+r.name);
        Require(root.GetComponentsInChildren<Light>().Count(l=>l.enabled&&l.intensity>0&&l.range>0)==12,"all twelve warm lamp and crystal accent lights active");
        Require(root.GetComponentsInChildren<Collider>().Count(c=>c.enabled)==6,"only the four perimeter crystal displays and two pilasters add collision");
        var wall=GameObject.Find("Stage").GetComponent<MeshCollider>();
        foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("Alcove")||t.name.Contains("Pilaster")))
        {
            var origin=wall.bounds.center;origin.y=3.2f;
            var target=t.position;target.y=origin.y;var delta=target-origin;RaycastHit hit;
            Require(wall.Raycast(new Ray(origin,delta.normalized),out hit,50)&&hit.distance>delta.magnitude+1.25f,"perimeter display is fully inside the visible chamber: "+t.name);
        }
        // These use the real CharacterController: teleporting between stations would miss obstructions.
        Vector3[] route=neutralAltar?new[]{
            new Vector3(62.8f,0,63.6f),new Vector3(62.8f,0,55),new Vector3(63.2f,0,53),new Vector3(63.2f,0,38),
            new Vector3(56.7f,0,38),new Vector3(50.2f,0,38),new Vector3(50.2f,0,53),new Vector3(56.7f,0,55)
        }:new[]{
            new Vector3(56.7f,0,65),new Vector3(58.45f,0,63.6f),new Vector3(62.8f,0,63.6f),
            new Vector3(62.8f,0,55),new Vector3(67.7f,0,49.2f),new Vector3(56.7f,0,53),
            new Vector3(45.7f,0,49.2f),new Vector3(56.7f,0,53)
        };
        var player=Player.LocalInstance;int firstFrame=Time.frameCount;
        foreach(var waypoint in route)
        {
            var marker=new GameObject("Temporary navigation test destination").transform;
            marker.position=new Vector3(waypoint.x,player.transform.position.y,waypoint.z);
            player.BeginGuidedMovement(marker,5,.18f);
            float deadline=Time.realtimeSinceStartup+9;
            while(Vector3.ProjectOnPlane(player.transform.position-marker.position,Vector3.up).magnitude>.28f&&Time.realtimeSinceStartup<deadline)yield return null;
            player.EndGuidedMovement();
            float distance=Vector3.ProjectOnPlane(player.transform.position-marker.position,Vector3.up).magnitude;
            UnityEngine.Object.Destroy(marker.gameObject);
            Require(distance<.3f,"walkable route to "+waypoint+"; remaining="+distance);
            Require(player.transform.position.y>.8f&&player.transform.position.y<4,"player stays grounded on chamber floor");
        }
        Require(Time.frameCount>firstFrame+30,"Play Mode frames advance throughout actual walking");
        yield return new WaitForSecondsRealtime(.6f);yield return new WaitForEndOfFrame();
        var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
        File.WriteAllBytes("Library/DirectDecorations_"+(neutralAltar?"Ordering":"Classification")+"_PlayMode.png",image.EncodeToPNG());
        UnityEngine.Object.Destroy(image);
        File.AppendAllText(Report,"PASS "+(neutralAltar?"neutral ordering altar and routes around it":"spawn, Book Statue, both answer altars and board approach")+"; actual CharacterController walking, grounded floor, lit supported materials and isolated art.\n");
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Chamber decorations: "+message);}
}
#endif
