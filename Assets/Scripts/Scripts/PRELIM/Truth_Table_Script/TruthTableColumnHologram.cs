using UnityEngine;

/// <summary>Presentation only. The puzzle still owns barrier materials, visibility and collision.</summary>
[DisallowMultipleComponent]
public sealed class TruthTableColumnHologram : MonoBehaviour
{
    public MeshRenderer veil, outline, symbols;
    public float phase;
    MaterialPropertyBlock properties;
    Material previous;
    [SerializeField] bool feedbackVisible;
    public bool FeedbackVisible => feedbackVisible;
    public void SetFeedbackVisible(bool visible)
    {
        feedbackVisible=visible;
        if(veil!=null)veil.enabled=visible;
        if(outline!=null)outline.enabled=visible;
        if(symbols!=null)symbols.enabled=visible;
    }
    static readonly int Clock=Shader.PropertyToID("_Clock");
    static readonly int[] Colors={Shader.PropertyToID("_DarkRed"),Shader.PropertyToID("_Crimson"),Shader.PropertyToID("_Scarlet"),Shader.PropertyToID("_RuneHighlights")};
    void OnEnable(){if(!Application.isPlaying)return;previous=null;SetFeedbackVisible(feedbackVisible);if(feedbackVisible)ApplyVisualTime(Time.unscaledTime);}
    void LateUpdate(){if(feedbackVisible)ApplyVisualTime(Time.unscaledTime);}
    void OnDisable(){if(Application.isPlaying&&symbols!=null)symbols.transform.localPosition=Vector3.zero;}
    public void ApplyVisualTime(float time)
    {
        if(veil==null||veil.sharedMaterial==null)return;
        if(properties==null){properties=new MaterialPropertyBlock();previous=null;}
        var material=veil.sharedMaterial;
        if(previous!=material)
        {
            previous=material;
            foreach(int id in Colors)properties.SetColor(id,material.GetColor(id));
        }
        properties.SetFloat(Clock,time+phase);
        veil.SetPropertyBlock(properties);
        if(outline!=null)outline.SetPropertyBlock(properties);
        if(symbols!=null)
        {
            symbols.SetPropertyBlock(properties);
            // Less than a tenth of a metre of drift; the answer cells remain clear.
            symbols.transform.localPosition=new Vector3(0,Mathf.Sin(time*.45f+phase)*.008f,0);
        }
    }
}
