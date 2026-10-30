using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Tests.Khezu
{
    /// <summary>On-screen controls for the local animation test only.</summary>
    public sealed class KhezuAwakeningTestPanel : MonoBehaviour
    {
        [SerializeField] KhezuAwakeningTestController test;
        [SerializeField] Text status;
        [SerializeField] Button increaseButton, awakenAllButton, resetButton;
        float nextRefresh;

        public Button IncreaseButton { get { return increaseButton; } }
        public Button AwakenAllButton { get { return awakenAllButton; } }
        public Button ResetButton { get { return resetButton; } }

        public void Configure(KhezuAwakeningTestController controller, Text label, Button increase, Button awaken, Button reset)
        {
            test = controller; status = label;
            increaseButton = increase; awakenAllButton = awaken; resetButton = reset;
        }
        void OnEnable() { nextRefresh = 0; }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f;
            Refresh();
        }
        public void IncreaseMistakes()
        {
            if (KhezuLegacyTestPermission.CanUse && test != null) test.IncreaseMistakeCount();
            Refresh();
        }
        public void AwakenAll()
        {
            if (!KhezuLegacyTestPermission.CanUse || test == null) return;
            if (test.IsPreviewing) test.ResetTest();
            for (int i = test.Mistakes; i < 4; i++) test.IncreaseMistakeCount();
            Refresh();
        }
        public void ResetToSleep()
        {
            if (KhezuLegacyTestPermission.CanUse && test != null) test.ResetTest();
            Refresh();
        }
        void Refresh()
        {
            bool ready = KhezuLegacyTestPermission.CanUse && test != null && test.isActiveAndEnabled && Time.timeScale > 0;
            if (status != null) status.text = test == null ? "KHEZU TEST — Monster unavailable" :
                "KHEZU TEST   |   Mistakes " + test.Mistakes + " / 4   |   " + test.CurrentState;
            bool canIncrease = ready && (test.Mistakes < 4 || test.IsPreviewing);
            if (increaseButton != null) increaseButton.interactable = canIncrease;
            if (awakenAllButton != null) awakenAllButton.interactable = canIncrease;
            if (resetButton != null) resetButton.interactable = ready;
        }
    }
}
