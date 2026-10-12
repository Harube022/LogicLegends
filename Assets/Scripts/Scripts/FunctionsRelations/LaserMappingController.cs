using System;
using System.Collections.Generic;
using UnityEngine;

public enum LaserVisualState { Blue, Green, Red }

// Stores relation pairs and draws their beams between ports on the existing diamond meshes.
public sealed class LaserMappingController : MonoBehaviour
{
    [SerializeField] private Transform domainDiamond;
    [SerializeField] private Transform rangeDiamond;
    [SerializeField] private Transform[] domainDiamonds = new Transform[0];
    [SerializeField] private Transform[] rangeDiamonds = new Transform[0];
    [SerializeField] private Transform beamRoot;
    [SerializeField] private Material laserMaterial;
    [SerializeField] private Material greenLaserMaterial;
    [SerializeField] private Material redLaserMaterial;

    private readonly Dictionary<MappingPair, LineRenderer> beams = new Dictionary<MappingPair, LineRenderer>();
    private readonly HashSet<MappingPair> locked = new HashSet<MappingPair>();
    private readonly List<MappingPair> connectionOrder = new List<MappingPair>();
    private string[] domainIds = new string[0];
    private string[] codomainIds = new string[0];
    private string selectedDomain;
    private bool canEdit;
    private LaserVisualState visualState;
    private MaterialPropertyBlock glowBlock;

    public event Action<string> Feedback;
    public IReadOnlyCollection<MappingPair> Pairs => beams.Keys;
    public int ConnectionCount => beams.Count;
    public LaserVisualState VisualState => visualState;
    public string SelectedDomain => selectedDomain;
    public Transform DomainDiamond => domainDiamonds.Length > 0 ? domainDiamonds[0] : domainDiamond;
    public Transform RangeDiamond => rangeDiamonds.Length > 0 ? rangeDiamonds[0] : rangeDiamond;
    public Transform[] DomainDiamonds => domainDiamonds;
    public Transform[] RangeDiamonds => rangeDiamonds;
    public bool CanUndo => LastUndoablePair.HasValue;
    public MappingPair? LastUndoablePair
    {
        get
        {
            for (int i = connectionOrder.Count - 1; i >= 0; i--)
                if (beams.ContainsKey(connectionOrder[i]) && !locked.Contains(connectionOrder[i]))
                    return connectionOrder[i];
            return null;
        }
    }

    public void ConfigureDiamonds(Transform[] domains, Transform[] ranges, Transform lines,
        Material blueMaterial, Material greenMaterial, Material redMaterial)
    {
        domainDiamonds = domains ?? new Transform[0];
        rangeDiamonds = ranges ?? new Transform[0];
        domainDiamond = DomainDiamond;
        rangeDiamond = RangeDiamond;
        beamRoot = lines;
        laserMaterial = blueMaterial;
        greenLaserMaterial = greenMaterial;
        redLaserMaterial = redMaterial;
    }

    // Legacy Canvas prefabs are no longer used for mapping.
    public void Configure(DomainNode domainPrefab, CodomainNode codomainPrefab,
        RectTransform domainsPanel, RectTransform codomainsPanel, RectTransform linesPanel) { }
    public void SelectDomain(DomainNode node) { if (node != null) SelectDomain(node.Id); }
    public void SelectCodomain(CodomainNode node) { if (node != null) SelectCodomain(node.Id); }

    public void LoadChallenge(MappingChallengeData challenge)
    {
        Clear();
        if (challenge == null || DomainDiamond == null || RangeDiamond == null ||
            beamRoot == null || laserMaterial == null || greenLaserMaterial == null || redLaserMaterial == null)
        {
            Debug.LogError("Diamond mapping references or laser material are missing.", this);
            return;
        }
        domainIds = challenge.domain ?? new string[0];
        codomainIds = challenge.codomain ?? new string[0];
        if (domainIds.Length > domainDiamonds.Length || codomainIds.Length > rangeDiamonds.Length)
        {
            Debug.LogError("This challenge has more IDs than the configured Domain or Range diamonds.", this);
            return;
        }
        canEdit = true;
        if (challenge.initialPairs != null)
            foreach (var pair in challenge.initialPairs)
            {
                AddConnection(pair, true);
                if (challenge.initialPairsLocked) locked.Add(pair);
            }
        canEdit = challenge.questionKind == MappingQuestionKind.BuildExactRelation;
        SetVisualState(LaserVisualState.Blue);
    }

    public bool SelectDomain(string id)
    {
        if (!canEdit || Array.IndexOf(domainIds, id) < 0) return false;
        selectedDomain = id;
        SetDomainGlow();
        Feedback?.Invoke("Domain " + id + " selected. Go to the RANGE diamond and choose an output.");
        return true;
    }

    public bool SelectCodomain(string id)
    {
        if (!canEdit || Array.IndexOf(codomainIds, id) < 0) return false;
        if (selectedDomain == null)
        {
            Feedback?.Invoke("Choose an input at the DOMAIN diamond first.");
            return false;
        }
        var pair = new MappingPair(selectedDomain, id);
        if (beams.ContainsKey(pair))
        {
            Feedback?.Invoke(pair + " is already connected. Use Undo at RANGE to remove a connection.");
            return false;
        }
        if (!AddConnection(pair, false)) return false;
        Feedback?.Invoke("Blue laser connected " + pair + ". You can choose another output or change the selected input.");
        return true;
    }

    public bool AddConnection(MappingPair pair, bool force)
    {
        if ((!canEdit && !force) || beams.ContainsKey(pair) ||
            Array.IndexOf(domainIds, pair.domain) < 0 || Array.IndexOf(codomainIds, pair.codomain) < 0)
            return false;
        var go = new GameObject("Laser " + pair);
        go.transform.SetParent(beamRoot, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = laserMaterial;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = .28f;
        line.numCapVertices = 6;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        beams.Add(pair, line);
        if (!force) connectionOrder.Add(pair);
        ApplyColor(line);
        UpdateBeam(pair, line);
        return true;
    }

    public bool RemoveConnection(MappingPair pair)
    {
        LineRenderer line;
        if (!canEdit || locked.Contains(pair) || !beams.TryGetValue(pair, out line)) return false;
        Destroy(line.gameObject);
        beams.Remove(pair);
        connectionOrder.Remove(pair);
        Feedback?.Invoke("Removed " + pair + ". Choose a new output at RANGE.");
        return true;
    }

    public bool UndoLastConnection()
    {
        var pair = LastUndoablePair;
        if (!pair.HasValue) { Feedback?.Invoke("No editable laser to undo."); return false; }
        return RemoveConnection(pair.Value);
    }

    public void SetInteractive(bool value) { canEdit = value; if (!value) ClearSelection(); }
    public List<MappingPair> Snapshot() => new List<MappingPair>(beams.Keys);
    public void SetVisualState(LaserVisualState state)
    {
        visualState = state;
        foreach (var line in beams.Values) ApplyColor(line);
    }
    private void ApplyColor(LineRenderer line)
    {
        line.sharedMaterial = visualState == LaserVisualState.Green ? greenLaserMaterial :
            visualState == LaserVisualState.Red ? redLaserMaterial : laserMaterial;
        line.startColor = line.endColor = Color.white;
    }
    private void LateUpdate()
    {
        foreach (var item in beams) UpdateBeam(item.Key, item.Value);
    }
    private void UpdateBeam(MappingPair pair, LineRenderer line)
    {
        if (line == null) return;
        line.SetPosition(0, PortPosition(domainDiamonds, domainIds, pair.domain, true));
        line.SetPosition(1, PortPosition(rangeDiamonds, codomainIds, pair.codomain, false));
    }
    private static Vector3 PortPosition(Transform[] diamonds, string[] ids, string id, bool outgoing)
    {
        int index = Array.IndexOf(ids, id);
        if (index < 0 || index >= diamonds.Length || diamonds[index] == null) return Vector3.zero;
        var diamond = diamonds[index];
        if (outgoing)
        {
            var emitter = diamond.GetComponent<LaserEmitter>();
            if (emitter != null && emitter.BeamAnchor != diamond) return emitter.BeamAnchor.position;
        }
        else
        {
            var receiver = diamond.GetComponent<LaserReceiver>();
            if (receiver != null && receiver.BeamAnchor != diamond) return receiver.BeamAnchor.position;
        }
        var renderer = diamond.GetComponent<Renderer>();
        if (renderer == null) return diamond.position;
        var bounds = renderer.bounds;
        return new Vector3(outgoing ? bounds.max.x - .1f : bounds.min.x + .1f,
            bounds.center.y, bounds.center.z);
    }
    private void SetDomainGlow()
    {
        if (glowBlock == null) glowBlock = new MaterialPropertyBlock();
        for (int i = 0; i < domainDiamonds.Length; i++)
        {
            var renderer = domainDiamonds[i] != null ? domainDiamonds[i].GetComponent<Renderer>() : null;
            if (renderer == null) continue;
            renderer.GetPropertyBlock(glowBlock);
            Color normal = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_EmissionColor") ?
                renderer.sharedMaterial.GetColor("_EmissionColor") : Color.black;
            glowBlock.SetColor("_EmissionColor", i == Array.IndexOf(domainIds, selectedDomain) ?
                new Color(.25f, 1.6f, 2.5f) : normal);
            renderer.SetPropertyBlock(glowBlock);
        }
    }
    public void ClearSelection()
    {
        selectedDomain = null;
        SetDomainGlow();
    }
    public void Clear()
    {
        ClearSelection();
        foreach (var line in beams.Values) if (line != null) Destroy(line.gameObject);
        beams.Clear();
        locked.Clear();
        connectionOrder.Clear();
        domainIds = new string[0];
        codomainIds = new string[0];
    }
}
