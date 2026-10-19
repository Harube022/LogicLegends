using LogicLegends.OpenArchive.Cinematics;
using LogicLegends.OpenArchive.Hunting;
using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>Development outcome selector; the existing encounter validates and owns every result.</summary>
    public sealed class KhezuBarrierDevelopmentUI : MonoBehaviour
    {
        public KhezuCaptureEncounterController encounter;
        public GameObject panel;
        public Button successButton,failedButton;
        public Text status;
        public CanvasGroup[] otherTestControls;
        public bool Visible=>panel!=null && panel.activeInHierarchy;
        bool pending;
        float sentAt;
        int serial;
        void Awake(){if(panel!=null)panel.SetActive(false);if(!KhezuCaptureEncounterController.CompiledDevelopmentOutcomes)gameObject.SetActive(false);}
        void Update(){
            bool show=encounter!=null && encounter.DevelopmentOutcomesEnabled && encounter.LocalChallengeActive;
            if(show && serial!=encounter.ActiveCaptureSerial){serial=encounter.ActiveCaptureSerial;pending=false;KhezuCaptureEncounterController.BarrierLog("Test UI opened for capture "+serial);}
            if(!show)pending=false;
            if(pending && Time.unscaledTime-sentAt>2)pending=false;
            if(panel!=null && panel.activeSelf!=show)panel.SetActive(show);
            if(successButton!=null)successButton.interactable=show && !pending;
            if(failedButton!=null)failedButton.interactable=show && !pending;
            if(status!=null)status.text=pending?"Waiting for the host to confirm the outcome…":"Choose an outcome to test the encounter.";
            bool busy=encounter!=null && encounter.Phase!=KhezuEncounterPhase.None;
            if(otherTestControls!=null)foreach(var group in otherTestControls)if(group!=null)group.interactable=!busy;
        }
        public void Success(){Submit(true);}
        public void Failed(){Submit(false);}
        void Submit(bool success){
            if(pending || encounter==null || !encounter.LocalChallengeActive || !encounter.DevelopmentOutcomesEnabled)return;
            pending=encounter.SubmitDevelopmentOutcome(success);sentAt=Time.unscaledTime;
            if(pending){successButton.interactable=false;failedButton.interactable=false;}
        }
        void OnDisable(){if(panel!=null)panel.SetActive(false);if(otherTestControls!=null)foreach(var group in otherTestControls)if(group!=null)group.interactable=true;}
    }
}
