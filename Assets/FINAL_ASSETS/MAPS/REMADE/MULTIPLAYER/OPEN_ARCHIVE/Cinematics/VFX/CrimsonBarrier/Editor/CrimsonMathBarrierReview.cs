using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LogicLegends.OpenArchive.Cinematics.VFX;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LogicLegends.OpenArchive.Editor
{
    public static class CrimsonMathBarrierReview
    {
        [Serializable] sealed class Check {public string name,detail;public bool passed;}
        [Serializable] sealed class Report {public string checkedUtc;public bool passed;public List<Check> checks=new List<Check>();public string limitation="Visual assets tested in Unity 6000.3.6f1 URP 17.3. Phase 3B gameplay controller and challenge outcome service are absent from this checkout. No movement lock, success/failure, heart or networking logic was added. Android device performance and live multiplayer unverified.";}
        static void Add(Report r,string name,bool pass,string detail=""){r.checks.Add(new Check{name=name,passed=pass,detail=detail});}
        public static void Verify(){
            var report=new Report{checkedUtc=DateTime.UtcNow.ToString("o")};string folder=CrimsonMathBarrierBuilder.Folder;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(CrimsonMathBarrierBuilder.PrefabPath);var vfx=prefab.GetComponent<CrimsonMathBarrierVfx>();
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(folder+"/Fonts/Crimson_NotoSansMath_SDF.asset");
            Add(report,"All thirteen requested mathematical glyphs present",CrimsonMathBarrierBuilder.Symbols.All(c=>font.characterLookupTable.ContainsKey(c)),CrimsonMathBarrierBuilder.Symbols);
            Add(report,"Static font atlas prevents runtime glyph requests",font.atlasPopulationMode==AtlasPopulationMode.Static && font.atlasTextures.Length==1,"1024 x 1024 Noto Sans Math SDF");
            Add(report,"Exact requested palette",ColorUtility.ToHtmlStringRGB(vfx.style.darkRed)=="650009" && ColorUtility.ToHtmlStringRGB(vfx.style.crimson)=="C8102E" && ColorUtility.ToHtmlStringRGB(vfx.style.scarlet)=="FF3030" && ColorUtility.ToHtmlStringRGB(vfx.style.runeHighlights)=="FF7777");
            Add(report,"Semi-transparent wall and inside visibility",vfx.style.wallOpacity<=.06f && vfx.style.insideWallOpacity<=.02f);
            Add(report,"No gameplay or blocking components in reusable VFX",prefab.GetComponentsInChildren<Collider>(true).Length==0 && prefab.GetComponentsInChildren<Rigidbody>(true).Length==0 && prefab.GetComponentsInChildren<Canvas>(true).Length==0 && prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c=>c is CrimsonMathBarrierVfx));
            Add(report,"No additional dynamic lights or audio listeners",prefab.GetComponentsInChildren<Light>(true).Length==0 && prefab.GetComponentsInChildren<AudioListener>(true).Length==0);
            Add(report,"Both energy rings, rotating wall and ground runes authored",vfx.upperRing!=null && vfx.lowerRing!=null && vfx.wallRunes!=null && vfx.sealRunes!=null);
            Add(report,"Venn, graph, truth table, sets and mappings baked",vfx.diagrams.GetComponent<MeshFilter>().sharedMesh.vertexCount>200 && vfx.diagrams.Find("Diagram_Notation")!=null);
            int budget=prefab.GetComponentsInChildren<ParticleSystem>(true).Sum(p=>p.main.maxParticles);
            Add(report,"Bounded Android particle budget",budget==32,budget+" maximum live particles");
            int triangles=prefab.GetComponentsInChildren<MeshFilter>(true).Sum(m=>m.sharedMesh.triangles.Length/3);
            Add(report,"Lightweight baked geometry",triangles<16000,triangles+" triangles, "+vfx.effectRenderers.Length+" renderers; no full-screen effect");
            foreach(var material in vfx.effectRenderers.Select(r=>r.sharedMaterial).Distinct()){
                var messages=ShaderUtil.GetShaderMessages(material.shader);Add(report,"URP shader compiles: "+material.name,!messages.Any(m=>m.severity.ToString()=="Error"),string.Join("; ",messages.Select(m=>m.message).ToArray()));
            }
            report.passed=report.checks.All(c=>c.passed);File.WriteAllText(CrimsonMathBarrierBuilder.Reports+"/VisualAssetsValidation.json",JsonUtility.ToJson(report,true));
        }
        public static void RenderPreviews(){
            if(Application.isPlaying)throw new InvalidOperationException("Use edit mode for staged previews.");
            var root=new GameObject("Temporary_Crimson_Review");root.SetActive(false);root.transform.position=new Vector3(15000,0,15000);
            var original=Object.FindFirstObjectByType<Player>();if(original==null)throw new InvalidOperationException("Open the existing exploration/cinematic scene for its original player model.");
            Camera camera=null;RenderTexture rt=null;Material floorMaterial=null;
            try{
                var vfx=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CrimsonMathBarrierBuilder.PrefabPath),root.transform).GetComponent<CrimsonMathBarrierVfx>();vfx.transform.localPosition=Vector3.zero;
                var actor=Object.Instantiate(original.gameObject,root.transform);actor.name="Original_Player_Visual_Only";
                foreach(var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(behaviour);
                foreach(var collider in actor.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
                foreach(var listener in actor.GetComponentsInChildren<AudioListener>(true))Object.DestroyImmediate(listener);
                actor.transform.localPosition=Vector3.up*1;actor.transform.localRotation=Quaternion.Euler(0,180,0);
                var animator=actor.GetComponentInChildren<Animator>();
                if(animator!=null && animator.runtimeAnimatorController!=null){var idle=animator.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name.ToLowerInvariant().Contains("idle"));if(idle!=null)idle.SampleAnimation(animator.gameObject,.3f);animator.enabled=false;}
                var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.SetParent(root.transform,false);floor.transform.localPosition=Vector3.down*.07f;floor.transform.localScale=Vector3.one*4;
                Object.DestroyImmediate(floor.GetComponent<Collider>());floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));floorMaterial.SetColor("_BaseColor",new Color(.028f,.034f,.044f));floor.GetComponent<MeshRenderer>().sharedMaterial=floorMaterial;
                var light=new GameObject("Review_Light").AddComponent<Light>();light.transform.SetParent(root.transform,false);light.type=LightType.Directional;light.intensity=1.5f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(40,-30,0);
                camera=new GameObject("Review_Camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.014f,.019f,.029f);camera.cullingMask=1<<30;camera.fieldOfView=44;camera.nearClipPlane=.1f;camera.farClipPlane=70;
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                root.SetActive(true);rt=new RenderTexture(1440,1000,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.aspect=1.44f;
                Action<string,float,float,Vector3,Vector3> shot=(name,age,dissolve,position,target)=>{
                    vfx.Preview(age,dissolve);vfx.sparks.Simulate(age,true,true);vfx.fragments.Simulate(age,true,true);camera.transform.localPosition=position;camera.transform.LookAt(root.transform.position+target);camera.Render();
                    var active=RenderTexture.active;RenderTexture.active=rt;var pixels=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();
                    File.WriteAllBytes(CrimsonMathBarrierBuilder.Reports+"/"+name+".png",pixels.EncodeToPNG());Object.DestroyImmediate(pixels);RenderTexture.active=active;
                };
                var outside=new Vector3(11,8,14);var focus=Vector3.up*3.5f;
                shot("Crimson_Barrier_Outside",2,0,outside,focus);
                shot("Crimson_Barrier_Forming",.38f,0,outside,focus);
                shot("Crimson_Barrier_Breaking",2,.55f,outside,focus);
                camera.fieldOfView=75;shot("Crimson_Barrier_Inside",2,0,new Vector3(2.7f,4,-2.7f),new Vector3(0,3.2f,2.8f));camera.fieldOfView=44;
                shot("Crimson_Mathematical_Seal",2,0,new Vector3(0,15,1),Vector3.up*.1f);
            }finally{
                if(camera!=null)camera.targetTexture=null;if(rt!=null){rt.Release();Object.DestroyImmediate(rt);}Object.DestroyImmediate(root);if(floorMaterial!=null)Object.DestroyImmediate(floorMaterial);
            }
            AssetDatabase.Refresh();
        }
    }
}
