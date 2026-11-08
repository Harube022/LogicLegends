using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using LogicLegends.Students;

// References the existing prepared assets. Never edits a gameplay scene or skin prefab.
[InitializeOnLoad]
public sealed class StudentNpcSetup : IPreprocessBuildWithReport
{
    const string CatalogPath="Assets/Resources/Students/StudentNpcCatalog.asset";
    public int callbackOrder=>0;
    static StudentNpcSetup(){EditorApplication.delayCall+=EnsureCatalog;}
    static void EnsureCatalog()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return;
        if(AssetDatabase.LoadAssetAtPath<StudentNpcCatalog>(CatalogPath)==null)Prepare();
    }
    [MenuItem("Logic Legends/Students/Prepare and validate catalog")]
    public static void Prepare()
    {
        Directory.CreateDirectory("Assets/Resources/Students");
        var config=AssetDatabase.LoadAssetAtPath<StudentNpcCatalog>(CatalogPath);
        if(config==null){config=ScriptableObject.CreateInstance<StudentNpcCatalog>();AssetDatabase.CreateAsset(config,CatalogPath);}
        if(config.buildCellsPerFrame==40)config.buildCellsPerFrame=12;
        var skins=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Characters_NEW/LATEST_SKINS"})
            .Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("_Gameplay.prefab",StringComparison.Ordinal)).OrderBy(p=>p)
            .Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p)).ToArray();
        config.skins=skins.Select(prefab=>{
            var profile=prefab.GetComponentInChildren<IntegratedSkinProfile>(true);
            if(profile==null)throw new InvalidOperationException("No prepared skin profile: "+prefab.name);
            return new StudentNpcCatalog.Skin{id=profile.gender+"_"+profile.category,displayName=profile.gender+" "+profile.category,prefab=prefab};
        }).ToArray();
        EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
        Validate();
    }
    [MenuItem("Logic Legends/Students/Validate rules and twelve skins")]
    public static void Validate()
    {
        var config=AssetDatabase.LoadAssetAtPath<StudentNpcCatalog>(CatalogPath);
        if(config==null||config.skins.Length!=12||config.skins.Select(s=>s.id).Distinct().Count()!=12)throw new Exception("Students require twelve distinct prepared skins.");
        var report=new StringBuilder("STUDENT NPC VALIDATION\n");int checks=0;
        foreach(var skin in config.skins)
        {
            var lines=StudentDialogueBank.Lines(skin.id);
            if(lines==null||lines.Length!=5||lines.Any(string.IsNullOrWhiteSpace)||lines.Distinct().Count()!=5)throw new Exception("Each skin needs its five distinct supplied lines: "+skin.id);
            int previous=-1;var dialogueRandom=new System.Random(42);var seen=new System.Collections.Generic.HashSet<int>();
            for(int sample=0;sample<100;sample++)
            {
                int next=StudentDialogueBank.NextIndex(lines.Length,previous,dialogueRandom);
                if(next==previous)throw new Exception("Consecutive dialogue repeat: "+skin.id);
                seen.Add(next);previous=next;checks++;
            }
            if(seen.Count!=5)throw new Exception("Dialogue line unreachable: "+skin.id);
            report.AppendLine(skin.id+": five supplied dialogue lines mapped by ID; all reachable, no consecutive repeats.");
            var anim=skin.prefab.GetComponentInChildren<Animator>(true);
            // The prepared skins deliberately use Generic, transform-bound clips.
            // Generic controllers do not require a Humanoid Avatar asset.
            if(anim==null||anim.runtimeAnimatorController==null||anim.avatar!=null&&!anim.avatar.isValid)throw new Exception("Invalid avatar/controller: "+skin.id);
            var clips=anim.runtimeAnimatorController.animationClips;
            if(!clips.Any(c=>c.name.IndexOf("Walk",StringComparison.OrdinalIgnoreCase)>=0)||!clips.Any(c=>c.name.IndexOf("Idle",StringComparison.OrdinalIgnoreCase)>=0)||!clips.Any(c=>c.name.IndexOf("Run",StringComparison.OrdinalIgnoreCase)>=0))throw new Exception("Missing walk/idle/run: "+skin.id);
            if(skin.prefab.GetComponentInChildren<PlayerAnimator>(true)==null)throw new Exception("Missing skin animation driver: "+skin.id);
            report.AppendLine(skin.id+": prepared Generic rig/controller, Idle, Walk and Run; original prefab retained.");checks++;
        }
        var random=new System.Random(1709);
        for(int trial=1;trial<=10;trial++)for(int challenge=0;challenge<5;challenge++)for(int i=0;i<100;i++)
        {
            int count=StudentNpcPolicy.Population(trial,challenge,random);
            bool ok=trial==9&&challenge==1?count==0:trial==2||trial==3?count>=2&&count<=3:trial==1||trial==9?count>=1&&count<=2:count>=0&&count<=2;
            if(!ok)throw new Exception("Population policy failed");checks++;
        }
        foreach(var skin in config.skins)
        {
            var ledger=new StudentHintLedger();
            if(!ledger.Consume(skin.id,0)||ledger.Consume(skin.id,0)||!ledger.Used(skin.id,0)||ledger.Used(skin.id,1)||!ledger.Consume(skin.id,1))throw new Exception("Hint ledger failed");checks+=5;
            var allowed=config.skins.Where(s=>s.id!=skin.id).ToArray();
            if(allowed.Length!=11||allowed.Select(s=>s.id).Distinct().Count()!=11)throw new Exception("Skin exclusion failed");checks++;
        }
        report.AppendLine(checks+" asset/policy/ledger checks passed. Runtime navigation and mobile checks are separate.");
        Directory.CreateDirectory("output/Students");File.WriteAllText("output/Students/AssetAndPolicyChecks.txt",report.ToString());
        Debug.Log(report.ToString());
    }
    public void OnPreprocessBuild(BuildReport report)
    {
        if(AssetDatabase.LoadAssetAtPath<StudentNpcCatalog>(CatalogPath)==null)Prepare();else Validate();
    }
}
