using TMPro;
using UnityEngine;

/// <summary>Names the sets and intersection above their existing meshes; never a placement target.</summary>
[DefaultExecutionOrder(1600), DisallowMultipleComponent]
public sealed class SetssssDiagramLabels : MonoBehaviour
{
    private SetssssChallengeOne stage;
    private GameObject root;
    private readonly TextMeshPro[] labels = new TextMeshPro[6];
    private readonly Material[] materials = new Material[6];
    private readonly System.Collections.Generic.List<Rect> occupied = new System.Collections.Generic.List<Rect>();
    private readonly System.Collections.Generic.Dictionary<Mesh,Mesh> captionFreeVisuals = new System.Collections.Generic.Dictionary<Mesh,Mesh>();
    private readonly string[] captions = { "A", "A ∩ B", "B", "U — Universal Set", "A − B", "B − A" };

    public void Initialize(SetssssChallengeOne owner)
    {
        if (root != null) return;
        stage = owner;
        root = new GameObject("Sets Diagram Region Labels");
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i] = new GameObject("Diagram label " + captions[i], typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            labels[i].transform.SetParent(root.transform, false);
            labels[i].font = StageUITheme.HeadingFont;
            labels[i].text = captions[i]; labels[i].fontSize = i == 3 ? 15f : 18f;
            labels[i].fontStyle = FontStyles.Bold;
            labels[i].alignment = TextAlignmentOptions.Center;
            labels[i].rectTransform.sizeDelta = new Vector2(i == 3 ? 30 : 10, 4);
            labels[i].textWrappingMode = TextWrappingModes.NoWrap;
            labels[i].color = i == 0 ? new Color(.65f, 1, 1) : i == 2 ? new Color(.95f, .85f, 1) : new Color(1, .96f, .8f);
            labels[i].raycastTarget = false;
            // Each label gets its own outlined material; do not change the shared font.
            labels[i].outlineColor = new Color(.015f, .025f, .035f); labels[i].outlineWidth = .12f;
            labels[i].extraPadding = true; labels[i].UpdateMeshPadding();
            materials[i] = labels[i].fontMaterial;
            if(materials[i].HasProperty("_FaceDilate"))materials[i].SetFloat("_FaceDilate",.08f);
            // World labels should be occluded by real scenery, unlike HUD text.
            materials[i].shader = labels[i].font.material.shader;
            if(materials[i].HasProperty("_ZTestMode"))materials[i].SetFloat("_ZTestMode",4);
        }
        // The imported difference meshes contain their old text. Replace only
        // those captions, keeping the meshes and validation boundaries intact.
        foreach (bool left in new[] { true, false })
            foreach (var old in stage.DifferenceRegion(left).GetComponentsInChildren<TMP_Text>(true)) old.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (stage == null || root == null || Camera.main == null) return;
        var visual=stage.DiagramVisual;
        if(visual!=null&&visual.sharedMesh!=null&&!captionFreeVisuals.ContainsValue(visual.sharedMesh))
        {
            var source=visual.sharedMesh;Mesh clean;
            if(!captionFreeVisuals.TryGetValue(source,out clean)&&source.subMeshCount==3)
            {
                // Slot 2 is the imported A / intersection / B lettering. Keep
                // every floor and border triangle, and leave collider/validator
                // meshes untouched. Only the render copy loses old captions.
                clean=Instantiate(source);clean.name=source.name+" — captions replaced";
                clean.SetTriangles(System.Array.Empty<int>(),2);
                captionFreeVisuals.Add(source,clean);
            }
            if(clean!=null)visual.sharedMesh=clean;
        }
        var intro = stage.Introductions;
        bool showingIntro = intro != null && intro.IsOpen;
        bool visible = stage.isActiveAndEnabled && !stage.Complete && !stage.IsGameOver &&
            (SetssssControlsOnboarding.Active == null || !SetssssControlsOnboarding.Active.IsActive);
        Bounds a = stage.IntroductionBounds(SetssssChallengeIntroductions.Focus.A);
        Bounds b = stage.IntroductionBounds(SetssssChallengeIntroductions.Focus.B);
        Bounds u = stage.IntroductionBounds(SetssssChallengeIntroductions.Focus.Universe);
        Bounds differenceA = stage.DifferenceRegion(true).GetComponent<Renderer>().bounds;
        Bounds differenceB = stage.DifferenceRegion(false).GetComponent<Renderer>().bounds;
        Vector3[] anchors = {
            new Vector3(a.center.x, a.max.y + 3.5f, Mathf.Lerp(a.center.z, a.max.z, .55f)),
            new Vector3((Mathf.Max(a.min.x,b.min.x)+Mathf.Min(a.max.x,b.max.x))*.5f, Mathf.Max(a.max.y,b.max.y)+3.5f, Mathf.Lerp(a.center.z,a.max.z,.35f)),
            new Vector3(b.center.x, b.max.y + 3.5f, Mathf.Lerp(b.center.z, b.max.z, .55f)),
            new Vector3(u.center.x, u.max.y + 3.5f, u.max.z - 1.5f),
            new Vector3(differenceA.center.x, differenceA.max.y+3.5f, differenceA.center.z),
            new Vector3(differenceB.center.x, differenceB.max.y+3.5f, differenceB.center.z)
        };
        string singleSet = stage.DisplayedComplementSet;
        occupied.Clear();
        // Avoid covering the readable numbers on nearby element blocks.
        foreach(var block in stage.DiagramLabelObstacles)
        {
            var number=block!=null?block.GetComponent<SetssssNumberedBlock>():null;
            var label=number!=null?number.NumberLabel:null;
            if(label!=null&&label.gameObject.activeInHierarchy&&Camera.main.WorldToScreenPoint(label.transform.position).z>0)
                occupied.Add(ScreenBounds(label));
        }
        for (int i = 0; i < labels.Length; i++)
        {
            bool active = visible && (singleSet == "" || i == 3 || singleSet == "A" && i == 0 || singleSet == "B" && i == 2);
            if(i>=4)active &= stage.DifferenceRegion(i==4).activeInHierarchy;
            Bounds region = i==4?differenceA:i==5?differenceB:i==3?u:stage.IntroductionBounds(SetssssChallengeIntroductions.Focus.Both);
            if(!showingIntro)
            {
                var player=Player.LocalInstance;
                var point=player!=null?player.transform.position:Camera.main.transform.position;
                point.y=region.center.y;
                active &= Vector3.Distance(point,region.ClosestPoint(point))<=24f&&!stage.IsViewingDiagramBoard;
            }
            if (showingIntro)
            {
                var focus = intro.CurrentFocus;
                active &= focus == SetssssChallengeIntroductions.Focus.Universe ||
                    focus == SetssssChallengeIntroductions.Focus.Both && i < 3 ||
                    focus == SetssssChallengeIntroductions.Focus.A && i == 0 ||
                    focus == SetssssChallengeIntroductions.Focus.B && i == 2 ||
                    focus == SetssssChallengeIntroductions.Focus.Intersection && i == 1 ||
                    focus == SetssssChallengeIntroductions.Focus.AMinusB && i == 4 ||
                    focus == SetssssChallengeIntroductions.Focus.BMinusA && i == 5;
            }
            labels[i].gameObject.SetActive(active);
            if (!active) continue;
            labels[i].transform.position = anchors[i];
            labels[i].transform.localScale = Vector3.one * Mathf.Clamp(Vector3.Distance(anchors[i], Camera.main.transform.position) * .025f, .3f, 10f);
            labels[i].transform.rotation = Camera.main.transform.rotation;
            // Looking along the circles can project separate world anchors onto
            // the same pixels. Lift a caption without changing its region's X/Z.
            labels[i].ForceMeshUpdate();
            Rect screen=ScreenBounds(labels[i]);
            for(int attempt=0;attempt<5&&occupied.Exists(other=>other.Overlaps(screen));attempt++)
            {
                labels[i].transform.position+=Vector3.up*(labels[i].preferredHeight*labels[i].transform.lossyScale.y+.5f);
                screen=ScreenBounds(labels[i]);
            }
            occupied.Add(screen);
        }
    }
    private static Rect ScreenBounds(TMP_Text label)
    {
        Bounds bounds=label.textBounds;Vector2 min=Vector2.positiveInfinity,max=Vector2.negativeInfinity;
        foreach(float x in new[]{bounds.min.x,bounds.max.x})foreach(float y in new[]{bounds.min.y,bounds.max.y})
        {
            Vector2 p=Camera.main.WorldToScreenPoint(label.transform.TransformPoint(new Vector3(x,y,0)));
            min=Vector2.Min(min,p);max=Vector2.Max(max,p);
        }
        return Rect.MinMaxRect(min.x-4,min.y-4,max.x+4,max.y+4);
    }
    private void OnDisable() { if (root != null) root.SetActive(false); }
    private void OnEnable() { if (root != null) root.SetActive(true); }
    private void OnDestroy()
    {
        if (root != null) Destroy(root);
        foreach (var material in materials) if (material != null) Destroy(material);
        foreach(var mesh in captionFreeVisuals.Values)if(mesh!=null)Destroy(mesh);
    }
}

public sealed partial class SetssssChallengeOne
{
    internal GameObject DifferenceRegion(bool left) => left ? aMinusBRegion : bMinusARegion;
    internal bool IsViewingDiagramBoard => boardView != null && boardView.IsViewingBoard;
    internal MeshFilter DiagramVisual => diagramMeshFilter;
    internal System.Collections.Generic.IEnumerable<GrabbableObject> DiagramLabelObstacles => InteractableBlocks();
}
