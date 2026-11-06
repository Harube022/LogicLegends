#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Induction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Offline, opt-in QA. No account, reward, challenge completion or save calls.</summary>
public static class InventoryAndAdminPresentationChecks
{
    public const string Report="Library/InventoryToggleChecks.txt";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Hidden).SetValue(owner,value);
    static void Check(bool okay,string label){if(!okay)throw new InvalidOperationException(label);}
    static string Answer(Type owner,object question)=>(string)owner.GetMethod("AdminCurrentAnswer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new[]{question});
    public static string Answers()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode,"Use Edit Mode");
        int formal=0,direct=0,induction=0;var random=UnityEngine.Random.state;
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Read-only answer QA");SceneManager.MoveGameObjectToScene(root,scene);
        try
        {
            var board=root.AddComponent<ProofWorldBoardController>();
            foreach(var guid in AssetDatabase.FindAssets("t:ProofChallengeCatalog"))
            foreach(var set in AssetDatabase.LoadAssetAtPath<ProofChallengeCatalog>(AssetDatabase.GUIDToAssetPath(guid)).variantSets)
            foreach(var q in set.variants)
            {
                string answer=Answer(typeof(ProofsLibraryStageController),q);Check(!string.IsNullOrWhiteSpace(answer),"Formal answer exists");
                if(q.kind==ProofChallengeKind.SortScrolls)
                {for(int i=0;i<q.scrolls.Length;i++)Check(answer.Contains("Scroll "+(char)('A'+i)+" → "+(q.scrolls[i].isFormal?"Formal Proof":"Informal Proof")),"scroll matches validation flag");}
                else
                {
                    Set(board,"challenge",q);
                    Set(board,"assignedSteps",q.kind==ProofChallengeKind.MissingRule?new[]{-1}:q.acceptableOrders[0].stepIds.ToArray());
                    Set(board,"assignedRules",q.kind==ProofChallengeKind.MissingRule?new[]{Array.IndexOf(q.ruleOptions,q.expectedRule)}:q.acceptableOrders[0].stepIds.ToArray());
                    Check(board.IsCorrect(),"displayed Formal solution passes actual board validator");
                    if(q.kind==ProofChallengeKind.MissingRule)Check(answer==q.expectedRule,"exact expected rule");
                    else for(int i=0;i<q.steps.Length;i++){var step=q.steps[q.acceptableOrders[0].stepIds[i]];Check(answer.Contains((i+1)+". "+step.statement),"displayed step order");if(q.kind==ProofChallengeKind.Formalize)Check(answer.Contains(" — "+step.justification),"required rule pairing");}
                }
                Check(!answer.Contains("Step ID")&&!answer.Contains("EXPECTED SOLUTION"),"no internal metadata");formal++;
            }
            foreach(var guid in AssetDatabase.FindAssets("t:DirectIndirectProofCatalog"))
            foreach(var set in AssetDatabase.LoadAssetAtPath<DirectIndirectProofCatalog>(AssetDatabase.GUIDToAssetPath(guid)).variantSets)
            foreach(var q in set.variants)
            {
                string answer=Answer(typeof(DirectIndirectProofStageController),q);
                int[] order=q.kind==TrialProofKind.IdentifyMethod?null:q.acceptedOrders[0].tabletIds;
                Check(q.IsCorrect((int)q.correctMethod,order),"displayed Direct/Indirect answer passes actual validator");
                if(q.kind==TrialProofKind.IdentifyMethod)Check(answer.StartsWith(q.correctMethod==TrialProofMethod.Direct?"Direct Proof":"Indirect Proof"),"correct proof family");
                else for(int i=0;i<order.Length;i++)Check(answer.Contains((i+1)+". "+q.tablets[order[i]].statement),"actual accepted sequence");
                Check(!answer.Contains("Tablet ID")&&!answer.Contains(q.explanation),"no extra explanation or IDs");direct++;
            }
            foreach(var bank in InductionChallenges.VariantSets)foreach(var q in bank)
            {
                string answer=Answer(typeof(InductionStageManager),q);var slots=new List<InductionSlot>();
                foreach(var expected in q.answers)
                {
                    var slot=new GameObject("Expected slot").AddComponent<InductionSlot>();slot.transform.SetParent(root.transform);slot.key=expected.Key;slot.tabletAnchor=slot.transform;
                    var tablet=new GameObject("Expected tablet").AddComponent<InductionTablet>();tablet.transform.SetParent(root.transform);
                    typeof(InductionTablet).GetField("<Data>k__BackingField",Hidden).SetValue(tablet,q.choices.Single(c=>c.id==expected.Value));slot.SetTablet(tablet);slots.Add(slot);
                    Check(answer.Contains(tablet.Data.expression)&&!answer.Contains("["+tablet.Data.id+"]"),"required formula only, no IDs");
                }
                Check(InductionChallenges.IncorrectSlots(q,slots).Length==0,"displayed induction placements pass actual validator");
                foreach(var child in root.GetComponentsInChildren<InductionSlot>())Object.DestroyImmediate(child.gameObject);
                foreach(var child in root.GetComponentsInChildren<InductionTablet>())Object.DestroyImmediate(child.gameObject);
                induction++;
            }
            Check(random.Equals(UnityEngine.Random.state),"answer viewing consumes no random state");
        }
        finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
        string summary="PASS "+formal+" Formal / "+direct+" Direct-Indirect / "+induction+" Induction questions: concise solutions match actual validation, no reroll or progress changes.";
        File.WriteAllText("Library/ConciseAdminAnswerChecks.txt",summary+"\n");return summary;
    }
    public static IEnumerator Inventory()
    {
        Time.timeScale=1;yield return null;
        var player=SceneManager.GetActiveScene().GetRootGameObjects().Select(g=>g.GetComponent<Player>()).First(p=>p!=null);
        player.gameObject.SetActive(true);yield return null;
        var loader=player.GetComponent<PlayerEquipmentLoader>();var entries=(PlayerEquipmentLoader.EquippableModel[])typeof(PlayerEquipmentLoader).GetField("clothesModels",Hidden).GetValue(loader);
        var entry=entries.First(e=>e!=null&&e.model!=null&&e.model.GetComponentInChildren<IntegratedSkinProfile>(true)!=null&&e.model.GetComponentInChildren<IntegratedSkinProfile>(true).category=="Uniform");typeof(PlayerEquipmentLoader).GetMethod("ApplyEquipment",Hidden).Invoke(loader,new object[]{entry.itemID,entries});
        player.SetRunningEnabled(false);var inventory=new GameObject("Offline inventory").AddComponent<InventoryManager>();
        var ui=new GameObject("Inventory UI",typeof(RectTransform)).AddComponent<MobileInventoryUI>();
        var canvas=ui.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;ui.gameObject.AddComponent<GraphicRaycaster>();
        new GameObject("Offline UI events",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        var cleanup=ui.gameObject.AddComponent<InventoryInputCleanup>();cleanup.previous=InputSystem.settings;
        cleanup.temporary=Object.Instantiate(cleanup.previous);cleanup.temporary.hideFlags=HideFlags.HideAndDontSave;
        cleanup.temporary.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;cleanup.temporary.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=cleanup.temporary;cleanup.touch=InputSystem.AddDevice<Touchscreen>();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        var buttons=new Button[2];
        for(int i=0;i<2;i++){var g=new GameObject("Inventory slot "+i,typeof(RectTransform),typeof(Image),typeof(Button));g.transform.SetParent(ui.transform,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(140,100);r.anchoredPosition=new Vector2(i==0?-100:100,0);buttons[i]=g.GetComponent<Button>();}
        buttons[0].onClick.AddListener(ui.OnClickSlot0);buttons[1].onClick.AddListener(ui.OnClickSlot1);
        yield return null;yield return null;
        foreach(bool value in new[]{true,false})
        {
            inventory.ResetInventory();var ids=new List<TruthBlock>();
            for(int count=1;count<=4;count++)
            {
                var block=Block(value);ids.Add(block);block.GetComponent<GrabbableObject>().Grab(player.HoldPoint);yield return null;
                Check(inventory.CarriedCount==count&&inventory.EquippedCount==count,"pickup preserves count and equips matching value");
                Check(inventory.TryPickupBlock(value,block)==0&&inventory.CarriedCount==count,"duplicate pickup cannot add an identity");
                for(int repeat=0;repeat<3;repeat++)
                {
                    yield return Tap(buttons[0],cleanup.touch);
                    Check(!inventory.HasBlockSelected()&&inventory.CarriedCount==count&&inventory.EquippedCount==0,"inventory tap hides without consuming");
                    Check(ids.All(b=>!b.gameObject.activeSelf&&b.GetComponent<Rigidbody>().isKinematic&&!b.GetComponent<Collider>().enabled),"hidden identities have no world physics or pickup presence");
                    Check(player.GetHeldObject()==null&&!player.GetComponent<PlayerCarryRig>().IsCarrying&&player.MovementSpeedMultiplier==1&&!player.IsRunningSuppressed,"unequip removes carry pose and weight");
                    yield return Tap(buttons[0],cleanup.touch);
                    Check(inventory.EquippedCount==count&&player.GetComponent<TruthTableCarryStack>().Count==count&&ids.All(b=>b.gameObject.activeInHierarchy&&Vector3.Distance(b.transform.lossyScale,Vector3.one*2.6f)<.01f),"reequip restores same full-size stack");
                }
                float expected=new[]{.95f,.88f,.8f,.75f}[count-1];Check(Mathf.Abs(player.MovementSpeedMultiplier-expected)<.001f&&player.IsRunningSuppressed,"equipped stack applies correct weight");
            }
            var extra=Block(value);Check(inventory.TryPickupBlock(value,extra)<0&&inventory.CarriedCount==4,"four stored identity limit");Object.Destroy(extra.gameObject);
            var puzzleRoot=new GameObject("Placement API only");puzzleRoot.SetActive(false);var puzzle=puzzleRoot.AddComponent<DynamicLogicPuzzle>();var target=new GameObject("Test board slot").transform;target.position=player.transform.position+Vector3.forward*30;
            var placed=ids.Last();puzzle.LockBlock(placed,target);Check(placed.IsPlacedInColumn&&inventory.CarriedCount==3&&!inventory.Contains(placed),"actual board lock consumes only placed identity");
            Check(placed.ReturnToInventory()&&inventory.CarriedCount==4&&ids.All(inventory.Contains),"rejected placement restores same identity");
            inventory.DropSelectedBlock();yield return new WaitForSeconds(.15f);var dropped=ids.Single(b=>!inventory.Contains(b));Check(dropped.gameObject.activeSelf&&!dropped.GetComponent<GrabbableObject>().IsHeld,"drop releases exactly one identity");
            Check(dropped.ReturnToInventory()&&inventory.CarriedCount==4,"dropped identity can be collected again");
            inventory.ClearSelectionSilently();yield return null;Check(!inventory.HasBlockSelected()&&player.MovementSpeedMultiplier==1,"clear stays unequipped after refresh");
            inventory.ResetInventory();yield return null;Check(inventory.CarriedCount==0&&player.GetComponent<TruthTableCarryStack>().Count==0&&player.MovementSpeedMultiplier==1,"reset clears visible and hidden identities and penalties");
            Object.Destroy(puzzleRoot);Object.Destroy(target.gameObject);File.AppendAllText(Report,"PASS "+value+" value: sizes 1–4; 3 touch hide/reequip cycles each; counts/identities/scale/physics/weight; board placement/return/drop/reset.\n");
        }
        var t1=Block(true);var t2=Block(true);var f1=Block(false);inventory.TryPickupBlock(true,t1);inventory.TryPickupBlock(true,t2);inventory.TryPickupBlock(false,f1);yield return null;
        Check(inventory.CarriedCount==3&&inventory.EquippedCount==1&&!t1.gameObject.activeSelf&&!t2.gameObject.activeSelf&&f1.gameObject.activeSelf,"mixed values show selected stack only");
        yield return Tap(buttons[0],cleanup.touch);Check(inventory.EquippedCount==2&&t1.gameObject.activeSelf&&t2.gameObject.activeSelf&&!f1.gameObject.activeSelf&&Mathf.Abs(player.MovementSpeedMultiplier-.88f)<.001f,"switch value updates visible stack and penalty");
        yield return Tap(buttons[0],cleanup.touch);Check(inventory.CarriedCount==3&&inventory.EquippedCount==0,"mixed unequip keeps all counts");
        inventory.ResetInventory();Object.Destroy(ui.gameObject);File.AppendAllText(Report,"PASS mixed value selection; no saves, quizzes, rewards or progression invoked. Editor simulated touch only.\n");
    }
    static TruthBlock Block(bool value)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=value?"QA True":"QA False";g.transform.localScale=Vector3.one*2.6f;g.AddComponent<Rigidbody>();g.AddComponent<GrabbableObject>();var b=g.AddComponent<TruthBlock>();b.value=value;return b;}
    static int finger=300;
    static IEnumerator Tap(Button button,Touchscreen touch)
    {
        Canvas.ForceUpdateCanvases();var r=(RectTransform)button.transform;var point=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"mobile raycast hits slot");
        int id=++finger;InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=point});yield return null;yield return null;
        InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=point});yield return null;yield return null;
    }
}
public sealed class InventoryInputCleanup:MonoBehaviour
{
    public InputSettings previous,temporary;public Touchscreen touch;
    void OnDestroy(){if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);if(previous!=null)InputSystem.settings=previous;if(temporary!=null)Object.Destroy(temporary);}
}
#endif
