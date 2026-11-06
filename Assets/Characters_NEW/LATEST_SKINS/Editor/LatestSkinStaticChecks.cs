#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LatestSkinStaticChecks
{
    static void Check(bool pass,string text){if(!pass)throw new InvalidOperationException(text);}
    public static string Run()
    {
        var report=new List<string>();
        var mappings=JsonUtility.FromJson<LatestSkinIntegration.Manifest>(File.ReadAllText(LatestSkinIntegration.Reports+"/Mapping.json")).mappings;
        foreach(var path in LatestSkinIntegration.BasePlayers)
        {
            var before=File.ReadAllText("Temp/LatestSkinsIntegrationBackup/"+path);var after=File.ReadAllText(path);
            foreach(var field in new[]{"itemID","defaultClothesID","moveSpeed","jumpForce","gravity","walkSecondsBeforeRun","runSpeedMultiplier"})
            {
                string pattern="^  "+(field=="itemID"?"- ":"")+field+":.*$";
                var a=Regex.Matches(before,pattern,RegexOptions.Multiline).Cast<Match>().Select(m=>m.Value.Trim()).ToArray();
                var b=Regex.Matches(after,pattern,RegexOptions.Multiline).Cast<Match>().Select(m=>m.Value.Trim()).ToArray();
                Check(a.SequenceEqual(b),path+" changed identity/physics field "+field);
            }
            report.Add("PASS "+path+": equipment IDs/defaults and movement/run/jump fields unchanged.");
        }
        Check(PlayerEquipmentLoader.SameEquipmentID("male default","male_default"),"legacy male default alias");
        foreach(var path in new[]{"Assets/Resources/Multiplayer_Male.prefab","Assets/Resources/Multiplayer_Female.prefab"})
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var profiles=root.GetComponentsInChildren<IntegratedSkinProfile>(true);
            Check(profiles.Length==6,path+" six profiles");Check(root.GetComponents<Player>().Length==1&&root.GetComponents<CharacterController>().Length==1&&root.GetComponents<PlayerCarryRig>().Length==1,path+" original movement root");
            foreach(var profile in profiles)
            {
                var animator=profile.GetComponent<Animator>();Check(!animator.applyRootMotion,profile.itemID+" root motion");
                var map=mappings.Single(m=>m.gender==profile.gender&&m.category==profile.category);
                var ownFolder=Path.GetDirectoryName(map.controller).Replace('\\','/')+"/";
                var reused=LatestSkinAnimationReuse.Requests.Where(r=>r.target==profile.gender+"_"+profile.category)
                    .Select(r=>LatestSkinAnimationReuse.OriginalClip(mappings.Single(m=>m.gender+"_"+m.category==r.donor),r.action)).ToArray();
                Check(animator.runtimeAnimatorController.animationClips.All(c=>AssetDatabase.GetAssetPath(c).StartsWith(ownFolder,StringComparison.Ordinal)||reused.Contains(c)),profile.itemID+" own or explicitly mapped donor clips");
                Check(reused.Length==0||profile.GetComponent<SharedSkinAnimationRetarget>()!=null,profile.itemID+" reused clip retarget calibration");
                Check(profile.leftHand!=null&&profile.rightHand!=null,profile.itemID+" hand binding");
            }
            report.Add("PASS "+path+": six inherited matching profiles, one player/capsule/carry rig; hand bindings and clip paths valid.");
        }
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main Menu.unity");
        try
        {
            var components=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c is CustomizationManager||c is ShopPreviewUI||c is MainMenuProfileSection).ToArray();
            foreach(var component in components)
            {
                var so=new SerializedObject(component);var inspect=so.FindProperty("inspection").objectReferenceValue as CharacterInspectView;Check(inspect!=null,"menu inspection reference");
                var camera=(Camera)new SerializedObject(inspect).FindProperty("previewCamera").objectReferenceValue;
                var models=so.FindProperty(component is CustomizationManager?"mannequinModels":"models");int count=0;
                for(int i=0;i<models.arraySize;i++)
                {
                    var e=models.GetArrayElementAtIndex(i);var id=e.FindPropertyRelative("itemID").stringValue;var category=LatestSkinIntegration.Category(id);if(category==null)continue;
                    var root=e.FindPropertyRelative("model").objectReferenceValue as GameObject;Check(root!=null,id+" menu identity reference");
                    var profile=root.GetComponentInChildren<IntegratedSkinProfile>(true);Check(profile!=null&&profile.category==category&&PlayerEquipmentLoader.SameEquipmentID(id,profile.itemID),id+" menu category/ID mapping");
                    for(var t=root.transform;t!=null;t=t.parent)t.gameObject.SetActive(true);
                    for(int j=0;j<models.arraySize;j++){var g=models.GetArrayElementAtIndex(j).FindPropertyRelative("model").objectReferenceValue as GameObject;if(g!=null&&g!=root)g.SetActive(false);}
                    root.SetActive(true);inspect.ShowCharacter(root.transform.parent,root.transform);
                    Bounds b;Check(IntegratedSkinProfile.TryVisualBounds(root.transform,out b),id+" visible skinned mesh");
                    for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    {
                        var v=camera.WorldToViewportPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));
                        Check(v.z>0&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1,component.GetType().Name+" "+id+" clips in preview: "+v);
                    }
                    inspect.Clear();count++;
                }
                Check(count==12,component.GetType().Name+" all 12 outfits");report.Add("PASS "+component.GetType().Name+": all 12 IDs and full outfit bounds fit the actual preview camera.");
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        foreach(var field in new[]{"itemID","price","currencyType","targetCharacter","isDefaultItem"})
        {
            var pattern="^  (?:- )?"+field+":.*$";
            var a=Regex.Matches(File.ReadAllText("Temp/LatestSkinsIntegrationBackup/Assets/Scenes/Main Menu.unity"),pattern,RegexOptions.Multiline).Cast<Match>().Select(m=>m.Value.Trim()).OrderBy(s=>s).ToArray();
            var b=Regex.Matches(File.ReadAllText("Assets/Scenes/Main Menu.unity"),pattern,RegexOptions.Multiline).Cast<Match>().Select(m=>m.Value.Trim()).OrderBy(s=>s).ToArray();
            Check(a.SequenceEqual(b),"Main Menu catalog field changed: "+field);
        }
        report.Add("PASS Main Menu catalog IDs, stored prices/currencies, gender restrictions and default/owned-item metadata unchanged.");
        File.WriteAllLines(LatestSkinIntegration.Reports+"/StaticChecks.txt",report);return string.Join(Environment.NewLine,report);
    }
}
#endif
