using System;

/// <summary>Trial numbers shared by the main menu, HUDs, and saved progression.</summary>
public static class TrialCatalog
{
    public sealed class Trial
    {
        public readonly int Number;
        public readonly string Scene, Topic;
        public Trial(int number,string scene,string topic){Number=number;Scene=scene;Topic=topic;}
        public string Title => "Trial " + Number + " / " + Topic;
    }
    private static readonly Trial[] trials = {
        new Trial(1,"PRELIM","Propositional Logic"),
        new Trial(2,"TruthTable","Truth Table"),
        new Trial(3,"RulesOfInference","Rules of Inference"),
        new Trial(4,"Formal vs. Informal Proofs","Formal vs Informal Proofs"),
        new Trial(5,"Direct&Indirect","Direct and Indirect Proofs"),
        new Trial(6,"Induction","Mathematical Induction"),
        new Trial(7,"Setssss","Sets"),
        new Trial(8,"Functions and Relations","Functions & Relations"),
        new Trial(9,"Graph","Graphs"),
        new Trial(10,"Trees","Trees")
    };
    public static int Count => trials.Length;
    public static Trial Get(int number)
    {
        if(number<1||number>trials.Length)throw new ArgumentOutOfRangeException(nameof(number));
        return trials[number-1];
    }
    public static Trial ForScene(string scene,int prelimTrial=1)
    {
        if(scene=="PRELIM")return Get(prelimTrial==2?2:1);
        foreach(var trial in trials)if(trial.Scene==scene)return trial;
        throw new ArgumentException("No trial menu entry for scene: "+scene,nameof(scene));
    }
}
