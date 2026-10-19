using UnityEngine;

public sealed partial class ProofsLibraryStageController
{
    private ProofWorldPedestal worldReadingSource;

    private void ReadWorldScroll(ProofWorldPedestal source)
    {
        if (!started || finished || InputSuppressed || HasReaderOpen || HasCarriedScroll ||
            !worldBoard.TryReadScroll(source, out string title, out string content)) return;
        worldReadingSource = source; readingScroll = -1;
        readerTitle.text = title; readerBody.text = content;
        promptText.gameObject.SetActive(false); readerPanel.SetActive(true);
        Presentation?.RefreshReader(true); HoldReaderInput();
    }
    private void RefreshWorldCarryVisual()
    {
        if (Current.kind == ProofChallengeKind.SortScrolls) return;
        var visual = carriedVisuals[0].transform;
        if (started && !finished && worldBoard.HasCarriedScroll)
        {
            if (player == null) player = Player.LocalInstance;
            if (player == null) return;
            visual.SetParent(player.transform, false);
            visual.localPosition = new Vector3(.43f, 1.12f, .22f);
            visual.localRotation = Quaternion.Euler(0, 0, 28);
            visual.localScale = Vector3.one * .55f; visual.gameObject.SetActive(true);
        }
        else { visual.gameObject.SetActive(false); visual.SetParent(transform, false); }
    }
    public void ReturnScroll()
    {
        if (!started || finished || InputSuppressed || HasReaderOpen || Tutorial != null) return;
        if (worldBoard.HasCarriedScroll) { worldBoard.ReturnCarriedScroll(); return; }
        if (carriedScroll < 0) return;
        int index = carriedScroll; carriedScroll = -1;
        carriedVisuals[index].SetActive(false); carriedVisuals[index].transform.SetParent(transform, false);
        placedScrollLabels[index].gameObject.SetActive(false);
        scrolls[index].gameObject.SetActive(true); scrollSources[index].SetTabletVisible(true);
        RefreshVisuals();
    }
}
