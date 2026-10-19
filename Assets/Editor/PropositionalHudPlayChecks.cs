#if UNITY_EDITOR
using UnityEditor;
/// <summary>The shared book/mobile suite now verifies the hand-only PRELIM HUD.</summary>
public static class PropositionalHudPlayChecks
{
    [MenuItem("Logic Legends/Propositional Logic/Check HUD and Book Prompts")]
    public static void Begin() { BookStatuePlayChecks.BeginPropositional(); }
}
#endif
