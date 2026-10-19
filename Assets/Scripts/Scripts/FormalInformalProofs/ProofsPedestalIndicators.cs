using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Neutral placement guidance: every empty altar, never the answer classification.</summary>
[DisallowMultipleComponent]
public sealed class ProofsPedestalIndicators : MonoBehaviour
{
    private ProofsLibraryStageController stage;
    private Mesh mesh;
    private Material yellow, outline;
    public Transform[] Arrows { get; private set; }
    private readonly Dictionary<ProofWorldPedestal, Transform> slotArrows = new Dictionary<ProofWorldPedestal, Transform>();
    public IReadOnlyDictionary<ProofWorldPedestal, Transform> SlotArrows => slotArrows;

    public void Initialize(ProofsLibraryStageController owner)
    {
        stage = owner;
        mesh = new Mesh { name = "Proof Pedestal Down Arrow" };
        mesh.vertices = new[] { new Vector3(-.16f,.65f,0), new Vector3(.16f,.65f,0), new Vector3(.16f,0,0),
            new Vector3(.5f,0,0), new Vector3(0,-.6f,0), new Vector3(-.5f,0,0), new Vector3(-.16f,0,0) };
        mesh.triangles = new[] { 0,1,2,0,2,6,5,3,4 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        yellow = new Material(shader) { name = "Proof Placement Yellow" }; yellow.SetColor("_BaseColor", new Color(1, .86f, .08f));
        outline = new Material(shader) { name = "Proof Placement Outline" }; outline.SetColor("_BaseColor", new Color(.12f, .085f, .02f));
        Arrows = new Transform[stage.AltarTargets.Length];
        for (int i = 0; i < Arrows.Length; i++)
        {
            Arrows[i] = CreateArrow("Available Proof Pedestal Arrow " + i);
        }
    }
    private Transform CreateArrow(string name)
    {
        var arrow = new GameObject(name).transform; arrow.SetParent(transform, false);
        Shape(arrow, "Outline", outline, 1.16f, .025f); Shape(arrow, "Yellow Arrow", yellow, 1, 0);
        arrow.localScale = Vector3.one * .72f; arrow.gameObject.SetActive(false); return arrow;
    }
    private void Shape(Transform parent, string name, Material material, float scale, float depth)
    {
        var shape = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); shape.transform.SetParent(parent, false);
        shape.transform.localScale = Vector3.one * scale; shape.transform.localPosition = Vector3.forward * depth;
        shape.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = shape.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }
    private void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if (stage == null || Arrows == null) return;
        bool carrying = stage.isActiveAndEnabled && stage.Started && !stage.Finished && stage.CarriedScroll >= 0 &&
            !stage.InputSuppressed && !stage.HasReaderOpen;
        for (int i = 0; i < Arrows.Length; i++)
        {
            var altar = stage.AltarTargets[i]; var arrow = Arrows[i];
            bool visible = carrying && altar.gameObject.activeInHierarchy && stage.IsPedestalEmpty(i);
            arrow.gameObject.SetActive(visible); if (!visible) continue;
            float top = altar.position.y + 3;
            foreach (var label in altar.GetComponentsInChildren<TMP_Text>())
            {
                var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners) top = Mathf.Max(top, corner.y);
            }
            float bob = Mathf.Sin(Time.time * Mathf.PI * 2 / 2.4f) * .12f;
            arrow.position = new Vector3(altar.position.x, top + .85f + bob, altar.position.z);
            if (Camera.main != null) arrow.rotation = Camera.main.transform.rotation;
        }
        RefreshSlotArrows();
    }
    private void RefreshSlotArrows()
    {
        // Reconcile actual configured instances, including sources/slots recreated at runtime.
        var current = new HashSet<ProofWorldPedestal>(stage.WorldBoard.PlacementSlots);
        var removed = new List<ProofWorldPedestal>();
        foreach (var pair in slotArrows)
            if (pair.Key == null || !current.Contains(pair.Key))
            { if (pair.Value != null) { pair.Value.gameObject.SetActive(false); Destroy(pair.Value.gameObject); } removed.Add(pair.Key); }
        foreach (var slot in removed) slotArrows.Remove(slot);
        foreach (var slot in current)
        {
            if (slot == null) continue;
            if (!slotArrows.TryGetValue(slot, out var arrow))
            { arrow = CreateArrow("Available Proof Slot Arrow — " + slot.name); arrow.localScale = Vector3.one * .55f; slotArrows.Add(slot, arrow); }
            bool visible = stage.isActiveAndEnabled && stage.Started && !stage.Finished && !stage.InputSuppressed &&
                !stage.HasReaderOpen && stage.WorldBoard.IsAvailablePlacement(slot);
            arrow.gameObject.SetActive(visible); if (!visible) continue;
            // Offset beside the parchment, in its local right direction, rather than above its text.
            Bounds bounds = new Bounds(slot.transform.position + Vector3.up * 1.5f, Vector3.one);
            var tablet = slot.transform.Find("Reading Tablet");
            if (tablet != null && tablet.TryGetComponent<Renderer>(out var renderer)) bounds = renderer.bounds;
            Vector3 right = slot.transform.right;
            float halfWidth = Mathf.Abs(right.x) * bounds.extents.x + Mathf.Abs(right.y) * bounds.extents.y + Mathf.Abs(right.z) * bounds.extents.z;
            arrow.position = bounds.center + right * (halfWidth + .55f) + Vector3.up * (.3f + Mathf.Sin(Time.time * Mathf.PI * 2 / 2.4f) * .12f);
            if (Camera.main != null) arrow.rotation = Camera.main.transform.rotation;
        }
    }
    private void OnDisable()
    {
        if (Arrows != null) foreach (var arrow in Arrows) if (arrow != null) arrow.gameObject.SetActive(false);
        foreach (var pair in slotArrows) if (pair.Value != null) pair.Value.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        if (Arrows != null) foreach (var arrow in Arrows) if (arrow != null) Destroy(arrow.gameObject);
        foreach (var pair in slotArrows) if (pair.Value != null) Destroy(pair.Value.gameObject);
        if (mesh != null) Destroy(mesh); if (yellow != null) Destroy(yellow); if (outline != null) Destroy(outline);
    }
}
