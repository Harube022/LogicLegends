using TMPro;
using UnityEngine;

/// <summary>Names the sets and intersection above their existing meshes; never a placement target.</summary>
[DefaultExecutionOrder(1600), DisallowMultipleComponent]
public sealed class SetssssDiagramLabels : MonoBehaviour
{
    private SetssssChallengeOne stage;
    private GameObject root;
    private readonly TextMeshPro[] labels = new TextMeshPro[4];
    private readonly Material[] materials = new Material[4];
    private readonly string[] captions = { "A", "A ∩ B", "B", "U — Universal Set" };

    public void Initialize(SetssssChallengeOne owner)
    {
        if (root != null) return;
        stage = owner;
        root = new GameObject("Sets Diagram Region Labels");
        var regionMaterial = Resources.Load<Material>("SetssssRegionLabels");
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i] = new GameObject("Diagram label " + captions[i], typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            labels[i].transform.SetParent(root.transform, false);
            labels[i].font = StageUITheme.ReadingFont;
            if (regionMaterial != null) labels[i].fontSharedMaterial = regionMaterial;
            labels[i].text = captions[i]; labels[i].fontSize = 9f;
            labels[i].alignment = TextAlignmentOptions.Center;
            labels[i].rectTransform.sizeDelta = new Vector2(i == 3 ? 24 : 8, 3);
            labels[i].textWrappingMode = TextWrappingModes.NoWrap;
            labels[i].color = i == 0 ? new Color(.45f, .95f, 1) : i == 2 ? new Color(.9f, .72f, 1) : StageUITheme.Gold;
            labels[i].raycastTarget = false;
            // Each label gets its own outlined material; do not change the shared font.
            labels[i].outlineColor = Color.black; labels[i].outlineWidth = .22f;
            materials[i] = labels[i].fontMaterial;
        }
    }

    private void LateUpdate()
    {
        if (stage == null || root == null || Camera.main == null) return;
        var intro = stage.Introductions;
        bool showingIntro = intro != null && intro.IsOpen;
        bool visible = stage.isActiveAndEnabled && !stage.Complete && !stage.IsGameOver &&
            (SetssssControlsOnboarding.Active == null || !SetssssControlsOnboarding.Active.IsActive);
        Bounds a = stage.IntroductionBounds(SetssssChallengeIntroductions.Focus.A);
        Bounds b = stage.IntroductionBounds(SetssssChallengeIntroductions.Focus.B);
        Bounds u = stage.IntroductionBounds(SetssssChallengeIntroductions.Focus.Universe);
        Vector3[] anchors = {
            new Vector3(a.center.x, a.max.y + 6f, a.max.z + 4.5f),
            new Vector3((a.center.x + b.center.x) * .5f, Mathf.Max(a.max.y, b.max.y) + 6f, Mathf.Max(a.max.z, b.max.z) + 4.5f),
            new Vector3(b.center.x, b.max.y + 6f, b.max.z + 4.5f),
            new Vector3(u.center.x, u.max.y + 6f, u.max.z + 5f)
        };
        string singleSet = stage.DisplayedComplementSet;
        for (int i = 0; i < labels.Length; i++)
        {
            bool active = visible && (singleSet == "" || i == 3 || singleSet == "A" && i == 0 || singleSet == "B" && i == 2);
            if (showingIntro)
            {
                var focus = intro.CurrentFocus;
                active &= focus == SetssssChallengeIntroductions.Focus.Universe ||
                    focus == SetssssChallengeIntroductions.Focus.Both && i < 3 ||
                    focus == SetssssChallengeIntroductions.Focus.A && i == 0 ||
                    focus == SetssssChallengeIntroductions.Focus.B && i == 2 ||
                    focus == SetssssChallengeIntroductions.Focus.Intersection && i == 1;
            }
            labels[i].gameObject.SetActive(active);
            if (!active) continue;
            labels[i].transform.position = anchors[i];
            labels[i].transform.localScale = Vector3.one * Mathf.Clamp(Vector3.Distance(anchors[i], Camera.main.transform.position) * .025f, 1f, 6f);
            Vector3 forward = anchors[i] - Camera.main.transform.position;
            if (forward.sqrMagnitude > .001f) labels[i].transform.rotation = Quaternion.LookRotation(forward, Camera.main.transform.up);
        }
    }
    private void OnDisable() { if (root != null) root.SetActive(false); }
    private void OnEnable() { if (root != null) root.SetActive(true); }
    private void OnDestroy()
    {
        if (root != null) Destroy(root);
        foreach (var material in materials) if (material != null) Destroy(material);
    }
}
