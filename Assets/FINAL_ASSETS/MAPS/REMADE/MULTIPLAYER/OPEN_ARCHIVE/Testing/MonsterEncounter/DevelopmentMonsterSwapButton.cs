using UnityEngine;
using UnityEngine.UI;

namespace LogicLegends.Tests.Monsters
{
    public sealed class DevelopmentMonsterSwapButton : MonoBehaviour
    {
        public DevelopmentMonsterSelection selection;
        public Button button;
        public Text label, heading, awakenLabel, barrierDescription;
        void Start()
        {
            foreach (var text in new[] { label, heading, awakenLabel })
            {
                if (text == null) continue;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 12;
                text.resizeTextMaxSize = text.fontSize;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }
        public void Swap() { if (!LogicLegends.Tests.Khezu.KhezuLegacyTestPermission.CanUse) return; selection.SwapMonster(); Refresh(); }
        void Update() { Refresh(); }
        void Refresh()
        {
            bool rajang = selection.Selected == DevelopmentMonster.Rajang;
            button.interactable = LogicLegends.Tests.Khezu.KhezuLegacyTestPermission.CanUse && selection.CanSwap;
            label.text = "Swap to " + LogicLegends.OpenArchive.ArchiveMonsterNames.ForPrimalAxiom(!rajang);
            heading.text = LogicLegends.OpenArchive.ArchiveMonsterNames.ForPrimalAxiom(rajang).ToUpperInvariant() + " TEST TOOLS";
            awakenLabel.text = "Awaken " + LogicLegends.OpenArchive.ArchiveMonsterNames.ForPrimalAxiom(rajang);
            if (barrierDescription != null) barrierDescription.text = LogicLegends.OpenArchive.ArchiveMonsterNames.ForPrimalAxiom(rajang) + " has trapped you inside a mathematical seal!";
        }
    }
}
