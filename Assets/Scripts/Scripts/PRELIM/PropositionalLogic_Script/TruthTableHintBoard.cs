using System;
using System.Collections.Generic;
using UnityEngine;

public enum PropositionalOperator
{
    Conjunction,
    Disjunction,
    ExclusiveOr,
    Implication,
    Biconditional
}

public class TruthTableHintBoard : MonoBehaviour
{
    private const int TruthRowCount = 4;

    [Header("Existing Project Assets")]
    [SerializeField] private GameObject trueVisualPrefab;
    [SerializeField] private GameObject falseVisualPrefab;

    [Header("Reveal Timing")]
    [SerializeField, Min(0.1f)] private float secondsPerRow = 20f;

    [Header("Answer Alignment (Board Local Space)")]
    [Tooltip("Center of the first answer and its front face, measured from UPDATED_BOARD's baked A/B lettering and result-column underline.")]
    [SerializeField] private Vector3 firstAnswerCenter = new Vector3(2.93035f, 1.86951f, -0.32741f);
    [Tooltip("Vertical distance between the baked A/B rows.")]
    [SerializeField, Min(0.001f)] private float answerRowSpacing = 0.85567f;
    [Tooltip("Full height of the baked True/False lettering.")]
    [SerializeField, Min(0.001f)] private float answerLetterHeight = 0.59127f;

    private readonly List<GameObject> generatedObjects = new List<GameObject>();
    private readonly GameObject[] outputVisuals = new GameObject[TruthRowCount];
    private PropositionalOperator currentOperator;
    private float activeTime;
    private int revealedRows;

    public int RevealedRows => revealedRows;
    public float ActiveHintTime => activeTime;

    public void BeginChallenge(PropositionalOperator logicOperator)
    {
        currentOperator = logicOperator;
        activeTime = 0f;
        revealedRows = 0;

        EnsureBoardContent();
        for (int i = 0; i < outputVisuals.Length; i++)
        {
            if (outputVisuals[i] != null) outputVisuals[i].SetActive(false);
        }
    }

    public void AdvanceActiveTime(float deltaTime)
    {
        if (deltaTime <= 0f || revealedRows >= TruthRowCount) return;

        activeTime += deltaTime;
        int rowsDue = Mathf.Min(TruthRowCount, Mathf.FloorToInt(activeTime / secondsPerRow));
        while (revealedRows < rowsDue)
        {
            if (outputVisuals[revealedRows] != null)
            {
                outputVisuals[revealedRows].SetActive(true);
            }

            revealedRows++;
        }
    }

    public static PropositionalOperator ParseOperator(string topicName)
    {
        string normalized = (topicName ?? string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("&", "AND")
            .ToUpperInvariant();

        if (normalized.Contains("IFANDONLYIF") || normalized.Contains("BICONDITIONAL"))
            return PropositionalOperator.Biconditional;
        if (normalized.Contains("IMPLICATION"))
            return PropositionalOperator.Implication;
        if (normalized.Contains("EXCLUSIVEOR") || normalized.Contains("XOR"))
            return PropositionalOperator.ExclusiveOr;
        if (normalized.Contains("DISJUNCTION"))
            return PropositionalOperator.Disjunction;
        return PropositionalOperator.Conjunction;
    }

    public static bool Evaluate(PropositionalOperator logicOperator, bool p, bool q)
    {
        switch (logicOperator)
        {
            case PropositionalOperator.Conjunction: return p && q;
            case PropositionalOperator.Disjunction: return p || q;
            case PropositionalOperator.ExclusiveOr: return p ^ q;
            case PropositionalOperator.Implication: return !p || q;
            case PropositionalOperator.Biconditional: return p == q;
            default: throw new ArgumentOutOfRangeException(nameof(logicOperator), logicOperator, null);
        }
    }

    private void EnsureBoardContent()
    {
        ClearGeneratedContent();

        bool[] pValues = { true, true, false, false };
        bool[] qValues = { true, false, true, false };

        // UPDATED_BOARD already contains the intended A, B, and A _ B headings.
        // Generating another set here produces the tiny duplicate overlay labels.

        for (int row = 0; row < TruthRowCount; row++)
        {
            // A/B values belong to the existing board artwork. Only the result
            // column receives generated answer assets.
            outputVisuals[row] = CreateTruthVisual(
                Evaluate(currentOperator, pValues[row], qValues[row]), 2, row, false);
        }
    }

    private GameObject CreateTruthVisual(bool value, int column, int row, bool visible)
    {
        GameObject prefab = value ? trueVisualPrefab : falseVisualPrefab;
        if (prefab == null)
        {
            Debug.LogError($"{name} is missing the {(value ? "TRUE" : "FALSE")} visual asset.", this);
            return null;
        }

        GameObject instance = Instantiate(prefab, transform);
        instance.name = $"Hint_{column}_{row}_{(value ? "TRUE" : "FALSE")}";
        instance.transform.localPosition = firstAnswerCenter + Vector3.down * (row * answerRowSpacing);
        Vector3 visualEuler = prefab.transform.localEulerAngles;
        visualEuler.x = 90f;
        instance.transform.localRotation = Quaternion.Euler(visualEuler);
        FitVisualToCell(instance);
        instance.SetActive(visible);
        generatedObjects.Add(instance);
        return instance;
    }

    private void FitVisualToCell(GameObject instance)
    {
        // Match the baked lettering and center the visible mesh rather than the
        // imported model's off-center pivot.
        Bounds visualBounds = CalculateLocalBounds(instance.transform, false, transform);
        if (visualBounds.size.x <= Mathf.Epsilon || visualBounds.size.y <= Mathf.Epsilon) return;

        float scale = answerLetterHeight / visualBounds.size.y;

        Vector3 offset = new Vector3(visualBounds.center.x, visualBounds.center.y, visualBounds.min.z)
            - instance.transform.localPosition;
        instance.transform.localScale *= scale;
        instance.transform.localPosition -= offset * scale;
    }

    private static Bounds CalculateLocalBounds(Transform root, bool ignoreGenerated = false, Transform referenceSpace = null)
    {
        if (referenceSpace == null) referenceSpace = root;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool found = false;

        foreach (Renderer renderer in renderers)
        {
            Transform contentRoot = renderer.transform;
            while (contentRoot.parent != null && contentRoot.parent != root && contentRoot != root)
                contentRoot = contentRoot.parent;
            if (ignoreGenerated && contentRoot != root &&
                contentRoot.name.StartsWith("Hint_", StringComparison.Ordinal))
            {
                continue;
            }

            Bounds meshBounds = renderer.localBounds;
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 corner = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                Vector3 localCorner = referenceSpace.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                if (!found)
                {
                    bounds = new Bounds(localCorner, Vector3.zero);
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(localCorner);
                }
            }
        }

        return found ? bounds : new Bounds(Vector3.zero, new Vector3(3f, 2f, 0.1f));
    }

    private void ClearGeneratedContent()
    {
        foreach (GameObject generatedObject in generatedObjects)
        {
            if (generatedObject != null) Destroy(generatedObject);
        }

        generatedObjects.Clear();
        Array.Clear(outputVisuals, 0, outputVisuals.Length);
    }

}
