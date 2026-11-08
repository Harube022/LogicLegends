using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Read the existing lesson owners; confirmations and repeats are still tutorial mode.</summary>
public static class TrialTutorialMode
{
    static int sceneHandle=-1;
    static ProofsChallengeIntroductions proofs;
    static SetssssChallengeIntroductions sets;
    static LogicLegends.Graph.GraphStageManager graph;
    static LogicLegends.Trees.TreeStageManager trees;
    public static bool Active
    {
        get
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.handle!=sceneHandle){sceneHandle=scene.handle;proofs=null;sets=null;graph=null;trees=null;}
            if(PropositionalControlsOnboarding.Active!=null&&PropositionalControlsOnboarding.Active.IsActive ||
               TruthTableControlsOnboarding.Active!=null&&TruthTableControlsOnboarding.Active.IsActive ||
               InferenceControlsOnboarding.Active!=null&&InferenceControlsOnboarding.Active.IsActive ||
               ProofsControlsOnboarding.Active!=null&&ProofsControlsOnboarding.Active.IsActive ||
               DirectProofsGuide.Active!=null&&(DirectProofsGuide.Active.IsPractice||DirectProofsGuide.Active.IsIntroduction) ||
               LogicLegends.Induction.InductionControlsOnboarding.Active!=null&&LogicLegends.Induction.InductionControlsOnboarding.Active.IsActive ||
               SetssssControlsOnboarding.Active!=null&&SetssssControlsOnboarding.Active.IsActive ||
               FunctionsRelationsControlsOnboarding.Active!=null&&FunctionsRelationsControlsOnboarding.Active.IsActive ||
               LogicLegends.Graph.GraphControlsOnboarding.Active!=null&&LogicLegends.Graph.GraphControlsOnboarding.Active.IsActive)return true;
            switch(scene.name)
            {
                case "Formal vs. Informal Proofs": if(proofs==null)proofs=Object.FindFirstObjectByType<ProofsChallengeIntroductions>();return proofs!=null&&proofs.IsOpen;
                case "Setssss": if(sets==null)sets=Object.FindFirstObjectByType<SetssssChallengeIntroductions>();return sets!=null&&sets.IsOpen;
                case "Graph": if(graph==null)graph=Object.FindFirstObjectByType<LogicLegends.Graph.GraphStageManager>();return graph!=null&&graph.Introductions!=null&&graph.Introductions.IsOpen;
                case "Trees": if(trees==null)trees=Object.FindFirstObjectByType<LogicLegends.Trees.TreeStageManager>();return trees!=null&&trees.Introductions!=null&&trees.Introductions.IsOpen;
                default:return false;
            }
        }
    }
}

/// <summary>A separate UI gate never overwrites a tutorial's own Button.interactable snapshot.</summary>
[DefaultExecutionOrder(32100)]
public sealed class TutorialSettingsLock : MonoBehaviour
{
    Button button;
    CanvasGroup group;
    bool locked,oldInteractable,oldRaycasts;
    float oldAlpha;
    public static bool Returning { get; private set; }
    static bool seenTutorial;
    static int lastTutorialFrame=-2,currentScene=-1;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){Returning=false;seenTutorial=false;lastTutorialFrame=-2;currentScene=-1;}
    public static bool Blocked {get {Observe();return TrialTutorialMode.Active||Returning;} }
    public static void Observe()
    {
        int scene=SceneManager.GetActiveScene().handle;
        if(scene!=currentScene){Reset();currentScene=scene;}
        if(TrialTutorialMode.Active){seenTutorial=true;lastTutorialFrame=Time.frameCount;Returning=false;return;}
        var camera=Camera.main;var brain=camera!=null?camera.GetComponent<Unity.Cinemachine.CinemachineBrain>():null;
        Returning=seenTutorial&&(Time.frameCount<=lastTutorialFrame+1||PrelimTutorialCameraRig.HasActiveTutorialView||brain!=null&&brain.IsBlending);
        if(!Returning)seenTutorial=false;
    }
    public static void Install(Scene scene)
    {
        if(!SoloGameSettings.IsTrial(scene.name))return;
        foreach(var root in scene.GetRootGameObjects())foreach(var candidate in root.GetComponentsInChildren<Button>(true))
            if(candidate.name=="SettingsButton"||candidate.name=="OptionsButton")
                if(candidate.GetComponent<TutorialSettingsLock>()==null)candidate.gameObject.AddComponent<TutorialSettingsLock>();
    }
    void Awake(){button=GetComponent<Button>();group=GetComponent<CanvasGroup>();if(group==null)group=gameObject.AddComponent<CanvasGroup>();}
    void Update()=>Refresh();
    void LateUpdate()=>Refresh();
    void Refresh()
    {
        bool hold=Blocked;
        if(hold)
        {
            if(!locked){oldAlpha=group.alpha;oldInteractable=group.interactable;oldRaycasts=group.blocksRaycasts;locked=true;}
            group.alpha=oldAlpha*.38f;group.interactable=false;group.blocksRaycasts=false;
            var events=UnityEngine.EventSystems.EventSystem.current;
            if(events!=null&&events.currentSelectedGameObject!=null&&events.currentSelectedGameObject.transform.IsChildOf(transform))events.SetSelectedGameObject(null);
        }
        else Release();
    }
    void Release(){if(!locked)return;group.alpha=oldAlpha;group.interactable=oldInteractable;group.blocksRaycasts=oldRaycasts;locked=false;}
    void OnDisable()=>Release();
    void OnDestroy()=>Release();
}
