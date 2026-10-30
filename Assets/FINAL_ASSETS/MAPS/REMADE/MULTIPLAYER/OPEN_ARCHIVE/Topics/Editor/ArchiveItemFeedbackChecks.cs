using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Explicit offline fixtures verify real UI actions and actual URP-lit pixels, not only Light.enabled.</summary>
    public static class ArchiveItemFeedbackChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;public float unlitSurfaceLuminance,litSurfaceLuminance,mapUnlitLuminance,mapLitLuminance;public string existingMapSurface;
            public string scope="Native offline saved scene: themed item button pointer events, authoritative heal/battery/revive/fire, ammo equip shortcut, front-orbit aim guard; actual URP render readback in the existing night lighting. A temporary Lit wall isolates spotlight pixels while decorative beam/glow are disabled. Physical Android and two rendered devices remain unverified.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static Report report;static IEnumerator flow;static double next;
        static ArchiveTopicSession s;static ArchiveRewardView view;static bool oldDefeat,oldThreats;
        static GameObject wall,cameraObject;static Material wallMaterial;static Camera probeCamera;
        static Vector3 originalPosition;static Quaternion originalRotation;static Vector3 originalLook;
        public static string Status{get;private set;}="Idle";
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use fresh offline MultiplayerGameplay Play.");
            s=ArchiveTopicSession.Current;view=ArchiveRewardView.Current;report=new Report();
            oldDefeat=s.suppressDefeatForVerification;oldThreats=s.worldProgression.suppressThreatsForVerification;
            s.suppressDefeatForVerification=s.worldProgression.suppressThreatsForVerification=true;
            originalPosition=Player.LocalInstance.transform.position;originalRotation=Player.LocalInstance.transform.rotation;
            originalLook=Player.LocalInstance.GetComponentInChildren<CameraTargetController>().transform.eulerAngles;
            Directory.CreateDirectory("Temp/ArchiveItemFeedback");flow=Flow();next=0;
            EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        }
        static void Check(bool value,string text){report.checks.Add((value?"PASS ":"FAIL ")+text);Save();if(!value)throw new InvalidOperationException(text);}
        static void Save()=>File.WriteAllText("Temp/ArchiveItemFeedback/Play.json",JsonUtility.ToJson(report,true));
        static void Log(string message,string stack,LogType type){if(report.errors.Count<8&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(message+"\n"+stack);}
        static void Tick()
        {
            if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();
            try{if(EditorApplication.timeSinceStartup<next)return;if(!Application.isPlaying)throw new Exception("Play stopped");if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float seconds?seconds:0);}
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Finish(bool ok)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;
            if(wall!=null)UnityEngine.Object.Destroy(wall);if(cameraObject!=null)UnityEngine.Object.Destroy(cameraObject);if(wallMaterial!=null)UnityEngine.Object.Destroy(wallMaterial);
            if(Application.isPlaying&&s!=null){s.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);s.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);s.suppressDefeatForVerification=oldDefeat;s.worldProgression.suppressThreatsForVerification=oldThreats;Move(originalPosition,originalRotation);Player.LocalInstance.GetComponentInChildren<CameraTargetController>().SetViewAngles(originalLook.x,originalLook.y);}
            report.passed=ok&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Save();
        }
        static void Move(Vector3 position,Quaternion rotation)
        {
            var p=Player.LocalInstance;var cc=p.GetComponent<CharacterController>();cc.enabled=false;p.transform.SetPositionAndRotation(position,rotation);p.ResetMotionAfterTeleport();cc.enabled=true;Physics.SyncTransforms();
            TeleportCameraSync.ResetAfterMove(p.transform,Vector3.zero);
        }
        static void Select(int slot){if(s.EquippedItem(-1)!=slot)view.SelectHotbarSlot(slot);}
        static void ClickAction()
        {
            var b=view.UseEquippedButton;Check(b!=null&&b.isActiveAndEnabled&&b.interactable,"Contextual item action is active and usable");
            var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.position),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Check(hits.Count>0&&(hits[0].gameObject==b.gameObject||hits[0].gameObject.transform.IsChildOf(b.transform)),"Item action is not covered by another mobile control");
            ExecuteEvents.Execute(b.gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        static Texture2D Render(string name)
        {
            var rt=RenderTexture.GetTemporary(512,512,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            var texture=new Texture2D(512,512,TextureFormat.RGB24,false);
            try{RenderPipeline.SubmitRenderRequest(probeCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,512,512),0,0);texture.Apply();File.WriteAllBytes("Temp/ArchiveItemFeedback/"+name+".png",texture.EncodeToPNG());}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}return texture;
        }
        static float CenterLuminance(Texture2D texture)
        {
            float value=0;int count=0;var pixels=texture.GetPixels32();
            for(int y=224;y<288;y++)for(int x=224;x<288;x++){var c=pixels[y*512+x];value+=(.2126f*c.r+.7152f*c.g+.0722f*c.b)/255;count++;}return value/count;
        }
        static IEnumerator Flow()
        {
            Status="Existing theme and contextual actions";s.Initialize(704219);ArchiveTopicUI.Current.Close();
            s.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,-1);yield return .3f;
            Check(view.flashlightBeamMaterial!=null&&view.flashlightBeamMaterial.shader.isSupported&&!ShaderUtil.ShaderHasError(view.flashlightBeamMaterial.shader),"Saved reusable URP flashlight material compiles and is supported");
            Check(Enumerable.Range(0,6).All(i=>view.HotbarButton(i).GetComponent<Image>().sprite==StageUITheme.ButtonArt),"Hotbar slots preserve the existing item selection artwork");
            Check(Enumerable.Range(0,6).SelectMany(i=>view.HotbarButton(i).GetComponentsInChildren<TMPro.TMP_Text>()).All(t=>
                t.font.atlasTextures.Any(atlas=>atlas==t.fontSharedMaterial.mainTexture)),
                "Every slot label uses the matching font atlas after applying the shared button theme");
            Select(2);yield return .25f;int medkits=s.CountItem(-1,ArchiveSurvivalItem.Medkit);
            Check(view.SelectedActionLabel.Contains("HEAL")&&!view.UseEquippedButton.interactable&&!view.UseSelectedHotbarItem()&&s.CountItem(-1,ArchiveSurvivalItem.Medkit)==medkits,
                "Medkit button explains full hearts and prevents accidental waste");
            s.RequestAdminTest(ArchiveAdminTestAction.SetHearts,-1,2);yield return .25f;ClickAction();yield return .5f;
            Check(s.hunting.health.ActorHearts(-1)==3&&s.CountItem(-1,ArchiveSurvivalItem.Medkit)==medkits-1,"HEAL action restores exactly one real heart and consumes one medkit");
            var inv=s.Inventory(-1);inv.batterySeconds=ArchiveTopicSession.FlashlightCapacity;inv.batteryUpdatedAt=ArchiveTopicSession.Clock;Select(1);yield return .25f;
            Check(view.SelectedActionLabel.Contains("BATTERY")&&!view.UseEquippedButton.interactable&&!view.UseSelectedHotbarItem(),"Full flashlight charge disables LOAD BATTERY with an explanation");
            inv.batterySeconds=30;inv.batteryUpdatedAt=ArchiveTopicSession.Clock;inv.items[(int)ArchiveSurvivalItem.Battery]=1;yield return .2f;int batteries=s.CountItem(-1,ArchiveSurvivalItem.Battery);ClickAction();yield return .5f;
            Check(Mathf.Abs(s.FlashlightRemaining(-1)-240)<.1f&&s.CountItem(-1,ArchiveSurvivalItem.Battery)==batteries-1,"LOAD BATTERY restores exactly four minutes and consumes one battery");
            Check(!view.HotbarButton(1).gameObject.activeSelf&&view.HotbarButton(0).gameObject.activeInHierarchy,
                "Loading the final battery removes the battery holder and keeps the owned flashlight");
            inv.items[(int)ArchiveSurvivalItem.TranquilizerAmmo]=1;
            Select(5);yield return .25f;int darts=s.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo);ClickAction();yield return .25f;
            Check(s.EquippedItem(-1)==4&&s.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo)==darts,"Ammo action equips the existing gun without consuming or manually reloading darts");
            var look=Player.LocalInstance.GetComponentInChildren<CameraTargetController>();look.SetViewAngles(12,Player.LocalInstance.transform.eulerAngles.y+180);yield return .3f;int serial=s.State.survival.shotSerial;
            Check(view.SelectedActionLabel.Contains("TURN TO")&&!view.UseEquippedButton.interactable&&!view.UseSelectedHotbarItem()&&s.State.survival.shotSerial==serial,
                "Front-facing orbit explains how to aim and never fires opposite the visible barrel");
            look.SetViewAngles(12,Player.LocalInstance.transform.eulerAngles.y);yield return .3f;ClickAction();yield return .5f;
            Check(s.State.survival.shotSerial==serial+1&&s.CountItem(-1,ArchiveSurvivalItem.TranquilizerAmmo)==darts-1,"The actual FIRE button creates exactly one original authoritative dart shot");
            Check(!view.HotbarButton(5).gameObject.activeSelf&&view.HotbarButton(4).gameObject.activeInHierarchy&&
                s.EquippedItem(-1)==4&&!view.UseEquippedButton.interactable,
                "Firing the last dart removes AMMO while keeping the equipped gun with its empty-ammo explanation");
            Select(3);yield return .25f;
            Check(!view.UseEquippedButton.interactable&&view.SelectedActionHint.Contains("No teammate"),"Revival Kit explains when no teammate is available at the nest");
            s.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate);s.RequestAdminTest(ArchiveAdminTestAction.DownAtNest,-2);yield return .3f;
            Check(!view.UseEquippedButton.interactable&&view.SelectedActionHint.Contains("Move beside"),"Revival Kit requires the original nest proximity instead of spending a kit remotely");
            Move(s.Downed(-2).anchor+Vector3.back*2.5f,originalRotation);yield return .25f;int kits=s.CountItem(-1,ArchiveSurvivalItem.RevivalKit);ClickAction();yield return .5f;
            Check(s.hunting.health.ActorHearts(-2)==1&&s.CountItem(-1,ArchiveSurvivalItem.RevivalKit)==kits-1,"REVIVE button rescues the nearest actual downed teammate with one heart and one kit");
            s.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);Move(originalPosition,originalRotation);

            Status="Real night spotlight illumination and visible surface VFX";
            var w=s.State.worldTime;w.adminPhaseOverride=w.phase=w.previousPhase=3;w.changedAt=ArchiveTopicSession.Clock-100;w.arrivalStartedAt=-1;
            look.SetViewAngles(0,Player.LocalInstance.transform.eulerAngles.y);Select(0);yield return .3f;ClickAction();yield return .3f;
            var lamp=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.name=="Actor_-1_Flashlight");var fx=lamp.GetComponent<ArchiveFlashlightVfx>();
            Check(lamp.enabled&&lamp.type==LightType.Spot&&lamp.shadows==LightShadows.None&&fx!=null,"TURN ON drives the existing real spotlight plus lightweight VFX without realtime shadow maps");
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Flashlight_Lit_Surface_QA";
            wall.transform.SetPositionAndRotation(lamp.transform.position+lamp.transform.forward*6,Quaternion.LookRotation(lamp.transform.forward));wall.transform.localScale=new Vector3(8,8,.2f);
            wallMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));wallMaterial.SetColor("_BaseColor",new Color(.65f,.65f,.65f));wallMaterial.SetFloat("_Smoothness",.1f);wall.GetComponent<Renderer>().sharedMaterial=wallMaterial;
            cameraObject=new GameObject("Flashlight_Pixel_Readback_QA");probeCamera=cameraObject.AddComponent<Camera>();probeCamera.enabled=false;probeCamera.fieldOfView=50;probeCamera.nearClipPlane=.1f;probeCamera.farClipPlane=80;probeCamera.allowHDR=probeCamera.allowMSAA=false;
            probeCamera.transform.position=lamp.transform.position-lamp.transform.forward*2+Player.LocalInstance.transform.right*1.5f+Vector3.up*.4f;probeCamera.transform.LookAt(wall.transform.position);
            probeCamera.GetUniversalAdditionalCameraData().renderPostProcessing=false;Physics.SyncTransforms();yield return .3f;
            Check(fx.BeamRenderer!=null&&fx.BeamRenderer.enabled&&fx.ImpactRenderer!=null&&fx.ImpactRenderer.enabled&&fx.HitSurface==wall.GetComponent<Collider>()&&fx.VisualLength<=fx.CurrentHitDistance+.01f,
                "The visible soft beam stops at the actual wall and produces a feathered impact at the pointed surface");
            Check(fx.GetComponentsInChildren<Collider>().Length==0&&fx.BeamRenderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3<=64,
                "Beam uses a small bounded mesh without collider, physics or particle overhead");
            fx.enabled=false;lamp.enabled=false;var dark=Render("Flashlight_Off_ActualSurface");lamp.enabled=true;var lit=Render("Flashlight_On_ActualSurface");
            report.unlitSurfaceLuminance=CenterLuminance(dark);report.litSurfaceLuminance=CenterLuminance(lit);
            UnityEngine.Object.Destroy(dark);UnityEngine.Object.Destroy(lit);
            Check(report.litSurfaceLuminance>report.unlitSurfaceLuminance+.035f,"Actual URP-lit surface pixels brighten when the flashlight turns on, with all decorative VFX disabled");
            fx.enabled=true;yield return .25f;var complete=Render("Flashlight_On_With_Beam");UnityEngine.Object.Destroy(complete);
            yield return .4f;ScreenCapture.CaptureScreenshot("Temp/ArchiveItemFeedback/Item_Action_Buttons_Night.png");yield return .25f;
            wall.transform.position=lamp.transform.position+lamp.transform.forward*1.1f;Physics.SyncTransforms();yield return .2f;
            Check(lamp.intensity<=60&&lamp.intensity>0,"Close walls soften the flashlight hotspot without weakening its long-range night setting");
            Check(view.SelectedActionLabel.Contains("TURN OFF"),"Flashlight action clearly changes to TURN OFF while illuminated");ClickAction();yield return .3f;
            Check(!lamp.enabled&&!fx.BeamRenderer.enabled&&!fx.ImpactRenderer.enabled,"TURN OFF immediately hides real illumination, beam and surface glow");
            Status="Existing Open Archive surface receives real additional lighting";
            UnityEngine.Object.Destroy(wall);wall=null;yield return .25f;
            look.SetViewAngles(18,Player.LocalInstance.transform.eulerAngles.y);yield return .25f;ClickAction();yield return .3f;
            Check(fx.HitSurface!=null&&fx.HitSurface.name!="Flashlight_Lit_Surface_QA","Flashlight now targets the existing map geometry rather than a test wall");
            report.existingMapSurface=fx.HitSurface.name;
            Vector3 mapPoint=fx.HitPoint;
            probeCamera.transform.position=mapPoint+Vector3.up*6-Player.LocalInstance.transform.forward*3+Player.LocalInstance.transform.right*2;
            probeCamera.transform.LookAt(mapPoint);
            fx.enabled=false;lamp.enabled=false;var mapDark=Render("Original_Map_Flashlight_Off");lamp.enabled=true;var mapLit=Render("Original_Map_Flashlight_On");
            report.mapUnlitLuminance=CenterLuminance(mapDark);report.mapLitLuminance=CenterLuminance(mapLit);
            UnityEngine.Object.Destroy(mapDark);UnityEngine.Object.Destroy(mapLit);
            Check(report.mapLitLuminance>report.mapUnlitLuminance+.035f,"The original Open Archive surface clearly brightens for night navigation with decorative beam/glow disabled");
            fx.enabled=true;yield return .25f;
            yield return .4f;ScreenCapture.CaptureScreenshot("Temp/ArchiveItemFeedback/Flashlight_Original_Map.png");yield return .2f;

        }
    }
}
