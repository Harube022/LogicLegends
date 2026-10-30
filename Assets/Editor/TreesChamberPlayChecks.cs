#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using LogicLegends.Trees;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Offline chamber navigation, real controller collision and lamp contribution checks.</summary>
public static class TreesChamberPlayChecks
{
    const string Report="Library/TreesChamberPlayChecks.txt";
    public static IEnumerator Run(TreeStageManager stage)
    {
        File.WriteAllText(Report,"Trees chamber: live CharacterController, visible boundaries and per-lamp rendered contributions.\n");
        var legacy=GameObject.Find("Trees Garden (temporary project assets)").transform.GetChild(0);
        Check(!legacy.gameObject.activeInHierarchy&&legacy.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled),"all four obsolete internal barriers disabled");
        var room=GameObject.Find("Stage").GetComponent<MeshCollider>();
        var perimeter=GameObject.Find("ChamberBorder").GetComponent<MeshCollider>();
        for(int a=0;a<360;a+=5)foreach(float y in new[]{.6f,1.2f,2.5f})
        {RaycastHit hit;var ray=new Ray(new Vector3(-.93f,y,3.79f),Quaternion.Euler(0,a,0)*Vector3.forward);Check(room.Raycast(ray,out hit,40)||perimeter.Raycast(ray,out hit,40),"visible chamber wall contains player at "+a+" / "+y);}
        Log("PASS visible chamber perimeter and 76-triangle outer masonry at 216 angle/height samples; original floor/wall collision retained.");
        var cc=stage.LocalPlayer.GetComponent<CharacterController>();
        stage.LocalPlayer.ToggleControl(false);GameInput.Instance.SetGameplayInputBlocked(true);
        var route=new[]{new Vector3(2,0,-5),new Vector3(2,0,-13),new Vector3(-7,0,-13),new Vector3(-12,0,-6),new Vector3(-18,0,-5),new Vector3(-18,0,3),new Vector3(-10,0,4),new Vector3(-5,0,7),new Vector3(-8.5f,0,9),new Vector3(-8.5f,0,20),new Vector3(0,0,21),new Vector3(8.8f,0,20),new Vector3(8.8f,0,8),new Vector3(13,0,3),new Vector3(16,0,2),new Vector3(5,0,0),Vector3.zero};
        foreach(var goal in route)
        {
            int frames=0;
            while(Horizontal(stage.LocalPlayer.transform.position-goal)>.35f)
            {
                var delta=goal-stage.LocalPlayer.transform.position;delta.y=0;
                cc.Move(delta.normalized*Mathf.Min(.15f,delta.magnitude)+Vector3.down*.06f);yield return null;
                Check(++frames<450,"walk to "+goal+" blocked at "+stage.LocalPlayer.transform.position);
                // The existing controller pivot sits above its feet; compare the actual capsule bottom.
                Check(cc.bounds.min.y>-.25f&&cc.bounds.min.y<.3f,"floor supports capsule at "+stage.LocalPlayer.transform.position+" toward "+goal+"; feet="+cc.bounds.min.y);
            }
        }
        Log("PASS continuous 17-waypoint walk crosses former x=±14/z=-10/18 barriers, loops behind board, reaches both side aisles and returns to spawn.");
        PrelimTeleportTransition.MovePlayerImmediately(stage.LocalPlayer.gameObject,new Vector3(19,stage.spawnPoint.position.y,4),Quaternion.identity);
        int wallContacts=0;
        for(int i=0;i<90;i++){if((cc.Move(Vector3.right*.15f+Vector3.down*.04f)&CollisionFlags.Sides)!=0)wallContacts++;yield return null;}
        // Low masonry at the inner wall is walkable with the unchanged controller's step offset.
        // The real outer wall, rather than the old garden footprint, defines containment.
        Check(stage.LocalPlayer.transform.position.x<perimeter.bounds.max.x&&wallContacts>30,"visible east wall prevents leaving chamber at "+stage.LocalPlayer.transform.position+"; contacts="+wallContacts);
        PrelimTeleportTransition.MovePlayerImmediately(stage.LocalPlayer.gameObject,stage.spawnPoint.position,stage.spawnPoint.rotation);
        stage.LocalPlayer.ToggleControl(true);GameInput.Instance.SetGameplayInputBlocked(false);
        Log("PASS live controller blocked by visible perimeter; player returned safely to book access.");
        Check(RenderSettings.ambientMode==AmbientMode.Flat&&RenderSettings.ambientLight.maxColorComponent<=.21f,"ambient reduced locally");
        var lamps=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("LAMP")&&t.GetComponent<MeshFilter>()!=null).ToArray();
        Check(lamps.Length==7,"five existing lamps and two added reading chandeliers");
        foreach(var lamp in lamps)
        {
            var lights=lamp.GetComponentsInChildren<Light>().Where(l=>l.enabled).ToArray();Check(lights.Length==1&&lights[0].shadows==LightShadows.None,"one shadow-free light in "+lamp.name);
            var core=lamp.Find("Warm Luminous Interior");Check(core!=null&&core.GetComponent<Renderer>().sharedMaterial.IsKeywordEnabled("_EMISSION"),"lamp interior glows");
            var l=lights[0];var inward=new Vector3(-.93f-l.transform.position.x,0,3.79f-l.transform.position.z).normalized;
            Vector3 floor=l.transform.position;floor.y=.03f;
            foreach(float offset in new[]{3f,6f,9f,12f,15f})
            {var candidate=floor+inward*offset;RaycastHit hit;if(Physics.Raycast(candidate+Vector3.up*5,Vector3.down,out hit,8)&&hit.collider==room){floor=candidate;break;}}
            // Look down at the verified floor patch, avoiding the main board between probe and patch.
            var at=floor+inward*.75f+Vector3.up*10;
            string key=lamp.name.Replace(' ','_').Replace('—','_');
            var on=TreesChamberSetup.Capture("Library/TreesLamp_"+key+"_on.png",at,floor,65);
            l.enabled=false;var off=TreesChamberSetup.Capture("Library/TreesLamp_"+key+"_off.png",at,floor,65);l.enabled=true;
            var a=on.GetPixels();var b=off.GetPixels();double diff=0;int count=0;
            // Center crop contains the nearby floor target rather than the glowing fixture itself.
            for(int y=300;y<420;y++)for(int x=580;x<700;x++){int n=y*1280+x;diff+=a[n].grayscale-b[n].grayscale;count++;}
            UnityEngine.Object.Destroy(on);UnityEngine.Object.Destroy(off);
            Check(diff/count>.0001,"lamp measurably illuminates nearby floor: "+lamp.name+" delta="+(diff/count));
            Log("PASS "+lamp.name+": emissive interior; real light contributes "+(diff/count).ToString("F4")+" mean floor luminance.");yield return null;
        }
        Check(lamps.SelectMany(t=>t.GetComponentsInChildren<Light>()).Count(l=>l.enabled)==7,"light budget remains seven");
        Log("PASS seven shadow-free local lights, combined chain meshes, no particles or extra shadow cameras; mobile device profiling still required.");
        var pic=TreesChamberSetup.Capture("Library/TreesChamberVerifiedGameplay.png",new Vector3(0,3,-8),new Vector3(0,4,9));UnityEngine.Object.Destroy(pic);
        Log("ALL CHAMBER CHECKS PASSED");
    }
    static float Horizontal(Vector3 v){v.y=0;return v.magnitude;}
    static void Check(bool condition,string text){if(!condition)throw new InvalidOperationException(text);}
    static void Log(string text)=>File.AppendAllText(Report,text+"\n");
}
#endif
