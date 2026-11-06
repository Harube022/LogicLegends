#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Offline regressions for the reported bugs; no authenticated services or rewards run.</summary>
public static class LatestSkinDebugChecks
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool pass,string text){if(!pass)throw new InvalidOperationException(text);}
    public static IEnumerator CharacterPreview(GameObject root)
    {
        var loader=root.GetComponent<PlayerEquipmentLoader>();
        var field=typeof(PlayerEquipmentLoader).GetField("clothesModels",Hidden);
        var entries=(PlayerEquipmentLoader.EquippableModel[])field.GetValue(loader);
        var selected=entries.Where(e=>e.model!=null&&e.model.activeSelf).Select(e=>e.itemID).ToArray();
        if(selected.Length==0)
        {
            typeof(PlayerEquipmentLoader).GetMethod("ApplyEquipment",Hidden).Invoke(loader,new object[]{entries[0].itemID,entries});
            selected=entries.Where(e=>e.model!=null&&e.model.activeSelf).Select(e=>e.itemID).ToArray();
        }
        var preview=root.AddComponent<AdminCharacterOverride>();preview.enabled=false;
        Check(!preview.Select(0),"unauthenticated character change denied");
        for(int i=0;i<12;i++)
        {
            // Exercise the local visual operation without bypassing the public account gate.
            Check((bool)typeof(AdminCharacterOverride).GetMethod("ApplyVisual",Hidden).Invoke(preview,new object[]{i}),"debug visual catalog entry "+i);
            yield return null;
            var profiles=root.GetComponentsInChildren<IntegratedSkinProfile>();
            Check(profiles.Length==1&&profiles[0].gender+"_"+profiles[0].category==AdminCharacterOverride.Catalog.skins[i].name,"one matching debug skin");
            Check(root.GetComponentsInChildren<Player>().Length==1&&root.GetComponentsInChildren<CharacterController>().Length==1,"switch never clones gameplay controller");
        }
        preview.RestoreEquipped();yield return null;
        Check(selected.SequenceEqual(entries.Where(e=>e.model!=null&&e.model.activeSelf).Select(e=>e.itemID)),"preview restores the originally equipped identity");
        Object.Destroy(preview);
        File.AppendAllText(LatestSkinPlayChecks.Report,"PASS temporary Admin preview: all 12 visual mappings, both genders on one player, one capsule, original outfit restored, unauthorized public entry denied. No account/save code called.\n");
    }
    public static IEnumerator ReleaseAndScroll(GameObject root,string label)
    {
        var player=root.GetComponent<Player>();var cc=root.GetComponent<CharacterController>();
        for(int i=0;i<3;i++)
        {
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.transform.localScale=Vector3.one*2.6f;
            var body=item.AddComponent<Rigidbody>();var grab=item.AddComponent<GrabbableObject>();
            grab.Grab(player.HoldPoint);player.SetHeldObjectSilently(grab);yield return new WaitForSeconds(.15f);
            float y=root.transform.position.y;
            grab.Drop();player.SetHeldObjectSilently(null);yield return new WaitForSeconds(.2f);
            Check(root.transform.position.y-y<.08f&&cc.isGrounded,label+" repeated natural release never lifts player");
            Check(item.GetComponent<Collider>().enabled&&!body.isKinematic,label+" drop keeps world physics");
            Object.Destroy(item);
        }
        var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.transform.localScale=Vector3.one*2.6f;
        block.AddComponent<Rigidbody>();var truth=block.AddComponent<TruthBlock>();var grabbed=block.AddComponent<GrabbableObject>();
        grabbed.Grab(player.HoldPoint);player.SetHeldObjectSilently(grabbed);yield return new WaitForSeconds(.15f);
        var puzzleRoot=new GameObject("Offline placement only");puzzleRoot.SetActive(false);var puzzle=puzzleRoot.AddComponent<DynamicLogicPuzzle>();
        var slot=new GameObject("Offline snap");slot.transform.position=player.HoldPoint.position;
        float before=root.transform.position.y;puzzle.LockBlock(truth,slot.transform);yield return new WaitForSeconds(.2f);
        Check(!grabbed.IsHeld&&player.GetHeldObject()==null&&!block.GetComponent<Collider>().enabled,label+" atomic TruthTable placement stops all carrying before placement");
        Check(Vector3.Distance(block.transform.position,slot.transform.position)<.001f&&root.transform.position.y-before<.08f,label+" placement stays in exact slot without lifting player");
        Object.Destroy(block);Object.Destroy(slot);Object.Destroy(puzzleRoot);

        var source=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Offline Real Proof Scroll");
        var visual=Object.Instantiate(source);visual.name="Offline carried actual proof scroll";
        var stageObject=new GameObject("Offline scroll attach only");stageObject.SetActive(false);
        var stage=stageObject.AddComponent<ProofsLibraryStageController>();typeof(ProofsLibraryStageController).GetField("player",Hidden).SetValue(stage,player);
        var attach=typeof(ProofsLibraryStageController).GetMethod("AttachCarriedScroll",Hidden);
        for(int i=0;i<2;i++)
        {
            attach.Invoke(stage,new object[]{visual.transform});yield return new WaitForSeconds(.25f);yield return new WaitForEndOfFrame();
            var renderers=visual.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
            Check(Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z))>.65f,label+" actual scroll has readable carry size");
            Check(Vector3.Distance(b.center,player.HoldPoint.position)<.04f,label+" actual scroll centred between hands");
            visual.GetComponent<CarriedObjectAlignment>().StopFollowing();visual.transform.SetParent(null,true);visual.SetActive(false);
        }
        Object.Destroy(visual);Object.Destroy(stageObject);yield return null;
        File.AppendAllText(LatestSkinPlayChecks.Report,"PASS "+label+": three natural releases with no player lift, atomic TruthTable slot placement, two actual proof-scroll collections at readable world size.\n");
    }
}
#endif
