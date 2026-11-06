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
            AttachCarriedScroll(visual);
        }
        else { visual.gameObject.SetActive(false); visual.SetParent(transform, false); }
    }
    private void AttachCarriedScroll(Transform visual)
    {
        // The imported scroll is only half a world unit long. Size its mesh, not
        // its FBX transform, and compensate for any parent scale at the socket.
        visual.GetComponent<CarriedObjectAlignment>()?.StopFollowing();
        visual.SetParent(player.GetHoldPoint(), false);
        visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
        bool found = false; Bounds bounds = new Bounds();
        foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.transform.GetComponentInParent<TMPro.TMP_Text>() != null ||
                filter.transform.parent != null && filter.transform.parent.name == "__CarryPresentation") continue;
            Bounds b = filter.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                var p = visual.InverseTransformPoint(filter.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x,y,z))));
                if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; } else bounds.Encapsulate(p);
            }
        }
        float size = found ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) : 1f;
        float multiplier = 1.4f / Mathf.Max(.001f, size);
        var parentScale = visual.parent.lossyScale;
        visual.localScale = new Vector3(multiplier / Mathf.Abs(parentScale.x), multiplier / Mathf.Abs(parentScale.y), multiplier / Mathf.Abs(parentScale.z));
        visual.gameObject.SetActive(true);
        CarriedObjectAlignment.Follow(visual, player.GetHoldPoint(), true);
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
