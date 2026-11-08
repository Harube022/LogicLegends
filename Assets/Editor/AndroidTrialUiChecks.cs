#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AndroidTrialUiChecks
{
    public const string Flag="Android.TrialAudit";
    public static bool Enabled=>SessionState.GetBool(Flag,false);
    public static bool Baseline=>SessionState.GetBool(Flag+".Baseline",false);
    public static bool ExtraOnly=>SessionState.GetBool(Flag+".Extras",false);
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    public static void BeginBaseline(){SessionState.SetBool(Flag,true);SessionState.SetBool(Flag+".Baseline",true);StageHelpPlayChecks.Begin("RulesOfInference");}
    public static void Begin(){SessionState.SetBool(Flag,true);SessionState.SetBool(Flag+".Baseline",false);StageHelpPlayChecks.Begin("PRELIM");}
    public static void BeginFrom(string scene){SessionState.SetBool(Flag,true);SessionState.SetBool(Flag+".Baseline",false);StageHelpPlayChecks.Begin(scene);}
    public static void BeginExtras(){SessionState.SetBool(Flag+".Extras",true);SessionState.SetBool(Flag+".Reentry",false);BeginFrom("PRELIM");}
    public static void BeginOne(string scene){SessionState.SetBool(Flag+".Extras",false);SessionState.SetBool(Flag+".StopAfter",true);BeginFrom(scene);}
    static void Check(bool pass,string text){File.AppendAllText("output/AndroidUIAudit/Flows.txt",(pass?"PASS ":"FAIL ")+text+"\n");if(!pass)throw new Exception(text);}
    public static IEnumerator Tutorial(string scene,MonoBehaviour coach)
    {
        if(!Enabled)yield break;
        if(ExtraOnly)yield break;
        if(SessionState.GetBool(Flag+".Reentry",false))yield break;
        if(!Baseline)
        {
            for(int cycle=0;cycle<2;cycle++)
            {
                yield return new WaitForSecondsRealtime(1.1f);CheckSettings(scene);
                yield return TapOnce(Label("Skip Tutorial"));yield return new WaitForSecondsRealtime(1.1f);CheckSettings(scene);
                yield return TapOnce(Label("No"));yield return null;yield return null;CheckSettings(scene);
                Check(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Count(b=>b.GetComponentInChildren<TMP_Text>()?.text.Trim()=="Skip Tutorial")==1,scene+": one Skip restored after No/repeat");
            }
            var hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.name=="InteractButton");
            typeof(MobileTutorialInteractionChecks).GetMethod("BeginHand",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{coach,scene,(RectTransform)hand.transform});
            yield return null;yield return new WaitForSecondsRealtime(1.1f);
            int fired=0;EventHandler observed=(s,e)=>fired++;GameInput.Instance.OnInteractAction+=observed;
            yield return TapOnce(hand);GameInput.Instance.OnInteractAction-=observed;
            Check(fired==0,scene+": practice hand touch is consumed without a world action");CheckSettings(scene);
        }
        if(scene=="RulesOfInference")
        {
            coach.GetType().GetMethod("BeginBook",Hidden).Invoke(coach,null);
            yield return new WaitForSecondsRealtime(1.8f);
            CaptureInference(Baseline?"Before":"After");
            if(!Baseline)
            {
                var book=UnityEngine.Object.FindFirstObjectByType<LogicLegends.Inference.InferenceBookStart>();
                var c=Camera.main;var planes=GeometryUtility.CalculateFrustumPlanes(c);
                var geometry=book.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
                Check(geometry.Any(r=>(c.cullingMask&(1<<r.gameObject.layer))!=0&&GeometryUtility.TestPlanesAABB(planes,r.bounds)),"RulesOfInference Book Statue is in rendered camera frustum after transition");
                for(int phase=0;phase<4;phase++)
                {
                    yield return new WaitForSecondsRealtime(.8f);
                    coach.GetType().GetMethod("BeginBook",Hidden).Invoke(coach,null);
                    var goal=(Vector3)coach.GetType().GetField("cameraGoal",Hidden).GetValue(coach);
                    Check(Vector3.Distance(goal,book.transform.position)<25,"page-flip phase "+phase+" cannot displace tutorial camera");
                }
            }
        }
    }
    static Button Label(string label)=>UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.GetComponentInChildren<TMP_Text>()!=null&&b.GetComponentInChildren<TMP_Text>().text.Trim().Equals(label,StringComparison.OrdinalIgnoreCase));
    static IEnumerator Tap(Button button)=>(IEnumerator)typeof(StageHelpPlayChecks).GetMethod("Tap",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{button});
    static IEnumerator TapOnce(Button button)
    {
        string name=button.name;int calls=0;UnityEngine.Events.UnityAction listener=()=>calls++;button.onClick.AddListener(listener);
        yield return Tap(button);if(button!=null)button.onClick.RemoveListener(listener);
        Check(calls==1,"one callback per touch: "+name);
    }
    public static void CheckSettings(string scene)
    {
        if(!Enabled||Baseline)return;
        var settings=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.name=="SettingsButton");
        Check(TrialTutorialMode.Active&&TutorialSettingsLock.Blocked,scene+": restriction covers tutorial and confirmation");
        Check(!settings.gameObject.activeInHierarchy||!settings.IsInteractable(),scene+": Options cannot receive tutorial input");
        if(settings.gameObject.activeInHierarchy)Check(settings.GetComponents<CanvasGroup>().Any(g=>g.alpha<=.4f&&!g.blocksRaycasts),scene+": Options visibly disabled without an invisible overlay");
        var menu=UnityEngine.Object.FindFirstObjectByType<LevelMenu>();menu.OpenPause();
        Check(!(bool)typeof(LevelMenu).GetField("pauseOpen",Hidden).GetValue(menu),scene+": Settings handler rejects direct tutorial invocation");
    }
    public static IEnumerator Panels(string scene)
    {
        if(!Enabled||Baseline)yield break;
        if(scene=="Trees")
        {
            var tree=UnityEngine.Object.FindFirstObjectByType<LogicLegends.Trees.TreeStageManager>();
            if(tree.BoardOpen){yield return Tap(tree.ui.BackButton);yield return new WaitForSecondsRealtime(1.2f);}
        }
        var touchscreen=typeof(StageHelpPlayChecks).GetField("screen",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        typeof(SceneReliabilityPlayChecks).GetField("touch",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,touchscreen);
        foreach(int size in new[]{2,4,6,12})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.4f);
            foreach(string name in new[]{"PointerOwnership","GestureFiltering"})yield return (IEnumerator)typeof(SceneReliabilityPlayChecks).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{scene});
            typeof(SceneReliabilityPlayChecks).GetMethod("CheckControls",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{scene});
            yield return (IEnumerator)typeof(SceneReliabilityPlayChecks).GetMethod("Board",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{scene,size==2});
            Check(!TutorialSettingsLock.Blocked,scene+": settings restriction released after tutorial/camera cleanup");
            Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Count(e=>e.isActiveAndEnabled)==1,scene+": one active EventSystem");
            Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).All(e=>e.GetComponents<UnityEngine.EventSystems.BaseInputModule>().Count(m=>m.isActiveAndEnabled)==1),scene+": one UI input module");
        }
        if(scene=="TruthTable")
        {
            typeof(MobileTutorialInteractionChecks).GetField("touchscreen",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,touchscreen);
            yield return (IEnumerator)typeof(MobileTutorialInteractionChecks).GetMethod("Quizzes",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        }
        if(scene=="RulesOfInference")
        {
            var quiz=UnityEngine.Object.FindFirstObjectByType<LogicLegends.Inference.InferenceCrystalQuiz>();
            foreach(int size in new[]{2,4,6,12})
            {
                BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.3f);
                bool before=Player.LocalInstance.enabled,blocked=GameInput.Instance.GameplayInputBlocked;
                int collected=0;quiz.Open(()=>collected++,true);yield return null;
                yield return TapOnce(quiz.BackButton);
                Check(!quiz.IsOpen&&Player.LocalInstance.enabled==before&&GameInput.Instance.GameplayInputBlocked==blocked,"crystal quiz unanswered Back restores input at "+Screen.width+"x"+Screen.height);
                for(int i=0;i<4;i++)
                {
                    quiz.Open(()=>collected++,true);yield return null;
                    yield return TapOnce(quiz.AnswerButtons[i]);yield return TapOnce(quiz.CollectButton);
                    Check(!quiz.IsOpen&&collected==i+1,"crystal quiz choice/Continue receives one touch and closes");
                }
            }
        }
        Check(true,scene+": landscape touch, board Back, gesture ownership and controls checked");
    }
    public static IEnumerator ExtraPanels(string scene)
    {
        if(scene=="TruthTable")
        {
            var inventory=InventoryManager.Instance;
            Check(inventory.CarriedCount==0,"inventory test starts with no gameplay blocks");
            for(int i=0;i<2;i++)
            {
                var sample=GameObject.CreatePrimitive(PrimitiveType.Cube);sample.name="Isolated UI inventory sample";sample.transform.position=Player.LocalInstance.transform.position+Vector3.up*8;
                sample.AddComponent<Rigidbody>().isKinematic=true;sample.AddComponent<GrabbableObject>();var block=sample.AddComponent<TruthBlock>();block.value=i==0;
                Check(inventory.TryPickupBlock(block.value,block)>=0,"inventory accepts one isolated truth-value sample");
            }
            yield return null;
            var ui=UnityEngine.Object.FindFirstObjectByType<MobileInventoryUI>();
            var slots=(MobileInventoryUI.SlotUIElements[])typeof(MobileInventoryUI).GetField("uiSlots",Hidden).GetValue(ui);
            var first=slots[0].slotBackground.GetComponent<UnityEngine.UI.Button>();
            var second=slots[1].slotBackground.GetComponent<UnityEngine.UI.Button>();
            foreach(int size in new[]{2,6,12})
            {
                BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.3f);
                yield return TapOnce(first);Check(inventory.HasBlockSelected()&&inventory.GetSelectedBlockValue(),"inventory touch equips True");
                yield return TapOnce(first);Check(!inventory.HasBlockSelected()&&inventory.CarriedCount==2,"inventory touch hides without losing blocks");
                yield return TapOnce(second);Check(inventory.HasBlockSelected()&&!inventory.GetSelectedBlockValue()&&inventory.CarriedCount==2,"inventory touch equips False and preserves identities");
            }
            inventory.ResetInventory();yield return null;
        }
        if(scene=="Formal vs. Informal Proofs")
        {
            var stage=UnityEngine.Object.FindFirstObjectByType<ProofsLibraryStageController>();
            foreach(int size in new[]{2,6,12})
            {
                BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.3f);
                stage.ReadScroll(0);yield return null;Check(stage.HasReaderOpen,"Formal scroll reader opens");
                yield return TapOnce(stage.ReaderCloseButton);Check(!stage.HasReaderOpen&&!GameInput.Instance.GameplayInputBlocked,"Formal reading Back releases input");
                stage.ReadScroll(0);yield return null;yield return TapOnce(stage.CollectButton);yield return null;
                Check(stage.HasCarriedScroll&&!stage.HasReaderOpen,"Get This Scroll collects once and closes reader");
                yield return TapOnce(stage.Presentation.ReturnScrollButton);Check(!stage.HasCarriedScroll,"Return Scroll releases inventory state");
            }
        }
        if(scene=="Induction")
        {
            var stage=UnityEngine.Object.FindFirstObjectByType<LogicLegends.Induction.InductionStageManager>();
            var at=stage.scrollStand.position+Vector3.back*2;at.y=Player.LocalInstance.transform.position.y;
            PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,at,Quaternion.identity);yield return null;
            foreach(int size in new[]{2,6,12})
            {
                BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.3f);
                stage.OpenScrollCollection();yield return null;Check(stage.BrowsingScrolls,"Induction catalogue opens at scroll stand");
                var panel=stage.ui.Collection;var choice=panel.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("Scroll Choice"));
                yield return TapOnce(choice);yield return TapOnce(stage.ui.GetScrollButton);yield return null;
                Check(stage.Held!=null&&!stage.Reading&&!GameInput.Instance.GameplayInputBlocked,"Induction Get Scroll restores controls");
                yield return TapOnce(stage.ui.ReturnButton);Check(stage.Held==null,"Induction Return Scroll receives touch");
                stage.OpenScrollCollection();yield return null;
                yield return TapOnce(panel.GetComponentsInChildren<Button>().First(b=>b.name=="Close Catalogue"));
                Check(!stage.Reading&&!GameInput.Instance.GameplayInputBlocked,"Induction catalogue Close releases input");
            }
        }
        if(scene=="Functions and Relations")
        {
            var stage=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>();
            var mapping=UnityEngine.Object.FindFirstObjectByType<LaserMappingController>();
            foreach(int size in new[]{2,6,12})
            {
                BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.3f);
                var at=mapping.DomainDiamond.position+Vector3.back*2;at.y=Player.LocalInstance.transform.position.y;
                PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,at,Quaternion.identity);yield return null;
                stage.InteractNearby();yield return null;
                var choices=(UnityEngine.UI.Button[])typeof(FunctionsRelationsStageManager).GetField("domainChoiceButtons",Hidden).GetValue(stage);
                yield return TapOnce(choices[0]);Check(stage.SelectedDomain!=null,"Domain choice receives touch");
                at=mapping.RangeDiamond.position+Vector3.back*2;at.y=Player.LocalInstance.transform.position.y;
                PrelimTeleportTransition.MovePlayerImmediately(Player.LocalInstance.gameObject,at,Quaternion.identity);yield return null;
                stage.InteractNearby();yield return null;
                choices=(UnityEngine.UI.Button[])typeof(FunctionsRelationsStageManager).GetField("rangeChoiceButtons",Hidden).GetValue(stage);
                yield return TapOnce(choices[0]);yield return null;
                var connections=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsConnectionsView>();
                Check(connections.RemoveButtons.Count==1,"Range list shows the newly selected connection");
                yield return TapOnce(connections.RemoveButtons.Values.First());yield return null;
                Check(connections.RemoveButtons.Count==0,"individual Remove updates the Range list");
                stage.CloseDiamondChoices();Check(!GameInput.Instance.GameplayInputBlocked,"closing Range preserves mobile input");
            }
        }
        Check(true,scene+": additional panel checks finished");
    }
    public static IEnumerator Reenter(string scene)
    {
        if(!Enabled||Baseline)yield break;
        SessionState.SetBool(Flag+".Reentry",true);
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/"+scene+".unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        float until=Time.realtimeSinceStartup+40;
        while(Time.realtimeSinceStartup<until&&(Player.LocalInstance==null||TeleportManager.Instance!=null&&TeleportManager.Instance.IsTransitioning||!UnityEngine.Object.FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).Any(p=>p.isActiveAndEnabled)))yield return null;
        Check(TeleportManager.Instance==null||!TeleportManager.Instance.IsTransitioning,scene+": reentry loading cover releases its input blocker");
        Check(Player.LocalInstance!=null&&UnityEngine.Object.FindObjectsByType<StageHelpPage>(FindObjectsSortMode.None).Any(p=>p.isActiveAndEnabled),scene+": fresh entry initializes Help and player without stale panels");
        yield return StageHelpPlayChecks.CompleteInitialGuide(scene);
        until=Time.realtimeSinceStartup+6;while(TutorialSettingsLock.Blocked&&Time.realtimeSinceStartup<until)yield return null;
        Check(!GameInput.Instance.GameplayInputBlocked&&Player.LocalInstance.enabled&&!TutorialSettingsLock.Blocked,scene+": reentry tutorial releases input, Options and camera state");
        Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Count(e=>e.isActiveAndEnabled)==1,scene+": reentry retains one EventSystem");
        SessionState.SetBool(Flag+".Reentry",false);
    }
    static void CaptureInference(string suffix)
    {
        var camera=Camera.main;var book=UnityEngine.Object.FindFirstObjectByType<LogicLegends.Inference.InferenceBookStart>();
        var tutorial=UnityEngine.Object.FindFirstObjectByType<InferenceControlsOnboarding>();
        var lines=new List<string>{"Main camera "+camera.transform.position+" rotation "+camera.transform.eulerAngles+" lens "+camera.nearClipPlane+".."+camera.farClipPlane+" FOV "+camera.fieldOfView+" mask "+camera.cullingMask,"Book "+book.transform.position+" scale "+book.transform.lossyScale};
        var instruction=(TMP_Text)typeof(InferenceControlsOnboarding).GetField("instructionText",Hidden).GetValue(tutorial);
        instruction.ForceMeshUpdate();lines.Add("Instruction text="+instruction.text+" chars="+instruction.textInfo.characterCount+" alpha="+instruction.alpha+" color="+instruction.color+" bounds="+instruction.textBounds+" rect="+instruction.rectTransform.rect+" cull="+instruction.canvasRenderer.cull);
        foreach(var r in book.GetComponentsInChildren<Renderer>(true))lines.Add(r.name+" enabled="+r.enabled+" active="+r.gameObject.activeInHierarchy+" "+r.bounds);
        foreach(var c in UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))lines.Add("CM "+c.name+" "+c.transform.position+" lens "+c.Lens.NearClipPlane+".."+c.Lens.FarClipPlane+" fov "+c.Lens.FieldOfView+" priority "+c.Priority.Value);
        File.WriteAllLines("output/AndroidUIAudit/InferenceCamera"+suffix+".txt",lines);
        ScreenCapture.CaptureScreenshot("output/AndroidUIAudit/InferenceCamera"+suffix+".png");
    }
}
#endif
