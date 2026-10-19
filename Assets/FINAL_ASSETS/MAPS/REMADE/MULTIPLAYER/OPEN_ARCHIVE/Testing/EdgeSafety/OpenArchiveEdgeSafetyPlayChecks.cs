#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LogicLegends.OpenArchive.Hunting;
using LogicLegends.OpenArchive.Water;
using LogicLegends.OpenArchiveTesting;
using Photon.Pun;
using UnityEngine;

namespace LogicLegends.Tests.OpenArchive
{
    public sealed class OpenArchiveEdgeSafetyPlayChecks : MonoBehaviour
    {
        [Serializable] sealed class Check {public string name,detail;public bool passed;}
        [Serializable] sealed class Report {public string checkedUtc,scope="Offline Unity Editor: original enabled Player, CharacterController, GameInput movement/jump events, eight edge directions. Android and live multiplayer not exercised.";public bool passed;public List<Check> checks=new List<Check>();}
        readonly Report report=new Report();Player player;CharacterController capsule;Transform root;
        public static void Begin(){if(!Application.isPlaying||PhotonNetwork.InRoom)throw new InvalidOperationException("Use offline Play Mode.");new GameObject("OpenArchive_Edge_Safety_Checks").AddComponent<OpenArchiveEdgeSafetyPlayChecks>();}
        static Vector3 Polar(float angle,float radius){angle*=Mathf.Deg2Rad;return new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius);}
        void Record(string name,bool passed,string detail=""){report.checks.Add(new Check{name=name,passed=passed,detail=detail});}
        void Input(Vector3 direction){var f=Camera.main.transform.forward;f.y=0;f.Normalize();var r=Camera.main.transform.right;r.y=0;r.Normalize();GameInput.Instance.SetMobileMovement(new Vector2(Vector3.Dot(direction,r),Vector3.Dot(direction,f)));}
        void Place(float angle){
            var point=root.TransformPoint(Polar(angle,94));var ground=root.Find("Environment/Terrain/Island_Grassland_WithRiverHoles").GetComponent<Collider>();RaycastHit hit;
            if(!ground.Raycast(new Ray(point+Vector3.up*40,Vector3.down),out hit,80))throw new InvalidOperationException("Missing outer grass support.");
            point.y=hit.point.y-(capsule.center.y-capsule.height*.5f)*player.transform.lossyScale.y+capsule.skinWidth;
            capsule.enabled=false;player.transform.position=point;capsule.enabled=true;player.ResetMotionAfterTeleport();Physics.SyncTransforms();
        }
        float Radius(){var p=root.InverseTransformPoint(player.transform.position);return new Vector2(p.x,p.z).magnitude;}
        IEnumerator Start(){
            yield return null;player=Player.LocalInstance;capsule=player.GetComponent<CharacterController>();root=GameObject.Find("OpenArchive").transform;
            var session=FindFirstObjectByType<OpenArchiveExplorationSession>();var hunt=FindFirstObjectByType<KhezuHuntingController>();var sinking=FindFirstObjectByType<OpenArchiveWaterSinking>();
            bool sessionEnabled=session.enabled,sinkingEnabled=sinking.enabled,oldInput=GameInput.Instance.GameplayInputBlocked;var oldPosition=player.transform.position;var oldRotation=player.transform.rotation;
            report.checkedUtc=DateTime.UtcNow.ToString("o");
            try{
                session.enabled=false;sinking.enabled=false;hunt.Request(KhezuCommand.Sleep);GameInput.Instance.SetGameplayInputBlocked(false);
                for(int i=0;i<8;i++){
                    float angle=i*45f;Input(Vector3.zero);Place(angle);yield return new WaitForSeconds(.15f);var direction=root.TransformDirection(Polar(angle,1));
                    float end=Time.time+2;float largest=Radius(),lowest=player.transform.position.y;
                    while(Time.time<end){Input(direction);largest=Mathf.Max(largest,Radius());lowest=Mathf.Min(lowest,player.transform.position.y);yield return null;}
                    Record("Edge "+angle+": outward walking is blocked",largest<98.1f&&lowest>-.2f,"maxLocalRadius="+largest+", minY="+lowest);
                    Place(angle);yield return new WaitForSeconds(.15f);player.OnRun();end=Time.time+2.4f;bool jumped=false,ran=false;float startY=player.transform.position.y,maxY=startY;largest=Radius();
                    while(Time.time<end){Input(direction);ran|=player.IsRunning();if(!jumped&&player.IsRunning()){GameInput.Instance.MobileJump();jumped=true;}largest=Mathf.Max(largest,Radius());maxY=Mathf.Max(maxY,player.transform.position.y);yield return null;}
                    Record("Edge "+angle+": running jump cannot cross the cliff boundary",ran&&jumped&&largest<98.1f&&maxY>startY+.5f&&player.transform.position.y>-.2f,"maxLocalRadius="+largest+", jumpRise="+(maxY-startY)+", running="+ran);
                }
                Record("Original controls remain enabled",player.enabled&&capsule.enabled&&!player.IsMovementLocked);
            }
            finally{
                Input(Vector3.zero);capsule.enabled=false;player.transform.SetPositionAndRotation(oldPosition,oldRotation);capsule.enabled=true;player.ResetMotionAfterTeleport();
                session.enabled=sessionEnabled;sinking.enabled=sinkingEnabled;GameInput.Instance.SetGameplayInputBlocked(oldInput);
                report.passed=report.checks.Count==17&&report.checks.TrueForAll(c=>c.passed);File.WriteAllText("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/EdgeSafety/Reports/EdgeSafetyPlayMode.json",JsonUtility.ToJson(report,true));
                Debug.Log("Open Archive edge safety: "+report.checks.FindAll(c=>c.passed).Count+"/"+report.checks.Count);Destroy(gameObject);
            }
        }
    }
}
#endif
