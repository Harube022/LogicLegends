using System;
using System.Collections;
using UnityEngine;

/// <summary>A single reversible UI sheet turn, shared by every library book.</summary>
public sealed class StudyBookPageTurn : MonoBehaviour
{
    [SerializeField, Range(.25f, 1.2f)] float duration = .6f;
    RectTransform sheet;
    GameObject stationary;
    UnityEngine.UI.Image paper;
    Coroutine routine;
    Action finished;
    public bool IsTurning { get; private set; }

    public void Play(RectTransform left, RectTransform right, bool forward, Action changeSpread, Action afterTurn)
    {
        if (IsTurning) return;
        IsTurning = true; finished = afterTurn;
        // Keep the old stationary page until the incoming sheet covers it.
        // Clones retain text, native diagrams and source images without rendering cameras.
        stationary = Copy(forward ? left : right).gameObject;
        SetSheet(forward ? right : left, forward);
        changeSpread();
        routine = StartCoroutine(Turn(left, right, forward));
    }

    static RectTransform Copy(RectTransform source)
    {
        var copy = Instantiate(source.gameObject, source.parent, false);
        copy.name = "Page Turn Preview";
        foreach (var graphic in copy.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
        foreach (var button in copy.GetComponentsInChildren<UnityEngine.UI.Selectable>(true)) button.enabled = false;
        return (RectTransform)copy.transform;
    }

    void SetSheet(RectTransform source, bool extendsRight)
    {
        if (sheet != null) { sheet.gameObject.SetActive(false); Destroy(sheet.gameObject); }
        sheet = Copy(source);
        sheet.anchorMin = sheet.anchorMax = new Vector2(.5f, .5f);
        sheet.pivot = new Vector2(extendsRight ? 0 : 1, .5f);
        sheet.anchoredPosition = Vector2.zero;
        sheet.sizeDelta = source.rect.size;
        paper = sheet.GetComponent<UnityEngine.UI.Image>();
        sheet.SetAsLastSibling();
    }

    IEnumerator Turn(RectTransform left, RectTransform right, bool forward)
    {
        bool backFace = false;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
            if (progress >= .5f && !backFace)
            { backFace = true; SetSheet(forward ? left : right, !forward); }
            float angle = backFace ? (progress - 1) * 180 : progress * 180;
            sheet.localRotation = Quaternion.Euler(0, forward ? angle : -angle,
                (forward ? -1 : 1) * Mathf.Sin(progress * Mathf.PI) * 1.5f);
            float shade = 1 - .24f * Mathf.Sin(progress * Mathf.PI);
            paper.color = new Color(.94f * shade, .87f * shade, .70f * shade, 1);
            yield return null;
        }
        routine = null;
        Clear();
    }

    public void Cancel()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null; Clear();
    }

    void Clear()
    {
        if (sheet != null) { sheet.gameObject.SetActive(false); Destroy(sheet.gameObject); }
        if (stationary != null) { stationary.SetActive(false); Destroy(stationary); }
        sheet = null; stationary = null; IsTurning = false;
        var callback = finished; finished = null; callback?.Invoke();
    }
    void OnDisable() { Cancel(); }
}
