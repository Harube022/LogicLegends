#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Question-bank fit, runtime copies and unchanged gameplay-camera captures; offline only.</summary>
public static class FormalScrollReadabilityChecks
{
    const string Folder="Library/FormalScrollReadability",Report=Folder+"/Checks.txt";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Hidden).SetValue(o,value);
    static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
    public static string Catalog()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Report,"Formal scroll typography: actual bank strings, original colliders, unlit SDF, no answer/progression edits.\n");
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();int count=0;
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PREFABS/FormalInformalProofs/ProofWorldPedestal.prefab");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var station=go.GetComponent<ProofWorldPedestal>();
            var collider=go.GetComponent<BoxCollider>();var size=collider.size;var centre=collider.center;
            var bank=Resources.Load<ProofChallengeCatalog>("ProofChallenges");
            foreach(var set in bank.variantSets)foreach(var q in set.variants)
            {
                foreach(var scroll in q.scrolls){Content(station,scroll.title,scroll.content);count++;}
                foreach(var step in q.steps){Content(station,"STATEMENT A",step.statement+"\n"+step.justification);count++;}
                foreach(var rule in q.ruleOptions){Content(station,"RULE A",rule);count++;}
            }
            Content(station,"RULE SLOT 5","[EMPTY]");Content(station,"PRACTICE SCROLL","Read and collect this example.");
            Check(size==collider.size&&centre==collider.center,"presentation leaves interaction/collision dimensions intact");
            Check(station.GetComponentInChildren<Collider>()==collider,"no additional collider or interaction component created");
            var shared=new SerializedObject(prefab.GetComponent<ProofWorldPedestal>()).FindProperty("heading").objectReferenceValue as TMP_Text;
            Check(shared.font!=StageUITheme.ReadingFont,"shared Direct&Indirect prefab typography remains unchanged");
        }
        finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
        File.AppendAllText(Report,"ALL "+count+" BANK TEXTS / PLACEHOLDERS / PRACTICE EXAMPLES FIT.\n");return count+" bank text cases passed";
    }
    static void Content(ProofWorldPedestal station,string title,string body)
    {
        station.SetContent(title,body);station.RefreshTextPresentation(true);
        Check(station.Heading==title&&Field<TMP_Text>(station,"detail").text==body,"original title and complete content preserved");
        Validate(station);
    }
    static void Validate(ProofWorldPedestal station)
    {
        var heading=Field<TMP_Text>(station,"heading");var body=Field<TMP_Text>(station,"detail");
        var paper=station.transform.Find("Reading Tablet");
        float left=paper.localPosition.x-paper.localScale.x*.5f,right=paper.localPosition.x+paper.localScale.x*.5f;
        float bottom=paper.localPosition.y-paper.localScale.y*.5f,top=paper.localPosition.y+paper.localScale.y*.5f;
        foreach(var label in new[]{heading,body})
        {
            label.ForceMeshUpdate(true);
            Check(label.font==StageUITheme.ReadingFont&&label.fontSharedMaterial==ProofScrollTypography.Neon,"reading font and shared neon material applied to "+label.name);
            Check(label.fontSharedMaterial.shader.name=="TextMeshPro/Mobile/Distance Field"&&!label.isTextOverflowing,"unlit mobile SDF with no text overflow: "+label.name+" "+label.text);
            var bounds=label.textBounds;
            var min=station.transform.InverseTransformPoint(label.transform.TransformPoint(bounds.min));
            var max=station.transform.InverseTransformPoint(label.transform.TransformPoint(bounds.max));
            Check(min.x>=left+.03f&&max.x<=right-.03f&&min.y>=bottom+.03f&&max.y<=top-.03f,"glyphs fit inside parchment: "+label.name+" text="+label.text+" min="+min+" max="+max+" paper="+bottom+".."+top);
            var missing=label.text.Where(c=>!char.IsControl(c)&&!label.font.HasCharacter(c,true)).Distinct().ToArray();
            Check(missing.Length==0,"font/fallback covers every symbol: "+new string(missing));
        }
        Check(heading.alignment==TextAlignmentOptions.Center&&heading.fontSize==3&&body.fontSize==2.6f,"larger centred title and consistent readable body size");
        float titleBottom=heading.transform.localPosition.y-heading.rectTransform.rect.height*.5f;
        float bodyTop=body.transform.localPosition.y+body.rectTransform.rect.height*.5f;
        Check(titleBottom>bodyTop+.01f,"title and body areas have a clear gap");
    }
    public static IEnumerator Run()
    {
        var stage=Object.FindFirstObjectByType<ProofsLibraryStageController>();var player=Player.LocalInstance;
        if(stage.Introductions.IsOpen)stage.Introductions.Skip();
        var seen=Field<bool[]>(stage.Introductions,"seen");for(int i=0;i<seen.Length;i++)seen[i]=true;
        var look=player.GetComponentInChildren<CameraTargetController>();var cc=player.GetComponent<CharacterController>();
        Check(!stage.InputSuppressed&&look!=null,"normal gameplay and camera available after Help/tutorial cleanup");
        for(int challenge=0;challenge<5;challenge++)
        {
            Set(stage,"challengeIndex",challenge);Set(stage,"questionIndex",0);Set(stage,"activeChallenge",null);Set(stage,"started",false);
            stage.Activate();yield return null;
            Check(stage.Started,"real book-start path configures challenge "+(challenge+1));
            var q=Field<ProofChallengeData>(stage,"activeChallenge");
            var sources=q.kind==ProofChallengeKind.SortScrolls?stage.ScrollTargets.Where(x=>x.gameObject.activeInHierarchy).ToArray():stage.WorldBoard.SourceStations.Where(x=>x.gameObject.activeInHierarchy&&x.TabletVisible).ToArray();
            foreach(var source in sources)Validate(source);
            var original=sources[0];var clone=Object.Instantiate(original,original.transform.parent);clone.name="QA dynamic readable scroll";clone.SetContent("DYNAMIC SCROLL",Field<TMP_Text>(original,"detail").text);Validate(clone);Object.Destroy(clone.gameObject);
            var detail=Field<TMP_Text>(original,"detail");
            // The Android player permits landscape only. Check a smaller phone
            // viewport and a wide phone with a simulated display cutout.
            foreach(int size in new[]{4,12})
            {
                BookMapMobileChecks.SetSize(size);yield return null;yield return null;
                // Stand at a normal, in-range side approach so the third-person
                // avatar stays visible without covering the parchment.
                var position=original.transform.position-original.transform.forward*2f+original.transform.right*2f+Vector3.up*1.08f;
                var displacement=position-player.transform.position;cc.enabled=false;player.transform.SetPositionAndRotation(position,original.transform.rotation);cc.enabled=true;player.ResetMotionAfterTeleport();
                look.SetViewAngles(14,original.transform.eulerAngles.y);TeleportCameraSync.ResetAfterMove(player.transform,displacement);
                yield return new WaitForSecondsRealtime(.5f);
                Check(GameplayTouchRouting.CameraInputAllowed&&!PrelimTutorialCameraRig.HasActiveView,"capture uses normal player camera, without board view or zoom");
                var camera=Camera.main;var glyphs=detail.textInfo.characterInfo.Where(c=>c.isVisible).ToArray();
                var visible=camera.WorldToViewportPoint(detail.transform.TransformPoint(detail.textBounds.center));
                Check(visible.z>0&&visible.x>0&&visible.x<1&&visible.y>0&&visible.y<1,"scroll text remains in the normal gameplay camera view");
                float pixels=glyphs.Select(c=>Mathf.Abs(camera.WorldToScreenPoint(detail.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(detail.transform.TransformPoint(c.bottomLeft)).y)).DefaultIfEmpty(0).Max();
                File.AppendAllText(Report,"CAMERA C"+(challenge+1)+" "+Screen.width+"x"+Screen.height+" authored FOV="+camera.fieldOfView+" body glyph max height="+pixels.ToString("F1")+"px\n");
                Check(pixels>=8,"body letters remain visible from normal mobile gameplay framing");
                Capture(camera,Folder+"/Challenge"+(challenge+1)+"_"+Screen.width+"x"+Screen.height+".png");yield return null;
            }
            if(q.kind==ProofChallengeKind.SortScrolls)
            {
                stage.ReadScroll(0);Check(stage.ReaderPanel.activeSelf&&stage.ReaderBody.text==q.scrolls[0].content,"reader retains exact proof data");stage.CloseReader();
                stage.ReadScroll(0);stage.CollectButton.onClick.Invoke();Check(stage.HasCarriedScroll,"collection remains connected");stage.ReturnScroll();Check(!stage.HasCarriedScroll,"return remains connected");
            }
            else
            {
                foreach(var source in sources){Check(stage.WorldBoard.TryReadScroll(source,out _,out var body)&&body==Field<TMP_Text>(source,"detail").text,"world source still maps to exact reader content");}
                var selectedSource=sources[0];Check(stage.WorldBoard.CollectReadScroll(selectedSource),"world collection still works");
                var slot=stage.WorldBoard.PlacementSlots.First(s=>stage.WorldBoard.IsAvailablePlacement(s));stage.WorldBoard.Interact(slot);Validate(slot);
                stage.WorldBoard.Interact(slot);Check(selectedSource.TabletVisible&&!stage.HasCarriedScroll,"filled-slot removal restores readable source");
            }
            stage.WorldBoard.Hide();Set(stage,"started",false);
        }
        File.AppendAllText(Report,"ALL FIVE CHALLENGES / DYNAMIC SCROLLS / TWO LANDSCAPE MOBILE ASPECT RATIOS / READER-GET-RETURN-SLOT CHECKS PASSED. Editor only.\n");
    }
    static void Capture(Camera camera,string path)
    {
        var old=RenderTexture.active;var prior=camera.targetTexture;
        var target=RenderTexture.GetTemporary(Screen.width,Screen.height,24);
        try
        {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Object.Destroy(image);
        }
        finally{camera.targetTexture=prior;RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);}
    }
}
#endif
