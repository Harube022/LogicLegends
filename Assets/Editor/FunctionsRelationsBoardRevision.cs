#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Remove retired boilerplate in place; retain every question and its actual solution.</summary>
public static class FunctionsRelationsBoardRevision
{
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().name!="Functions and Relations")
            throw new InvalidOperationException("Open Functions and Relations in Edit Mode.");
        var stage=UnityEngine.Object.FindFirstObjectByType<FunctionsRelationsStageManager>();
        var catalog=(MappingChallengeCatalog)new SerializedObject(stage).FindProperty("catalog").objectReferenceValue;
        int changed=0;
        foreach(var question in catalog.ChallengeGroups.SelectMany(g=>g.variants))
        {
            string clean=Regex.Replace(question.instruction,@"\s*The range contains the outputs you use\.\s*"," ",RegexOptions.IgnoreCase);
            clean=Regex.Replace(clean,@"\s*Tap Submit to check\.\s*"," ",RegexOptions.IgnoreCase).Trim();
            if(clean!=question.instruction){question.instruction=clean;changed++;}
        }
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        var title=stage.WorldBoardLabels[0];
        title.rectTransform.sizeDelta=new Vector2(1.43f,.48f);title.textWrappingMode=TextWrappingModes.NoWrap;
        EditorUtility.SetDirty(title);PrefabUtility.RecordPrefabInstancePropertyModifications(title);
        PrefabUtility.RecordPrefabInstancePropertyModifications(title.rectTransform);
        var label=stage.WorldBoardLabels[1];
        label.font=StageUITheme.ReadingFont;label.richText=true;label.enableAutoSizing=true;
        label.fontSizeMin=1.5f;label.fontSizeMax=2;label.lineSpacing=2;label.textWrappingMode=TextWrappingModes.NoWrap;
        EditorUtility.SetDirty(label);PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        VerifyData(catalog);
        File.WriteAllText("Library/FunctionsRelationsBoardRevision.txt","Updated "+changed+" existing instruction strings in place. Full expected relations supply the intended Range; choice lists and live player selections are not board data sources. Validated 50 bank questions plus distractor, duplicate-output and partial-preset cases.\n");
    }
    public static void VerifyData(MappingChallengeCatalog catalog)
    {
        foreach(var q in catalog.ChallengeGroups.SelectMany(g=>g.variants))
        {
            if(q.IntendedRange.Length==0||q.IntendedRange.Except(q.codomain).Any())throw new InvalidOperationException("Invalid intended range: "+q.title);
            if(q.instruction.Contains("The range contains the outputs you use")||q.instruction.Contains("Tap Submit to check"))throw new InvalidOperationException("Retired boilerplate remains.");
        }
        var example=new MappingChallengeData{domain=new[]{"1","2","3"},codomain=new[]{"X","Y","Z"},
            questionKind=MappingQuestionKind.BuildExactRelation,
            initialPairs=new System.Collections.Generic.List<MappingPair>{new MappingPair("1","X")},
            requiredPairs=new System.Collections.Generic.List<MappingPair>{new MappingPair("1","X"),new MappingPair("2","X"),new MappingPair("3","Y")}};
        if(!example.IntendedRange.SequenceEqual(new[]{"X","Y"}))throw new InvalidOperationException("Build range must use the full answer, excluding distractor Z and duplicate X.");
        example.questionKind=MappingQuestionKind.ClassifyFunction;
        if(!example.IntendedRange.SequenceEqual(new[]{"X"}))throw new InvalidOperationException("Classification must use the given fixed diagram.");
        example.questionKind=MappingQuestionKind.SelectProperties;
        if(!example.IntendedRange.SequenceEqual(new[]{"X"}))throw new InvalidOperationException("Properties must use the given fixed diagram.");
    }
}
#endif
