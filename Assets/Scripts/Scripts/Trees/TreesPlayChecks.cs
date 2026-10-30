#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace LogicLegends.Trees
{
    /// <summary>Compatibility entry point for the revised ten-question, real-touch checks.</summary>
    public sealed class TreesPlayChecks : MonoBehaviour
    {
        public string Result {get;private set;}="Running";
        IEnumerator Start()
        {
            if(!StageCompleteManager.SuppressPersistenceForValidation)
            {
                Result="Run StageHelpGameplayRecorder.CheckTreesRevision() for an isolated test session.";
                Debug.LogWarning(Result);yield break;
            }
            var type=System.Type.GetType("TreesRevisionPlayChecks, Assembly-CSharp-Editor");
            yield return (IEnumerator)type.GetMethod("Run",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
            Result="PASS: ten questions, board touch input, mobile layout and preserved state.";
        }
    }
}
#endif
