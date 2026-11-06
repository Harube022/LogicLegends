#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>Real capsule movement and joystick axes in an isolated saved-scene session.</summary>
public static class TruthTablePlatformStairsChecks
{
    const string Report = "Library/TruthTablePlatformStairsChecks.txt";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static void Check(bool pass, string message)
    { if (!pass) throw new InvalidOperationException(message); File.AppendAllText(Report,"PASS "+message+"\n"); }
    static void Mobile(Vector2 direction)
    {
        var ui = UnityEngine.Object.FindFirstObjectByType<MobileInputUI>();
        if(ui != null)typeof(MobileInputUI).GetField("inputVector",Hidden).SetValue(ui,direction);
        GameInput.Instance.SetMobileMovement(direction);
    }
    static void Place(Vector3 feet)
    {
        var player=Player.LocalInstance;var cc=player.GetComponent<CharacterController>();
        cc.enabled=false;player.transform.position=feet+Vector3.up*(cc.height*.5f-cc.center.y+.12f);cc.enabled=true;Physics.SyncTransforms();
    }
    static RaycastHit Ground(Vector3 at)
    {
        // The central island is Extension above FLOOR's river bed.
        return Physics.RaycastAll(new Vector3(at.x,150.3f,at.z),Vector3.down,30,~0,QueryTriggerInteraction.Ignore)
            .Where(h=>h.collider is MeshCollider&&h.normal.y>.7f).OrderBy(h=>h.distance).First();
    }
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Truth Table platform: real saved scene, mobile joystick axes, no jump input during walking. Offline test session; no rewards or saved progress.\n");
        var table=GameObject.Find("TRUTIBOL").GetComponent<MeshCollider>();
        var ramp=GameObject.Find(TruthTablePlatformStairsSetup.ObjectName).GetComponent<MeshCollider>();
        var floor=GameObject.Find("FLOOR (1)").GetComponent<MeshCollider>();
        var player=Player.LocalInstance;var cc=player.GetComponent<CharacterController>();
        float speed=player.moveSpeed,step=cc.stepOffset,slope=cc.slopeLimit;
        float jump=(float)typeof(Player).GetField("jumpForce",Hidden).GetValue(player);
        int jumps=0;Action onJump=()=>jumps++;player.OnJumpStarted+=onJump;
        var camera=Camera.main;var brain=camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        bool brainOn=brain!=null&&brain.enabled;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;
        if(brain!=null)brain.enabled=false;
        var clock=UnityEngine.Object.FindFirstObjectByType<TruthTableStageClock>();float time=clock.RemainingSeconds;
        try
        {
            Check(ramp.GetComponent<Renderer>()==null&&!ramp.convex&&!ramp.isTrigger,"one invisible precise walking surface; original stairs remain visible");
            Check(GameObject.Find("TruthTable Collision Repairs").GetComponentsInChildren<MeshCollider>().Count(c=>c.name==TruthTablePlatformStairsSetup.ObjectName)==1,"one platform ramp, no duplicate colliders");
            foreach(float x in new[]{624f,632f,643f})
                foreach(float angle in new[]{-30f,0f,30f})
                {
                    var direction=Quaternion.Euler(0,angle,0)*Vector3.back;
                    float outside=ramp.bounds.max.z+1.5f,inside=ramp.bounds.min.z-1.2f;
                    float travel=(outside-inside)/-direction.z;
                    var start=new Vector3(x-direction.x*travel,0,outside);var ground=Ground(start);
                    Check(ground.collider!=null,"approach ground at "+x+" / "+angle+" on "+ground.collider.name);
                    Place(ground.point);camera.transform.rotation=Quaternion.LookRotation(direction);
                    Mobile(Vector2.zero);yield return new WaitForSecondsRealtime(.4f);
                    Mobile(Vector2.up);float by=Time.realtimeSinceStartup+4;
                    while(player.transform.position.z>inside&&Time.realtimeSinceStartup<by)yield return null;
                    Mobile(Vector2.zero);yield return new WaitForSecondsRealtime(.25f);
                    Check(player.transform.position.z<=inside+.1f&&cc.bounds.min.y>147,"walk up at "+x+" / "+angle+" degrees: at="+player.transform.position+" feet="+cc.bounds.min.y);
                    Check(jumps==0,"ascent uses no jumping");
                    Check(clock.IsPlayerAtBoard,"existing board proximity still detects arrival");
                    Mobile(Vector2.down);by=Time.realtimeSinceStartup+4;
                    while(player.transform.position.z<outside&&Time.realtimeSinceStartup<by)yield return null;
                    Mobile(Vector2.zero);yield return new WaitForSecondsRealtime(.4f);
                    Check(player.transform.position.z>=outside-.1f&&Mathf.Abs(cc.bounds.min.y-ground.point.y)<.2f,"walk down and settle on grass at "+x+" / "+angle+" degrees");
                    Check(!clock.IsPlayerAtBoard,"board proximity clears after descending");
                    Check(cc.stepOffset==step&&cc.slopeLimit==slope,"normal player slope and step settings preserved on the table stairs");
                }
            // Sample just beyond every outside boundary: the new ramp must never support the player there.
            foreach(var at in new[]{new Vector3(table.bounds.min.x-1.3f,0,ramp.bounds.center.z),new Vector3(table.bounds.max.x+1.3f,0,ramp.bounds.center.z),new Vector3(632,0,ramp.bounds.max.z+1.4f)})
            {
                RaycastHit support;Check(!ramp.Raycast(new Ray(at+Vector3.up*180,Vector3.down),out support,80),"no ramp collision outside the visible stair footprint at "+at);
                var ground=Ground(at);Place(ground.point);yield return new WaitForSecondsRealtime(.5f);
                Check(Mathf.Abs(cc.bounds.min.y-ground.point.y)<.2f,"player stands on the actual ground beside the platform");
            }
            Place(new Vector3(632,147.0543f,747));yield return new WaitForSecondsRealtime(.4f);
            float before=player.transform.position.y,peak=before;GameInput.Instance.MobileJump();float until=Time.realtimeSinceStartup+2;
            while(Time.realtimeSinceStartup<until){peak=Mathf.Max(peak,player.transform.position.y);yield return null;}
            Check(jumps==1&&peak-before>1&&cc.isGrounded,"jump button still launches and lands normally on the platform");
            Check(player.moveSpeed==speed&&(float)typeof(Player).GetField("jumpForce",Hidden).GetValue(player)==jump,"movement speed and jump force unchanged");
            Check(clock.IsWaitingForBook&&clock.RemainingSeconds==time,"walking and jumping do not start a question or timer");
            clock.StartFromBook();yield return null;yield return null;
            Check(clock.IsRunning&&clock.IsPlayerAtBoard&&clock.IsCountdownPaused,"real book start and existing board timer pause still work");
            var puzzle=UnityEngine.Object.FindObjectsByType<DynamicLogicPuzzle>(FindObjectsSortMode.None).First(p=>p.IsTruthTableHintSource);
            Check((bool)typeof(DynamicLogicPuzzle).GetField("isPlayerNear",Hidden).GetValue(puzzle)&&clock.CurrentChallengeNumber==1,"Truth Table placement proximity and first challenge remain intact");
            File.AppendAllText(Report,"ALL PLATFORM STAIR CHECKS PASSED\n");
        }
        finally
        {
            Mobile(Vector2.zero);player.OnJumpStarted-=onJump;
            camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);if(brain!=null)brain.enabled=brainOn;
        }
    }
}
#endif
