using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Tests.Monsters
{
    public sealed class DevelopmentMonsterSwapButton : MonoBehaviour
    {
        public DevelopmentMonsterSelection selection;
        public Button button;
        public Text label, heading, awakenLabel, barrierDescription;
        public void Swap() { selection.SwapMonster(); Refresh(); }
        void Update() { Refresh(); }
        void Refresh()
        {
            bool rajang = selection.Selected == DevelopmentMonster.Rajang;
            button.interactable = selection.CanSwap;
            label.text = rajang ? "Swap to Khezu" : "Swap to Rajang";
            heading.text = rajang ? "RAJANG TEST TOOLS" : "KHEZU TEST TOOLS";
            awakenLabel.text = rajang ? "Awaken Rajang" : "Awaken Khezu";
            if (barrierDescription != null) barrierDescription.text = (rajang ? "Rajang" : "Khezu") + " has trapped you inside a mathematical seal!";
        }
    }
}
